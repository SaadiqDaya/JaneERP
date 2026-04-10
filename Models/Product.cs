namespace JaneERP.Models
{
    public class Product
    {
        public int     ProductID         { get; set; }
        public string  SKU               { get; set; } = string.Empty;
        public string  ProductName       { get; set; } = string.Empty;
        public decimal RetailPrice        { get; set; }
        public decimal WholesalePrice    { get; set; }
        public int     CurrentStock      { get; set; }
        public int     ReorderPoint      { get; set; } = 0;
        /// <summary>Target stock level to order up to when restocking.</summary>
        public int     OrderUpTo         { get; set; } = 0;
        public bool    IsActive          { get; set; }

        /// <summary>The location where this product is normally stored / received.</summary>
        public int?    DefaultLocationID { get; set; }

        /// <summary>Optional product type (used to drive required custom attributes).</summary>
        public int?    ProductTypeID     { get; set; }

        // Joined display names — not stored in Products table
        public string? ProductTypeName      { get; set; }
        public string? DefaultLocationName  { get; set; }

        /// <summary>Default vendor for purchasing this product.</summary>
        public int?    DefaultVendorID      { get; set; }

        /// <summary>Joined display name from the Vendors table (read-only, populated by queries).</summary>
        public string? DefaultVendorName    { get; set; }

        public List<ProductAttribute> Attributes { get; set; } = [];
    }
}
