using JaneERP.Data;
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
            // Order matters — Products must exist before anything that FKs to it (Shopify, Parts, Manufacturing).
            // LocationRepository and MigrateProductColumns add FK columns to Products/InventoryTransactions
            // via ALTER TABLE once their referenced tables (Locations, Vendors, ProductTypes) are ready.
            SchemaStep("Users",          () => { var r = new UserRepository(); r.EnsureSchema(); r.MigrateUserColumns(); r.MigrateLockout(); });
            SchemaStep("Stores",         () => new StoreRepository().EnsureSchema());
            SchemaStep("Products",       () => new ProductRepository().EnsureSchema());          // creates Products, InventoryTransactions, ProductAttributes
            SchemaStep("Shopify",        () => new ShopifySyncService().EnsureSchema());         // creates Customers, SalesOrders, SalesOrderItems (FK to Products)
            SchemaStep("Locations",      () => { var r = new LocationRepository(); r.EnsureSchema(); r.SeedDefaultLocations(); r.EnsureBinsSchema(); }); // adds FK cols to Products/InventoryTransactions
            SchemaStep("Vendors",        () => new VendorRepository().EnsureSchema());
            SchemaStep("ProductTypes",   () => new ProductTypeRepository().EnsureSchema());
            SchemaStep("Parts",          () => new PartRepository().EnsureSchema());             // creates Parts, ProductParts (FK to Products)
            SchemaStep("Suppliers",      () => new SupplierRepository().EnsureSchema());
            SchemaStep("ProductMigrate", () => new ProductRepository().MigrateProductColumns()); // adds DefaultVendorID, ProductTypeID FK cols (Vendors/ProductTypes now exist)
            SchemaStep("Manufacturing",  () => new ManufacturingRepository().EnsureSchema());    // creates ManufacturingOrders, WorkOrders (FK to Products)
            SchemaStep("Tasks",         () => new TaskRepository().EnsureSchema());
            SchemaStep("CycleCount",    () => new CycleCountRepository().EnsureSchema());
            SchemaStep("PackageComponents", () => new PackageRepository().EnsureSchema());
            SchemaStep("DiscountTiers",  () => { var r = new DiscountTierRepository(); r.EnsureSchema(); r.MigrateCustomerTier(); r.MigrateOrderDiscount(); });
            SchemaStep("PONotifiedAt",   () => new SupplierRepository().MigrateOverdueNotifiedColumn());

            // ── Migration version table (must run before any RunOnce migrations) ──
            SchemaStep("MigrationTable", () => DataMigrations.EnsureMigrationTable());

            // ── One-time data migrations — each runs exactly once, tracked by AppliedMigrations ──
            SchemaStep("FgEjuiceType",        () => DataMigrations.SetFgProductTypeEjuice());
            SchemaStep("FgBoms",              () => DataMigrations.SetFgProductBoms());
            SchemaStep("ProductPartsMigration", () => DataMigrations.EnsureAllProductsHaveParts());

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
