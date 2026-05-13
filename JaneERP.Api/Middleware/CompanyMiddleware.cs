using JaneERP.Api.Services;

namespace JaneERP.Api.Middleware;

/// <summary>
/// Runs after JWT authentication. Reads the "company" claim from the token,
/// looks up the connection string in appsettings.json, and populates CompanyContext
/// so repositories can use it for this request.
/// </summary>
public class CompanyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IConfiguration  _config;

    public CompanyMiddleware(RequestDelegate next, IConfiguration config)
    {
        _next   = next;
        _config = config;
    }

    public async Task InvokeAsync(HttpContext ctx, CompanyContext companyCtx)
    {
        var user = ctx.User;
        if (user.Identity?.IsAuthenticated == true)
        {
            var companyName = user.FindFirst("company")?.Value;
            var username    = user.FindFirst("username")?.Value ?? user.Identity.Name ?? "";
            var role        = user.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? "Viewer";

            companyCtx.Username    = username;
            companyCtx.Role        = role;
            companyCtx.CompanyName = companyName ?? "";

            if (!string.IsNullOrEmpty(companyName))
            {
                var companies = _config.GetSection("Companies").Get<List<CompanyConfig>>();
                var match = companies?.FirstOrDefault(c =>
                    string.Equals(c.Name, companyName, StringComparison.OrdinalIgnoreCase));

                if (match != null)
                    companyCtx.ConnectionString = match.ConnectionString;
            }
        }

        await _next(ctx);
    }
}

public class CompanyConfig
{
    public string Name             { get; set; } = "";
    public string ConnectionString { get; set; } = "";
}
