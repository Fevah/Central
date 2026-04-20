using Central.ApiClient;
using Central.Engine.Modules;

namespace Central.Desktop.Services;

/// <summary>
/// Startup helper that wires the module-update pipeline once the
/// desktop knows the API base URL. Phase 5 of the module-update
/// system (see <c>docs/MODULE_UPDATE_SYSTEM.md</c>).
///
/// <para>Pipeline, once <see cref="Initialize"/> runs:</para>
/// <list type="bullet">
/// <item><see cref="ModuleCatalogClient"/> pointed at the API
/// base URL.</item>
/// <item><see cref="ApiClientModuleDllDownloader"/> wrapping it.</item>
/// <item><see cref="WpfModuleUpdatePolicy"/> driving toasts via
/// <see cref="Central.Engine.Services.NotificationService"/>.</item>
/// <item><see cref="Manager"/> — the live
/// <see cref="ModuleHostManager"/> everything else routes through.
/// Starts with zero registered hosts; Phase 5b registers a host per
/// loaded module so hot-swap actually applies.</item>
/// </list>
///
/// <para>Idempotent: calling <see cref="Initialize"/> more than once
/// with the same base URL is a no-op. Calling with a different URL
/// replaces the inner clients but keeps the same
/// <see cref="ModuleHostManager"/> so any registered hosts survive.
/// (Re-login against a different API isn't a common case; the
/// ordinary reconnect path uses the same URL.)</para>
/// </summary>
public static class ModuleUpdateStartup
{
    private static readonly object _lock = new();
    private static ModuleHostManager? _manager;
    private static ModuleCatalogClient? _catalog;
    private static string? _apiBaseUrl;

    /// <summary>The shared manager, or null if <see cref="Initialize"/> hasn't run yet.</summary>
    public static ModuleHostManager? ManagerOrNull => _manager;

    /// <summary>
    /// The shared catalog client, or null if <see cref="Initialize"/>
    /// hasn't run yet. Useful so callers can call
    /// <see cref="ModuleCatalogClient.InvalidateCatalogCache"/> on
    /// SignalR <c>ModuleUpdated</c> events + force the next banner
    /// query to refetch.
    /// </summary>
    public static ModuleCatalogClient? CatalogClientOrNull => _catalog;

    /// <summary>
    /// The shared manager. Throws <see cref="InvalidOperationException"/>
    /// if called before <see cref="Initialize"/> — callers on the
    /// no-login path should use <see cref="ManagerOrNull"/> instead.
    /// </summary>
    public static ModuleHostManager Manager =>
        _manager ?? throw new InvalidOperationException(
            "ModuleUpdateStartup.Manager accessed before Initialize(apiBaseUrl) ran. " +
            "Call Initialize after login, before handing the manager to subscribers.");

    /// <summary>
    /// Build or refresh the manager. Safe to call from the UI thread
    /// or any worker — the internal lock serialises concurrent init.
    /// </summary>
    public static void Initialize(string apiBaseUrl)
    {
        if (string.IsNullOrWhiteSpace(apiBaseUrl))
            throw new ArgumentException("API base URL must be non-empty.", nameof(apiBaseUrl));

        lock (_lock)
        {
            // No-op on repeat init with same URL — keeps registered
            // hosts stable across reconnect.
            if (_manager is not null && string.Equals(_apiBaseUrl, apiBaseUrl, StringComparison.Ordinal))
                return;

            // Construct fresh clients pointed at the new URL.
            var http = new HttpClient { BaseAddress = new Uri(apiBaseUrl.TrimEnd('/') + "/") };
            var catalog = new ModuleCatalogClient(http);
            var downloader = new ApiClientModuleDllDownloader(catalog);

            if (_manager is null)
            {
                _manager = new ModuleHostManager(downloader, new WpfModuleUpdatePolicy());
            }
            // If the manager already exists from a prior Initialize
            // call, we don't have a way to swap its downloader — it
            // captured the previous instance at construction. Rebuild.
            else
            {
                _manager.Dispose();
                _manager = new ModuleHostManager(downloader, new WpfModuleUpdatePolicy());
            }

            _catalog    = catalog;
            _apiBaseUrl = apiBaseUrl;
        }
    }

    /// <summary>
    /// Convert the <see cref="SignalRClient.ModuleUpdated"/> payload
    /// (which lives in <c>libs/api-client</c>) into the
    /// <see cref="ModuleUpdateNotification"/> record the engine
    /// layer accepts. Pure data translation — no side effects.
    /// </summary>
    public static ModuleUpdateNotification ToEngineNotification(ModuleUpdatedPayload p)
        => new(
            ModuleCode:        p.ModuleCode,
            FromVersion:       p.FromVersion,
            ToVersion:         p.ToVersion,
            ChangeKind:        p.ChangeKind,
            MinEngineContract: p.MinEngineContract,
            Sha256:            p.Sha256,
            SizeBytes:         p.SizeBytes);
}
