using Central.Engine.Auth;
using Central.Engine.Modules;
using Central.Engine.Shell;

namespace Central.Engine.Net.Ribbons;

/// <summary>
/// Engine-side builder for the Networking module's ribbon surface.
/// Lives here rather than inside the WPF module assembly so it can be
/// called from both <c>Central.Module.Networking.NetworkingModule</c>
/// (WPF) and the test project (net10.0, no WPF) without duplicating
/// the button list.
///
/// Every button's <see cref="RibbonButtonRegistration.OnClick"/>
/// publishes a <see cref="NavigateToPanelMessage"/> or
/// <see cref="RefreshPanelMessage"/> on <see cref="PanelMessageBus"/>.
/// Subscribing panels pick up the action via their own handlers.
///
/// The test <c>NetworkingRibbonAuditTests</c> loads this registrar
/// into a fresh <see cref="RibbonBuilder"/>, invokes each button's
/// <c>OnClick</c> in turn, and asserts the right message was
/// published — any new button added here without a wired handler
/// will fail the audit.
/// </summary>
public static class NetworkingRibbonRegistrar
{
    /// <summary>Target panel id for Devices / IPAM-style routing.</summary>
    public const string PanelDevices  = "devices";
    public const string PanelSwitches = "switches";
    public const string PanelLinks    = "links";
    public const string PanelBgp      = "bgp";
    public const string PanelVlans    = "vlans";
    public const string PanelServers  = "servers";

