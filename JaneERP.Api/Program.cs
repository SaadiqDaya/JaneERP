using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;
using JaneERP.Api.Data;
using JaneERP.Api.Middleware;
using JaneERP.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;


var builder = WebApplication.CreateBuilder(args);

// ── Serilog ───────────────────────────────────────────────────────────────────
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File(
        path: Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "JaneERP", "logs", "api-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30));

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

// ── CORS ──────────────────────────────────────────────────────────────────────
// Internal mobile PWA — allow all origins but require credentials (JWT cookie/header).
// No specific domain is configured in appsettings, so SetIsOriginAllowed is used.
builder.Services.AddCors(opts => opts.AddDefaultPolicy(policy =>
    policy.SetIsOriginAllowed(_ => true)   // Internal app — allow all origins but require credentials
          .AllowAnyMethod()
          .AllowAnyHeader()
          .AllowCredentials()));

// ── Rate Limiting ─────────────────────────────────────────────────────────────
// "general"  — 60 req/min per IP, for all protected endpoints
// "auth"     — 10 req/min per IP, for the login endpoint (brute-force protection)
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Default policy applied to all endpoints not covered by a named policy
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
        RateLimitPartition.GetSlidingWindowLimiter(
            ctx.Connection.RemoteIpAddress?.ToString() ?? "anon",
            _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit          = 60,
                Window               = TimeSpan.FromMinutes(1),
                SegmentsPerWindow    = 6,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit           = 0
            }));

    // Named policy for general API endpoints: matches the global default
    options.AddPolicy("general", ctx =>
        RateLimitPartition.GetSlidingWindowLimiter(
            ctx.Connection.RemoteIpAddress?.ToString() ?? "anon",
            _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit          = 60,
                Window               = TimeSpan.FromMinutes(1),
                SegmentsPerWindow    = 6,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit           = 0
            }));

    // Named policy for auth endpoints: tighter limit to block brute-force attacks
    options.AddPolicy("auth", ctx =>
        RateLimitPartition.GetFixedWindowLimiter(
            ctx.Connection.RemoteIpAddress?.ToString() ?? "anon",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit          = 10,
                Window               = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit           = 0
            }));
});

// ── Swagger / OpenAPI ─────────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title   = "JaneERP Mobile API",
        Version = "v1",
        Description = "REST API for the JaneERP mobile PWA. All endpoints (except /api/auth/login) require a Bearer JWT."
    });

    // Enable "Authorize" button in Swagger UI
    var jwtScheme = new OpenApiSecurityScheme
    {
        Name         = "Authorization",
        In           = ParameterLocation.Header,
        Type         = SecuritySchemeType.Http,
        Scheme       = "bearer",
        BearerFormat = "JWT",
        Description  = "Paste the JWT token from /api/auth/login — no 'Bearer ' prefix needed here."
    };
    c.AddSecurityDefinition("Bearer", jwtScheme);
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
    c.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, "JaneERP.Api.xml"), true);
});

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
builder.Services.AddScoped<ApiCookingRepository>();
builder.Services.AddScoped<ApiAccountingRepository>();
builder.Services.AddScoped<ApiReportingRepository>();
builder.Services.AddScoped<ApiTaskRepository>();
#pragma warning disable CA1416 // API always runs on Windows (same machine as WinForms app)
builder.Services.AddScoped<ApiSyncRepository>();
#pragma warning restore CA1416

// Services
builder.Services.AddSingleton<JwtService>();
builder.Services.AddHttpClient<ApiShopifyClient>();

// ── App pipeline ──────────────────────────────────────────────────────────────

var app = builder.Build();

// Global exception handler — must be first so it catches errors from all subsequent middleware
app.UseGlobalExceptionHandler();

// HTTPS redirect — always redirect HTTP → HTTPS in production
app.UseHttpsRedirection();
if (!app.Environment.IsDevelopment())
    app.UseHsts();

// Correlation ID — propagate or generate X-Correlation-ID for every request
app.Use(async (context, next) =>
{
    var correlationId = context.Request.Headers["X-Correlation-ID"].FirstOrDefault()
        ?? Guid.NewGuid().ToString("N")[..8];
    context.Response.Headers["X-Correlation-ID"] = correlationId;
    using (Serilog.Context.LogContext.PushProperty("CorrelationId", correlationId))
    { await next(); }
});

// Swagger UI only in development (don't expose schema in production)
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "JaneERP Mobile API v1");
        c.RoutePrefix = "swagger";   // available at /swagger
    });
}

app.UseRateLimiter();

// Serve the PWA from wwwroot
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseAuthentication();

// Populate CompanyContext from JWT claims before controllers run
app.UseMiddleware<CompanyMiddleware>();

app.UseAuthorization();
app.UseCors();
app.MapControllers();

// SPA fallback: non-API routes serve index.html; API misses get a JSON 404
app.MapFallback(ctx =>
{
    if (ctx.Request.Path.StartsWithSegments("/api"))
    {
        ctx.Response.StatusCode  = 404;
        ctx.Response.ContentType = "application/json";
        return ctx.Response.WriteAsync(
            $"{{\"error\":\"Not found: {ctx.Request.Method} {ctx.Request.Path}\"}}");
    }
    ctx.Response.ContentType = "text/html";
    return ctx.Response.SendFileAsync(
        Path.Combine(app.Environment.WebRootPath, "index.html"));
});

// Ensure optional DB columns exist before serving any requests
ApiSchemaBootstrap.Run(app.Configuration);

app.Run();
