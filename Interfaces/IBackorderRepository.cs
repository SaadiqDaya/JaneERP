using JaneERP.Models;

namespace JaneERP.Interfaces
{
    public interface IBackorderRepository
    {
        void EnsureSchema();

        /// <summary>All Open and PartiallyFilled backorders, newest first.</summary>
        List<Backorder> GetOpenBackorders();

        List<Backorder> GetBackordersForProduct(int productId);
        List<Backorder> GetBackordersForOrder(int salesOrderId);

        /// <summary>
        /// Records that qty units of a product are backordered on a specific order line.
        /// Returns the new BackorderID.
        /// </summary>
        int CreateBackorder(int salesOrderId, int salesOrderItemId, int productId, int backorderedQty);

        /// <summary>
        /// Called when new stock arrives for a product.
        /// Fulfils open backorders in FIFO order up to the available quantity.
        /// Does NOT move inventory — caller is responsible for the stock adjustment.
        /// Returns a summary of what was fulfilled.
        /// </summary>
        BackorderFulfillResult FulfillBackorders(int productId, int availableQty);

        void CancelBackorder(int backorderId);
    }
}
