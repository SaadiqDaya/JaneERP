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

    /// <summary>One revenue row in the accounting grid — one row per sales order.</summary>
    public class RevenueRow
    {
        public int      SalesOrderID  { get; set; }
        public int      OrderNumber   { get; set; }
        public DateTime OrderDate     { get; set; }
        public string   CustomerName  { get; set; } = "";
        public decimal  TotalPrice    { get; set; }
        public string   Status        { get; set; } = "";
        public bool     IsPaid        { get; set; }
    }

    /// <summary>Breakdown of COGS into sub-components for a date range.</summary>
    public class COGSBreakdown
    {
        /// <summary>
        /// Raw materials cost derived from BOM × completed batch quantities.
        /// Populated from WorkOrders.MaterialsCost when available; zero if not tracked.
        /// </summary>
        public decimal MaterialsCost  { get; set; }

        /// <summary>
        /// Labour cost derived from labour rate × hours logged.
        /// Populated from WorkOrders.LaborCost when available; zero if not tracked.
        /// </summary>
        public decimal LaborCost      { get; set; }

        /// <summary>
        /// Batch loss cost (concentrate / e-juice loss recorded on cook sessions).
        /// Populated from WorkOrders.BatchLossCost when available; zero if not tracked.
        /// </summary>
        public decimal BatchLossCost  { get; set; }

        /// <summary>
        /// Remaining COGS that is tracked as a single lump (CostOfGoods column on WorkOrders)
        /// and cannot be further decomposed yet.
        /// </summary>
        public decimal OtherCost      { get; set; }

        public decimal Total          => MaterialsCost + LaborCost + BatchLossCost + OtherCost;

        /// <summary>True when sub-component columns are not yet populated in WorkOrders.</summary>
        public bool    IsPlaceholder  { get; set; }
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
