namespace JaneERP.Models
{
    public class SalesOrderItem
    {
        public int       SalesOrderItemID { get; set; }
        public int       SalesOrderID     { get; set; }
        public int       ProductID        { get; set; }
        public string?   SKU              { get; set; }
        public string?   Title            { get; set; }
        public int       Quantity         { get; set; }
        public decimal   UnitPrice        { get; set; }
        // Picking workflow
        public int       PickedQty        { get; set; }
        public string?   PickedBy         { get; set; }
        public DateTime? PickedAt         { get; set; }
        /// <summary>Location(s) from which to pick — populated by GetOrderItemsWithPicking(), not stored.</summary>
        public string?   PickLocation     { get; set; }
        // Shipping workflow
        public int       ShippedQty       { get; set; }
    }
}
