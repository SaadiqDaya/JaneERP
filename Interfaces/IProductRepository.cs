using JaneERP.Models;

namespace JaneERP.Interfaces
{
    public interface IProductRepository
    {
        /// <summary>Products whose available stock (on-hand minus reservations) is at or below their reorder point.</summary>
        List<ProductReorderRow> GetProductsAtReorderPoint();
        IEnumerable<Product>          GetProducts(bool showInactive = false, int? locationId = null);
        Product?                      GetProductById(int productId);
        int                           GetUnverifiedCount();
        IEnumerable<string>           GetAllAttributeNames();
        IEnumerable<ProductAttribute> GetAttributes(int productId);
        List<ProductAttribute>        GetProductAttributes(IEnumerable<int> productIds);
        IEnumerable<string>           GetDistinctAttributeValues(string attributeName);
        List<(string LocationName, int Stock)> GetStockByLocation(int productId);
        IEnumerable<InventoryTransaction> GetTransactions(int productId, int? locationId = null);
        void                          AddTransaction(InventoryTransaction transaction);
        void                          AddProduct(Product product);
        (int inserted, int updated)   UpsertProducts(IEnumerable<Product> products);
        void                          UpdateProduct(Product product);
        void                          DeactivateProduct(int productId);
        void                          RestoreProduct(int productId);
        int                           GetBomCount(int productId);
        void                          SetBomSource(int productId, int sourceProductId);
        string                        NextBomNumber();
        void                          AssignBomNumber(int productId, string bomNumber);
        void                          ClearBomSource(int productId);
        int                           CountProductsWithNoBOM();
        void                          DeleteProduct(int productId);
    }
}
