using System.Configuration;
using System.Data;
using Dapper;
using JaneERP.Interfaces;
using JaneERP.Models;
using Microsoft.Data.SqlClient;

namespace JaneERP.Data
{
    public class ProductTypeRepository : IProductTypeRepository
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

            db.Execute(@"
                IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='AttributeDefinitions' AND xtype='U')
                CREATE TABLE AttributeDefinitions (
                    AttributeDefID INT IDENTITY(1,1) PRIMARY KEY,
                    AttributeName  NVARCHAR(100) NOT NULL UNIQUE,
                    AllowedValues  NVARCHAR(MAX) NULL
                );");

            // ── Migrations: add Category / DataType / Unit if missing ─────────────
            db.Execute(@"
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AttributeDefinitions') AND name = 'Category')
                    ALTER TABLE AttributeDefinitions ADD Category NVARCHAR(20) NOT NULL DEFAULT 'General';
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AttributeDefinitions') AND name = 'DataType')
                    ALTER TABLE AttributeDefinitions ADD DataType NVARCHAR(20) NOT NULL DEFAULT 'Text';
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('AttributeDefinitions') AND name = 'Unit')
                    ALTER TABLE AttributeDefinitions ADD Unit NVARCHAR(20) NULL;");

            // ── Seed standard manufacturing & marketing attributes ────────────────
            SeedAttributeDefinitions(db);
        }

        private static void SeedAttributeDefinitions(IDbConnection db)
        {
            var seeds = new[]
            {
                // Manufacturing — used for batch calculations
                ("SizeML",           "Manufacturing", "Number", "ml",     (string?)null),
                ("VGPercent",        "Manufacturing", "Number", "%",      (string?)null),
                ("NicStrengthMgMl",  "Manufacturing", "Number", "mg/ml",  (string?)null),
                ("NicType",          "Manufacturing", "List",   (string?)null, "FreeBase,Salt"),
                // Marketing — display and labelling
                ("Size",             "Marketing",     "List",   (string?)null, "10ml,30ml,60ml,120ml,250ml,500ml,1L"),
                ("Brand",            "Marketing",     "Text",   (string?)null, (string?)null),
                ("Nicotine",         "Marketing",     "List",   (string?)null, "0mg,3mg,6mg,12mg,18mg,50mg"),
                ("VG",               "Marketing",     "Text",   (string?)null, "50VG,60VG,70VG"),
                ("BottleType",       "Marketing",     "List",   (string?)null, "10ml Plastic,30ml Plastic,30ml Glass,60ml Plastic,120ml Plastic"),
                ("Version",          "Marketing",     "Text",   (string?)null, (string?)null),
                ("Concentrate",      "Marketing",     "Text",   (string?)null, (string?)null),
                ("Note",             "Marketing",     "Text",   (string?)null, (string?)null),
            };
            foreach (var (name, cat, dtype, unit, vals) in seeds)
            {
                db.Execute(@"
                    IF NOT EXISTS (SELECT 1 FROM AttributeDefinitions WHERE AttributeName = @name)
                        INSERT INTO AttributeDefinitions (AttributeName, Category, DataType, Unit, AllowedValues)
                        VALUES (@name, @cat, @dtype, @unit, @vals)",
                    new { name, cat, dtype, unit, vals });
            }
        }

        // ── Attribute Definitions ─────────────────────────────────────────────────

        public List<AttributeDefinition> GetAttributeDefinitions()
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            return db.Query(@"
                SELECT AttributeDefID, AttributeName, Category, DataType, Unit, AllowedValues
                FROM   AttributeDefinitions
                ORDER  BY Category, AttributeName")
                .Select(r => new AttributeDefinition
                {
                    Id            = (int)r.AttributeDefID,
                    Name          = (string)r.AttributeName,
                    Category      = (string)(r.Category ?? "General"),
                    DataType      = (string)(r.DataType  ?? "Text"),
                    Unit          = (string?)r.Unit,
                    AllowedValues = (string?)r.AllowedValues
                })
                .ToList();
        }

        public void UpsertAttributeDefinition(string name, string? allowedValues,
            string category = "General", string dataType = "Text", string? unit = null)
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            db.Execute(@"
                IF EXISTS (SELECT 1 FROM AttributeDefinitions WHERE AttributeName = @name)
                    UPDATE AttributeDefinitions
                    SET    AllowedValues = @allowedValues,
                           Category     = @category,
                           DataType     = @dataType,
                           Unit         = @unit
                    WHERE  AttributeName = @name
                ELSE
                    INSERT INTO AttributeDefinitions (AttributeName, AllowedValues, Category, DataType, Unit)
                    VALUES (@name, @allowedValues, @category, @dataType, @unit)",
                new { name, allowedValues, category, dataType, unit });
        }

        public void DeleteAttributeDefinition(int id)
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            db.Execute("DELETE FROM AttributeDefinitions WHERE AttributeDefID = @id", new { id });
        }

        public string[] GetAllowedValues(string attributeName)
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            var csv = db.QueryFirstOrDefault<string>(
                "SELECT AllowedValues FROM AttributeDefinitions WHERE AttributeName = @attributeName",
                new { attributeName });
            if (string.IsNullOrWhiteSpace(csv)) return [];
            return csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
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

        public List<string> GetAttributeNamesForType(int typeId)
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            return db.Query<string>(
                "SELECT AttributeName FROM ProductTypeAttributes WHERE ProductTypeID = @typeId ORDER BY AttributeName",
                new { typeId }).ToList();
        }
    }
}
