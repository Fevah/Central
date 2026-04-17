using Central.Engine.Modules;

namespace Central.Desktop;

/// <summary>
/// Application bootstrapper — decides which modules load for the current
/// tenant, instantiates them, and lets each register its ribbon / panels /
/// dashboard contributions.
///
/// The tenant-toggle story hinges on this file: an <see cref="IModuleLicenseGate"/>
/// is consulted for every module except <see cref="_alwaysRequired"/>. When a
/// module is gated off, its type is never instantiated — no ribbon tab, no
/// panels, no dashboard cards, no DB queries. Exactly the "disable and it's
/// gone" semantics.
/// </summary>
public static class Bootstrapper
{
    /// <summary>
    /// Instantiate the licensed module types, register their ribbon pages
    /// and panels. Call with <see cref="AllowAllModuleGate"/> during dev /
    /// pre-login; replace with a tenant-aware gate (e.g. one backed by
    /// <c>Central.Licensing.ModuleLicenseService</c>) once the tenant ID is
    /// known. Calling again is safe — ribbon/panel builders accumulate.
    /// </summary>
    public static void Initialize(IModuleLicenseGate? gate = null)
    {
        gate ??= new AllowAllModuleGate();

        // Filter the type list by the gate. Required modules (Global today)
        // bypass the check — the shell has no coherent state without them.
        var toLoad = _moduleTypes
            .Where(t => _alwaysRequired.Contains(t) || gate.IsEnabled(_moduleCodes[t]))
            .ToList();

        App.Modules = toLoad.Select(t =>
        {
            try { return (IModule)Activator.CreateInstance(t)!; }
            catch { return null; }
        }).Where(m => m != null).Cast<IModule>().OrderBy(m => m.SortOrder).ToList();

        foreach (var mod in App.Modules.OfType<IModuleRibbon>())
            mod.RegisterRibbon(App.RibbonBuilder);
        foreach (var mod in App.Modules.OfType<IModulePanels>())
            mod.RegisterPanels(App.PanelBuilder);

        var moduleList = string.Join(", ", App.Modules.Select(m => m.Name));
        System.Diagnostics.Debug.WriteLine($"[Bootstrapper] {App.Modules.Count} modules: {moduleList}");
        try
        {
            System.IO.File.WriteAllText(
                System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "modules.log"),
                $"{DateTime.Now}: {App.Modules.Count} modules: {moduleList}\nPages: {App.RibbonBuilder.Pages.Count}: {string.Join(", ", App.RibbonBuilder.Pages.Select(p => p.Header))}");
        }
        catch { }
    }

    // Module registry. Each entry is a tenant-togglable unit — the gate
    // controls whether it loads. The module code is the string the gate
    // sees (and the one that lives in central_platform.module_catalog.code
    // when licensing becomes DB-backed).
    private static readonly Dictionary<Type, string> _moduleCodes = new()
    {
        [typeof(Central.Module.Global.GlobalModule)]           = "global",
        [typeof(Central.Module.Devices.DevicesModule)]         = "devices",
        [typeof(Central.Module.Networking.NetworkingModule)]   = "networking",
        [typeof(Central.Module.Projects.ProjectsModule)]       = "projects",
        [typeof(Central.Module.ServiceDesk.ServiceDeskModule)] = "servicedesk",
        [typeof(Central.Module.CRM.CrmModule)]                 = "crm",
    };

    // Always loaded regardless of the gate. Disabling Global leaves the app
    // with no dashboard, no admin, no platform ops — there is no scenario
    // where that makes sense.
    private static readonly HashSet<Type> _alwaysRequired = new()
    {
        typeof(Central.Module.Global.GlobalModule),
    };

    // Preserved for ordering / iteration.
    private static readonly Type[] _moduleTypes = _moduleCodes.Keys.ToArray();

    private static void ForceLoadModuleAssemblies()
    {
        foreach (var type in _moduleTypes)
            System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(type.TypeHandle);
    }
}
