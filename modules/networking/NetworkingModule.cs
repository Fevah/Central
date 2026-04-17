using Central.Engine.Auth;
using Central.Engine.Modules;

namespace Central.Module.Networking;

/// <summary>
/// Networking module — one self-contained unit covering switches, routing,
/// VLANs, and links. Disabling this module for a tenant removes every
/// networking ribbon group, panel, and command in one switch.
///
/// Merged from the former separate modules (switches, routing, vlans,
/// links) on 2026-04-17 for tenant-level enable/disable cleanness. Each
/// former module's code lives in its own subfolder below
/// (Switches/, Routing/, Vlans/, Links/) so it's still organised, just
/// not independently deployable.
/// </summary>
public class NetworkingModule : IModule, IModuleRibbon, IModulePanels
{
    public string Name => "Networking";

    // A tenant with *any* of these permissions sees the Networking tab.
    // The per-sub-area RequirePermission calls below control what's
    // visible within it.
    public string PermissionCategory => "switches";

    public int SortOrder => 20;

    public void RegisterRibbon(IRibbonBuilder ribbon)
    {
        ribbon.AddPage("Networking", SortOrder, page =>
        {
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

            // ── Panels (all networking panels in one group for quick toggle) ──
            page.AddGroup("Panels", group =>
            {
                group.AddCheckButton("Switches", panelId: "SwitchesPanel");
                group.AddCheckButton("Details",  panelId: "SwitchDetailPanel");
                group.AddCheckButton("P2P",      panelId: "P2PPanel");
                group.AddCheckButton("B2B",      panelId: "B2BPanel");
                group.AddCheckButton("FW",       panelId: "FWPanel");
                group.AddCheckButton("BGP",      panelId: "BgpPanel");
                group.AddCheckButton("VLANs",    panelId: "VlanPanel");
            });
        });
    }

    public void RegisterPanels(IPanelBuilder panels)
    {
        // Panels are registered via MainWindow's DockLayoutManager (XAML-defined).
        // When that moves into a pure module-registration model, re-wire here.
    }
}
