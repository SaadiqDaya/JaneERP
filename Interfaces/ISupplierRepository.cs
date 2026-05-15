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

        /// <summary>
        /// Cancels a PO atomically (status guard + audit log). Only cancels if not already
        /// Received or Cancelled.  Received stock is not reversed.
        /// </summary>
        void                 CancelOrder(int poid);

        void                 ReceiveItems(int poid, List<(int poItemId, int qtyReceived)> receivals);

        /// <summary>Paginated PO list with optional status and supplier filters.</summary>
        (List<PurchaseOrder> orders, int totalCount) GetPagedOrders(
            int page, int pageSize, string? statusFilter = null, int? supplierId = null);
    }
}
