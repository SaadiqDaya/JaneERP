using JaneERP.Models;

namespace JaneERP.Interfaces
{
    public interface IShopifySyncService
    {
        List<Order>          GetErpOrders(string? orderType = null, bool nonShopifyOnly = false);
        bool                 UpdateOrderStatus(int salesOrderId, string newStatus);
        void                 MarkAsPaid(int salesOrderId, string? paymentMethod = null, string? notes = null);
        List<SalesOrderItem> GetOrderItems(int salesOrderId);
        List<Customer>       GetAllCustomers();
        HashSet<long>        GetSyncedOrderIds();
        int                  CreateManualOrder(
                                 string customerEmail, string? customerName,
                                 DateTime orderDate, string? notes, string? currency,
                                 int? storeId,
                                 IEnumerable<(string Sku, string Title, int Qty, decimal UnitPrice)> lineItems,
                                 string status = "Live",
                                 string orderType = "Manual",
                                 string? discountType = null,
                                 decimal discountAmount = 0,
                                 decimal discountPercent = 0,
                                 decimal shippingCost = 0);
        bool                 ProcessShopifyOrder(OrderDetails order, int? storeId = null);
    }
}
