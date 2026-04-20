using System.Collections.Concurrent;
using Central.Engine.Shell;

namespace Central.Engine.Modules;

/// <summary>
/// Owns the collection of <see cref="ModuleHost"/> instances for every
/// module loaded in the current shell + routes server-pushed
/// <see cref="ModuleUpdateNotification"/> events to the right host
/// through a pluggable <see cref="IModuleUpdatePolicy"/>. Phase 4 of
/// the module-update system (see <c>docs/MODULE_UPDATE_SYSTEM.md</c>).
///
/// <para>Designed to be WPF-free — the test suite constructs it with
/// a fake <see cref="IModuleDllDownloader"/> + a recording
/// <see cref="IModuleUpdatePolicy"/> to exercise the dispatch logic
/// headlessly. The desktop shell wires:</para>
///
/// <list type="bullet">
/// <item><see cref="IModuleDllDownloader"/> →
/// <c>ModuleCatalogClient.DownloadDllAsync</c>.</item>
/// <item><see cref="IModuleUpdatePolicy"/> → a WPF-aware
/// implementation that shows toasts / banners / restart dialogs per
/// <c>changeKind</c>.</item>
/// </list>
///
/// <para>Per-module serialisation: updates for the same
/// <c>moduleCode</c> run sequentially (a <see cref="SemaphoreSlim"/>
/// per host). Updates for different modules may overlap — they're
/// isolated by design. This prevents the "two HotSwaps race +
/// leave the host in an indeterminate Load/Unload cycle" bug that
/// a naïve implementation would hit.</para>
///
/// <para>Failure semantics: download failure / SHA mismatch / load
/// failure all publish <see cref="ModuleLoadFailedMessage"/> on the
/// bus (already done by <see cref="ModuleHost.Reload"/>) but the
/// manager also surfaces the failure to the policy via
/// <see cref="IModuleUpdatePolicy.HandleFailureAsync"/>. Neither path
/// throws out of <see cref="HandleNotificationAsync"/>; the method
/// always completes (success or logged-failure).</para>
/// </summary>
public sealed class ModuleHostManager : IDisposable
{
    private readonly IModuleDllDownloader _downloader;
    private readonly IModuleUpdatePolicy  _policy;
    private readonly ConcurrentDictionary<string, ModuleHost> _hosts = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new(StringComparer.OrdinalIgnoreCase);

    public ModuleHostManager(IModuleDllDownloader downloader, IModuleUpdatePolicy policy)
    {
        _downloader = downloader ?? throw new ArgumentNullException(nameof(downloader));
        _policy     = policy     ?? throw new ArgumentNullException(nameof(policy));
    }

    /// <summary>Register a <see cref="ModuleHost"/> so update notifications route to it.</summary>
    public void RegisterHost(ModuleHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        _hosts[host.ModuleCode] = host;
        _locks.TryAdd(host.ModuleCode, new SemaphoreSlim(1, 1));
    }

    /// <summary>Remove a host from the routing table (e.g. on tenant-level module disable).</summary>
    public bool UnregisterHost(string moduleCode)
    {
        if (_locks.TryRemove(moduleCode, out var sem)) sem.Dispose();
        return _hosts.TryRemove(moduleCode, out _);
    }

    /// <summary>Look up a live host by module code.</summary>
    public ModuleHost? GetHost(string moduleCode) =>
        _hosts.TryGetValue(moduleCode, out var h) ? h : null;

    /// <summary>Count of registered hosts (diagnostic).</summary>
    public int RegisteredHostCount => _hosts.Count;

    /// <summary>
    /// Handle a server-pushed update notification. Serialises per
    /// moduleCode so concurrent notifications for the same module
    /// don't overlap + corrupt the host state. Always completes;
    /// failures go to <see cref="IModuleUpdatePolicy.HandleFailureAsync"/>
    /// rather than propagating.
    /// </summary>
    public async Task HandleNotificationAsync(ModuleUpdateNotification notification, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        // Silently drop notifications for modules this client doesn't
        // have loaded — a tenant that doesn't license CRM shouldn't
        // react to CRM updates. The catalog broadcast is platform-wide
        // so filtering happens here.
        if (!_hosts.TryGetValue(notification.ModuleCode, out var host))
            return;

        // Per-module serialisation.
        var sem = _locks.GetOrAdd(notification.ModuleCode, _ => new SemaphoreSlim(1, 1));
        await sem.WaitAsync(ct);
        try
        {
            // Skip no-op notifications — we're already on this version.
            if (string.Equals(host.LoadedVersion, notification.ToVersion, StringComparison.Ordinal))
                return;

            // Static hosts (project-referenced modules in the default
            // ALC) can't hot-swap regardless of the notification's
            // changeKind — the default ALC isn't collectible. Route
            // every update through HandleFullRestartAsync so the
            // policy surfaces a "restart to apply" prompt. Phase 5b
            // stops here; future refactor migrates modules to the
            // dynamic ALC path so the switch below can take over.
            if (host.IsStatic)
            {
                await _policy.HandleFullRestartAsync(notification, ct);
                return;
            }

            switch (notification.ChangeKind)
            {
                case "HotSwap":
                    await HandleHotSwapAsync(host, notification, ct);
                    break;

                case "SoftReload":
                    // Don't pull the DLL yet — wait for the user to click
                    // "Reload now" via the policy's UX. The policy
                    // returns once the user acts (or timer fires / they
                    // dismiss).
                    await _policy.HandleSoftReloadAsync(notification, () => ApplyUpdateAsync(host, notification, ct), ct);
                    break;

                case "FullRestart":
                    // Don't touch this host — the whole process restarts.
                    // Policy schedules the restart + downloads the full
                    // client package.
                    await _policy.HandleFullRestartAsync(notification, ct);
                    break;

                default:
                    await _policy.HandleFailureAsync(notification,
                        reason: $"Unknown changeKind '{notification.ChangeKind}'.", ex: null, ct);
                    break;
            }
        }
        finally
        {
            sem.Release();
        }
    }

