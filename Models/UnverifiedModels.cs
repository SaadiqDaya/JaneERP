namespace JaneERP.Models
{
    /// <summary>A product that was auto-created and is waiting for a user to verify it.</summary>
    public class UnverifiedProduct
    {
        public int     ProductID    { get; set; }
        public string  SKU          { get; set; } = "";
        public string  ProductName  { get; set; } = "";
        public string  TypeName     { get; set; } = "";
        public decimal RetailPrice  { get; set; }
        public int     CurrentStock { get; set; }
    }

    /// <summary>A part that was auto-created and is waiting for a user to verify it.</summary>
    public class UnverifiedPart
    {
        public int     PartID        { get; set; }
        public string  PartNumber    { get; set; } = "";
        public string  PartName      { get; set; } = "";
        public decimal UnitCost      { get; set; }
        public int     CurrentStock  { get; set; }
    }
}
