namespace JaneERP.Models
{
    public class PurchaseOrderItem
    {
        public int     POItemID          { get; set; }
        public int     POID              { get; set; }
        public int?    PartID            { get; set; }
        public int?    ProductID         { get; set; }
        public string? SKU               { get; set; }
        public string  ItemName          { get; set; } = string.Empty;
        public int     QuantityOrdered   { get; set; }
        public int     QuantityReceived  { get; set; }
        public decimal UnitCost          { get; set; }

        // Convenience
        public int     QuantityRemaining => QuantityOrdered - QuantityReceived;
        /// <summary>"Part" when this line was created against a raw-material Part, "Product" when it was created against a finished-goods Product.</summary>
        public string  ItemType          => PartID.HasValue ? "Part" : "Product";
    }
}
