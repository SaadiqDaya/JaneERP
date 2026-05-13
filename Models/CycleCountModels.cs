namespace JaneERP.Models
{
    /// <summary>One product/location row in a cycle count session.</summary>
    public class CycleCountEntry
    {
        public int       ProductID      { get; set; }
        public string    SKU            { get; set; } = "";
        public string    ProductName    { get; set; } = "";
        public int       SystemQty      { get; set; }
        public int?      LocationID     { get; set; }
        public string?   LocationName   { get; set; }
        public DateTime? LastVerifiedAt { get; set; }
        public string?   LastVerifiedBy { get; set; }
    }

    /// <summary>A warehouse location with its cycle count schedule.</summary>
    public class ScheduledLocation
    {
        public int       LocationID    { get; set; }
        public string    LocationName  { get; set; } = "";
        public int?      FrequencyDays { get; set; }
        public DateTime? LastCountedAt { get; set; }

        public DateTime? NextDueAt => FrequencyDays.HasValue && LastCountedAt.HasValue
            ? LastCountedAt.Value.AddDays(FrequencyDays.Value)
            : (DateTime?)null;
    }
}
