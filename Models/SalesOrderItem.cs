namespace JaneERP.Models
{
    public class SalesOrderItem
    {
        public int      SalesOrderItemID { get; set; }
        public int      SalesOrderID     { get; set; }
        public int      ProductID        { get; set; }
        public string?  SKU              { get; set; }
        public string?  Title            { get; set; }
        public int      Quantity         { get; set; }
        public decimal  UnitPrice        { get; set; }
    }
}
