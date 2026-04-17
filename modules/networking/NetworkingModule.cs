using Central.Engine.Auth;
using Central.Engine.Modules;
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

    public void RegisterRibbon(IRibbonBuilder ribbon)
    {
        ribbon.AddPage("Networking", SortOrder, page =>
        {
            // ── Devices (IPAM) ─────────────────────────────────────────────
            page.AddGroup("Devices", group =>
            {
                group.AddButton("New Device",    P.DevicesWrite,  "AddItem_16x16",
                    () => PanelMessageBus.Publish(new NavigateToPanelMessage("devices", "action:new")));
                group.AddButton("Delete Device", P.DevicesDelete, "Delete_16x16",
                    () => PanelMessageBus.Publish(new NavigateToPanelMessage("devices", "action:delete")));
                group.AddButton("Refresh",       P.DevicesRead,   "Refresh_16x16",
                    () => PanelMessageBus.Publish(new RefreshPanelMessage("devices")));
                group.AddButton("Export",        P.DevicesExport, "ExportFile_16x16",
                    () => PanelMessageBus.Publish(new NavigateToPanelMessage("devices", "action:export")));
            });

            // ── Switches ───────────────────────────────────────────────────
            page.AddGroup("Switches", group =>
            {
                group.AddButton("New Switch",    P.SwitchesWrite,  null, () => { });
                group.AddButton("Edit Switch",   P.SwitchesWrite,  null, () => { });
                group.AddButton("Delete Switch", P.SwitchesDelete, null, () => { });
                group.AddButton("Ping All",      P.SwitchesPing,   null, () => { });
                group.AddButton("Ping Selected", P.SwitchesPing,   null, () => { });
                group.AddButton("Sync Config",   P.SwitchesSync,   null, () => { });
            });

            // ── Links ──────────────────────────────────────────────────────
            page.AddGroup("Links", group =>
            {
                group.AddButton("New Link",     P.LinksWrite,  null, () => { });
                group.AddButton("Delete Link",  P.LinksDelete, null, () => { });
                group.AddButton("Build Config", P.LinksRead,   null, () => { });
            });

            // ── Routing ────────────────────────────────────────────────────
            page.AddGroup("Routing", group =>
            {
                group.AddButton("Sync BGP",     P.BgpSync, null, () => { });
                group.AddButton("Sync All BGP", P.BgpSync, null, () => { });
            });

            // ── VLANs ──────────────────────────────────────────────────────
            page.AddGroup("VLANs", group =>
            {
                group.AddButton("Refresh VLANs", P.VlansRead, null, () => { });
                group.AddToggleButton("Show Default VLAN", P.VlansRead, isOn => { });
            });

            // ── Panels (everything networking exposes as a dockable panel) ──
            page.AddGroup("Panels", group =>
            {
                group.AddCheckButton("Hierarchy",   panelId: "HierarchyPanel");
                group.AddCheckButton("IPAM",        panelId: "DevicesPanel");
                group.AddCheckButton("Device Details", panelId: "DeviceDetailPanel");
                group.AddCheckButton("Switches",    panelId: "SwitchesPanel");
                group.AddCheckButton("Switch Details", panelId: "SwitchDetailPanel");
                group.AddCheckButton("P2P",         panelId: "P2PPanel");
                group.AddCheckButton("B2B",         panelId: "B2BPanel");
                group.AddCheckButton("FW",          panelId: "FWPanel");
                group.AddCheckButton("BGP",         panelId: "BgpPanel");
                group.AddCheckButton("VLANs",       panelId: "VlanPanel");
            });
        });
    }

    public void RegisterPanels(IPanelBuilder panels)
    {
        // Panels wired via apps/desktop/MainWindow.xaml (XAML-defined DockLayoutManager).
    }
}
