using System.Configuration;
using Dapper;
using JaneERP.Interfaces;
using JaneERP.Models;   // AccountingModels + ReturnModels (CustomerCredit) — same namespace
using Microsoft.Data.SqlClient;

namespace JaneERP.Data
{
    public class AccountingRepository : IAccountingRepository
    {
        private readonly string _cs =
            ConfigurationManager.ConnectionStrings["MyERP"]?.ConnectionString
            ?? throw new InvalidOperationException("Connection string 'MyERP' not found.");

        public void EnsureSchema()
        {
            using var db = new SqlConnection(_cs);
            db.Execute(@"
                IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='ExpenseCategories' AND xtype='U')
                CREATE TABLE ExpenseCategories (
                    CategoryID INT IDENTITY(1,1) PRIMARY KEY,
                    Name       NVARCHAR(100) NOT NULL,
                    IsActive   BIT NOT NULL DEFAULT 1
                );

                IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='Expenses' AND xtype='U')
                CREATE TABLE Expenses (
                    ExpenseID   INT IDENTITY(1,1) PRIMARY KEY,
                    CategoryID  INT           NULL REFERENCES ExpenseCategories(CategoryID),
                    Amount      DECIMAL(18,2) NOT NULL,
                    Description NVARCHAR(500) NULL,
                    ExpenseDate DATETIME      NOT NULL DEFAULT GETDATE(),
                    CreatedAt   DATETIME      NOT NULL DEFAULT GETDATE(),
                    CreatedBy   NVARCHAR(100) NULL
                );");

            // Seed default categories on first run
            db.Execute(@"
                IF NOT EXISTS (SELECT 1 FROM ExpenseCategories)
                BEGIN
                    INSERT INTO ExpenseCategories (Name) VALUES
                        ('Rent / Utilities'),
                        ('Payroll'),
                        ('Supplies'),
                        ('Marketing'),
                        ('Shipping & Logistics'),
                        ('Software & Tools'),
                        ('Other')
                END");
        }

