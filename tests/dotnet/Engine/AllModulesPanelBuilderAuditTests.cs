using Central.Engine.Modules;
using Central.Engine.Shell;

namespace Central.Tests.Engine;

/// <summary>
/// Engine-level panel-builder audit. Same discovery pattern as
/// <see cref="AllModulesRibbonAuditTests"/>: one class enumerates every
/// <c>IModulePanels</c> in the loaded AppDomain and asserts the same
/// registration-contract invariants against all of them.
///
/// The drift this prevents: a module registering a panel with an empty
/// id (crash on DockLayoutManager.Restore), a blank caption (tab with
/// no label), a missing view type (typeof(object) fallback renders a
/// grey square), or two panels colliding on the same id within the
/// same module (silent overwrite, last-wins).
/// </summary>
[Collection("RibbonAudit")]
public class AllModulesPanelBuilderAuditTests
{
    /// <summary>
    /// Mirror of <see cref="AllModulesRibbonAuditTests.DiscoveredModules"/>
    /// scoped to the ones that implement <c>IModulePanels</c>. When a new
    /// module lands AND registers panels, add its <c>typeof(...)</c> here.
    /// CRM + Global currently don't register panels programmatically
    /// (panels live in MainWindow.xaml), so they're intentionally absent.
    /// </summary>
    public static IEnumerable<object[]> PanelContributingModules() => new[]
    {
        new object[] { typeof(Central.Module.Networking.NetworkingModule) },
        new object[] { typeof(Central.Module.Projects.ProjectsModule) },
        new object[] { typeof(Central.Module.Audit.AuditModule) },
        new object[] { typeof(Central.Module.ServiceDesk.ServiceDeskModule) },
    };

    [Theory]
    [MemberData(nameof(PanelContributingModules))]
    public void EveryRegisteredPanelHasNonEmptyId(Type moduleType)
    {
        var module = (IModulePanels)Activator.CreateInstance(moduleType)!;
        var pb = new PanelBuilder();
        module.RegisterPanels(pb);

        Assert.All(pb.Panels, p =>
            Assert.False(string.IsNullOrWhiteSpace(p.Id),
                $"{moduleType.Name} registered a panel with empty Id " +
                $"(caption='{p.Caption}', view={p.ViewType.Name})."));
    }

    [Theory]
    [MemberData(nameof(PanelContributingModules))]
    public void EveryRegisteredPanelHasNonEmptyCaption(Type moduleType)
    {
        var module = (IModulePanels)Activator.CreateInstance(moduleType)!;
        var pb = new PanelBuilder();
        module.RegisterPanels(pb);

        Assert.All(pb.Panels, p =>
            Assert.False(string.IsNullOrWhiteSpace(p.Caption),
                $"{moduleType.Name} / '{p.Id}' has empty Caption — " +
                $"renders as a tab with no label."));
    }

    [Theory]
    [MemberData(nameof(PanelContributingModules))]
    public void EveryRegisteredPanelHasConcreteViewType(Type moduleType)
    {
        var module = (IModulePanels)Activator.CreateInstance(moduleType)!;
        var pb = new PanelBuilder();
        module.RegisterPanels(pb);

        Assert.All(pb.Panels, p =>
            Assert.True(p.ViewType != typeof(object) && p.ViewType is not null,
                $"{moduleType.Name} / '{p.Id}' registered with " +
                $"ViewType=typeof(object) — will render as empty grey square."));
    }

    [Theory]
    [MemberData(nameof(PanelContributingModules))]
    public void NoDuplicatePanelIdsWithinAModule(Type moduleType)
    {
        var module = (IModulePanels)Activator.CreateInstance(moduleType)!;
        var pb = new PanelBuilder();
        module.RegisterPanels(pb);

        var duplicates = pb.Panels
            .GroupBy(p => p.Id, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => $"id='{g.Key}' appears {g.Count()} times")
            .ToList();

        Assert.True(duplicates.Count == 0,
            $"{moduleType.Name} has duplicate panel ids (silent last-wins " +
            $"overwrite in DockLayoutManager):\n" +
            string.Join("\n", duplicates));
    }
}