    private async Task HandleHotSwapAsync(ModuleHost host, ModuleUpdateNotification n, CancellationToken ct)
    {
        try
        {
            await ApplyUpdateAsync(host, n, ct);
            await _policy.HandleHotSwapAppliedAsync(n, ct);
        }
        catch (Exception ex)
        {
            await _policy.HandleFailureAsync(n, reason: ex.Message, ex, ct);
        }
    }

    private async Task ApplyUpdateAsync(ModuleHost host, ModuleUpdateNotification n, CancellationToken ct)
    {
        var bytes = await _downloader.DownloadAsync(n.ModuleCode, n.ToVersion, ct)
            ?? throw new InvalidOperationException(
                $"Downloader returned null for {n.ModuleCode}@{n.ToVersion} — the version may have been yanked or the byte blob is missing.");

        host.Reload(bytes, n.ToVersion, n.Sha256);
    }

    public void Dispose()
    {
        foreach (var host in _hosts.Values) host.Dispose();
        _hosts.Clear();
        foreach (var sem in _locks.Values) sem.Dispose();
        _locks.Clear();
    }
}

/// <summary>
/// Per-changeKind reaction policy. Implementations decide UX
/// (silent / banner / scheduled-restart). Desktop ships a WPF-aware
/// implementation; tests ship a recording implementation.
/// </summary>
public interface IModuleUpdatePolicy
{
    /// <summary>
    /// HotSwap has applied successfully. Typical implementation:
    /// silent toast "Networking updated to 2.4.1."
    /// </summary>
    Task HandleHotSwapAppliedAsync(ModuleUpdateNotification notification, CancellationToken ct);

    /// <summary>
    /// SoftReload notification received. Implementation shows a
    /// banner with "Reload now" button; when user clicks, invokes
    /// <paramref name="applyAction"/>. Returns once the decision is
    /// made (apply, defer, or dismiss). <paramref name="applyAction"/>
    /// may never be called if the user dismisses.
    /// </summary>
    Task HandleSoftReloadAsync(ModuleUpdateNotification notification, Func<Task> applyAction, CancellationToken ct);

    /// <summary>
    /// FullRestart notification — schedule a process restart after
    /// user confirmation. Typical implementation: 5-min countdown
    /// banner with opt-out; on fire, call the existing
    /// <c>UpdateManager.RestartApplication()</c>. Does NOT touch the
    /// host — the process restart is the mechanism.
    /// </summary>
    Task HandleFullRestartAsync(ModuleUpdateNotification notification, CancellationToken ct);

    /// <summary>
    /// Download / SHA-verify / load failure. Typical implementation:
    /// error toast + audit-log entry. Must not throw.
    /// </summary>
    Task HandleFailureAsync(ModuleUpdateNotification notification, string reason, Exception? ex, CancellationToken ct);
}

/// <summary>
/// No-op default policy — useful as a starting point for the desktop
/// wiring + for tests that don't want to assert on UX. Every method
/// is a completed <see cref="Task"/>.
/// </summary>
public sealed class DefaultModuleUpdatePolicy : IModuleUpdatePolicy
{
    public Task HandleHotSwapAppliedAsync(ModuleUpdateNotification _, CancellationToken __)   => Task.CompletedTask;
    public Task HandleSoftReloadAsync(ModuleUpdateNotification _, Func<Task> __, CancellationToken ___) => Task.CompletedTask;
    public Task HandleFullRestartAsync(ModuleUpdateNotification _, CancellationToken __)       => Task.CompletedTask;
    public Task HandleFailureAsync(ModuleUpdateNotification _, string __, Exception? ___, CancellationToken ____) => Task.CompletedTask;
}

/// <summary>
/// Downloader abstraction — keeps <see cref="ModuleHostManager"/>
/// decoupled from the HTTP client. Desktop wires this to
/// <c>ModuleCatalogClient.DownloadDllAsync</c>; tests ship a
/// deterministic fake.
/// </summary>
public interface IModuleDllDownloader
{
    /// <summary>
    /// Download the DLL bytes for <paramref name="moduleCode"/> at
    /// <paramref name="version"/>. Returns null when the version is
    /// yanked or unknown (the caller surfaces that as a failure).
    /// </summary>
    Task<byte[]?> DownloadAsync(string moduleCode, string version, CancellationToken ct = default);
}

/// <summary>
/// Plain data record for the manager's dispatch input. Decoupled
/// from SignalR payload types + DTOs in <c>libs/api-client</c> so
/// the engine layer doesn't have to reference the API client.
/// </summary>
public record ModuleUpdateNotification(
    string ModuleCode,
    string? FromVersion,
    string ToVersion,
    string ChangeKind,
    int MinEngineContract,
    string Sha256,
    long SizeBytes
);
