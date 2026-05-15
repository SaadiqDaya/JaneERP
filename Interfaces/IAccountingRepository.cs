using JaneERP.Models;

namespace JaneERP.Interfaces
{
    public interface IAccountingRepository
    {
        void EnsureSchema();

        // ── P&L summary ──────────────────────────────────────────────────────────
        AccountingSummary GetSummary(DateTime from, DateTime to, bool showPaid = false);

        // ── Revenue rows (order-level drill-through) ─────────────────────────────
        List<RevenueRow> GetRevenueRows(DateTime from, DateTime to, bool showPaid = false);

        // ── COGS breakdown ───────────────────────────────────────────────────────
        COGSBreakdown GetCOGSBreakdown(DateTime from, DateTime to);

        // ── Expense transactions ─────────────────────────────────────────────────
        List<ExpenseRow> GetExpenseRows(DateTime from, DateTime to);
        void             AddExpense(int categoryId, decimal amount, string? description, DateTime date, string? createdBy);
        void             UpdateExpense(int expenseId, int categoryId, decimal amount, string? description, DateTime date, string? updatedBy);
        void             DeleteExpense(int expenseId);

        // ── Paged queries ─────────────────────────────────────────────────────────
        (List<RevenueRow> rows, int total) GetPagedRevenue(int page, int pageSize, DateTime from, DateTime to, bool showPaidOnly = false);

        // ── Expense categories ───────────────────────────────────────────────────
        List<ExpenseCategory> GetActiveCategories();
        List<ExpenseCategory> GetAllCategories();
        void                  AddCategory(string name);
        void                  ToggleCategory(int categoryId);

        // ── Credit notes (read-only; written by ReturnRepository) ────────────────
        List<CustomerCredit> GetCreditNoteRows(DateTime from, DateTime to);

        // ── Tax rates ─────────────────────────────────────────────────────────────
        void           EnsureTaxRatesSchema();
        List<TaxRate>  GetActiveTaxRates();
        List<TaxRate>  GetAllTaxRates();
        void           AddTaxRate(string name, decimal rate);
        void           ToggleTaxRate(int taxRateId);
    }
}
