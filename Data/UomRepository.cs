using System.Configuration;
using System.Data;
using Dapper;
using JaneERP.Interfaces;
using JaneERP.Models;
using Microsoft.Data.SqlClient;

namespace JaneERP.Data
{
    public class UomRepository : IUomRepository
    {
        private readonly string _connectionString =
            ConfigurationManager.ConnectionStrings["MyERP"]?.ConnectionString
            ?? throw new InvalidOperationException("Connection string 'MyERP' not found in App.config.");

        public void EnsureSchema()
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            db.Execute(@"
                IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='UnitOfMeasures' AND xtype='U')
                BEGIN
                    CREATE TABLE UnitOfMeasures (
                        UOMID            INT IDENTITY(1,1) PRIMARY KEY,
                        Name             NVARCHAR(50)   NOT NULL,
                        Abbreviation     NVARCHAR(20)   NOT NULL,
                        BaseUnit         NVARCHAR(20)   NULL,
                        ConversionFactor DECIMAL(18,6)  NOT NULL DEFAULT 1,
                        DisplayOrder     INT            NOT NULL DEFAULT 0,
                        IsActive         BIT            NOT NULL DEFAULT 1,
                        CONSTRAINT UQ_UOM_Abbreviation UNIQUE (Abbreviation)
                    );

                    -- Seed with common units
                    INSERT INTO UnitOfMeasures (Name, Abbreviation, BaseUnit, ConversionFactor, DisplayOrder) VALUES
                        ('Each',       'ea',  'ea',  1,       0),
                        ('Pieces',     'pcs', 'ea',  1,       1),
                        ('Gram',       'g',   'g',   1,       10),
                        ('Kilogram',   'kg',  'g',   1000,    11),
                        ('Milligram',  'mg',  'g',   0.001,   12),
                        ('Millilitre', 'mL',  'mL',  1,       20),
                        ('Litre',      'L',   'mL',  1000,    21),
                        ('Ounce',      'oz',  'g',   28.3495, 30),
                        ('Pound',      'lb',  'g',   453.592, 31),
                        ('Fluid Ounce','fl oz','mL', 29.5735, 32);
                END");
        }

        public List<UnitOfMeasure> GetAll(bool includeInactive = false)
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            string filter = includeInactive ? "1=1" : "IsActive = 1";
            return db.Query<UnitOfMeasure>(
                $"SELECT * FROM UnitOfMeasures WHERE {filter} ORDER BY DisplayOrder, Name").ToList();
        }

        public void Add(UnitOfMeasure uom)
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            db.Execute(@"
                INSERT INTO UnitOfMeasures (Name, Abbreviation, BaseUnit, ConversionFactor, DisplayOrder, IsActive)
                VALUES (@Name, @Abbreviation, @BaseUnit, @ConversionFactor, @DisplayOrder, @IsActive)",
                uom);
        }

        public void Update(UnitOfMeasure uom)
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            db.Execute(@"
                UPDATE UnitOfMeasures
                SET Name             = @Name,
                    Abbreviation     = @Abbreviation,
                    BaseUnit         = @BaseUnit,
                    ConversionFactor = @ConversionFactor,
                    DisplayOrder     = @DisplayOrder,
                    IsActive         = @IsActive
                WHERE UOMID = @UOMID",
                uom);
        }

        public void Delete(int id)
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            db.Execute("UPDATE UnitOfMeasures SET IsActive = 0 WHERE UOMID = @id", new { id });
        }

        public List<string> GetAbbreviations()
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            try
            {
                return db.Query<string>(
                    "SELECT Abbreviation FROM UnitOfMeasures WHERE IsActive = 1 ORDER BY DisplayOrder, Abbreviation")
                    .ToList();
            }
            catch (Exception ex) { Logging.AppLogger.Error($"[UomRepository.GetAbbreviations] {ex}"); return new List<string>(); }
        }
    }
}
