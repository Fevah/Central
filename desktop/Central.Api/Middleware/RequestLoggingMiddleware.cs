using System.Diagnostics;

namespace Central.Api.Middleware;

/// <summary>
/// Logs all API requests with method, path, status code, duration.
/// Structured logging for monitoring and debugging.
/// </summary>
public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var sw = Stopwatch.StartNew();
        var method = context.Request.Method;
        var path = context.Request.Path.Value;

        try
        {
            await _next(context);
        }
        finally
        {
            var statusCode = context.Response.StatusCode;
            var elapsed = sw.ElapsedMilliseconds;
            var user = context.User.Identity?.Name ?? "anonymous";

            if (elapsed > 1000)
                _logger.LogWarning("{Method} {Path} → {Status} ({Elapsed}ms) [SLOW] user={User}", method, path, statusCode, elapsed, user);
            else if (statusCode >= 400)
                _logger.LogWarning("{Method} {Path} → {Status} ({Elapsed}ms) user={User}", method, path, statusCode, elapsed, user);
            else
                _logger.LogInformation("{Method} {Path} → {Status} ({Elapsed}ms) user={User}", method, path, statusCode, elapsed, user);
        }
    }
}

public static class RequestLoggingExtensions
{
    public static IApplicationBuilder UseRequestLogging(this IApplicationBuilder app)
        => app.UseMiddleware<RequestLoggingMiddleware>();
}
