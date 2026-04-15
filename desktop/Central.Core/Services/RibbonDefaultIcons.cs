namespace Central.Core.Services;

/// <summary>
/// Curated catalogue of DX 25.2 icon names to use as the default Glyph for
/// each ribbon page, group, and section in the app — so the ribbon never
/// renders an icon-less tab/group when the admin hasn't set an explicit
/// override.
///
/// All names are confirmed-present in <c>DevExpress.Images.v25.2.dll</c>.
/// Resolution chain at runtime is:
///   user_ribbon_overrides.custom_icon
///     ↓
///   admin_ribbon_defaults.default_icon
///     ↓
///   ribbon_pages/groups.icon_name (DB seed)
///     ↓
///   <see cref="ForPage"/> / <see cref="ForGroup"/> (this catalogue)
///     ↓
///   no glyph
/// </summary>
public static class RibbonDefaultIcons
{
    /// <summary>
    /// Default DX glyph for a ribbon page caption. Returns null if the caption
    /// isn't in the catalogue — caller falls back to no glyph.
    /// </summary>
    public static string? ForPage(string? caption) => caption?.ToLowerInvariant() switch
    {
        "home"          => "Home_16x16",
        "devices"       => "Database_16x16",        // IPAM grid
        "switches"      => "ServerMode_16x16",      // switch fabric
        "links"         => "Hierarchy_16x16",       // P2P / B2B links
        "link actions"  => "Hierarchy_16x16",
        "routing"       => "Hierarchy_16x16",       // BGP / route table
        "bgp"           => "Hierarchy_16x16",
        "vlans"         => "Grid_16x16",
        "tasks"         => "Task_16x16",
        "service desk"  => "News_16x16",
        "servicedesk"   => "News_16x16",
        "admin"         => "Security_16x16",
        "admin actions" => "Security_16x16",
        "global admin"  => "BOPermission_32x32",
        "globaladmin"   => "BOPermission_32x32",
        "builder"       => "Wizard_32x32",
        "connectivity"  => "Wireless_32x32",
        "★ global"      => "Apply_16x16",
        _ => null
    };

    /// <summary>
    /// Default DX glyph for a ribbon group caption. Group captions repeat
    /// across pages (e.g. "Actions" on every module tab) so the lookup is by
    /// caption, not by (page, group).
    /// </summary>
    public static string? ForGroup(string? caption) => caption?.ToLowerInvariant() switch
    {
        "actions"            => "Edit_16x16",
        "★ global actions"   => "Apply_16x16",
        "row"                => "ListBullets_16x16",
        "filter"             => "Filter_16x16",
        "group"              => "GroupBy_16x16",
        "group by"           => "GroupBy_16x16",
        "layout"             => "LayoutPanel_16x16",
        "view"               => "Show_32x32",
        "theme"              => "ChangeFontStyle_16x16",
        "data"               => "Database_16x16",
        "export"             => "ExportToXLS_32x32",
        "web app"            => "Browser_32x32",
        "diagram"            => "ShowDependencies_32x32",
        "deploy"             => "Apply_32x32",
        "connectivity"       => "Wireless_32x32",
        "bgp"                => "Hierarchy_16x16",
        "panels"             => "LayoutPanel_16x16",
        "security"           => "Security_Permission_16x16",
        "user management"    => "BOUser_32x32",
        "tools"              => "EditDataSource_16x16",
        "undo"               => "Undo_32x32",
        "descriptions"       => "News_16x16",
        "vlans"              => "Grid_16x16",
        "sections"           => "LayoutPanel_16x16",
        "sync"               => "Refresh_32x32",
        "write back"         => "Apply_32x32",
        "dashboards"         => "ChartLine_16x16",
        _ => null
    };

    /// <summary>
    /// Default DX glyph for a ribbon-page-category caption (contextual tabs).
    /// </summary>
    public static string? ForCategory(string? caption) => caption?.ToLowerInvariant() switch
    {
        "link tools"   => "Hierarchy_16x16",
        "switch tools" => "ServerMode_16x16",
        "admin tools"  => "Security_16x16",
        _ => null
    };
}
