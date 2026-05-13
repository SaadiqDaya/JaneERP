using JaneERP.Models;

namespace JaneERP.Interfaces
{
    public interface ILocationRepository
    {
        IEnumerable<Location>    GetAll(bool includeInactive = false);
        Location?                GetById(int locationId);
        void                     AddLocation(string name, string? notes = null);
        void                     BulkAddLocations(IEnumerable<string> names);
        void                     UpdateLocation(Location location);
        void                     SetActive(int locationId, bool active);
        IEnumerable<LocationBin> GetBinsForLocation(int locationId, bool includeInactive = false);
        void                     AddBin(LocationBin bin);
        void                     UpdateBin(LocationBin bin);
        void                     DeleteBin(int binId);
    }
}
