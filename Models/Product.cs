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
        /// <summary>Qty soft-locked by open Live/WIP sales orders. Available = CurrentStock - ReservedQty.</summary>
        public int     ReservedQty       { get; set; }
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

        /// <summary>True when the product was created automatically (Shopify sync, manual order, CSV import).</summary>
        public bool IsAutoCreated { get; set; }
        /// <summary>True once a user has reviewed and confirmed the auto-created record.</summary>
        public bool IsVerified    { get; set; }

        /// <summary>
        /// SQL Server ROWVERSION — auto-updated on every write.
        /// Used for optimistic concurrency in UpdateProduct: if two users load the same product
        /// and one saves first, the second save will see a RowVersion mismatch and fail safely.
        /// Null when not loaded (e.g. from list queries that don't need concurrency control).
        /// </summary>
        public byte[]? RowVersion { get; set; }
    }
}
