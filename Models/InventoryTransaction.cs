namespace JaneERP.Models
{
    public class InventoryTransaction
    {
        public int       TransactionID   { get; set; }
        public int       ProductID       { get; set; }
        public int       QuantityChange  { get; set; }
        public string    TransactionType { get; set; } = string.Empty;
        public string    Notes           { get; set; } = string.Empty;
        public DateTime  TransactionDate { get; set; }

        // Multi-location support
        public int?      LocationID      { get; set; }
        public string?   LocationName    { get; set; }

        // Store reference (set for Shopify sales)
        public int?      StoreID         { get; set; }
        public string?   StoreName       { get; set; }

        // Batch / lot traceability
        public string?   LotNumber       { get; set; }
        public DateTime? ExpirationDate  { get; set; }

        /// <summary>Human-readable type for display: store name for Shopify sales, else friendly label.</summary>
        public string DisplayType => TransactionType switch
        {
            "SHOPIFY_SALE" => StoreName ?? "Shopify Sale",
            "Opening"      => "Opening Stock",
            "Adjustment"   => "Manual Entry",
            _              => TransactionType
        };
    }
}