        public AccountingSummary GetSummary(DateTime from, DateTime to, bool showPaid = false)
        {
            using var db = new SqlConnection(_cs);

            // showPaid=false  → no filter: show all invoices (paid + unpaid)
            // showPaid=true   → filter to paid invoices only
            string revenueFilter = showPaid
                ? "AND ISNULL(IsPaid, 0) = 1"
                : "";

            decimal revenue = db.ExecuteScalar<decimal>($@"
                SELECT ISNULL(SUM(TotalPrice), 0)
                FROM   SalesOrders
                WHERE  OrderDate >= @from AND OrderDate <= @to
                  AND  Status NOT IN ('Draft', 'Cancelled')
                  AND  TotalPrice > 0
                  {revenueFilter}", new { from, to });

            decimal cogs = db.ExecuteScalar<decimal>(@"
                SELECT ISNULL(SUM(CostOfGoods), 0)
                FROM   WorkOrders
                WHERE  Status = 'Complete'
                  AND  CompletedAt >= @from AND CompletedAt <= @to
                  AND  CostOfGoods IS NOT NULL", new { from, to });

            decimal expenses = db.ExecuteScalar<decimal>(@"
                SELECT ISNULL(SUM(Amount), 0)
                FROM   Expenses
                WHERE  ExpenseDate >= @from AND ExpenseDate <= @to", new { from, to });

            // Only outstanding (unredeemed) credits reduce period revenue.
            // Redeemed credits have already been consumed by a customer order, so
            // double-counting them here would understate revenue for that later period.
            decimal creditNotes = 0m;
            try
            {
                creditNotes = db.ExecuteScalar<decimal>(@"
                    SELECT ISNULL(SUM(cc.Amount), 0)
                    FROM   CustomerCredits cc
                    LEFT JOIN ReturnOrders ro ON ro.ReturnID = cc.ReturnID
                    WHERE  COALESCE(ro.ReturnDate, cc.CreatedAt) >= @from
                      AND  COALESCE(ro.ReturnDate, cc.CreatedAt) <= @to
                      AND  cc.IsRedeemed = 0",
                    new { from, to });
            }
            catch (Exception ex) { Logging.AppLogger.Info($"[AccountingRepository.GetSummary] CustomerCredits not available: {ex.Message}"); }

            return new AccountingSummary
            {
                Revenue     = revenue,
                CreditNotes = creditNotes,
                Cogs        = cogs,
                Expenses    = expenses
            };
        }

        public List<RevenueRow> GetRevenueRows(DateTime from, DateTime to, bool showPaid = false)
        {
            using var db = new SqlConnection(_cs);
            // showPaid=false → no filter (all invoices); showPaid=true → paid only
            string filter = showPaid ? "AND ISNULL(IsPaid, 0) = 1" : "";
            return db.Query<RevenueRow>($@"
                SELECT so.SalesOrderID,
                       so.OrderNumber,
                       so.OrderDate,
                       ISNULL(c.FullName, c.Email) AS CustomerName,
                       so.TotalPrice,
                       so.Status,
                       so.IsPaid
                FROM   SalesOrders so
                LEFT JOIN Customers c ON c.CustomerID = so.CustomerID
                WHERE  so.OrderDate >= @from AND so.OrderDate <= @to
                  AND  so.Status NOT IN ('Draft', 'Cancelled')
                  AND  so.TotalPrice > 0
                  {filter}
                ORDER  BY so.OrderDate DESC", new { from, to }).ToList();
        }

        public COGSBreakdown GetCOGSBreakdown(DateTime from, DateTime to)
        {
            using var db = new SqlConnection(_cs);

            // Attempt to read sub-component columns (MaterialsCost, LaborCost, BatchLossCost)
            // which may not exist yet in the WorkOrders table.  Fall back gracefully.
            bool hasSubColumns = false;
            try
            {
                hasSubColumns = db.ExecuteScalar<int>(@"
                    SELECT COUNT(1) FROM sys.columns
                    WHERE  object_id = OBJECT_ID('WorkOrders')
                      AND  name IN ('MaterialsCost','LaborCost','BatchLossCost')") == 3;
            }
            catch (Exception ex) { Logging.AppLogger.Info($"[AccountingRepository.GetCOGSBreakdown] Schema check for sub-cost columns failed (treating as missing): {ex.Message}"); }

            if (hasSubColumns)
            {
                var row = db.QueryFirstOrDefault(@"
                    SELECT ISNULL(SUM(MaterialsCost), 0)  AS MaterialsCost,
                           ISNULL(SUM(LaborCost),     0)  AS LaborCost,
                           ISNULL(SUM(BatchLossCost), 0)  AS BatchLossCost
                    FROM   WorkOrders
                    WHERE  Status = 'Complete'
                      AND  CompletedAt >= @from AND CompletedAt <= @to
                      AND  CostOfGoods IS NOT NULL", new { from, to });

                return new COGSBreakdown
                {
                    MaterialsCost = (decimal)(row?.MaterialsCost ?? 0m),
                    LaborCost     = (decimal)(row?.LaborCost     ?? 0m),
                    BatchLossCost = (decimal)(row?.BatchLossCost ?? 0m),
                    OtherCost     = 0m,
                    IsPlaceholder = false
                };
            }
            else
            {
                // Sub-columns not yet added to WorkOrders.
                // Return the total COGS as OtherCost and flag as placeholder so UI can inform the user.
                decimal total = db.ExecuteScalar<decimal>(@"
                    SELECT ISNULL(SUM(CostOfGoods), 0)
                    FROM   WorkOrders
                    WHERE  Status = 'Complete'
                      AND  CompletedAt >= @from AND CompletedAt <= @to
                      AND  CostOfGoods IS NOT NULL", new { from, to });

                return new COGSBreakdown
                {
                    MaterialsCost = 0m,
                    LaborCost     = 0m,
                    BatchLossCost = 0m,
                    OtherCost     = total,
                    IsPlaceholder = true
                };
            }
        }

        public List<ExpenseRow> GetExpenseRows(DateTime from, DateTime to)
        {
            using var db = new SqlConnection(_cs);
            return db.Query<ExpenseRow>(@"
                SELECT e.ExpenseID,
                       e.ExpenseDate,
                       ISNULL(ec.Name, 'Uncategorised') AS Category,
                       e.Amount,
                       e.Description
                FROM   Expenses e
                LEFT JOIN ExpenseCategories ec ON ec.CategoryID = e.CategoryID
                WHERE  e.ExpenseDate >= @from AND e.ExpenseDate <= @to
                ORDER  BY e.ExpenseDate DESC", new { from, to }).ToList();
        }

        public void AddExpense(int categoryId, decimal amount, string? description, DateTime date, string? createdBy)
        {
            using var db = new SqlConnection(_cs);
            db.Execute(@"
                INSERT INTO Expenses (CategoryID, Amount, Description, ExpenseDate, CreatedBy)
                VALUES (@categoryId, @amount, @description, @date, @createdBy)",
                new { categoryId, amount, description, date, createdBy });
        }

        public void UpdateExpense(int expenseId, int categoryId, decimal amount, string? description,
            DateTime date, string? updatedBy)
        {
            using var db = new SqlConnection(_cs);
            db.Execute(@"
                UPDATE Expenses
                SET CategoryID  = @categoryId,
                    Amount      = @amount,
                    Description = @description,
                    ExpenseDate = @date,
                    UpdatedBy   = @updatedBy,
                    UpdatedAt   = @now
                WHERE ExpenseID = @expenseId",
                new { categoryId, amount, description, date, updatedBy, now = DateTime.UtcNow, expenseId });
        }

        public void DeleteExpense(int expenseId)
        {
            using var db = new SqlConnection(_cs);
            db.Execute("DELETE FROM Expenses WHERE ExpenseID = @expenseId", new { expenseId });
        }

        public List<ExpenseCategory> GetActiveCategories()
        {
            using var db = new SqlConnection(_cs);
            return db.Query<ExpenseCategory>(
                "SELECT CategoryID, Name, IsActive FROM ExpenseCategories WHERE IsActive = 1 ORDER BY Name")
                .ToList();
        }

        public List<ExpenseCategory> GetAllCategories()
        {
            using var db = new SqlConnection(_cs);
            return db.Query<ExpenseCategory>(
                "SELECT CategoryID, Name, IsActive FROM ExpenseCategories ORDER BY Name")
                .ToList();
        }

        public void AddCategory(string name)
        {
            using var db = new SqlConnection(_cs);
            db.Execute("INSERT INTO ExpenseCategories (Name) VALUES (@name)", new { name });
        }

        public void ToggleCategory(int categoryId)
        {
            using var db = new SqlConnection(_cs);
            db.Execute("UPDATE ExpenseCategories SET IsActive = 1 - IsActive WHERE CategoryID = @categoryId",
                new { categoryId });
        }

        // ── Tax Rates ─────────────────────────────────────────────────────────────

        public void EnsureTaxRatesSchema()
        {
            using var db = new SqlConnection(_cs);
            db.Execute(@"
                IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='TaxRates' AND xtype='U')
                BEGIN
                    CREATE TABLE TaxRates (
                        TaxRateID INT IDENTITY(1,1) PRIMARY KEY,
                        Name      NVARCHAR(100)  NOT NULL,
                        Rate      DECIMAL(8,6)   NOT NULL,
                        IsActive  BIT            NOT NULL DEFAULT 1
                    );
                    -- Seed common Canadian tax rates
                    INSERT INTO TaxRates (Name, Rate) VALUES
                        ('GST',   0.05),
                        ('PST BC',0.07),
                        ('HST',   0.13);
                END");
        }

        public List<TaxRate> GetActiveTaxRates()
        {
            using var db = new SqlConnection(_cs);
            try
            {
                return db.Query<TaxRate>(
                    "SELECT TaxRateID, Name, Rate, IsActive FROM TaxRates WHERE IsActive = 1 ORDER BY Name")
                    .ToList();
            }
            catch (Exception ex) { Logging.AppLogger.Error($"[AccountingRepository.GetActiveTaxRates] {ex}"); return new List<TaxRate>(); }
        }

        public List<TaxRate> GetAllTaxRates()
        {
            using var db = new SqlConnection(_cs);
            try
            {
                return db.Query<TaxRate>(
                    "SELECT TaxRateID, Name, Rate, IsActive FROM TaxRates ORDER BY Name")
                    .ToList();
            }
            catch (Exception ex) { Logging.AppLogger.Error($"[AccountingRepository.GetAllTaxRates] {ex}"); return new List<TaxRate>(); }
        }

        public void AddTaxRate(string name, decimal rate)
        {
            using var db = new SqlConnection(_cs);
            db.Execute("INSERT INTO TaxRates (Name, Rate) VALUES (@name, @rate)", new { name, rate });
        }

        public void ToggleTaxRate(int taxRateId)
        {
            using var db = new SqlConnection(_cs);
            db.Execute("UPDATE TaxRates SET IsActive = 1 - IsActive WHERE TaxRateID = @taxRateId", new { taxRateId });
        }

        public List<CustomerCredit> GetCreditNoteRows(DateTime from, DateTime to)
        {
            using var db = new SqlConnection(_cs);
            try
            {
                return db.Query<CustomerCredit>(@"
                    SELECT cc.CreditID, cc.CustomerID,
                           ISNULL(c.FullName, c.Email) AS CustomerName,
                           cc.ReturnID, cc.Amount, cc.CreditType, cc.Notes,
                           cc.IsRedeemed, cc.RedeemedAt, cc.RedeemedOnOrderID,
                           cc.CreatedBy, cc.CreatedAt
                    FROM   CustomerCredits cc
                    JOIN   Customers c ON c.CustomerID = cc.CustomerID
                    LEFT JOIN ReturnOrders ro ON ro.ReturnID = cc.ReturnID
                    WHERE  COALESCE(ro.ReturnDate, cc.CreatedAt) >= @from
                      AND  COALESCE(ro.ReturnDate, cc.CreatedAt) <= @to
                    ORDER  BY COALESCE(ro.ReturnDate, cc.CreatedAt) DESC",
                    new { from, to }).ToList();
            }
            catch (Exception ex) { Logging.AppLogger.Error($"[AccountingRepository.GetCreditNoteRows] {ex}"); return []; }
        }

        // ── Paged queries ─────────────────────────────────────────────────────────

        public (List<RevenueRow> rows, int total) GetPagedRevenue(
            int page, int pageSize, DateTime from, DateTime to, bool showPaidOnly = false)
        {
            using var db = new SqlConnection(_cs);
            string paidFilter = showPaidOnly ? "AND ISNULL(IsPaid, 0) = 1" : "";
            string where = $@"
                WHERE  so.OrderDate >= @from AND so.OrderDate <= @to
                  AND  so.Status NOT IN ('Draft', 'Cancelled')
                  AND  so.TotalPrice > 0
                  {paidFilter}";

            int total = db.ExecuteScalar<int>($"SELECT COUNT(*) FROM SalesOrders so {where}", new { from, to });

            var rows = db.Query<RevenueRow>($@"
                SELECT so.SalesOrderID,
                       so.OrderNumber,
                       so.OrderDate,
                       ISNULL(c.FullName, c.Email) AS CustomerName,
                       so.TotalPrice,
                       so.Status,
                       so.IsPaid
                FROM   SalesOrders so
                LEFT JOIN Customers c ON c.CustomerID = so.CustomerID
                {where}
                ORDER BY so.OrderDate DESC
                OFFSET @offset ROWS FETCH NEXT @pageSize ROWS ONLY",
                new { from, to, offset = (page - 1) * pageSize, pageSize }).ToList();

            return (rows, total);
        }
    }
}
