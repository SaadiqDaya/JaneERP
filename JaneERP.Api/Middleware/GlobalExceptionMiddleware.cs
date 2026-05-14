using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Text.Json;

namespace JaneERP.Api.Middleware;

public class GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try { await next(context); }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception for {Method} {Path}", context.Request.Method, context.Request.Path);
            context.Response.ContentType = "application/problem+json";
            var (status, title) = ex switch
            {
                UnauthorizedAccessException => (HttpStatusCode.Forbidden, "Access denied"),
                ArgumentException or ArgumentNullException => (HttpStatusCode.BadRequest, "Invalid request"),
                KeyNotFoundException => (HttpStatusCode.NotFound, "Resource not found"),
                _ => (HttpStatusCode.InternalServerError, "An unexpected error occurred")
            };
            context.Response.StatusCode = (int)status;
            var problem = new ProblemDetails
            {
                Title    = title,
                Status   = (int)status,
                Detail   = ex.Message,
                Instance = context.Request.Path
            };
            await context.Response.WriteAsync(JsonSerializer.Serialize(problem,
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
        }
    }
}

public static class GlobalExceptionMiddlewareExtensions
{
    public static IApplicationBuilder UseGlobalExceptionHandler(this IApplicationBuilder app)
        => app.UseMiddleware<GlobalExceptionMiddleware>();
}
