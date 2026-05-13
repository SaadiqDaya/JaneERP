using System.Configuration;
using System.Data;
using Dapper;
using JaneERP.Interfaces;
using JaneERP.Logging;
using JaneERP.Models;
using JaneERP.Security;
using Microsoft.Data.SqlClient;

namespace JaneERP.Data
{
    public class ReturnRepository : IReturnRepository
    {
        private readonly string _cs =
            ConfigurationManager.ConnectionStrings["MyERP"]?.ConnectionString
            ?? throw new InvalidOperationException("Connection string 'MyERP' not found.");

        public void EnsureSchema()
        {
            using var db = new SqlConnection(_cs);
            db.Execute(@"
                IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='ReturnOrders' AND xtype='U')
                CREATE TABLE ReturnOrders (
                    ReturnID         INT          NOT NULL IDENTITY PRIMARY KEY,
                    OriginalOrderID  INT          NOT NULL,
                    CustomerID       INT          NOT NULL,
                    ReturnDate       DATE         NOT NULL DEFAULT CAST(GETDATE() AS DATE),
                    Status           NVARCHAR(20) NOT NULL DEFAULT 'Pending',
                    Reason           NVARCHAR(200) NULL,
                    Notes            NVARCHAR(MAX) NULL,
                    CreatedBy        NVARCHAR(100) NULL,
                    CreatedAt        DATETIME      NOT NULL DEFAULT GETDATE()
                );

                IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='ReturnOrderItems' AND xtype='U')
                CREATE TABLE ReturnOrderItems (
                    ReturnItemID      INT          NOT NULL IDENTITY PRIMARY KEY,
                    ReturnID          INT          NOT NULL,
                    SalesOrderItemID  INT          NULL,
                    ProductID         INT          NOT NULL,
                    SKU               NVARCHAR(100) NULL,
                    ProductName       NVARCHAR(200) NULL,
                    OriginalQty       INT          NOT NULL DEFAULT 0,
                    ReturnQty         INT          NOT NULL DEFAULT 1,
                    Condition         NVARCHAR(20) NOT NULL DEFAULT 'Resalable',
                    RestockLocationID INT          NULL
                );");
        }

        public List<ReturnOrder> GetReturns(int? customerId = null)
        {
            using IDbConnection db = new SqlConnection(_cs);
            string where = customerId.HasValue ? "WHERE r.CustomerID = @customerId" : "";
            var returns = db.Query<ReturnOrder>($@"
                SELECT r.ReturnID, r.OriginalOrderID,
                       ISNULL(CAST(so.OrderNumber AS NVARCHAR), CAST(r.OriginalOrderID AS NVARCHAR)) AS OriginalOrderNumber,
                       r.CustomerID,
                       ISNULL(c.FullName, c.Email) AS CustomerName,
                       r.ReturnDate, r.Status, r.Reason, r.Notes, r.CreatedBy, r.CreatedAt
                FROM   ReturnOrders r
                LEFT JOIN SalesOrders so ON so.SalesOrderID = r.OriginalOrderID
                LEFT JOIN Customers   c  ON c.CustomerID    = r.CustomerID
                {where}
                ORDER  BY r.CreatedAt DESC",
                new { customerId }).ToList();

            if (returns.Count > 0)
            {
                var ids = returns.Select(r => r.ReturnID).ToArray();
                var items = db.Query<ReturnOrderItem>(
                    "SELECT * FROM ReturnOrderItems WHERE ReturnID IN @ids",
                    new { ids }).ToList();

                foreach (var ret in returns)
                    ret.Items = items.Where(i => i.ReturnID == ret.ReturnID).ToList();
            }

            return returns;
        }

        public ReturnOrder? GetById(int returnId)
        {
            using IDbConnection db = new SqlConnection(_cs);
            var ret = db.QueryFirstOrDefault<ReturnOrder>(@"
                SELECT r.ReturnID, r.OriginalOrderID,
                       ISNULL(CAST(so.OrderNumber AS NVARCHAR), CAST(r.OriginalOrderID AS NVARCHAR)) AS OriginalOrderNumber,
                       r.CustomerID,
                       ISNULL(c.FullName, c.Email) AS CustomerName,
                       r.ReturnDate, r.Status, r.Reason, r.Notes, r.CreatedBy, r.CreatedAt
                FROM   ReturnOrders r
                LEFT JOIN SalesOrders so ON so.SalesOrderID = r.OriginalOrderID
                LEFT JOIN Customers   c  ON c.CustomerID    = r.CustomerID
                WHERE  r.ReturnID = @returnId",
                new { returnId });

            if (ret != null)
                ret.Items = db.Query<ReturnOrderItem>(
                    "SELECT * FROM ReturnOrderItems WHERE ReturnID = @returnId",
                    new { returnId }).ToList();

            return ret;
        }

