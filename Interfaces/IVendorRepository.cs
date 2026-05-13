using JaneERP.Models;

namespace JaneERP.Interfaces
{
    public interface IVendorRepository
    {
        IEnumerable<Vendor> GetAll(bool includeInactive = false);
        void                Add(Vendor vendor);
        void                Update(Vendor vendor);
        void                Deactivate(int id);
        /// <summary>Returns active parts whose DefaultVendorID matches this vendor.</summary>
        List<Part>          GetPartsByVendor(int vendorId);
        /// <summary>Copies new supplier names from PurchaseOrders/Suppliers into Vendors. Returns rows inserted.</summary>
        int                 ImportFromSuppliers();
    }
}
