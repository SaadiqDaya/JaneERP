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
    }
}
