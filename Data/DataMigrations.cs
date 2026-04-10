using System.Configuration;
using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;

namespace JaneERP.Data
{
    /// <summary>
    /// One-time idempotent data migrations that run at startup after schema setup.
    /// Each method is safe to call repeatedly — it only acts when work is needed.
    /// </summary>
    internal static class DataMigrations
    {
        private static string ConnStr =>
            ConfigurationManager.ConnectionStrings["MyERP"]?.ConnectionString
            ?? throw new InvalidOperationException("Connection string 'MyERP' not found.");

        private static IDbConnection OpenDb() => new SqlConnection(ConnStr);

        // ── Sync Products → Parts ─────────────────────────────────────────────────

        /// <summary>
        /// Ensures every active Product has a corresponding Part (PartNumber = SKU)
        /// and at minimum a BOM entry linking the product to that Part.
        /// Safe to run repeatedly — only acts where work is needed.
        /// </summary>
        public static void SyncProductsToParts()
        {
            using var db = new SqlConnection(ConnStr);
            db.Open();

            var products = db.Query<(int ProductID, string SKU, string ProductName)>(
                "SELECT ProductID, SKU, ProductName FROM Products WHERE IsActive = 1 AND SKU IS NOT NULL AND SKU <> ''")
                .ToList();

            foreach (var (productId, sku, productName) in products)
            {
                // Ensure the Part exists
                int? partId = db.ExecuteScalar<int?>(
                    "SELECT PartID FROM Parts WHERE PartNumber = @sku", new { sku });

                if (partId == null)
                {
                    partId = db.QuerySingle<int>(@"
                        INSERT INTO Parts (PartNumber, PartName, Description, UnitCost, CurrentStock, IsActive)
                        VALUES (@sku, @productName, NULL, 0, 0, 1);
                        SELECT CAST(SCOPE_IDENTITY() AS INT);",
                        new { sku, productName });
                }

                // Ensure the BOM link exists (the Part is on the product's BOM)
                db.Execute(@"
                    IF NOT EXISTS (SELECT 1 FROM ProductParts WHERE ProductID = @productId AND PartID = @partId)
                    INSERT INTO ProductParts (ProductID, PartID, Quantity) VALUES (@productId, @partId, 1);",
                    new { productId, partId });
            }
        }
    }
}
