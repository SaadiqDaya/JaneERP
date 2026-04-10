namespace JaneERP.Models
{
    public class DiscountTier
    {
        public int     TierID          { get; set; }
        public string  TierName        { get; set; } = string.Empty;
        public decimal DiscountPercent { get; set; }
        public string? Description     { get; set; }
        public bool    IsActive        { get; set; } = true;
    }
}
