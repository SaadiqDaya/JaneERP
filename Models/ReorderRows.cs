namespace JaneERP.Models
{
    /// <summary>
    /// A product whose available stock is at or below its reorder point.
    /// Returned by IProductRepository.GetProductsAtReorderPoint().
    /// </summary>
    public class ProductReorderRow
    {
        public string  SKU            { get; set; } = "";
        public string  ProductName    { get; set; } = "";
        public int     CurrentStock   { get; set; }
        public int     ReservedQty    { get; set; }
        public int     ReorderPoint   { get; set; }
        public decimal RetailPrice    { get; set; }
        public decimal WholesalePrice { get; set; }

        /// <summary>On-hand minus reservations, floored at zero.</summary>
        public int     Available          => Math.Max(0, CurrentStock - ReservedQty);
        public int     Shortfall          { get; set; }
        public int     SuggestedQty       { get; set; }
        public decimal EstCost            { get; set; }
        public string  RetailPriceDisplay { get; set; } = "";
        public string  EstCostDisplay     { get; set; } = "";

        /// <summary>Populates Shortfall, SuggestedQty (1.5x shortfall), and EstCost.</summary>
        public void Compute()
        {
            Shortfall          = Math.Max(0, ReorderPoint - Available);
            SuggestedQty       = (int)Math.Ceiling(Shortfall * 1.5);
            EstCost            = SuggestedQty * WholesalePrice;
            RetailPriceDisplay = RetailPrice.ToString("C");
            EstCostDisplay     = EstCost.ToString("C");
        }
    }

    /// <summary>
    /// A part whose current stock is at or below its reorder point.
    /// Returned by IPartRepository.GetPartsAtReorderPoint().
    /// </summary>
    public class PartReorderRow
    {
        public string  PartNumber    { get; set; } = "";
        public string  PartName      { get; set; } = "";
        public int     CurrentStock  { get; set; }
        public int     ReorderPoint  { get; set; }
        public decimal UnitCost      { get; set; }

        public int?    DefaultVendorID   { get; set; }
        public string? DefaultVendorName { get; set; }

        public int     Shortfall       { get; set; }
        public int     SuggestedQty    { get; set; }
        public decimal EstCost         { get; set; }
        public string  UnitCostDisplay { get; set; } = "";
        public string  EstCostDisplay  { get; set; } = "";

        /// <summary>Populates Shortfall, SuggestedQty (1.5x, min 5), and EstCost.</summary>
        public void Compute()
        {
            Shortfall      = Math.Max(0, ReorderPoint - CurrentStock);
            SuggestedQty   = (int)Math.Ceiling(Shortfall * 1.5);
            if (SuggestedQty == 0) SuggestedQty = 5;
            EstCost        = SuggestedQty * UnitCost;
            UnitCostDisplay = UnitCost.ToString("C");
            EstCostDisplay  = EstCost.ToString("C");
        }
    }
}
