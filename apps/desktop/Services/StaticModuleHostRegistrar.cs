using Central.Engine.Modules;

namespace Central.Desktop.Services;

/// <summary>
/// Phase 5b of the module-update system (see
/// <c>docs/MODULE_UPDATE_SYSTEM.md</c>). Walks the list of live
/// <see cref="IModule"/> instances the <see cref="Bootstrapper"/>
/// has already loaded via project reference + registers a static
/// <see cref="ModuleHost"/> per module with the
/// <see cref="ModuleHostManager"/>.
///
/// <para>Static hosts wrap modules that live in the default
/// <see cref="System.Runtime.Loader.AssemblyLoadContext"/> + can't
/// be unloaded — the manager recognises them via
/// <see cref="ModuleHost.IsStatic"/> and routes every update
/// notification through <c>HandleFullRestartAsync</c> regardless of
/// the publisher's <c>changeKind</c>. Users see a
/// <see cref="Central.Engine.Services.NotificationService"/>
/// warning toast "restart to apply"; no crash, no silent drop.</para>
///
/// <para>When a future roll migrates modules to the dynamic ALC
/// path, this registrar becomes unnecessary — dynamic hosts go
/// through <see cref="ModuleHost.Load"/> with bytes from the
/// catalog endpoint + actually hot-swap on reload. Until then,
/// the visibility path here is the best we can do without a bigger
/// refactor.</para>
/// </summary>
public static class StaticModuleHostRegistrar
{
    /// <summary>
    /// Register a <see cref="ModuleHost"/> per live module on
    /// <see cref="App.Modules"/>. Idempotent — calling twice
    /// leaves the manager's host table in the same state (the
    /// manager's <see cref="ModuleHostManager.RegisterHost"/>
    /// overwrites on duplicate code).
    ///
    /// <para>Returns the number of hosts registered. Zero means
    /// <see cref="ModuleUpdateStartup.ManagerOrNull"/> hadn't been
    /// initialised yet — callers should re-invoke after
    /// <see cref="ModuleUpdateStartup.Initialize"/> runs.</para>
    /// </summary>
    public static int RegisterAll()
    {
        var mgr = ModuleUpdateStartup.ManagerOrNull;
        if (mgr is null) return 0;

        var count = 0;
        foreach (var module in App.Modules)
        {
            var code = Bootstrapper.GetModuleCode(module);
            if (string.IsNullOrWhiteSpace(code)) continue;

            try
            {
                var host = new ModuleHost(code!, module, module.Version);
                mgr.RegisterHost(host);
                count++;
            }
            catch (Exception ex)
            {
                // Registration failure is non-fatal — missing coverage
                // for this module on this boot, but the rest of the
                // shell keeps working. Log + continue.
                System.Diagnostics.Debug.WriteLine(
                    $"[StaticModuleHostRegistrar] Failed to register '{code}': {ex.Message}");
            }
        }
        return count;
    }
}
