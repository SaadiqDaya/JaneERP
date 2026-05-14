using System.Configuration;
using System.Data;
using Dapper;
using JaneERP.Interfaces;
using JaneERP.Logging;
using JaneERP.Models;
using Microsoft.Data.SqlClient;

namespace JaneERP.Services
{
    public class ShopifySyncService : IShopifySyncService
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
            Migrate(db, "IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('SalesOrders') AND name='ShippingCost') ALTER TABLE SalesOrders ADD ShippingCost DECIMAL(18,2) NOT NULL DEFAULT 0");
            Migrate(db, "UPDATE SalesOrders SET OrderType='Manual' WHERE ShopifyOrderID IS NULL AND OrderType='Shopify'");
            Migrate(db, "IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('SalesOrders') AND name='IsPaid') ALTER TABLE SalesOrders ADD IsPaid BIT NOT NULL DEFAULT 0");
            Migrate(db, "IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('SalesOrders') AND name='PaidAt') ALTER TABLE SalesOrders ADD PaidAt DATETIME NULL");
            Migrate(db, "IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('SalesOrders') AND name='PaymentGateway') ALTER TABLE SalesOrders ADD PaymentGateway NVARCHAR(100) NULL");
            Migrate(db, @"
        IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='CustomerPayments' AND xtype='U')
        CREATE TABLE CustomerPayments (
            PaymentID     INT IDENTITY(1,1) PRIMARY KEY,
            CustomerID    INT           NOT NULL REFERENCES Customers(CustomerID),
            SalesOrderID  INT           NULL REFERENCES SalesOrders(SalesOrderID),
            Amount        DECIMAL(18,2) NOT NULL,
            PaymentMethod NVARCHAR(50)  NULL,
            Notes         NVARCHAR(500) NULL,
            PaidAt        DATETIME      NOT NULL DEFAULT GETDATE()
        )");

            // Replace UNIQUE constraint on ShopifyOrderID (which disallows multiple NULLs) with a filtered
            // unique index that only applies when ShopifyOrderID IS NOT NULL — allows unlimited manual orders.
            Migrate(db, @"
        DECLARE @cn NVARCHAR(128)
        SELECT TOP 1 @cn = i.name
        FROM sys.indexes i
        JOIN sys.index_columns ic ON i.object_id=ic.object_id AND i.index_id=ic.index_id
        JOIN sys.columns c        ON ic.object_id=c.object_id  AND ic.column_id=c.column_id
        WHERE i.object_id=OBJECT_ID('SalesOrders') AND c.name='ShopifyOrderID'
          AND (i.is_unique_constraint=1 OR (i.is_unique=1 AND i.filter_definition IS NULL))
        IF @cn IS NOT NULL EXEC('ALTER TABLE SalesOrders DROP CONSTRAINT IF EXISTS [' + @cn + ']; DROP INDEX IF EXISTS [' + @cn + '] ON SalesOrders')
        IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID('SalesOrders') AND name='UIX_SalesOrders_ShopifyOrderID')
            EXEC('CREATE UNIQUE INDEX UIX_SalesOrders_ShopifyOrderID ON SalesOrders(ShopifyOrderID) WHERE ShopifyOrderID IS NOT NULL')");

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

