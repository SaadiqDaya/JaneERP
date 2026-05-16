using System.Configuration;
using System.Data;
using Dapper;
using JaneERP.Interfaces;
using JaneERP.Models;
using Microsoft.Data.SqlClient;

namespace JaneERP.Data
{
    public class ProductRepository : IProductRepository
    {
        private readonly string _connectionString =
            ConfigurationManager.ConnectionStrings["MyERP"]?.ConnectionString
            ?? throw new InvalidOperationException(
                "Connection string 'MyERP' not found in App.config.");

        /// <summary>
        /// Creates the Products, InventoryTransactions, and ProductAttributes tables if they do not
        /// already exist. Safe to call on every startup — all statements are IF NOT EXISTS guarded.
        /// Call this BEFORE LocationRepository, PartRepository, and ManufacturingRepository so that
        /// the tables they reference via FK already exist.
        /// </summary>
        public void EnsureSchema()
        {
            using IDbConnection db = new SqlConnection(_connectionString);

            // ── Products ─────────────────────────────────────────────────────────────
            // FK columns (DefaultLocationID, DefaultVendorID, ProductTypeID) are added later
            // by LocationRepository.EnsureSchema() and MigrateProductColumns() once the
            // referenced tables have been created.
            db.Execute(@"
                IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='Products' AND xtype='U')
                CREATE TABLE Products (
                    ProductID      INT IDENTITY(1,1) PRIMARY KEY,
                    SKU            NVARCHAR(100)  NOT NULL UNIQUE,
                    ProductName    NVARCHAR(200)  NOT NULL,
                    RetailPrice    DECIMAL(18,2)  NOT NULL DEFAULT 0,
                    WholesalePrice DECIMAL(18,2)  NOT NULL DEFAULT 0,
                    IsActive       BIT            NOT NULL DEFAULT 1,
                    ReorderPoint   INT            NOT NULL DEFAULT 0,
                    OrderUpTo      INT            NOT NULL DEFAULT 0,
                    IsAutoCreated  BIT            NOT NULL DEFAULT 0,
                    IsVerified     BIT            NOT NULL DEFAULT 0
                );");

            // ── InventoryTransactions ────────────────────────────────────────────────
            // LocationID, LotNumber, ExpirationDate, StoreID added by LocationRepository.EnsureSchema()
            db.Execute(@"
                IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='InventoryTransactions' AND xtype='U')
                CREATE TABLE InventoryTransactions (
                    TransactionID   INT IDENTITY(1,1) PRIMARY KEY,
                    ProductID       INT           NOT NULL REFERENCES Products(ProductID),
                    QuantityChange  INT           NOT NULL,
                    TransactionType NVARCHAR(50)  NOT NULL,
                    Notes           NVARCHAR(500) NULL,
                    TransactionDate DATETIME      NOT NULL DEFAULT GETDATE()
                );");

            // ── ProductAttributes ────────────────────────────────────────────────────
            db.Execute(@"
                IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='ProductAttributes' AND xtype='U')
                CREATE TABLE ProductAttributes (
                    AttributeID    INT IDENTITY(1,1) PRIMARY KEY,
                    ProductID      INT            NOT NULL REFERENCES Products(ProductID),
                    AttributeName  NVARCHAR(100)  NOT NULL,
                    AttributeValue NVARCHAR(500)  NULL
                );");
        }

