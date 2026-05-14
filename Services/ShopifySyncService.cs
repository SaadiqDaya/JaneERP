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
            Migrate(db, "IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('SalesOrders') AND name='CancelledAt') ALTER TABLE SalesOrders ADD CancelledAt DATETIME NULL");
            Migrate(db, "IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('SalesOrders') AND name='PaymentStatus') ALTER TABLE SalesOrders ADD PaymentStatus NVARCHAR(50) NULL");
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
        /// Updates the Status of a SalesOrder, enforcing business rules:
        /// – Complete and Cancelled are terminal (cannot transition out).
        /// – Reverting to Draft or Cancelled from an active status releases reservations and
        ///   reverses any previously-recorded InventoryTransactions (if InventoryAffected=1),
        ///   then resets InventoryAffected=0 so the order can be re-processed.
        /// – Going to Shipped (primary) or Complete (fallback) deducts inventory once;
        ///   a hard stock-sufficiency check blocks the transition if stock is short.
        /// Returns true if the row was found and updated.
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
                bool   wasAffected   = (bool)order.InventoryAffected;

                // Terminal states: Complete and Cancelled cannot be transitioned out of
                if (currentStatus is "Complete" or "Cancelled")
                {
                    tx.Rollback();
                    throw new InvalidOperationException(
                        $"Order #{order.OrderNumber} is {currentStatus} and cannot be changed.");
                }

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

                // Record cancellation timestamp
                if (newStatus == "Cancelled")
                    db.Execute(
                        "UPDATE SalesOrders SET CancelledAt = GETDATE() WHERE SalesOrderID = @id",
                        new { id = salesOrderId }, tx);

                // Release stock reservations on terminal or revert transitions
                var activeStatuses = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    { "Live", "Picking", "Packing", "Shipped", "WIP" };
                bool releaseReservations =
                    newStatus is "Complete" or "Shipped" or "Cancelled" ||
                    (newStatus == "Draft" && activeStatuses.Contains(currentStatus));
                if (releaseReservations)
                    db.Execute("DELETE FROM StockReservations WHERE SalesOrderID = @id",
                        new { id = salesOrderId }, tx);

                // Reverse inventory when cancelling or reverting to Draft (if already deducted)
                if ((newStatus is "Draft" or "Cancelled") && wasAffected)
                {
                    var revertItems = db.Query(
                        "SELECT ProductID, Quantity FROM SalesOrderItems WHERE SalesOrderID = @id",
                        new { id = salesOrderId }, tx).ToList();

                    int orderNum = (int)order.OrderNumber;
                    foreach (var li in revertItems)
                    {
                        db.Execute(@"
                            INSERT INTO InventoryTransactions (ProductID, QuantityChange, TransactionType, Notes, TransactionDate)
                            VALUES (@ProductID, @QuantityChange, 'Sale Reversal', @Notes, @TransactionDate);",
                            new
                            {
                                ProductID       = (int)li.ProductID,
                                QuantityChange  = (int)li.Quantity,   // positive — returns stock
                                Notes           = $"Order #{orderNum} → {newStatus} (reversal)",
                                TransactionDate = DateTime.Now
                            }, tx);
                    }

                    db.Execute(
                        "UPDATE SalesOrders SET InventoryAffected = 0 WHERE SalesOrderID = @id",
                        new { id = salesOrderId }, tx);

                    AppLogger.Audit(Security.AppSession.CurrentUser?.Username ?? "system",
                        "InventoryReversed",
                        $"OrderID={salesOrderId} OrderNumber={order.OrderNumber} Reason={newStatus} items={revertItems.Count}");
                }

                // Deduct inventory once when going Shipped (primary trigger) or Complete (fallback
                // for orders that skip the Shipped step, e.g. Draft → Complete directly).
                // The wasAffected flag prevents double-deduction across both paths.
                if ((newStatus is "Shipped" or "Complete") && !wasAffected)
                {
                    var deductItems = db.Query(
                        "SELECT ProductID, Quantity FROM SalesOrderItems WHERE SalesOrderID = @id",
                        new { id = salesOrderId }, tx).ToList();

                    // Hard block: refuse the transition if any item is short on stock
                    var shortages = new List<string>();
                    foreach (var li in deductItems)
                    {
                        int stock = db.ExecuteScalar<int>(
                            "SELECT ISNULL(SUM(QuantityChange), 0) FROM InventoryTransactions WHERE ProductID = @pid",
                            new { pid = (int)li.ProductID }, tx);
                        if (stock < (int)li.Quantity)
                            shortages.Add($"ProductID {li.ProductID}: need {li.Quantity}, have {stock}");
                    }

                    if (shortages.Any())
                    {
                        tx.Rollback();
                        throw new InvalidOperationException(
                            $"Cannot {newStatus.ToLower()} order #{order.OrderNumber}: insufficient stock.\n" +
                            string.Join("\n", shortages));
                    }

                    int orderNum = (int)order.OrderNumber;
                    foreach (var li in deductItems)
                    {
                        db.Execute(@"
                            INSERT INTO InventoryTransactions (ProductID, QuantityChange, TransactionType, Notes, TransactionDate)
                            VALUES (@ProductID, @QuantityChange, 'Sale', @Notes, @TransactionDate);",
                            new
                            {
                                ProductID       = (int)li.ProductID,
                                QuantityChange  = -(int)li.Quantity,
                                Notes           = $"Order #{orderNum} → {newStatus}",
                                TransactionDate = DateTime.Now
                            }, tx);
                    }

                    db.Execute(
                        "UPDATE SalesOrders SET InventoryAffected = 1 WHERE SalesOrderID = @id",
                        new { id = salesOrderId }, tx);

                    AppLogger.Audit(Security.AppSession.CurrentUser?.Username ?? "system",
                        "InventoryDeducted",
                        $"OrderID={salesOrderId} OrderNumber={order.OrderNumber} items={deductItems.Count}");
                }

                tx.Commit();
                return true;
            }
            catch { tx.Rollback(); throw; }
        }

        /// <summary>
        /// Records a payment against a SalesOrder and marks it fully paid when the amount
        /// meets or exceeds the order total. Supports partial payments: pass a specific
        /// <paramref name="amount"/> to record less than the full total — IsPaid stays false
        /// until the full amount is recorded. Pass null to record the full TotalPrice at once.
        /// </summary>
        public void MarkAsPaid(int salesOrderId, string? paymentMethod = null, string? notes = null, decimal? amount = null)
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

                decimal paymentAmount = amount ?? (decimal)order.TotalPrice;
                if (paymentAmount <= 0)
                    throw new InvalidOperationException("Payment amount must be greater than zero.");

                var now = DateTime.Now;

                // Sum all prior payments to determine whether this payment completes the order
                decimal priorPaid = db.ExecuteScalar<decimal>(
                    "SELECT ISNULL(SUM(Amount), 0) FROM CustomerPayments WHERE SalesOrderID = @id",
                    new { id = salesOrderId }, tx);

                bool isNowFullyPaid = (priorPaid + paymentAmount) >= (decimal)order.TotalPrice;

                if (isNowFullyPaid && !(bool)order.IsPaid)
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
                        Amount        = paymentAmount,
                        PaymentMethod = paymentMethod,
                        Notes         = notes,
                        PaidAt        = now
                    }, tx);

                tx.Commit();

                AppLogger.Audit(Security.AppSession.CurrentUser?.Username ?? "system",
                    "PaymentRecorded",
                    $"OrderID={salesOrderId} OrderNumber={order.OrderNumber} Amount={paymentAmount:N2} FullyPaid={isNowFullyPaid}");
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

                // Auto-generate order number using UPDLOCK + HOLDLOCK so concurrent inserts
                // within the same READ COMMITTED transaction isolation level cannot read the
                // same MAX and produce duplicate order numbers.
                var orderNumber = db.ExecuteScalar<int>(
                    "SELECT ISNULL(MAX(OrderNumber), 0) + 1 FROM SalesOrders WITH (UPDLOCK, HOLDLOCK) WHERE ShopifyOrderID IS NULL",
                    null, tx);

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

                // Pass 1: find/create products and insert order items; collect (productId, qty, sku) for inventory
                var inventoryItems = new List<(int productId, int qty, string sku)>();
                foreach (var li in lineItems)
                {
                    var sku = li.Sku.Trim();
                    var productId = db.ExecuteScalar<int?>(
                        "SELECT ProductID FROM Products WHERE SKU = @sku", new { sku }, tx);
                    if (productId == null)
                    {
                        // Placeholder — needs setup via the product review screen (no Part/BOM auto-created).
                        productId = db.QuerySingle<int>(@"
                            INSERT INTO Products (SKU, ProductName, RetailPrice, IsActive, IsAutoCreated, IsVerified)
                            VALUES (@SKU, @ProductName, @RetailPrice, 1, 1, 0);
                            SELECT CAST(SCOPE_IDENTITY() AS INT);",
                            new { SKU = sku, ProductName = li.Title, RetailPrice = li.UnitPrice }, tx);
                        AppLogger.Info($"ManualOrder: queued new product for setup SKU={sku}");
                    }

                    db.Execute(@"
                        INSERT INTO SalesOrderItems (SalesOrderID, ProductID, SKU, Title, Quantity, UnitPrice)
                        VALUES (@SalesOrderID, @ProductID, @SKU, @Title, @Quantity, @UnitPrice);",
                        new { SalesOrderID = salesOrderId, ProductID = productId,
                              SKU = sku, Title = li.Title, Quantity = li.Qty, UnitPrice = li.UnitPrice }, tx);

                    inventoryItems.Add((productId.Value, li.Qty, sku));
                }

                // Pass 2: stock guard then deduction (only for Live orders)
                if (affectsInventory)
                {
                    var shortages = new List<string>();
                    foreach (var (pid, qty, sku) in inventoryItems)
                    {
                        int stock = db.ExecuteScalar<int>(
                            "SELECT ISNULL(SUM(QuantityChange), 0) FROM InventoryTransactions WHERE ProductID = @pid",
                            new { pid }, tx);
                        if (stock < qty)
                            shortages.Add($"SKU {sku}: need {qty}, have {stock}");
                    }

                    if (shortages.Any())
                        throw new InvalidOperationException(
                            $"Insufficient stock for order #{orderNumber}:\n{string.Join("\n", shortages)}");

                    foreach (var (pid, qty, sku) in inventoryItems)
                    {
                        db.Execute(@"
                            INSERT INTO InventoryTransactions (ProductID, QuantityChange, TransactionType, Notes, TransactionDate)
                            VALUES (@ProductID, @QuantityChange, 'Manual Sale', @Notes, @TransactionDate);",
                            new { ProductID = pid, QuantityChange = -qty,
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
                bool isPaid = string.Equals(order.FinancialStatus, "paid", StringComparison.OrdinalIgnoreCase);

                // Map refunded/voided Shopify orders directly to Cancelled in ERP
                bool isCancelled = order.FinancialStatus != null &&
                    (order.FinancialStatus.Equals("refunded", StringComparison.OrdinalIgnoreCase) ||
                     order.FinancialStatus.Equals("voided",   StringComparison.OrdinalIgnoreCase));

                string    erpStatus     = isCancelled ? "Cancelled" : "Live";
                DateTime? cancelledAt   = isCancelled ? order.CreatedAt : (DateTime?)null;
                string?   paymentStatus = order.FinancialStatus; // preserve raw Shopify value (partially_paid etc.)

                var salesOrderId = db.QuerySingle<int>(@"
                    INSERT INTO SalesOrders (ShopifyOrderID, OrderNumber, CustomerID, StoreID, OrderDate, TotalPrice, Currency, Status, InventoryAffected, OrderType, IsPaid, PaidAt, PaymentGateway, PaymentStatus, CancelledAt)
                    VALUES (@ShopifyOrderID, @OrderNumber, @CustomerID, @StoreID, @OrderDate, @TotalPrice, @Currency, @Status, 0, 'Shopify', @IsPaid, @PaidAt, @PaymentGateway, @PaymentStatus, @CancelledAt);
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
                        Status         = erpStatus,
                        IsPaid         = isPaid,
                        PaidAt         = isPaid ? (DateTime?)order.CreatedAt : null,
                        PaymentGateway = order.PaymentGateway,
                        PaymentStatus  = paymentStatus,
                        CancelledAt    = cancelledAt
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
                        // Create a placeholder product so the order line item can be saved.
                        // IsAutoCreated=1 / IsVerified=0 signals it needs full setup before use.
                        // Part and BOM are NOT auto-created here — the user must configure the
                        // product source (Part/BOM/Package) via the product setup screen.
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

                        AppLogger.Info($"ShopifySync: queued new product for setup SKU={sku}");
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

        public int GetUnverifiedProductCount()
        {
            try
            {
                using IDbConnection db = new SqlConnection(_connectionString);
                return db.ExecuteScalar<int>(
                    "SELECT COUNT(1) FROM Products WHERE IsAutoCreated = 1 AND IsVerified = 0 AND IsActive = 1");
            }
            catch { return 0; }
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
        /// Records shipment details, stamps ShippedAt/ShippedBy, moves the order to Shipped,
        /// releases stock reservations, and deducts inventory in a single transaction.
        /// Throws if the order is already Complete or Cancelled, or if stock is insufficient.
        /// </summary>
        public void RecordShipment(int salesOrderId, string? trackingNumber, string? carrier)
        {
            string shippedBy = Security.AppSession.CurrentUser?.Username ?? "system";

            using var db = new SqlConnection(_connectionString);
            db.Open();
            using var tx = db.BeginTransaction();
            try
            {
                var order = db.QueryFirstOrDefault(
                    "SELECT SalesOrderID, OrderNumber, Status, InventoryAffected FROM SalesOrders WHERE SalesOrderID = @id",
                    new { id = salesOrderId }, tx)
                    ?? throw new InvalidOperationException("Order not found.");

                string currentStatus = (string)order.Status;
                bool   wasAffected   = (bool)order.InventoryAffected;

                if (currentStatus is "Complete" or "Cancelled")
                    throw new InvalidOperationException(
                        $"Order #{order.OrderNumber} is {currentStatus} and cannot be changed.");

                // Update status + shipping metadata atomically
                db.Execute(@"
                    UPDATE SalesOrders
                    SET    Status         = 'Shipped',
                           TrackingNumber = @trackingNumber,
                           Carrier        = @carrier,
                           ShippedAt      = GETDATE(),
                           ShippedBy      = @shippedBy
                    WHERE  SalesOrderID = @id",
                    new { trackingNumber, carrier, shippedBy, id = salesOrderId }, tx);

                // Release stock reservations
                db.Execute("DELETE FROM StockReservations WHERE SalesOrderID = @id",
                    new { id = salesOrderId }, tx);

                // Deduct inventory once if not already done
                if (!wasAffected)
                {
                    var items = db.Query(
                        "SELECT ProductID, Quantity FROM SalesOrderItems WHERE SalesOrderID = @id",
                        new { id = salesOrderId }, tx).ToList();

                    // Hard block on stock shortage
                    var shortages = new List<string>();
                    foreach (var li in items)
                    {
                        int stock = db.ExecuteScalar<int>(
                            "SELECT ISNULL(SUM(QuantityChange), 0) FROM InventoryTransactions WHERE ProductID = @pid",
                            new { pid = (int)li.ProductID }, tx);
                        if (stock < (int)li.Quantity)
                            shortages.Add($"ProductID {li.ProductID}: need {li.Quantity}, have {stock}");
                    }

                    if (shortages.Any())
                        throw new InvalidOperationException(
                            $"Cannot ship order #{order.OrderNumber}: insufficient stock.\n" +
                            string.Join("\n", shortages));

                    int orderNum = (int)order.OrderNumber;
                    foreach (var li in items)
                    {
                        db.Execute(@"
                            INSERT INTO InventoryTransactions (ProductID, QuantityChange, TransactionType, Notes, TransactionDate)
                            VALUES (@ProductID, @QuantityChange, 'Sale', @Notes, @TransactionDate);",
                            new
                            {
                                ProductID       = (int)li.ProductID,
                                QuantityChange  = -(int)li.Quantity,
                                Notes           = $"Order #{orderNum} → Shipped",
                                TransactionDate = DateTime.Now
                            }, tx);
                    }

                    db.Execute(
                        "UPDATE SalesOrders SET InventoryAffected = 1 WHERE SalesOrderID = @id",
                        new { id = salesOrderId }, tx);

                    AppLogger.Audit(shippedBy, "InventoryDeducted",
                        $"OrderID={salesOrderId} OrderNumber={order.OrderNumber} items={items.Count}");
                }

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
