namespace JaneERP.Models
{
    public class ReturnOrder
    {
        public int      ReturnID            { get; set; }
        public int      OriginalOrderID     { get; set; }
        public string   OriginalOrderNumber { get; set; } = string.Empty;
        public int      CustomerID          { get; set; }
        public string?  CustomerName        { get; set; }
        public DateTime ReturnDate          { get; set; }
        public string   Status              { get; set; } = "Pending"; // Pending | Approved | Completed | Rejected
        public string?  Reason              { get; set; }
        public string?  Notes               { get; set; }
        public string?  CreatedBy           { get; set; }
        public DateTime CreatedAt           { get; set; }
        public List<ReturnOrderItem> Items  { get; set; } = [];
    }

    public class ReturnOrderItem
    {
        public int     ReturnItemID      { get; set; }
        public int     ReturnID          { get; set; }
        public int?    SalesOrderItemID  { get; set; }
        public int     ProductID         { get; set; }
        public string  SKU               { get; set; } = string.Empty;
        public string  ProductName       { get; set; } = string.Empty;
        public int     OriginalQty       { get; set; }
        public int     ReturnQty         { get; set; }
        public string  Condition         { get; set; } = "Resalable"; // Resalable | Damaged | Destroy
        public int?    RestockLocationID { get; set; }
    }

    public class CreateReturnRequest
    {
        public int                   OriginalOrderID { get; set; }
        public string?               Reason          { get; set; }
        public string?               Notes           { get; set; }
        public List<ReturnOrderItem> Items           { get; set; } = [];
    }
}
