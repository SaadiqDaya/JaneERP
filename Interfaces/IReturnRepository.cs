using JaneERP.Models;

namespace JaneERP.Interfaces
{
    public interface IReturnRepository
    {
        void EnsureSchema();

        /// <summary>All return orders, optionally filtered to one customer.</summary>
        List<ReturnOrder> GetReturns(int? customerId = null);

        ReturnOrder? GetById(int returnId);

        /// <summary>
        /// Creates a return order in Pending status.
        /// Returns the new ReturnID.
        /// </summary>
        int CreateReturn(CreateReturnRequest request);

        /// <summary>
        /// Approves the return and credits inventory for Resalable items.
        /// Creates positive InventoryTransactions for each resalable line.
        /// </summary>
        void ApproveReturn(int returnId);

        void RejectReturn(int returnId);
    }
}