    public static void BuildRibbon(IRibbonBuilder ribbon, int sortOrder)
    {
        ribbon.AddPage("Networking", sortOrder, page =>
        {
            page.AddGroup("Devices", group =>
            {
                group.AddButton("New Device",    P.DevicesWrite,  "AddItem_16x16",
                    () => PanelMessageBus.Publish(new NavigateToPanelMessage(PanelDevices, "action:new")));
                group.AddButton("Delete Device", P.DevicesDelete, "Delete_16x16",
                    () => PanelMessageBus.Publish(new NavigateToPanelMessage(PanelDevices, "action:delete")));
                group.AddButton("Refresh",       P.DevicesRead,   "Refresh_16x16",
                    () => PanelMessageBus.Publish(new RefreshPanelMessage(PanelDevices)));
                group.AddButton("Export",        P.DevicesExport, "ExportFile_16x16",
                    () => PanelMessageBus.Publish(new NavigateToPanelMessage(PanelDevices, "action:export")));
            });

            page.AddGroup("Switches", group =>
            {
                group.AddButton("New Switch",    P.SwitchesWrite,  "AddItem_16x16",
                    () => PanelMessageBus.Publish(new NavigateToPanelMessage(PanelSwitches, "action:new")));
                group.AddButton("Edit Switch",   P.SwitchesWrite,  "EditItem_16x16",
                    () => PanelMessageBus.Publish(new NavigateToPanelMessage(PanelSwitches, "action:edit")));
                group.AddButton("Delete Switch", P.SwitchesDelete, "Delete_16x16",
                    () => PanelMessageBus.Publish(new NavigateToPanelMessage(PanelSwitches, "action:delete")));
                group.AddButton("Ping All",      P.SwitchesPing,   "Refresh_16x16",
                    () => PanelMessageBus.Publish(new NavigateToPanelMessage(PanelSwitches, "action:pingAll")));
                group.AddButton("Ping Selected", P.SwitchesPing,   "Refresh_16x16",
                    () => PanelMessageBus.Publish(new NavigateToPanelMessage(PanelSwitches, "action:pingSelected")));
                group.AddButton("Sync Config",   P.SwitchesSync,   "Apply_16x16",
                    () => PanelMessageBus.Publish(new NavigateToPanelMessage(PanelSwitches, "action:syncConfig")));
            });

            // Target panel id "links" is the unified Phase-5 bucket.
            // Legacy P2P / B2B / FW panels still listen directly until
            // their grids merge; the new panel will subscribe here when
            // it lands.
            page.AddGroup("Links", group =>
            {
                group.AddButton("New Link",     P.LinksWrite,  "AddItem_16x16",
                    () => PanelMessageBus.Publish(new NavigateToPanelMessage(PanelLinks, "action:new")));
                group.AddButton("Delete Link",  P.LinksDelete, "Delete_16x16",
                    () => PanelMessageBus.Publish(new NavigateToPanelMessage(PanelLinks, "action:delete")));
                group.AddButton("Build Config", P.LinksRead,   "Apply_16x16",
                    () => PanelMessageBus.Publish(new NavigateToPanelMessage(PanelLinks, "action:build")));
            });

            page.AddGroup("Routing", group =>
            {
                group.AddButton("Sync BGP",     P.BgpSync, "Refresh_16x16",
                    () => PanelMessageBus.Publish(new NavigateToPanelMessage(PanelBgp, "action:syncSelected")));
                group.AddButton("Sync All BGP", P.BgpSync, "Refresh_16x16",
                    () => PanelMessageBus.Publish(new NavigateToPanelMessage(PanelBgp, "action:syncAll")));
            });

            page.AddGroup("VLANs", group =>
            {
                group.AddButton("Refresh VLANs", P.VlansRead, "Refresh_16x16",
                    () => PanelMessageBus.Publish(new RefreshPanelMessage(PanelVlans)));
                group.AddToggleButton("Show Default VLAN", P.VlansRead,
                    isOn => PanelMessageBus.Publish(
                        new NavigateToPanelMessage(PanelVlans,
                            $"action:showDefault:{isOn.ToString().ToLowerInvariant()}")));
            });

            // ── Servers (Phase 6c) ──────────────────────────────────────
            // "New Server" kicks off the creation flow which allocates
            // ASN + loopback + 4 NICs per the server_profile — handled
            // on the WPF side once the Servers panel lands (Phase 6f).
            page.AddGroup("Servers", group =>
            {
                group.AddButton("New Server",    P.NetServersWrite,  "AddItem_16x16",
                    () => PanelMessageBus.Publish(new NavigateToPanelMessage(PanelServers, "action:new")));
                group.AddButton("Edit Server",   P.NetServersWrite,  "EditItem_16x16",
                    () => PanelMessageBus.Publish(new NavigateToPanelMessage(PanelServers, "action:edit")));
                group.AddButton("Delete Server", P.NetServersDelete, "Delete_16x16",
                    () => PanelMessageBus.Publish(new NavigateToPanelMessage(PanelServers, "action:delete")));
                group.AddButton("Ping NICs",     P.NetServersWrite,  "Refresh_16x16",
                    () => PanelMessageBus.Publish(new NavigateToPanelMessage(PanelServers, "action:pingNics")));
                group.AddButton("Refresh",       P.NetServersRead,   "Refresh_16x16",
                    () => PanelMessageBus.Publish(new RefreshPanelMessage(PanelServers)));
            });

            page.AddGroup("Panels", group =>
            {
                group.AddCheckButton("Hierarchy",      panelId: "HierarchyPanel");
                group.AddCheckButton("Pools",          panelId: "PoolsPanel");
                group.AddCheckButton("Servers",        panelId: "ServersPanel");
                group.AddCheckButton("IPAM",           panelId: "DevicesPanel");
                group.AddCheckButton("Device Details", panelId: "DeviceDetailPanel");
                group.AddCheckButton("Switches",       panelId: "SwitchesPanel");
                group.AddCheckButton("Switch Details", panelId: "SwitchDetailPanel");
                group.AddCheckButton("P2P",            panelId: "P2PPanel");
                group.AddCheckButton("B2B",            panelId: "B2BPanel");
                group.AddCheckButton("FW",             panelId: "FWPanel");
                group.AddCheckButton("BGP",            panelId: "BgpPanel");
                group.AddCheckButton("VLANs",          panelId: "VlanPanel");
            });
        });
    }
}
