namespace JaneERP.Models
{
    public class ManufacturingOrder
    {
        public int      MOID        { get; set; }
        public string   MONumber    { get; set; } = string.Empty;
        public string   Status      { get; set; } = "Open";   // Open | InProgress | Complete
        public DateTime CreatedAt   { get; set; } = DateTime.Now;
        public string?  Notes       { get; set; }
        public string?  OrderedBy   { get; set; }
        public List<WorkOrder> WorkOrders { get; set; } = [];
        public override string ToString() => $"{MONumber} [{Status}]";
    }

    public class WorkOrder
    {
        public int      WorkOrderID   { get; set; }
        public int      MOID          { get; set; }
        public int      ProductID     { get; set; }
        public string   ProductName   { get; set; } = string.Empty;
        public string   SKU           { get; set; } = string.Empty;
        public int      Quantity      { get; set; }
        public string   Status        { get; set; } = "Pending"; // Pending | InProgress | Complete
        public string?  Notes         { get; set; }
        public DateTime? CompletedAt  { get; set; }
        /// <summary>Shopify SalesOrderID that triggered this WO (optional).</summary>
        public long?    ShopifyOrderID { get; set; }
    }
}
