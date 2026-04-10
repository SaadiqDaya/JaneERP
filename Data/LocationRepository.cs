using System.Configuration;
using System.Data;
using Dapper;
using JaneERP.Models;
using JaneERP.Logging;
using Microsoft.Data.SqlClient;

namespace JaneERP.Data
{
    public class LocationRepository
    {
        private readonly string _connectionString =
            ConfigurationManager.ConnectionStrings["MyERP"]?.ConnectionString
            ?? throw new InvalidOperationException("Connection string 'MyERP' not found in App.config.");

        /// <summary>
        /// Creates the Locations table if it does not exist, then migrates
        /// InventoryTransactions to add LocationID, LotNumber, and ExpirationDate.
        /// Safe to call multiple times.
        /// </summary>
        public void EnsureSchema()
        {
            using IDbConnection db = new SqlConnection(_connectionString);

            // 1. Locations table
            db.Execute(@"
                IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='Locations' AND xtype='U')
                CREATE TABLE Locations (
                    LocationID   INT IDENTITY(1,1) PRIMARY KEY,
                    LocationName NVARCHAR(100) NOT NULL,
                    IsActive     BIT           NOT NULL DEFAULT 1
                );");

            // 2. InventoryTransactions — LocationID (FK)
            db.Execute(@"
                IF NOT EXISTS (
                    SELECT 1 FROM sys.columns
                    WHERE  object_id = OBJECT_ID('InventoryTransactions') AND name = 'LocationID')
                ALTER TABLE InventoryTransactions
                    ADD LocationID INT NULL REFERENCES Locations(LocationID);");

            // 3. InventoryTransactions — LotNumber
            db.Execute(@"
                IF NOT EXISTS (
                    SELECT 1 FROM sys.columns
                    WHERE  object_id = OBJECT_ID('InventoryTransactions') AND name = 'LotNumber')
                ALTER TABLE InventoryTransactions
                    ADD LotNumber NVARCHAR(100) NULL;");

            // 4. InventoryTransactions — ExpirationDate
            db.Execute(@"
                IF NOT EXISTS (
                    SELECT 1 FROM sys.columns
                    WHERE  object_id = OBJECT_ID('InventoryTransactions') AND name = 'ExpirationDate')
                ALTER TABLE InventoryTransactions
                    ADD ExpirationDate DATETIME NULL;");

            // 5. Products — DefaultLocationID (FK to Locations)
            db.Execute(@"
                IF NOT EXISTS (
                    SELECT 1 FROM sys.columns
                    WHERE  object_id = OBJECT_ID('Products') AND name = 'DefaultLocationID')
                ALTER TABLE Products
                    ADD DefaultLocationID INT NULL REFERENCES Locations(LocationID);");

            // 6. Locations — Notes
            db.Execute(@"
                IF NOT EXISTS (
                    SELECT 1 FROM sys.columns
                    WHERE  object_id = OBJECT_ID('Locations') AND name = 'Notes')
                ALTER TABLE Locations
                    ADD Notes NVARCHAR(500) NULL;");

            // 7. Products — WholesalePrice
            db.Execute(@"
                IF NOT EXISTS (
                    SELECT 1 FROM sys.columns
                    WHERE  object_id = OBJECT_ID('Products') AND name = 'WholesalePrice')
                ALTER TABLE Products
                    ADD WholesalePrice DECIMAL(18,2) NOT NULL DEFAULT 0;");

            // 8. InventoryTransactions — StoreID (FK to Stores)
            db.Execute(@"
                IF NOT EXISTS (
                    SELECT 1 FROM sys.columns
                    WHERE  object_id = OBJECT_ID('InventoryTransactions') AND name = 'StoreID')
                ALTER TABLE InventoryTransactions
                    ADD StoreID INT NULL REFERENCES Stores(StoreID);");
        }

        /// <summary>
        /// Inserts the three default locations if none exist yet.
        /// Idempotent — skips if any locations are already present.
        /// </summary>
        public void SeedDefaultLocations()
        {
            using IDbConnection db = new SqlConnection(_connectionString);

            int count = db.ExecuteScalar<int>("SELECT COUNT(1) FROM Locations");
            if (count > 0) return;

            db.Execute(@"
                INSERT INTO Locations (LocationName, IsActive) VALUES ('Main Warehouse', 1);
                INSERT INTO Locations (LocationName, IsActive) VALUES ('Shipping Dock',  1);
                INSERT INTO Locations (LocationName, IsActive) VALUES ('Quality Lab',    1);");
        }

        /// <returns>All active locations (or every location when <paramref name="includeInactive"/> is true).</returns>
        public IEnumerable<Location> GetAll(bool includeInactive = false)
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            string filter = includeInactive ? "1=1" : "IsActive = 1";
            return db.Query<Location>(
                $"SELECT * FROM Locations WHERE {filter} ORDER BY LocationName").ToList();
        }

        public Location? GetById(int locationId)
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            return db.QueryFirstOrDefault<Location>(
                "SELECT * FROM Locations WHERE LocationID = @locationId",
                new { locationId });
        }

        public void AddLocation(string name, string? notes = null)
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            db.Execute("INSERT INTO Locations (LocationName, IsActive, Notes) VALUES (@name, 1, @notes)",
                new { name, notes });
        }

        /// <summary>Inserts multiple locations in a single transaction. Skips any whose name already exists.</summary>
        public void BulkAddLocations(IEnumerable<string> names)
        {
            using var db = new SqlConnection(_connectionString);
            db.Open();
            using var tx = db.BeginTransaction();
            try
            {
                foreach (var name in names)
                {
                    db.Execute(@"
                        IF NOT EXISTS (SELECT 1 FROM Locations WHERE LocationName = @name)
                            INSERT INTO Locations (LocationName, IsActive) VALUES (@name, 1)",
                        new { name }, tx);
                }
                tx.Commit();
            }
            catch { tx.Rollback(); throw; }
        }

        public void UpdateLocation(Location location)
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            db.Execute(@"
                UPDATE Locations
                SET LocationName = @LocationName, IsActive = @IsActive, Notes = @Notes
                WHERE LocationID = @LocationID",
                location);
        }

        public void SetActive(int locationId, bool active)
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            db.Execute("UPDATE Locations SET IsActive = @active WHERE LocationID = @locationId",
                new { active, locationId });
        }

        // ── Bins ──────────────────────────────────────────────────────────────

        /// <summary>Creates the LocationBins table if it does not exist. Safe to call multiple times.</summary>
        public void EnsureBinsSchema()
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            db.Execute(@"
                IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='LocationBins' AND xtype='U')
                CREATE TABLE LocationBins (
                    BinID       INT IDENTITY(1,1) PRIMARY KEY,
                    LocationID  INT NOT NULL REFERENCES Locations(LocationID),
                    BinCode     NVARCHAR(50)  NOT NULL,
                    Description NVARCHAR(200) NULL,
                    Capacity    INT           NULL,
                    IsActive    BIT           NOT NULL DEFAULT 1,
                    CONSTRAINT UQ_LocationBins_Code UNIQUE (LocationID, BinCode)
                );");
        }

        /// <returns>All bins for the given location (active only by default).</returns>
        public IEnumerable<LocationBin> GetBinsForLocation(int locationId, bool includeInactive = false)
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            string filter = includeInactive ? "1=1" : "lb.IsActive = 1";
            return db.Query<LocationBin>($@"
                SELECT lb.*, l.LocationName
                FROM   LocationBins lb
                JOIN   Locations l ON l.LocationID = lb.LocationID
                WHERE  lb.LocationID = @locationId AND {filter}
                ORDER BY lb.BinCode",
                new { locationId }).ToList();
        }

        public void AddBin(LocationBin bin)
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            bin.BinID = db.ExecuteScalar<int>(@"
                INSERT INTO LocationBins (LocationID, BinCode, Description, Capacity, IsActive)
                VALUES (@LocationID, @BinCode, @Description, @Capacity, @IsActive);
                SELECT SCOPE_IDENTITY();",
                bin);
            AppLogger.Audit("system", "AddBin", $"BinID={bin.BinID} LocationID={bin.LocationID} Code={bin.BinCode}");
        }

        public void UpdateBin(LocationBin bin)
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            db.Execute(@"
                UPDATE LocationBins
                SET BinCode = @BinCode, Description = @Description,
                    Capacity = @Capacity, IsActive = @IsActive
                WHERE BinID = @BinID",
                bin);
            AppLogger.Audit("system", "UpdateBin", $"BinID={bin.BinID} Code={bin.BinCode}");
        }

        /// <summary>Soft-deletes a bin by marking it inactive.</summary>
        public void DeleteBin(int binId)
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            db.Execute("UPDATE LocationBins SET IsActive = 0 WHERE BinID = @binId", new { binId });
            AppLogger.Audit("system", "DeleteBin", $"BinID={binId}");
        }
    }
}
