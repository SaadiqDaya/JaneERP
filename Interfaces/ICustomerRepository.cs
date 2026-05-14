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

        /// <summary>Marks a sales order as paid with the given payment date.</summary>
        void MarkOrderPaid(int salesOrderId, DateTime paidAt);

        // ── Payments ─────────────────────────────────────────────────────────
        void EnsurePaymentsSchema();
        void RecordPayment(int salesOrderId, int customerId, decimal amount, string paymentMethod, DateTime paidAt, string? notes = null);
        List<CustomerPaymentRecord> GetPayments(int customerId);

        // ── CRM Notes ────────────────────────────────────────────────────────
        void EnsureNotesSchema();
        List<CustomerNote> GetNotes(int customerId);
        void AddNote(CustomerNote note);
        void DeleteNote(int noteId);
    }
}
