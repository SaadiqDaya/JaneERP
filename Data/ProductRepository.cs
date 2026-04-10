using System.Configuration;
using System.Data;
using Dapper;
using JaneERP.Models;
using Microsoft.Data.SqlClient;

namespace JaneERP.Data
{
    public class ProductRepository
    {
        private readonly string _connectionString =
            ConfigurationManager.ConnectionStrings["MyERP"]?.ConnectionString
            ?? throw new InvalidOperationException(
                "Connection string 'MyERP' not found in App.config.");

        // CurrentStock is 100% calculated from InventoryTransactions.
        // Pass locationId to scope the stock to a single location; null = all locations combined.
        public IEnumerable<Product> GetProducts(bool showInactive = false, int? locationId = null)
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            string filter = showInactive ? "p.IsActive = 0" : "p.IsActive = 1";
            return db.Query<Product>($@"
                SELECT  p.ProductID,
                        p.SKU,
                        p.ProductName,
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
                        ISNULL((
                            SELECT SUM(t.QuantityChange)
                            FROM   InventoryTransactions t
                            WHERE  t.ProductID = p.ProductID
                              AND  (@LocationID IS NULL OR t.LocationID = @LocationID)
                        ), 0) AS CurrentStock
                FROM    Products p
                LEFT JOIN ProductTypes pt ON pt.ProductTypeID = p.ProductTypeID
                LEFT JOIN Locations    l  ON l.LocationID     = p.DefaultLocationID
                LEFT JOIN Vendors       v  ON v.VendorID      = p.DefaultVendorID
                WHERE   {filter}",
                new { LocationID = locationId }).ToList();
        }

        /// <summary>Adds new product columns (ReorderPoint, OrderUpTo, DefaultVendorID) if they don't exist yet.</summary>
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
                        ALTER TABLE Products ADD DefaultVendorID INT NULL REFERENCES Vendors(VendorID);");
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
                    INSERT INTO Products (SKU, ProductName, RetailPrice, WholesalePrice, IsActive, DefaultLocationID, ProductTypeID, ReorderPoint, OrderUpTo, DefaultVendorID)
                    VALUES (@SKU, @ProductName, @RetailPrice, @WholesalePrice, @IsActive, @DefaultLocationID, @ProductTypeID, @ReorderPoint, @OrderUpTo, @DefaultVendorID);
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

        public void UpdateProduct(Product product)
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

                db.Execute(@"
                    UPDATE Products
                    SET SKU               = @SKU,
                        ProductName       = @ProductName,
                        RetailPrice       = @RetailPrice,
                        WholesalePrice    = @WholesalePrice,
                        DefaultLocationID = @DefaultLocationID,
                        ProductTypeID     = @ProductTypeID,
                        ReorderPoint      = @ReorderPoint,
                        OrderUpTo         = @OrderUpTo,
                        DefaultVendorID   = @DefaultVendorID
                    WHERE ProductID = @ProductID", product, tx);

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
                    INSERT INTO Parts (PartNumber, PartName, UnitCost, CurrentStock, IsActive)
                    VALUES (@sku, @productName, 0, 0, 1);
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

        /// <summary>Returns the count of active products that have no BOM entries (ProductParts).</summary>
        public int CountProductsWithNoBOM()
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            try
            {
                return db.ExecuteScalar<int>(
                    "SELECT COUNT(*) FROM Products p WHERE p.IsActive=1 AND NOT EXISTS (SELECT 1 FROM ProductParts pp WHERE pp.ProductID=p.ProductID)");
            }
            catch { return 0; }
        }
    }
}