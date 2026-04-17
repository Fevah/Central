using System.Collections.Concurrent;
using System.Diagnostics;

namespace Central.Core.Shell;

/// <summary>
/// Enterprise mediator for in-process message routing.
/// Replaces PanelMessageBus with typed handlers, pipeline behaviors,
/// selective routing, and performance tracking.
///
/// Architecture:
///   Mediator (singleton) → Pipeline → Handler(s)
///   - Publish: 1-to-many broadcast (with optional filter)
///   - Send: 1-to-1 request/response (future)
///   - Pipeline: logging, performance, validation behaviors
///
/// For cross-instance: use SignalR (already in place).
/// For cross-service: use API endpoints.
/// NOT RabbitMQ — that's for microservices, not in-app panel linking.
/// </summary>
public interface IMediator
{
    /// <summary>Publish a message to all registered handlers.</summary>
    void Publish<T>(T message) where T : IPanelMessage;

    /// <summary>Publish asynchronously.</summary>
    Task PublishAsync<T>(T message) where T : IPanelMessage;

    /// <summary>Subscribe to a message type. Returns a disposable subscription token.</summary>
    IDisposable Subscribe<T>(Action<T> handler, string? subscriberId = null) where T : IPanelMessage;

    /// <summary>Subscribe with an async handler.</summary>
    IDisposable Subscribe<T>(Func<T, Task> handler, string? subscriberId = null) where T : IPanelMessage;

    /// <summary>Subscribe with a message filter — handler only invoked if filter returns true.</summary>
    IDisposable Subscribe<T>(Action<T> handler, Func<T, bool> filter, string? subscriberId = null) where T : IPanelMessage;

    /// <summary>Get active subscription count for diagnostics.</summary>
    int GetSubscriptionCount<T>() where T : IPanelMessage;

    /// <summary>Get all subscription diagnostics.</summary>
    IReadOnlyDictionary<string, int> GetDiagnostics();
}

/// <summary>Pipeline behavior executed before/after message delivery.</summary>
public interface IMediatorBehavior
{
    void BeforePublish<T>(T message) where T : IPanelMessage;
    void AfterPublish<T>(T message, int handlerCount, long elapsedMs) where T : IPanelMessage;
}

/// <summary>
/// Enterprise mediator implementation.
/// Thread-safe, supports filtered subscriptions, pipeline behaviors, and diagnostics.
/// </summary>
public sealed class Mediator : IMediator
{
    private readonly ConcurrentDictionary<Type, List<SubscriptionEntry>> _subscriptions = new();
    private readonly List<IMediatorBehavior> _behaviors = new();
    private readonly object _lock = new();
    private long _totalPublished;
    private long _totalDelivered;

    public static Mediator Instance { get; } = new();

    /// <summary>Add a pipeline behavior (logging, performance, etc.).</summary>
    public void AddBehavior(IMediatorBehavior behavior) => _behaviors.Add(behavior);

    public void Publish<T>(T message) where T : IPanelMessage
    {
        var sw = Stopwatch.StartNew();
        Interlocked.Increment(ref _totalPublished);

        foreach (var b in _behaviors) b.BeforePublish(message);

        var entries = GetEntries<T>();
        int delivered = 0;

        foreach (var entry in entries)
        {
            try
            {
                if (entry.Filter != null && !((Func<T, bool>)entry.Filter)(message))
                    continue;

                if (entry.SyncHandler != null)
                    ((Action<T>)entry.SyncHandler)(message);
                else if (entry.AsyncHandler != null)
                    _ = ((Func<T, Task>)entry.AsyncHandler)(message);

                delivered++;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Mediator] Handler error in {entry.SubscriberId}: {ex.Message}");
            }
        }

