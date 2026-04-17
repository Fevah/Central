using System.Collections.Concurrent;

namespace Central.Api.Middleware;

/// <summary>
/// Sliding-window rate limiter per user (or per IP for anonymous requests).
/// Returns 429 Too Many Requests when exceeded.
/// Includes RFC 6585 / RateLimit draft headers.
/// </summary>
public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly int _maxRequests;
    private readonly int _windowSeconds;
    private readonly ConcurrentDictionary<string, SlidingWindow> _windows = new();

    public RateLimitingMiddleware(RequestDelegate next, int maxRequests = 200, int windowSeconds = 60)
    {
        _next = next;
        _maxRequests = maxRequests;
        _windowSeconds = windowSeconds;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip rate limiting for health checks, swagger, and SignalR hubs
        var path = context.Request.Path.Value ?? "";
        if (path.StartsWith("/api/health") || path.StartsWith("/swagger") || path.StartsWith("/hubs/"))
        {
            await _next(context);
            return;
        }

        // Identify by authenticated user if available, otherwise by IP
        var identity = context.User?.Identity?.Name;
        var clientKey = !string.IsNullOrEmpty(identity)
            ? $"user:{identity}"
            : $"ip:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";

        // Stricter limits for expensive endpoints
        var effectiveMax = _maxRequests;
        if (path.StartsWith("/api/ssh")) effectiveMax = 10;       // SSH ops are expensive
        else if (path.StartsWith("/api/auth/login")) effectiveMax = 20; // Brute-force protection

        var window = _windows.GetOrAdd(clientKey, _ => new SlidingWindow());
        var (allowed, count) = window.TryAcquire(_windowSeconds, effectiveMax);

        var remaining = Math.Max(0, effectiveMax - count);
        var resetSeconds = _windowSeconds;

        // RFC 6585 + RateLimit draft headers (always included)
        context.Response.Headers["RateLimit-Limit"] = effectiveMax.ToString();
        context.Response.Headers["RateLimit-Remaining"] = remaining.ToString();
        context.Response.Headers["RateLimit-Reset"] = resetSeconds.ToString();

        if (!allowed)
        {
            context.Response.StatusCode = 429;
            context.Response.Headers["Retry-After"] = resetSeconds.ToString();
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsJsonAsync(new
            {
                type = "https://tools.ietf.org/html/rfc6585#section-4",
                title = "Too Many Requests",
                status = 429,
                detail = $"Rate limit of {effectiveMax} requests per {_windowSeconds}s exceeded. Retry after {resetSeconds}s.",
                retry_after = resetSeconds
            });
            return;
        }

        await _next(context);

        // Cleanup stale entries periodically
        if (_windows.Count > 10000)
        {
            var cutoff = DateTime.UtcNow.AddSeconds(-_windowSeconds * 2);
            foreach (var kv in _windows)
                if (kv.Value.LastAccess < cutoff)
                    _windows.TryRemove(kv.Key, out _);
        }
    }

    private class SlidingWindow
    {
        private readonly Queue<DateTime> _timestamps = new();
        private readonly object _lock = new();

        public DateTime LastAccess { get; private set; }

        /// <summary>Try to add a request. Returns (allowed, currentCount).</summary>
        public (bool Allowed, int Count) TryAcquire(int windowSeconds, int maxRequests)
        {
            lock (_lock)
            {
                var now = DateTime.UtcNow;
                LastAccess = now;
                var cutoff = now.AddSeconds(-windowSeconds);

                while (_timestamps.Count > 0 && _timestamps.Peek() < cutoff)
                    _timestamps.Dequeue();

                if (_timestamps.Count >= maxRequests)
                    return (false, _timestamps.Count);

                _timestamps.Enqueue(now);
                return (true, _timestamps.Count);
            }
        }
    }
}

public static class RateLimitingExtensions
{
    /// <summary>Add rate limiting middleware. Defaults: 200 requests per 60 seconds per user/IP.</summary>
    public static IApplicationBuilder UseRateLimiting(this IApplicationBuilder app, int maxRequests = 200, int windowSeconds = 60)
    {
        return app.UseMiddleware<RateLimitingMiddleware>(maxRequests, windowSeconds);
    }
}
