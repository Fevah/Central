using Central.Engine.Auth;
using Central.Engine.Modules;
using Central.Engine.Net.Ribbons;
using Central.Engine.Shell;
using Central.Engine.Widgets;
using Central.Module.Networking.Dashboards;

namespace Central.Module.Networking;

/// <summary>
/// Networking module — one self-contained unit covering every network
/// concept: IPAM (devices, ASNs, IP ranges, MLAG, MSTP, servers),
/// switches, routing (BGP), VLANs, and links (P2P / B2B / FW). Disabling
/// this module for a tenant removes every networking ribbon group,
/// panel, and command in one switch.
///
/// Merged into one assembly on 2026-04-17. Internal subfolders keep the
/// code organised (Devices/, Switches/, Routing/, Vlans/, Links/,
/// Dashboards/) but the assembly boundary is singular — there is no
/// scenario where "networking minus devices" makes sense.
/// </summary>
public class NetworkingModule : IModule, IModuleRibbon, IModulePanels
{
    public string Name => "Networking";

    // A tenant with *any* networking permission sees the tab; per-group
    // RequirePermission calls below control what's visible inside.
    public string PermissionCategory => "switches";

    public int SortOrder => 20;

    public NetworkingModule()
    {
        // Two contributions -> two sections on the landing dashboard:
        // "Devices" (IPAM counts) and "Networking" (switch/VLAN/BGP counts).
        // Both register from this module, so disabling Networking removes
        // both sections in one step.
        DashboardContributionRegistry.Register(new DevicesDashboardContribution());
        DashboardContributionRegistry.Register(new NetworkingDashboardContribution());
    }

    /// <summary>
    /// Ribbon registration delegates to
    /// <see cref="NetworkingRibbonRegistrar.BuildRibbon"/> so the button
    /// list stays in a net10.0 assembly the test project can reference.
    /// See <c>NetworkingRibbonAuditTests</c> for the audit that asserts
    /// every button here publishes a message through
    /// <see cref="PanelMessageBus"/>.
    /// </summary>
    public void RegisterRibbon(IRibbonBuilder ribbon)
        => NetworkingRibbonRegistrar.BuildRibbon(ribbon, SortOrder);

    public void RegisterPanels(IPanelBuilder panels)
    {
        // Panels wired via apps/desktop/MainWindow.xaml (XAML-defined DockLayoutManager).
    }
}
