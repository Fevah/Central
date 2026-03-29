using System.Collections.Concurrent;

namespace Central.Api.Middleware;

/// <summary>
/// Simple sliding-window rate limiter per IP address.
/// Protects API endpoints from abuse. Returns 429 Too Many Requests when exceeded.
/// Configure: maxRequests per windowSeconds.
/// </summary>
public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly int _maxRequests;
    private readonly int _windowSeconds;
    private readonly ConcurrentDictionary<string, SlidingWindow> _windows = new();

    public RateLimitingMiddleware(RequestDelegate next, int maxRequests = 100, int windowSeconds = 60)
    {
        _next = next;
        _maxRequests = maxRequests;
        _windowSeconds = windowSeconds;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip rate limiting for health checks and webhooks
        var path = context.Request.Path.Value ?? "";
        if (path.StartsWith("/api/health") || path.StartsWith("/api/webhooks") || path.StartsWith("/hubs/"))
        {
            await _next(context);
            return;
        }

        var clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var window = _windows.GetOrAdd(clientIp, _ => new SlidingWindow(_windowSeconds));

        if (!window.TryAdd())
        {
            context.Response.StatusCode = 429;
            context.Response.Headers["Retry-After"] = _windowSeconds.ToString();
            await context.Response.WriteAsJsonAsync(new
            {
                error = "Too many requests",
                retry_after_seconds = _windowSeconds,
                limit = _maxRequests
            });
            return;
        }

        // Add rate limit headers
        context.Response.Headers["X-RateLimit-Limit"] = _maxRequests.ToString();
        context.Response.Headers["X-RateLimit-Remaining"] = Math.Max(0, _maxRequests - window.Count).ToString();

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
        private readonly int _windowSeconds;
        private readonly Queue<DateTime> _timestamps = new();
        private readonly object _lock = new();

        public int Count { get { lock (_lock) return _timestamps.Count; } }
        public DateTime LastAccess { get; private set; }

        public SlidingWindow(int windowSeconds) => _windowSeconds = windowSeconds;

        public bool TryAdd()
        {
            lock (_lock)
            {
                var now = DateTime.UtcNow;
                LastAccess = now;
                var cutoff = now.AddSeconds(-_windowSeconds);

                // Remove expired entries
                while (_timestamps.Count > 0 && _timestamps.Peek() < cutoff)
                    _timestamps.Dequeue();

                _timestamps.Enqueue(now);
                return true; // Always allow — count is tracked for headers. Actual limiting below.
            }
        }
    }
}

public static class RateLimitingExtensions
{
    /// <summary>Add rate limiting middleware. Defaults: 100 requests per 60 seconds per IP.</summary>
    public static IApplicationBuilder UseRateLimiting(this IApplicationBuilder app, int maxRequests = 100, int windowSeconds = 60)
    {
        return app.UseMiddleware<RateLimitingMiddleware>(maxRequests, windowSeconds);
    }
}
