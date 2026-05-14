using System.Configuration;
using System.Data;
using Dapper;
using JaneERP.Infrastructure;
using JaneERP.Interfaces;
using JaneERP.Logging;
using JaneERP.Models;
using JaneERP.Security;
using Microsoft.Data.SqlClient;

namespace JaneERP.Data
{
    public class BackorderRepository : IBackorderRepository
    {
        private readonly string _cs =
            ConfigurationManager.ConnectionStrings["MyERP"]?.ConnectionString
            ?? throw new InvalidOperationException("Connection string 'MyERP' not found.");

        public void EnsureSchema()
        {
            using var db = new SqlConnection(_cs);
            db.Execute(@"
                IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='Backorders' AND xtype='U')
                CREATE TABLE Backorders (
                    BackorderID      INT          NOT NULL IDENTITY PRIMARY KEY,
                    SalesOrderID     INT          NOT NULL,
                    SalesOrderItemID INT          NOT NULL,
                    ProductID        INT          NOT NULL,
                    BackorderedQty   INT          NOT NULL,
                    FulfilledQty     INT          NOT NULL DEFAULT 0,
                    Status           NVARCHAR(20) NOT NULL DEFAULT 'Open',
                    CreatedAt        DATETIME     NOT NULL DEFAULT GETDATE(),
                    FulfilledAt      DATETIME     NULL
                );

                IF NOT EXISTS (SELECT 1 FROM sys.indexes
                               WHERE name='IX_Backorders_ProductID_Status'
                                 AND object_id=OBJECT_ID('Backorders'))
                    CREATE INDEX IX_Backorders_ProductID_Status
                        ON Backorders (ProductID, Status)
                        INCLUDE (BackorderedQty, FulfilledQty);");
        }

