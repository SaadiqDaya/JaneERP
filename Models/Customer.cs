namespace JaneERP.Models
{
    public class Customer
    {
        public int      CustomerID { get; set; }
        public string   Email      { get; set; } = string.Empty;
        public string?  FullName   { get; set; }
        public DateTime CreatedAt  { get; set; }

        // Discount tier (FK to DiscountTiers)
        public int?     TierID          { get; set; }
        public string?  TierName        { get; set; }
        public decimal? DiscountPercent { get; set; }

        // Per-order discount fields (populated from SalesOrders)
        public string?  DiscountType   { get; set; }
        public decimal? DiscountAmount { get; set; }

        /// <summary>Display text for dropdowns: "Full Name &lt;email&gt;" or just email.</summary>
        public string DisplayLabel => string.IsNullOrWhiteSpace(FullName) ? Email : $"{FullName} <{Email}>";
    }
}
