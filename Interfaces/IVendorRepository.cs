using JaneERP.Models;

namespace JaneERP.Interfaces
{
    public interface IVendorRepository
    {
        IEnumerable<Vendor> GetAll(bool includeInactive = false);
        void                Add(Vendor vendor);
        void                Update(Vendor vendor);
        void                Deactivate(int id);
    }
}
