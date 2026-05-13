using System.Configuration;
using System.Data;
using Dapper;
using JaneERP.Interfaces;
using JaneERP.Models;
using Microsoft.Data.SqlClient;

namespace JaneERP.Manufacturing
{
    public class ManufacturingRepository : IManufacturingRepository
    {
        private readonly string _connectionString =
            ConfigurationManager.ConnectionStrings["MyERP"]?.ConnectionString
            ?? throw new InvalidOperationException("Connection string 'MyERP' not found in App.config.");

        public void EnsureSchema()
        {
            using IDbConnection db = new SqlConnection(_connectionString);

            db.Execute(@"
                IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='ManufacturingOrders' AND xtype='U')
                CREATE TABLE ManufacturingOrders (
                    MOID      INT IDENTITY(1,1) PRIMARY KEY,
                    MONumber  NVARCHAR(50)  NOT NULL UNIQUE,
                    Status    NVARCHAR(20)  NOT NULL DEFAULT 'Open',
                    CreatedAt DATETIME      NOT NULL DEFAULT GETDATE(),
                    Notes     NVARCHAR(500) NULL,
                    OrderedBy NVARCHAR(100) NULL
                );

                IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='WorkOrders' AND xtype='U')
                CREATE TABLE WorkOrders (
                    WorkOrderID    INT IDENTITY(1,1) PRIMARY KEY,
                    MOID           INT           NOT NULL REFERENCES ManufacturingOrders(MOID),
                    ProductID      INT           NOT NULL REFERENCES Products(ProductID),
                    Quantity       INT           NOT NULL,
                    Status         NVARCHAR(20)  NOT NULL DEFAULT 'Pending',
                    Notes          NVARCHAR(500) NULL,
                    CompletedAt    DATETIME      NULL,
                    ShopifyOrderID BIGINT        NULL,
                    CostOfGoods    DECIMAL(18,2) NULL
                );");

