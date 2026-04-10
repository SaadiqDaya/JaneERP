namespace JaneERP.Models
{
    public class LocationBin
    {
        public int     BinID       { get; set; }
        public int     LocationID  { get; set; }
        public string  BinCode     { get; set; } = "";
        public string? Description { get; set; }
        public int?    Capacity    { get; set; }
        public bool    IsActive    { get; set; } = true;

        /// <summary>Joined display — populated by queries that join Locations.</summary>
        public string? LocationName { get; set; }

        public override string ToString() => BinCode;
    }
}
