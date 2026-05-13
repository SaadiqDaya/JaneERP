using System.Configuration;
using System.Data;
using Dapper;
using JaneERP.Interfaces;
using JaneERP.Models;
using Microsoft.Data.SqlClient;

namespace JaneERP.Data
{
    public class CycleCountRepository : ICycleCountRepository
    {
        private readonly string _cs =
            ConfigurationManager.ConnectionStrings["MyERP"]?.ConnectionString
            ?? throw new InvalidOperationException("Connection string 'MyERP' not found.");

        public void EnsureSchema()
        {
            using IDbConnection db = new SqlConnection(_cs);

            // LastVerifiedAt / LastVerifiedBy per product (global, not per-location for simplicity)
            db.Execute(@"
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Products') AND name = 'LastVerifiedAt')
                    ALTER TABLE Products ADD LastVerifiedAt DATETIME NULL;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Products') AND name = 'LastVerifiedBy')
                    ALTER TABLE Products ADD LastVerifiedBy NVARCHAR(100) NULL;

                IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='LocationCycleSchedule' AND xtype='U')
                CREATE TABLE LocationCycleSchedule (
                    LocationID    INT  NOT NULL PRIMARY KEY REFERENCES Locations(LocationID) ON DELETE CASCADE,
                    FrequencyDays INT  NULL,
                    LastCountedAt DATETIME NULL
                );");
        }

        /// <summary>
        /// Returns the count of active products not verified within the last <paramref name="days"/> days.
        /// Used to drive the overdue badge on the main menu Cycle Count tile.
        /// </summary>
        public int GetOverdueCount(int days = 30)
        {
            using IDbConnection db = new SqlConnection(_cs);
            return db.ExecuteScalar<int>(@"
                SELECT COUNT(*)
                FROM   Products
                WHERE  IsActive = 1
                  AND  (LastVerifiedAt IS NULL OR LastVerifiedAt < DATEADD(DAY, -@days, GETDATE()))",
                new { days });
        }

        /// <summary>
        /// Returns products with system qty scoped to the given location.
        /// When locationId is null (All Locations), returns one row per (product, location)
        /// combination with positive stock, with the location name populated.
        /// When a specific location is provided, returns only products with transactions there.
        /// </summary>
        public List<CycleCountEntry> GetEntries(int? locationId)
        {
            using IDbConnection db = new SqlConnection(_cs);

            if (locationId == null)
            {
                // All Locations: one row per (product, location) with stock > 0
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
            else
            {
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
        }

        /// <summary>Sets or clears the cycle-count schedule for a location. Null frequencyDays removes the schedule.</summary>
        public void SetLocationSchedule(int locationId, int? frequencyDays)
        {
            using IDbConnection db = new SqlConnection(_cs);
            db.Execute(@"
                IF EXISTS (SELECT 1 FROM LocationCycleSchedule WHERE LocationID = @locationId)
                    UPDATE LocationCycleSchedule SET FrequencyDays = @frequencyDays WHERE LocationID = @locationId
                ELSE IF @frequencyDays IS NOT NULL
                    INSERT INTO LocationCycleSchedule (LocationID, FrequencyDays) VALUES (@locationId, @frequencyDays)",
                new { locationId, frequencyDays });
        }

        /// <summary>Returns all locations that have a cycle count schedule configured.</summary>
        public List<ScheduledLocation> GetScheduledLocations()
        {
            using IDbConnection db = new SqlConnection(_cs);
            return db.Query<ScheduledLocation>(@"
                SELECT l.LocationID, l.LocationName, s.FrequencyDays, s.LastCountedAt
                FROM   Locations l
                JOIN   LocationCycleSchedule s ON s.LocationID = l.LocationID
                WHERE  s.FrequencyDays IS NOT NULL
                ORDER  BY l.LocationName").ToList();
        }

        /// <summary>Returns count of scheduled locations overdue for a cycle count.</summary>
        public int GetOverdueScheduledCount()
        {
            using IDbConnection db = new SqlConnection(_cs);
            return db.ExecuteScalar<int>(@"
                SELECT COUNT(*)
                FROM   LocationCycleSchedule s
                WHERE  s.FrequencyDays IS NOT NULL
                  AND  (s.LastCountedAt IS NULL
                        OR DATEDIFF(DAY, s.LastCountedAt, GETDATE()) >= s.FrequencyDays)");
        }

        /// <summary>
        /// Records the verified count for a product. Creates an adjustment transaction for the difference.
        /// </summary>
        public void RecordVerification(int productId, int locationId, int systemQty, int actualQty, string verifiedBy)
        {
            int diff = actualQty - systemQty;
            using var db = new SqlConnection(_cs);
            db.Open();
            using var tx = db.BeginTransaction();
            try
            {
                // Stamp verification on the product
                db.Execute(@"UPDATE Products SET LastVerifiedAt = GETDATE(), LastVerifiedBy = @verifiedBy WHERE ProductID = @productId",
                    new { verifiedBy, productId }, tx);

                // Create adjustment transaction only if there's a difference
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
}
