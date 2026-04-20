using System.Reflection;
using System.Security.Cryptography;
using Central.Engine.Shell;

namespace Central.Engine.Modules;

/// <summary>
/// Owns the lifecycle of one hot-swappable module: load bytes into
/// a <see cref="CollectibleModuleLoadContext"/>, instantiate the
/// <see cref="IModule"/>, and later unload so a new version can
/// take its place. Phase 3 of the module-update system
/// (see <c>docs/MODULE_UPDATE_SYSTEM.md</c>).
///
/// <para>This class is intentionally WPF-free so the test suite can
/// exercise it headlessly. Wiring the ribbon + panels on top (so a
/// reload actually tears down + rebuilds the UI) is the next layer —
/// see the upcoming <c>ModuleHostManager</c> that will consume these
/// primitives and coordinate with MainWindow through the lifecycle
/// messages below.</para>
///
/// <para>Host emits three messages on <see cref="PanelMessageBus"/>:
/// <see cref="ModuleReloadingMessage"/> before unload,
/// <see cref="ModuleReloadedMessage"/> after the new version is live,
/// <see cref="ModuleLoadFailedMessage"/> on any failure. Panels +
/// MainWindow subscribe to drive their own tear-down/re-open.</para>
///
/// <para>Failure behaviour: <see cref="Reload"/> catches + reports
/// without leaving the host in a half-loaded state. If the new DLL
/// fails to load, the context is unloaded and the host transitions
/// to <see cref="IsLoaded"/>=false — the caller decides whether to
/// retry the old bytes or surface the failure. Host does NOT keep a
/// copy of the previous DLL bytes; rollback is a caller concern
/// (usually "re-download the previous version and Load() again").</para>
/// </summary>
public sealed class ModuleHost : IDisposable
{
    /// <summary>Module code this host owns.</summary>
    public string ModuleCode { get; }

    /// <summary>Currently-loaded version string (null before Load()).</summary>
    public string? LoadedVersion { get; private set; }

    /// <summary>The live <see cref="IModule"/> instance, or null when not loaded.</summary>
    public IModule? LiveModule { get; private set; }

    /// <summary>
    /// <c>WeakReference</c> to the most recently unloaded context.
    /// Tests assert <c>.TryGetTarget(out _) == false</c> after
    /// <see cref="GC.Collect"/> to prove the context actually went
    /// away. Production code doesn't need this — it's diagnostic.
    /// </summary>
    public WeakReference<CollectibleModuleLoadContext>? LastUnloadedContext { get; private set; }

    private CollectibleModuleLoadContext? _context;
    private Assembly? _loadedAssembly;

    /// <summary>
    /// True when this host wraps a module that was loaded into the
    /// default <see cref="AssemblyLoadContext"/> via project reference
    /// (pre-Phase 5b). Static hosts are visible to the
    /// <see cref="ModuleHostManager"/> so notifications route to them,
    /// but they can't be unloaded — updates force a full restart
    /// regardless of the notification's <c>changeKind</c>.
    /// </summary>
    public bool IsStatic { get; }

    /// <summary>Construct a dynamic host — load DLL bytes via
    /// <see cref="Load"/> / <see cref="Reload"/>.</summary>
    public ModuleHost(string moduleCode)
    {
        if (string.IsNullOrWhiteSpace(moduleCode))
            throw new ArgumentException("Module code must be non-empty.", nameof(moduleCode));
        ModuleCode = moduleCode;
    }

    /// <summary>
    /// Construct a static host wrapping an already-loaded module
    /// instance. Used by the desktop shell to register project-
    /// referenced modules with the <see cref="ModuleHostManager"/>
    /// so they participate in the update notification flow. Attempts
    /// to <see cref="Load"/> / <see cref="Reload"/> throw; update
    /// notifications get routed to <c>HandleFullRestartAsync</c> in
    /// the policy since the default ALC can't be unloaded.
    /// </summary>
    public ModuleHost(string moduleCode, IModule staticModule, string version)
    {
        if (string.IsNullOrWhiteSpace(moduleCode))
            throw new ArgumentException("Module code must be non-empty.", nameof(moduleCode));
        ArgumentNullException.ThrowIfNull(staticModule);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);

