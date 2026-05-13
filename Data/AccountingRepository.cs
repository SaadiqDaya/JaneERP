using System.Configuration;
using Dapper;
using JaneERP.Interfaces;
using JaneERP.Models;
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

        public AccountingSummary GetSummary(DateTime from, DateTime to)
        {
            using var db = new SqlConnection(_cs);

            decimal revenue = db.ExecuteScalar<decimal>(@"
                SELECT ISNULL(SUM(TotalPrice), 0)
                FROM   SalesOrders
                WHERE  OrderDate >= @from AND OrderDate <= @to
                  AND  (Status = 'Complete' OR IsPaid = 1)", new { from, to });

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

            return new AccountingSummary { Revenue = revenue, Cogs = cogs, Expenses = expenses };
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
    }
}