        public int CreateReturn(CreateReturnRequest request)
        {
            using IDbConnection db = new SqlConnection(_cs);
            db.Open();

            // Look up CustomerID from the original order
            int customerId = db.QuerySingle<int>(
                "SELECT CustomerID FROM SalesOrders WHERE SalesOrderID = @id",
                new { id = request.OriginalOrderID });

            using var tx = db.BeginTransaction();
            int returnId = db.QuerySingle<int>(@"
                INSERT INTO ReturnOrders (OriginalOrderID, CustomerID, Reason, Notes, CreatedBy)
                VALUES (@OriginalOrderID, @customerId, @Reason, @Notes, @CreatedBy);
                SELECT CAST(SCOPE_IDENTITY() AS INT);",
                new
                {
                    request.OriginalOrderID,
                    customerId,
                    request.Reason,
                    request.Notes,
                    CreatedBy = AppSession.CurrentUser?.Username
                }, tx);

            foreach (var item in request.Items.Where(i => i.ReturnQty > 0))
            {
                db.Execute(@"
                    INSERT INTO ReturnOrderItems
                           (ReturnID, SalesOrderItemID, ProductID, SKU, ProductName,
                            OriginalQty, ReturnQty, Condition, RestockLocationID)
                    VALUES (@ReturnID, @SalesOrderItemID, @ProductID, @SKU, @ProductName,
                            @OriginalQty, @ReturnQty, @Condition, @RestockLocationID)",
                    new
                    {
                        ReturnID          = returnId,
                        item.SalesOrderItemID,
                        item.ProductID,
                        item.SKU,
                        item.ProductName,
                        item.OriginalQty,
                        item.ReturnQty,
                        item.Condition,
                        item.RestockLocationID
                    }, tx);
            }

            tx.Commit();

            AppLogger.Audit(AppSession.CurrentUser?.Username ?? "system",
                "ReturnCreated", $"ReturnID={returnId} for OrderID={request.OriginalOrderID}");

            return returnId;
        }

        public void ApproveReturn(int returnId)
        {
            using IDbConnection db = new SqlConnection(_cs);
            db.Open();
            using var tx = db.BeginTransaction();

            db.Execute(
                "UPDATE ReturnOrders SET Status='Approved' WHERE ReturnID=@returnId AND Status='Pending'",
                new { returnId }, tx);

            // Credit inventory for resalable items
            var items = db.Query<ReturnOrderItem>(
                "SELECT * FROM ReturnOrderItems WHERE ReturnID=@returnId AND Condition='Resalable'",
                new { returnId }, tx).ToList();

            foreach (var item in items)
            {
                int locationId = item.RestockLocationID ?? db.QueryFirstOrDefault<int>(
                    "SELECT ISNULL(DefaultLocationID, 1) FROM Products WHERE ProductID=@id",
                    new { id = item.ProductID }, tx);

                db.Execute(@"
                    INSERT INTO InventoryTransactions
                           (ProductID, LocationID, QuantityChange, TransactionType, Notes, TransactionDate)
                    VALUES (@ProductID, @locationId, @qty, 'Return', @notes, GETDATE())",
                    new
                    {
                        item.ProductID,
                        locationId,
                        qty   = item.ReturnQty,
                        notes = $"Return #{returnId} approved"
                    }, tx);
            }

            tx.Commit();

            AppLogger.Audit(AppSession.CurrentUser?.Username ?? "system",
                "ReturnApproved", $"ReturnID={returnId}");
        }

        public void RejectReturn(int returnId)
        {
            using IDbConnection db = new SqlConnection(_cs);
            db.Execute(
                "UPDATE ReturnOrders SET Status='Rejected' WHERE ReturnID=@returnId",
                new { returnId });

            AppLogger.Audit(AppSession.CurrentUser?.Username ?? "system",
                "ReturnRejected", $"ReturnID={returnId}");
        }
    }
}
