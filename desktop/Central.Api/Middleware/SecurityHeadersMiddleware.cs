namespace Central.Api.Middleware;

/// <summary>
/// Adds security headers to all API responses.
/// OWASP recommended headers for production APIs.
/// </summary>
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        // Prevent clickjacking
        context.Response.Headers["X-Frame-Options"] = "DENY";

        // Prevent MIME type sniffing
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";

        // XSS protection (legacy browsers)
        context.Response.Headers["X-XSS-Protection"] = "1; mode=block";

        // Referrer policy
        context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

        // Content Security Policy (API returns JSON, not HTML)
        context.Response.Headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";

        // Permissions policy
        context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";

        // Cache control for API responses (no caching by default)
        if (!context.Response.Headers.ContainsKey("Cache-Control"))
            context.Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate";

        await _next(context);
    }
}

public static class SecurityHeadersExtensions
{
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app)
        => app.UseMiddleware<SecurityHeadersMiddleware>();
}
