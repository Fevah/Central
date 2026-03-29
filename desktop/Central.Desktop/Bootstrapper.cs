using Central.Core.Modules;

namespace Central.Desktop;

/// <summary>
/// Application bootstrapper — discovers modules, builds DI container.
/// Based on TotalLink's Bootstrapper + InitModulesStartupWorker pattern.
///
/// Phase 2: Simple module discovery + registration collection.
/// Phase 3: Full Autofac container with per-module service registration.
/// </summary>
public static class Bootstrapper
{
    /// <summary>
    /// Discover all IModule implementations and register their ribbon/panel contributions.
    /// Called from App.OnStartup before MainWindow is created.
    /// </summary>
    public static void Initialize()
    {
        // Directly instantiate all known modules (discovery scan unreliable in Release builds)
        App.Modules = _moduleTypes.Select(t =>
        {
            try { return (IModule)Activator.CreateInstance(t)!; }
            catch { return null; }
        }).Where(m => m != null).Cast<IModule>().OrderBy(m => m.SortOrder).ToList();

        // Each module registers its ribbon pages and panels
        foreach (var mod in App.Modules.OfType<IModuleRibbon>())
            mod.RegisterRibbon(App.RibbonBuilder);
        foreach (var mod in App.Modules.OfType<IModulePanels>())
            mod.RegisterPanels(App.PanelBuilder);

        var moduleList = string.Join(", ", App.Modules.Select(m => m.Name));
        System.Diagnostics.Debug.WriteLine($"[Bootstrapper] {App.Modules.Count} modules: {moduleList}");
        try { System.IO.File.WriteAllText(
            System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "modules.log"),
            $"{DateTime.Now}: {App.Modules.Count} modules: {moduleList}\nPages: {App.RibbonBuilder.Pages.Count}: {string.Join(", ", App.RibbonBuilder.Pages.Select(p => p.Header))}"); }
        catch { }
    }

    private static readonly Type[] _moduleTypes =
    {
        // Devices parent + sub-modules (all merge into Devices ribbon tab)
        typeof(Central.Module.Devices.DevicesModule),
        typeof(Central.Module.Switches.SwitchesModule),
        typeof(Central.Module.Links.LinksModule),
        typeof(Central.Module.Routing.RoutingModule),
        typeof(Central.Module.VLANs.VlansModule),

        // Core (always loaded)
        typeof(Central.Module.Admin.AdminModule),

        // Standalone module DLLs (expandable)
        typeof(Central.Module.Tasks.TasksModule),
        typeof(Central.Module.ServiceDesk.ServiceDeskModule),

        // Platform-level (global admin only)
        typeof(Central.Module.GlobalAdmin.GlobalAdminModule),
    };

    private static void ForceLoadModuleAssemblies()
    {
        // Force-load each module assembly so DiscoverModules can scan them
        foreach (var type in _moduleTypes)
            System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(type.TypeHandle);
    }
}
