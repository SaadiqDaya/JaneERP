using System.Configuration;
using System.Data;
using Dapper;
using JaneERP.Interfaces;
using JaneERP.Models;
using Microsoft.Data.SqlClient;

namespace JaneERP.Data
{
    public class PartRepository : IPartRepository
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
                    PartID           INT IDENTITY(1,1) PRIMARY KEY,
                    PartNumber       NVARCHAR(100) NOT NULL UNIQUE,
                    PartName         NVARCHAR(200) NOT NULL,
                    Description      NVARCHAR(500) NULL,
                    UnitCost         DECIMAL(18,2) NOT NULL DEFAULT 0,
                    CurrentStock     INT           NOT NULL DEFAULT 0,
                    IsActive         BIT           NOT NULL DEFAULT 1,
                    DefaultVendorID  INT           NULL REFERENCES Vendors(VendorID)
                );

                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Parts') AND name='DefaultVendorID')
                    ALTER TABLE Parts ADD DefaultVendorID INT NULL REFERENCES Vendors(VendorID);
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Parts') AND name='IsAutoCreated')
                    ALTER TABLE Parts ADD IsAutoCreated BIT NOT NULL DEFAULT 0;
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Parts') AND name='IsVerified')
                    ALTER TABLE Parts ADD IsVerified BIT NOT NULL DEFAULT 0;

                IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='ProductParts' AND xtype='U')
                CREATE TABLE ProductParts (
                    ProductID INT NOT NULL REFERENCES Products(ProductID) ON DELETE CASCADE,
                    PartID    INT NOT NULL REFERENCES Parts(PartID)       ON DELETE CASCADE,
                    Quantity  INT NOT NULL DEFAULT 1,
                    PRIMARY KEY (ProductID, PartID)
                );

                IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='BomLabourCosts' AND xtype='U')
                CREATE TABLE BomLabourCosts (
                    LabourCostID INT IDENTITY(1,1) PRIMARY KEY,
                    ProductID    INT           NOT NULL REFERENCES Products(ProductID) ON DELETE CASCADE,
                    Description  NVARCHAR(100) NOT NULL DEFAULT 'Labour',
                    HourlyRate   DECIMAL(18,2) NOT NULL DEFAULT 0,
                    Hours        DECIMAL(10,2) NOT NULL DEFAULT 1
                );");
        }

        public List<Part> GetAll(bool includeInactive = false)
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            string filter = includeInactive ? "1=1" : "p.IsActive = 1";
            return db.Query<Part>($@"
                SELECT p.*, v.VendorName AS DefaultVendorName
                FROM Parts p
                LEFT JOIN Vendors v ON v.VendorID = p.DefaultVendorID
                WHERE {filter}
                ORDER BY p.PartNumber").ToList();
        }

        public Part? GetById(int id)
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            return db.QueryFirstOrDefault<Part>(@"
                SELECT p.*, v.VendorName AS DefaultVendorName
                FROM Parts p
                LEFT JOIN Vendors v ON v.VendorID = p.DefaultVendorID
                WHERE p.PartID = @id", new { id });
        }

        public int Add(Part part)
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            return db.QuerySingle<int>(@"
                INSERT INTO Parts (PartNumber, PartName, Description, UnitCost, CurrentStock, IsActive, DefaultVendorID)
                VALUES (@PartNumber, @PartName, @Description, @UnitCost, @CurrentStock, @IsActive, @DefaultVendorID);
                SELECT CAST(SCOPE_IDENTITY() AS INT);", part);
        }

        public void Update(Part part)
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            db.Execute(@"
                UPDATE Parts
                SET PartNumber      = @PartNumber,
                    PartName        = @PartName,
                    Description     = @Description,
                    UnitCost        = @UnitCost,
                    IsActive        = @IsActive,
                    DefaultVendorID = @DefaultVendorID
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
                SELECT pp.ProductID, pp.PartID, p.PartNumber, p.PartName, pp.Quantity,
                       ISNULL(p.UnitCost, 0) AS UnitCost
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

        // ── BOM Labour Costs ─────────────────────────────────────────────────────

        public List<BomLabourCost> GetLabourCosts(int productId)
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            return db.Query<BomLabourCost>(
                "SELECT * FROM BomLabourCosts WHERE ProductID = @productId ORDER BY LabourCostID",
                new { productId }).ToList();
        }

        public void SetLabourCosts(int productId, IEnumerable<BomLabourCost> costs)
        {
            using var db = new SqlConnection(_connectionString);
            db.Open();
            using var tx = db.BeginTransaction();
            try
            {
                db.Execute("DELETE FROM BomLabourCosts WHERE ProductID = @productId",
                    new { productId }, tx);
                foreach (var c in costs.Where(c => c.HourlyRate > 0 || c.Hours > 0))
                    db.Execute(@"
                        INSERT INTO BomLabourCosts (ProductID, Description, HourlyRate, Hours)
                        VALUES (@ProductID, @Description, @HourlyRate, @Hours)",
                        new { ProductID = productId, c.Description, c.HourlyRate, c.Hours }, tx);
                tx.Commit();
            }
            catch { tx.Rollback(); throw; }
        }

        /// <summary>Returns all active products that have at least one BOM part, with their BOM number and part count.</summary>
        public List<(int ProductID, string ProductName, string? BomNumber, int PartCount)> GetProductsWithBoms()
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            return db.Query(@"
                SELECT p.ProductID, p.ProductName, p.BomNumber,
                       COUNT(pp.PartID) AS PartCount
                FROM   Products p
                JOIN   ProductParts pp ON pp.ProductID = p.ProductID
                WHERE  p.IsActive = 1
                GROUP BY p.ProductID, p.ProductName, p.BomNumber
                ORDER BY p.ProductName")
                .Select(r => ((int)r.ProductID, (string)r.ProductName, (string?)r.BomNumber, (int)r.PartCount))
                .ToList();
        }

        public List<Models.PartReorderRow> GetPartsAtReorderPoint()
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            List<Models.PartReorderRow> rows;
            try
            {
                rows = db.Query<Models.PartReorderRow>(@"
                    SELECT  PartNumber,
                            PartName,
                            CurrentStock,
                            ISNULL(ReorderPoint, 0) AS ReorderPoint,
                            UnitCost
                    FROM    Parts
                    WHERE   IsActive = 1
                      AND   CurrentStock <= ISNULL(ReorderPoint, 5)
                    ORDER   BY PartNumber").ToList();
            }
            catch
            {
                // Fallback: ReorderPoint column may not exist yet
                rows = db.Query<Models.PartReorderRow>(@"
                    SELECT  PartNumber,
                            PartName,
                            CurrentStock,
                            0 AS ReorderPoint,
                            UnitCost
                    FROM    Parts
                    WHERE   IsActive = 1
                      AND   CurrentStock <= 5
                    ORDER   BY PartNumber").ToList();
            }
            foreach (var r in rows) r.Compute();
            return rows;
        }

        // ── Unverified items workflow ─────────────────────────────────────────────

        public List<Models.UnverifiedPart> GetUnverifiedParts()
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            return db.Query<Models.UnverifiedPart>(@"
                SELECT PartID, PartNumber, PartName, UnitCost, CurrentStock
                FROM   Parts
                WHERE  IsAutoCreated = 1 AND IsVerified = 0 AND IsActive = 1
                ORDER  BY PartNumber").ToList();
        }

        public void VerifyParts(IEnumerable<int> partIds)
        {
            var ids = partIds.ToList();
            if (ids.Count == 0) return;
            using IDbConnection db = new SqlConnection(_connectionString);
            db.Execute("UPDATE Parts SET IsVerified = 1 WHERE PartID IN @ids", new { ids });
        }
    }
}
