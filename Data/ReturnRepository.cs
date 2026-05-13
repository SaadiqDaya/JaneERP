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
                    ReturnID         INT           NOT NULL IDENTITY PRIMARY KEY,
                    OriginalOrderID  INT           NOT NULL,
                    CustomerID       INT           NOT NULL,
                    ReturnDate       DATE          NOT NULL DEFAULT CAST(GETDATE() AS DATE),
                    Status           NVARCHAR(20)  NOT NULL DEFAULT 'Pending',
                    Reason           NVARCHAR(200) NULL,
                    Notes            NVARCHAR(MAX) NULL,
                    CreatedBy        NVARCHAR(100) NULL,
                    CreatedAt        DATETIME      NOT NULL DEFAULT GETDATE()
                );

                IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='ReturnOrderItems' AND xtype='U')
                CREATE TABLE ReturnOrderItems (
                    ReturnItemID      INT           NOT NULL IDENTITY PRIMARY KEY,
                    ReturnID          INT           NOT NULL,
                    SalesOrderItemID  INT           NULL,
                    ProductID         INT           NOT NULL,
                    SKU               NVARCHAR(100) NULL,
                    ProductName       NVARCHAR(200) NULL,
                    OriginalQty       INT           NOT NULL DEFAULT 0,
                    ReturnQty         INT           NOT NULL DEFAULT 1,
                    Condition         NVARCHAR(20)  NOT NULL DEFAULT 'Resalable',
                    RestockLocationID INT           NULL
                );

                IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='CustomerCredits' AND xtype='U')
                CREATE TABLE CustomerCredits (
                    CreditID          INT           NOT NULL IDENTITY PRIMARY KEY,
                    CustomerID        INT           NOT NULL,
                    ReturnID          INT           NULL,
                    Amount            DECIMAL(18,2) NOT NULL,
                    CreditType        NVARCHAR(50)  NOT NULL DEFAULT 'Return',
                    Notes             NVARCHAR(500) NULL,
                    IsRedeemed        BIT           NOT NULL DEFAULT 0,
                    RedeemedAt        DATETIME      NULL,
                    RedeemedOnOrderID INT           NULL,
                    CreatedBy         NVARCHAR(100) NULL,
                    CreatedAt         DATETIME      NOT NULL DEFAULT GETDATE()
                );

                IF NOT EXISTS (SELECT 1 FROM sys.indexes
                               WHERE name='IX_CustomerCredits_CustomerID'
                                 AND object_id=OBJECT_ID('CustomerCredits'))
                    CREATE INDEX IX_CustomerCredits_CustomerID
                        ON CustomerCredits (CustomerID, IsRedeemed)
                        INCLUDE (Amount, ReturnID, CreatedAt);");
        }

        // ── Queries ──────────────────────────────────────────────────────────────

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
                var ids   = returns.Select(r => r.ReturnID).ToArray();
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

        // ── Create ────────────────────────────────────────────────────────────────

        public int CreateReturn(CreateReturnRequest request)
        {
            using IDbConnection db = new SqlConnection(_cs);
            db.Open();

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

        // ── Approve ───────────────────────────────────────────────────────────────

        public void ApproveReturn(int returnId)
        {
            using IDbConnection db = new SqlConnection(_cs);
            db.Open();
            using var tx = db.BeginTransaction();

            // Guard: only move from Pending → Approved
            int affected = db.Execute(
                "UPDATE ReturnOrders SET Status='Approved' WHERE ReturnID=@returnId AND Status='Pending'",
                new { returnId }, tx);
            if (affected == 0) { tx.Rollback(); return; }

            var allItems = db.Query<ReturnOrderItem>(
                "SELECT * FROM ReturnOrderItems WHERE ReturnID=@returnId",
                new { returnId }, tx).ToList();

            // 1. Credit inventory for Resalable items
            foreach (var item in allItems.Where(i => i.Condition == "Resalable"))
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

            // 2. Calculate credit amount = sum of (ReturnQty × original UnitPrice)
            //    Look up UnitPrice from SalesOrderItems via SalesOrderItemID where available,
            //    fall back to product RetailPrice
            decimal creditAmount = 0m;
            int customerId = db.QuerySingle<int>(
                "SELECT CustomerID FROM ReturnOrders WHERE ReturnID=@returnId",
                new { returnId }, tx);

            foreach (var item in allItems)
            {
                decimal unitPrice = 0m;
                if (item.SalesOrderItemID.HasValue)
                {
                    unitPrice = db.QueryFirstOrDefault<decimal>(
                        "SELECT ISNULL(UnitPrice,0) FROM SalesOrderItems WHERE SalesOrderItemID=@id",
                        new { id = item.SalesOrderItemID.Value }, tx);
                }
                if (unitPrice == 0m && item.ProductID > 0)
                {
                    unitPrice = db.QueryFirstOrDefault<decimal>(
                        "SELECT ISNULL(RetailPrice,0) FROM Products WHERE ProductID=@id",
                        new { id = item.ProductID }, tx);
                }
                creditAmount += item.ReturnQty * unitPrice;
            }

            // 3. Issue the credit note
            if (creditAmount > 0m)
            {
                db.Execute(@"
                    INSERT INTO CustomerCredits
                           (CustomerID, ReturnID, Amount, CreditType, Notes, CreatedBy)
                    VALUES (@customerId, @returnId, @creditAmount, 'Return',
                            @notes, @createdBy)",
                    new
                    {
                        customerId,
                        returnId,
                        creditAmount,
                        notes     = $"Credit for Return #{returnId}",
                        createdBy = AppSession.CurrentUser?.Username
                    }, tx);
            }

            tx.Commit();
            AppLogger.Audit(AppSession.CurrentUser?.Username ?? "system",
                "ReturnApproved", $"ReturnID={returnId} CreditIssued={creditAmount:N2}");
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

        // ── Credits ───────────────────────────────────────────────────────────────

        public List<CustomerCredit> GetCreditsForCustomer(int customerId)
        {
            using IDbConnection db = new SqlConnection(_cs);
            return db.Query<CustomerCredit>(@"
                SELECT cc.CreditID, cc.CustomerID,
                       ISNULL(c.FullName, c.Email) AS CustomerName,
                       cc.ReturnID, cc.Amount, cc.CreditType, cc.Notes,
                       cc.IsRedeemed, cc.RedeemedAt, cc.RedeemedOnOrderID,
                       cc.CreatedBy, cc.CreatedAt
                FROM   CustomerCredits cc
                JOIN   Customers c ON c.CustomerID = cc.CustomerID
                WHERE  cc.CustomerID = @customerId
                ORDER  BY cc.CreatedAt DESC",
                new { customerId }).ToList();
        }

        public decimal GetActiveCreditBalance(int customerId)
        {
            using IDbConnection db = new SqlConnection(_cs);
            return db.ExecuteScalar<decimal>(
                "SELECT ISNULL(SUM(Amount),0) FROM CustomerCredits WHERE CustomerID=@customerId AND IsRedeemed=0",
                new { customerId });
        }

        public decimal GetTotalCreditsIssued(DateTime from, DateTime to)
        {
            using IDbConnection db = new SqlConnection(_cs);
            return db.ExecuteScalar<decimal>(@"
                SELECT ISNULL(SUM(Amount),0)
                FROM   CustomerCredits
                WHERE  CreatedAt >= @from AND CreatedAt <= @to",
                new { from, to });
        }

        // ── Report ────────────────────────────────────────────────────────────────

        public List<ReturnReportRow> GetReturnReport(DateTime from, DateTime to)
        {
            using IDbConnection db = new SqlConnection(_cs);
            return db.Query<ReturnReportRow>(@"
                SELECT
                    r.ReturnID,
                    ISNULL(CAST(so.OrderNumber AS NVARCHAR), CAST(r.OriginalOrderID AS NVARCHAR)) AS OriginalOrderNumber,
                    ISNULL(c.FullName, c.Email)     AS CustomerName,
                    r.ReturnDate,
                    r.Status,
                    r.Reason,
                    COUNT(ri.ReturnItemID)           AS TotalItems,
                    ISNULL(SUM(CASE WHEN ri.Condition='Resalable' THEN ri.ReturnQty ELSE 0 END), 0) AS ResalableQty,
                    ISNULL(SUM(CASE WHEN ri.Condition='Damaged'   THEN ri.ReturnQty ELSE 0 END), 0) AS DamagedQty,
                    ISNULL(SUM(CASE WHEN ri.Condition='Destroy'   THEN ri.ReturnQty ELSE 0 END), 0) AS DestroyQty,
                    ISNULL(cc.Amount, 0)             AS CreditAmount,
                    r.CreatedBy,
                    r.CreatedAt
                FROM   ReturnOrders r
                LEFT JOIN SalesOrders so   ON so.SalesOrderID = r.OriginalOrderID
                LEFT JOIN Customers   c    ON c.CustomerID    = r.CustomerID
                LEFT JOIN ReturnOrderItems ri ON ri.ReturnID  = r.ReturnID
                LEFT JOIN CustomerCredits  cc ON cc.ReturnID  = r.ReturnID
                WHERE  r.CreatedAt >= @from AND r.CreatedAt <= @to
                GROUP  BY r.ReturnID, so.OrderNumber, r.OriginalOrderID,
                          c.FullName, c.Email, r.ReturnDate, r.Status, r.Reason,
                          cc.Amount, r.CreatedBy, r.CreatedAt
                ORDER  BY r.CreatedAt DESC",
                new { from, to }).ToList();
        }
    }
}
