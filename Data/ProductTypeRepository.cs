using System.Configuration;
using System.Data;
using Dapper;
using JaneERP.Models;
using Microsoft.Data.SqlClient;

namespace JaneERP.Data
{
    public class ProductTypeRepository
    {
        private readonly string _connectionString =
            ConfigurationManager.ConnectionStrings["MyERP"]?.ConnectionString
            ?? throw new InvalidOperationException("Connection string 'MyERP' not found in App.config.");

        public void EnsureSchema()
        {
            using IDbConnection db = new SqlConnection(_connectionString);

            db.Execute(@"
                IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='ProductTypes' AND xtype='U')
                CREATE TABLE ProductTypes (
                    ProductTypeID INT IDENTITY(1,1) PRIMARY KEY,
                    TypeName      NVARCHAR(100) NOT NULL UNIQUE
                );

                IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='ProductTypeAttributes' AND xtype='U')
                CREATE TABLE ProductTypeAttributes (
                    ProductTypeID  INT           NOT NULL REFERENCES ProductTypes(ProductTypeID) ON DELETE CASCADE,
                    AttributeName  NVARCHAR(100) NOT NULL,
                    IsRequired     BIT           NOT NULL DEFAULT 1,
                    PRIMARY KEY (ProductTypeID, AttributeName)
                );");

            // Migration: add IsRequired column if it was created before this version
            db.Execute(@"
                IF NOT EXISTS (
                    SELECT 1 FROM sys.columns
                    WHERE object_id = OBJECT_ID('ProductTypeAttributes') AND name = 'IsRequired')
                ALTER TABLE ProductTypeAttributes
                    ADD IsRequired BIT NOT NULL DEFAULT 1;");

            db.Execute(@"
                IF NOT EXISTS (
                    SELECT 1 FROM sys.columns
                    WHERE object_id = OBJECT_ID('Products') AND name = 'ProductTypeID')
                ALTER TABLE Products
                    ADD ProductTypeID INT NULL REFERENCES ProductTypes(ProductTypeID);");

            db.Execute(@"
                IF NOT EXISTS (SELECT 1 FROM ProductTypes WHERE TypeName = 'Package')
                    INSERT INTO ProductTypes (TypeName) VALUES ('Package');
                IF NOT EXISTS (SELECT 1 FROM ProductTypes WHERE TypeName = 'Standard')
                    INSERT INTO ProductTypes (TypeName) VALUES ('Standard');");
        }

        public List<ProductType> GetAll()
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            var types = db.Query<ProductType>(
                "SELECT * FROM ProductTypes ORDER BY TypeName").ToList();
            var attrs = db.Query<(int TypeId, string Name, bool IsRequired)>(
                "SELECT ProductTypeID, AttributeName, IsRequired FROM ProductTypeAttributes").ToList();

            foreach (var t in types)
                t.AllAttributes = attrs
                    .Where(a => a.TypeId == t.ProductTypeID)
                    .Select(a => new ProductTypeAttr(a.Name, a.IsRequired))
                    .ToList();

            return types;
        }

        public ProductType? GetById(int id)
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            var type = db.QueryFirstOrDefault<ProductType>(
                "SELECT * FROM ProductTypes WHERE ProductTypeID = @id", new { id });
            if (type == null) return null;

            type.AllAttributes = db.Query<(string Name, bool IsRequired)>(
                "SELECT AttributeName, IsRequired FROM ProductTypeAttributes WHERE ProductTypeID = @id",
                new { id })
                .Select(a => new ProductTypeAttr(a.Name, a.IsRequired))
                .ToList();

            return type;
        }

        public void Add(string typeName, IEnumerable<ProductTypeAttr> attributes)
        {
            using var db = new SqlConnection(_connectionString);
            db.Open();
            using var tx = db.BeginTransaction();
            try
            {
                int id = db.QuerySingle<int>(@"
                    INSERT INTO ProductTypes (TypeName) VALUES (@typeName);
                    SELECT CAST(SCOPE_IDENTITY() AS INT);",
                    new { typeName }, tx);

                foreach (var a in attributes.Where(a => !string.IsNullOrWhiteSpace(a.AttributeName)))
                    db.Execute(@"INSERT INTO ProductTypeAttributes (ProductTypeID, AttributeName, IsRequired)
                                 VALUES (@id, @name, @req)",
                        new { id, name = a.AttributeName, req = a.IsRequired }, tx);

                tx.Commit();
            }
            catch { tx.Rollback(); throw; }
        }

        public void Update(ProductType type)
        {
            using var db = new SqlConnection(_connectionString);
            db.Open();
            using var tx = db.BeginTransaction();
            try
            {
                db.Execute("UPDATE ProductTypes SET TypeName = @TypeName WHERE ProductTypeID = @ProductTypeID",
                    type, tx);

                db.Execute("DELETE FROM ProductTypeAttributes WHERE ProductTypeID = @ProductTypeID",
                    new { type.ProductTypeID }, tx);

                foreach (var a in type.AllAttributes.Where(a => !string.IsNullOrWhiteSpace(a.AttributeName)))
                    db.Execute(@"INSERT INTO ProductTypeAttributes (ProductTypeID, AttributeName, IsRequired)
                                 VALUES (@id, @name, @req)",
                        new { id = type.ProductTypeID, name = a.AttributeName, req = a.IsRequired }, tx);

                tx.Commit();
            }
            catch { tx.Rollback(); throw; }
        }

        public void Delete(int id)
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            db.Execute("DELETE FROM ProductTypes WHERE ProductTypeID = @id", new { id });
        }
    }
}
