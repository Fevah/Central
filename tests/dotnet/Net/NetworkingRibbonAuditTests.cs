using Central.Engine.Net.Ribbons;
using Central.Engine.Shell;

namespace Central.Tests.Net;

/// <summary>
/// Chunk-B ribbon audit — invokes every button registered by
/// <see cref="NetworkingRibbonRegistrar.BuildRibbon"/>, verifies the
/// expected <see cref="NavigateToPanelMessage"/> or
/// <see cref="RefreshPanelMessage"/> is published through
/// <see cref="PanelMessageBus"/>.
///
/// Any new button added to the registrar without a wired handler
/// (i.e. an <c>() =&gt; { }</c> placeholder) will fail
/// <see cref="EveryActionButton_PublishesAMessage"/> — the audit's
/// safety net against placeholder lambdas landing in a commit, as
/// required by the per-phase checklist in
/// <c>docs/NETWORKING_BUILDOUT_PLAN.md</c>.
/// </summary>
[Collection("RibbonAudit")]
public class NetworkingRibbonAuditTests
{
    [Fact]
    public void BuildsOneNetworkingPage()
    {
        var rb = new RibbonBuilder();
        NetworkingRibbonRegistrar.BuildRibbon(rb, sortOrder: 20);

        Assert.Single(rb.Pages);
        var page = rb.Pages[0];
        Assert.Equal("Networking", page.Header);
        Assert.Equal(20, page.SortOrder);
    }

    [Fact]
    public void HasNineFunctionalGroupsPlusPanels()
    {
        // Spec: Devices, Switches, Links, Routing, VLANs, Servers,
        // Governance, Validation, Panels. Validation group added in
        // Phase 9a — surfaces named-rule execution (run-all / run-selected
        // / toggle / refresh) against the Rust networking-engine.
        var rb = new RibbonBuilder();
        NetworkingRibbonRegistrar.BuildRibbon(rb, 20);
        var groups = rb.Pages[0].Groups.Select(g => g.Header).ToList();
        Assert.Equal(
            new[] { "Devices", "Switches", "Links", "Routing", "VLANs",
                    "Servers", "Governance", "Validation", "Panels" },
            groups);
    }

    [Fact]
    public void EveryActionButton_PublishesAMessage()
    {
        var rb = new RibbonBuilder();
        NetworkingRibbonRegistrar.BuildRibbon(rb, 20);

        // Collect every RibbonButtonRegistration across all non-Panels
        // groups. Panels group contains CheckButtons which are a
        // separate pattern (handled below).
        var buttons = rb.Pages[0].Groups
            .Where(g => g.Header != "Panels")
            .SelectMany(g => g.Buttons)
            .ToList();

        Assert.NotEmpty(buttons);

        foreach (var btn in buttons)
        {
            object? captured = null;
            using var sub1 = PanelMessageBus.Subscribe<NavigateToPanelMessage>(m => captured ??= m);
            using var sub2 = PanelMessageBus.Subscribe<RefreshPanelMessage>(m => captured ??= m);
            btn.OnClick();

            Assert.NotNull(btn.OnClick);
            Assert.True(captured is NavigateToPanelMessage or RefreshPanelMessage,
                $"Button '{btn.Content}' clicked but no NavigateToPanelMessage / " +
                $"RefreshPanelMessage was published — placeholder lambda landed.");
        }
    }

    [Theory]
    [InlineData("Devices", "New Device",       "devices",  "action:new")]
    [InlineData("Devices", "Delete Device",    "devices",  "action:delete")]
    [InlineData("Devices", "Export",           "devices",  "action:export")]
    [InlineData("Switches", "New Switch",      "switches", "action:new")]
    [InlineData("Switches", "Edit Switch",     "switches", "action:edit")]
    [InlineData("Switches", "Delete Switch",   "switches", "action:delete")]
    [InlineData("Switches", "Ping All",        "switches", "action:pingAll")]
    [InlineData("Switches", "Ping Selected",   "switches", "action:pingSelected")]
    [InlineData("Switches", "Sync Config",     "switches", "action:syncConfig")]
    [InlineData("Links",    "New Link",        "links",    "action:new")]
    [InlineData("Links",    "Delete Link",     "links",    "action:delete")]
    [InlineData("Links",    "Build Config",    "links",    "action:build")]
    [InlineData("Routing",  "Sync BGP",        "bgp",      "action:syncSelected")]
    [InlineData("Routing",  "Sync All BGP",    "bgp",      "action:syncAll")]
    [InlineData("Servers",  "New Server",      "servers",  "action:new")]
    [InlineData("Servers",  "Edit Server",     "servers",  "action:edit")]
    [InlineData("Servers",  "Delete Server",   "servers",  "action:delete")]
    [InlineData("Servers",  "Ping NICs",       "servers",  "action:pingNics")]
    [InlineData("Governance", "New Change Set",   "changesets", "action:new")]
    [InlineData("Governance", "Add Item",         "changesets", "action:addItem")]
    [InlineData("Governance", "Submit",           "changesets", "action:submit")]
    [InlineData("Governance", "Approve / Reject", "changesets", "action:decide")]
    [InlineData("Governance", "Apply",            "changesets", "action:apply")]
    [InlineData("Governance", "Rollback",         "changesets", "action:rollback")]
    [InlineData("Governance", "Cancel",           "changesets", "action:cancel")]
    [InlineData("Governance", "Details",          "changesets", "action:details")]
    [InlineData("Governance", "Rename Device",    "changesets", "action:renameDevice")]
    [InlineData("Validation", "Run All",          "validation", "action:runAll")]
    [InlineData("Validation", "Run Selected",     "validation", "action:runSelected")]
    [InlineData("Validation", "Toggle Rule",      "validation", "action:toggleRule")]
    public void NavigateButton_PublishesCorrectTargetAndAction(
        string groupHeader, string buttonContent, string expectedPanel, string expectedAction)
    {
        var rb = new RibbonBuilder();
        NetworkingRibbonRegistrar.BuildRibbon(rb, 20);
        var btn = FindButton(rb, groupHeader, buttonContent);

        NavigateToPanelMessage? captured = null;
        using var sub = PanelMessageBus.Subscribe<NavigateToPanelMessage>(m => captured = m);
        btn.OnClick();

        Assert.NotNull(captured);
        Assert.Equal(expectedPanel, captured!.TargetPanel);
        Assert.Equal(expectedAction, captured.SelectItem);
    }

