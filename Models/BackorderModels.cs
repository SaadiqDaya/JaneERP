namespace JaneERP.Models
{
    public class Backorder
    {
        public int       BackorderID      { get; set; }
        public int       SalesOrderID     { get; set; }
        public string    OrderNumber      { get; set; } = string.Empty;
        public string?   CustomerName     { get; set; }
        public int       SalesOrderItemID { get; set; }
        public int       ProductID        { get; set; }
        public string    SKU              { get; set; } = string.Empty;
        public string    ProductName      { get; set; } = string.Empty;
        public int       BackorderedQty   { get; set; }
        public int       FulfilledQty     { get; set; }
        public int       RemainingQty     => BackorderedQty - FulfilledQty;
        public string    Status           { get; set; } = "Open"; // Open | PartiallyFilled | Fulfilled | Cancelled
        public int       AvailableStock   { get; set; }
        public DateTime  CreatedAt        { get; set; }
        public DateTime? FulfilledAt      { get; set; }
    }

    public class BackorderFulfillResult
    {
        public int           FulfilledCount { get; set; }
        public List<string>  Messages       { get; set; } = [];
    }
}
