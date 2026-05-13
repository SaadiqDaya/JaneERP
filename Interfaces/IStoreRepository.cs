using JaneERP.Models;

namespace JaneERP.Interfaces
{
    public interface IStoreRepository
    {
        IEnumerable<ShopifyStore> GetAll();
        ShopifyStore              Add(string name, string domain, string token);
        void                      Update(int storeId, string name, string domain, string? newToken);
        void                      Delete(int storeId);
    }
}
