namespace JaneERP.Models
{
    /// <summary>Customer with aggregate order stats. Returned by ICustomerRepository.GetSummaries().</summary>
    public class CustomerSummary
    {
        public int     CustomerID  { get; set; }
        public string  FullName    { get; set; } = "";
        public string  Email       { get; set; } = "";
        public int     OrderCount  { get; set; }
        public decimal TotalSpent  { get; set; }
    }

    /// <summary>A sales order belonging to a customer. Returned by ICustomerRepository.GetOrders().</summary>
    public class CustomerOrder
    {
        public int      SalesOrderID  { get; set; }
        public int      OrderNumber   { get; set; }
        public DateTime OrderDate     { get; set; }
        public decimal  TotalPrice    { get; set; }
        public string?  Currency      { get; set; }
        public string?  OrderType     { get; set; }
        public string?  Status        { get; set; }
        public bool     IsPaid        { get; set; }
        public DateTime? PaidAt       { get; set; }
        public decimal  PaidAmount    { get; set; }
    }

    /// <summary>A line item on a customer order. Returned by ICustomerRepository.GetOrderLineItems().</summary>
    public class CustomerOrderItem
    {
        public string?  SKU         { get; set; }
        public string?  ProductName { get; set; }
        public int      Quantity    { get; set; }
        public decimal  UnitPrice   { get; set; }
        public decimal  LineTotal   { get; set; }
    }
}
