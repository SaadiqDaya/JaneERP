using System.Configuration;
using System.Data;
using Dapper;
using JaneERP.Logging;
using JaneERP.Models;
using JaneERP.Security;
using Microsoft.Data.SqlClient;

namespace JaneERP.Data
{
    public class SupplierRepository
    {
        private readonly string _cs =
            ConfigurationManager.ConnectionStrings["MyERP"]?.ConnectionString
            ?? throw new InvalidOperationException("Connection string 'MyERP' not found in App.config.");

        // ── Schema ────────────────────────────────────────────────────────────────
        public void EnsureSchema()
        {
            using IDbConnection db = new SqlConnection(_cs);
            db.Execute(@"
                IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='Suppliers' AND xtype='U')
                CREATE TABLE Suppliers (
                    SupplierID   INT IDENTITY(1,1) PRIMARY KEY,
                    SupplierName NVARCHAR(200) NOT NULL,
                    ContactName  NVARCHAR(200) NULL,
                    Email        NVARCHAR(200) NULL,
                    Phone        NVARCHAR(50)  NULL,
                    Address      NVARCHAR(500) NULL,
                    IsActive     BIT           NOT NULL DEFAULT 1,
                    Notes        NVARCHAR(500) NULL,
                    CreatedAt    DATETIME      NOT NULL DEFAULT GETDATE()
                );

                IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='PurchaseOrders' AND xtype='U')
                CREATE TABLE PurchaseOrders (
                    POID         INT IDENTITY(1,1) PRIMARY KEY,
                    PONumber     NVARCHAR(50)  NOT NULL UNIQUE,
                    SupplierID   INT           NOT NULL REFERENCES Suppliers(SupplierID),
                    Status       NVARCHAR(20)  NOT NULL DEFAULT 'Draft',
                    OrderDate    DATETIME      NOT NULL DEFAULT GETDATE(),
                    ExpectedDate DATETIME      NULL,
                    Notes        NVARCHAR(500) NULL,
                    CreatedBy    NVARCHAR(100) NULL,
                    TotalCost    DECIMAL(18,2) NOT NULL DEFAULT 0,
                    CreatedAt    DATETIME      NOT NULL DEFAULT GETDATE()
                );

                IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='PurchaseOrderItems' AND xtype='U')
                CREATE TABLE PurchaseOrderItems (
                    POItemID         INT IDENTITY(1,1) PRIMARY KEY,
                    POID             INT           NOT NULL REFERENCES PurchaseOrders(POID),
                    PartID           INT           NULL REFERENCES Parts(PartID),
                    ProductID        INT           NULL REFERENCES Products(ProductID),
                    SKU              NVARCHAR(100) NULL,
                    ItemName         NVARCHAR(200) NOT NULL,
                    QuantityOrdered  INT           NOT NULL,
                    QuantityReceived INT           NOT NULL DEFAULT 0,
                    UnitCost         DECIMAL(18,2) NOT NULL DEFAULT 0
                );");
        }

        // ── Suppliers ─────────────────────────────────────────────────────────────
        public List<Supplier> GetAllSuppliers(bool includeInactive = false)
        {
            using IDbConnection db = new SqlConnection(_cs);
            string filter = includeInactive ? "1=1" : "IsActive = 1";
            return db.Query<Supplier>(
                $"SELECT * FROM Suppliers WHERE {filter} ORDER BY SupplierName").ToList();
        }

        public int AddSupplier(Supplier s)
        {
            using IDbConnection db = new SqlConnection(_cs);
            return db.QuerySingle<int>(@"
                INSERT INTO Suppliers (SupplierName, ContactName, Email, Phone, Address, IsActive, Notes)
                VALUES (@SupplierName, @ContactName, @Email, @Phone, @Address, @IsActive, @Notes);
                SELECT CAST(SCOPE_IDENTITY() AS INT);", s);
        }

        public void UpdateSupplier(Supplier s)
        {
            using IDbConnection db = new SqlConnection(_cs);
            db.Execute(@"
                UPDATE Suppliers
                SET SupplierName = @SupplierName,
                    ContactName  = @ContactName,
                    Email        = @Email,
                    Phone        = @Phone,
                    Address      = @Address,
                    IsActive     = @IsActive,
                    Notes        = @Notes
                WHERE SupplierID = @SupplierID", s);
        }

        // ── Purchase Orders ───────────────────────────────────────────────────────
        public List<PurchaseOrder> GetOrders(string? status = null)
        {
            using IDbConnection db = new SqlConnection(_cs);
            string filter = status == null ? "1=1" : "po.Status = @status";
            return db.Query<PurchaseOrder>($@"
                SELECT po.*, s.SupplierName
                FROM   PurchaseOrders po
                JOIN   Suppliers s ON s.SupplierID = po.SupplierID
                WHERE  {filter}
                ORDER  BY po.CreatedAt DESC",
                new { status }).ToList();
        }

        public PurchaseOrder? GetOrder(int poid)
        {
            using IDbConnection db = new SqlConnection(_cs);
            var po = db.QueryFirstOrDefault<PurchaseOrder>(@"
                SELECT po.*, s.SupplierName
                FROM   PurchaseOrders po
                JOIN   Suppliers s ON s.SupplierID = po.SupplierID
                WHERE  po.POID = @poid", new { poid });

            if (po != null)
            {
                po.Items = db.Query<PurchaseOrderItem>(
                    "SELECT * FROM PurchaseOrderItems WHERE POID = @poid ORDER BY POItemID",
                    new { poid }).ToList();
            }
            return po;
        }

        public int CreateOrder(PurchaseOrder po)
        {
            using var db = new SqlConnection(_cs);
            db.Open();
            using var tx = db.BeginTransaction();
            try
            {
                // Auto-generate PO number: PO-YYYY-NNNN
                int year = DateTime.Now.Year;
                int seq = db.QuerySingle<int>(
                    "SELECT COUNT(*) + 1 FROM PurchaseOrders WHERE YEAR(CreatedAt) = @year",
                    new { year }, tx);
                po.PONumber = $"PO-{year}-{seq:D4}";
                po.CreatedBy ??= AppSession.CurrentUser?.Username;

                // Recalculate total cost
                po.TotalCost = po.Items.Sum(i => i.UnitCost * i.QuantityOrdered);

                int poid = db.QuerySingle<int>(@"
                    INSERT INTO PurchaseOrders (PONumber, SupplierID, Status, OrderDate, ExpectedDate, Notes, CreatedBy, TotalCost)
                    VALUES (@PONumber, @SupplierID, @Status, @OrderDate, @ExpectedDate, @Notes, @CreatedBy, @TotalCost);
                    SELECT CAST(SCOPE_IDENTITY() AS INT);", po, tx);

                foreach (var item in po.Items)
                {
                    item.POID = poid;
                    db.Execute(@"
                        INSERT INTO PurchaseOrderItems (POID, PartID, ProductID, SKU, ItemName, QuantityOrdered, QuantityReceived, UnitCost)
                        VALUES (@POID, @PartID, @ProductID, @SKU, @ItemName, @QuantityOrdered, 0, @UnitCost);",
                        item, tx);
                }

                tx.Commit();

                AppLogger.Audit(AppSession.CurrentUser?.Username ?? "system",
                    "CreatePO", $"PO# {po.PONumber} supplier={po.SupplierID} items={po.Items.Count}");

                return poid;
            }
            catch { tx.Rollback(); throw; }
        }

        public void UpdateOrderStatus(int poid, string status)
        {
            using IDbConnection db = new SqlConnection(_cs);
            db.Execute("UPDATE PurchaseOrders SET Status = @status WHERE POID = @poid",
                new { status, poid });
        }

        public void ReceiveItems(int poid, List<(int poItemId, int qtyReceived)> receivals)
        {
            using var db = new SqlConnection(_cs);
            db.Open();
            using var tx = db.BeginTransaction();
            try
            {
                string user = AppSession.CurrentUser?.Username ?? "system";

                foreach (var (poItemId, qty) in receivals)
                {
                    if (qty <= 0) continue;

                    // Get the item details
                    var item = db.QueryFirstOrDefault<PurchaseOrderItem>(
                        "SELECT * FROM PurchaseOrderItems WHERE POItemID = @poItemId",
                        new { poItemId }, tx);

                    if (item == null) continue;

                    // Update received qty
                    db.Execute(@"
                        UPDATE PurchaseOrderItems
                        SET QuantityReceived = QuantityReceived + @qty
                        WHERE POItemID = @poItemId",
                        new { qty, poItemId }, tx);

                    // Update Parts stock if applicable
                    if (item.PartID.HasValue)
                    {
                        db.Execute(
                            "UPDATE Parts SET CurrentStock = CurrentStock + @qty WHERE PartID = @partId",
                            new { qty, partId = item.PartID.Value }, tx);
                    }

                    // Insert InventoryTransaction if product
                    if (item.ProductID.HasValue)
                    {
                        db.Execute(@"
                            INSERT INTO InventoryTransactions (ProductID, QuantityChange, TransactionType, Notes, TransactionDate)
                            VALUES (@ProductID, @QuantityChange, 'PurchaseReceipt', @Notes, GETDATE());",
                            new
                            {
                                ProductID      = item.ProductID.Value,
                                QuantityChange = qty,
                                Notes          = $"PO# received: {item.ItemName} (POItemID={poItemId})"
                            }, tx);
                    }

                    AppLogger.Audit(user, "ReceivePOItem",
                        $"POID={poid} POItemID={poItemId} qty={qty} item={item.ItemName}");
                }

                // Recalculate PO status
                var items = db.Query<PurchaseOrderItem>(
                    "SELECT QuantityOrdered, QuantityReceived FROM PurchaseOrderItems WHERE POID = @poid",
                    new { poid }, tx).ToList();

                string newStatus;
                if (items.All(i => i.QuantityReceived >= i.QuantityOrdered))
                    newStatus = "Received";
                else if (items.Any(i => i.QuantityReceived > 0))
                    newStatus = "PartiallyReceived";
                else
                    newStatus = "Sent";

                db.Execute("UPDATE PurchaseOrders SET Status = @newStatus WHERE POID = @poid",
                    new { newStatus, poid }, tx);

                tx.Commit();
            }
            catch { tx.Rollback(); throw; }
        }
    }
}
