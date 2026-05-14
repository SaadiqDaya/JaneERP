using JaneERP.Models;

namespace JaneERP.Interfaces
{
    public interface IImportRepository
    {
        /// <summary>Upserts a product by SKU. Returns true if inserted new, false if updated.</summary>
        bool UpsertProduct(string sku, string name, decimal retail, decimal wholesale, int reorder, int stock);
        /// <summary>Upserts a part by PartNumber. Returns true if inserted new, false if updated.</summary>
        bool UpsertPart(string num, string name, decimal cost, int stock);
        /// <summary>Upserts a discount tier by name. Returns true if inserted new, false if updated.</summary>
        bool UpsertDiscountTier(string name, decimal pct, string desc);
        /// <summary>Upserts a customer by email. Returns true if inserted new, false if updated.</summary>
        bool UpsertCustomer(string email, string fullName, string phone);
        /// <summary>
        /// Resolves each (sku, from, to, qty) tuple against the database and returns
        /// validated rows ready to show in a preview grid.
        /// </summary>
        List<InventoryMoveRow> ValidateInventoryMoves(
            IEnumerable<(string sku, string from, string to, int? qty)> input);
        /// <summary>
        /// Writes two InventoryTransactions per valid row (debit source, credit destination).
        /// Returns (moved, skipped) counts.
        /// </summary>
        (int moved, int skipped) ExecuteInventoryMoves(
            IEnumerable<InventoryMoveRow> validRows, string movedBy);
    }
}
