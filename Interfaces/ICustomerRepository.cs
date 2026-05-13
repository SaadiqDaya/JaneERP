using JaneERP.Models;

namespace JaneERP.Interfaces
{
    /// <summary>Customer data access. Separate from IShopifySyncService to keep Shopify concerns isolated.</summary>
    public interface ICustomerRepository
    {
        /// <summary>All customers with aggregate order count and lifetime spend, ordered by total spend desc.</summary>
        List<CustomerSummary> GetSummaries();

        /// <summary>All sales orders for a given customer, ordered by date descending.</summary>
        List<CustomerOrder> GetOrders(int customerId);

        /// <summary>Line items for a given sales order with computed LineTotal.</summary>
        List<CustomerOrderItem> GetOrderLineItems(int salesOrderId);

        // ── CRM Notes ────────────────────────────────────────────────────────
        void EnsureNotesSchema();
        List<CustomerNote> GetNotes(int customerId);
        void AddNote(CustomerNote note);
        void DeleteNote(int noteId);
    }
}
