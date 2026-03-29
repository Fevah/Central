namespace Central.Core.Shell;

/// <summary>
/// Lightweight message bus for cross-panel communication.
/// Follows TotalLink's document-scoped messaging pattern but simplified
/// (single bus since we have one document/workspace).
///
/// Usage:
///   PanelMessageBus.Publish(new SelectionChangedMessage("devices", selectedDevice));
///   PanelMessageBus.Subscribe&lt;SelectionChangedMessage&gt;(msg => UpdateDetail(msg));
/// </summary>
public static class PanelMessageBus
{
    private static readonly Dictionary<Type, List<Delegate>> _subscriptions = new();
    private static readonly object _lock = new();

    /// <summary>Subscribe to a message type. Returns an IDisposable to unsubscribe.</summary>
    public static IDisposable Subscribe<T>(Action<T> handler) where T : IPanelMessage
    {
        lock (_lock)
        {
            if (!_subscriptions.TryGetValue(typeof(T), out var list))
            {
                list = new List<Delegate>();
                _subscriptions[typeof(T)] = list;
            }
            list.Add(handler);
        }
        return new Unsubscriber<T>(handler);
    }

    /// <summary>Publish a message to all subscribers of this type. Also routes through Mediator.</summary>
    public static void Publish<T>(T message) where T : IPanelMessage
    {
        // Legacy subscribers
        List<Delegate>? list;
        lock (_lock)
        {
            if (!_subscriptions.TryGetValue(typeof(T), out list)) list = null;
            if (list != null) list = new List<Delegate>(list); // snapshot
        }
        if (list != null)
            foreach (var handler in list)
                ((Action<T>)handler)(message);

        // Enterprise mediator — routes to LinkEngine, logging, performance tracking
        Mediator.Instance.Publish(message);
    }

    private class Unsubscriber<T> : IDisposable where T : IPanelMessage
    {
        private readonly Action<T> _handler;
        public Unsubscriber(Action<T> handler) => _handler = handler;
        public void Dispose()
        {
            lock (_lock)
            {
                if (_subscriptions.TryGetValue(typeof(T), out var list))
                    list.Remove(_handler);
            }
        }
    }
}

/// <summary>Marker interface for all panel messages.</summary>
public interface IPanelMessage { }

/// <summary>Grid selection changed — detail panel should update.</summary>
public record SelectionChangedMessage(string SourcePanel, object? SelectedItem, IReadOnlyList<object>? SelectedItems = null) : IPanelMessage;

/// <summary>Request to navigate to a specific panel and select an item.</summary>
public record NavigateToPanelMessage(string TargetPanel, object? SelectItem = null) : IPanelMessage;

/// <summary>Data in a panel was modified — other panels may need to refresh.</summary>
public record DataModifiedMessage(string SourcePanel, string EntityType, string Operation) : IPanelMessage;

/// <summary>Request to refresh a specific panel's data.</summary>
public record RefreshPanelMessage(string TargetPanel) : IPanelMessage;

/// <summary>
/// Cross-panel link: selecting a row in one grid filters related grids.
/// SourcePanel published the selection. Field is the column name (e.g. "TechnicianName").
/// Value is the filter value. Null value = clear filter.
/// Subscribing panels check if they have a matching field and apply the filter.
/// </summary>
public record LinkSelectionMessage(string SourcePanel, string Field, object? Value) : IPanelMessage;
