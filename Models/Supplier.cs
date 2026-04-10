namespace JaneERP.Models
{
    public class Supplier
    {
        public int      SupplierID   { get; set; }
        public string   SupplierName { get; set; } = string.Empty;
        public string?  ContactName  { get; set; }
        public string?  Email        { get; set; }
        public string?  Phone        { get; set; }
        public string?  Address      { get; set; }
        public bool     IsActive     { get; set; } = true;
        public string?  Notes        { get; set; }
        public DateTime CreatedAt    { get; set; } = DateTime.Now;

        public override string ToString() => SupplierName;
    }
}
