namespace JaneERP.Models
{
    /// <summary>P&amp;L summary totals for a date range.</summary>
    public class AccountingSummary
    {
        public decimal Revenue      { get; set; }
        public decimal CreditNotes  { get; set; }   // approved customer credits issued in period
        public decimal NetRevenue   => Revenue - CreditNotes;
        public decimal Cogs         { get; set; }
        public decimal Expenses     { get; set; }
        public decimal GrossProfit  => NetRevenue - Cogs;
        public decimal NetProfit    => GrossProfit - Expenses;
    }

    /// <summary>One row in the expense transaction grid.</summary>
    public class ExpenseRow
    {
        public int      ExpenseID   { get; set; }
        public DateTime ExpenseDate { get; set; }
        public string   Category    { get; set; } = "";
        public decimal  Amount      { get; set; }
        public string?  Description { get; set; }
    }

    /// <summary>An expense category (user-defined label for grouping expenses).</summary>
    public class ExpenseCategory
    {
        public int    CategoryID { get; set; }
        public string Name       { get; set; } = "";
        public bool   IsActive   { get; set; }
    }

    /// <summary>A reusable tax rate preset (e.g. GST 5%, PST 7%).</summary>
    public class TaxRate
    {
        public int     TaxRateID { get; set; }
        public string  Name      { get; set; } = "";
        /// <summary>Rate as a fraction — e.g. 0.05 for 5%.</summary>
        public decimal Rate      { get; set; }
        public bool    IsActive  { get; set; } = true;

        public override string ToString() => $"{Name} ({Rate:P0})";
    }
}
