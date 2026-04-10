namespace JaneERP.Models
{
    public class ShopifyStore
    {
        public int      StoreID     { get; set; }
        public string   StoreName   { get; set; } = string.Empty;
        public string   StoreDomain { get; set; } = string.Empty;
        public bool     IsActive    { get; set; } = true;
        public DateTime CreatedAt   { get; set; }

        /// <summary>Not stored in the database — loaded from SecureStore (DPAPI) at runtime.</summary>
        public string? Token { get; set; }

        public override string ToString() => StoreName;
    }
}
