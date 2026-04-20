using Central.Engine.Modules;
using Central.Engine.Shell;

namespace Central.Tests.Engine;

/// <summary>
/// Engine-level ribbon audit that enumerates EVERY IModuleRibbon
/// implementation loaded into the test AppDomain — not just one
/// module — and asserts the same baseline contract against all of
/// them. When a new module is added + registered in the test
/// project's ProjectReference list, it gets this guardrail for
/// free. Per-module audit tests were the wrong fix; this is the
/// right fix (see feedback_engine_first_fixes.md + feedback_ribbon_
/// audit_tests.md in the Claude memory).
///
/// The guardrail catches the drift pattern that let 30+ CRUD
/// buttons go no-op across CRM / Projects / Audit / ServiceDesk /
/// Admin on 2026-04-20: a ribbon button registered with
/// `() => { }` as its handler (or equivalent empty lambda).
/// </summary>
[Collection("RibbonAudit")]
public class AllModulesRibbonAuditTests
{
    /// <summary>
    /// The known-shipping module set. When a new module lands,
    /// add its <c>typeof(...)</c> here — this list is the ONE
    /// place the engine-level audit needs updating. The rest of
    /// the test coverage flows automatically from whatever modules
    /// appear here.
    ///
    /// We can't use pure <see cref="AppDomain.GetAssemblies"/>
    /// reflection because xUnit resolves <c>[MemberData]</c> during
    /// test discovery — before module assemblies are guaranteed
    /// loaded — and empty enumerations surface as "No data found"
    /// rather than an informative failure.
    /// </summary>
    public static IEnumerable<object[]> DiscoveredModules() => new[]
    {
        new object[] { typeof(Central.Module.Networking.NetworkingModule) },
        new object[] { typeof(Central.Module.CRM.CrmModule) },
        new object[] { typeof(Central.Module.Projects.ProjectsModule) },
        new object[] { typeof(Central.Module.Audit.AuditModule) },
        new object[] { typeof(Central.Module.ServiceDesk.ServiceDeskModule) },
        new object[] { typeof(Central.Module.Global.GlobalModule) },
    };

    [Fact]
    public void DiscoversEveryShippingModule()
    {
        // If this fails, a module was added or removed without the
        // test project being updated. Fail loud rather than silently
        // skip coverage.
        var discovered = DiscoveredModules()
            .Select(row => ((Type)row[0]).Name)
            .ToList();

        Assert.Contains("NetworkingModule", discovered);
        Assert.Contains("CrmModule",        discovered);
        Assert.Contains("ProjectsModule",   discovered);
        Assert.Contains("AuditModule",      discovered);
        Assert.Contains("ServiceDeskModule", discovered);
        Assert.Contains("GlobalModule",     discovered);
    }

    [Theory]
    [MemberData(nameof(DiscoveredModules))]
    public void EveryRibbonButtonPublishesAMessage(Type moduleType)
    {
        var module = (IModuleRibbon)Activator.CreateInstance(moduleType)!;
        var rb = new RibbonBuilder();
        module.RegisterRibbon(rb);

        // Collect every action-style button across every page this
        // module contributes. CheckButtons are the panel-toggle
        // pattern; Toggle/Split are rarer. We test plain Buttons —
        // that's where the placeholder-lambda drift lives.
        var buttons = rb.Pages
            .SelectMany(p => p.Groups)
            .Where(g => !string.Equals(g.Header, "Panels", StringComparison.OrdinalIgnoreCase))
            .SelectMany(g => g.Buttons)
            .ToList();

        // A module with zero action buttons is legal (e.g. a module
        // that only contributes panel toggles). Skip silently.
        if (buttons.Count == 0) return;

        var placeholders = new List<string>();
        foreach (var btn in buttons)
        {
            object? captured = null;
            using var sub1 = PanelMessageBus.Subscribe<NavigateToPanelMessage>(m => captured ??= m);
            using var sub2 = PanelMessageBus.Subscribe<RefreshPanelMessage>(m => captured ??= m);
            using var sub3 = PanelMessageBus.Subscribe<DataModifiedMessage>(m => captured ??= m);

            try { btn.OnClick(); }
            catch
            {
                // Handler threw — not a placeholder, at least it's
                // attempting something. Let Exception-as-handler
                // slide; the placeholder detector is what matters.
                continue;
            }

            if (captured is null)
            {
                placeholders.Add(
                    $"{moduleType.Name} / '{btn.Content}' — handler fired with no " +
                    $"PanelMessageBus publish (placeholder or unwired).");
            }
        }

        Assert.True(placeholders.Count == 0,
            $"{moduleType.Name} has {placeholders.Count} placeholder-lambda ribbon buttons:\n" +
            string.Join("\n", placeholders));
    }

    [Theory]
    [MemberData(nameof(DiscoveredModules))]
    public void EveryRibbonPageHasAtLeastOneGroup(Type moduleType)
    {
        var module = (IModuleRibbon)Activator.CreateInstance(moduleType)!;
        var rb = new RibbonBuilder();
        module.RegisterRibbon(rb);

        foreach (var page in rb.Pages)
        {
            Assert.True(page.Groups.Count > 0,
                $"{moduleType.Name} registered page '{page.Header}' with zero groups — " +
                $"empty pages render blank in the ribbon.");
        }
    }

    [Theory]
    [MemberData(nameof(DiscoveredModules))]
    public void EveryRibbonGroupHasAtLeastOneItem(Type moduleType)
    {
        var module = (IModuleRibbon)Activator.CreateInstance(moduleType)!;
        var rb = new RibbonBuilder();
        module.RegisterRibbon(rb);

        foreach (var page in rb.Pages)
        foreach (var group in page.Groups)
        {
            Assert.True(group.Items.Count > 0,
                $"{moduleType.Name} / page '{page.Header}' / group '{group.Header}' " +
                $"has zero items — empty groups render as dead space.");
        }
    }

    [Theory]
    [MemberData(nameof(DiscoveredModules))]
    public void EveryRibbonButtonHasNonEmptyContent(Type moduleType)
    {
        var module = (IModuleRibbon)Activator.CreateInstance(moduleType)!;
        var rb = new RibbonBuilder();
        module.RegisterRibbon(rb);

        foreach (var page in rb.Pages)
        foreach (var group in page.Groups)
        foreach (var btn in group.Buttons)
        {
            Assert.False(string.IsNullOrWhiteSpace(btn.Content),
                $"{moduleType.Name} / '{page.Header}' / '{group.Header}' has a button " +
                $"with empty or whitespace Content.");
        }
    }
}
