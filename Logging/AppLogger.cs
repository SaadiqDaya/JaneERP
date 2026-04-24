using System;
using System.IO;
using System.Threading;
using JaneERP.Data;

namespace JaneERP.Logging
{
    public static class AppLogger
    {
        private static readonly object _sync = new object();
        private static string LogFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "JaneERP", "logs");
        private static string LogFile => Path.Combine(LogFolder, "app.log");

        public static void Init()
        {
            try
            {
                Directory.CreateDirectory(LogFolder);
                RolloverIfNeeded();
                File.AppendAllText(LogFile, $"{DateTime.UtcNow:O} [Info] App starting{Environment.NewLine}");
            }
            catch
            {
                // swallow - logging is best-effort
            }
        }

        /// <summary>
        /// If app.log was last written on a previous day, renames it to app_YYYY-MM-DD.log
        /// and deletes any archived logs older than 30 days.
        /// </summary>
        private static void RolloverIfNeeded()
        {
            try
            {
                if (!File.Exists(LogFile)) return;

                var lastWrite = File.GetLastWriteTimeUtc(LogFile);
                if (lastWrite.Date >= DateTime.UtcNow.Date) return;

                // Archive the old log with the date it was last written
                var archive = Path.Combine(LogFolder, $"app_{lastWrite:yyyy-MM-dd}.log");
                if (!File.Exists(archive))
                    File.Move(LogFile, archive);

                // Purge archived logs older than 30 days
                foreach (var old in Directory.GetFiles(LogFolder, "app_????.??.??.log"))
                {
                    try
                    {
                        if (File.GetLastWriteTimeUtc(old) < DateTime.UtcNow.AddDays(-30))
                            File.Delete(old);
                    }
                    catch { /* skip files we can't delete */ }
                }
            }
            catch { /* best-effort */ }
        }

        public static void Shutdown()
        {
            try
            {
                File.AppendAllText(LogFile, $"{DateTime.UtcNow:O} [Info] App shutting down{Environment.NewLine}");
            }
            catch { }
        }

        public static void Info(string message)
        {
            Write("Info", message);
        }

        public static void Error(string message)
        {
            Write("Error", message);
        }

        private static void Write(string level, string message)
        {
            try
            {
                lock (_sync)
                {
                    File.AppendAllText(LogFile, $"{DateTime.UtcNow:O} [{level}] {message}{Environment.NewLine}");
                }
            }
            catch { /* best-effort */ }
        }

        // audit: logs to file and attempts to write an AuditLog entry into the DB (best-effort)
        public static void Audit(string? user, string action, string? details)
        {
            var u = string.IsNullOrEmpty(user) ? "unknown" : user;
            var d = details ?? "";
            Write("Audit", $"{u} {action} {d}");

            // try to write to DB AuditLogs table if AppDbContext exists and DB accessible
            try
            {
                using var db = new AppDbContext();
                db.AuditLogs.Add(new AuditLog
                {
                    When = DateTime.UtcNow,
                    User = u,
                    Action = action,
                    Details = d
                });
                db.SaveChanges();
            }
            catch (Exception ex)
            {
                // if DB write fails, log the DB error to file but do not throw
                Write("AuditError", ex.ToString());
            }
        }
    }
}
