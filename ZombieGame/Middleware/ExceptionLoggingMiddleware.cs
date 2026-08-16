namespace ZombieGame.Api.Middleware;

using System.Net;
using System.Text.Json;
using ZombieGame.Application.Common;

/// <summary>
/// Catches unhandled exceptions, logs them (so the daily file logger receives them), and returns JSON.
/// </summary>
public sealed class ExceptionLoggingMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionLoggingMiddleware> _logger;

    public ExceptionLoggingMiddleware(RequestDelegate next, ILogger<ExceptionLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (ServiceException ex)
        {
            _logger.LogWarning(ex, "Service error on {Method} {Path}", context.Request.Method, context.Request.Path);
            await WriteJsonAsync(context, HttpStatusCode.BadRequest, ex.Message, ex.Code);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception on {Method} {Path}", context.Request.Method, context.Request.Path);
            await WriteJsonAsync(
                context,
                HttpStatusCode.InternalServerError,
                "An unexpected error occurred.",
                "server_error");
        }
    }

    private static Task WriteJsonAsync(HttpContext context, HttpStatusCode status, string message, string? code)
    {
        if (context.Response.HasStarted)
            return Task.CompletedTask;

        context.Response.Clear();
        context.Response.StatusCode = (int)status;
        context.Response.ContentType = "application/json; charset=utf-8";
        var body = JsonSerializer.Serialize(new { message, code }, JsonOptions);
        return context.Response.WriteAsync(body);
    }
}
