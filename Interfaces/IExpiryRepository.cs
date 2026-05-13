using JaneERP.Models;

namespace JaneERP.Interfaces
{
    /// <summary>Lot and expiry tracking against existing InventoryTransactions data.</summary>
    public interface IExpiryRepository
    {
        /// <summary>
        /// Returns all lots with positive net stock.
        /// When daysAhead is supplied, restricts to lots expiring within that many days
        /// (negative daysAhead = already expired).
        /// </summary>
        List<LotStockRow> GetLotStock(int? daysAhead = null);
    }
}
