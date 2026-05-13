namespace JaneERP.Models
{
    public class UnitOfMeasure
    {
        public int     UOMID            { get; set; }
        public string  Name             { get; set; } = string.Empty;
        public string  Abbreviation     { get; set; } = string.Empty;
        /// <summary>The base unit this unit converts to (e.g. "g" for kg, "mL" for L). Null = is itself a base unit.</summary>
        public string? BaseUnit         { get; set; }
        /// <summary>How many of BaseUnit equal 1 of this unit (e.g. kg→g = 1000). 1 = same as base unit.</summary>
        public decimal ConversionFactor { get; set; } = 1;
        public int     DisplayOrder     { get; set; }
        public bool    IsActive         { get; set; } = true;

        public override string ToString() => Abbreviation;
    }
}
