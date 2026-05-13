using JaneERP.Models;

namespace JaneERP.Interfaces
{
    public interface IPartRepository
    {
        List<Part>    GetAll(bool includeInactive = false);
        Part?         GetById(int id);
        int           Add(Part part);
        void          Update(Part part);
        void          AdjustStock(int partId, int delta, string notes = "");
        List<BomEntry>       GetBom(int productId);
        void                 SetBom(int productId, IEnumerable<(int partId, decimal qty)> entries);
        List<BomLabourCost>  GetLabourCosts(int productId);
        void                 SetLabourCosts(int productId, IEnumerable<BomLabourCost> costs);
        List<(int ProductID, string ProductName, string? BomNumber, int PartCount)> GetProductsWithBoms();

        /// <summary>Parts whose current stock is at or below their reorder point.</summary>
        List<PartReorderRow> GetPartsAtReorderPoint();

        // ── Unverified items workflow ─────────────────────────────────────────────
        List<UnverifiedPart> GetUnverifiedParts();
        void                 VerifyParts(IEnumerable<int> partIds);
    }
}