        ModuleCode    = moduleCode;
        LiveModule    = staticModule;
        LoadedVersion = version;
        IsStatic      = true;
    }

    /// <summary>True between a successful <see cref="Load"/> and the next <see cref="Unload"/>.</summary>
    public bool IsLoaded => LiveModule is not null;

    /// <summary>
    /// Load a module from in-memory DLL bytes. If
    /// <paramref name="expectedSha256"/> is non-null, the bytes are
    /// hashed + compared before touching the <see cref="AssemblyLoadContext"/> —
    /// a mismatch throws without polluting the AppDomain.
    ///
    /// <para>Post-condition on success: <see cref="IsLoaded"/>=true,
    /// <see cref="LiveModule"/>=the instance, <see cref="LoadedVersion"/>
    /// =<paramref name="version"/>.</para>
    ///
    /// <para>Throws <see cref="InvalidOperationException"/> if the
    /// host is already loaded — callers must <see cref="Unload"/>
    /// first (or use <see cref="Reload"/> which handles both sides).</para>
    /// </summary>
    public void Load(byte[] dllBytes, string version, string? expectedSha256 = null)
    {
        if (IsStatic)
            throw new InvalidOperationException(
                $"ModuleHost '{ModuleCode}' is static (project-referenced). " +
                "Dynamic Load/Unload/Reload are not supported — the module must be " +
                "migrated to the ALC path or the host replaced with a dynamic one first.");

        if (IsLoaded)
            throw new InvalidOperationException(
                $"ModuleHost '{ModuleCode}' is already loaded at version '{LoadedVersion}'. " +
                "Call Unload() first, or use Reload() which does both sides in one call.");

        ArgumentNullException.ThrowIfNull(dllBytes);
        if (dllBytes.Length == 0)
            throw new ArgumentException("DLL byte array is empty.", nameof(dllBytes));
        ArgumentException.ThrowIfNullOrWhiteSpace(version);

        if (expectedSha256 is not null)
        {
            var actual = Convert.ToHexString(SHA256.HashData(dllBytes)).ToLowerInvariant();
            if (!string.Equals(actual, expectedSha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    $"SHA-256 mismatch for module '{ModuleCode}' version '{version}': " +
                    $"expected '{expectedSha256}', got '{actual}'. Refusing to load.");
        }

        var ctx = new CollectibleModuleLoadContext(ModuleCode);
        try
        {
            var asm = ctx.LoadFromBytes(dllBytes);
            var moduleType = FindModuleType(asm)
                ?? throw new InvalidOperationException(
                    $"No IModule implementation found in '{asm.FullName}'. A module DLL must expose exactly one public non-abstract IModule type.");

            if (Activator.CreateInstance(moduleType) is not IModule module)
                throw new InvalidOperationException(
                    $"Failed to instantiate {moduleType.FullName} as IModule.");

            if (module.EngineContractVersion > EngineContract.CurrentVersion)
                throw new InvalidOperationException(
                    $"Module '{ModuleCode}' declares EngineContractVersion={module.EngineContractVersion} " +
                    $"but host's EngineContract.CurrentVersion={EngineContract.CurrentVersion}. " +
                    "Refusing to load — the module was compiled against a newer engine than the host.");

            _context        = ctx;
            _loadedAssembly = asm;
            LiveModule      = module;
            LoadedVersion   = version;
        }
        catch
        {
            // Partially-constructed — unload the context immediately
            // so failed Load() doesn't leak ALCs.
            try { ctx.Unload(); } catch { /* best effort */ }
            throw;
        }
    }

    /// <summary>
    /// Drop refs to the live module + unload the
    /// <see cref="CollectibleModuleLoadContext"/>. The unload is
    /// asynchronous from the GC's perspective — see class XML for
    /// why full collection isn't immediate.
    ///
    /// <para>After this returns, <see cref="IsLoaded"/>=false and
    /// <see cref="LiveModule"/>=null. <see cref="LastUnloadedContext"/>
    /// holds a weak reference to the now-unloading context so tests
    /// can verify collectibility.</para>
    ///
    /// <para>Safe to call when not loaded (no-op).</para>
    /// </summary>
    public void Unload()
    {
        if (IsStatic) return;    // default-ALC modules can't unload
        if (!IsLoaded) return;

        var ctx = _context!;
        LiveModule      = null;
        LoadedVersion   = null;
        _loadedAssembly = null;
        _context        = null;
        LastUnloadedContext = new WeakReference<CollectibleModuleLoadContext>(ctx);
        ctx.Unload();
    }

    /// <summary>
    /// Hot-swap the loaded module to a new version. Order is: emit
    /// <see cref="ModuleReloadingMessage"/> so panels close + release
    /// refs → <see cref="Unload"/> → <see cref="Load"/> new bytes →
    /// emit <see cref="ModuleReloadedMessage"/>. On any failure, the
    /// host ends in the unloaded state + emits
    /// <see cref="ModuleLoadFailedMessage"/>; callers are responsible
    /// for retrying with the previous bytes if desired.
    /// </summary>
    public void Reload(byte[] newDllBytes, string newVersion, string? expectedSha256 = null)
    {
        var fromVersion = LoadedVersion ?? "(not loaded)";

        if (IsLoaded)
            PanelMessageBus.Publish(new ModuleReloadingMessage(ModuleCode, fromVersion, newVersion));

        try
        {
            if (IsLoaded) Unload();
            Load(newDllBytes, newVersion, expectedSha256);
            PanelMessageBus.Publish(new ModuleReloadedMessage(ModuleCode, fromVersion, newVersion));
        }
        catch (Exception ex)
        {
            PanelMessageBus.Publish(new ModuleLoadFailedMessage(
                ModuleCode,
                AttemptedVersion: newVersion,
                Reason: ex.Message,
                Exception: ex));
            throw;
        }
    }

    /// <summary>Dispose unloads if currently loaded.</summary>
    public void Dispose() => Unload();

    private static Type? FindModuleType(Assembly asm)
    {
        // Expect exactly one concrete IModule per module assembly.
        // If there's ever more than one (shouldn't happen in practice),
        // return the first — the rule is enforced by AllModulesModule
        // ContractAuditTests rather than crashing here.
        Type[] types;
        try { types = asm.GetTypes(); }
        catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(t => t != null).ToArray()!; }

        return types.FirstOrDefault(t =>
            t is not null &&
            typeof(IModule).IsAssignableFrom(t) &&
            !t.IsAbstract &&
            !t.IsInterface);
    }
}
