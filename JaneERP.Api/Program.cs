using System.Text;
using JaneERP.Api.Data;
using JaneERP.Api.Middleware;
using JaneERP.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;


var builder = WebApplication.CreateBuilder(args);

// ── Services ──────────────────────────────────────────────────────────────────

builder.Services.AddControllers();

// JWT Authentication
var jwtSecret = builder.Configuration["Jwt:Secret"]
    ?? throw new InvalidOperationException("Jwt:Secret must be configured in appsettings.json.");
var jwtIssuer   = builder.Configuration["Jwt:Issuer"]   ?? "JaneERP.Api";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "JaneERP.Mobile";

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opts =>
    {
        opts.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = jwtIssuer,
            ValidAudience            = jwtAudience,
            IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ClockSkew                = TimeSpan.Zero,
        };
    });

builder.Services.AddAuthorization();

// Scoped per request — populated by CompanyMiddleware
builder.Services.AddScoped<CompanyContext>();

// Repositories — all scoped so they get the right CompanyContext per request
builder.Services.AddScoped<ApiUserRepository>();
builder.Services.AddScoped<ApiProductRepository>();
builder.Services.AddScoped<ApiOrderRepository>();
builder.Services.AddScoped<ApiPurchaseOrderRepository>();
builder.Services.AddScoped<ApiCycleCountRepository>();
builder.Services.AddScoped<ApiLocationRepository>();
builder.Services.AddScoped<ApiCustomerRepository>();
builder.Services.AddScoped<ApiWorkOrderRepository>();

// Services
builder.Services.AddSingleton<JwtService>();

// ── App pipeline ──────────────────────────────────────────────────────────────

var app = builder.Build();

// Serve the PWA from wwwroot
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthentication();

// Populate CompanyContext from JWT claims before controllers run
app.UseMiddleware<CompanyMiddleware>();

app.UseAuthorization();
app.MapControllers();

// SPA fallback: any non-API route serves index.html (supports browser refresh)
app.MapFallbackToFile("index.html");

// Ensure optional DB columns exist before serving any requests
ApiSchemaBootstrap.Run(app.Configuration);

app.Run();
