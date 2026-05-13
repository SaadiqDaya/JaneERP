namespace JaneERP.Models
{
    /// <summary>
    /// Order projection used by the Picking and Packing dashboards.
    /// Includes fulfillment progress fields not present on the base Order model.
    /// </summary>
    public class FulfillmentOrder
    {
        public int       SalesOrderID   { get; set; }
        public int       OrderNumber    { get; set; }
        public string    CustomerName   { get; set; } = "";
        public string    ContactEmail   { get; set; } = "";
        public decimal   TotalPrice     { get; set; }
        public string    Currency       { get; set; } = "";
        public string    Status         { get; set; } = "";
        public string?   Notes          { get; set; }
        // Shipping
        public string?   TrackingNumber { get; set; }
        public string?   Carrier        { get; set; }
        public DateTime? ShippedAt      { get; set; }
        public string?   ShippedBy      { get; set; }
        // Packing
        public DateTime? PackedAt       { get; set; }
        public string?   PackedBy       { get; set; }
        // Picking progress (computed by query)
        public int       ItemCount      { get; set; }
        public int       PickedCount    { get; set; }
        public bool      AllPicked      => ItemCount > 0 && PickedCount >= ItemCount;
        public string    ProgressText   => ItemCount > 0 ? $"{PickedCount}/{ItemCount}" : "—";
    }
}
