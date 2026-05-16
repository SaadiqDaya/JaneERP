using JaneERP.Models;

namespace JaneERP.Interfaces
{
    public interface IBoxTypeRepository
    {
        IReadOnlyList<BoxType> GetBoxTypes(bool activeOnly = false);
        BoxType SaveBoxType(BoxType bt);
        void    DeleteBoxType(int boxTypeId);
    }
}