    [Fact]
    public void RefreshButtons_PublishRefreshPanelMessage()
    {
        var rb = new RibbonBuilder();
        NetworkingRibbonRegistrar.BuildRibbon(rb, 20);

        // Refresh + Refresh VLANs are the two RefreshPanelMessage
        // emitters — everything else uses NavigateToPanelMessage.
        foreach (var (group, button, panel) in new[]
        {
            ("Devices", "Refresh",       "devices"),
            ("VLANs",   "Refresh VLANs", "vlans"),
            ("Servers", "Refresh",       "servers"),
        })
        {
            RefreshPanelMessage? captured = null;
            using var sub = PanelMessageBus.Subscribe<RefreshPanelMessage>(m => captured = m);
            FindButton(rb, group, button).OnClick();
            Assert.NotNull(captured);
            Assert.Equal(panel, captured!.TargetPanel);
        }
    }

    [Theory]
    [InlineData(true,  "action:showDefault:true")]
    [InlineData(false, "action:showDefault:false")]
    public void VlanToggleButton_SerialisesBoolInActionPayload(bool state, string expectedAction)
    {
        var rb = new RibbonBuilder();
        NetworkingRibbonRegistrar.BuildRibbon(rb, 20);

        var toggle = rb.Pages[0].Groups
            .First(g => g.Header == "VLANs")
            .ToggleButtons
            .Single(t => t.Content == "Show Default VLAN");

        NavigateToPanelMessage? captured = null;
        using var sub = PanelMessageBus.Subscribe<NavigateToPanelMessage>(m => captured = m);
        toggle.OnToggle(state);

        Assert.NotNull(captured);
        Assert.Equal("vlans", captured!.TargetPanel);
        Assert.Equal(expectedAction, captured.SelectItem);
    }

    [Fact]
    public void PanelsGroup_UsesCheckButtonsOnly_NoActionLambdas()
    {
        // Check buttons are bound to DockController panels via
        // IsChecked binding in the shell; they don't have OnClick
        // lambdas of their own. Confirm the Panels group is wholly
        // CheckButtons — any button smuggled in here would bypass
        // the action-message audit above.
        var rb = new RibbonBuilder();
        NetworkingRibbonRegistrar.BuildRibbon(rb, 20);
        var panels = rb.Pages[0].Groups.Single(g => g.Header == "Panels");
        Assert.Empty(panels.Buttons);
        Assert.NotEmpty(panels.CheckButtons);
        // Every CheckButton names a real panel id (non-empty).
        Assert.All(panels.CheckButtons, cb =>
            Assert.False(string.IsNullOrWhiteSpace(cb.PanelId), $"CheckButton '{cb.Content}' has empty PanelId"));
    }

    [Fact]
    public void EveryActionButton_HasAPermissionCode()
    {
        // Ribbons inherit visibility from the per-group permission
        // check; every action button should declare which permission
        // gates it. Empty permissions would leak the action to every
        // logged-in user.
        var rb = new RibbonBuilder();
        NetworkingRibbonRegistrar.BuildRibbon(rb, 20);
        var buttons = rb.Pages[0].Groups
            .Where(g => g.Header != "Panels")
            .SelectMany(g => g.Buttons)
            .ToList();
        Assert.All(buttons, b =>
            Assert.False(string.IsNullOrWhiteSpace(b.Permission),
                $"Button '{b.Content}' has no Permission — admin-gated actions can't be open."));
    }

    private static RibbonButtonRegistration FindButton(RibbonBuilder rb, string group, string content)
    {
        var grp = rb.Pages[0].Groups.Single(g => g.Header == group);
        return grp.Buttons.Single(b => b.Content == content);
    }
}
