using JaneERP.Models;

namespace JaneERP.Interfaces
{
    public interface IReturnRepository
    {
        void EnsureSchema();

        /// <summary>All return orders, optionally filtered to one customer.</summary>
        List<ReturnOrder> GetReturns(int? customerId = null);

        ReturnOrder? GetById(int returnId);

        /// <summary>Creates a return order in Pending status. Returns the new ReturnID.</summary>
        int CreateReturn(CreateReturnRequest request);

        /// <summary>
        /// Approves the return: credits inventory for Resalable items and issues a CustomerCredit
        /// whose amount = sum of (ReturnQty × original UnitPrice) across all returned lines.
        /// </summary>
        void ApproveReturn(int returnId);

        void RejectReturn(int returnId);

        // ── Credits ──────────────────────────────────────────────────────────────
        /// <summary>All credits for a specific customer, newest first.</summary>
        List<CustomerCredit> GetCreditsForCustomer(int customerId);

        /// <summary>Total active (non-redeemed) credit balance for a customer.</summary>
        decimal GetActiveCreditBalance(int customerId);

        /// <summary>All credits issued in a date range — used by accounting summary.</summary>
        decimal GetTotalCreditsIssued(DateTime from, DateTime to);

        // ── Report ───────────────────────────────────────────────────────────────
        List<ReturnReportRow> GetReturnReport(DateTime from, DateTime to);
    }
}
