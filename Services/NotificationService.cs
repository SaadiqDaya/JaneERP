using System.Net;
using System.Net.Mail;
using JaneERP.Interfaces;
using JaneERP.Logging;

namespace JaneERP.Services
{
    /// <summary>
    /// Sends email notifications via the SMTP settings configured in AppSettings.
    /// All send methods are fire-and-forget async — failures are logged, not thrown.
    /// </summary>
    public static class NotificationService
    {
        /// <summary>
        /// Sends a plain-text email. Returns true if sent successfully.
        /// Silently logs and returns false if SMTP is not configured or send fails.
        /// </summary>
        public static async Task<bool> SendEmailAsync(string to, string subject, string body)
        {
            var cfg = AppSettings.Current;
            if (!cfg.IsEmailConfigured)
            {
                AppLogger.Info("[NotificationService] SMTP not configured — skipping email.");
                return false;
            }

            try
            {
                using var client = new SmtpClient(cfg.SmtpServer, cfg.SmtpPort)
                {
                    EnableSsl   = cfg.SmtpUseSsl,
                    Credentials = new NetworkCredential(cfg.SmtpUser, cfg.SmtpPasswordPlain)
                };

                var msg = new MailMessage
                {
                    From    = new MailAddress(cfg.FromEmail, cfg.FromName),
                    Subject = subject,
                    Body    = body,
                    IsBodyHtml = false
                };
                msg.To.Add(to);

                await client.SendMailAsync(msg);
                AppLogger.Info($"[NotificationService] Email sent to {to} — Subject: {subject}");
                return true;
            }
            catch (Exception ex)
            {
                AppLogger.Info($"[NotificationService] Send failed to {to}: {ex.Message}");
                return false;
            }
        }

        // ── Pre-built notification helpers ────────────────────────────────────────

        /// <summary>Notifies an admin that a user account has been locked out.</summary>
        public static Task<bool> NotifyUserLockedAsync(string lockedUsername, DateTime lockedUntil)
        {
            var cfg = AppSettings.Current;
            var adminEmail = cfg.AdminEmail;
            if (string.IsNullOrWhiteSpace(adminEmail)) return Task.FromResult(false);

            return SendEmailAsync(
                adminEmail,
                $"[JaneERP] Account locked: {lockedUsername}",
                $"The account '{lockedUsername}' has been locked due to too many failed login attempts.\n\n" +
                $"Locked until: {lockedUntil:yyyy-MM-dd h:mm tt}\n\n" +
                $"Log in to JaneERP as an admin and go to Manage Users to unlock the account.");
        }

        /// <summary>Notifies a user that they have been @mentioned in a task.</summary>
        public static Task<bool> NotifyMentionAsync(string toEmail, string mentionedBy, string taskTitle)
        {
            return SendEmailAsync(
                toEmail,
                $"[JaneERP] You were mentioned in a task",
                $"{mentionedBy} mentioned you in the task: \"{taskTitle}\"\n\n" +
                $"Open JaneERP and go to Tasks to view the full message.");
        }

        /// <summary>Notifies a user that they have been @mentioned in a task, including the comment text.</summary>
        public static Task<bool> NotifyMentionAsync(string toEmail, string mentionedBy, string taskTitle, string commentText)
        {
            return SendEmailAsync(
                toEmail,
                $"JaneERP — {mentionedBy} mentioned you in a task",
                $"Hi,\n\n{mentionedBy} mentioned you in task \"{taskTitle}\":\n\n\"{commentText}\"\n\n" +
                $"Log in to JaneERP to view and respond.\n\nJaneERP Task System");
        }

        /// <summary>
        /// Groups overdue tasks by assignee and sends one summary email per person.
        /// Only runs when SMTP is configured; silently skips users with no email address.
        /// </summary>
        public static async Task SendOverdueTaskNotificationsAsync(ITaskRepository taskRepo)
        {
            try
            {
                var overdue = taskRepo.GetOverdueTasks();
                if (!overdue.Any()) return;

                // Collect all unique assignees and resolve their emails in one query
                var assignees   = overdue.Select(t => t.AssignedTo).Distinct().ToList();
                var emailLookup = taskRepo.GetUserEmails(assignees)
                                         .ToDictionary(r => r.Username, r => r.Email,
                                                       StringComparer.OrdinalIgnoreCase);

                // Group by assignee and send one digest per person
                var byAssignee = overdue.GroupBy(t => t.AssignedTo);

                foreach (var group in byAssignee)
                {
                    if (!emailLookup.TryGetValue(group.Key, out var email) ||
                        string.IsNullOrWhiteSpace(email))
                        continue;

                    var taskList = string.Join("\n", group.Select(t =>
                        $"  \u2022 {t.Title} (due {t.DueDate:yyyy-MM-dd}, Priority: {t.Priority})"));

                    await SendEmailAsync(
                        to:      email,
                        subject: $"JaneERP \u2014 You have {group.Count()} overdue task(s)",
                        body:    $"Hi {group.Key},\n\nThe following tasks are overdue:\n\n{taskList}\n\n" +
                                 $"Please log in to JaneERP to update these tasks.\n\nJaneERP Task System");
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error($"[SendOverdueTaskNotifications] {ex}");
            }
        }

        /// <summary>Notifies an admin that a product has fallen below its reorder point.</summary>
        public static Task<bool> NotifyLowStockAsync(string sku, string productName, int currentStock, int reorderPoint)
        {
            var cfg = AppSettings.Current;
            var adminEmail = cfg.AdminEmail;
            if (string.IsNullOrWhiteSpace(adminEmail)) return Task.FromResult(false);

            return SendEmailAsync(
                adminEmail,
                $"[JaneERP] Low stock alert: {sku}",
                $"Product \"{productName}\" ({sku}) has fallen below its reorder point.\n\n" +
                $"Current stock: {currentStock}\n" +
                $"Reorder point: {reorderPoint}\n\n" +
                $"Consider creating a Purchase Order to replenish stock.");
        }

        /// <summary>Notifies an admin that a purchase order is overdue.</summary>
        public static Task<bool> NotifyOverduePOAsync(string poNumber, string supplierName, DateTime expectedDate)
        {
            var cfg = AppSettings.Current;
            var adminEmail = cfg.AdminEmail;
            if (string.IsNullOrWhiteSpace(adminEmail)) return Task.FromResult(false);

            return SendEmailAsync(
                adminEmail,
                $"[JaneERP] Overdue PO: {poNumber}",
                $"Purchase Order {poNumber} from {supplierName} was expected on " +
                $"{expectedDate:yyyy-MM-dd} and has not been marked as received.\n\n" +
                $"Open JaneERP and go to Purchase Orders to update its status.");
        }

        /// <summary>Notifies a user that a task has been assigned to them.</summary>
        public static Task<bool> NotifyTaskAssignedAsync(string toEmail, string assignedBy, string taskTitle)
        {
            return SendEmailAsync(
                toEmail,
                $"[JaneERP] New task assigned to you",
                $"{assignedBy} assigned you a task: \"{taskTitle}\"\n\n" +
                $"Open JaneERP and go to Tasks to view the details.");
        }
    }
}
