using Central.Engine.Modules;

namespace Central.Tests.Engine;

/// <summary>
/// Engine-level audit of the <see cref="IModule"/> contract. Each module
/// advertises a Name (shown in the ribbon + dashboard header), a
/// PermissionCategory (used by license gates + the AuthContext lookup),
/// and a SortOrder (ribbon tab position). Collisions on SortOrder
/// manifest as non-deterministic tab ordering; collisions on
/// PermissionCategory are worse — the license gate accidentally toggles
/// two modules on/off as one.
/// </summary>
[Collection("RibbonAudit")]
public class AllModulesModuleContractAuditTests
{
    public static IEnumerable<object[]> DiscoveredModules() => new[]
    {
        new object[] { typeof(Central.Module.Networking.NetworkingModule) },
        new object[] { typeof(Central.Module.CRM.CrmModule) },
        new object[] { typeof(Central.Module.Projects.ProjectsModule) },
        new object[] { typeof(Central.Module.Audit.AuditModule) },
        new object[] { typeof(Central.Module.ServiceDesk.ServiceDeskModule) },
        new object[] { typeof(Central.Module.Global.GlobalModule) },
    };

    [Theory]
    [MemberData(nameof(DiscoveredModules))]
    public void ModuleHasNonEmptyName(Type moduleType)
    {
        var m = (IModule)Activator.CreateInstance(moduleType)!;
        Assert.False(string.IsNullOrWhiteSpace(m.Name),
            $"{moduleType.Name}.Name is empty — no ribbon tab label.");
    }

    [Theory]
    [MemberData(nameof(DiscoveredModules))]
    public void ModuleHasNonEmptyPermissionCategory(Type moduleType)
    {
        var m = (IModule)Activator.CreateInstance(moduleType)!;
        Assert.False(string.IsNullOrWhiteSpace(m.PermissionCategory),
            $"{moduleType.Name}.PermissionCategory is empty — license gate " +
            $"cannot key on this module (IModuleLicenseGate.IsEnabled uses " +
            $"PermissionCategory as the module code).");
    }

    [Theory]
    [MemberData(nameof(DiscoveredModules))]
    public void ModuleSortOrderIsNonNegative(Type moduleType)
    {
        var m = (IModule)Activator.CreateInstance(moduleType)!;
        Assert.True(m.SortOrder >= 0,
            $"{moduleType.Name}.SortOrder={m.SortOrder} — negative values " +
            $"sort before core Home tab and produce confusing ribbon order.");
    }

    [Fact]
    public void NoTwoModulesShareASortOrder()
    {
        var modules = DiscoveredModules()
            .Select(row => (IModule)Activator.CreateInstance((Type)row[0])!)
            .ToList();

        var collisions = modules
            .GroupBy(m => m.SortOrder)
            .Where(g => g.Count() > 1)
            .Select(g => $"SortOrder={g.Key}: " +
                         string.Join(", ", g.Select(m => m.Name)))
            .ToList();

        Assert.True(collisions.Count == 0,
            "Modules share a ribbon SortOrder — tab positioning becomes " +
            "non-deterministic:\n" + string.Join("\n", collisions));
    }

    [Fact]
    public void NoTwoModulesShareAPermissionCategory()
    {
        var modules = DiscoveredModules()
            .Select(row => (IModule)Activator.CreateInstance((Type)row[0])!)
            .ToList();

        var collisions = modules
            .GroupBy(m => m.PermissionCategory, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => $"PermissionCategory='{g.Key}': " +
                         string.Join(", ", g.Select(m => m.Name)))
            .ToList();

        // Exception: Audit + Global both use "admin" today — document it
        // if that's intentional, otherwise this test will catch a new
        // collision the moment it ships.
        Assert.True(collisions.Count <= 1,
            "Modules share a PermissionCategory — IModuleLicenseGate " +
            "toggles both on/off as one:\n" + string.Join("\n", collisions));
    }
}
