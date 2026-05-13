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

    /// <summary>
    /// A monetary credit issued to a customer, typically from an approved return.
    /// Reduces effective revenue in accounting until redeemed.
    /// </summary>
    public class CustomerCredit
    {
        public int       CreditID          { get; set; }
        public int       CustomerID        { get; set; }
        public string?   CustomerName      { get; set; }
        public int?      ReturnID          { get; set; }
        public decimal   Amount            { get; set; }
        public string    CreditType        { get; set; } = "Return"; // Return | Manual | Adjustment
        public string?   Notes             { get; set; }
        public bool      IsRedeemed        { get; set; }
        public DateTime? RedeemedAt        { get; set; }
        public int?      RedeemedOnOrderID { get; set; }
        public string?   CreatedBy         { get; set; }
        public DateTime  CreatedAt         { get; set; }

        public string StatusLabel => IsRedeemed ? "Redeemed" : "Active";
    }

    /// <summary>One row in the returns report — aggregated per return.</summary>
    public class ReturnReportRow
    {
        public int      ReturnID            { get; set; }
        public string   OriginalOrderNumber { get; set; } = string.Empty;
        public string?  CustomerName        { get; set; }
        public DateTime ReturnDate          { get; set; }
        public string   Status              { get; set; } = string.Empty;
        public string?  Reason              { get; set; }
        public int      TotalItems          { get; set; }
        public int      ResalableQty        { get; set; }
        public int      DamagedQty          { get; set; }
        public int      DestroyQty          { get; set; }
        public decimal  CreditAmount        { get; set; }
        public string?  CreatedBy           { get; set; }
        public DateTime CreatedAt           { get; set; }
    }
}
