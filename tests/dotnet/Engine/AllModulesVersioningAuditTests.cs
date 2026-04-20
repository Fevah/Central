using Central.Engine.Modules;

namespace Central.Tests.Engine;

/// <summary>
/// Engine-level audit of the <see cref="IModule.Version"/> +
/// <see cref="IModule.EngineContractVersion"/> contract added in
/// Phase 1 of the module-update system (see
/// <c>docs/MODULE_UPDATE_SYSTEM.md</c>).
///
/// What this catches:
/// 1. A module somehow returning null / empty Version (the default
///    impl's final fallback is "0.0.0", but a module that explicitly
///    overrides could violate this).
/// 2. A module advertising <see cref="IModule.EngineContractVersion"/>
///    greater than <see cref="EngineContract.CurrentVersion"/> — means
///    the module was compiled against a newer engine than the host
///    knows about + cannot be loaded safely.
/// 3. A module advertising <c>EngineContractVersion &lt; 1</c> —
///    accidental zero / negative value breaks the load check.
/// </summary>
[Collection("RibbonAudit")]
public class AllModulesVersioningAuditTests
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
    public void ModuleVersionIsNonEmpty(Type moduleType)
    {
        var m = (IModule)Activator.CreateInstance(moduleType)!;
        Assert.False(string.IsNullOrWhiteSpace(m.Version),
            $"{moduleType.Name}.Version is empty — the default " +
            $"implementation's fallback chain ('informational' → " +
            $"'assembly' → '0.0.0') should never return empty.");
    }

    [Theory]
    [MemberData(nameof(DiscoveredModules))]
    public void ModuleEngineContractIsPositive(Type moduleType)
    {
        var m = (IModule)Activator.CreateInstance(moduleType)!;
        Assert.True(m.EngineContractVersion >= 1,
            $"{moduleType.Name}.EngineContractVersion={m.EngineContractVersion} " +
            $"— values < 1 break the host's load-time compatibility check.");
    }

    [Theory]
    [MemberData(nameof(DiscoveredModules))]
    public void ModuleEngineContractIsNotAheadOfHost(Type moduleType)
    {
        var m = (IModule)Activator.CreateInstance(moduleType)!;
        Assert.True(m.EngineContractVersion <= EngineContract.CurrentVersion,
            $"{moduleType.Name}.EngineContractVersion={m.EngineContractVersion} " +
            $"but host's EngineContract.CurrentVersion={EngineContract.CurrentVersion}. " +
            $"A module compiled against a newer engine than the host " +
            $"cannot be loaded safely — either bump EngineContract.CurrentVersion " +
            $"(and everything that depends on the bump) or back the module out.");
    }
}