        // CurrentStock is 100% calculated from InventoryTransactions.
        // Pass locationId to scope the stock to a single location; null = all locations combined.
        // Uses pre-aggregated derived-table JOINs instead of correlated subqueries so the stock
        // totals are computed once per query rather than once per product row (critical at 8k products).
        public IEnumerable<Product> GetProducts(bool showInactive = false, int? locationId = null)
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            string filter = showInactive ? "p.IsActive = 0" : "p.IsActive = 1";
            return db.Query<Product>($@"
                SELECT  p.ProductID,
                        p.SKU,
                        p.ProductName,
                        p.UnitOfMeasure,
                        p.RetailPrice,
                        p.WholesalePrice,
                        p.ReorderPoint,
                        ISNULL(p.OrderUpTo, 0) AS OrderUpTo,
                        p.IsActive,
                        p.DefaultLocationID,
                        p.ProductTypeID,
                        pt.TypeName     AS ProductTypeName,
                        l.LocationName  AS DefaultLocationName,
                        p.DefaultVendorID,
                        v.VendorName    AS DefaultVendorName,
                        p.BomNumber,
                        ISNULL(inv.CurrentStock, 0) AS CurrentStock,
                        ISNULL(res.ReservedQty,  0) AS ReservedQty,
                        ISNULL(so_open.SoQty,    0) AS SoQty,
                        ISNULL(mo_open.MoQty,    0) AS MoQty
                FROM    Products p
                LEFT JOIN ProductTypes pt ON pt.ProductTypeID = p.ProductTypeID
                LEFT JOIN Locations    l  ON l.LocationID     = p.DefaultLocationID
                LEFT JOIN Vendors      v  ON v.VendorID       = p.DefaultVendorID
                LEFT JOIN (
                    SELECT ProductID, SUM(QuantityChange) AS CurrentStock
                    FROM   InventoryTransactions
                    WHERE  @LocationID IS NULL OR LocationID = @LocationID
                    GROUP  BY ProductID
                ) inv ON inv.ProductID = p.ProductID
                LEFT JOIN (
                    SELECT ProductID, SUM(Quantity) AS ReservedQty
                    FROM   StockReservations
                    GROUP  BY ProductID
                ) res ON res.ProductID = p.ProductID
                LEFT JOIN (
                    SELECT soi.ProductID, SUM(soi.Quantity) AS SoQty
                    FROM   SalesOrderItems soi
                    JOIN   SalesOrders so ON so.SalesOrderID = soi.SalesOrderID
                    WHERE  so.Status NOT IN ('Shipped','Complete','Cancelled')
                    GROUP  BY soi.ProductID
                ) so_open ON so_open.ProductID = p.ProductID
                LEFT JOIN (
                    SELECT wo.ProductID, SUM(wo.Quantity) AS MoQty
                    FROM   WorkOrders wo
                    WHERE  wo.Status NOT IN ('Complete','Cancelled')
                    GROUP  BY wo.ProductID
                ) mo_open ON mo_open.ProductID = p.ProductID
                WHERE   {filter}",
                new { LocationID = locationId }).ToList();
        }

