using System.Data;
using Dapper;
using JaneERP.Api.Models;
using JaneERP.Api.Services;
using Microsoft.Data.SqlClient;

namespace JaneERP.Api.Data;

public class ApiLocationRepository
{
    private readonly CompanyContext _ctx;
    public ApiLocationRepository(CompanyContext ctx) => _ctx = ctx;

    private IDbConnection Connect() => new SqlConnection(_ctx.ConnectionString);

    public List<LocationItem> GetActive()
    {
        using var db = Connect();
        return db.Query<LocationItem>(
            "SELECT LocationID, LocationName FROM Locations WHERE IsActive = 1 ORDER BY LocationName").ToList();
    }
}
