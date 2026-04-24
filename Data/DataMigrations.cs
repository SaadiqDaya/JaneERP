using System.Configuration;
using Dapper;
using Microsoft.Data.SqlClient;

namespace JaneERP.Data
{
    /// <summary>
    /// One-time data migrations tracked by the AppliedMigrations table.
    /// Each migration runs exactly once per database — the version table prevents re-runs.
    /// </summary>
    internal static class DataMigrations
    {
        private static string ConnStr =>
            ConfigurationManager.ConnectionStrings["MyERP"]?.ConnectionString
            ?? throw new InvalidOperationException("Connection string 'MyERP' not found.");

        /// <summary>
        /// Creates the AppliedMigrations tracking table if it doesn't exist.
        /// Call this before any migration checks.
        /// </summary>
        public static void EnsureMigrationTable()
        {
            using var db = new SqlConnection(ConnStr);
            db.Execute(@"
                IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='AppliedMigrations' AND xtype='U')
                CREATE TABLE AppliedMigrations (
                    MigrationName NVARCHAR(200) NOT NULL PRIMARY KEY,
                    AppliedAt     DATETIME      NOT NULL DEFAULT GETDATE()
                );");
        }

        /// <summary>Returns true if a migration with this name has already been applied.</summary>
        private static bool IsApplied(string name)
        {
            using var db = new SqlConnection(ConnStr);
            return db.ExecuteScalar<int>(
                "SELECT COUNT(1) FROM AppliedMigrations WHERE MigrationName = @name",
                new { name }) > 0;
        }

        /// <summary>Records that a migration has been applied so it won't run again.</summary>
        private static void MarkApplied(string name)
        {
            using var db = new SqlConnection(ConnStr);
            db.Execute(
                "INSERT INTO AppliedMigrations (MigrationName) VALUES (@name)",
                new { name });
        }

        /// <summary>
        /// Runs the action only if the migration has not been applied before.
        /// </summary>
        public static void RunOnce(string migrationName, Action action)
        {
            if (IsApplied(migrationName)) return;
            action();
            MarkApplied(migrationName);
        }

