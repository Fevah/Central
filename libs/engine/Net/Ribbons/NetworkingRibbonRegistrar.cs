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
    public const string PanelChangeSets = "changesets";
    public const string PanelValidation = "validation";
    public const string PanelAudit      = "audit";
    public const string PanelLocks      = "locks";

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

            // ── Governance (Phase 8) ─────────────────────────────────────
            // Change Sets are the policy gate for any mutation that wants
            // approval. Creation / submission / decisions publish through
            // the same PanelMessageBus as the rest of the ribbon so the
            // panel picks them up.
            page.AddGroup("Governance", group =>
            {
                group.AddButton("New Change Set",  P.ChangeSetsWrite,   "AddItem_16x16",
                    () => PanelMessageBus.Publish(new NavigateToPanelMessage(PanelChangeSets, "action:new")));
                group.AddButton("Add Item",        P.ChangeSetsWrite,   "Add_16x16",
                    () => PanelMessageBus.Publish(new NavigateToPanelMessage(PanelChangeSets, "action:addItem")));
                group.AddButton("Rename Device",   P.ChangeSetsWrite,   "Rename_16x16",
                    () => PanelMessageBus.Publish(new NavigateToPanelMessage(PanelChangeSets, "action:renameDevice")));
                group.AddButton("Update Device",   P.ChangeSetsWrite,   "EditItem_16x16",
                    () => PanelMessageBus.Publish(new NavigateToPanelMessage(PanelChangeSets, "action:updateDevice")));
                group.AddButton("Delete Device",   P.ChangeSetsWrite,   "Delete_16x16",
                    () => PanelMessageBus.Publish(new NavigateToPanelMessage(PanelChangeSets, "action:deleteDevice")));
                group.AddButton("Create VLAN",     P.ChangeSetsWrite,   "NewItem_16x16",
                    () => PanelMessageBus.Publish(new NavigateToPanelMessage(PanelChangeSets, "action:createVlan")));
                group.AddButton("Create ASN",      P.ChangeSetsWrite,   "NewItem_16x16",
                    () => PanelMessageBus.Publish(new NavigateToPanelMessage(PanelChangeSets, "action:createAsn")));
                group.AddButton("Create MLAG",     P.ChangeSetsWrite,   "NewItem_16x16",
                    () => PanelMessageBus.Publish(new NavigateToPanelMessage(PanelChangeSets, "action:createMlag")));
                group.AddButton("Carve Subnet",    P.ChangeSetsWrite,   "NewItem_16x16",
                    () => PanelMessageBus.Publish(new NavigateToPanelMessage(PanelChangeSets, "action:createSubnet")));
                group.AddButton("Submit",          P.ChangeSetsWrite,   "Submit_16x16",
                    () => PanelMessageBus.Publish(new NavigateToPanelMessage(PanelChangeSets, "action:submit")));
                group.AddButton("Approve / Reject", P.ChangeSetsApprove, "Apply_16x16",
                    () => PanelMessageBus.Publish(new NavigateToPanelMessage(PanelChangeSets, "action:decide")));
                group.AddButton("Apply",           P.ChangeSetsApply,   "ApplyStyle_16x16",
                    () => PanelMessageBus.Publish(new NavigateToPanelMessage(PanelChangeSets, "action:apply")));
                group.AddButton("Rollback",        P.ChangeSetsRollback, "Undo_16x16",
                    () => PanelMessageBus.Publish(new NavigateToPanelMessage(PanelChangeSets, "action:rollback")));
                group.AddButton("Cancel",          P.ChangeSetsWrite,   "Close_16x16",
                    () => PanelMessageBus.Publish(new NavigateToPanelMessage(PanelChangeSets, "action:cancel")));
                group.AddButton("Details",         P.ChangeSetsRead,    "Properties_16x16",
                    () => PanelMessageBus.Publish(new NavigateToPanelMessage(PanelChangeSets, "action:details")));
                group.AddButton("Refresh",         P.ChangeSetsRead,    "Refresh_16x16",
                    () => PanelMessageBus.Publish(new RefreshPanelMessage(PanelChangeSets)));
            });

            // ── Validation (Phase 9a) ────────────────────────────────────
            // Run the named-rule set against the tenant's net.* data.
            // Rule catalog lives in code (services/networking-engine/src/
            // validation.rs) so buttons here are execution-side only —
            // rule edits are a per-tenant config action toggled from
            // inside the panel.
            page.AddGroup("Validation", group =>
            {
                group.AddButton("Run All",          P.ValidationRun,     "Play_16x16",
                    () => PanelMessageBus.Publish(new NavigateToPanelMessage(PanelValidation, "action:runAll")));
                group.AddButton("Run Selected",     P.ValidationRun,     "Arrow_Right_16x16",
                    () => PanelMessageBus.Publish(new NavigateToPanelMessage(PanelValidation, "action:runSelected")));
                group.AddButton("Edit Rule",        P.ValidationConfigure, "EditItem_16x16",
                    () => PanelMessageBus.Publish(new NavigateToPanelMessage(PanelValidation, "action:editRule")));
                group.AddButton("Export Violations", P.ValidationRead,  "ExportFile_16x16",
                    () => PanelMessageBus.Publish(new NavigateToPanelMessage(PanelValidation, "action:exportViolations")));
                group.AddButton("Refresh",          P.ValidationRead,    "Refresh_16x16",
                    () => PanelMessageBus.Publish(new RefreshPanelMessage(PanelValidation)));
            });

            // ── Locks (Phase 8f) ─────────────────────────────────────────
            // Lifecycle lock state management — the engine's trigger-backed
            // HardLock / Immutable enforcement is meaningless without a
            // way for admins to apply it. Actions are Change Lock State
            // (pick from dropdown) and Clear Lock (set back to Open).
            page.AddGroup("Locks", group =>
            {
                group.AddButton("Change State",    P.ChangeSetsWrite,   "EditItem_16x16",
                    () => PanelMessageBus.Publish(new NavigateToPanelMessage(PanelLocks, "action:changeState")));
                group.AddButton("Clear Lock",      P.ChangeSetsWrite,   "Cancel_16x16",
                    () => PanelMessageBus.Publish(new NavigateToPanelMessage(PanelLocks, "action:clearLock")));
                group.AddButton("Refresh",         P.ChangeSetsRead,    "Refresh_16x16",
                    () => PanelMessageBus.Publish(new RefreshPanelMessage(PanelLocks)));
            });

            // ── Audit (Phase 9) ──────────────────────────────────────────
            // Tenant-wide audit browser. Filters + verify-chain + export.
            page.AddGroup("Audit", group =>
            {
                group.AddButton("Run Query",       P.AuditRead,    "FindData_16x16",
                    () => PanelMessageBus.Publish(new NavigateToPanelMessage(PanelAudit, "action:runQuery")));
                group.AddButton("Verify Chain",    P.AuditVerify,  "CheckAll_16x16",
                    () => PanelMessageBus.Publish(new NavigateToPanelMessage(PanelAudit, "action:verifyChain")));
                group.AddButton("Export CSV",      P.AuditRead,    "ExportFile_16x16",
                    () => PanelMessageBus.Publish(new NavigateToPanelMessage(PanelAudit, "action:exportCsv")));
                group.AddButton("Refresh",         P.AuditRead,    "Refresh_16x16",
                    () => PanelMessageBus.Publish(new RefreshPanelMessage(PanelAudit)));
            });

            page.AddGroup("Panels", group =>
            {
                group.AddCheckButton("Hierarchy",      panelId: "HierarchyPanel");
                group.AddCheckButton("Pools",          panelId: "PoolsPanel");
                group.AddCheckButton("Servers",        panelId: "ServersPanel");
                group.AddCheckButton("Change Sets",    panelId: "ChangeSetsPanel");
                group.AddCheckButton("Validation",     panelId: "ValidationPanel");
                group.AddCheckButton("Audit",          panelId: "AuditPanel");
                group.AddCheckButton("Locks",          panelId: "LocksPanel");
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
