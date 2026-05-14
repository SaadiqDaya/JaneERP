using JaneERP.Models;

namespace JaneERP.Interfaces
{
    public interface IUomRepository
    {
        void                EnsureSchema();
        List<UnitOfMeasure> GetAll(bool includeInactive = false);
        void                Add(UnitOfMeasure uom);
        void                Update(UnitOfMeasure uom);
        void                Delete(int id);
        /// <summary>Returns abbreviation strings for ComboBox population (e.g. "g", "kg", "mL").</summary>
        List<string>        GetAbbreviations();
        /// <summary>
        /// Converts <paramref name="quantity"/> from <paramref name="fromAbbr"/> to <paramref name="toAbbr"/>
        /// using the base-unit + conversion-factor defined on each UOM row.
        /// Returns false if either abbreviation is unknown or the two units have incompatible base units.
        /// </summary>
        bool TryConvert(string fromAbbr, string toAbbr, decimal quantity, out decimal result);

        // ── Explicit pairwise conversions ────────────────────────────────────────
        /// <summary>Returns all explicitly defined pairwise conversions.</summary>
        List<UomConversion> GetConversions();
        /// <summary>Adds a new pairwise conversion row.  Throws if the pair already exists.</summary>
        void                AddConversion(int fromUomId, int toUomId, decimal multiplier);
        /// <summary>Deletes a pairwise conversion by its ID.</summary>
        void                DeleteConversion(int conversionId);
    }
}
