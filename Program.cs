using System.Configuration;
using JaneERP.Data;
using JaneERP.Infrastructure;
using JaneERP.Interfaces;
using JaneERP.Logging;
using JaneERP.Manufacturing;
using JaneERP.Services;

namespace JaneERP
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            AppLogger.Init();
            Application.ThreadException += (s, e) =>
                AppLogger.Audit("system", "UnhandledException", e.Exception.ToString());

            ApplicationConfiguration.Initialize();

            // ── Apply custom colours before any form is shown ─────────────────────────
            Theme.ApplyCustomColors(AppSettings.Current);

            // ── Step 1: Company / database selection (must happen before any DB access) ──
            using (var selector = new FormCompanySelector())
            {
                if (selector.ShowDialog() != DialogResult.OK || !selector.Confirmed)
                {
                    AppLogger.Shutdown();
                    return; // User exited without selecting a database
                }
            }

            // ── Step 2: SQLite schema (orders cache — no company dependency) ──────────
            try
            {
                using var db = new AppDbContext();
                db.Database.EnsureCreated();
                db.MigrateSchema();
                db.PurgeOldOrders(90); // remove cached Shopify orders older than 90 days
            }
            catch (Exception ex)
            {
                AppLogger.Audit("system", "DbEnsureCreatedFailed", ex.ToString());
            }

            // ── Step 3: SQL Server schema against the selected company database ────────
            // Progress screen shows each of the 72 DDL steps so the user is never left
            // staring at a blank screen during first-run or after an update.
            var activeCs = JaneERP.Security.CompanyManager.ActiveConnectionString
                ?? ConfigurationManager.ConnectionStrings["MyERP"]?.ConnectionString
                ?? throw new InvalidOperationException("No active database connection.");

            FormSchemaProgress.RunWithProgress(activeCs);

            // ── One-time data migrations (tracked by AppliedMigrations table) ─────────
            SchemaStep("FgEjuiceType",          () => DataMigrations.SetFgProductTypeEjuice());
            SchemaStep("FgBoms",                () => DataMigrations.SetFgProductBoms());
            SchemaStep("ProductPartsMigration", () => DataMigrations.EnsureAllProductsHaveParts());

            // ── First-run wizard (only when the database has no users yet) ────────────
            // Lets the user create an admin account and configure currencies, labour
            // rates, and tax codes before they ever see the login screen.
            try
            {
                var userRepo = new UserRepository();
                if (!userRepo.HasAnyUsers())
                {
                    using var setup = new FormFirstRunSetup();
                    if (setup.ShowDialog() != DialogResult.OK)
                    {
                        // User closed the wizard without finishing — still boot normally,
                        // FormAppLogin will prompt for first-run account creation.
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.Audit("system", "FirstRunWizardFailed", ex.ToString());
            }

            // ── Auto-backup (runs after schema is ready, before login) ─────────────
            if (BackupService.IsBackupDue())
            {
                try
                {
                    BackupService.Backup(AppSettings.Current.BackupFolder);
                    AppLogger.Audit("system", "AutoBackup", "Scheduled backup completed");
                }
                catch (Exception ex)
                {
                    AppLogger.Audit("system", "AutoBackupFailed", ex.Message);
                    // Don't block startup on a backup failure — just log it
                }
            }

            // ── Overdue PO notifications (fire-and-forget, each PO only notified once) ──
            _ = Task.Run(async () =>
            {
                try
                {
                    var repo   = new SupplierRepository();
                    var overdue = repo.GetUnnotifiedOverduePOs();
                    foreach (var po in overdue)
                    {
                        bool sent = await NotificationService.NotifyOverduePOAsync(
                            po.PONumber, po.SupplierName ?? "Unknown", po.ExpectedDate!.Value);
                        if (sent) repo.MarkOverdueNotified(po.POID);
                    }
                }
                catch (Exception ex)
                {
                    AppLogger.Audit("system", "OverduePONotifyFailed", ex.Message);
                }
            });

            // ── Build DI container (after schema, before login) ───────────────────────
            ServiceRegistration.Build();

            // ── Overdue task notifications (fire-and-forget, once per calendar day) ──
            _ = Task.Run(async () =>
            {
                try
                {
                    var checkFile = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "JaneERP", "last_overdue_check.txt");
                    var lastCheck = DateTime.MinValue;
                    if (File.Exists(checkFile))
                        DateTime.TryParse(File.ReadAllText(checkFile).Trim(), out lastCheck);

                    if (lastCheck.Date < DateTime.Today)
                    {
                        var taskRepo = AppServices.Get<ITaskRepository>();
                        await NotificationService.SendOverdueTaskNotificationsAsync(taskRepo);

                        Directory.CreateDirectory(Path.GetDirectoryName(checkFile)!);
                        File.WriteAllText(checkFile, DateTime.Now.ToString("o"));
                    }
                }
                catch (Exception ex)
                {
                    AppLogger.Audit("system", "OverdueTaskNotifyFailed", ex.Message);
                }
            });

            Application.AddMessageFilter(new JaneERP.Security.GlobalActivityFilter());
            Application.Run(new FormAppLogin());

            AppLogger.Shutdown();
        }

        private static void SchemaStep(string name, Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                AppLogger.Audit("system", $"SchemaFailed_{name}", ex.ToString());
                MessageBox.Show(
                    $"Failed to initialise the '{name}' database schema:\n\n{ex.Message}\n\n" +
                    "Some features may not work correctly. Check the log for details.",
                    "Database Setup Warning",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }
    }
}
