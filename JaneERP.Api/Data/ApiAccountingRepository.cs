using System.Data;
using Dapper;
using JaneERP.Api.Services;
using Microsoft.Data.SqlClient;

namespace JaneERP.Api.Data;

public class ApiAccountingRepository
{
    private readonly CompanyContext _ctx;
    public ApiAccountingRepository(CompanyContext ctx) => _ctx = ctx;

    private IDbConnection Connect() => new SqlConnection(_ctx.ConnectionString);

    public AccountingSummaryDto GetSummary(DateTime from, DateTime to)
    {
        using var db = Connect();

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

        return new AccountingSummaryDto
        {
            Revenue     = revenue,
            Cogs        = cogs,
            Expenses    = expenses,
            GrossProfit = revenue - cogs,
            NetProfit   = revenue - cogs - expenses
        };
    }

    public List<ExpenseRowDto> GetExpenseRows(DateTime from, DateTime to)
    {
        using var db = Connect();
        return db.Query<ExpenseRowDto>(@"
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

    public List<ExpenseCategoryDto> GetActiveCategories()
    {
        using var db = Connect();
        return db.Query<ExpenseCategoryDto>(
            "SELECT CategoryID, Name FROM ExpenseCategories WHERE IsActive = 1 ORDER BY Name")
            .ToList();
    }

    public void AddExpense(int categoryId, decimal amount, string? description, DateTime date, string? createdBy)
    {
        using var db = Connect();
        db.Execute(@"
            INSERT INTO Expenses (CategoryID, Amount, Description, ExpenseDate, CreatedBy)
            VALUES (@categoryId, @amount, @description, @date, @createdBy)",
            new { categoryId, amount, description, date, createdBy });

        // Resolve category name for the audit trail
        var categoryName = db.ExecuteScalar<string>(
            "SELECT ISNULL(Name, '') FROM ExpenseCategories WHERE CategoryID = @categoryId",
            new { categoryId }) ?? categoryId.ToString();

        db.Execute(@"
            INSERT INTO AuditLog (UserName, Action, Details, LoggedAt)
            VALUES (@user, 'AddExpense', CONCAT('Category: ', @category, ', Amount: ', @amount), GETDATE())",
            new
            {
                user     = createdBy ?? "unknown",
                category = categoryName,
                amount
            });
    }
}

// ── Response DTOs ─────────────────────────────────────────────────────────────

public record AccountingSummaryDto(
    decimal Revenue,
    decimal Cogs,
    decimal GrossProfit,
    decimal Expenses,
    decimal NetProfit)
{
    public AccountingSummaryDto() : this(0, 0, 0, 0, 0) { }
}

public record ExpenseRowDto(
    int       ExpenseID,
    DateTime  ExpenseDate,
    string    Category,
    decimal   Amount,
    string?   Description)
{
    public ExpenseRowDto() : this(0, default, "", 0, null) { }
}

public record ExpenseCategoryDto(int CategoryID, string Name)
{
    public ExpenseCategoryDto() : this(0, "") { }
}
