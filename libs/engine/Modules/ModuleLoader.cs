using System.IO;
using System.Reflection;

namespace Central.Engine.Modules;

/// <summary>
/// Discovers IModule implementations from loaded assemblies.
/// Based on TotalLink's InitModulesStartupWorker assembly scan.
/// </summary>
public static class ModuleLoader
{
    /// <summary>Load plugin DLLs from a directory before discovery.</summary>
    public static void LoadPluginAssemblies(string? pluginDirectory = null)
    {
        var dir = pluginDirectory ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "plugins");
        if (!Directory.Exists(dir)) return;

        foreach (var dll in Directory.GetFiles(dir, "Central.Module.*.dll"))
        {
            try
            {
                // Only load if not already loaded
                var name = AssemblyName.GetAssemblyName(dll);
                if (AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == name.Name))
                    continue;
                Assembly.LoadFrom(dll);
            }
            catch { /* Skip DLLs that fail to load */ }
        }
    }

    public static List<IModule> DiscoverModules()
    {
        // Auto-load plugin DLLs before scanning
        LoadPluginAssemblies();

        var modules = new List<IModule>();

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type[] types;
            try { types = assembly.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { types = ex.Types.Where(t => t != null).ToArray()!; }

            foreach (var type in types)
            {
                if (typeof(IModule).IsAssignableFrom(type) && !type.IsAbstract && !type.IsInterface)
                {
                    try
                    {
                        var module = (IModule)Activator.CreateInstance(type)!;
                        modules.Add(module);
                    }
                    catch { /* Skip modules that fail to instantiate */ }
                }
            }
        }

        return modules.OrderBy(m => m.SortOrder).ToList();
    }
}
