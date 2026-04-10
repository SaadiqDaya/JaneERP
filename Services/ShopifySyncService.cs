using System.Configuration;
using System.Data;
using Dapper;
using JaneERP.Logging;
using JaneERP.Models;
using Microsoft.Data.SqlClient;

namespace JaneERP.Services
{
    public class ShopifySyncService
    {
        private readonly string _connectionString =
            ConfigurationManager.ConnectionStrings["MyERP"]?.ConnectionString
            ?? throw new InvalidOperationException("Connection string 'MyERP' not found in App.config.");

        /// <summary>Creates the Customers, SalesOrders, and SalesOrderItems tables if they don't exist,
        /// and adds StoreID to SalesOrders if it was created before multi-store support.</summary>
        public void EnsureSchema()
        {
            using IDbConnection db = new SqlConnection(_connectionString);

            // ── Core tables ───────────────────────────────────────────────────────────
            db.Execute(@"
        IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='Customers' AND xtype='U')
        CREATE TABLE Customers (
            CustomerID  INT           IDENTITY(1,1) PRIMARY KEY,
            Email       NVARCHAR(200) NOT NULL,
            FullName    NVARCHAR(200) NULL,
            CreatedAt   DATETIME      NOT NULL DEFAULT GETDATE(),
            CONSTRAINT UQ_Customers_Email UNIQUE (Email)
        );

        IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='SalesOrders' AND xtype='U')
        CREATE TABLE SalesOrders (
            SalesOrderID    INT           IDENTITY(1,1) PRIMARY KEY,
            ShopifyOrderID  BIGINT        NULL,
            OrderNumber     INT           NOT NULL,
            CustomerID      INT           NOT NULL REFERENCES Customers(CustomerID),
            StoreID         INT           NULL REFERENCES Stores(StoreID),
            OrderDate       DATETIME      NOT NULL,
            TotalPrice      DECIMAL(18,2) NOT NULL,
            Currency        NVARCHAR(10)  NULL,
            Notes           NVARCHAR(1000) NULL,
            Status          NVARCHAR(20)  NOT NULL DEFAULT 'Draft',
            InventoryAffected BIT         NOT NULL DEFAULT 0,
            OrderType       NVARCHAR(50)  NOT NULL DEFAULT 'Shopify',
            CreatedAt       DATETIME      NOT NULL DEFAULT GETDATE()
        );

        IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='SalesOrderItems' AND xtype='U')
        CREATE TABLE SalesOrderItems (
            SalesOrderItemID INT           IDENTITY(1,1) PRIMARY KEY,
            SalesOrderID     INT           NOT NULL REFERENCES SalesOrders(SalesOrderID),
            ProductID        INT           NOT NULL REFERENCES Products(ProductID),
            SKU              NVARCHAR(100) NULL,
            Title            NVARCHAR(500) NULL,
            Quantity         INT           NOT NULL,
            UnitPrice        DECIMAL(18,2) NOT NULL
        );");

            // ── Migrations (each isolated so one failure doesn't block others) ────────
            Migrate(db, "IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('SalesOrders') AND name='StoreID') ALTER TABLE SalesOrders ADD StoreID INT NULL REFERENCES Stores(StoreID)");
            Migrate(db, "IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('SalesOrders') AND name='Notes') ALTER TABLE SalesOrders ADD Notes NVARCHAR(1000) NULL");
            Migrate(db, "IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('SalesOrders') AND name='Status') ALTER TABLE SalesOrders ADD Status NVARCHAR(20) NOT NULL DEFAULT 'Draft'");
            Migrate(db, "IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('SalesOrders') AND name='InventoryAffected') ALTER TABLE SalesOrders ADD InventoryAffected BIT NOT NULL DEFAULT 0");
            Migrate(db, "IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('SalesOrders') AND name='OrderType') ALTER TABLE SalesOrders ADD OrderType NVARCHAR(50) NOT NULL DEFAULT 'Shopify'");
            Migrate(db, "UPDATE SalesOrders SET OrderType='Manual' WHERE ShopifyOrderID IS NULL AND OrderType='Shopify'");

