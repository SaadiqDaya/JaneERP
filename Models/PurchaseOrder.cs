namespace JaneERP.Models
{
    public class PurchaseOrder
    {
        public int       POID         { get; set; }
        public string    PONumber     { get; set; } = string.Empty;
        public int       SupplierID   { get; set; }
        public string?   SupplierName { get; set; }   // joined for display
        public string    Status       { get; set; } = "Draft";
        public DateTime  OrderDate    { get; set; } = DateTime.Now;
        public DateTime? ExpectedDate { get; set; }
        public string?   Notes        { get; set; }
        public string?   CreatedBy    { get; set; }
        public decimal   TotalCost    { get; set; }
        public DateTime  CreatedAt    { get; set; } = DateTime.Now;

        public List<PurchaseOrderItem> Items { get; set; } = new();
    }
}
