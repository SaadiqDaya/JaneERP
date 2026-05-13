using System.Data;
using Dapper;
using JaneERP.Api.Models;
using JaneERP.Api.Services;
using Microsoft.Data.SqlClient;

namespace JaneERP.Api.Data;

public class ApiCycleCountRepository
{
    private readonly CompanyContext _ctx;
    public ApiCycleCountRepository(CompanyContext ctx) => _ctx = ctx;

    private IDbConnection Connect() => new SqlConnection(_ctx.ConnectionString);

    public int GetOverdueCount(int days = 30)
    {
        using var db = Connect();
        return db.ExecuteScalar<int>(@"
            SELECT COUNT(*)
            FROM   Products
            WHERE  IsActive = 1
              AND  (LastVerifiedAt IS NULL OR LastVerifiedAt < DATEADD(DAY, -@days, GETDATE()))",
            new { days });
    }

    public List<CycleCountEntry> GetEntries(int? locationId)
    {
        using var db = Connect();

        if (locationId == null)
        {
            return db.Query<CycleCountEntry>(@"
                SELECT  p.ProductID, p.SKU, p.ProductName,
                        SUM(t.QuantityChange) AS SystemQty,
                        l.LocationID,
                        l.LocationName,
                        p.LastVerifiedAt,
                        p.LastVerifiedBy
                FROM    Products p
                JOIN    InventoryTransactions t  ON t.ProductID  = p.ProductID
                LEFT JOIN Locations           l  ON l.LocationID = t.LocationID
                WHERE   p.IsActive = 1
                GROUP BY p.ProductID, p.SKU, p.ProductName, l.LocationID, l.LocationName,
                         p.LastVerifiedAt, p.LastVerifiedBy
                HAVING  SUM(t.QuantityChange) > 0
                ORDER   BY p.ProductName, l.LocationName").ToList();
        }

        return db.Query<CycleCountEntry>(@"
            SELECT  p.ProductID, p.SKU, p.ProductName,
                    ISNULL((
                        SELECT SUM(t.QuantityChange)
                        FROM   InventoryTransactions t
                        WHERE  t.ProductID  = p.ProductID
                          AND  t.LocationID = @LocationID
                    ), 0) AS SystemQty,
                    @LocationID AS LocationID,
                    l.LocationName,
                    p.LastVerifiedAt,
                    p.LastVerifiedBy
            FROM    Products p
            LEFT JOIN Locations l ON l.LocationID = @LocationID
            WHERE   p.IsActive = 1
              AND   EXISTS (
                        SELECT 1 FROM InventoryTransactions t2
                        WHERE  t2.ProductID  = p.ProductID
                          AND  t2.LocationID = @LocationID)
            ORDER   BY p.ProductName",
            new { LocationID = locationId }).ToList();
    }

    public void RecordVerification(int productId, int locationId, int systemQty, int actualQty, string verifiedBy)
    {
        int diff = actualQty - systemQty;
        using var db = new SqlConnection(_ctx.ConnectionString);
        db.Open();
        using var tx = db.BeginTransaction();
        try
        {
            db.Execute(
                "UPDATE Products SET LastVerifiedAt = GETDATE(), LastVerifiedBy = @verifiedBy WHERE ProductID = @productId",
                new { verifiedBy, productId }, tx);

            if (diff != 0)
            {
                db.Execute(@"
                    INSERT INTO InventoryTransactions (ProductID, QuantityChange, TransactionType, Notes, TransactionDate, LocationID)
                    VALUES (@ProductID, @QuantityChange, 'Cycle Count', @Notes, GETDATE(), @LocationID)",
                    new
                    {
                        ProductID      = productId,
                        QuantityChange = diff,
                        Notes          = $"Cycle count adjustment by {verifiedBy} (expected {systemQty}, actual {actualQty})",
                        LocationID     = locationId
                    }, tx);
            }

            tx.Commit();
        }
        catch { tx.Rollback(); throw; }
    }
}
