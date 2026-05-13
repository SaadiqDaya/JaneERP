using JaneERP.Models;

namespace JaneERP.Interfaces
{
    /// <summary>
    /// Inventory operations that span multiple tables or require transactional logic.
    /// Extracted from form code so the same business rules can be reused across any UI or API.
    /// </summary>
    public interface IInventoryService
    {
        /// <summary>
        /// Returns the current stock of <paramref name="productId"/> broken down by location,
        /// including only locations that hold positive stock.
        /// </summary>
        List<LocationStock> GetStockPerLocation(int productId);

        /// <summary>
        /// Returns the current stock of <paramref name="productId"/> at a specific location.
        /// Returns 0 if no transactions exist.
        /// </summary>
        int GetStockAtLocation(int productId, int locationId);

        /// <summary>
        /// Atomically moves <paramref name="qty"/> units of <paramref name="productId"/>
        /// from <paramref name="fromLocationId"/> to <paramref name="toLocationId"/>.
        /// Validates that sufficient stock exists at the source location before executing.
        /// Throws <see cref="InvalidOperationException"/> on insufficient stock.
        /// Throws <see cref="Exception"/> on database error (rolled back).
        /// </summary>
        void TransferStock(int productId, int fromLocationId, int toLocationId,
                           int qty, string notes, string performedBy);

        /// <summary>
        /// Returns inventory lots whose expiration date falls within the next
        /// <paramref name="dayWindow"/> days (default 30).
        /// </summary>
        List<ExpiringItem> GetExpiringItems(int dayWindow = 30);
    }
}
