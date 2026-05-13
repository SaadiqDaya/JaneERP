using System.Configuration;
using System.Data;
using Dapper;
using JaneERP.Interfaces;
using JaneERP.Logging;
using JaneERP.Models;
using Microsoft.Data.SqlClient;

namespace JaneERP.Services
{
    /// <summary>
    /// Inventory operations that require transactional logic or span multiple tables.
    /// Extracted from form code so the same rules apply across any UI or API consumer.
    /// </summary>
    public class InventoryService : IInventoryService
    {
        private readonly string _cs =
            ConfigurationManager.ConnectionStrings["MyERP"]?.ConnectionString
            ?? throw new InvalidOperationException("Connection string 'MyERP' not found.");

        /// <inheritdoc/>
        public List<LocationStock> GetStockPerLocation(int productId)
        {
            using IDbConnection db = new SqlConnection(_cs);
            return db.Query<LocationStock>(@"
                SELECT  l.LocationID,
                        l.LocationName,
                        SUM(t.QuantityChange) AS StockQty
                FROM    InventoryTransactions t
                JOIN    Locations l ON l.LocationID = t.LocationID
                WHERE   t.ProductID = @productId
                GROUP BY l.LocationID, l.LocationName
                HAVING  SUM(t.QuantityChange) > 0
                ORDER BY l.LocationName",
                new { productId }).ToList();
        }

        /// <inheritdoc/>
        public virtual int GetStockAtLocation(int productId, int locationId)
        {
            using IDbConnection db = new SqlConnection(_cs);
            return db.ExecuteScalar<int>(@"
                SELECT ISNULL(SUM(QuantityChange), 0)
                FROM   InventoryTransactions
                WHERE  ProductID  = @productId
                  AND  LocationID = @locationId",
                new { productId, locationId });
        }

        /// <inheritdoc/>
        public void TransferStock(int productId, int fromLocationId, int toLocationId,
                                  int qty, string notes, string performedBy)
        {
            // Validate stock at source
            int available = GetStockAtLocation(productId, fromLocationId);
            if (available < qty)
                throw new InvalidOperationException(
                    $"Insufficient stock at source location.\n" +
                    $"Available: {available}  |  Requested: {qty}");

            using var db = new SqlConnection(_cs);
            db.Open();
            using var tx = db.BeginTransaction();
            try
            {
                var now = DateTime.Now;

                db.Execute(@"
                    INSERT INTO InventoryTransactions
                        (ProductID, QuantityChange, TransactionType, LocationID, Notes, TransactionDate)
                    VALUES
                        (@productId, @out, 'Transfer Out', @fromLocationId, @notes, @now)",
                    new { productId, @out = -qty, fromLocationId, notes, now }, tx);

                db.Execute(@"
                    INSERT INTO InventoryTransactions
                        (ProductID, QuantityChange, TransactionType, LocationID, Notes, TransactionDate)
                    VALUES
                        (@productId, @qty, 'Transfer In', @toLocationId, @notes, @now)",
                    new { productId, qty, toLocationId, notes, now }, tx);

                tx.Commit();
            }
            catch
            {
                tx.Rollback();
                throw;
            }

            AppLogger.Audit(performedBy, "StockTransfer",
                $"ProductID={productId} qty={qty} from={fromLocationId} to={toLocationId}");
        }

        /// <inheritdoc/>
        public List<ExpiringItem> GetExpiringItems(int dayWindow = 30)
        {
            using IDbConnection db = new SqlConnection(_cs);
            return db.Query<ExpiringItem>(@"
                SELECT p.SKU, p.ProductName, l.LocationName,
                       it.LotNumber, it.ExpirationDate,
                       it.QuantityChange AS Quantity
                FROM   InventoryTransactions it
                JOIN   Products  p ON p.ProductID  = it.ProductID
                LEFT JOIN Locations l ON l.LocationID = it.LocationID
                WHERE  it.ExpirationDate IS NOT NULL
                  AND  it.ExpirationDate <= DATEADD(day, @dayWindow, GETDATE())
                  AND  it.QuantityChange > 0
                ORDER  BY it.ExpirationDate ASC",
                new { dayWindow }).ToList();
        }
    }
}
