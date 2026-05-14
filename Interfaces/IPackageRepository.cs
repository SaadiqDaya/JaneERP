using JaneERP.Models;

namespace JaneERP.Interfaces
{
    public interface IPackageRepository
    {
        void EnsureSchema();
        List<PackageProductRow> GetAllPackageProducts();
        List<PackageComponent>  GetComponents(int packageProductID);
        void                    SetComponents(int packageProductID, IEnumerable<PackageComponent> components);
        List<PackageComponent>  GetPackagesContaining(int componentProductID);
    }
}
