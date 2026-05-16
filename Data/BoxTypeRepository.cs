using System.Configuration;
using System.Data;
using Dapper;
using JaneERP.Interfaces;
using JaneERP.Models;
using Microsoft.Data.SqlClient;

namespace JaneERP.Data
{
    public class BoxTypeRepository : IBoxTypeRepository
    {
        private readonly string _connectionString =
            ConfigurationManager.ConnectionStrings["MyERP"]?.ConnectionString
            ?? throw new InvalidOperationException("Connection string 'MyERP' not found in App.config.");

        public IReadOnlyList<BoxType> GetBoxTypes(bool activeOnly = false)
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            const string sql = @"
                SELECT BoxTypeID, BoxName, Notes, IsActive
                FROM   BoxTypes
                WHERE  (@activeOnly = 0 OR IsActive = 1)
                ORDER  BY BoxName";
            return db.Query<BoxType>(sql, new { activeOnly = activeOnly ? 1 : 0 }).ToList();
        }

        public BoxType SaveBoxType(BoxType bt)
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            if (bt.BoxTypeID == 0)
            {
                const string insert = @"
                    INSERT INTO BoxTypes (BoxName, Notes, IsActive)
                    OUTPUT INSERTED.BoxTypeID
                    VALUES (@BoxName, @Notes, @IsActive)";
                bt.BoxTypeID = db.ExecuteScalar<int>(insert, bt);
            }
            else
            {
                const string update = @"
                    UPDATE BoxTypes
                    SET    BoxName  = @BoxName,
                           Notes    = @Notes,
                           IsActive = @IsActive
                    WHERE  BoxTypeID = @BoxTypeID";
                db.Execute(update, bt);
            }
            return bt;
        }

        public void DeleteBoxType(int boxTypeId)
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            db.Execute(
                "UPDATE BoxTypes SET IsActive = 0 WHERE BoxTypeID = @id",
                new { id = boxTypeId });
        }
    }
}