        /// <summary>
        /// One-time cleanup: sets ProductTypeID = eJuice on every product whose SKU starts with "FG".
        /// Creates the eJuice ProductType if it doesn't exist.
        /// Only runs once — tracked by AppliedMigrations.
        /// </summary>
        public static void SetFgProductTypeEjuice() => RunOnce("FgProductTypeEjuice", () =>
        {
            using var db = new SqlConnection(ConnStr);
            db.Open();

            // Ensure eJuice type exists
            int ejuiceTypeId = db.QueryFirstOrDefault<int?>(
                "SELECT ProductTypeID FROM ProductTypes WHERE TypeName = 'eJuice'") ?? 0;
            if (ejuiceTypeId == 0)
            {
                ejuiceTypeId = db.QuerySingle<int>(@"
                    INSERT INTO ProductTypes (TypeName) VALUES ('eJuice');
                    SELECT CAST(SCOPE_IDENTITY() AS INT);");
            }

            // Assign to all FG products that don't already have it
            db.Execute(@"
                UPDATE Products
                SET ProductTypeID = @ejuiceTypeId
                WHERE SKU LIKE 'FG%'
                  AND IsActive = 1
                  AND (ProductTypeID IS NULL OR ProductTypeID <> @ejuiceTypeId)",
                new { ejuiceTypeId });
        });

        /// <summary>
        /// One-time migration: for every active Product that has no matching Part (PartNumber = SKU),
        /// creates the Part and links it in the BOM (ProductParts).
        /// Only runs once — tracked by AppliedMigrations.
        /// </summary>
        public static void EnsureAllProductsHaveParts() => RunOnce("EnsureAllProductsHaveParts", () =>
        {
            using var db = new SqlConnection(ConnStr);
            db.Open();

            // Find active products with no BOM entry at all
            var orphans = db.Query<(int ProductID, string SKU, string ProductName)>(@"
                SELECT p.ProductID, p.SKU, p.ProductName
                FROM   Products p
                WHERE  p.IsActive = 1
                  AND  NOT EXISTS (SELECT 1 FROM ProductParts pp WHERE pp.ProductID = p.ProductID)")
                .ToList();

            foreach (var (productId, sku, productName) in orphans)
            {
                using var tx = db.BeginTransaction();
                try
                {
                    int? partId = db.ExecuteScalar<int?>(
                        "SELECT PartID FROM Parts WHERE PartNumber = @sku", new { sku }, tx);
                    if (partId == null)
                    {
                        partId = db.QuerySingle<int>(@"
                            INSERT INTO Parts (PartNumber, PartName, UnitCost, CurrentStock, IsActive, IsAutoCreated, IsVerified)
                            VALUES (@sku, @productName, 0, 0, 1, 1, 0);
                            SELECT CAST(SCOPE_IDENTITY() AS INT);",
                            new { sku, productName }, tx);
                    }
                    db.Execute(@"
                        IF NOT EXISTS (SELECT 1 FROM ProductParts WHERE ProductID=@productId AND PartID=@partId)
                        INSERT INTO ProductParts (ProductID, PartID, Quantity) VALUES (@productId, @partId, 1);",
                        new { productId, partId }, tx);
                    tx.Commit();
                }
                catch { tx.Rollback(); /* log and continue */ }
            }
        });

        /// <summary>
        /// One-time cleanup: for every product whose SKU starts with "FG", creates a BOM containing:
        ///   - The corresponding Part (PartNumber = SKU)
        ///   - A "Bottle" part at $0.50
        /// Only applies to products that currently have an empty BOM.
        /// Only runs once — tracked by AppliedMigrations.
        /// </summary>
        public static void SetFgProductBoms() => RunOnce("FgProductBoms", () =>
        {
            using var db = new SqlConnection(ConnStr);
            db.Open();

            // Ensure Bottle part exists
            int bottleId = db.QueryFirstOrDefault<int?>(
                "SELECT PartID FROM Parts WHERE PartNumber = 'BOTTLE'") ?? 0;
            if (bottleId == 0)
            {
                bottleId = db.QuerySingle<int>(@"
                    INSERT INTO Parts (PartNumber, PartName, Description, UnitCost, CurrentStock, IsActive)
                    VALUES ('BOTTLE', 'Bottle', 'Standard bottle component', 0.50, 0, 1);
                    SELECT CAST(SCOPE_IDENTITY() AS INT);");
            }

            var fgProducts = db.Query<(int ProductID, string SKU, string ProductName)>(
                "SELECT ProductID, SKU, ProductName FROM Products WHERE SKU LIKE 'FG%' AND IsActive = 1")
                .ToList();

            foreach (var (productId, sku, productName) in fgProducts)
            {
                // Skip products that already have a BOM
                int bomCount = db.QuerySingle<int>(
                    "SELECT COUNT(*) FROM ProductParts WHERE ProductID = @productId", new { productId });
                if (bomCount > 0) continue;

                using var tx = db.BeginTransaction();
                try
                {
                    // Ensure corresponding Part exists
                    int? partId = db.ExecuteScalar<int?>(
                        "SELECT PartID FROM Parts WHERE PartNumber = @sku", new { sku }, tx);
                    if (partId == null)
                    {
                        partId = db.QuerySingle<int>(@"
                            INSERT INTO Parts (PartNumber, PartName, UnitCost, CurrentStock, IsActive)
                            VALUES (@sku, @productName, 0, 0, 1);
                            SELECT CAST(SCOPE_IDENTITY() AS INT);",
                            new { sku, productName }, tx);
                    }

                    // Set BOM: corresponding part + bottle
                    db.Execute(@"
                        INSERT INTO ProductParts (ProductID, PartID, Quantity) VALUES (@productId, @partId, 1);
                        INSERT INTO ProductParts (ProductID, PartID, Quantity) VALUES (@productId, @bottleId, 1);",
                        new { productId, partId, bottleId }, tx);

                    tx.Commit();
                }
                catch { tx.Rollback(); throw; }
            }
        });
    }
}
