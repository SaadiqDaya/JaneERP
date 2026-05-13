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
    }
}
