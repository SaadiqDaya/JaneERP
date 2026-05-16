using JaneERP.Models;

namespace JaneERP.Interfaces
{
    public interface IShopifySyncService
    {
        List<Order>              GetErpOrders(string? orderType = null, bool nonShopifyOnly = false);
        bool                     UpdateOrderStatus(int salesOrderId, string newStatus);
        void                     MarkAsPaid(int salesOrderId, string? paymentMethod = null, string? notes = null, decimal? amount = null);
        List<SalesOrderItem>     GetOrderItems(int salesOrderId);
        List<ReservationLine>    GetSOReservationItems(int salesOrderId);
        void                     SaveSOReservations(int salesOrderId, IEnumerable<ReservationLine> lines);
        List<Customer>           GetAllCustomers();
        HashSet<long>            GetSyncedOrderIds();
        int                      CreateManualOrder(
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
        bool                     ProcessShopifyOrder(OrderDetails order, int? storeId = null);
        /// <summary>Count of products created from order sync (IsAutoCreated=1) that still need manual setup (IsVerified=0).</summary>
        int                      GetUnverifiedProductCount();
        List<FulfillmentOrder>   GetFulfillmentOrders(params string[] statuses);
        List<SalesOrderItem>     GetOrderItemsWithPicking(int salesOrderId);
        void                     UpdatePickedQty(int salesOrderItemId, int pickedQty, string pickedBy);
        void                     RecordShipment(int salesOrderId, string? trackingNumber, string? carrier);
        bool                     MarkComplete(int salesOrderId);
        /// <summary>Returns true if the order has already had inventory deducted (InventoryAffected = 1).</summary>
        bool                     IsInventoryAffected(int salesOrderId);

        // ── Box Types ──────────────────────────────────────────────────────────
        IReadOnlyList<BoxType> GetBoxTypes(bool activeOnly = true);
        BoxType SaveBoxType(BoxType bt);
        void DeleteBoxType(int boxTypeId);

        // ── Shipments (packing) ────────────────────────────────────────────────
        IReadOnlyList<Shipment> GetShipmentsForOrder(int salesOrderId);
        int CreateShipment(int salesOrderId, int? boxTypeId, string boxLabel, string createdBy);
        void SetShipmentItems(int shipmentId, IReadOnlyList<(int SalesOrderItemId, int Qty)> items, string packedBy);
        void MarkShipmentShipped(int shipmentId, string trackingNumber, string carrier, string shippedBy);
        void DeleteShipment(int shipmentId);
    }
}
