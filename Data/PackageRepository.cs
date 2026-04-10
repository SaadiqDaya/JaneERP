using System.Configuration;
using System.Data;
using Dapper;
using JaneERP.Models;
using Microsoft.Data.SqlClient;

namespace JaneERP.Data
{
    public class PackageRepository
    {
        private readonly string _connectionString =
            ConfigurationManager.ConnectionStrings["MyERP"]?.ConnectionString
            ?? throw new InvalidOperationException("Connection string 'MyERP' not found in App.config.");

        public void EnsureSchema()
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            db.Execute(@"
                IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='PackageComponents' AND xtype='U')
                CREATE TABLE PackageComponents (
                    PackageComponentID INT IDENTITY PRIMARY KEY,
                    PackageProductID   INT NOT NULL REFERENCES Products(ProductID) ON DELETE CASCADE,
                    ComponentProductID INT NOT NULL REFERENCES Products(ProductID),
                    Quantity           INT NOT NULL DEFAULT 1,
                    Notes              NVARCHAR(500) NULL
                )");
        }

        public List<PackageComponent> GetComponents(int packageProductID)
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            return db.Query<PackageComponent>(@"
                SELECT  pc.PackageComponentID,
                        pc.PackageProductID,
                        pc.ComponentProductID,
                        p.SKU          AS ComponentSKU,
                        p.ProductName  AS ComponentName,
                        pc.Quantity,
                        pc.Notes
                FROM    PackageComponents pc
                JOIN    Products p ON p.ProductID = pc.ComponentProductID
                WHERE   pc.PackageProductID = @packageProductID",
                new { packageProductID }).ToList();
        }

        public void SetComponents(int packageProductID, IEnumerable<PackageComponent> components)
        {
            using var db = new SqlConnection(_connectionString);
            db.Open();
            using var tx = db.BeginTransaction();
            try
            {
                db.Execute("DELETE FROM PackageComponents WHERE PackageProductID = @packageProductID",
                    new { packageProductID }, tx);

                foreach (var c in components)
                {
                    db.Execute(@"
                        INSERT INTO PackageComponents (PackageProductID, ComponentProductID, Quantity, Notes)
                        VALUES (@PackageProductID, @ComponentProductID, @Quantity, @Notes)",
                        new
                        {
                            PackageProductID   = packageProductID,
                            c.ComponentProductID,
                            c.Quantity,
                            c.Notes
                        }, tx);
                }

                tx.Commit();
            }
            catch { tx.Rollback(); throw; }
        }

        public List<PackageComponent> GetPackagesContaining(int componentProductID)
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            return db.Query<PackageComponent>(@"
                SELECT  pc.PackageComponentID,
                        pc.PackageProductID,
                        pc.ComponentProductID,
                        p.SKU          AS ComponentSKU,
                        p.ProductName  AS ComponentName,
                        pc.Quantity,
                        pc.Notes
                FROM    PackageComponents pc
                JOIN    Products p ON p.ProductID = pc.PackageProductID
                WHERE   pc.ComponentProductID = @componentProductID",
                new { componentProductID }).ToList();
        }
    }
}
