using Central.Engine.Widgets;

namespace Central.Tests.Engine;

/// <summary>
/// Engine-level audit of <see cref="IDashboardContribution"/> instances
/// that modules register on startup. The dashboard renders whatever
/// contributes, so a contribution with a blank SectionTitle shows an
/// empty header strip, and two contributions sharing a SortOrder
/// flip-flop order between runs (non-deterministic ThenBy on title only
/// happens when SortOrder ties — still a smell worth flagging).
///
/// The contribution list is the live registry, not a hardcoded typeof()
/// map: each module's constructor self-registers during its ctor, so
/// simply new-ing the module list populates <see cref="DashboardContributionRegistry.All"/>
/// naturally. When a new module starts contributing, it shows up for free.
/// </summary>
[Collection("RibbonAudit")]
public class AllModulesDashboardContributionAuditTests
{
    private static IReadOnlyList<IDashboardContribution> LoadContributions()
    {
        // Construct every module — ctors self-register via
        // DashboardContributionRegistry.Register. Registry is idempotent
        // per concrete type, so running this alongside the ribbon audit
        // (which also constructs the modules) is safe.
        _ = new Central.Module.Networking.NetworkingModule();
        _ = new Central.Module.CRM.CrmModule();
        _ = new Central.Module.Projects.ProjectsModule();
        _ = new Central.Module.Audit.AuditModule();
        _ = new Central.Module.ServiceDesk.ServiceDeskModule();
        _ = new Central.Module.Global.GlobalModule();

        return DashboardContributionRegistry.All;
    }

    [Fact]
    public void RegistryIsNotEmpty()
    {
        var items = LoadContributions();
        Assert.NotEmpty(items);
    }

    [Fact]
    public void EveryContributionHasNonEmptySectionTitle()
    {
        var items = LoadContributions();
        var offenders = items
            .Where(c => string.IsNullOrWhiteSpace(c.SectionTitle))
            .Select(c => c.GetType().Name)
            .ToList();

        Assert.True(offenders.Count == 0,
            "Dashboard contributions with blank SectionTitle render an " +
            "empty header strip:\n" + string.Join("\n", offenders));
    }

    [Fact]
    public void EveryContributionHasNonNegativeSortOrder()
    {
        var items = LoadContributions();
        var offenders = items
            .Where(c => c.SortOrder < 0)
            .Select(c => $"{c.GetType().Name} SortOrder={c.SortOrder}")
            .ToList();

        Assert.True(offenders.Count == 0,
            "Dashboard contributions with negative SortOrder sort before " +
            "the PlatformHealth header (SortOrder=0):\n" +
            string.Join("\n", offenders));
    }

    [Fact]
    public void NoTwoContributionsShareASortOrder()
    {
        var items = LoadContributions();
        var collisions = items
            .GroupBy(c => c.SortOrder)
            .Where(g => g.Count() > 1)
            .Select(g => $"SortOrder={g.Key}: " +
                         string.Join(", ", g.Select(c => c.GetType().Name)))
            .ToList();

        Assert.True(collisions.Count == 0,
            "Dashboard contributions share a SortOrder — section order " +
            "becomes non-deterministic across restarts:\n" +
            string.Join("\n", collisions));
    }
}
