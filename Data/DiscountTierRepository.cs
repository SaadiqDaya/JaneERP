using System.Configuration;
using System.Data;
using Dapper;
using JaneERP.Models;
using Microsoft.Data.SqlClient;

namespace JaneERP.Data
{
    public class DiscountTierRepository
    {
        private readonly string _connectionString =
            ConfigurationManager.ConnectionStrings["MyERP"]?.ConnectionString
            ?? throw new InvalidOperationException("Connection string 'MyERP' not found in App.config.");

        /// <summary>Creates the DiscountTiers table if it does not exist. Safe to call multiple times.</summary>
        public void EnsureSchema()
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            db.Execute(@"
                IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='DiscountTiers' AND xtype='U')
                CREATE TABLE DiscountTiers (
                    TierID          INT IDENTITY PRIMARY KEY,
                    TierName        NVARCHAR(100) NOT NULL,
                    DiscountPercent DECIMAL(5,2)  NOT NULL DEFAULT 0,
                    Description     NVARCHAR(500) NULL,
                    IsActive        BIT           NOT NULL DEFAULT 1
                );");
        }

        /// <summary>Adds TierID (FK to DiscountTiers) to the Customers table if not already present.</summary>
        public void MigrateCustomerTier()
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            db.Execute(@"
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('Customers') AND name='TierID')
                    ALTER TABLE Customers ADD TierID INT NULL REFERENCES DiscountTiers(TierID);");
        }

        /// <summary>Adds DiscountType, DiscountAmount, and DiscountPercent columns to SalesOrders if not present.</summary>
        public void MigrateOrderDiscount()
        {
            using IDbConnection db = new SqlConnection(_connectionString);

            db.Execute(@"
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('SalesOrders') AND name='DiscountType')
                    ALTER TABLE SalesOrders ADD DiscountType NVARCHAR(20) NULL;");

            db.Execute(@"
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('SalesOrders') AND name='DiscountAmount')
                    ALTER TABLE SalesOrders ADD DiscountAmount DECIMAL(18,2) NULL DEFAULT 0;");

            db.Execute(@"
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID('SalesOrders') AND name='DiscountPercent')
                    ALTER TABLE SalesOrders ADD DiscountPercent DECIMAL(5,2) NULL DEFAULT 0;");
        }

        /// <returns>All discount tiers (including inactive).</returns>
        public IEnumerable<DiscountTier> GetAll()
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            return db.Query<DiscountTier>(
                "SELECT * FROM DiscountTiers ORDER BY TierName").ToList();
        }

        /// <returns>Only active discount tiers.</returns>
        public IEnumerable<DiscountTier> GetActive()
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            return db.Query<DiscountTier>(
                "SELECT * FROM DiscountTiers WHERE IsActive = 1 ORDER BY TierName").ToList();
        }

        /// <summary>Inserts a new discount tier and returns the new TierID.</summary>
        public int Add(DiscountTier tier)
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            return db.QuerySingle<int>(@"
                INSERT INTO DiscountTiers (TierName, DiscountPercent, Description, IsActive)
                VALUES (@TierName, @DiscountPercent, @Description, @IsActive);
                SELECT CAST(SCOPE_IDENTITY() AS INT);", tier);
        }

        /// <summary>Updates an existing tier's name, percent, description, and active state.</summary>
        public void Update(DiscountTier tier)
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            db.Execute(@"
                UPDATE DiscountTiers
                SET    TierName        = @TierName,
                       DiscountPercent = @DiscountPercent,
                       Description     = @Description,
                       IsActive        = @IsActive
                WHERE  TierID = @TierID", tier);
        }

        /// <summary>Marks a tier as inactive (soft delete).</summary>
        public void Deactivate(int id)
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            db.Execute("UPDATE DiscountTiers SET IsActive = 0 WHERE TierID = @id", new { id });
        }

        /// <summary>Returns the discount tier assigned to a customer, or null if none.</summary>
        public DiscountTier? GetTierForCustomer(int customerId)
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            return db.QueryFirstOrDefault<DiscountTier>(@"
                SELECT dt.*
                FROM   DiscountTiers dt
                JOIN   Customers c ON c.TierID = dt.TierID
                WHERE  c.CustomerID = @customerId", new { customerId });
        }
    }
}