            // Make ShopifyOrderID nullable (for manual orders)
            Migrate(db, @"
        IF EXISTS (
            SELECT 1 FROM sys.columns
            WHERE object_id=OBJECT_ID('SalesOrders') AND name='ShopifyOrderID' AND is_nullable=0)
        BEGIN
            DECLARE @uc NVARCHAR(128)
            SELECT TOP 1 @uc = i.name
            FROM sys.indexes i
            JOIN sys.index_columns ic ON i.object_id=ic.object_id AND i.index_id=ic.index_id
            JOIN sys.columns c        ON ic.object_id=c.object_id  AND ic.column_id=c.column_id
            WHERE i.is_unique_constraint=1
              AND i.object_id=OBJECT_ID('SalesOrders')
              AND c.name='ShopifyOrderID'
            IF @uc IS NOT NULL EXEC('ALTER TABLE SalesOrders DROP CONSTRAINT [' + @uc + ']')
            ALTER TABLE SalesOrders ALTER COLUMN ShopifyOrderID BIGINT NULL
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('SalesOrders') AND name='UQ_SalesOrders_ShopifyOrderID')
                ALTER TABLE SalesOrders ADD CONSTRAINT UQ_SalesOrders_ShopifyOrderID UNIQUE (ShopifyOrderID)
        END");
        }

        private static void Migrate(IDbConnection db, string sql)
        {
            try { db.Execute(sql); }
            catch (Exception ex) { AppLogger.Info($"Schema migration warning (non-fatal): {ex.Message}"); }
        }

        /// <summary>
        /// Returns ERP SalesOrders as Order display objects.
        /// Pass orderType = "Manual" for manual orders, "Shopify" for Shopify-synced,
        /// or null to return all order types.
        /// Pass nonShopifyOnly = true to return all non-Shopify orders.
        /// </summary>
        public List<Models.Order> GetErpOrders(string? orderType = null, bool nonShopifyOnly = false)
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            string filter = nonShopifyOnly
                ? "WHERE so.OrderType <> 'Shopify'"
                : (orderType != null ? "WHERE so.OrderType = @orderType" : "");
            return db.Query<Models.Order>($@"
                SELECT so.SalesOrderID                        AS ErpSalesOrderID,
                       so.ShopifyOrderID                      AS Id,
                       so.OrderNumber,
                       c.FullName                             AS Name,
                       c.Email                                AS ContactEmail,
                       so.OrderDate                           AS CreatedAt,
                       so.TotalPrice,
                       so.Currency,
                       so.Status                              AS ErpStatus,
                       ISNULL(st.StoreName, so.OrderType)     AS StoreName,
                       so.DiscountType,
                       ISNULL(so.DiscountAmount, 0)           AS DiscountAmount,
                       ISNULL(so.DiscountPercent, 0)          AS DiscountPercent
                FROM  SalesOrders so
                JOIN  Customers   c   ON c.CustomerID = so.CustomerID
                LEFT JOIN Stores  st  ON st.StoreID   = so.StoreID
                {filter}
                ORDER BY so.OrderDate DESC", new { orderType }).ToList();
        }

        /// <summary>
        /// Updates the Status of a SalesOrder.
        /// If transitioning to "Live" and inventory has not yet been deducted,
        /// creates InventoryTransactions for each line item and marks InventoryAffected = 1.
        /// Returns true if the row was found.
        /// </summary>
        public bool UpdateOrderStatus(int salesOrderId, string newStatus)
        {
            using var db = new SqlConnection(_connectionString);
            db.Open();
            using var tx = db.BeginTransaction();
            try
            {
                var order = db.QueryFirstOrDefault(
                    "SELECT SalesOrderID, OrderNumber, InventoryAffected FROM SalesOrders WHERE SalesOrderID = @id",
                    new { id = salesOrderId }, tx);

                if (order == null) { tx.Rollback(); return false; }

                db.Execute(
                    "UPDATE SalesOrders SET Status = @s WHERE SalesOrderID = @id",
                    new { s = newStatus, id = salesOrderId }, tx);

                // Deduct inventory once when first going Live
                bool wasAffected = order.InventoryAffected == true || (int)order.InventoryAffected == 1;
                if (newStatus == "Live" && !wasAffected)
                {
                    var items = db.Query(
                        "SELECT ProductID, Quantity, SalesOrderID FROM SalesOrderItems WHERE SalesOrderID = @id",
                        new { id = salesOrderId }, tx).ToList();

                    // Check stock sufficiency before deducting
                    var stockShortages = new List<string>();
                    foreach (var li in items)
                    {
                        int currentStock = db.ExecuteScalar<int>(
                            "SELECT ISNULL(SUM(QuantityChange), 0) FROM InventoryTransactions WHERE ProductID = @pid",
                            new { pid = (int)li.ProductID }, tx);
                        if (currentStock < (int)li.Quantity)
                            stockShortages.Add($"ProductID {li.ProductID}: need {li.Quantity}, have {currentStock}");
                    }

                    if (stockShortages.Any())
                        AppLogger.Audit("system", "StockShortageOnLive",
                            $"OrderID={salesOrderId}: {string.Join("; ", stockShortages)}");
                    // Proceed with deduction regardless — log is the warning; don't block the status change

                    int orderNumber = (int)order.OrderNumber;
                    foreach (var li in items)
                    {
                        db.Execute(@"
                            INSERT INTO InventoryTransactions (ProductID, QuantityChange, TransactionType, Notes, TransactionDate)
                            VALUES (@ProductID, @QuantityChange, 'Sale', @Notes, @TransactionDate);",
                            new
                            {
                                ProductID       = (int)li.ProductID,
                                QuantityChange  = -(int)li.Quantity,
                                Notes           = $"Order #{orderNumber} → Live",
                                TransactionDate = DateTime.Now
                            }, tx);
                    }

                    db.Execute(
                        "UPDATE SalesOrders SET InventoryAffected = 1 WHERE SalesOrderID = @id",
                        new { id = salesOrderId }, tx);
                }

                tx.Commit();
                return true;
            }
            catch { tx.Rollback(); throw; }
        }

        /// <summary>Returns all customers from the ERP database, ordered by FullName then Email.</summary>
        public List<Models.Customer> GetAllCustomers()
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            return db.Query<Models.Customer>(
                "SELECT CustomerID, Email, FullName, CreatedAt FROM Customers ORDER BY FullName, Email")
                .ToList();
        }

        /// <summary>Returns the set of Shopify order IDs that have already been synced to the ERP.</summary>
        public HashSet<long> GetSyncedOrderIds()
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            var ids = db.Query<long>("SELECT ShopifyOrderID FROM SalesOrders WHERE ShopifyOrderID IS NOT NULL");
            return new HashSet<long>(ids);
        }

        /// <summary>
        /// Creates a manual (non-Shopify) sales order.
        /// Returns the new SalesOrderID.
        /// </summary>
        public int CreateManualOrder(
            string customerEmail, string? customerName,
            DateTime orderDate, string? notes, string? currency,
            int? storeId,
            IEnumerable<(string Sku, string Title, int Qty, decimal UnitPrice)> lineItems,
            string status = "Live",
            string orderType = "Manual",
            string? discountType = null,
            decimal discountAmount = 0,
            decimal discountPercent = 0)
        {
            using var db = new SqlConnection(_connectionString);
            db.Open();
            using var tx = db.BeginTransaction();
            try
            {
                // Customer: find or create
                var email = customerEmail.Trim().ToLowerInvariant();
                var customerId = db.ExecuteScalar<int?>(
                    "SELECT CustomerID FROM Customers WHERE Email = @email", new { email }, tx);
                if (customerId == null)
                {
                    customerId = db.QuerySingle<int>(@"
                        INSERT INTO Customers (Email, FullName)
                        VALUES (@email, @fullName);
                        SELECT CAST(SCOPE_IDENTITY() AS INT);",
                        new { email, fullName = customerName ?? email }, tx);
                }

                // Auto-generate order number for manual orders
                var orderNumber = db.ExecuteScalar<int>(
                    "SELECT ISNULL(MAX(OrderNumber), 0) + 1 FROM SalesOrders WHERE ShopifyOrderID IS NULL", null, tx);

                decimal total = lineItems.Sum(li => li.Qty * li.UnitPrice);

                bool affectsInventory = status == "Live";

                var salesOrderId = db.QuerySingle<int>(@"
                    INSERT INTO SalesOrders (ShopifyOrderID, OrderNumber, CustomerID, StoreID, OrderDate, TotalPrice, Currency, Notes, Status, InventoryAffected, OrderType, DiscountType, DiscountAmount, DiscountPercent)
                    VALUES (NULL, @OrderNumber, @CustomerID, @StoreID, @OrderDate, @TotalPrice, @Currency, @Notes, @Status, @InventoryAffected, @OrderType, @DiscountType, @DiscountAmount, @DiscountPercent);
                    SELECT CAST(SCOPE_IDENTITY() AS INT);",
                    new { OrderNumber = orderNumber, CustomerID = customerId, StoreID = storeId,
                          OrderDate = orderDate, TotalPrice = total - discountAmount, Currency = currency ?? "CAD",
                          Notes = notes, Status = status, InventoryAffected = affectsInventory,
                          OrderType = orderType,
                          DiscountType = discountType, DiscountAmount = discountAmount, DiscountPercent = discountPercent }, tx);

                foreach (var li in lineItems)
                {
                    var sku = li.Sku.Trim();
                    var productId = db.ExecuteScalar<int?>(
                        "SELECT ProductID FROM Products WHERE SKU = @sku", new { sku }, tx);
                    if (productId == null)
                    {
                        productId = db.QuerySingle<int>(@"
                            INSERT INTO Products (SKU, ProductName, RetailPrice, IsActive)
                            VALUES (@SKU, @ProductName, @RetailPrice, 1);
                            SELECT CAST(SCOPE_IDENTITY() AS INT);",
                            new { SKU = sku, ProductName = li.Title, RetailPrice = li.UnitPrice }, tx);

                        // Every product must have a Part and a BOM entry
                        var partId = db.ExecuteScalar<int?>(
                            "SELECT PartID FROM Parts WHERE PartNumber = @sku", new { sku }, tx);
                        if (partId == null)
                        {
                            partId = db.QuerySingle<int>(@"
                                INSERT INTO Parts (PartNumber, PartName, UnitCost, CurrentStock, IsActive)
                                VALUES (@sku, @name, 0, 0, 1);
                                SELECT CAST(SCOPE_IDENTITY() AS INT);",
                                new { sku, name = li.Title ?? sku }, tx);
                        }
                        db.Execute(@"
                            IF NOT EXISTS (SELECT 1 FROM ProductParts WHERE ProductID=@pid AND PartID=@partId)
                            INSERT INTO ProductParts (ProductID, PartID, Quantity) VALUES (@pid, @partId, 1);",
                            new { pid = productId, partId }, tx);
                    }

                    db.Execute(@"
                        INSERT INTO SalesOrderItems (SalesOrderID, ProductID, SKU, Title, Quantity, UnitPrice)
                        VALUES (@SalesOrderID, @ProductID, @SKU, @Title, @Quantity, @UnitPrice);",
                        new { SalesOrderID = salesOrderId, ProductID = productId,
                              SKU = sku, Title = li.Title, Quantity = li.Qty, UnitPrice = li.UnitPrice }, tx);

                    if (affectsInventory)
                    {
                        db.Execute(@"
                            INSERT INTO InventoryTransactions (ProductID, QuantityChange, TransactionType, Notes, TransactionDate)
                            VALUES (@ProductID, @QuantityChange, 'Manual Sale', @Notes, @TransactionDate);",
                            new { ProductID = productId, QuantityChange = -li.Qty,
                                  Notes = $"Manual Order #{orderNumber}", TransactionDate = orderDate }, tx);
                    }
                }

                tx.Commit();
                return salesOrderId;
            }
            catch { tx.Rollback(); throw; }
        }

        /// <summary>
        /// Saves a single Shopify order to the ERP database in one transaction.
        /// Returns true if saved, false if already existed (skipped).
        /// Throws on error — caller is responsible for catching and continuing.
        /// </summary>
        public bool ProcessShopifyOrder(OrderDetails order, int? storeId = null)
        {
            using var db = new SqlConnection(_connectionString);
            db.Open();
            using var tx = db.BeginTransaction();

            try
            {
                // ── Idempotency: skip if this Shopify order was already saved ────────
                var exists = db.ExecuteScalar<int>(
                    "SELECT COUNT(1) FROM SalesOrders WHERE ShopifyOrderID = @id",
                    new { id = order.Id }, tx);

                if (exists > 0)
                    return false;

                // ── Customer: find by email or create ────────────────────────────────
                var email = string.IsNullOrWhiteSpace(order.ContactEmail)
                    ? $"guest_{order.Id}@shopify.local"
                    : order.ContactEmail.Trim().ToLowerInvariant();

                var customerId = db.ExecuteScalar<int?>(
                    "SELECT CustomerID FROM Customers WHERE Email = @email",
                    new { email }, tx);

                if (customerId == null)
                {
                    customerId = db.QuerySingle<int>(@"
                        INSERT INTO Customers (Email, FullName)
                        VALUES (@email, @fullName);
                        SELECT CAST(SCOPE_IDENTITY() AS INT);",
                        new { email, fullName = order.CustomerName }, tx);
                }

                // ── SalesOrder ───────────────────────────────────────────────────────
                // Shopify orders come in as Live but InventoryAffected=0;
                // inventory is only deducted when an order is marked Fulfilled.
                var salesOrderId = db.QuerySingle<int>(@"
                    INSERT INTO SalesOrders (ShopifyOrderID, OrderNumber, CustomerID, StoreID, OrderDate, TotalPrice, Currency, Status, InventoryAffected, OrderType)
                    VALUES (@ShopifyOrderID, @OrderNumber, @CustomerID, @StoreID, @OrderDate, @TotalPrice, @Currency, 'Draft', 0, 'Shopify');
                    SELECT CAST(SCOPE_IDENTITY() AS INT);",
                    new
                    {
                        ShopifyOrderID = order.Id,
                        OrderNumber    = order.OrderNumber,
                        CustomerID     = customerId,
                        StoreID        = storeId,
                        OrderDate      = order.CreatedAt,
                        TotalPrice     = order.TotalPrice,
                        Currency       = order.Currency
                    }, tx);

                // ── Line items ───────────────────────────────────────────────────────
                foreach (var li in order.LineItems)
                {
                    // Use SKU if present, otherwise fall back to the Shopify line item ID
                    var sku = string.IsNullOrWhiteSpace(li.Sku)
                        ? $"SHOPIFY-{li.Id}"
                        : li.Sku.Trim();

                    // Product: find by SKU or auto-create
                    var productId = db.ExecuteScalar<int?>(
                        "SELECT ProductID FROM Products WHERE SKU = @sku",
                        new { sku }, tx);

                    if (productId == null)
                    {
                        productId = db.QuerySingle<int>(@"
                            INSERT INTO Products (SKU, ProductName, RetailPrice, IsActive)
                            VALUES (@SKU, @ProductName, @RetailPrice, 1);
                            SELECT CAST(SCOPE_IDENTITY() AS INT);",
                            new
                            {
                                SKU         = sku,
                                ProductName = li.Title ?? sku,
                                RetailPrice = li.Price
                            }, tx);

                        AppLogger.Info($"ShopifySync: auto-created product SKU={sku}");

                        // Every product must have a Part and a BOM entry
                        var partId = db.ExecuteScalar<int?>(
                            "SELECT PartID FROM Parts WHERE PartNumber = @sku", new { sku }, tx);
                        if (partId == null)
                        {
                            partId = db.QuerySingle<int>(@"
                                INSERT INTO Parts (PartNumber, PartName, UnitCost, CurrentStock, IsActive)
                                VALUES (@sku, @name, 0, 0, 1);
                                SELECT CAST(SCOPE_IDENTITY() AS INT);",
                                new { sku, name = li.Title ?? sku }, tx);
                        }
                        db.Execute(@"
                            IF NOT EXISTS (SELECT 1 FROM ProductParts WHERE ProductID=@pid AND PartID=@partId)
                            INSERT INTO ProductParts (ProductID, PartID, Quantity) VALUES (@pid, @partId, 1);",
                            new { pid = productId, partId }, tx);
                    }

                    // SalesOrderItem
                    db.Execute(@"
                        INSERT INTO SalesOrderItems (SalesOrderID, ProductID, SKU, Title, Quantity, UnitPrice)
                        VALUES (@SalesOrderID, @ProductID, @SKU, @Title, @Quantity, @UnitPrice);",
                        new
                        {
                            SalesOrderID = salesOrderId,
                            ProductID    = productId,
                            SKU          = sku,
                            Title        = li.Title,
                            Quantity     = li.Quantity,
                            UnitPrice    = li.Price
                        }, tx);

                    // Inventory is deducted when the order is marked Fulfilled, not on sync.
                }

                // Auto-create manufacturing work orders for BOM-linked products
                var moId = (int?)null;
                foreach (var li in order.LineItems)
                {
                    var sku = string.IsNullOrWhiteSpace(li.Sku) ? $"SHOPIFY-{li.Id}" : li.Sku.Trim();
                    var productId = db.ExecuteScalar<int?>(
                        "SELECT ProductID FROM Products WHERE SKU = @sku", new { sku }, tx);
                    if (productId == null) continue;

                    var hasBom = db.ExecuteScalar<int>(
                        "SELECT COUNT(1) FROM ProductParts WHERE ProductID = @pid", new { pid = productId }, tx) > 0;
                    if (!hasBom) continue;

                    // Create MO if not yet created for this Shopify order
                    if (moId == null)
                    {
                        string moNumber = $"MO-SHP-{order.OrderNumber}";
                        // Skip if MO already exists (re-sync)
                        moId = db.ExecuteScalar<int?>(
                            "SELECT MOID FROM ManufacturingOrders WHERE MONumber = @moNumber",
                            new { moNumber }, tx);
                        if (moId == null)
                        {
                            moId = db.QuerySingle<int>(@"
                                INSERT INTO ManufacturingOrders (MONumber, Status, Notes, OrderedBy)
                                VALUES (@MONumber, 'Open', @Notes, 'Shopify Sync');
                                SELECT CAST(SCOPE_IDENTITY() AS INT);",
                                new { MONumber = moNumber, Notes = $"Auto-created from Shopify Order #{order.OrderNumber}" }, tx);
                        }
                    }

                    // Add work order for this line item
                    var existingWo = db.ExecuteScalar<int>(
                        "SELECT COUNT(1) FROM WorkOrders WHERE MOID = @moid AND ProductID = @pid",
                        new { moid = moId, pid = productId }, tx);
                    if (existingWo == 0)
                    {
                        db.Execute(@"
                            INSERT INTO WorkOrders (MOID, ProductID, Quantity, Status, Notes, ShopifyOrderID)
                            VALUES (@MOID, @ProductID, @Quantity, 'Pending', @Notes, @ShopifyOrderID);",
                            new { MOID = moId, ProductID = productId, Quantity = li.Quantity,
                                  Notes = $"Shopify Order #{order.OrderNumber}", ShopifyOrderID = order.Id }, tx);
                    }
                }

                tx.Commit();
                return true;
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        }
    }
}
