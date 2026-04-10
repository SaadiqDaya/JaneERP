namespace JaneERP.Models
{
    public class Part
    {
        public int     PartID       { get; set; }
        public string  PartNumber   { get; set; } = string.Empty;
        public string  PartName     { get; set; } = string.Empty;
        public string? Description  { get; set; }
        public decimal UnitCost     { get; set; }
        public int     CurrentStock { get; set; }
        public bool    IsActive     { get; set; } = true;
        public override string ToString() => $"{PartNumber} – {PartName}";
    }

    public class BomEntry
    {
        public int    ProductID  { get; set; }
        public int    PartID     { get; set; }
        public string PartNumber { get; set; } = string.Empty;
        public string PartName   { get; set; } = string.Empty;
        public int    Quantity   { get; set; }
    }
}
