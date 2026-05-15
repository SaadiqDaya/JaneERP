using System.Configuration;
using System.Data;
using Dapper;
using JaneERP.Core.Models;
using JaneERP.Core.Services;
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

            // Lot tracking and new WO lifecycle columns (WorkOrderService owns the DDL)
            try { WorkOrderService.EnsureSchema(_connectionString); }
            catch (Exception ex) { Logging.AppLogger.Info($"WorkOrderService schema: {ex.Message}"); }

            // Cook session tables: ingredient-first batch cooking workflow
            try
            {
                db.Execute(@"
                    IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='CookSessions' AND xtype='U')
                    CREATE TABLE CookSessions (
                        CookSessionID INT IDENTITY(1,1) PRIMARY KEY,
                        SessionName   NVARCHAR(100)  NOT NULL,
                        Status        NVARCHAR(20)   NOT NULL DEFAULT 'Open',
                        CreatedBy     NVARCHAR(100)  NULL,
                        CreatedAt     DATETIME       NOT NULL DEFAULT GETDATE(),
                        CompletedAt   DATETIME       NULL
                    );

                    IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='CookSessionBatches' AND xtype='U')
                    CREATE TABLE CookSessionBatches (
                        CookSessionID INT NOT NULL REFERENCES CookSessions(CookSessionID) ON DELETE CASCADE,
                        WorkOrderID   INT NOT NULL REFERENCES WorkOrders(WorkOrderID),
                        PRIMARY KEY (CookSessionID, WorkOrderID)
                    );

                    IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='CookSessionSteps' AND xtype='U')
                    CREATE TABLE CookSessionSteps (
                        StepID        INT IDENTITY(1,1) PRIMARY KEY,
                        CookSessionID INT NOT NULL REFERENCES CookSessions(CookSessionID) ON DELETE CASCADE,
                        WorkOrderID   INT NOT NULL REFERENCES WorkOrders(WorkOrderID),
                        PartID        INT NOT NULL REFERENCES Parts(PartID),
                        IsDone        BIT           NOT NULL DEFAULT 0,
                        DoneBy        NVARCHAR(100) NULL,
                        DoneAt        DATETIME      NULL,
                        UNIQUE (CookSessionID, WorkOrderID, PartID)
                    )");

                // ── Cook schema migrations ──────────────────────────────────────────
                db.Execute(@"
                    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('CookSessions') AND name = 'BatchLossPercent')
                        ALTER TABLE CookSessions ADD BatchLossPercent DECIMAL(5,2) NOT NULL DEFAULT 0;

                    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('CookSessionBatches') AND name = 'FlaskType')
                        ALTER TABLE CookSessionBatches ADD FlaskType NVARCHAR(50) NULL;

                    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('CookSessionBatches') AND name = 'BatchSizeML')
                        ALTER TABLE CookSessionBatches ADD BatchSizeML DECIMAL(12,3) NULL;

                    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('CookSessionSteps') AND name = 'RequiredQtyML')
                        ALTER TABLE CookSessionSteps ADD RequiredQtyML DECIMAL(12,3) NULL;

                    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('CookSessions') AND name = 'UpdatedBy')
                        ALTER TABLE CookSessions ADD UpdatedBy NVARCHAR(100) NULL;

                    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('CookSessions') AND name = 'UpdatedAt')
                        ALTER TABLE CookSessions ADD UpdatedAt DATETIME NULL;

                    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('CookSessions') AND name = 'CompletedBy')
                        ALTER TABLE CookSessions ADD CompletedBy NVARCHAR(100) NULL;");
            }
            catch (Exception ex) { Logging.AppLogger.Info($"CookSession migration: {ex.Message}"); }
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
            catch (Exception ex)
            {
                tx.Rollback();
                Logging.AppLogger.Error($"[CreateOrder] Transaction rolled back for MO={mo.MONumber}: {ex}");
                throw;
            }
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
        /// Marks a work order Complete. Delegates to WorkOrderService for lot deduction,
        /// inventory transactions, COGS, and cascade logic.
        /// </summary>
        public void CompleteWorkOrder(int workOrderId, string? notes = null)
        {
            var wo = GetWorkOrderBasic(workOrderId);
            var svc = new WorkOrderService(_connectionString);
            svc.Complete(workOrderId, new CompleteWorkOrderRequest
            {
                CompletedQty = wo?.Quantity ?? 0,
                ScrapQty     = 0,
                Notes        = notes,
            }, Security.AppSession.CurrentUser?.Username ?? "system");
        }

        private dynamic? GetWorkOrderBasic(int workOrderId)
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            return db.QueryFirstOrDefault(
                "SELECT Quantity, ProductID FROM WorkOrders WHERE WorkOrderID = @workOrderId",
                new { workOrderId });
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
            catch (Exception ex)
            {
                tx.Rollback();
                Logging.AppLogger.Error($"[UpdateWorkOrderStatus] Transaction rolled back for WorkOrderID={workOrderId}: {ex}");
                throw;
            }
        }

        /// <summary>
        /// Builds the reservation-line list for the Go Live dialog.
        /// Returns one row per available lot (FEFO sorted); parts with no lots get a single unlotted row.
        /// </summary>
        public List<Models.ReservationLine> GetWOReservationItems(int workOrderId)
        {
            var svc   = new WorkOrderService(_connectionString);
            var parts = svc.GetLotAvailability(workOrderId);
            var lines = new List<Models.ReservationLine>();

            foreach (var part in parts)
            {
                int remaining = part.Required;
                foreach (var lot in part.Lots)
                {
                    int toLock = Math.Min(Math.Max(0, remaining), lot.Available);
                    string expiry = lot.ExpirationDate.HasValue
                        ? $" | Exp: {lot.ExpirationDate.Value:yyyy-MM-dd}" : "";
                    string lotLabel = lot.LotID > 0
                        ? $" [{lot.LotNumber ?? "No#"}{expiry}]" : " [Unlotted]";

                    lines.Add(new Models.ReservationLine
                    {
                        ItemId          = part.PartID,
                        LotID           = lot.LotID,
                        LotNumber       = lot.LotNumber,
                        ExpirationDate  = lot.ExpirationDate,
                        LocationId      = lot.LocationID,
                        DisplayLabel    = $"{part.PartNumber} — {part.PartName}{lotLabel}",
                        LocationName    = lot.LocationName,
                        Required        = part.Required,
                        OnHand          = lot.TotalQty,
                        AlreadyReserved = lot.AlreadyReserved,
                        ToLock          = toLock,
                    });
                    remaining -= toLock;
                }
            }

            return lines;
        }

        /// <summary>
        /// Persists reservation choices and transitions the WO Pending → Live.
        /// Delegates to WorkOrderService.GoLive() so lot deductions are recorded.
        /// </summary>
        public void SaveWOReservations(int workOrderId, IEnumerable<Models.ReservationLine> lines)
        {
            var reservations = lines
                .Where(l => l.ToLock > 0)
                .Select(l => new LotReservation
                {
                    LotID    = l.LotID,
                    PartID   = l.ItemId,
                    Quantity = l.ToLock,
                });

            var svc = new WorkOrderService(_connectionString);
            svc.GoLive(workOrderId, reservations,
                Security.AppSession.CurrentUser?.Username ?? "system");

            Logging.AppLogger.Audit(Security.AppSession.CurrentUser?.Username ?? "system",
                "WOGoLive",
                $"WorkOrderID={workOrderId} lots={lines.Count(l => l.ToLock > 0)}");
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
        /// Completes a work order with explicit completed/scrap quantities.
        /// Delegates to WorkOrderService.Complete() for lot deduction, COGS, and cascade logic.
        /// </summary>
        public void PartialCompleteWorkOrder(int workOrderId, int completedQty, int scrapQty = 0,
            string? scrapReason = null, string? notes = null)
        {
            var svc = new WorkOrderService(_connectionString);
            svc.Complete(workOrderId, new CompleteWorkOrderRequest
            {
                CompletedQty = completedQty,
                ScrapQty     = scrapQty,
                ScrapReason  = scrapReason,
                Notes        = notes,
            }, Security.AppSession.CurrentUser?.Username ?? "system");

            Logging.AppLogger.Audit(Security.AppSession.CurrentUser?.Username ?? "system",
                "PartialCompleteWO",
                $"WorkOrderID={workOrderId} completed={completedQty} scrap={scrapQty}");
        }

        // ── BOM Preview ───────────────────────────────────────────────────────────

        /// <summary>
        /// Returns BOM ingredients for a work order multiplied by the WO quantity,
        /// used by FormBatchCooking to preview ingredient totals before starting a session.
        /// </summary>
        public List<Models.WOBomPreviewRow> GetWOBomPreview(int workOrderId)
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            return db.Query<Models.WOBomPreviewRow>(@"
                SELECT pt.PartID, pt.PartNumber, pt.PartName, pt.UnitOfMeasure AS UOM,
                       pp.Quantity * wo.Quantity AS RequiredQty,
                       pt.CurrentStock           AS OnHand,
                       pp.CreatesBatchLoss, pp.BatchLossRate,
                       pt.Density
                FROM   WorkOrders   wo
                JOIN   ProductParts pp ON pp.ProductID = wo.ProductID
                JOIN   Parts        pt ON pt.PartID    = pp.PartID
                WHERE  wo.WorkOrderID = @workOrderId",
                new { workOrderId }).ToList();
        }

        // ── Cook Sessions ─────────────────────────────────────────────────────────

        /// <summary>
        /// Creates a new cook session, calculates batch sizes and flask assignments from the
        /// product's SizeML attribute and the given batch-loss percentage, pre-computes
        /// RequiredQtyML for every step, and returns the new CookSessionID.
        /// </summary>
        public int CreateCookSession(string sessionName, IEnumerable<int> workOrderIds,
            decimal batchLossPercent = 0m, string? createdBy = null)
        {
            var woIds = workOrderIds.ToList();
            if (woIds.Count == 0) throw new ArgumentException("At least one work order is required.");

            var settings = AppSettings.Current;

            using var db = new SqlConnection(_connectionString);
            db.Open();
            using var tx = db.BeginTransaction();
            try
            {
                int sessionId = db.QuerySingle<int>(@"
                    INSERT INTO CookSessions (SessionName, CreatedBy, BatchLossPercent)
                    VALUES (@sessionName, @createdBy, @batchLossPercent);
                    SELECT CAST(SCOPE_IDENTITY() AS INT)",
                    new { sessionName, createdBy, batchLossPercent }, tx);

                decimal lossMultiplier = 1m + (batchLossPercent / 100m);

                foreach (int woId in woIds)
                {
                    // Read WO quantity and the product's SizeML attribute
                    var woInfo = db.QueryFirstOrDefault(@"
                        SELECT wo.Quantity, wo.ProductID
                        FROM   WorkOrders wo
                        WHERE  wo.WorkOrderID = @woId", new { woId }, tx);

                    if (woInfo == null) continue;
                    int    woQty      = (int)woInfo.Quantity;
                    int    productId  = (int)woInfo.ProductID;

                    // SizeML is a Manufacturing/Number attribute on the product
                    string? sizeMlStr = db.QueryFirstOrDefault<string>(@"
                        SELECT AttributeValue
                        FROM   ProductAttributes
                        WHERE  ProductID = @productId AND AttributeName = 'SizeML'",
                        new { productId }, tx);

                    decimal sizeMl     = decimal.TryParse(sizeMlStr,
                        System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out var parsed) ? parsed : 0m;
                    decimal batchSizeMl = woQty * sizeMl * lossMultiplier;
                    string  flaskType   = batchSizeMl > 0
                        ? settings.GetFlaskForBatchMl(batchSizeMl)
                        : "";

                    db.Execute(@"
                        INSERT INTO CookSessionBatches (CookSessionID, WorkOrderID, FlaskType, BatchSizeML)
                        VALUES (@sessionId, @woId, @flaskType, @batchSizeMl)",
                        new { sessionId, woId, flaskType, batchSizeMl }, tx);

                    // Create one step per BOM part, using per-row batch loss rate where set
                    var bomParts = db.Query(@"
                        SELECT pp.PartID, pp.Quantity AS BomQty, pp.CreatesBatchLoss, pp.BatchLossRate
                        FROM   WorkOrders   wo
                        JOIN   ProductParts pp ON pp.ProductID = wo.ProductID
                        WHERE  wo.WorkOrderID = @woId", new { woId }, tx).ToList();

                    foreach (var part in bomParts)
                    {
                        decimal bomQty     = (decimal)part.BomQty;
                        decimal effectiveRate = (bool)part.CreatesBatchLoss
                            ? ((decimal)part.BatchLossRate > 0m ? (decimal)part.BatchLossRate : batchLossPercent)
                            : 0m;
                        decimal requiredQtyMl = bomQty * woQty * (1m + effectiveRate / 100m);
                        db.Execute(@"
                            INSERT INTO CookSessionSteps (CookSessionID, WorkOrderID, PartID, RequiredQtyML)
                            VALUES (@sessionId, @woId, @partId, @requiredQtyMl)",
                            new { sessionId, woId, partId = (int)part.PartID, requiredQtyMl }, tx);
                    }
                }

                tx.Commit();
                Logging.AppLogger.Audit(createdBy ?? "system", "CreateCookSession",
                    $"SessionID={sessionId} Name={sessionName} Loss={batchLossPercent}% WOs={string.Join(",", woIds)}");
                return sessionId;
            }
            catch (Exception ex)
            {
                tx.Rollback();
                Logging.AppLogger.Error($"[CreateCookSession] Transaction rolled back for Name={sessionName}: {ex}");
                throw;
            }
        }

        public Models.CookSession? GetCookSession(int cookSessionId)
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            var session = db.QueryFirstOrDefault<Models.CookSession>(
                "SELECT * FROM CookSessions WHERE CookSessionID = @cookSessionId",
                new { cookSessionId });
            if (session == null) return null;

            session.WorkOrderIDs = db.Query<int>(
                "SELECT WorkOrderID FROM CookSessionBatches WHERE CookSessionID = @cookSessionId",
                new { cookSessionId }).ToList();
            return session;
        }

        public List<Models.CookSession> GetOpenCookSessions()
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            return db.Query<Models.CookSession>(
                "SELECT * FROM CookSessions WHERE Status = 'Open' ORDER BY CreatedAt DESC").ToList();
        }

        /// <summary>All steps for a session, enriched with part/product display data.
        /// Uses the pre-computed RequiredQtyML when available, falling back to BOM × WO qty.</summary>
        public List<Models.CookSessionStep> GetCookSessionSteps(int cookSessionId)
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            return db.Query<Models.CookSessionStep>(@"
                SELECT s.StepID, s.CookSessionID, s.WorkOrderID, s.PartID,
                       s.IsDone, s.DoneBy, s.DoneAt,
                       p.PartNumber, p.PartName, p.UnitOfMeasure,
                       pr.ProductName, pr.SKU AS ProductSKU,
                       COALESCE(s.RequiredQtyML, pp.Quantity * wo.Quantity) AS RequiredQty,
                       wo.Quantity AS WorkOrderQty,
                       csb.FlaskType,
                       p.Density
                FROM   CookSessionSteps   s
                JOIN   Parts              p   ON p.PartID      = s.PartID
                JOIN   WorkOrders         wo  ON wo.WorkOrderID = s.WorkOrderID
                JOIN   Products           pr  ON pr.ProductID   = wo.ProductID
                JOIN   ProductParts       pp  ON pp.ProductID   = wo.ProductID AND pp.PartID = s.PartID
                JOIN   CookSessionBatches csb ON csb.CookSessionID = s.CookSessionID
                                             AND csb.WorkOrderID   = s.WorkOrderID
                WHERE  s.CookSessionID = @cookSessionId
                ORDER  BY p.PartName, pr.ProductName",
                new { cookSessionId }).ToList();
        }

        /// <summary>
        /// Returns one summary row per ingredient across all batches in the session,
        /// used to populate the ingredient ComboBox and progress panel.
        /// Uses pre-computed RequiredQtyML when available.
        /// </summary>
        public List<Models.CookIngredientSummary> GetCookIngredients(int cookSessionId)
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            return db.Query<Models.CookIngredientSummary>(@"
                SELECT p.PartID, p.PartNumber, p.PartName, p.UnitOfMeasure,
                       SUM(COALESCE(s.RequiredQtyML, pp.Quantity * wo.Quantity)) AS TotalRequired,
                       p.CurrentStock AS OnHand,
                       SUM(CAST(s.IsDone AS INT)) AS StepsDone,
                       COUNT(s.StepID)             AS StepsTotal,
                       p.Density
                FROM   CookSessionSteps s
                JOIN   Parts        p  ON p.PartID      = s.PartID
                JOIN   WorkOrders   wo ON wo.WorkOrderID = s.WorkOrderID
                JOIN   ProductParts pp ON pp.ProductID   = wo.ProductID AND pp.PartID = s.PartID
                WHERE  s.CookSessionID = @cookSessionId
                GROUP BY p.PartID, p.PartNumber, p.PartName, p.UnitOfMeasure, p.CurrentStock, p.Density
                ORDER BY p.PartName",
                new { cookSessionId }).ToList();
        }

        /// <summary>Mark a single (WorkOrder × Part) step as done.</summary>
        public void MarkStepDone(int stepId, string doneBy)
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            db.Execute(@"
                UPDATE CookSessionSteps
                SET IsDone = 1, DoneBy = @doneBy, DoneAt = GETDATE()
                WHERE StepID = @stepId AND IsDone = 0",
                new { stepId, doneBy });
        }

        /// <summary>Mark all steps for a given ingredient across all batches in the session as done.</summary>
        public void MarkAllIngredientStepsDone(int cookSessionId, int partId, string doneBy)
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            db.Execute(@"
                UPDATE CookSessionSteps
                SET IsDone = 1, DoneBy = @doneBy, DoneAt = GETDATE()
                WHERE CookSessionID = @cookSessionId AND PartID = @partId AND IsDone = 0",
                new { cookSessionId, partId, doneBy });
        }

        /// <summary>
        /// Completes a cook session — marks it Complete in the database.
        /// Throws if any steps are still pending and forceComplete is false.
        /// Sets UpdatedBy, UpdatedAt, and CompletedBy for audit trail.
        /// </summary>
        public void CompleteCookSession(int cookSessionId, bool forceComplete = false)
        {
            string user = Security.AppSession.CurrentUser?.Username ?? "system";
            using var db = new SqlConnection(_connectionString);
            db.Open();
            using var tx = db.BeginTransaction();
            try
            {
                if (!forceComplete)
                {
                    int pending = db.ExecuteScalar<int>(
                        "SELECT COUNT(*) FROM CookSessionSteps WHERE CookSessionID = @cookSessionId AND IsDone = 0",
                        new { cookSessionId }, tx);
                    if (pending > 0)
                        throw new InvalidOperationException($"{pending} ingredient step(s) are still pending.");
                }

                db.Execute(@"
                    UPDATE CookSessions
                    SET    Status      = 'Complete',
                           CompletedAt = GETDATE(),
                           CompletedBy = @user,
                           UpdatedBy   = @user,
                           UpdatedAt   = GETDATE()
                    WHERE  CookSessionID = @cookSessionId",
                    new { cookSessionId, user }, tx);

                tx.Commit();

                Logging.AppLogger.Audit(user, "CompleteCookSession", $"SessionID={cookSessionId}");
            }
            catch (Exception ex)
            {
                tx.Rollback();
                Logging.AppLogger.Error($"[CompleteCookSession] Transaction rolled back for SessionID={cookSessionId}: {ex}");
                throw;
            }
        }

        /// <summary>
        /// Atomically marks a cook session Complete AND deducts all ingredient stock in one transaction.
        /// This is the preferred method over calling CompleteCookSession + DeductSessionIngredients separately.
        /// Returns true on success; false if the transaction fails (caller should warn the user).
        /// Throws InvalidOperationException if steps are pending and forceComplete is false.
        /// </summary>
        public bool CompleteCookSessionAndDeductStock(int cookSessionId, bool forceComplete = false, string? completedBy = null)
        {
            string user = completedBy ?? Security.AppSession.CurrentUser?.Username ?? "system";
            using var db = new SqlConnection(_connectionString);
            db.Open();
            using var tx = db.BeginTransaction();
            try
            {
                // Guard: refuse if steps are still pending (unless forced)
                if (!forceComplete)
                {
                    int pending = db.ExecuteScalar<int>(
                        "SELECT COUNT(*) FROM CookSessionSteps WHERE CookSessionID = @cookSessionId AND IsDone = 0",
                        new { cookSessionId }, tx);
                    if (pending > 0)
                        throw new InvalidOperationException($"{pending} ingredient step(s) are still pending.");
                }

                // Guard: refuse if session is already Complete or Cancelled
                string? currentStatus = db.ExecuteScalar<string?>(
                    "SELECT Status FROM CookSessions WHERE CookSessionID = @cookSessionId",
                    new { cookSessionId }, tx);
                if (currentStatus is "Complete" or "Cancelled")
                    throw new InvalidOperationException(
                        $"Cook session {cookSessionId} is already {currentStatus}.");

                // 1. Mark session complete with full audit trail
                db.Execute(@"
                    UPDATE CookSessions
                    SET    Status      = 'Complete',
                           CompletedAt = GETDATE(),
                           CompletedBy = @user,
                           UpdatedBy   = @user,
                           UpdatedAt   = GETDATE()
                    WHERE  CookSessionID = @cookSessionId",
                    new { cookSessionId, user }, tx);

                // 2. Aggregate required qty per part across all steps and deduct from Parts.CurrentStock
                var ingredients = db.Query<(int PartId, decimal TotalQty)>(@"
                    SELECT PartID, SUM(COALESCE(RequiredQtyML, 0)) AS TotalQty
                    FROM   CookSessionSteps
                    WHERE  CookSessionID = @cookSessionId
                    GROUP  BY PartID",
                    new { cookSessionId }, tx).ToList();

                foreach (var (partId, totalQty) in ingredients)
                {
                    if (totalQty <= 0) continue;
                    int deductQty = (int)Math.Ceiling((double)totalQty);
                    db.Execute(
                        "UPDATE Parts SET CurrentStock = CurrentStock - @deductQty WHERE PartID = @partId",
                        new { deductQty, partId }, tx);
                }

                tx.Commit();

                Logging.AppLogger.Audit(user, "CompleteCookSessionAndDeductStock",
                    $"SessionID={cookSessionId} parts={ingredients.Count} forced={forceComplete}");
                return true;
            }
            catch (InvalidOperationException)
            {
                tx.Rollback();
                throw;   // re-throw validation errors without swallowing them
            }
            catch (Exception ex)
            {
                tx.Rollback();
                Logging.AppLogger.Error($"[CompleteCookSessionAndDeductStock] Transaction rolled back for SessionID={cookSessionId}: {ex}");
                return false;
            }
        }

        // ── Batch Traveller & Label Export ────────────────────────────────────────

        /// <summary>Data for the printable Batch Traveller — one row per work order in a session.</summary>
        public List<Models.BatchTravellerRow> GetBatchTravellerData(int cookSessionId)
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            var rows = db.Query(@"
                SELECT wo.WorkOrderID,
                       m.MONumber AS WONumber,
                       pr.SKU     AS PartNumber,
                       pr.ProductName AS PartName,
                       wo.Quantity AS Qty,
                       wo.Notes
                FROM   CookSessionBatches csb
                JOIN   WorkOrders         wo ON wo.WorkOrderID   = csb.WorkOrderID
                JOIN   Products           pr ON pr.ProductID     = wo.ProductID
                JOIN   ManufacturingOrders m ON m.MOID           = wo.MOID
                WHERE  csb.CookSessionID = @cookSessionId
                ORDER  BY m.MONumber", new { cookSessionId }).ToList();

            // Pull product attributes for Nicotine, Size per product
            var productIds = rows.Select(r => (int)r.WorkOrderID).ToList();
            if (productIds.Count == 0) return new List<Models.BatchTravellerRow>();

            var attrs = db.Query(@"
                SELECT wo.WorkOrderID, pa.AttributeName, pa.AttributeValue
                FROM   WorkOrders      wo
                JOIN   ProductAttributes pa ON pa.ProductID = wo.ProductID
                WHERE  wo.WorkOrderID IN @productIds
                  AND  pa.AttributeName IN ('Nicotine','Size','FlaskType','Bins','Concentrate')",
                new { productIds }).ToList();

            var attrMap = attrs.GroupBy(a => (int)a.WorkOrderID)
                               .ToDictionary(g => g.Key, g => g.ToDictionary(a => (string)a.AttributeName, a => (string?)a.AttributeValue));

            return rows.Select(r =>
            {
                int woId = (int)r.WorkOrderID;
                attrMap.TryGetValue(woId, out var a);
                return new Models.BatchTravellerRow
                {
                    WONumber   = (string)r.WONumber,
                    PartNumber = (string)r.PartNumber,
                    PartName   = (string)r.PartName,
                    Qty        = (int)r.Qty,
                    Nicotine   = a?.GetValueOrDefault("Nicotine"),
                    Size       = a?.GetValueOrDefault("Size"),
                    FlaskType  = a?.GetValueOrDefault("FlaskType"),
                    Bins       = a?.GetValueOrDefault("Bins"),
                    Concentrate = a?.GetValueOrDefault("Concentrate"),
                    Notes      = (string?)r.Notes
                };
            }).ToList();
        }

        /// <summary>
        /// Deducts ingredient stock (Parts.CurrentStock) for every ingredient used in the completed cook session.
        /// Aggregates RequiredQtyML from CookSessionSteps per PartID, rounds up to the nearest integer
        /// (matching the same CEILING convention used elsewhere in the app), then subtracts from Parts.CurrentStock
        /// inside a single transaction so the deduction is atomic.
        /// Returns true on success; logs and returns false on failure so the caller can show a warning.
        /// </summary>
        public bool DeductSessionIngredients(int sessionId)
        {
            using var db = new SqlConnection(_connectionString);
            db.Open();
            using var tx = db.BeginTransaction();
            try
            {
                // Aggregate total required qty per part across all steps in the session
                var ingredients = db.Query<(int PartId, decimal TotalQty)>(@"
                    SELECT PartID, SUM(COALESCE(RequiredQtyML, 0)) AS TotalQty
                    FROM   CookSessionSteps
                    WHERE  CookSessionID = @sessionId
                    GROUP BY PartID",
                    new { sessionId }, tx).ToList();

                foreach (var (partId, totalQty) in ingredients)
                {
                    if (totalQty <= 0) continue;

                    // Parts.CurrentStock is INT; round up to avoid under-deducting fractional ml amounts
                    int deductQty = (int)Math.Ceiling((double)totalQty);

                    db.Execute(
                        "UPDATE Parts SET CurrentStock = CurrentStock - @deductQty WHERE PartID = @partId",
                        new { deductQty, partId }, tx);
                }

                tx.Commit();

                Logging.AppLogger.Audit(Security.AppSession.CurrentUser?.Username ?? "system",
                    "CookDeductIngredients",
                    $"SessionID={sessionId} parts={ingredients.Count} deducted");
                return true;
            }
            catch (Exception ex)
            {
                tx.Rollback();
                Logging.AppLogger.Error($"[DeductSessionIngredients] SessionID={sessionId} {ex}");
                return false;
            }
        }

        /// <summary>Data for the label-printing CSV export — one row per work order in a session.</summary>
        public List<Models.LabelExportRow> GetLabelExportData(int cookSessionId)
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            var rows = db.Query(@"
                SELECT wo.WorkOrderID,
                       m.MONumber        AS WONumber,
                       pr.ProductName    AS PartName,
                       pr.SKU            AS PartNumber,
                       wo.Quantity       AS QtyOrdered,
                       cs.CreatedAt      AS BatchMadeDate
                FROM   CookSessionBatches csb
                JOIN   WorkOrders         wo ON wo.WorkOrderID   = csb.WorkOrderID
                JOIN   Products           pr ON pr.ProductID     = wo.ProductID
                JOIN   ManufacturingOrders m ON m.MOID           = wo.MOID
                JOIN   CookSessions       cs ON cs.CookSessionID = csb.CookSessionID
                WHERE  csb.CookSessionID = @cookSessionId
                ORDER  BY m.MONumber", new { cookSessionId }).ToList();

            var productIds = rows.Select(r => (int)r.WorkOrderID).ToList();
            if (productIds.Count == 0) return new List<Models.LabelExportRow>();

            var attrs = db.Query(@"
                SELECT wo.WorkOrderID, pa.AttributeName, pa.AttributeValue
                FROM   WorkOrders      wo
                JOIN   ProductAttributes pa ON pa.ProductID = wo.ProductID
                WHERE  wo.WorkOrderID IN @productIds
                  AND  pa.AttributeName IN ('Nicotine','VG','Size','Brand','BottleType','Version','Note')",
                new { productIds }).ToList();

            var attrMap = attrs.GroupBy(a => (int)a.WorkOrderID)
                               .ToDictionary(g => g.Key, g => g.ToDictionary(a => (string)a.AttributeName, a => (string?)a.AttributeValue));

            return rows.Select(r =>
            {
                int woId = (int)r.WorkOrderID;
                attrMap.TryGetValue(woId, out var a);
                return new Models.LabelExportRow
                {
                    BottleType    = a?.GetValueOrDefault("BottleType"),
                    WONumber      = (string)r.WONumber,
                    PartName      = (string)r.PartName,
                    PartNumber    = (string)r.PartNumber,
                    QtyOrdered    = (int)r.QtyOrdered,
                    Version       = a?.GetValueOrDefault("Version"),
                    BatchMadeDate = ((DateTime)r.BatchMadeDate).ToString("yyyy-MM-dd"),
                    Size          = a?.GetValueOrDefault("Size"),
                    Brand         = a?.GetValueOrDefault("Brand"),
                    Nicotine      = a?.GetValueOrDefault("Nicotine"),
                    VG            = a?.GetValueOrDefault("VG"),
                    Note          = a?.GetValueOrDefault("Note")
                };
            }).ToList();
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
