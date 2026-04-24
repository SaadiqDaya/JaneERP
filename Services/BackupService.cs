using System.Configuration;
using Dapper;
using Microsoft.Data.SqlClient;

namespace JaneERP.Services
{
    /// <summary>
    /// Handles SQL Server and SQLite database backups.
    /// SQL Server is backed up using the built-in BACKUP DATABASE T-SQL command.
    /// SQLite (app.db) is backed up with a simple file copy.
    /// </summary>
    public static class BackupService
    {
        private static string ConnStr =>
            ConfigurationManager.ConnectionStrings["MyERP"]?.ConnectionString
            ?? throw new InvalidOperationException("Connection string 'MyERP' not found in App.config.");

        private static string SqliteDbPath =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                         "JaneERP", "app.db");

        /// <summary>
        /// Returns true when an automatic backup is due based on BackupSchedule and LastBackupAt.
        /// </summary>
        public static bool IsBackupDue()
        {
            var s = AppSettings.Current;
            if (string.IsNullOrEmpty(s.BackupFolder) || s.BackupSchedule == "None") return false;

            if (!s.LastBackupAt.HasValue) return true; // never backed up

            var since = DateTime.UtcNow - s.LastBackupAt.Value;
            return s.BackupSchedule == "Daily"  ? since.TotalHours  >= 24
                 : s.BackupSchedule == "Weekly" ? since.TotalDays   >= 7
                 : false;
        }

        /// <summary>
        /// Creates a timestamped backup of both databases in <paramref name="backupFolder"/>.
        /// Throws on failure — the caller should catch and display the error.
        /// </summary>
        public static void Backup(string backupFolder)
        {
            Directory.CreateDirectory(backupFolder);
            var stamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");

            // ── SQL Server backup ──────────────────────────────────────────────────
            var sqlBackupPath = Path.Combine(backupFolder, $"JaneERP_SQL_{stamp}.bak");
            BackupSqlServer(sqlBackupPath);

            // ── SQLite backup (file copy) ──────────────────────────────────────────
            if (File.Exists(SqliteDbPath))
            {
                var sqliteBackupPath = Path.Combine(backupFolder, $"JaneERP_SQLite_{stamp}.db");
                File.Copy(SqliteDbPath, sqliteBackupPath, overwrite: false);
            }

            // ── Record success ─────────────────────────────────────────────────────
            var settings = AppSettings.Current;
            settings.LastBackupAt = DateTime.UtcNow;
            settings.Save();

            Logging.AppLogger.Audit("system", "Backup",
                $"SQL={sqlBackupPath}");
        }

        /// <summary>
        /// Runs BACKUP DATABASE on the connected SQL Server database.
        /// The backup file path must be accessible by the SQL Server service account.
        /// </summary>
        private static void BackupSqlServer(string backupFilePath)
        {
            // Extract database name from connection string
            var builder = new SqlConnectionStringBuilder(ConnStr);
            var dbName  = builder.InitialCatalog;
            if (string.IsNullOrWhiteSpace(dbName))
                throw new InvalidOperationException("Could not determine database name from connection string.");

            using var db = new SqlConnection(ConnStr);
            db.Open();
            // BACKUP DATABASE writes the file on the SQL Server host, which is localhost\SQLEXPRESS
            // so the path is local — this works for the standard local install.
            db.Execute(
                $"BACKUP DATABASE [{dbName}] TO DISK = @path WITH FORMAT, INIT, COMPRESSION",
                new { path = backupFilePath },
                commandTimeout: 120);
        }
    }
}
