using System.IO;
using System.Reflection;
using System.Runtime.Loader;

namespace Central.Engine.Modules;

/// <summary>
/// One isolated, collectible <see cref="AssemblyLoadContext"/> per
/// hot-swappable module. Phase 3 of the module-update system
/// (see <c>docs/MODULE_UPDATE_SYSTEM.md</c>).
///
/// Each module lives in its own context so <see cref="AssemblyLoadContext.Unload"/>
/// can free the old version's types before the new version's bytes
/// load. Without per-module contexts, every module shares the default
/// <see cref="AssemblyLoadContext"/> and <b>nothing unloads</b> until
/// the process exits — which defeats hot-swap entirely.
///
/// <para><b>What doesn't get unloaded automatically:</b></para>
/// <list type="bullet">
/// <item>Static event subscriptions keep the context rooted until
/// every subscription is explicitly disposed. Use
/// <see cref="Central.Engine.Shell.PanelMessageBus"/>-returned
/// <see cref="IDisposable"/> handles; modules must dispose on
/// <see cref="Central.Engine.Shell.ModuleReloadingMessage"/>.</item>
/// <item>Any strong reference held by host code — e.g. a ribbon
/// button's <c>OnClick</c> lambda captured by the shell — roots the
/// module's assembly. <see cref="ModuleHost"/> drops all strong refs
/// before calling <see cref="Unload"/>.</item>
/// <item>WPF <c>ResourceDictionary</c> entries merged into the
/// application's global resources hold typed references that prevent
/// collection. Feedback rule <c>feedback_wpf_resources</c> already
/// requires modules to keep resources scoped to their
/// <c>UserControl</c>s.</item>
/// <item>XAML <c>pack://application:,,,/Central.Module.X;component/...</c>
/// URIs bind to the assembly at parse time. After unload, any stale
/// URI hits the old assembly + crashes. <see cref="ModuleHost"/> fixes
/// this by re-resolving panel types from the new context on reload.</item>
/// </list>
///
/// <para>After <see cref="Unload"/>, GC collection is not guaranteed
/// to be immediate. Callers that need to verify the context actually
/// went away (e.g. the test suite) must force
/// <see cref="GC.Collect"/> + <see cref="GC.WaitForPendingFinalizers"/>
/// before checking a <see cref="WeakReference"/> to this context.
/// Two cycles are usually enough; three is common in tests to pin
/// generation-2 finalisers.</para>
/// </summary>
public sealed class CollectibleModuleLoadContext : AssemblyLoadContext
{
    /// <summary>
    /// Create a collectible context named for the module code
    /// (e.g. <c>"Central.Module.CRM"</c>). The name surfaces in dumps
    /// + <c>AssemblyLoadContext.All</c> diagnostics.
    /// </summary>
    public CollectibleModuleLoadContext(string moduleCode)
        : base(name: $"Central.Module.{moduleCode}", isCollectible: true)
    {
        if (string.IsNullOrWhiteSpace(moduleCode))
            throw new ArgumentException("Module code must be non-empty.", nameof(moduleCode));
        ModuleCode = moduleCode;
    }

    /// <summary>Module code this context was created for.</summary>
    public string ModuleCode { get; }

    /// <summary>
    /// Load an assembly from bytes. Simple wrapper over
    /// <see cref="AssemblyLoadContext.LoadFromStream(Stream)"/> that
    /// accepts the byte-array form the API download returns.
    /// </summary>
    public Assembly LoadFromBytes(byte[] dll)
    {
        ArgumentNullException.ThrowIfNull(dll);
        if (dll.Length == 0)
            throw new ArgumentException("DLL byte array is empty.", nameof(dll));
        using var ms = new MemoryStream(dll, writable: false);
        return LoadFromStream(ms);
    }

    /// <summary>
    /// Resolution fallback — delegates to the default context so
    /// shared framework + engine types (e.g. <c>Central.Engine</c>,
    /// <c>System.Windows</c>, <c>DevExpress.Xpf.Grid</c>) resolve
    /// once globally instead of duplicating per module. Returning
    /// null lets the runtime fall back to the default context for
    /// anything we don't want to isolate.
    /// </summary>
    protected override Assembly? Load(AssemblyName assemblyName) => null;
}
