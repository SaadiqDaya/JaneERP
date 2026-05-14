namespace JaneERP.Models
{
    public class Part
    {
        public int     PartID            { get; set; }
        public string  PartNumber        { get; set; } = string.Empty;
        public string  PartName          { get; set; } = string.Empty;
        public string? Description       { get; set; }
        public decimal UnitCost          { get; set; }
        public int     CurrentStock      { get; set; }
        public bool    IsActive          { get; set; } = true;
        public int?    DefaultVendorID   { get; set; }
        public string? DefaultVendorName { get; set; }
        public string? UnitOfMeasure     { get; set; }
        /// <summary>Specific gravity (g/ml). Set for liquids so the cook session can show gram equivalents.</summary>
        public decimal? Density          { get; set; }
        /// <summary>True when the part was created automatically (synced from a product).</summary>
        public bool    IsAutoCreated     { get; set; }
        /// <summary>True once a user has reviewed and confirmed the auto-created record.</summary>
        public bool    IsVerified        { get; set; }
        public override string ToString() => $"{PartNumber} – {PartName}";
    }

    public class BomEntry
    {
        public int     ProductID         { get; set; }
        public int     PartID            { get; set; }
        public string  PartNumber        { get; set; } = string.Empty;
        public string  PartName          { get; set; } = string.Empty;
        public decimal Quantity          { get; set; }
        public string? UnitOfMeasure     { get; set; }
        public decimal UnitCost          { get; set; }
        public decimal LineCost          => UnitCost * Quantity;
        /// <summary>True when this ingredient absorbs batch loss (liquids: VG, PG, Nic, concentrate).
        /// False for count items (bottles, labels) and labour.</summary>
        public bool    CreatesBatchLoss  { get; set; }
        /// <summary>Per-row override rate (%). 0 = use the cook session's default rate.</summary>
        public decimal BatchLossRate     { get; set; }
    }

    public class BomLabourCost
    {
        public int     LabourCostID { get; set; }
        public int     ProductID    { get; set; }
        public string  Description  { get; set; } = "Labour";
        public decimal HourlyRate   { get; set; }
        public decimal Hours        { get; set; } = 1;
        public decimal TotalCost    => HourlyRate * Hours;
    }
}
