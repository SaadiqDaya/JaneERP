namespace JaneERP.Interfaces
{
    public interface IImportRepository
    {
        /// <summary>Upserts a product by SKU. Returns true if inserted new, false if updated.</summary>
        bool UpsertProduct(string sku, string name, decimal retail, decimal wholesale, int reorder, int stock);
        /// <summary>Upserts a part by PartNumber. Returns true if inserted new, false if updated.</summary>
        bool UpsertPart(string num, string name, decimal cost, int stock);
        /// <summary>Upserts a discount tier by name. Returns true if inserted new, false if updated.</summary>
        bool UpsertDiscountTier(string name, decimal pct, string desc);
        /// <summary>Upserts a customer by email. Returns true if inserted new, false if updated.</summary>
        bool UpsertCustomer(string email, string fullName, string phone);
    }
}