        Interlocked.Add(ref _totalDelivered, delivered);
        foreach (var b in _behaviors) b.AfterPublish(message, delivered, sw.ElapsedMilliseconds);
    }

    public async Task PublishAsync<T>(T message) where T : IPanelMessage
    {
        var sw = Stopwatch.StartNew();
        Interlocked.Increment(ref _totalPublished);

        foreach (var b in _behaviors) b.BeforePublish(message);

        var entries = GetEntries<T>();
        int delivered = 0;

        foreach (var entry in entries)
        {
            try
            {
                if (entry.Filter != null && !((Func<T, bool>)entry.Filter)(message))
                    continue;

                if (entry.AsyncHandler != null)
                    await ((Func<T, Task>)entry.AsyncHandler)(message);
                else if (entry.SyncHandler != null)
                    ((Action<T>)entry.SyncHandler)(message);

                delivered++;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Mediator] Handler error in {entry.SubscriberId}: {ex.Message}");
            }
        }

        Interlocked.Add(ref _totalDelivered, delivered);
        foreach (var b in _behaviors) b.AfterPublish(message, delivered, sw.ElapsedMilliseconds);
    }

    public IDisposable Subscribe<T>(Action<T> handler, string? subscriberId = null) where T : IPanelMessage
    {
        var entry = new SubscriptionEntry
        {
            SyncHandler = handler,
            SubscriberId = subscriberId ?? handler.Method.DeclaringType?.Name ?? "anonymous"
        };
        AddEntry<T>(entry);
        return new Unsubscriber(() => RemoveEntry<T>(entry));
    }

    public IDisposable Subscribe<T>(Func<T, Task> handler, string? subscriberId = null) where T : IPanelMessage
    {
        var entry = new SubscriptionEntry
        {
            AsyncHandler = handler,
            SubscriberId = subscriberId ?? handler.Method.DeclaringType?.Name ?? "anonymous"
        };
        AddEntry<T>(entry);
        return new Unsubscriber(() => RemoveEntry<T>(entry));
    }

    public IDisposable Subscribe<T>(Action<T> handler, Func<T, bool> filter, string? subscriberId = null) where T : IPanelMessage
    {
        var entry = new SubscriptionEntry
        {
            SyncHandler = handler,
            Filter = filter,
            SubscriberId = subscriberId ?? handler.Method.DeclaringType?.Name ?? "anonymous"
        };
        AddEntry<T>(entry);
        return new Unsubscriber(() => RemoveEntry<T>(entry));
    }

    public int GetSubscriptionCount<T>() where T : IPanelMessage
        => _subscriptions.TryGetValue(typeof(T), out var list) ? list.Count : 0;

    public IReadOnlyDictionary<string, int> GetDiagnostics()
    {
        var result = new Dictionary<string, int>
        {
            ["total_published"] = (int)_totalPublished,
            ["total_delivered"] = (int)_totalDelivered
        };
        foreach (var kv in _subscriptions)
            result[$"subscribers_{kv.Key.Name}"] = kv.Value.Count;
        return result;
    }

    // ── Internal ──

    private List<SubscriptionEntry> GetEntries<T>()
    {
        lock (_lock)
        {
            if (!_subscriptions.TryGetValue(typeof(T), out var list)) return new();
            return new List<SubscriptionEntry>(list); // snapshot
        }
    }

    private void AddEntry<T>(SubscriptionEntry entry)
    {
        lock (_lock)
        {
            if (!_subscriptions.TryGetValue(typeof(T), out var list))
            {
                list = new List<SubscriptionEntry>();
                _subscriptions[typeof(T)] = list;
            }
            list.Add(entry);
        }
    }

    private void RemoveEntry<T>(SubscriptionEntry entry)
    {
        lock (_lock)
        {
            if (_subscriptions.TryGetValue(typeof(T), out var list))
                list.Remove(entry);
        }
    }

    private class SubscriptionEntry
    {
        public Delegate? SyncHandler;
        public Delegate? AsyncHandler;
        public Delegate? Filter;
        public string SubscriberId = "";
    }

    private class Unsubscriber : IDisposable
    {
        private readonly Action _unsub;
        public Unsubscriber(Action unsub) => _unsub = unsub;
        public void Dispose() => _unsub();
    }
}

/// <summary>Logs all mediator messages to debug output. Add via Mediator.Instance.AddBehavior().</summary>
public class MediatorLoggingBehavior : IMediatorBehavior
{
    public void BeforePublish<T>(T message) where T : IPanelMessage
    {
        Debug.WriteLine($"[Mediator] Publishing {typeof(T).Name}: {message}");
    }

    public void AfterPublish<T>(T message, int handlerCount, long elapsedMs) where T : IPanelMessage
    {
        if (elapsedMs > 50) // Only log slow messages
            Debug.WriteLine($"[Mediator] {typeof(T).Name} → {handlerCount} handlers in {elapsedMs}ms (SLOW)");
    }
}

/// <summary>Tracks message performance metrics. Add via Mediator.Instance.AddBehavior().</summary>
public class MediatorPerformanceBehavior : IMediatorBehavior
{
    private readonly ConcurrentDictionary<string, (long Count, long TotalMs)> _metrics = new();

    public void BeforePublish<T>(T message) where T : IPanelMessage { }

    public void AfterPublish<T>(T message, int handlerCount, long elapsedMs) where T : IPanelMessage
    {
        var key = typeof(T).Name;
        _metrics.AddOrUpdate(key,
            _ => (1, elapsedMs),
            (_, existing) => (existing.Count + 1, existing.TotalMs + elapsedMs));
    }

    public IReadOnlyDictionary<string, (long Count, long TotalMs, double AvgMs)> GetMetrics()
        => _metrics.ToDictionary(kv => kv.Key, kv => (kv.Value.Count, kv.Value.TotalMs, (double)kv.Value.TotalMs / kv.Value.Count));
}
