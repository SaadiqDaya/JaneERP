namespace JaneERP.Api.Services;

/// <summary>
/// Scoped per HTTP request. Populated by CompanyMiddleware after JWT validation.
/// Repositories use this to get the correct connection string for the current user's company.
/// </summary>
public class CompanyContext
{
    public string ConnectionString { get; set; } = string.Empty;
    public string CompanyName      { get; set; } = string.Empty;
    public string Username         { get; set; } = string.Empty;
    public string Role             { get; set; } = string.Empty;
}
