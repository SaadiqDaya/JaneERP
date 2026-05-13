using JaneERP.Models;

namespace JaneERP.Interfaces
{
    public interface ISupplierRepository
    {
        Supplier             FindOrCreateByName(string name);
        List<PurchaseOrder>  GetUnnotifiedOverduePOs();
        void                 MarkOverdueNotified(int poid);
        List<Supplier>       GetAllSuppliers(bool includeInactive = false);
        int                  AddSupplier(Supplier s);
        void                 UpdateSupplier(Supplier s);
        List<PurchaseOrder>  GetOrders(string? status = null);
        PurchaseOrder?       GetOrder(int poid);
        int                  CreateOrder(PurchaseOrder po);
        void                 UpdateDraftOrder(int poid, PurchaseOrder updated);
        void                 UpdateOrderStatus(int poid, string status);
        void                 ReceiveItems(int poid, List<(int poItemId, int qtyReceived)> receivals);
    }
}
