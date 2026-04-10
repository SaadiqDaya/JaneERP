namespace JaneERP.Models
{
    public class Vendor
    {
        public int    VendorID   { get; set; }
        public string VendorName { get; set; } = string.Empty;
        public string? ContactName { get; set; }
        public string? Email      { get; set; }
        public string? Phone      { get; set; }
        public string? Website    { get; set; }
        public bool   IsActive    { get; set; } = true;
    }
}
