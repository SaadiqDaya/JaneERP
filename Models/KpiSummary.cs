namespace JaneERP.Models
{
    public class KpiSummary
    {
        public int     OrdersToday    { get; set; }
        public decimal RevenueToday   { get; set; }
        public int     PendingOrders  { get; set; }
        public int     InStock        { get; set; }
        public int     OutOfStock     { get; set; }
        public int     LowStock       { get; set; }
        public int     OpenWorkOrders { get; set; }
        public int     TasksOverdue   { get; set; }
        public decimal InventoryValue { get; set; }

        // Purchasing
        public int     PendingPOs           { get; set; }
        public decimal OutstandingPOAmount  { get; set; }
        // Tasks
        public int     TasksOpenTotal       { get; set; }
        public int     TasksDueThisWeek     { get; set; }
        // Admin
        public int     ActiveUsers          { get; set; }
        // Data
        public int     TotalProducts        { get; set; }
        public int     TotalParts           { get; set; }
        // Analytics
        public decimal ExpensesLast7Days    { get; set; }
        public decimal ExpensesLast30Days   { get; set; }
        // Time-range variants for Sales
        public decimal RevenueLast7Days     { get; set; }
        public decimal RevenueLast30Days    { get; set; }
        public int     OrdersLast7Days      { get; set; }
        public int     OrdersLast30Days     { get; set; }
    }
}
