using System.Data;
using Dapper;
using JaneERP.Api.Services;
using Microsoft.Data.SqlClient;

namespace JaneERP.Api.Data;

public class ApiUserRepository
{
    private readonly CompanyContext              _ctx;
    private readonly ILogger<ApiUserRepository> _logger;

    public ApiUserRepository(CompanyContext ctx, ILogger<ApiUserRepository> logger)
    {
        _ctx    = ctx;
        _logger = logger;
    }

    private IDbConnection Connect() => new SqlConnection(_ctx.ConnectionString);

    /// <summary>
    /// Authenticates a user against the company database using the same PBKDF2 algorithm
    /// as the WinForms app. Returns the user record on success, null on failure/lockout.
    /// </summary>
    public UserRecord? Authenticate(string username, string password)
    {
        using var db = Connect();
        var user = db.QueryFirstOrDefault<UserRecord>(
            "SELECT * FROM Users WHERE Username = @username AND IsActive = 1",
            new { username });

        if (user == null) return null;

        // Check lockout
        if (user.LockedUntil.HasValue && user.LockedUntil.Value > DateTime.Now)
            return null;

        if (!PasswordHasher.Verify(password, user.PasswordHash, user.PasswordSalt))
        {
            // Track failed attempts
            try
            {
                var newCount  = user.FailedLoginCount + 1;
                var lockUntil = newCount >= 5 ? (DateTime?)DateTime.Now.AddMinutes(15) : null;
                db.Execute(
                    "UPDATE Users SET FailedLoginCount = @c, LockedUntil = @lu WHERE UserId = @id",
                    new { c = newCount, lu = lockUntil, id = user.UserId });
            }
            catch (Exception ex) { _logger.LogDebug(ex, "[ApiUserRepository.Authenticate] FailedLoginCount update skipped (column may not exist on older DBs)"); }
            return null;
        }

        // Success — reset lockout state
        try
        {
            db.Execute(
                "UPDATE Users SET LastLoginAt = @now, FailedLoginCount = 0, LockedUntil = NULL WHERE UserId = @id",
                new { now = DateTime.Now, id = user.UserId });
        }
        catch
        {
            db.Execute("UPDATE Users SET LastLoginAt = @now WHERE UserId = @id",
                new { now = DateTime.Now, id = user.UserId });
        }

        user.PasswordHash = "";
        user.PasswordSalt = "";
        return user;
    }
}

public class UserRecord
{
    public int       UserId           { get; set; }
    public string    Username         { get; set; } = "";
    public string    PasswordHash     { get; set; } = "";
    public string    PasswordSalt     { get; set; } = "";
    public string    Role             { get; set; } = "Viewer";
    public bool      IsActive         { get; set; }
    public string?   Email            { get; set; }
    public int       FailedLoginCount { get; set; }
    public DateTime? LockedUntil      { get; set; }
    public DateTime? LastLoginAt      { get; set; }
}
