using JaneERP.Models;

namespace JaneERP.Interfaces
{
    public interface ICycleCountRepository
    {
        int                       GetOverdueCount(int days = 30);
        List<CycleCountEntry>     GetEntries(int? locationId);
        void                      RecordVerification(int productId, int locationId, int systemQty, int actualQty, string verifiedBy);
        void                      SetLocationSchedule(int locationId, int? frequencyDays);
        List<ScheduledLocation>   GetScheduledLocations();
        int                       GetOverdueScheduledCount();
    }
}