        /// <summary>Returns a single product by ID, or null if not found.</summary>
        public Product? GetProductById(int productId)
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            return db.QueryFirstOrDefault<Product>(@"
                SELECT  p.ProductID, p.SKU, p.ProductName, p.UnitOfMeasure,
                        p.RetailPrice, p.WholesalePrice,
                        p.ReorderPoint, ISNULL(p.OrderUpTo, 0) AS OrderUpTo,
                        p.IsActive, p.DefaultLocationID, p.ProductTypeID,
                        pt.TypeName    AS ProductTypeName,
                        l.LocationName AS DefaultLocationName,
                        p.DefaultVendorID,
                        v.VendorName   AS DefaultVendorName,
                        p.RowVersion,
                        ISNULL((SELECT SUM(t.QuantityChange) FROM InventoryTransactions t
                                WHERE t.ProductID = p.ProductID), 0) AS CurrentStock,
                        ISNULL((SELECT SUM(sr.Quantity) FROM StockReservations sr
                                WHERE sr.ProductID = p.ProductID), 0) AS ReservedQty
                FROM    Products p
                LEFT JOIN ProductTypes pt ON pt.ProductTypeID = p.ProductTypeID
                LEFT JOIN Locations    l  ON l.LocationID     = p.DefaultLocationID
                LEFT JOIN Vendors      v  ON v.VendorID       = p.DefaultVendorID
                WHERE   p.ProductID = @productId",
                new { productId });
        }

        /// <summary>Returns the count of products and parts that are auto-created and not yet verified.</summary>
        /// Used to drive the badge overlay on the main menu Unverified tile.</summary>
        public int GetUnverifiedCount()
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            try
            {
                return db.ExecuteScalar<int>(@"
                    SELECT COUNT(*) FROM (
                        SELECT ProductID FROM Products WHERE IsAutoCreated = 1 AND IsVerified = 0 AND IsActive = 1
                        UNION ALL
                        SELECT PartID    FROM Parts    WHERE IsAutoCreated = 1 AND IsVerified = 0 AND IsActive = 1
                    ) AS unverified");
            }
            catch (Exception ex) { Logging.AppLogger.Error($"[ProductRepository.GetUnverifiedCount] {ex}"); return 0; }
        }

        /// <summary>Adds new product columns (ReorderPoint, OrderUpTo, DefaultVendorID, IsAutoCreated, IsVerified) if they don't exist yet.</summary>
        public void MigrateProductColumns()
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            try
            {
                db.Execute(@"
                    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Products') AND name = 'ReorderPoint')
                        ALTER TABLE Products ADD ReorderPoint INT NOT NULL DEFAULT 0;
                    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Products') AND name = 'OrderUpTo')
                        ALTER TABLE Products ADD OrderUpTo INT NOT NULL DEFAULT 0;
                    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Products') AND name='DefaultVendorID')
                        ALTER TABLE Products ADD DefaultVendorID INT NULL REFERENCES Vendors(VendorID);
                    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Products') AND name='IsAutoCreated')
                        ALTER TABLE Products ADD IsAutoCreated BIT NOT NULL DEFAULT 0;
                    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Products') AND name='IsVerified')
                        ALTER TABLE Products ADD IsVerified BIT NOT NULL DEFAULT 0;
                    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Products') AND name='BomSourceID')
                        ALTER TABLE Products ADD BomSourceID INT NULL REFERENCES Products(ProductID);
                    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Products') AND name='BomNumber')
                        ALTER TABLE Products ADD BomNumber NVARCHAR(20) NULL;
                    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Products') AND name='UnitOfMeasure')
                        ALTER TABLE Products ADD UnitOfMeasure NVARCHAR(20) NULL;");
            }
            catch (Exception ex) { Logging.AppLogger.Info($"MigrateProductColumns warning: {ex.Message}"); }

            // One-time fix: assign any inventory transactions with NULL LocationID to Main Warehouse
            try
            {
                db.Execute(@"
                    DECLARE @mainId INT = (SELECT TOP 1 LocationID FROM Locations WHERE LocationName = 'Main Warehouse' ORDER BY LocationID);
                    IF @mainId IS NOT NULL
                        UPDATE InventoryTransactions SET LocationID = @mainId WHERE LocationID IS NULL;");
            }
            catch (Exception ex) { Logging.AppLogger.Info($"MigrateProductColumns location fix warning: {ex.Message}"); }
        }

        /// <summary>Kept for backwards compatibility — calls MigrateProductColumns().</summary>
        public void MigrateReorderPoint() => MigrateProductColumns();

        /// <summary>Returns all distinct attribute names ever used, sorted alphabetically.</summary>
        public IEnumerable<string> GetAllAttributeNames()
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            return db.Query<string>(
                "SELECT DISTINCT AttributeName FROM ProductAttributes ORDER BY AttributeName").ToList();
        }

        public IEnumerable<ProductAttribute> GetAttributes(int productId)
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            return db.Query<ProductAttribute>(
                "SELECT * FROM ProductAttributes WHERE ProductID = @productId",
                new { productId }).ToList();
        }

        /// <summary>Returns all ProductAttributes for a set of product IDs in a single query.</summary>
        public List<ProductAttribute> GetProductAttributes(IEnumerable<int> productIds)
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            var ids = productIds.ToList();
            if (!ids.Any()) return new List<ProductAttribute>();
            return db.Query<ProductAttribute>(
                "SELECT * FROM ProductAttributes WHERE ProductID IN @ids",
                new { ids }).ToList();
        }

        /// <summary>Returns all distinct attribute values for a given attribute name, sorted.</summary>
        public IEnumerable<string> GetDistinctAttributeValues(string attributeName)
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            return db.Query<string>(
                "SELECT DISTINCT AttributeValue FROM ProductAttributes WHERE AttributeName = @attributeName ORDER BY AttributeValue",
                new { attributeName }).ToList();
        }

        /// <param name="locationId">Limit results to a specific location; null returns all locations.</param>
        /// <summary>Returns stock level per location for a product.
        /// Returns a list of (LocationName, Stock) tuples only for locations that have transactions.</summary>
        public List<(string LocationName, int Stock)> GetStockByLocation(int productId)
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            var rows = db.Query(@"
                SELECT  ISNULL(l.LocationName, 'No Location') AS LocationName,
                        SUM(it.QuantityChange) AS Stock
                FROM    InventoryTransactions it
                LEFT JOIN Locations l ON l.LocationID = it.LocationID
                WHERE   it.ProductID = @productId
                GROUP   BY l.LocationName
                HAVING  SUM(it.QuantityChange) <> 0
                ORDER   BY LocationName",
                new { productId }).ToList();
            return rows.Select(r => (LocationName: (string)r.LocationName, Stock: (int)r.Stock)).ToList();
        }

        public IEnumerable<InventoryTransaction> GetTransactions(int productId, int? locationId = null)
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            return db.Query<InventoryTransaction>(@"
                SELECT  it.*,
                        l.LocationName,
                        s.StoreName
                FROM    InventoryTransactions it
                LEFT JOIN Locations l ON l.LocationID = it.LocationID
                LEFT JOIN Stores    s ON s.StoreID    = it.StoreID
                WHERE   it.ProductID  = @productId
                  AND   (@LocationID IS NULL OR it.LocationID = @LocationID)
                ORDER   BY it.TransactionDate DESC",
                new { productId, LocationID = locationId }).ToList();
        }

        public void AddTransaction(InventoryTransaction transaction)
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            db.Execute(@"
                INSERT INTO InventoryTransactions
                    (ProductID, QuantityChange, TransactionType, Notes, TransactionDate,
                     LocationID, LotNumber, ExpirationDate)
                VALUES
                    (@ProductID, @QuantityChange, @TransactionType, @Notes, @TransactionDate,
                     @LocationID, @LotNumber, @ExpirationDate)",
                transaction);
        }

        public void AddProduct(Product product)
        {
            using var db = new SqlConnection(_connectionString);
            db.Open();
            using var tx = db.BeginTransaction();
            try
            {
                // Enforce SKU uniqueness
                int skuExists = db.ExecuteScalar<int>(
                    "SELECT COUNT(*) FROM Products WHERE SKU = @SKU", new { product.SKU }, tx);
                if (skuExists > 0)
                    throw new InvalidOperationException($"A product with SKU '{product.SKU}' already exists. SKUs must be unique.");

                int newId = db.QuerySingle<int>(@"
                    INSERT INTO Products (SKU, ProductName, RetailPrice, WholesalePrice, IsActive, DefaultLocationID, ProductTypeID, ReorderPoint, OrderUpTo, DefaultVendorID, UnitOfMeasure)
                    VALUES (@SKU, @ProductName, @RetailPrice, @WholesalePrice, @IsActive, @DefaultLocationID, @ProductTypeID, @ReorderPoint, @OrderUpTo, @DefaultVendorID, @UnitOfMeasure);
                    SELECT CAST(SCOPE_IDENTITY() AS INT);", product, tx);

                // Opening stock becomes the very first ledger transaction
                if (product.CurrentStock > 0)
                {
                    db.Execute(@"
                        INSERT INTO InventoryTransactions
                            (ProductID, QuantityChange, TransactionType, Notes, TransactionDate, LocationID)
                        VALUES
                            (@ProductID, @QuantityChange, @TransactionType, @Notes, @TransactionDate, @LocationID)",
                        new
                        {
                            ProductID       = newId,
                            QuantityChange  = product.CurrentStock,
                            TransactionType = "Opening",
                            Notes           = "Opening stock",
                            TransactionDate = DateTime.Now,
                            LocationID      = product.DefaultLocationID
                        }, tx);
                }

                foreach (var attr in product.Attributes
                    .Where(a => !string.IsNullOrWhiteSpace(a.AttributeName)))
                {
                    db.Execute(@"
                        INSERT INTO ProductAttributes (ProductID, AttributeName, AttributeValue)
                        VALUES (@ProductID, @AttributeName, @AttributeValue)",
                        new { ProductID = newId, attr.AttributeName, attr.AttributeValue }, tx);
                }

                // Every product must have a Part and a BOM entry
                EnsurePartAndBom(db, tx, newId, product.SKU, product.ProductName);

                tx.Commit();
            }
            catch { tx.Rollback(); throw; }
        }

        /// <summary>
        /// Upsert a batch of products by SKU.
        /// If a product with the same SKU already exists it is updated (name, prices, attributes).
        /// New products are inserted with opening stock.
        /// Returns (inserted, updated) counts.
        /// </summary>
        public (int inserted, int updated) UpsertProducts(IEnumerable<Product> products)
        {
            int inserted = 0, updated = 0;

            using var db = new SqlConnection(_connectionString);
            db.Open();
            using var tx = db.BeginTransaction();
            try
            {
                foreach (var product in products)
                {
                    int? existingId = db.ExecuteScalar<int?>(
                        "SELECT ProductID FROM Products WHERE SKU = @SKU",
                        new { product.SKU }, tx);

                    if (existingId.HasValue)
                    {
                        // Update existing product — preserve stock, just refresh info
                        db.Execute(@"
                            UPDATE Products
                            SET ProductName    = @ProductName,
                                RetailPrice    = @RetailPrice,
                                WholesalePrice = @WholesalePrice
                            WHERE ProductID = @id",
                            new { product.ProductName, product.RetailPrice, product.WholesalePrice, id = existingId.Value }, tx);

                        // Replace attributes
                        db.Execute("DELETE FROM ProductAttributes WHERE ProductID = @id",
                            new { id = existingId.Value }, tx);
                        foreach (var attr in product.Attributes
                            .Where(a => !string.IsNullOrWhiteSpace(a.AttributeName)))
                        {
                            db.Execute(@"
                                INSERT INTO ProductAttributes (ProductID, AttributeName, AttributeValue)
                                VALUES (@ProductID, @AttributeName, @AttributeValue)",
                                new { ProductID = existingId.Value, attr.AttributeName, attr.AttributeValue }, tx);
                        }
                        updated++;
                    }
                    else
                    {
                        int newId = db.QuerySingle<int>(@"
                            INSERT INTO Products (SKU, ProductName, RetailPrice, WholesalePrice, IsActive, DefaultLocationID)
                            VALUES (@SKU, @ProductName, @RetailPrice, @WholesalePrice, @IsActive, @DefaultLocationID);
                            SELECT CAST(SCOPE_IDENTITY() AS INT);", product, tx);

                        if (product.CurrentStock > 0)
                        {
                            db.Execute(@"
                                INSERT INTO InventoryTransactions
                                    (ProductID, QuantityChange, TransactionType, Notes, TransactionDate, LocationID)
                                VALUES
                                    (@ProductID, @QuantityChange, @TransactionType, @Notes, @TransactionDate, @LocationID)",
                                new
                                {
                                    ProductID       = newId,
                                    QuantityChange  = product.CurrentStock,
                                    TransactionType = "Opening",
                                    Notes           = "Opening stock (CSV import)",
                                    TransactionDate = DateTime.Now,
                                    LocationID      = product.DefaultLocationID
                                }, tx);
                        }

                        foreach (var attr in product.Attributes
                            .Where(a => !string.IsNullOrWhiteSpace(a.AttributeName)))
                        {
                            db.Execute(@"
                                INSERT INTO ProductAttributes (ProductID, AttributeName, AttributeValue)
                                VALUES (@ProductID, @AttributeName, @AttributeValue)",
                                new { ProductID = newId, attr.AttributeName, attr.AttributeValue }, tx);
                        }

                        // Every product must have a Part and a BOM entry
                        EnsurePartAndBom(db, tx, newId, product.SKU, product.ProductName);

                        inserted++;
                    }
                }
                tx.Commit();
                return (inserted, updated);
            }
            catch { tx.Rollback(); throw; }
        }

        public void UpdateProduct(Product product, string updatedBy = "")
        {
            using var db = new SqlConnection(_connectionString);
            db.Open();
            using var tx = db.BeginTransaction();
            try
            {
                // Enforce SKU uniqueness (exclude self)
                int skuExists = db.ExecuteScalar<int>(
                    "SELECT COUNT(*) FROM Products WHERE SKU = @SKU AND ProductID != @ProductID",
                    new { product.SKU, product.ProductID }, tx);
                if (skuExists > 0)
                    throw new InvalidOperationException($"Another product already uses SKU '{product.SKU}'. SKUs must be unique.");

                // Optimistic concurrency: if a RowVersion was loaded with the product, include it in
                // the WHERE clause so the update fails (0 rows) if another user saved in the meantime.
                // When RowVersion is null (pre-migration rows or list-loaded products), skip the check.
                int affected = db.Execute(@"
                    UPDATE Products
                    SET SKU               = @SKU,
                        ProductName       = @ProductName,
                        RetailPrice       = @RetailPrice,
                        WholesalePrice    = @WholesalePrice,
                        DefaultLocationID = @DefaultLocationID,
                        ProductTypeID     = @ProductTypeID,
                        ReorderPoint      = @ReorderPoint,
                        OrderUpTo         = @OrderUpTo,
                        DefaultVendorID   = @DefaultVendorID,
                        UnitOfMeasure     = @UnitOfMeasure,
                        UpdatedBy         = @updatedBy,
                        UpdatedAt         = @now
                    WHERE ProductID   = @ProductID
                      AND (@RowVersion IS NULL OR RowVersion = @RowVersion)",
                    new
                    {
                        product.SKU, product.ProductName, product.RetailPrice,
                        product.WholesalePrice, product.DefaultLocationID, product.ProductTypeID,
                        product.ReorderPoint, product.OrderUpTo, product.DefaultVendorID,
                        product.ProductID, product.RowVersion,
                        updatedBy = string.IsNullOrEmpty(updatedBy) ? null : updatedBy,
                        now = DateTime.UtcNow
                    }, tx);

                if (affected == 0)
                    throw new Infrastructure.ConcurrencyException(
                        "This product was updated by another user while you had it open. " +
                        "Please close and reopen the product to get the latest version, then save again.");

                db.Execute("DELETE FROM ProductAttributes WHERE ProductID = @ProductID",
                    new { product.ProductID }, tx);

                foreach (var attr in product.Attributes
                    .Where(a => !string.IsNullOrWhiteSpace(a.AttributeName)))
                {
                    db.Execute(@"
                        INSERT INTO ProductAttributes (ProductID, AttributeName, AttributeValue)
                        VALUES (@ProductID, @AttributeName, @AttributeValue)",
                        new { ProductID = product.ProductID, attr.AttributeName, attr.AttributeValue }, tx);
                }

                tx.Commit();
            }
            catch { tx.Rollback(); throw; }
        }

        /// <summary>
        /// Ensures the product has a corresponding Part (PartNumber = SKU) and at least one BOM entry.
        /// Must be called inside an open transaction.
        /// </summary>
        private static void EnsurePartAndBom(SqlConnection db, SqlTransaction tx, int productId, string sku, string productName)
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
                IF NOT EXISTS (SELECT 1 FROM ProductParts WHERE ProductID = @productId AND PartID = @partId)
                INSERT INTO ProductParts (ProductID, PartID, Quantity) VALUES (@productId, @partId, 1);",
                new { productId, partId }, tx);
        }

        public void DeactivateProduct(int productId)
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            db.Execute("UPDATE Products SET IsActive = 0 WHERE ProductID = @id", new { id = productId });
        }

        public void RestoreProduct(int productId)
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            db.Execute("UPDATE Products SET IsActive = 1 WHERE ProductID = @id", new { id = productId });
        }

        /// <summary>Returns the number of BOM (ProductParts) entries for a specific product.</summary>
        public int GetBomCount(int productId)
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            try { return db.ExecuteScalar<int>("SELECT COUNT(*) FROM ProductParts WHERE ProductID = @productId", new { productId }); }
            catch (Exception ex) { Logging.AppLogger.Error($"[ProductRepository.GetBomCount] productId={productId}: {ex}"); return 0; }
        }

        /// <summary>Returns the count of active products that have no BOM entries (ProductParts).</summary>
        public int CountProductsWithNoBOM()
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            try
            {
                return db.ExecuteScalar<int>(
                    "SELECT COUNT(*) FROM Products p WHERE p.IsActive=1 AND NOT EXISTS (SELECT 1 FROM ProductParts pp WHERE pp.ProductID=p.ProductID)");
            }
            catch (Exception ex) { Logging.AppLogger.Error($"[ProductRepository.CountProductsWithNoBOM] {ex}"); return 0; }
        }

        // ── BOM source / numbering ─────────────────────────────────────────────────

        /// <summary>Links a product's BOM to another product so its bill-of-materials can be copied or referenced.</summary>
        public void SetBomSource(int productId, int sourceProductId)
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            try
            {
                db.Execute("UPDATE Products SET BomSourceID = @sourceProductId WHERE ProductID = @productId",
                    new { productId, sourceProductId });
            }
            catch (Exception ex) { Logging.AppLogger.Info($"SetBomSource warning: {ex.Message}"); }
        }

        /// <summary>Returns the next available BOM number in the format BOM-0001.</summary>
        public string NextBomNumber()
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            try
            {
                int next = db.ExecuteScalar<int>(@"
                    SELECT ISNULL(MAX(TRY_CAST(SUBSTRING(BomNumber, 5, LEN(BomNumber)) AS INT)), 0) + 1
                    FROM   Products
                    WHERE  BomNumber IS NOT NULL AND BomNumber LIKE 'BOM-%'");
                return $"BOM-{next:D4}";
            }
            catch (Exception ex) { Logging.AppLogger.Error($"[ProductRepository.NextBomNumber] {ex}"); return $"BOM-{DateTime.Now:yyyyMMddHHmm}"; }
        }

        /// <summary>Assigns a BOM number to a product.</summary>
        public void AssignBomNumber(int productId, string bomNumber)
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            try
            {
                db.Execute("UPDATE Products SET BomNumber = @bomNumber WHERE ProductID = @productId",
                    new { productId, bomNumber });
            }
            catch (Exception ex) { Logging.AppLogger.Info($"AssignBomNumber warning: {ex.Message}"); }
        }

        /// <summary>Removes the BOM source link from a product.</summary>
        public void ClearBomSource(int productId)
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            try
            {
                db.Execute("UPDATE Products SET BomSourceID = NULL WHERE ProductID = @productId",
                    new { productId });
            }
            catch (Exception ex) { Logging.AppLogger.Info($"ClearBomSource warning: {ex.Message}"); }
        }

        /// <summary>Permanently deactivates (soft-deletes) a product. Hard deletion is not used due to FK constraints.</summary>
        public void DeleteProduct(int productId)
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            db.Execute("UPDATE Products SET IsActive = 0 WHERE ProductID = @productId", new { productId });
        }

        public List<Models.ProductReorderRow> GetProductsAtReorderPoint()
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            // Pre-aggregate both tables once; filter on derived columns avoids 5 correlated subqueries.
            var rows = db.Query<Models.ProductReorderRow>(@"
                SELECT  p.SKU,
                        p.ProductName,
                        p.RetailPrice,
                        p.WholesalePrice,
                        p.ReorderPoint,
                        ISNULL(inv.CurrentStock, 0) AS CurrentStock,
                        ISNULL(res.ReservedQty,  0) AS ReservedQty
                FROM    Products p
                LEFT JOIN (
                    SELECT ProductID, SUM(QuantityChange) AS CurrentStock
                    FROM   InventoryTransactions
                    GROUP  BY ProductID
                ) inv ON inv.ProductID = p.ProductID
                LEFT JOIN (
                    SELECT ProductID, SUM(Quantity) AS ReservedQty
                    FROM   StockReservations
                    GROUP  BY ProductID
                ) res ON res.ProductID = p.ProductID
                WHERE   p.IsActive = 1
                  AND   p.ReorderPoint > 0
                  AND   (ISNULL(inv.CurrentStock, 0) - ISNULL(res.ReservedQty, 0)) <= p.ReorderPoint
                ORDER   BY p.SKU").ToList();

            foreach (var r in rows) r.Compute();
            return rows;
        }

        // ── Unverified items workflow ─────────────────────────────────────────────

        public List<Models.UnverifiedProduct> GetUnverifiedProducts()
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            return db.Query<Models.UnverifiedProduct>(@"
                SELECT p.ProductID, p.SKU, p.ProductName,
                       ISNULL(pt.TypeName, '') AS TypeName,
                       p.RetailPrice,
                       ISNULL((SELECT SUM(QuantityChange) FROM InventoryTransactions WHERE ProductID = p.ProductID), 0) AS CurrentStock
                FROM   Products p
                LEFT JOIN ProductTypes pt ON pt.ProductTypeID = p.ProductTypeID
                WHERE  p.IsAutoCreated = 1 AND p.IsVerified = 0 AND p.IsActive = 1
                ORDER  BY p.SKU").ToList();
        }

        public void VerifyProducts(IEnumerable<int> productIds)
        {
            var ids = productIds.ToList();
            if (ids.Count == 0) return;
            using IDbConnection db = new SqlConnection(_connectionString);
            db.Execute("UPDATE Products SET IsVerified = 1 WHERE ProductID IN @ids", new { ids });
        }

        public void BulkApplyTypeAndAttributes(IEnumerable<int> productIds, int? typeId,
            IEnumerable<(string Name, string Value)> attrs)
        {
            var ids      = productIds.ToList();
            var attrList = attrs.ToList();
            if (ids.Count == 0) return;

            using var db = new SqlConnection(_connectionString);
            db.Open();
            using var tx = db.BeginTransaction();
            try
            {
                if (typeId.HasValue)
                    db.Execute("UPDATE Products SET ProductTypeID = @typeId WHERE ProductID IN @ids",
                        new { typeId = typeId.Value, ids }, tx);

                foreach (int pid in ids)
                {
                    foreach (var (name, value) in attrList)
                    {
                        db.Execute(@"
                            IF EXISTS (SELECT 1 FROM ProductAttributes WHERE ProductID=@pid AND AttributeName=@name)
                                UPDATE ProductAttributes SET AttributeValue=@value WHERE ProductID=@pid AND AttributeName=@name
                            ELSE
                                INSERT INTO ProductAttributes (ProductID, AttributeName, AttributeValue)
                                VALUES (@pid, @name, @value)",
                            new { pid, name, value }, tx);
                    }
                }

                tx.Commit();
            }
            catch { tx.Rollback(); throw; }
        }

        // ── Paged queries ─────────────────────────────────────────────────────────

        public (List<Product> products, int total) GetPagedProducts(
            int page, int pageSize, string? search = null)
        {
            using IDbConnection db = new SqlConnection(_connectionString);

            string searchCondition = string.IsNullOrWhiteSpace(search)
                ? ""
                : "AND (p.SKU LIKE @search OR p.ProductName LIKE @search)";
            string searchParam = $"%{search}%";

            int total = db.ExecuteScalar<int>(
                $"SELECT COUNT(*) FROM Products p WHERE p.IsActive = 1 {searchCondition}",
                new { search = searchParam });

            var products = db.Query<Product>($@"
                SELECT  p.ProductID, p.SKU, p.ProductName, p.UnitOfMeasure,
                        p.RetailPrice, p.WholesalePrice,
                        p.ReorderPoint, ISNULL(p.OrderUpTo, 0) AS OrderUpTo,
                        p.IsActive, p.DefaultLocationID, p.ProductTypeID,
                        pt.TypeName    AS ProductTypeName,
                        l.LocationName AS DefaultLocationName,
                        p.DefaultVendorID,
                        v.VendorName   AS DefaultVendorName,
                        p.BomNumber,
                        ISNULL(inv.CurrentStock, 0) AS CurrentStock,
                        ISNULL(res.ReservedQty,  0) AS ReservedQty
                FROM    Products p
                LEFT JOIN ProductTypes pt ON pt.ProductTypeID = p.ProductTypeID
                LEFT JOIN Locations    l  ON l.LocationID     = p.DefaultLocationID
                LEFT JOIN Vendors      v  ON v.VendorID       = p.DefaultVendorID
                LEFT JOIN (
                    SELECT ProductID, SUM(QuantityChange) AS CurrentStock
                    FROM   InventoryTransactions
                    GROUP  BY ProductID
                ) inv ON inv.ProductID = p.ProductID
                LEFT JOIN (
                    SELECT ProductID, SUM(Quantity) AS ReservedQty
                    FROM   StockReservations
                    GROUP  BY ProductID
                ) res ON res.ProductID = p.ProductID
                WHERE   p.IsActive = 1 {searchCondition}
                ORDER BY p.SKU
                OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY",
                new { search = searchParam, offset = (page - 1) * pageSize, pageSize }).ToList();

            return (products, total);
        }
    }
}