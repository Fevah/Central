using Central.Observability;

namespace Central.Api.Middleware;

/// <summary>
/// Propagates or generates a correlation ID for distributed tracing.
/// Reads X-Correlation-ID from the request, or generates a new one.
/// Sets the AsyncLocal CorrelationContext and echoes the ID in the response.
/// </summary>
public class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = context.Request.Headers["X-Correlation-ID"].FirstOrDefault()
                         ?? Guid.NewGuid().ToString("N");

        using var scope = CorrelationContext.BeginScope(correlationId);
        context.Response.Headers["X-Correlation-ID"] = correlationId;
        await _next(context);
    }
}

public static class CorrelationIdExtensions
{
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app)
        => app.UseMiddleware<CorrelationIdMiddleware>();
}
