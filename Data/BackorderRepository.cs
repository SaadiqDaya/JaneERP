using System.Configuration;
using System.Data;
using Dapper;
using JaneERP.Interfaces;
using JaneERP.Models;
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
            using IDbConnection db = new SqlConnection(_cs);
            return db.Query<Backorder>(@"
                SELECT b.BackorderID, b.SalesOrderID,
                       CAST(so.OrderNumber AS NVARCHAR) AS OrderNumber,
                       ISNULL(c.FullName, c.Email)      AS CustomerName,
                       b.SalesOrderItemID, b.ProductID,
                       p.SKU, p.ProductName,
                       b.BackorderedQty, b.FulfilledQty,
                       b.Status, b.CreatedAt, b.FulfilledAt,
                       ISNULL(stock.CurrentStock, 0)    AS AvailableStock
                FROM   Backorders b
                JOIN   SalesOrders so ON so.SalesOrderID = b.SalesOrderID
                JOIN   Customers   c  ON c.CustomerID    = so.CustomerID
                JOIN   Products    p  ON p.ProductID     = b.ProductID
                OUTER APPLY (
                    SELECT SUM(QuantityChange) AS CurrentStock
                    FROM   InventoryTransactions
                    WHERE  ProductID = b.ProductID
                ) stock
                WHERE  b.Status IN ('Open','PartiallyFilled')
                ORDER  BY b.CreatedAt ASC").ToList();
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
            return db.QuerySingle<int>(@"
                INSERT INTO Backorders (SalesOrderID, SalesOrderItemID, ProductID, BackorderedQty)
                VALUES (@salesOrderId, @salesOrderItemId, @productId, @backorderedQty);
                SELECT CAST(SCOPE_IDENTITY() AS INT);",
                new { salesOrderId, salesOrderItemId, productId, backorderedQty });
        }

        public BackorderFulfillResult FulfillBackorders(int productId, int availableQty)
        {
            var result = new BackorderFulfillResult();
            if (availableQty <= 0) return result;

            using IDbConnection db = new SqlConnection(_cs);
            db.Open();

            var open = db.Query<Backorder>(@"
                SELECT BackorderID, BackorderedQty, FulfilledQty, SalesOrderID
                FROM   Backorders
                WHERE  ProductID = @productId
                  AND  Status IN ('Open','PartiallyFilled')
                ORDER  BY CreatedAt ASC",
                new { productId }).ToList();

            int remaining = availableQty;
            using var tx = db.BeginTransaction();

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
                result.FulfilledCount++;
                result.Messages.Add(
                    $"Order #{bo.SalesOrderID}: fulfilled {fill} unit(s) — {(done ? "complete" : "partial")}");
            }

            tx.Commit();
            return result;
        }

        public void CancelBackorder(int backorderId)
        {
            using IDbConnection db = new SqlConnection(_cs);
            db.Execute(
                "UPDATE Backorders SET Status='Cancelled' WHERE BackorderID=@backorderId",
                new { backorderId });
        }
    }
}
