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
        /// Creates performance indexes on the most-queried tables.
        /// Safe to call on any database state — each CREATE INDEX is guarded by an existence check.
        /// Only runs once — tracked by AppliedMigrations.
        /// </summary>
        public static void AddPerformanceIndexes() => RunOnce("AddPerformanceIndexes_v1", () =>
        {
            using var db = new SqlConnection(ConnStr);

            // Helper: attempt an index creation; log and continue on any error so one bad
            // table/column name doesn't block the rest of the indexes from being created.
            void TryIndex(string sql)
            {
                try { db.Execute(sql); }
                catch (Exception ex)
                {
                    Logging.AppLogger.Info($"[Indexes] skipped: {ex.Message}");
                }
            }

            // InventoryTransactions — the most-queried table (stock lookups, reports, cycle counts)
            TryIndex(@"IF NOT EXISTS (SELECT 1 FROM sys.indexes
                            WHERE name = 'IX_InvTrans_ProductID_LocationID'
                              AND object_id = OBJECT_ID('InventoryTransactions'))
                        CREATE INDEX IX_InvTrans_ProductID_LocationID
                            ON InventoryTransactions (ProductID, LocationID)
                            INCLUDE (QuantityChange, TransactionType, TransactionDate);");

            TryIndex(@"IF NOT EXISTS (SELECT 1 FROM sys.indexes
                            WHERE name = 'IX_InvTrans_TransactionDate'
                              AND object_id = OBJECT_ID('InventoryTransactions'))
                        CREATE INDEX IX_InvTrans_TransactionDate
                            ON InventoryTransactions (TransactionDate DESC)
                            INCLUDE (ProductID, LocationID, QuantityChange, TransactionType);");

            TryIndex(@"IF NOT EXISTS (SELECT 1 FROM sys.indexes
                            WHERE name = 'IX_InvTrans_TransactionType'
                              AND object_id = OBJECT_ID('InventoryTransactions'))
                        CREATE INDEX IX_InvTrans_TransactionType
                            ON InventoryTransactions (TransactionType)
                            INCLUDE (ProductID, QuantityChange, TransactionDate);");

            // StockReservations — reservation lookups per product
            TryIndex(@"IF NOT EXISTS (SELECT 1 FROM sys.indexes
                            WHERE name = 'IX_StockReservations_ProductID'
                              AND object_id = OBJECT_ID('StockReservations'))
                        CREATE INDEX IX_StockReservations_ProductID
                            ON StockReservations (ProductID)
                            INCLUDE (Quantity);");

            // Products — filtered to active; covering SKU + ProductName avoids key lookups
            TryIndex(@"IF NOT EXISTS (SELECT 1 FROM sys.indexes
                            WHERE name = 'IX_Products_IsActive_SKU_Name'
                              AND object_id = OBJECT_ID('Products'))
                        CREATE INDEX IX_Products_IsActive_SKU_Name
                            ON Products (IsActive)
                            INCLUDE (SKU, ProductName, ReorderPoint, LastVerifiedAt);");

            // Products — cycle-count overdue badge query
            TryIndex(@"IF NOT EXISTS (SELECT 1 FROM sys.indexes
                            WHERE name = 'IX_Products_LastVerifiedAt'
                              AND object_id = OBJECT_ID('Products'))
                        CREATE INDEX IX_Products_LastVerifiedAt
                            ON Products (LastVerifiedAt)
                            WHERE IsActive = 1;");

            // SalesOrders — dashboard and customer order lookups
            TryIndex(@"IF NOT EXISTS (SELECT 1 FROM sys.indexes
                            WHERE name = 'IX_SalesOrders_Status'
                              AND object_id = OBJECT_ID('SalesOrders'))
                        CREATE INDEX IX_SalesOrders_Status
                            ON SalesOrders (Status)
                            INCLUDE (CustomerID, OrderDate, TotalAmount);");

            TryIndex(@"IF NOT EXISTS (SELECT 1 FROM sys.indexes
                            WHERE name = 'IX_SalesOrders_CustomerID_OrderDate'
                              AND object_id = OBJECT_ID('SalesOrders'))
                        CREATE INDEX IX_SalesOrders_CustomerID_OrderDate
                            ON SalesOrders (CustomerID, OrderDate DESC);");

            // WorkOrders — manufacturing dashboard queries
            TryIndex(@"IF NOT EXISTS (SELECT 1 FROM sys.indexes
                            WHERE name = 'IX_WorkOrders_ProductID_Status'
                              AND object_id = OBJECT_ID('WorkOrders'))
                        CREATE INDEX IX_WorkOrders_ProductID_Status
                            ON WorkOrders (ProductID, Status);");

            // ProductAttributes — attribute lookups by product
            TryIndex(@"IF NOT EXISTS (SELECT 1 FROM sys.indexes
                            WHERE name = 'IX_ProductAttributes_ProductID_Name'
                              AND object_id = OBJECT_ID('ProductAttributes'))
                        CREATE INDEX IX_ProductAttributes_ProductID_Name
                            ON ProductAttributes (ProductID, AttributeName);");
        });

        /// <summary>
        /// Adds a ROWVERSION column to Products for optimistic concurrency control.
        /// SQL Server auto-generates and auto-updates the value on every write — no app code needed.
        /// Only runs once — tracked by AppliedMigrations.
        /// </summary>
        public static void AddRowVersionToProducts() => RunOnce("AddRowVersionToProducts_v1", () =>
        {
            using var db = new SqlConnection(ConnStr);
            db.Execute(@"
                IF NOT EXISTS (SELECT 1 FROM sys.columns
                               WHERE  object_id = OBJECT_ID('Products') AND name = 'RowVersion')
                    ALTER TABLE Products ADD RowVersion ROWVERSION NOT NULL;");
        });

        /// <summary>
        /// Enables Read Committed Snapshot Isolation (RCSI) on the current database.
        /// RCSI lets readers never block writers and writers never block readers — critical for
        /// 5+ concurrent users. Safe to run even if already enabled; guarded by a sys.databases check.
        /// Only runs once — tracked by AppliedMigrations.
        /// </summary>
        public static void EnableReadCommittedSnapshot() => RunOnce("EnableReadCommittedSnapshot_v1", () =>
        {
            using var db = new SqlConnection(ConnStr);
            try
            {
                db.Execute(@"
                    IF (SELECT is_read_committed_snapshot_on
                        FROM   sys.databases
                        WHERE  name = DB_NAME()) = 0
                    ALTER DATABASE CURRENT
                        SET READ_COMMITTED_SNAPSHOT ON
                        WITH ROLLBACK IMMEDIATE;");
            }
            catch (Exception ex)
            {
                // RCSI requires no other active connections; log and continue rather than blocking startup.
                Logging.AppLogger.Info($"[RCSI] Could not enable Read Committed Snapshot: {ex.Message}");
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