            // Stock reservations: soft-locks created when an SO goes Live, released on Complete/Draft
            Migrate(db, @"
        IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='StockReservations' AND xtype='U')
        CREATE TABLE StockReservations (
            ReservationID INT IDENTITY(1,1) PRIMARY KEY,
            SalesOrderID  INT NOT NULL REFERENCES SalesOrders(SalesOrderID),
            ProductID     INT NOT NULL REFERENCES Products(ProductID),
            LocationID    INT NULL     REFERENCES Locations(LocationID),
            Quantity      INT NOT NULL,
            CreatedAt     DATETIME NOT NULL DEFAULT GETDATE()
        )");
            // Fulfillment workflow columns — picking, packing, shipping
            Migrate(db, "IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('SalesOrders') AND name='PackedBy') ALTER TABLE SalesOrders ADD PackedBy NVARCHAR(100) NULL");
            Migrate(db, "IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('SalesOrders') AND name='PackedAt') ALTER TABLE SalesOrders ADD PackedAt DATETIME NULL");
            Migrate(db, "IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('SalesOrders') AND name='TrackingNumber') ALTER TABLE SalesOrders ADD TrackingNumber NVARCHAR(100) NULL");
            Migrate(db, "IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('SalesOrders') AND name='Carrier') ALTER TABLE SalesOrders ADD Carrier NVARCHAR(100) NULL");
            Migrate(db, "IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('SalesOrders') AND name='ShippedBy') ALTER TABLE SalesOrders ADD ShippedBy NVARCHAR(100) NULL");
            Migrate(db, "IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('SalesOrders') AND name='ShippedAt') ALTER TABLE SalesOrders ADD ShippedAt DATETIME NULL");
            Migrate(db, "IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('SalesOrderItems') AND name='PickedQty') ALTER TABLE SalesOrderItems ADD PickedQty INT NOT NULL DEFAULT 0");
            Migrate(db, "IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('SalesOrderItems') AND name='PickedBy') ALTER TABLE SalesOrderItems ADD PickedBy NVARCHAR(100) NULL");
            Migrate(db, "IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('SalesOrderItems') AND name='PickedAt') ALTER TABLE SalesOrderItems ADD PickedAt DATETIME NULL");
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
                       ISNULL(so.DiscountPercent, 0)          AS DiscountPercent,
                       ISNULL(so.ShippingCost, 0)             AS ShippingCost,
                       ISNULL(so.IsPaid, 0)                   AS IsPaid,
                       so.PaidAt,
                       so.PaymentGateway
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
                    "SELECT SalesOrderID, OrderNumber, Status, InventoryAffected FROM SalesOrders WHERE SalesOrderID = @id",
                    new { id = salesOrderId }, tx);

                if (order == null) { tx.Rollback(); return false; }

                string currentStatus = (string)order.Status;

                db.Execute(
                    "UPDATE SalesOrders SET Status = @s WHERE SalesOrderID = @id",
                    new { s = newStatus, id = salesOrderId }, tx);

                // Record packing timestamp when entering Packing status
                if (newStatus == "Packing" && currentStatus == "Picking")
                {
                    string packedBy = Security.AppSession.CurrentUser?.Username ?? "system";
                    db.Execute(
                        "UPDATE SalesOrders SET PackedBy = @packedBy, PackedAt = GETDATE() WHERE SalesOrderID = @id",
                        new { packedBy, id = salesOrderId }, tx);
                }

                // Release stock reservations when completing or reverting to Draft from any active status
                var activeStatuses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    { "Live", "Picking", "Packing", "Shipped", "WIP" };
                if (newStatus == "Complete" ||
                    (newStatus == "Draft" && activeStatuses.Contains(currentStatus)))
                {
                    db.Execute("DELETE FROM StockReservations WHERE SalesOrderID = @id",
                        new { id = salesOrderId }, tx);
                }

                // Deduct inventory once when first going Complete
                // Dapper returns BIT as bool — cast directly, don't cast to int
                bool wasAffected = (bool)order.InventoryAffected;
                if (newStatus == "Complete" && !wasAffected)
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
                                Notes           = $"Order #{orderNumber} → Complete",
                                TransactionDate = DateTime.Now
                            }, tx);
                    }

                    db.Execute(
                        "UPDATE SalesOrders SET InventoryAffected = 1 WHERE SalesOrderID = @id",
                        new { id = salesOrderId }, tx);

                    AppLogger.Audit(Security.AppSession.CurrentUser?.Username ?? "system",
                        "InventoryDeducted",
                        $"OrderID={salesOrderId} OrderNumber={order.OrderNumber} items={items.Count}");
                }

                tx.Commit();
                return true;
            }
            catch { tx.Rollback(); throw; }
        }

        /// <summary>
        /// Marks a SalesOrder as paid and records a CustomerPayment transaction.
        /// Throws if the order is not found or is already paid.
        /// </summary>
        public void MarkAsPaid(int salesOrderId, string? paymentMethod = null, string? notes = null)
        {
            using var db = new SqlConnection(_connectionString);
            db.Open();
            using var tx = db.BeginTransaction();
            try
            {
                var order = db.QueryFirstOrDefault(
                    "SELECT SalesOrderID, CustomerID, TotalPrice, IsPaid, OrderNumber FROM SalesOrders WHERE SalesOrderID = @id",
                    new { id = salesOrderId }, tx)
                    ?? throw new InvalidOperationException("Sales order not found.");

                if ((bool)order.IsPaid)
                    throw new InvalidOperationException("This order is already marked as paid.");

                var now = DateTime.Now;

                db.Execute(
                    "UPDATE SalesOrders SET IsPaid = 1, PaidAt = @now WHERE SalesOrderID = @id",
                    new { now, id = salesOrderId }, tx);

                db.Execute(@"
                    INSERT INTO CustomerPayments (CustomerID, SalesOrderID, Amount, PaymentMethod, Notes, PaidAt)
                    VALUES (@CustomerID, @SalesOrderID, @Amount, @PaymentMethod, @Notes, @PaidAt)",
                    new
                    {
                        CustomerID    = (int)order.CustomerID,
                        SalesOrderID  = salesOrderId,
                        Amount        = (decimal)order.TotalPrice,
                        PaymentMethod = paymentMethod,
                        Notes         = notes,
                        PaidAt        = now
                    }, tx);

                tx.Commit();

                AppLogger.Audit(Security.AppSession.CurrentUser?.Username ?? "system",
                    "MarkAsPaid", $"OrderID={salesOrderId} OrderNumber={order.OrderNumber} Amount={order.TotalPrice:N2}");
            }
            catch { tx.Rollback(); throw; }
        }

        /// <summary>Returns the line items for a given SalesOrderID.</summary>
        public List<Models.SalesOrderItem> GetOrderItems(int salesOrderId)
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            return db.Query<Models.SalesOrderItem>(
                @"SELECT SKU, Title, Quantity, UnitPrice
                  FROM   SalesOrderItems
                  WHERE  SalesOrderID = @id
                  ORDER  BY SalesOrderItemID",
                new { id = salesOrderId }).ToList();
        }

        /// <summary>
        /// Builds the list of reservation lines for the stock-lock dialog when an SO goes Live.
        /// Returns one row per (product × location) that currently has positive stock.
        /// Products with no stock anywhere are included as zero-available rows so the operator
        /// can see the gap.
        /// </summary>
        public List<Models.ReservationLine> GetSOReservationItems(int salesOrderId)
        {
            using IDbConnection db = new SqlConnection(_connectionString);

            // Required qty per product
            var required = db.Query(
                    "SELECT ProductID, SUM(Quantity) AS Qty FROM SalesOrderItems WHERE SalesOrderID = @id GROUP BY ProductID",
                    new { id = salesOrderId })
                .ToDictionary(r => (int)r.ProductID, r => (int)r.Qty);

            // Stock per (product, location) with existing reservations from OTHER orders
            var rows = db.Query(@"
                SELECT
                    soi.ProductID,
                    p.SKU,
                    p.ProductName,
                    loc.LocationID,
                    loc.LocationName,
                    loc.StockQty                AS OnHand,
                    ISNULL(res.ReservedQty, 0)  AS AlreadyReserved
                FROM (SELECT DISTINCT ProductID FROM SalesOrderItems WHERE SalesOrderID = @id) soi
                JOIN Products p ON p.ProductID = soi.ProductID
                JOIN (
                    SELECT  it.ProductID,
                            it.LocationID,
                            ISNULL(l.LocationName, 'No Location') AS LocationName,
                            SUM(it.QuantityChange) AS StockQty
                    FROM    InventoryTransactions it
                    LEFT JOIN Locations l ON l.LocationID = it.LocationID
                    GROUP BY it.ProductID, it.LocationID, l.LocationName
                    HAVING  SUM(it.QuantityChange) > 0
                ) loc ON loc.ProductID = soi.ProductID
                LEFT JOIN (
                    SELECT  ProductID,
                            LocationID,
                            SUM(Quantity) AS ReservedQty
                    FROM    StockReservations
                    WHERE   SalesOrderID <> @id
                    GROUP BY ProductID, LocationID
                ) res ON res.ProductID  = loc.ProductID
                     AND (res.LocationID = loc.LocationID
                          OR (res.LocationID IS NULL AND loc.LocationID IS NULL))
                ORDER BY p.ProductName, loc.LocationName",
                new { id = salesOrderId }).ToList();

            var lines = new List<Models.ReservationLine>();

            foreach (var row in rows)
            {
                int productId = (int)row.ProductID;
                int req       = required.TryGetValue(productId, out int r) ? r : 0;
                int avail     = Math.Max(0, (int)row.OnHand - (int)row.AlreadyReserved);

                lines.Add(new Models.ReservationLine
                {
                    ItemId          = productId,
                    LocationId      = row.LocationID == null ? (int?)null : (int)row.LocationID,
                    DisplayLabel    = $"{row.SKU} — {row.ProductName}",
                    LocationName    = (string)row.LocationName,
                    Required        = req,
                    OnHand          = (int)row.OnHand,
                    AlreadyReserved = (int)row.AlreadyReserved,
                    ToLock          = Math.Min(req, avail)
                });
            }

            // Any product with no stock at all still needs a row so the operator sees it
            foreach (var (productId, qty) in required)
            {
                if (lines.Any(l => l.ItemId == productId)) continue;
                var p = db.QueryFirstOrDefault(
                    "SELECT SKU, ProductName FROM Products WHERE ProductID = @id",
                    new { id = productId });
                if (p == null) continue;
                lines.Add(new Models.ReservationLine
                {
                    ItemId       = productId,
                    LocationId   = null,
                    DisplayLabel = $"{p.SKU} — {p.ProductName}",
                    LocationName = "No Stock",
                    Required     = qty,
                    OnHand       = 0,
                    AlreadyReserved = 0,
                    ToLock       = 0
                });
            }

            return lines;
        }

        /// <summary>
        /// Persists the reservation choices made in the stock-lock dialog for a Sales Order.
        /// Replaces any prior reservations for this order.
        /// </summary>
        public void SaveSOReservations(int salesOrderId, IEnumerable<Models.ReservationLine> lines)
        {
            using var db = new SqlConnection(_connectionString);
            db.Open();
            using var tx = db.BeginTransaction();
            try
            {
                db.Execute("DELETE FROM StockReservations WHERE SalesOrderID = @id",
                    new { id = salesOrderId }, tx);

                foreach (var line in lines.Where(l => l.ToLock > 0))
                {
                    db.Execute(@"
                        INSERT INTO StockReservations (SalesOrderID, ProductID, LocationID, Quantity)
                        VALUES (@SalesOrderID, @ProductID, @LocationID, @Quantity)",
                        new
                        {
                            SalesOrderID = salesOrderId,
                            ProductID    = line.ItemId,
                            LocationID   = line.LocationId,
                            Quantity     = line.ToLock
                        }, tx);
                }

                tx.Commit();

                AppLogger.Audit(Security.AppSession.CurrentUser?.Username ?? "system",
                    "StockReserved",
                    $"OrderID={salesOrderId} lines={lines.Count(l => l.ToLock > 0)}");
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
            decimal discountPercent = 0,
            decimal shippingCost = 0)
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

                decimal subtotal = lineItems.Sum(li => li.Qty * li.UnitPrice);
                decimal total    = subtotal - discountAmount + shippingCost;

                bool affectsInventory = status == "Live";

                var salesOrderId = db.QuerySingle<int>(@"
                    INSERT INTO SalesOrders (ShopifyOrderID, OrderNumber, CustomerID, StoreID, OrderDate, TotalPrice, Currency, Notes, Status, InventoryAffected, OrderType, DiscountType, DiscountAmount, DiscountPercent, ShippingCost)
                    VALUES (NULL, @OrderNumber, @CustomerID, @StoreID, @OrderDate, @TotalPrice, @Currency, @Notes, @Status, @InventoryAffected, @OrderType, @DiscountType, @DiscountAmount, @DiscountPercent, @ShippingCost);
                    SELECT CAST(SCOPE_IDENTITY() AS INT);",
                    new { OrderNumber = orderNumber, CustomerID = customerId, StoreID = storeId,
                          OrderDate = orderDate, TotalPrice = total, Currency = currency ?? "CAD",
                          Notes = notes, Status = status, InventoryAffected = affectsInventory,
                          OrderType = orderType,
                          DiscountType = discountType, DiscountAmount = discountAmount, DiscountPercent = discountPercent,
                          ShippingCost = shippingCost }, tx);

                foreach (var li in lineItems)
                {
                    var sku = li.Sku.Trim();
                    var productId = db.ExecuteScalar<int?>(
                        "SELECT ProductID FROM Products WHERE SKU = @sku", new { sku }, tx);
                    if (productId == null)
                    {
                        productId = db.QuerySingle<int>(@"
                            INSERT INTO Products (SKU, ProductName, RetailPrice, IsActive, IsAutoCreated, IsVerified)
                            VALUES (@SKU, @ProductName, @RetailPrice, 1, 1, 0);
                            SELECT CAST(SCOPE_IDENTITY() AS INT);",
                            new { SKU = sku, ProductName = li.Title, RetailPrice = li.UnitPrice }, tx);

                        // Every product must have a Part and a BOM entry
                        var partId = db.ExecuteScalar<int?>(
                            "SELECT PartID FROM Parts WHERE PartNumber = @sku", new { sku }, tx);
                        if (partId == null)
                        {
                            partId = db.QuerySingle<int>(@"
                                INSERT INTO Parts (PartNumber, PartName, UnitCost, CurrentStock, IsActive, IsAutoCreated, IsVerified)
                                VALUES (@sku, @name, 0, 0, 1, 1, 0);
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
                bool isPaid = string.Equals(order.FinancialStatus, "paid", StringComparison.OrdinalIgnoreCase);

                var salesOrderId = db.QuerySingle<int>(@"
                    INSERT INTO SalesOrders (ShopifyOrderID, OrderNumber, CustomerID, StoreID, OrderDate, TotalPrice, Currency, Status, InventoryAffected, OrderType, IsPaid, PaidAt, PaymentGateway)
                    VALUES (@ShopifyOrderID, @OrderNumber, @CustomerID, @StoreID, @OrderDate, @TotalPrice, @Currency, 'Live', 0, 'Shopify', @IsPaid, @PaidAt, @PaymentGateway);
                    SELECT CAST(SCOPE_IDENTITY() AS INT);",
                    new
                    {
                        ShopifyOrderID = order.Id,
                        OrderNumber    = order.OrderNumber,
                        CustomerID     = customerId,
                        StoreID        = storeId,
                        OrderDate      = order.CreatedAt,
                        TotalPrice     = order.TotalPrice,
                        Currency       = order.Currency,
                        IsPaid         = isPaid,
                        PaidAt         = isPaid ? (DateTime?)order.CreatedAt : null,
                        PaymentGateway = order.PaymentGateway
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
                            INSERT INTO Products (SKU, ProductName, RetailPrice, IsActive, IsAutoCreated, IsVerified)
                            VALUES (@SKU, @ProductName, @RetailPrice, 1, 1, 0);
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
                                INSERT INTO Parts (PartNumber, PartName, UnitCost, CurrentStock, IsActive, IsAutoCreated, IsVerified)
                                VALUES (@sku, @name, 0, 0, 1, 1, 0);
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

        // ── Fulfillment: Picking / Packing / Shipping ─────────────────────────────

        /// <summary>
        /// Returns orders in the given statuses, including picking progress counts.
        /// Used by FormPickingDash and FormPackingDash.
        /// </summary>
        public List<Models.FulfillmentOrder> GetFulfillmentOrders(params string[] statuses)
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            return db.Query<Models.FulfillmentOrder>(@"
                SELECT so.SalesOrderID,
                       so.OrderNumber,
                       c.FullName                    AS CustomerName,
                       c.Email                       AS ContactEmail,
                       so.TotalPrice,
                       ISNULL(so.Currency, 'CAD')    AS Currency,
                       so.Status,
                       so.Notes,
                       so.TrackingNumber,
                       so.Carrier,
                       so.ShippedAt,
                       so.ShippedBy,
                       so.PackedAt,
                       so.PackedBy,
                       (SELECT COUNT(*)
                        FROM   SalesOrderItems soi
                        WHERE  soi.SalesOrderID = so.SalesOrderID)                      AS ItemCount,
                       (SELECT COUNT(*)
                        FROM   SalesOrderItems soi
                        WHERE  soi.SalesOrderID = so.SalesOrderID
                          AND  ISNULL(soi.PickedQty, 0) >= soi.Quantity)                AS PickedCount
                FROM   SalesOrders so
                JOIN   Customers   c ON c.CustomerID = so.CustomerID
                WHERE  so.Status IN @statuses
                ORDER  BY so.OrderDate ASC",
                new { statuses }).ToList();
        }

        /// <summary>
        /// Returns line items for a sales order with picking progress and the
        /// location(s) from which the item should be picked (from StockReservations).
        /// </summary>
        public List<Models.SalesOrderItem> GetOrderItemsWithPicking(int salesOrderId)
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            return db.Query<Models.SalesOrderItem>(@"
                SELECT soi.SalesOrderItemID,
                       soi.SalesOrderID,
                       soi.ProductID,
                       soi.SKU,
                       soi.Title,
                       soi.Quantity,
                       soi.UnitPrice,
                       ISNULL(soi.PickedQty, 0)           AS PickedQty,
                       soi.PickedBy,
                       soi.PickedAt,
                       ISNULL(res.PickLocations, '—')      AS PickLocation
                FROM   SalesOrderItems soi
                OUTER APPLY (
                    SELECT STRING_AGG(ISNULL(l.LocationName, 'Default'), ', ') AS PickLocations
                    FROM   StockReservations sr
                    LEFT JOIN Locations l ON l.LocationID = sr.LocationID
                    WHERE  sr.SalesOrderID = soi.SalesOrderID
                      AND  sr.ProductID   = soi.ProductID
                ) res
                WHERE  soi.SalesOrderID = @id
                ORDER  BY soi.SalesOrderItemID",
                new { id = salesOrderId }).ToList();
        }

        /// <summary>Updates the picked quantity for a single line item.</summary>
        public void UpdatePickedQty(int salesOrderItemId, int pickedQty, string pickedBy)
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            db.Execute(@"
                UPDATE SalesOrderItems
                SET    PickedQty = @pickedQty,
                       PickedBy  = @pickedBy,
                       PickedAt  = GETDATE()
                WHERE  SalesOrderItemID = @id",
                new { pickedQty, pickedBy, id = salesOrderItemId });
        }

        /// <summary>
        /// Records shipment details, stamps ShippedAt/ShippedBy, moves the order to
        /// Shipped, and releases any remaining stock reservations.
        /// </summary>
        public void RecordShipment(int salesOrderId, string? trackingNumber, string? carrier)
        {
            using var db = new SqlConnection(_connectionString);
            db.Open();
            using var tx = db.BeginTransaction();
            try
            {
                string shippedBy = Security.AppSession.CurrentUser?.Username ?? "system";
                db.Execute(@"
                    UPDATE SalesOrders
                    SET    Status         = 'Shipped',
                           TrackingNumber = @trackingNumber,
                           Carrier        = @carrier,
                           ShippedAt      = GETDATE(),
                           ShippedBy      = @shippedBy
                    WHERE  SalesOrderID = @id",
                    new { trackingNumber, carrier, shippedBy, id = salesOrderId }, tx);

                db.Execute("DELETE FROM StockReservations WHERE SalesOrderID = @id",
                    new { id = salesOrderId }, tx);

                tx.Commit();
                AppLogger.Audit(shippedBy, "OrderShipped",
                    $"OrderID={salesOrderId} Tracking={trackingNumber} Carrier={carrier}");
            }
            catch { tx.Rollback(); throw; }
        }

        /// <summary>
        /// Deducts inventory and moves the order to Complete.
        /// Delegates to UpdateOrderStatus so the existing deduction + audit logic runs once.
        /// </summary>
        public bool MarkComplete(int salesOrderId) => UpdateOrderStatus(salesOrderId, "Complete");
    }
}
