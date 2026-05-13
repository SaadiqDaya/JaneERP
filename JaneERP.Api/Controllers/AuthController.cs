using JaneERP.Api.Data;
using JaneERP.Api.Middleware;
using JaneERP.Api.Models;
using JaneERP.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace JaneERP.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly JwtService     _jwt;

    public AuthController(IConfiguration config, JwtService jwt)
    {
        _config = config;
        _jwt    = jwt;
    }

    /// <summary>Returns company names for the login screen dropdown. No auth required.</summary>
    [HttpGet("/api/companies")]
    public IActionResult GetCompanies()
    {
        var companies = _config.GetSection("Companies").Get<List<CompanyConfig>>();
        var names = companies?.Select(c => c.Name).ToList() ?? [];
        return Ok(names);
    }

    /// <summary>Authenticates against the selected company's database and returns a JWT.</summary>
    [HttpPost("login")]
    public IActionResult Login([FromBody] LoginRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Company) ||
            string.IsNullOrWhiteSpace(req.Username) ||
            string.IsNullOrWhiteSpace(req.Password))
            return BadRequest(new { error = "Company, username and password are required." });

        // Look up company connection string
        var companies = _config.GetSection("Companies").Get<List<CompanyConfig>>();
        var company   = companies?.FirstOrDefault(c =>
            string.Equals(c.Name, req.Company, StringComparison.OrdinalIgnoreCase));

        if (company == null)
            return BadRequest(new { error = "Company not found." });

        // Authenticate against that company's database
        var ctx = new CompanyContext { ConnectionString = company.ConnectionString };
        var repo = new ApiUserRepository(ctx);
        var user = repo.Authenticate(req.Username, req.Password);

        if (user == null)
            return Unauthorized(new { error = "Invalid credentials or account locked." });

        var expiryHours = int.TryParse(_config["Jwt:ExpiryHours"], out var h) ? h : 24;
        var token       = _jwt.GenerateToken(user.Username, user.Role, req.Company);

        return Ok(new LoginResponse(token, user.Username, user.Role, req.Company, expiryHours));
    }
}
