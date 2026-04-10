using System.Configuration;
using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;

namespace JaneERP.Data
{
    public class CycleCountEntry
    {
        public int      ProductID      { get; set; }
        public string   SKU            { get; set; } = "";
        public string   ProductName    { get; set; } = "";
        public int      SystemQty      { get; set; }
        public int?     LocationID     { get; set; }
        public string?  LocationName   { get; set; }
        public DateTime? LastVerifiedAt { get; set; }
        public string?  LastVerifiedBy { get; set; }
    }

    public class CycleCountRepository
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
                    ALTER TABLE Products ADD LastVerifiedBy NVARCHAR(100) NULL;");
        }

        /// <summary>Returns products with system qty scoped to the given location (null = all).</summary>
        public List<CycleCountEntry> GetEntries(int? locationId)
        {
            using IDbConnection db = new SqlConnection(_cs);
            return db.Query<CycleCountEntry>(@"
                SELECT p.ProductID, p.SKU, p.ProductName,
                       ISNULL((
                           SELECT SUM(t.QuantityChange)
                           FROM   InventoryTransactions t
                           WHERE  t.ProductID = p.ProductID
                             AND  (@LocationID IS NULL OR t.LocationID = @LocationID)
                       ), 0) AS SystemQty,
                       @LocationID AS LocationID,
                       l.LocationName,
                       p.LastVerifiedAt,
                       p.LastVerifiedBy
                FROM   Products p
                LEFT JOIN Locations l ON l.LocationID = @LocationID
                WHERE  p.IsActive = 1
                ORDER  BY p.ProductName",
                new { LocationID = locationId }).ToList();
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
