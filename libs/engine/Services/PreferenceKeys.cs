namespace Central.Engine.Services;

/// <summary>
/// Constants for user_settings preference keys.
/// Eliminates scattered magic strings throughout the codebase.
/// </summary>
public static class PreferenceKeys
{
    // ── Preferences ─────────────────────────────────────────────────────
    public const string HideReserved     = "pref.hide_reserved";
    public const string SiteSelections   = "pref.site_selections";
    public const string DevicesSearch    = "pref.devices_search";
    public const string ActiveRibbonTab  = "pref.active_ribbon_tab";
    public const string ActiveDocTab     = "pref.active_doc_tab";
    public const string GridFilters      = "pref.grid_filters";
    public const string ScanEnabled      = "pref.scan_enabled";
    public const string ScanInterval     = "pref.scan_interval";
    public const string Theme            = "pref.theme";
    // DetailTabOrder is in Layouts section below

    // ── Layouts ─────────────────────────────────────────────────────────
    public const string PanelStates      = "layout.panel_states";
    public const string DockLayout       = "layout.dock";
    public const string DevicesGrid      = "layout.devices_grid";
    public const string SwitchGrid       = "layout.switch_grid";
    public const string AdminGrid        = "layout.admin_grid";
    public const string UsersGrid        = "layout.users_grid";
    public const string RolesGrid        = "layout.roles_grid";
    public const string SettingsGrid     = "layout.settings_grid";
    public const string MasterGrid       = "layout.master_grid";
    public const string P2PGrid          = "layout.p2p_grid";
    public const string VlansGrid        = "layout.vlans_grid";
    public const string MlagGrid         = "layout.mlag_grid";
    public const string MstpGrid         = "layout.mstp_grid";
    public const string ServerAsGrid     = "layout.serveras_grid";
    public const string IpRangesGrid     = "layout.ipranges_grid";
    public const string ServersGrid      = "layout.servers_grid";
    public const string InterfacesGrid   = "layout.interfaces_grid";
    public const string SwitchesGrid    = "layout.switches_grid";
    public const string AsnGrid         = "layout.asn_grid";
    public const string B2BGrid         = "layout.b2b_grid";
    public const string FWGrid          = "layout.fw_grid";
    public const string DetailTabOrder  = "layout.detail_tab_order";
}
