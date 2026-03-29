using System.Collections.Concurrent;

namespace Central.Api.Services;

/// <summary>
/// Simple in-memory rate limiter for SSH endpoints.
/// Limits per-user requests to prevent abuse of SSH operations.
/// </summary>
public class RateLimiter
{
    private readonly ConcurrentDictionary<string, Queue<DateTime>> _requests = new();
    private readonly int _maxRequests;
    private readonly TimeSpan _window;

    public RateLimiter(int maxRequests = 10, int windowSeconds = 60)
    {
        _maxRequests = maxRequests;
        _window = TimeSpan.FromSeconds(windowSeconds);
    }

    /// <summary>Returns true if the request is allowed, false if rate limited.</summary>
    public bool TryAcquire(string key)
    {
        var now = DateTime.UtcNow;
        var queue = _requests.GetOrAdd(key, _ => new Queue<DateTime>());

        lock (queue)
        {
            // Purge expired entries
            while (queue.Count > 0 && now - queue.Peek() > _window)
                queue.Dequeue();

            if (queue.Count >= _maxRequests)
                return false;

            queue.Enqueue(now);
            return true;
        }
    }
}
