namespace JaneERP.Models
{
    public class SalesOrder
    {
        public int      SalesOrderID   { get; set; }
        public long     ShopifyOrderID { get; set; }
        public int      OrderNumber    { get; set; }
        public int      CustomerID     { get; set; }
        public DateTime OrderDate      { get; set; }
        public decimal  TotalPrice     { get; set; }
        public string?  Currency       { get; set; }
        public DateTime CreatedAt      { get; set; }
    }
}
