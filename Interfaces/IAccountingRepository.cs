using JaneERP.Models;

namespace JaneERP.Interfaces
{
    public interface IAccountingRepository
    {
        void EnsureSchema();

        // ── P&L summary ──────────────────────────────────────────────────────────
        AccountingSummary GetSummary(DateTime from, DateTime to);

        // ── Expense transactions ─────────────────────────────────────────────────
        List<ExpenseRow> GetExpenseRows(DateTime from, DateTime to);
        void             AddExpense(int categoryId, decimal amount, string? description, DateTime date, string? createdBy);
        void             DeleteExpense(int expenseId);

        // ── Expense categories ───────────────────────────────────────────────────
        List<ExpenseCategory> GetActiveCategories();
        List<ExpenseCategory> GetAllCategories();
        void                  AddCategory(string name);
        void                  ToggleCategory(int categoryId);
    }
}
