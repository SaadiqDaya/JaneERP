namespace JaneERP.Models
{
    public class SalesOrder
    {
        public int      SalesOrderID      { get; set; }
        public long?    ShopifyOrderID    { get; set; }
        public int      OrderNumber       { get; set; }
        public int      CustomerID        { get; set; }
        public int?     StoreID           { get; set; }
        public DateTime OrderDate         { get; set; }
        public decimal  TotalPrice        { get; set; }
        public decimal  ShippingCost      { get; set; }
        public string?  Currency          { get; set; }
        public string?  Notes             { get; set; }
        public string   Status            { get; set; } = "Draft";
        public bool     InventoryAffected { get; set; }
        public string   OrderType         { get; set; } = "Shopify";
        public bool     IsPaid            { get; set; }
        public DateTime? PaidAt           { get; set; }
        public DateTime CreatedAt              { get; set; }
        public int?     OriginalSalesOrderID   { get; set; }
    }
}