            // Migration: add CostOfGoods if WorkOrders existed before this column
            try
            {
                db.Execute(@"
                    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('WorkOrders') AND name = 'CostOfGoods')
                        ALTER TABLE WorkOrders ADD CostOfGoods DECIMAL(18,2) NULL;
                    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('WorkOrders') AND name = 'CompletedQty')
                        ALTER TABLE WorkOrders ADD CompletedQty INT NOT NULL DEFAULT 0;
                    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('WorkOrders') AND name = 'ScrapQty')
                        ALTER TABLE WorkOrders ADD ScrapQty INT NOT NULL DEFAULT 0;
                    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('WorkOrders') AND name = 'AssignedTo')
                        ALTER TABLE WorkOrders ADD AssignedTo NVARCHAR(100) NULL;");
            }
            catch (Exception ex) { Logging.AppLogger.Info($"ManufacturingSchema migration: {ex.Message}"); }

            // Parts reservations: soft-locks created when a WO goes InProgress, released on Complete
            try
            {
                db.Execute(@"
                    IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='PartsReservations' AND xtype='U')
                    CREATE TABLE PartsReservations (
                        ReservationID INT IDENTITY(1,1) PRIMARY KEY,
                        WorkOrderID   INT NOT NULL REFERENCES WorkOrders(WorkOrderID),
                        PartID        INT NOT NULL REFERENCES Parts(PartID),
                        Quantity      INT NOT NULL,
                        CreatedAt     DATETIME NOT NULL DEFAULT GETDATE()
                    )");
            }
            catch (Exception ex) { Logging.AppLogger.Info($"PartsReservations migration: {ex.Message}"); }
        }

        // ── Manufacturing Orders ──────────────────────────────────────────────────

        public List<ManufacturingOrder> GetOrders(bool openOnly = false)
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            string filter = openOnly ? "WHERE Status <> 'Complete'" : "";
            var orders = db.Query<ManufacturingOrder>(
                $"SELECT * FROM ManufacturingOrders {filter} ORDER BY CreatedAt DESC").ToList();

            if (orders.Count > 0)
            {
                var wos = db.Query<WorkOrder>(@"
                    SELECT wo.*, p.ProductName, p.SKU
                    FROM   WorkOrders wo
                    JOIN   Products   p ON p.ProductID = wo.ProductID
                    WHERE  wo.MOID IN @ids",
                    new { ids = orders.Select(o => o.MOID) }).ToList();

                foreach (var mo in orders)
                    mo.WorkOrders = wos.Where(w => w.MOID == mo.MOID).ToList();
            }

            return orders;
        }

        public ManufacturingOrder? GetOrder(int moid)
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            var mo = db.QueryFirstOrDefault<ManufacturingOrder>(
                "SELECT * FROM ManufacturingOrders WHERE MOID = @moid", new { moid });
            if (mo == null) return null;

            mo.WorkOrders = db.Query<WorkOrder>(@"
                SELECT wo.*, p.ProductName, p.SKU
                FROM   WorkOrders wo
                JOIN   Products   p ON p.ProductID = wo.ProductID
                WHERE  wo.MOID = @moid", new { moid }).ToList();

            return mo;
        }

        public int CreateOrder(ManufacturingOrder mo)
        {
            using var db = new SqlConnection(_connectionString);
            db.Open();
            using var tx = db.BeginTransaction();
            try
            {
                // Auto-generate MO number if not set
                if (string.IsNullOrWhiteSpace(mo.MONumber))
                {
                    int next = db.ExecuteScalar<int>(
                        "SELECT ISNULL(MAX(MOID),0)+1 FROM ManufacturingOrders", transaction: tx);
                    mo.MONumber = $"MO-{next:D4}";
                }

                int moid = db.QuerySingle<int>(@"
                    INSERT INTO ManufacturingOrders (MONumber, Status, Notes, OrderedBy)
                    VALUES (@MONumber, @Status, @Notes, @OrderedBy);
                    SELECT CAST(SCOPE_IDENTITY() AS INT);", mo, tx);

                foreach (var wo in mo.WorkOrders)
                {
                    wo.MOID = moid;
                    db.Execute(@"
                        INSERT INTO WorkOrders (MOID, ProductID, Quantity, Status, Notes, ShopifyOrderID)
                        VALUES (@MOID, @ProductID, @Quantity, @Status, @Notes, @ShopifyOrderID);",
                        wo, tx);

                    // Parts are deducted at WO completion, not at MO creation.
                }

                tx.Commit();
                return moid;
            }
            catch { tx.Rollback(); throw; }
        }

        public void UpdateOrderStatus(int moid, string status)
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            db.Execute("UPDATE ManufacturingOrders SET Status = @status WHERE MOID = @moid",
                new { status, moid });
        }

        // ── Work Orders ───────────────────────────────────────────────────────────

        public List<WorkOrder> GetPendingWorkOrders(DateTime? from = null, DateTime? to = null)
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            string dateFilter = "";
            if (from.HasValue) dateFilter += " AND wo.CreatedAt >= @from";
            if (to.HasValue)   dateFilter += " AND wo.CreatedAt <= @to";
            return db.Query<WorkOrder>($@"
                SELECT wo.*, p.ProductName, p.SKU
                FROM   WorkOrders wo
                JOIN   Products   p ON p.ProductID = wo.ProductID
                WHERE  wo.Status <> 'Complete'
                {dateFilter}
                ORDER  BY wo.WorkOrderID",
                new { from, to = to?.AddDays(1).AddTicks(-1) }).ToList();
        }

        /// <summary>
        /// Marks a work order Complete and atomically adds the finished-goods inventory transaction.
        /// Also deducts BOM parts from inventory if the WorkOrder has BOM line items.
        /// </summary>
        public void CompleteWorkOrder(int workOrderId, string? notes = null)
        {
            using var db = new SqlConnection(_connectionString);
            db.Open();
            using var tx = db.BeginTransaction();
            try
            {
                // Get the work order so we know ProductID + Quantity
                var wo = db.QueryFirstOrDefault(
                    "SELECT WorkOrderID, ProductID, Quantity FROM WorkOrders WHERE WorkOrderID = @workOrderId",
                    new { workOrderId }, tx)
                    ?? throw new InvalidOperationException($"Work order {workOrderId} not found.");

                int productId = (int)wo.ProductID;
                int quantity  = (int)wo.Quantity;
                var now       = DateTime.Now;

                // 1. Mark complete
                db.Execute(@"
                    UPDATE WorkOrders
                    SET Status = 'Complete', CompletedAt = @now, Notes = ISNULL(@notes, Notes)
                    WHERE WorkOrderID = @workOrderId",
                    new { workOrderId, now, notes }, tx);

                // 2. Add finished-goods stock
                db.Execute(@"
                    INSERT INTO InventoryTransactions (ProductID, QuantityChange, TransactionType, Notes, TransactionDate)
                    VALUES (@ProductID, @Qty, 'ManufacturingIn', @Notes, @Date);",
                    new
                    {
                        ProductID = productId,
                        Qty       = quantity,
                        Notes     = $"Completed Work Order #{workOrderId}" + (string.IsNullOrWhiteSpace(notes) ? "" : $" — {notes}"),
                        Date      = now
                    }, tx);

                // 2b. Release any parts reservations for this WO, then deduct BOM parts
                db.Execute("DELETE FROM PartsReservations WHERE WorkOrderID = @workOrderId",
                    new { workOrderId }, tx);

                var deductItems = db.Query(
                    "SELECT pp.PartID, pp.Quantity FROM ProductParts pp WHERE pp.ProductID = @productId",
                    new { productId }, tx).ToList();
                foreach (var bom in deductItems)
                {
                    int deduct = (int)Math.Round((decimal)bom.Quantity * quantity, MidpointRounding.AwayFromZero);
                    if (deduct > 0)
                        db.Execute(
                            "UPDATE Parts SET CurrentStock = CurrentStock - @deduct WHERE PartID = @partId",
                            new { deduct, partId = (int)bom.PartID }, tx);
                }

                // 3. Calculate COGS from BOM parts + labour
                var bomItems = db.Query(
                    "SELECT pp.PartID, pp.Quantity, ISNULL(p.UnitCost, 0) AS UnitCost " +
                    "FROM ProductParts pp JOIN Parts p ON p.PartID = pp.PartID " +
                    "WHERE pp.ProductID = @productId",
                    new { productId }, tx).ToList();

                var labourItems = db.Query(
                    "SELECT HourlyRate, Hours FROM BomLabourCosts WHERE ProductID = @productId",
                    new { productId }, tx).ToList();

                decimal partsCogs  = bomItems.Sum(bom => (decimal)bom.UnitCost * (int)bom.Quantity * quantity);
                decimal labourCogs = labourItems.Sum(lc => (decimal)lc.HourlyRate * (decimal)lc.Hours * quantity);
                decimal totalCogs  = partsCogs + labourCogs;

                // 4. Record COGS on the work order
                db.Execute(
                    "UPDATE WorkOrders SET CostOfGoods = @cogs WHERE WorkOrderID = @workOrderId",
                    new { cogs = totalCogs, workOrderId }, tx);

                // 5. Get the parent MO and check if all its work orders are now complete
                int moid = db.QuerySingle<int>(
                    "SELECT MOID FROM WorkOrders WHERE WorkOrderID = @workOrderId",
                    new { workOrderId }, tx);

                int remaining = db.QuerySingle<int>(
                    "SELECT COUNT(*) FROM WorkOrders WHERE MOID = @moid AND Status <> 'Complete'",
                    new { moid }, tx);

                if (remaining == 0)
                {
                    // All work orders done — close the Manufacturing Order
                    db.Execute(
                        "UPDATE ManufacturingOrders SET Status = 'Complete' WHERE MOID = @moid",
                        new { moid }, tx);

                    // 6. For each SalesOrder linked to this MO via ShopifyOrderID, mark it Complete
                    //    only if ALL work orders for that order (across every MO) are now complete.
                    //    This prevents prematurely completing an order that has other WOs still in progress.
                    db.Execute(@"
                        UPDATE SalesOrders
                        SET    Status = 'Complete'
                        WHERE  Status <> 'Complete'
                          AND  ShopifyOrderID IN (
                                SELECT wo1.ShopifyOrderID
                                FROM   WorkOrders wo1
                                WHERE  wo1.MOID = @moid AND wo1.ShopifyOrderID IS NOT NULL
                                  AND  NOT EXISTS (
                                        SELECT 1 FROM WorkOrders wo2
                                        WHERE  wo2.ShopifyOrderID = wo1.ShopifyOrderID
                                          AND  wo2.Status <> 'Complete'
                                       )
                               )",
                        new { moid }, tx);
                }

                tx.Commit();
            }
            catch { tx.Rollback(); throw; }
        }

        public void UpdateWorkOrderStatus(int workOrderId, string status)
        {
            using var db = new SqlConnection(_connectionString);
            db.Open();
            using var tx = db.BeginTransaction();
            try
            {
                var current = db.ExecuteScalar<string>(
                    "SELECT Status FROM WorkOrders WHERE WorkOrderID = @workOrderId",
                    new { workOrderId }, tx);

                if (current == null)
                    throw new InvalidOperationException($"Work order {workOrderId} not found.");

                // Reverting to Pending releases any parts reservations that were created on start
                if (status == "Pending" && current == "InProgress")
                {
                    db.Execute("DELETE FROM PartsReservations WHERE WorkOrderID = @workOrderId",
                        new { workOrderId }, tx);
                }

                db.Execute("UPDATE WorkOrders SET Status = @status WHERE WorkOrderID = @workOrderId",
                    new { status, workOrderId }, tx);
                tx.Commit();
            }
            catch { tx.Rollback(); throw; }
        }

        /// <summary>
        /// Builds the list of reservation lines for the parts-lock dialog when a WO goes InProgress.
        /// Returns one row per BOM part, showing current stock and reservations held by other WOs.
        /// </summary>
        public List<Models.ReservationLine> GetWOReservationItems(int workOrderId)
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            var rows = db.Query(@"
                SELECT
                    pp.PartID,
                    pt.PartNumber,
                    pt.PartName,
                    CAST(CEILING(pp.Quantity * wo.Quantity) AS INT) AS Required,
                    pt.CurrentStock AS OnHand,
                    ISNULL((
                        SELECT SUM(pr.Quantity)
                        FROM   PartsReservations pr
                        WHERE  pr.PartID      = pp.PartID
                          AND  pr.WorkOrderID <> @workOrderId
                    ), 0) AS AlreadyReserved
                FROM  WorkOrders   wo
                JOIN  ProductParts pp ON pp.ProductID = wo.ProductID
                JOIN  Parts        pt ON pt.PartID    = pp.PartID
                WHERE wo.WorkOrderID = @workOrderId
                ORDER BY pt.PartName",
                new { workOrderId }).ToList();

            return rows.Select(row =>
            {
                int req   = (int)row.Required;
                int avail = Math.Max(0, (int)row.OnHand - (int)row.AlreadyReserved);
                return new Models.ReservationLine
                {
                    ItemId          = (int)row.PartID,
                    LocationId      = null,
                    DisplayLabel    = $"{row.PartNumber} — {row.PartName}",
                    LocationName    = "—",
                    Required        = req,
                    OnHand          = (int)row.OnHand,
                    AlreadyReserved = (int)row.AlreadyReserved,
                    ToLock          = Math.Min(req, avail)
                };
            }).ToList();
        }

        /// <summary>
        /// Persists the parts-reservation choices made in the lock dialog for a Work Order.
        /// Replaces any prior reservations for this WO.
        /// </summary>
        public void SaveWOReservations(int workOrderId, IEnumerable<Models.ReservationLine> lines)
        {
            using var db = new SqlConnection(_connectionString);
            db.Open();
            using var tx = db.BeginTransaction();
            try
            {
                db.Execute("DELETE FROM PartsReservations WHERE WorkOrderID = @workOrderId",
                    new { workOrderId }, tx);

                foreach (var line in lines.Where(l => l.ToLock > 0))
                {
                    db.Execute(@"
                        INSERT INTO PartsReservations (WorkOrderID, PartID, Quantity)
                        VALUES (@WorkOrderID, @PartID, @Quantity)",
                        new
                        {
                            WorkOrderID = workOrderId,
                            PartID      = line.ItemId,
                            Quantity    = line.ToLock
                        }, tx);
                }

                tx.Commit();

                Logging.AppLogger.Audit(Security.AppSession.CurrentUser?.Username ?? "system",
                    "PartsReserved",
                    $"WorkOrderID={workOrderId} parts={lines.Count(l => l.ToLock > 0)}");
            }
            catch { tx.Rollback(); throw; }
        }

        /// <summary>
        /// Returns the BOM parts for a work order that currently have zero or negative stock.
        /// Used to warn the user before completing a work order.
        /// </summary>
        /// <summary>Returns BOM parts where current stock is below what's needed for this work order.</summary>
        public List<NegativePartInfo> GetNegativePartsForWorkOrder(int workOrderId)
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            return db.Query<NegativePartInfo>(@"
                SELECT pt.PartName,
                       pt.CurrentStock,
                       CAST(CEILING(pp.Quantity * wo.Quantity) AS INT) AS RequiredQty,
                       CAST(CEILING(pp.Quantity * wo.Quantity) AS INT) - pt.CurrentStock AS ShortageQty
                FROM   WorkOrders wo
                JOIN   ProductParts pp ON pp.ProductID = wo.ProductID
                JOIN   Parts        pt ON pt.PartID    = pp.PartID
                WHERE  wo.WorkOrderID = @workOrderId
                  AND  pt.CurrentStock < CAST(CEILING(pp.Quantity * wo.Quantity) AS INT)",
                new { workOrderId }).ToList();
        }

        /// <summary>Assigns (or unassigns) a work order to a user.</summary>
        public void AssignWorkOrder(int workOrderId, string? assignedTo)
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            db.Execute("UPDATE WorkOrders SET AssignedTo = @assignedTo WHERE WorkOrderID = @workOrderId",
                new { workOrderId, assignedTo });
        }

        /// <summary>
        /// Returns a dictionary of PartID → total reserved quantity across all active work orders.
        /// Used to show available-minus-reserved stock in the parts manager.
        /// </summary>
        public Dictionary<int, int> GetReservedPartsQty()
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            return db.Query<(int PartID, int Qty)>(@"
                SELECT pr.PartID, SUM(pr.Quantity) AS Qty
                FROM   PartsReservations pr
                JOIN   WorkOrders wo ON wo.WorkOrderID = pr.WorkOrderID
                WHERE  wo.Status NOT IN ('Completed', 'Cancelled')
                GROUP BY pr.PartID")
                .ToDictionary(r => r.PartID, r => r.Qty);
        }

        /// <summary>
        /// Partially completes a work order: records completed and scrap quantities,
        /// deducts parts proportionally, and marks the WO Completed if fully done.
        /// </summary>
        public void PartialCompleteWorkOrder(int workOrderId, int completedQty, int scrapQty = 0,
            string? scrapReason = null, string? notes = null)
        {
            using var db = new SqlConnection(_connectionString);
            db.Open();
            using var tx = db.BeginTransaction();
            try
            {
                var wo = db.QueryFirstOrDefault<WorkOrder>(
                    "SELECT * FROM WorkOrders WHERE WorkOrderID = @workOrderId",
                    new { workOrderId }, tx)
                    ?? throw new InvalidOperationException($"Work order {workOrderId} not found.");

                int totalDone = completedQty + scrapQty;

                // Deduct parts from stock proportionally to units completed+scrapped
                var bom = db.Query(@"
                    SELECT pp.PartID, CAST(CEILING(pp.Quantity * @totalDone) AS INT) AS QtyToDeduct
                    FROM   ProductParts pp
                    WHERE  pp.ProductID = @productId",
                    new { totalDone, productId = wo.ProductID }, tx).ToList();

                foreach (var part in bom)
                    db.Execute("UPDATE Parts SET CurrentStock = CurrentStock - @qty WHERE PartID = @partId",
                        new { qty = (int)part.QtyToDeduct, partId = (int)part.PartID }, tx);

                // Add inventory transaction for finished goods
                if (completedQty > 0)
                    db.Execute(@"
                        INSERT INTO InventoryTransactions (ProductID, QuantityChange, TransactionType, Notes, TransactionDate)
                        VALUES (@ProductID, @completedQty, 'WorkOrderComplete', @notes, GETDATE())",
                        new { wo.ProductID, completedQty, notes }, tx);

                // Release reservations for this WO
                db.Execute("DELETE FROM PartsReservations WHERE WorkOrderID = @workOrderId",
                    new { workOrderId }, tx);

                // Mark completed
                db.Execute(@"
                    UPDATE WorkOrders
                    SET Status = 'Completed', CompletedAt = GETDATE(),
                        CompletedQty = ISNULL(CompletedQty,0) + @completedQty,
                        ScrapQty = ISNULL(ScrapQty,0) + @scrapQty,
                        Notes = ISNULL(@notes, Notes)
                    WHERE WorkOrderID = @workOrderId",
                    new { completedQty, scrapQty, notes, workOrderId }, tx);

                tx.Commit();

                Logging.AppLogger.Audit(Security.AppSession.CurrentUser?.Username ?? "system",
                    "PartialCompleteWO",
                    $"WorkOrderID={workOrderId} completed={completedQty} scrap={scrapQty}");
            }
            catch { tx.Rollback(); throw; }
        }
    }

    public class NegativePartInfo
    {
        public string PartName     { get; set; } = "";
        public int    CurrentStock { get; set; }
        public int    RequiredQty  { get; set; }
        public int    ShortageQty  { get; set; }
    }
}
