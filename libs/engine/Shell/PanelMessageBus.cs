namespace Central.Engine.Shell;

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

/// <summary>Request to open (dock-restore) a document panel without
/// selecting any item inside it. NavigateToPanelMessage only covers
/// the subscribe-inside-the-panel half of the flow; this is the
/// outer-shell half — MainWindow subscribes and flips the matching
/// VM.Is*PanelOpen boolean, which drives DockController.Restore.
/// Use together when cross-panel drill-down needs both "open the
/// panel" and "select/filter to this item" (e.g. search → audit).
/// TargetPanel matches the panel-id constants in
/// NetworkingRibbonRegistrar (e.g. "audit", "search", "bulk").
/// </summary>
public record OpenPanelMessage(string TargetPanel) : IPanelMessage;

// ─── Module lifecycle messages ─────────────────────────────────────────────
// Phase 3 of the module-update system (docs/MODULE_UPDATE_SYSTEM.md).
// ModuleHost raises these as a module transitions Loaded → Reloading →
// Reloaded; MainWindow + open panels subscribe to close/reopen themselves
// cleanly around the AssemblyLoadContext swap. Additive messages —
// handlers that don't care about hot-swap simply don't subscribe.

/// <summary>
/// Raised by <c>ModuleHost.Reload()</c> before the current
/// AssemblyLoadContext is unloaded. Panels owned by this module should
/// unsubscribe from the bus + close themselves + release all strong
/// refs to types from the module's assembly before the host tries to
/// unload. MainWindow remembers which panels were open so it can
/// reopen them once <see cref="ModuleReloadedMessage"/> fires.
/// </summary>
public record ModuleReloadingMessage(string ModuleCode, string FromVersion, string ToVersion) : IPanelMessage;

/// <summary>
/// Raised by <c>ModuleHost.Reload()</c> after the new DLL has loaded
/// successfully into a fresh <c>AssemblyLoadContext</c> + the module's
/// <c>RegisterRibbon</c>/<c>RegisterPanels</c> have re-run. MainWindow
/// reopens any panels that were open before the reload. If a re-open
/// fails (e.g. missing panel type in the new version), the panel
/// stays closed and the user sees a toast; no crash.
/// </summary>
public record ModuleReloadedMessage(string ModuleCode, string FromVersion, string ToVersion) : IPanelMessage;

/// <summary>
/// Raised when <c>ModuleHost.Reload()</c> or <c>ModuleHost.Load()</c>
/// fails (bad DLL, SHA-256 mismatch, incompatible
/// <see cref="IModule.EngineContractVersion"/>, exception in
/// <c>RegisterRibbon</c>). The host falls back to the previous
/// version (if any) and emits this message so MainWindow can surface
/// a toast. <see cref="Reason"/> is user-facing; <see cref="Exception"/>
/// is the raw throwable for logs.
/// </summary>
public record ModuleLoadFailedMessage(string ModuleCode, string AttemptedVersion, string Reason, Exception? Exception = null) : IPanelMessage;
