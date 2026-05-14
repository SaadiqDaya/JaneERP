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
                END

                IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='UomConversions' AND xtype='U')
                BEGIN
                    CREATE TABLE UomConversions (
                        ConversionID INT           IDENTITY(1,1) PRIMARY KEY,
                        FromUOMID    INT           NOT NULL REFERENCES UnitOfMeasures(UOMID) ON DELETE NO ACTION,
                        ToUOMID      INT           NOT NULL REFERENCES UnitOfMeasures(UOMID) ON DELETE NO ACTION,
                        Multiplier   DECIMAL(18,6) NOT NULL DEFAULT 1,
                        CONSTRAINT UQ_UomConversions_Pair UNIQUE (FromUOMID, ToUOMID)
                    );

                    -- Seed with common conversions derived from the seeded units above
                    INSERT INTO UomConversions (FromUOMID, ToUOMID, Multiplier)
                    SELECT f.UOMID, t.UOMID,
                           CASE WHEN f.BaseUnit = t.Abbreviation THEN f.ConversionFactor
                                WHEN t.BaseUnit = f.Abbreviation THEN 1.0 / t.ConversionFactor
                                ELSE f.ConversionFactor / t.ConversionFactor END
                    FROM UnitOfMeasures f
                    JOIN UnitOfMeasures t ON f.UOMID <> t.UOMID
                                        AND ISNULL(f.BaseUnit, f.Abbreviation) = ISNULL(t.BaseUnit, t.Abbreviation)
                    WHERE f.IsActive = 1 AND t.IsActive = 1;
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

        public bool TryConvert(string fromAbbr, string toAbbr, decimal quantity, out decimal result)
        {
            result = 0;
            if (string.IsNullOrWhiteSpace(fromAbbr) || string.IsNullOrWhiteSpace(toAbbr)) return false;
            if (fromAbbr.Equals(toAbbr, StringComparison.OrdinalIgnoreCase)) { result = quantity; return true; }

            try
            {
                // First try explicit pairwise conversions table
                using IDbConnection db = new SqlConnection(_connectionString);
                var explict = db.QueryFirstOrDefault<decimal?>(@"
                    SELECT c.Multiplier
                    FROM   UomConversions c
                    JOIN   UnitOfMeasures f ON f.UOMID = c.FromUOMID
                    JOIN   UnitOfMeasures t ON t.UOMID = c.ToUOMID
                    WHERE  f.Abbreviation = @fromAbbr AND t.Abbreviation = @toAbbr",
                    new { fromAbbr, toAbbr });
                if (explict.HasValue) { result = quantity * explict.Value; return true; }

                // Fall back to base-unit conversion factor arithmetic
                var all  = GetAll(includeInactive: false);
                var from = all.FirstOrDefault(u => u.Abbreviation.Equals(fromAbbr, StringComparison.OrdinalIgnoreCase));
                var to   = all.FirstOrDefault(u => u.Abbreviation.Equals(toAbbr,   StringComparison.OrdinalIgnoreCase));
                if (from == null || to == null) return false;

                // Both must share the same base unit (or one must be the base unit of the other)
                string fromBase = from.BaseUnit ?? from.Abbreviation;
                string toBase   = to.BaseUnit   ?? to.Abbreviation;
                if (!fromBase.Equals(toBase, StringComparison.OrdinalIgnoreCase)) return false;

                // Convert: quantity in fromAbbr → base unit → toAbbr
                decimal inBase = quantity * from.ConversionFactor;
                result = inBase / to.ConversionFactor;
                return true;
            }
            catch { return false; }
        }

        // ── Pairwise conversions ──────────────────────────────────────────────────

        public List<UomConversion> GetConversions()
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            try
            {
                return db.Query<UomConversion>(@"
                    SELECT c.ConversionID, c.FromUOMID, c.ToUOMID, c.Multiplier,
                           f.Abbreviation AS FromAbbr,
                           t.Abbreviation AS ToAbbr
                    FROM   UomConversions c
                    JOIN   UnitOfMeasures f ON f.UOMID = c.FromUOMID
                    JOIN   UnitOfMeasures t ON t.UOMID = c.ToUOMID
                    ORDER  BY f.DisplayOrder, f.Abbreviation, t.Abbreviation").ToList();
            }
            catch { return new List<UomConversion>(); }
        }

        public void AddConversion(int fromUomId, int toUomId, decimal multiplier)
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            db.Execute(@"
                INSERT INTO UomConversions (FromUOMID, ToUOMID, Multiplier)
                VALUES (@fromUomId, @toUomId, @multiplier)",
                new { fromUomId, toUomId, multiplier });
        }

        public void DeleteConversion(int conversionId)
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            db.Execute("DELETE FROM UomConversions WHERE ConversionID = @conversionId",
                new { conversionId });
        }
    }
}
