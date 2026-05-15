using JaneERP.Models;

namespace JaneERP.Interfaces
{
    public interface IPartRepository
    {
        List<Part>    GetAll(bool includeInactive = false);
        Part?         GetById(int id);
        int           Add(Part part);
        void          Update(Part part, string updatedBy = "");
        void          AdjustStock(int partId, int delta, string notes = "");
        (List<Part> parts, int total) GetPagedParts(int page, int pageSize, string? search = null, bool activeOnly = true);
        List<BomEntry>       GetBom(int productId);
        void                 SetBom(int productId, IEnumerable<(int partId, decimal qty, bool createsBatchLoss, decimal batchLossRate)> entries);
        List<BomLabourCost>  GetLabourCosts(int productId);
        void                 SetLabourCosts(int productId, IEnumerable<BomLabourCost> costs);
        List<(int ProductID, string ProductName, string? BomNumber, int PartCount)> GetProductsWithBoms();

        /// <summary>
        /// Returns a dictionary of ProductID → PartNumber for active products whose BOM
        /// consists of exactly one linked part and have no BomNumber set (i.e., "Part" source type).
        /// Used by the Product Explorer to display the correct source label.
        /// </summary>
        Dictionary<int, string> GetLinkedPartNumberByProduct();

        /// <summary>Parts whose current stock is at or below their reorder point.</summary>
        List<PartReorderRow> GetPartsAtReorderPoint();

        // ── Unverified items workflow ─────────────────────────────────────────────
        List<UnverifiedPart> GetUnverifiedParts();
        void                 VerifyParts(IEnumerable<int> partIds);
    }
}
