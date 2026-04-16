using Central.Core.Modules;

namespace Central.Module.GlobalAdmin;

/// <summary>
/// Global Admin module — platform-level management above per-tenant Admin.
/// Manages: tenants, global users, subscriptions, module licenses, platform health.
/// Only visible to users with global_admin:read permission.
///
/// Action buttons live in XAML context tabs (GlobalAdminContextCategory in MainWindow.xaml)
/// that appear/disappear based on which panel is active. This static page provides only
/// the panel toggle buttons and a Refresh All.
/// </summary>
public class GlobalAdminModule : IModule, IModuleRibbon, IModulePanels
{
    public string Name => "Global Admin";
    public string PermissionCategory => "global_admin";
    public int SortOrder => 100; // Last tab — highest privilege

    public void RegisterRibbon(IRibbonBuilder ribbon)
    {
        ribbon.AddPage("Global Admin", SortOrder, "global_admin:read", page =>
        {
            page.AddGroup("Panels", group =>
            {
                group.AddCheckButton("Tenants", panelId: "GlobalTenantsPanel");
                group.AddCheckButton("Global Users", panelId: "GlobalUsersPanel");
                group.AddCheckButton("Subscriptions", panelId: "GlobalSubscriptionsPanel");
                group.AddCheckButton("Module Licenses", panelId: "GlobalLicensesPanel");
                group.AddCheckButton("Platform Dashboard", panelId: "PlatformDashboardPanel");
                group.AddCheckButton("Audit Log", panelId: "GlobalAdminAuditPanel");
            });

            page.AddGroup("Data", group =>
            {
                group.AddButton("Refresh All", null, null, () => { });
            });
        });
    }

    public void RegisterPanels(IPanelBuilder panels)
    {
        // Panels are defined in MainWindow.xaml and bound to ViewModels in code-behind
    }
}
