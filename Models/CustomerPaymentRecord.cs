namespace JaneERP.Models
{
    public class CustomerPaymentRecord
    {
        public int      PaymentID       { get; set; }
        public int      SalesOrderID    { get; set; }
        public string?  OrderReference  { get; set; }   // joined from SalesOrders.OrderNumber
        public decimal  Amount          { get; set; }
        public string   PaymentMethod   { get; set; } = "Cash";
        public DateTime PaidAt          { get; set; }
        public string?  Notes           { get; set; }
        public string?  RecordedBy      { get; set; }
    }
}
