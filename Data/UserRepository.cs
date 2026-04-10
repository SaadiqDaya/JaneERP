using System.Configuration;
using System.Data;
using Dapper;
using JaneERP.Models;
using JaneERP.Security;
using Microsoft.Data.SqlClient;

namespace JaneERP.Data
{
    public class UserRepository
    {
        private readonly string _connectionString =
            ConfigurationManager.ConnectionStrings["MyERP"]?.ConnectionString
            ?? throw new InvalidOperationException("Connection string 'MyERP' not found in App.config.");

        /// <summary>Creates the Users table if it doesn't exist yet.</summary>
        public void EnsureSchema()
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            db.Execute(@"
                IF NOT EXISTS (SELECT 1 FROM sysobjects WHERE name='Users' AND xtype='U')
                CREATE TABLE Users (
                    UserId       INT IDENTITY(1,1) PRIMARY KEY,
                    Username     NVARCHAR(100) NOT NULL UNIQUE,
                    PasswordHash NVARCHAR(256) NOT NULL,
                    PasswordSalt NVARCHAR(256) NOT NULL,
                    Role         NVARCHAR(50)  NOT NULL DEFAULT 'User',
                    IsActive     BIT           NOT NULL DEFAULT 1,
                    CreatedAt    DATETIME      NOT NULL DEFAULT GETDATE(),
                    LastLoginAt  DATETIME      NULL
                )");
        }

        public bool HasAnyUsers()
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            return db.ExecuteScalar<int>("SELECT COUNT(1) FROM Users") > 0;
        }

        public bool UsernameExists(string username)
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            return db.ExecuteScalar<int>(
                "SELECT COUNT(1) FROM Users WHERE Username = @username",
                new { username }) > 0;
        }

        /// <summary>Returns a user by username regardless of IsActive (for lockout checks).</summary>
        public AppUser? GetByUsername(string username)
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            return db.QueryFirstOrDefault<AppUser>(
                "SELECT * FROM Users WHERE Username = @username", new { username });
        }

        /// <summary>Adds Email and Permissions columns if they were created before those fields existed.</summary>
        public void MigrateUserColumns()
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            db.Execute(@"
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'Email')
                    ALTER TABLE Users ADD Email NVARCHAR(200) NOT NULL DEFAULT '';
                IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'Permissions')
                    ALTER TABLE Users ADD Permissions NVARCHAR(500) NOT NULL DEFAULT '';");
        }

        /// <summary>Adds FailedLoginCount and LockedUntil columns for DB-persisted lockout.</summary>
        public void MigrateLockout()
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            try
            {
                db.Execute(@"
                    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'FailedLoginCount')
                        ALTER TABLE Users ADD FailedLoginCount INT NOT NULL DEFAULT 0;
                    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Users') AND name = 'LockedUntil')
                        ALTER TABLE Users ADD LockedUntil DATETIME NULL;");
            }
            catch (Exception ex) { Logging.AppLogger.Info($"MigrateLockout warning: {ex.Message}"); }
        }

        private static void ValidatePassword(string password)
        {
            if (string.IsNullOrWhiteSpace(password) || password.Length < MinPasswordLength)
                throw new ArgumentException($"Password must be at least {MinPasswordLength} characters.");
        }

        public void CreateUser(string username, string password, string role = "Viewer",
                               string email = "", string permissions = "")
        {
            ValidatePassword(password);
            var (hash, salt) = PasswordHasher.Hash(password);
            using IDbConnection db = new SqlConnection(_connectionString);
            db.Execute(@"
                INSERT INTO Users (Username, PasswordHash, PasswordSalt, Role, Email, Permissions)
                VALUES (@Username, @PasswordHash, @PasswordSalt, @Role, @Email, @Permissions)",
                new { Username = username, PasswordHash = hash, PasswordSalt = salt,
                      Role = role, Email = email, Permissions = permissions });
        }

        public List<AppUser> GetAll(bool includeInactive = false)
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            var sql = includeInactive
                ? "SELECT * FROM Users ORDER BY IsActive DESC, Username"
                : "SELECT * FROM Users WHERE IsActive = 1 ORDER BY Username";
            var users = db.Query<AppUser>(sql).ToList();
            foreach (var u in users) { u.PasswordHash = ""; u.PasswordSalt = ""; }
            return users;
        }

        public void UpdateUser(AppUser user)
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            db.Execute(@"
                UPDATE Users
                SET Email = @Email, Role = @Role, Permissions = @Permissions, IsActive = @IsActive
                WHERE UserId = @UserId",
                new { user.Email, user.Role, user.Permissions, user.IsActive, user.UserId });
        }

        public void SetPassword(int userId, string newPassword)
        {
            ValidatePassword(newPassword);
            var (hash, salt) = PasswordHasher.Hash(newPassword);
            using IDbConnection db = new SqlConnection(_connectionString);
            db.Execute("UPDATE Users SET PasswordHash = @hash, PasswordSalt = @salt WHERE UserId = @userId",
                new { hash, salt, userId });
        }

        public const int MaxLoginAttempts  = 5;
        public const int LockoutMinutes   = 15;
        public const int MinPasswordLength = 8;

        /// <summary>
        /// Returns the authenticated user, or null if credentials are invalid or account is locked.
        /// Tracks failed attempts in the DB; locks account for 15 minutes after 5 failures.
        /// </summary>
        public AppUser? Authenticate(string username, string password)
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            var user = db.QueryFirstOrDefault<AppUser>(
                "SELECT * FROM Users WHERE Username = @username AND IsActive = 1",
                new { username });

            if (user == null) return null;

            // Check lockout (column may not exist yet on older DBs — treat as unlocked)
            try
            {
                if (user.LockedUntil.HasValue && user.LockedUntil.Value > DateTime.Now)
                    return null; // still locked — caller sees this as bad credentials
            }
            catch { /* LockedUntil not yet migrated */ }

            if (!PasswordHasher.Verify(password, user.PasswordHash, user.PasswordSalt))
            {
                // Increment failed count; lock if threshold reached
                try
                {
                    var newCount = (user.FailedLoginCount) + 1;
                    DateTime? lockUntil = newCount >= MaxLoginAttempts
                        ? DateTime.Now.AddMinutes(LockoutMinutes)
                        : (DateTime?)null;
                    db.Execute(
                        "UPDATE Users SET FailedLoginCount = @c, LockedUntil = @lu WHERE UserId = @id",
                        new { c = newCount, lu = lockUntil, id = user.UserId });
                }
                catch { /* column not yet migrated — ignore */ }
                return null;
            }

            // Success — reset failed count and update last login
            try
            {
                db.Execute(
                    "UPDATE Users SET LastLoginAt = @now, FailedLoginCount = 0, LockedUntil = NULL WHERE UserId = @id",
                    new { now = DateTime.Now, id = user.UserId });
            }
            catch
            {
                db.Execute(
                    "UPDATE Users SET LastLoginAt = @now WHERE UserId = @id",
                    new { now = DateTime.Now, id = user.UserId });
            }

            user.LastLoginAt  = DateTime.Now;
            user.PasswordHash = "";
            user.PasswordSalt = "";
            return user;
        }

        /// <summary>Unlocks a user account and resets their failed login counter.</summary>
        public void UnlockUser(int userId)
        {
            using IDbConnection db = new SqlConnection(_connectionString);
            try
            {
                db.Execute(
                    "UPDATE Users SET FailedLoginCount = 0, LockedUntil = NULL WHERE UserId = @id",
                    new { id = userId });
            }
            catch { /* column not yet migrated */ }
        }
    }
}
