namespace JaneERP.Models
{
    public class PackageComponent
    {
        public int    PackageComponentID  { get; set; }
        public int    PackageProductID    { get; set; }
        public int    ComponentProductID  { get; set; }
        public string ComponentSKU        { get; set; } = string.Empty;
        public string ComponentName       { get; set; } = string.Empty;
        public int    Quantity            { get; set; } = 1;
        public string? Notes             { get; set; }
    }
}
