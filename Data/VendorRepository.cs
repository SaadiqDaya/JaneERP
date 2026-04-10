using System.Configuration;
using System.Data;
using Dapper;
using JaneERP.Models;
using Microsoft.Data.SqlClient;

namespace JaneERP.Data
{
    public class VendorRepository
    {
        private readonly string _connectionString =
            ConfigurationManager.ConnectionStrings["MyERP"]?.ConnectionString
            ?? throw new InvalidOperationException(
                "Connection string 'MyERP' not found in App.config.");

        public void EnsureSchema()
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            db.Execute(@"
                IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Vendors')
                    CREATE TABLE Vendors (
                        VendorID    INT IDENTITY PRIMARY KEY,
                        VendorName  NVARCHAR(200) NOT NULL,
                        ContactName NVARCHAR(200) NULL,
                        Email       NVARCHAR(200) NULL,
                        Phone       NVARCHAR(50)  NULL,
                        Website     NVARCHAR(300) NULL,
                        IsActive    BIT NOT NULL DEFAULT 1
                    );");
        }

        public IEnumerable<Vendor> GetAll(bool includeInactive = false)
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            string filter = includeInactive ? "1=1" : "IsActive = 1";
            return db.Query<Vendor>($"SELECT * FROM Vendors WHERE {filter} ORDER BY VendorName").ToList();
        }

        public void Add(Vendor vendor)
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            db.Execute(@"
                INSERT INTO Vendors (VendorName, ContactName, Email, Phone, Website, IsActive)
                VALUES (@VendorName, @ContactName, @Email, @Phone, @Website, @IsActive)",
                vendor);
        }

        public void Update(Vendor vendor)
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            db.Execute(@"
                UPDATE Vendors
                SET VendorName  = @VendorName,
                    ContactName = @ContactName,
                    Email       = @Email,
                    Phone       = @Phone,
                    Website     = @Website,
                    IsActive    = @IsActive
                WHERE VendorID = @VendorID",
                vendor);
        }

        public void Deactivate(int id)
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            db.Execute("UPDATE Vendors SET IsActive = 0 WHERE VendorID = @id", new { id });
        }
    }
}
