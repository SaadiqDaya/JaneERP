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
            }
            catch (Exception ex)
            {
                AppLogger.Audit("system", "DbEnsureCreatedFailed", ex.ToString());
            }

            // ── Step 3: SQL Server schema against the selected company database ────────
            // Order matters: Users → Stores → Shopify → Locations → ProductTypes → Parts → Manufacturing → Tasks
            SchemaStep("Users",         () => { var r = new UserRepository(); r.EnsureSchema(); r.MigrateUserColumns(); r.MigrateLockout(); });
            SchemaStep("Stores",        () => new StoreRepository().EnsureSchema());
            SchemaStep("Shopify",       () => new ShopifySyncService().EnsureSchema());
            SchemaStep("Locations",     () => { var r = new LocationRepository(); r.EnsureSchema(); r.SeedDefaultLocations(); r.EnsureBinsSchema(); });
            SchemaStep("Vendors",       () => new VendorRepository().EnsureSchema());
            SchemaStep("ProductTypes",  () => new ProductTypeRepository().EnsureSchema());
            SchemaStep("Parts",         () => new PartRepository().EnsureSchema());
            SchemaStep("Suppliers",     () => new SupplierRepository().EnsureSchema());
            SchemaStep("Products",      () => new ProductRepository().MigrateProductColumns());
            SchemaStep("Manufacturing", () => new ManufacturingRepository().EnsureSchema());
            SchemaStep("Tasks",         () => new TaskRepository().EnsureSchema());
            SchemaStep("CycleCount",    () => new CycleCountRepository().EnsureSchema());
            SchemaStep("PackageComponents", () => new PackageRepository().EnsureSchema());
            SchemaStep("DiscountTiers", () => { var r = new DiscountTierRepository(); r.EnsureSchema(); r.MigrateCustomerTier(); r.MigrateOrderDiscount(); });

            // ── Step 4: One-time data migrations ─────────────────────────────────
            SchemaStep("SyncProductParts", () => DataMigrations.SyncProductsToParts());

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
