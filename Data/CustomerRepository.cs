using System.Configuration;
using System.Data;
using Dapper;
using JaneERP.Interfaces;
using JaneERP.Models;
using JaneERP.Security;
using Microsoft.Data.SqlClient;

namespace JaneERP.Data
{
    /// <summary>Customer read queries and CRM notes. EnsureSchema for Customers is handled by ShopifySyncService.</summary>
    public class CustomerRepository : ICustomerRepository
    {
        private readonly string _cs =
            ConfigurationManager.ConnectionStrings["MyERP"]?.ConnectionString
            ?? throw new InvalidOperationException("Connection string 'MyERP' not found.");

        public List<CustomerSummary> GetSummaries()
        {
            using IDbConnection db = new SqlConnection(_cs);
            return db.Query<CustomerSummary>(@"
                SELECT c.CustomerID,
                       ISNULL(c.FullName, '') AS FullName,
                       c.Email,
                       COUNT(so.SalesOrderID)        AS OrderCount,
                       ISNULL(SUM(so.TotalPrice), 0) AS TotalSpent
                FROM   Customers c
                LEFT JOIN SalesOrders so ON so.CustomerID = c.CustomerID
                GROUP BY c.CustomerID, c.FullName, c.Email
                ORDER  BY TotalSpent DESC").ToList();
        }

        public List<CustomerOrder> GetOrders(int customerId)
        {
            using IDbConnection db = new SqlConnection(_cs);
            return db.Query<CustomerOrder>(@"
                SELECT so.SalesOrderID, so.OrderNumber, so.OrderDate, so.TotalPrice, so.Currency,
                       so.OrderType, so.Status, ISNULL(so.IsPaid, 0) AS IsPaid, so.PaidAt,
                       ISNULL(SUM(cp.Amount), 0) AS PaidAmount
                FROM   SalesOrders so
                LEFT JOIN CustomerPayments cp ON cp.SalesOrderID = so.SalesOrderID
                WHERE  so.CustomerID = @customerId
                GROUP  BY so.SalesOrderID, so.OrderNumber, so.OrderDate, so.TotalPrice, so.Currency,
                          so.OrderType, so.Status, so.IsPaid, so.PaidAt
                ORDER  BY so.OrderDate DESC",
                new { customerId }).ToList();
        }

        public List<CustomerOrderItem> GetOrderLineItems(int salesOrderId)
        {
            using IDbConnection db = new SqlConnection(_cs);
            return db.Query<CustomerOrderItem>(@"
                SELECT ISNULL(soi.SKU, '')                      AS SKU,
                       ISNULL(soi.Title, p.ProductName)         AS ProductName,
                       soi.Quantity,
                       soi.UnitPrice,
                       soi.Quantity * soi.UnitPrice             AS LineTotal
                FROM   SalesOrderItems soi
                LEFT JOIN Products p ON p.ProductID = soi.ProductID
                WHERE  soi.SalesOrderID = @salesOrderId
                ORDER  BY soi.SalesOrderItemID",
                new { salesOrderId }).ToList();
        }

        public void MarkOrderPaid(int salesOrderId, DateTime paidAt)
        {
            using IDbConnection db = new SqlConnection(_cs);
            db.Execute(
                "UPDATE SalesOrders SET IsPaid = 1, PaidAt = @paidAt WHERE SalesOrderID = @salesOrderId",
                new { salesOrderId, paidAt });
        }

        // ── Payments ─────────────────────────────────────────────────────────

        public void EnsurePaymentsSchema()
        {
            using var db = new SqlConnection(_cs);
            db.Execute(@"
                IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='CustomerPayments' AND xtype='U')
                CREATE TABLE CustomerPayments (
                    PaymentID     INT IDENTITY(1,1) PRIMARY KEY,
                    SalesOrderID  INT          NOT NULL,
                    CustomerID    INT          NOT NULL,
                    Amount        DECIMAL(18,2) NOT NULL,
                    PaymentMethod NVARCHAR(50) NOT NULL DEFAULT 'Cash',
                    PaidAt        DATETIME     NOT NULL DEFAULT GETDATE(),
                    Notes         NVARCHAR(500) NULL,
                    RecordedBy    NVARCHAR(100) NULL
                );
                IF NOT EXISTS (
                    SELECT 1 FROM sys.columns
                    WHERE object_id = OBJECT_ID('CustomerPayments') AND name = 'PaymentMethod')
                ALTER TABLE CustomerPayments
                    ADD PaymentMethod NVARCHAR(50) NOT NULL DEFAULT 'Cash';
                IF NOT EXISTS (
                    SELECT 1 FROM sys.columns
                    WHERE object_id = OBJECT_ID('CustomerPayments') AND name = 'RecordedBy')
                ALTER TABLE CustomerPayments
                    ADD RecordedBy NVARCHAR(100) NULL;");
        }

        public void RecordPayment(int salesOrderId, int customerId, decimal amount,
                                  string paymentMethod, DateTime paidAt, string? notes = null)
        {
            using var db = new SqlConnection(_cs);
            db.Open();   // must be open before BeginTransaction
            using var tx = db.BeginTransaction();
            try
            {
                // ── Duplicate / overpayment guard ────────────────────────────
                var existingPayments = db.ExecuteScalar<decimal>(
                    "SELECT ISNULL(SUM(Amount), 0) FROM CustomerPayments WHERE SalesOrderID = @orderId",
                    new { orderId = salesOrderId }, tx);

                var orderTotal = db.ExecuteScalar<decimal>(
                    "SELECT TotalPrice FROM SalesOrders WHERE SalesOrderID = @orderId",
                    new { orderId = salesOrderId }, tx);

                if (existingPayments + amount > orderTotal * 1.01m)
                    throw new Exception(
                        $"Payment of {amount:C} would exceed order total of {orderTotal:C}. " +
                        $"Already paid: {existingPayments:C}.");

                // ── Insert payment record ────────────────────────────────────
                db.Execute(
                    @"INSERT INTO CustomerPayments (SalesOrderID, CustomerID, Amount, PaymentMethod, PaidAt, Notes, RecordedBy)
                      VALUES (@salesOrderId, @customerId, @amount, @paymentMethod, @paidAt, @notes, @recordedBy)",
                    new { salesOrderId, customerId, amount, paymentMethod, paidAt, notes,
                          recordedBy = JaneERP.Security.AppSession.CurrentUser?.Username ?? "system" }, tx);

                // ── Auto-mark paid when total collected covers 99%+ of order ─
                if (existingPayments + amount >= orderTotal * 0.99m)
                    db.Execute(
                        "UPDATE SalesOrders SET IsPaid = 1, PaidDate = @now WHERE SalesOrderID = @orderId",
                        new { now = DateTime.UtcNow, orderId = salesOrderId }, tx);

                tx.Commit();
            }
            catch (Exception ex)
            {
                tx.Rollback();
                Logging.AppLogger.Error($"[CustomerRepository.RecordPayment] salesOrderId={salesOrderId} customerId={customerId}: {ex}");
                throw new Exception($"Payment failed: {ex.Message}", ex);
            }
        }

        public List<CustomerPaymentRecord> GetPayments(int customerId)
        {
            using IDbConnection db = new SqlConnection(_cs);
            try
            {
                return db.Query<CustomerPaymentRecord>(@"
                    SELECT cp.PaymentID, cp.SalesOrderID, cp.Amount, cp.PaymentMethod, cp.PaidAt, cp.Notes, cp.RecordedBy,
                           so.OrderNumber AS OrderReference
                    FROM   CustomerPayments cp
                    JOIN   SalesOrders so ON so.SalesOrderID = cp.SalesOrderID
                    WHERE  cp.CustomerID = @customerId
                    ORDER  BY cp.PaidAt DESC",
                    new { customerId }).ToList();
            }
            catch (Exception ex) { Logging.AppLogger.Error($"[CustomerRepository.GetPayments] customerId={customerId}: {ex}"); return new List<CustomerPaymentRecord>(); }
        }

        public (List<CustomerTransactionRow> rows, int totalCount) GetPagedTransactions(
            int customerId, int page, int pageSize)
        {
            using IDbConnection db = new SqlConnection(_cs);
            try
            {
                int offset = (page - 1) * pageSize;

                // Count total rows across both invoices and payments
                int totalCount = db.ExecuteScalar<int>(@"
                    SELECT COUNT(*) FROM (
                        SELECT SalesOrderId AS RefId
                        FROM SalesOrders WHERE CustomerID = @customerId
                        UNION ALL
                        SELECT PaymentID AS RefId
                        FROM CustomerPayments WHERE CustomerID = @customerId
                    ) t",
                    new { customerId });

                var rows = db.Query<CustomerTransactionRow>(@"
                    SELECT * FROM (
                        SELECT 'Invoice'                                                      AS Type,
                               SalesOrderID                                                  AS RefId,
                               CAST(OrderNumber AS NVARCHAR(50))                             AS Reference,
                               TotalPrice                                                    AS Amount,
                               OrderDate                                                     AS TransDate,
                               CASE WHEN IsPaid = 1 THEN 'Paid' ELSE 'Outstanding' END      AS Status
                        FROM   SalesOrders
                        WHERE  CustomerID = @customerId
                        UNION ALL
                        SELECT 'Payment'                                                     AS Type,
                               PaymentID                                                     AS RefId,
                               ISNULL(PaymentMethod, 'Cash')                                AS Reference,
                               Amount                                                        AS Amount,
                               PaidAt                                                        AS TransDate,
                               'Received'                                                    AS Status
                        FROM   CustomerPayments
                        WHERE  CustomerID = @customerId
                    ) t
                    ORDER BY TransDate DESC
                    OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY",
                    new { customerId, offset, pageSize }).ToList();

                return (rows, totalCount);
            }
            catch (Exception ex)
            {
                Logging.AppLogger.Error($"[CustomerRepository.GetPagedTransactions] customerId={customerId} page={page}: {ex}");
                return (new List<CustomerTransactionRow>(), 0);
            }
        }

        // ── CRM Notes ─────────────────────────────────────────────────────────

        public void EnsureNotesSchema()
        {
            using var db = new SqlConnection(_cs);
            db.Execute(@"
                IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='CustomerNotes' AND xtype='U')
                CREATE TABLE CustomerNotes (
                    NoteID     INT           NOT NULL IDENTITY PRIMARY KEY,
                    CustomerID INT           NOT NULL,
                    NoteText   NVARCHAR(MAX) NOT NULL,
                    NoteType   NVARCHAR(50)  NOT NULL DEFAULT 'Note',
                    CreatedBy  NVARCHAR(100) NULL,
                    CreatedAt  DATETIME      NOT NULL DEFAULT GETDATE()
                );

                IF NOT EXISTS (SELECT 1 FROM sys.indexes
                               WHERE name='IX_CustomerNotes_CustomerID'
                                 AND object_id=OBJECT_ID('CustomerNotes'))
                    CREATE INDEX IX_CustomerNotes_CustomerID
                        ON CustomerNotes (CustomerID)
                        INCLUDE (NoteType, CreatedAt);");
        }

        public List<CustomerNote> GetNotes(int customerId)
        {
            using IDbConnection db = new SqlConnection(_cs);
            return db.Query<CustomerNote>(
                "SELECT * FROM CustomerNotes WHERE CustomerID=@customerId ORDER BY CreatedAt DESC",
                new { customerId }).ToList();
        }

        public void AddNote(CustomerNote note)
        {
            using IDbConnection db = new SqlConnection(_cs);
            db.Execute(@"
                INSERT INTO CustomerNotes (CustomerID, NoteText, NoteType, CreatedBy)
                VALUES (@CustomerID, @NoteText, @NoteType, @CreatedBy)",
                new
                {
                    note.CustomerID,
                    note.NoteText,
                    note.NoteType,
                    CreatedBy = AppSession.CurrentUser?.Username ?? note.CreatedBy
                });
        }

        public void DeleteNote(int noteId)
        {
            using IDbConnection db = new SqlConnection(_cs);
            db.Execute("DELETE FROM CustomerNotes WHERE NoteID=@noteId", new { noteId });
        }
    }
}