        public List<Backorder> GetOpenBackorders()
        {
            // Backorders are derived live: active orders where on-hand stock
            // (Products.CurrentStock — the authoritative source) is less than ordered qty.
            using IDbConnection db = new SqlConnection(_cs);
            return db.Query<Backorder>(@"
                SELECT  soi.SalesOrderItemID             AS BackorderID,
                        so.SalesOrderID,
                        CAST(so.OrderNumber AS NVARCHAR) AS OrderNumber,
                        ISNULL(c.FullName, c.Email)      AS CustomerName,
                        soi.SalesOrderItemID,
                        p.ProductID,
                        p.SKU,
                        p.ProductName,
                        soi.Quantity                     AS BackorderedQty,
                        0                                AS FulfilledQty,
                        'Open'                           AS Status,
                        so.CreatedAt,
                        NULL                             AS FulfilledAt,
                        (SELECT ISNULL(SUM(QuantityChange), 0)
                         FROM   InventoryTransactions
                         WHERE  ProductID = p.ProductID) AS AvailableStock
                FROM    SalesOrderItems soi
                JOIN    SalesOrders so ON so.SalesOrderID = soi.SalesOrderID
                JOIN    Customers   c  ON c.CustomerID    = so.CustomerID
                JOIN    Products    p  ON p.ProductID     = soi.ProductID
                WHERE   so.Status IN ('Live','Picking','Packing')
                  AND   (SELECT ISNULL(SUM(QuantityChange), 0)
                         FROM   InventoryTransactions
                         WHERE  ProductID = p.ProductID) < soi.Quantity
                ORDER   BY so.CreatedAt ASC").ToList();
        }

        public List<Backorder> GetBackordersForProduct(int productId)
        {
            using IDbConnection db = new SqlConnection(_cs);
            return db.Query<Backorder>(@"
                SELECT b.BackorderID, b.SalesOrderID,
                       CAST(so.OrderNumber AS NVARCHAR) AS OrderNumber,
                       ISNULL(c.FullName, c.Email)      AS CustomerName,
                       b.SalesOrderItemID, b.ProductID,
                       p.SKU, p.ProductName,
                       b.BackorderedQty, b.FulfilledQty,
                       b.Status, b.CreatedAt, b.FulfilledAt
                FROM   Backorders b
                JOIN   SalesOrders so ON so.SalesOrderID = b.SalesOrderID
                JOIN   Customers   c  ON c.CustomerID    = so.CustomerID
                JOIN   Products    p  ON p.ProductID     = b.ProductID
                WHERE  b.ProductID = @productId
                  AND  b.Status IN ('Open','PartiallyFilled')
                ORDER  BY b.CreatedAt ASC",
                new { productId }).ToList();
        }

        public List<Backorder> GetBackordersForOrder(int salesOrderId)
        {
            using IDbConnection db = new SqlConnection(_cs);
            return db.Query<Backorder>(@"
                SELECT b.BackorderID, b.SalesOrderID,
                       CAST(so.OrderNumber AS NVARCHAR) AS OrderNumber,
                       ISNULL(c.FullName, c.Email)      AS CustomerName,
                       b.SalesOrderItemID, b.ProductID,
                       p.SKU, p.ProductName,
                       b.BackorderedQty, b.FulfilledQty,
                       b.Status, b.CreatedAt, b.FulfilledAt
                FROM   Backorders b
                JOIN   SalesOrders so ON so.SalesOrderID = b.SalesOrderID
                JOIN   Customers   c  ON c.CustomerID    = so.CustomerID
                JOIN   Products    p  ON p.ProductID     = b.ProductID
                WHERE  b.SalesOrderID = @salesOrderId
                ORDER  BY b.CreatedAt ASC",
                new { salesOrderId }).ToList();
        }

        public int CreateBackorder(int salesOrderId, int salesOrderItemId, int productId, int backorderedQty)
        {
            using IDbConnection db = new SqlConnection(_cs);
            int id = db.QuerySingle<int>(@"
                INSERT INTO Backorders (SalesOrderID, SalesOrderItemID, ProductID, BackorderedQty)
                VALUES (@salesOrderId, @salesOrderItemId, @productId, @backorderedQty);
                SELECT CAST(SCOPE_IDENTITY() AS INT);",
                new { salesOrderId, salesOrderItemId, productId, backorderedQty });
            AppLogger.Audit(AppSession.CurrentUser?.Username ?? "system",
                "CreateBackorder", $"Created backorder #{id} for SalesOrderID {salesOrderId}");
            return id;
        }

        public BackorderFulfillResult FulfillBackorders(int productId, int availableQty)
        {
            var result = new BackorderFulfillResult();

            using IDbConnection db = new SqlConnection(_cs);
            db.Open();
            using var tx = db.BeginTransaction();

            // Re-read stock INSIDE the transaction with UPDLOCK so no concurrent
            // transaction can consume the same stock between the form reading it
            // and this repo executing.  The passed-in availableQty is ignored.
            int liveStock = db.QuerySingle<int>(@"
                SELECT ISNULL(SUM(QuantityChange), 0)
                FROM   InventoryTransactions WITH (UPDLOCK)
                WHERE  ProductID = @productId",
                new { productId }, tx);

            if (liveStock <= 0)
            {
                tx.Rollback();
                return result;
            }

            var open = db.Query<Backorder>(@"
                SELECT BackorderID, BackorderedQty, FulfilledQty, SalesOrderID
                FROM   Backorders
                WHERE  ProductID = @productId
                  AND  Status IN ('Open','PartiallyFilled')
                ORDER  BY CreatedAt ASC",
                new { productId }, tx).ToList();

            int remaining = liveStock;
            int moved = 0;
            var advanceOrders = new HashSet<int>(); // orders that may now be ready to advance

            foreach (var bo in open)
            {
                if (remaining <= 0) break;

                int needed = bo.RemainingQty;
                int fill   = Math.Min(needed, remaining);
                int newFilled = bo.FulfilledQty + fill;
                bool done = newFilled >= bo.BackorderedQty;
                string newStatus = done ? "Fulfilled" : "PartiallyFilled";

                db.Execute(@"
                    UPDATE Backorders
                    SET    FulfilledQty = @newFilled,
                           Status       = @newStatus,
                           FulfilledAt  = CASE WHEN @done=1 THEN GETDATE() ELSE NULL END
                    WHERE  BackorderID  = @id",
                    new { newFilled, newStatus, done = done ? 1 : 0, id = bo.BackorderID }, tx);

                remaining -= fill;
                moved++;
                result.FulfilledCount++;
                result.Messages.Add(
                    $"Order #{bo.SalesOrderID}: fulfilled {fill} unit(s) — {(done ? "complete" : "partial")}");

                if (done) advanceOrders.Add(bo.SalesOrderID);
            }

            // Advance SalesOrder status from Picking → Packing when ALL backorders
            // for that order are now fulfilled (none remain Open or PartiallyFilled).
            foreach (int orderId in advanceOrders)
            {
                int stillOpen = db.QuerySingle<int>(@"
                    SELECT COUNT(*)
                    FROM   Backorders
                    WHERE  SalesOrderID = @orderId
                      AND  Status IN ('Open','PartiallyFilled')",
                    new { orderId }, tx);

                if (stillOpen == 0)
                {
                    db.Execute(@"
                        UPDATE SalesOrders
                        SET    Status = 'Packing'
                        WHERE  SalesOrderID = @orderId
                          AND  Status = 'Picking'",
                        new { orderId }, tx);
                    result.Messages.Add($"Order #{orderId}: all backorders fulfilled — advanced to Packing");
                }
            }

            tx.Commit();
            AppLogger.Audit(AppSession.CurrentUser?.Username ?? "system",
                "FulfillBackorders", $"Fulfilled {moved} backorder(s) for ProductID {productId}");
            return result;
        }

        public void CancelBackorder(int backorderId)
        {
            using IDbConnection db = new SqlConnection(_cs);
            db.Execute(
                "UPDATE Backorders SET Status='Cancelled' WHERE BackorderID=@backorderId",
                new { backorderId });
            AppLogger.Audit(AppSession.CurrentUser?.Username ?? "system",
                "CancelBackorder", $"Cancelled backorder #{backorderId}");
        }
    }
}
