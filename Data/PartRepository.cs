using System.Configuration;
using System.Data;
using Dapper;
using JaneERP.Models;
using Microsoft.Data.SqlClient;

namespace JaneERP.Data
{
    public class PartRepository
    {
        private readonly string _connectionString =
            ConfigurationManager.ConnectionStrings["MyERP"]?.ConnectionString
            ?? throw new InvalidOperationException("Connection string 'MyERP' not found in App.config.");

        public void EnsureSchema()
        {
            using IDbConnection db = new SqlConnection(_connectionString);

            db.Execute(@"
                IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='Parts' AND xtype='U')
                CREATE TABLE Parts (
                    PartID       INT IDENTITY(1,1) PRIMARY KEY,
                    PartNumber   NVARCHAR(100) NOT NULL UNIQUE,
                    PartName     NVARCHAR(200) NOT NULL,
                    Description  NVARCHAR(500) NULL,
                    UnitCost     DECIMAL(18,2) NOT NULL DEFAULT 0,
                    CurrentStock INT           NOT NULL DEFAULT 0,
                    IsActive     BIT           NOT NULL DEFAULT 1
                );

                IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='ProductParts' AND xtype='U')
                CREATE TABLE ProductParts (
                    ProductID INT NOT NULL REFERENCES Products(ProductID) ON DELETE CASCADE,
                    PartID    INT NOT NULL REFERENCES Parts(PartID)       ON DELETE CASCADE,
                    Quantity  INT NOT NULL DEFAULT 1,
                    PRIMARY KEY (ProductID, PartID)
                );");
        }

        public List<Part> GetAll(bool includeInactive = false)
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            string filter = includeInactive ? "1=1" : "IsActive = 1";
            return db.Query<Part>(
                $"SELECT * FROM Parts WHERE {filter} ORDER BY PartNumber").ToList();
        }

        public Part? GetById(int id)
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            return db.QueryFirstOrDefault<Part>("SELECT * FROM Parts WHERE PartID = @id", new { id });
        }

        public int Add(Part part)
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            return db.QuerySingle<int>(@"
                INSERT INTO Parts (PartNumber, PartName, Description, UnitCost, CurrentStock, IsActive)
                VALUES (@PartNumber, @PartName, @Description, @UnitCost, @CurrentStock, @IsActive);
                SELECT CAST(SCOPE_IDENTITY() AS INT);", part);
        }

        public void Update(Part part)
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            db.Execute(@"
                UPDATE Parts
                SET PartNumber   = @PartNumber,
                    PartName     = @PartName,
                    Description  = @Description,
                    UnitCost     = @UnitCost,
                    IsActive     = @IsActive
                WHERE PartID = @PartID", part);
        }

        public void AdjustStock(int partId, int delta, string notes = "")
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            db.Execute("UPDATE Parts SET CurrentStock = CurrentStock + @delta WHERE PartID = @partId",
                new { delta, partId });
        }

        // ── BOM ──────────────────────────────────────────────────────────────────

        public List<BomEntry> GetBom(int productId)
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            return db.Query<BomEntry>(@"
                SELECT pp.ProductID, pp.PartID, p.PartNumber, p.PartName, pp.Quantity
                FROM   ProductParts pp
                JOIN   Parts p ON p.PartID = pp.PartID
                WHERE  pp.ProductID = @productId
                ORDER  BY p.PartNumber", new { productId }).ToList();
        }

        public void SetBom(int productId, IEnumerable<(int partId, int qty)> entries)
        {
            using var db = new SqlConnection(_connectionString);
            db.Open();
            using var tx = db.BeginTransaction();
            try
            {
                db.Execute("DELETE FROM ProductParts WHERE ProductID = @productId",
                    new { productId }, tx);

                foreach (var (partId, qty) in entries.Where(e => e.qty > 0))
                    db.Execute("INSERT INTO ProductParts (ProductID, PartID, Quantity) VALUES (@productId, @partId, @qty)",
                        new { productId, partId, qty }, tx);

                tx.Commit();
            }
            catch { tx.Rollback(); throw; }
        }
    }
}
