using System.Configuration;
using System.Data;
using Dapper;
using JaneERP.Interfaces;
using JaneERP.Models;
using Microsoft.Data.SqlClient;

namespace JaneERP.Data
{
    public class ExpiryRepository : IExpiryRepository
    {
        private readonly string _cs =
            ConfigurationManager.ConnectionStrings["MyERP"]?.ConnectionString
            ?? throw new InvalidOperationException("Connection string 'MyERP' not found.");

        public List<LotStockRow> GetLotStock(int? daysAhead = null)
        {
            using IDbConnection db = new SqlConnection(_cs);

            string filter = daysAhead.HasValue
                ? "AND it.ExpirationDate <= DATEADD(day, @days, GETDATE())"
                : "";

            return db.Query<LotStockRow>($@"
                SELECT
                    p.ProductID,
                    p.SKU,
                    p.ProductName,
                    it.LotNumber,
                    it.ExpirationDate,
                    SUM(it.QuantityChange)  AS CurrentQty,
                    it.LocationID,
                    l.LocationName
                FROM  InventoryTransactions it
                JOIN  Products  p ON p.ProductID  = it.ProductID
                LEFT JOIN Locations l ON l.LocationID = it.LocationID
                WHERE it.LotNumber      IS NOT NULL
                  AND it.ExpirationDate IS NOT NULL
                  {filter}
                GROUP BY p.ProductID, p.SKU, p.ProductName,
                         it.LotNumber, it.ExpirationDate,
                         it.LocationID, l.LocationName
                HAVING SUM(it.QuantityChange) > 0
                ORDER BY it.ExpirationDate ASC",
                new { days = daysAhead }).ToList();
        }
    }
}
