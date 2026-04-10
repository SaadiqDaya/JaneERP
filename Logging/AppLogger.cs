using System;
using System.IO;
using System.Threading;
using JaneERP.Data;

namespace JaneERP.Logging
{
    public static class AppLogger
    {
        private static readonly object _sync = new object();
        private static string LogFolder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "JaneERP", "logs");
        private static string LogFile => Path.Combine(LogFolder, "app.log");

        public static void Init()
        {
            try
            {
                Directory.CreateDirectory(LogFolder);
                // write start line
                File.AppendAllText(LogFile, $"{DateTime.UtcNow:O} [Info] App starting{Environment.NewLine}");
            }
            catch
            {
                // swallow - logging is best-effort
            }
        }

        public static void Shutdown()
        {
            try
            {
                File.AppendAllText(LogFile, $"{DateTime.UtcNow:O} [Info] App shutting down{Environment.NewLine}");
            }
            catch { }
        }

        public static void Info(string message)
        {
            Write("Info", message);
        }

        public static void Error(string message)
        {
            Write("Error", message);
        }

        private static void Write(string level, string message)
        {
            try
            {
                lock (_sync)
                {
                    File.AppendAllText(LogFile, $"{DateTime.UtcNow:O} [{level}] {message}{Environment.NewLine}");
                }
            }
            catch { /* best-effort */ }
        }

        // audit: logs to file and attempts to write an AuditLog entry into the DB (best-effort)
        public static void Audit(string? user, string action, string? details)
        {
            var u = string.IsNullOrEmpty(user) ? "unknown" : user;
            var d = details ?? "";
            Write("Audit", $"{u} {action} {d}");

            // try to write to DB AuditLogs table if AppDbContext exists and DB accessible
            try
            {
                using var db = new AppDbContext();
                db.AuditLogs.Add(new AuditLog
                {
                    When = DateTime.UtcNow,
                    User = u,
                    Action = action,
                    Details = d
                });
                db.SaveChanges();
            }
            catch (Exception ex)
            {
                // if DB write fails, log the DB error to file but do not throw
                Write("AuditError", ex.ToString());
            }
        }
    }
}
