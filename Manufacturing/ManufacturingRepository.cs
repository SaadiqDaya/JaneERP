using System.Configuration;
using System.Data;
using Dapper;
using JaneERP.Models;
using Microsoft.Data.SqlClient;

namespace JaneERP.Manufacturing
{
    public class ManufacturingRepository
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
                        ALTER TABLE WorkOrders ADD CostOfGoods DECIMAL(18,2) NULL;");
            }
            catch (Exception ex) { Logging.AppLogger.Info($"ManufacturingSchema migration: {ex.Message}"); }
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

        public List<WorkOrder> GetPendingWorkOrders()
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            return db.Query<WorkOrder>(@"
                SELECT wo.*, p.ProductName, p.SKU
                FROM   WorkOrders wo
                JOIN   Products   p ON p.ProductID = wo.ProductID
                WHERE  wo.Status <> 'Complete'
                ORDER  BY wo.WorkOrderID").ToList();
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

                // 3. Deduct BOM parts from Parts.CurrentStock
                //    Table is ProductParts (ProductID, PartID, Quantity), not BillOfMaterials
                var bomItems = db.Query(
                    "SELECT pp.PartID, pp.Quantity, ISNULL(p.UnitCost, 0) AS UnitCost " +
                    "FROM ProductParts pp JOIN Parts p ON p.PartID = pp.PartID " +
                    "WHERE pp.ProductID = @productId",
                    new { productId }, tx).ToList();

                decimal totalCogs = 0m;
                foreach (var bom in bomItems)
                {
                    int deduct = (int)bom.Quantity * quantity;
                    db.Execute(
                        "UPDATE Parts SET CurrentStock = CurrentStock - @deduct WHERE PartID = @partId",
                        new { deduct, partId = (int)bom.PartID }, tx);
                    totalCogs += (decimal)bom.UnitCost * deduct;
                }

                // 4. Record COGS on the work order
                db.Execute(
                    "UPDATE WorkOrders SET CostOfGoods = @cogs WHERE WorkOrderID = @workOrderId",
                    new { cogs = totalCogs, workOrderId }, tx);

                tx.Commit();
            }
            catch { tx.Rollback(); throw; }
        }

        public void UpdateWorkOrderStatus(int workOrderId, string status)
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            db.Execute("UPDATE WorkOrders SET Status = @status WHERE WorkOrderID = @workOrderId",
                new { status, workOrderId });
        }
    }
}
