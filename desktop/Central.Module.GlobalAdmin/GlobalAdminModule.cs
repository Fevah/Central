using Central.Core.Modules;

namespace Central.Module.GlobalAdmin;

/// <summary>
/// Global Admin module — platform-level management above per-tenant Admin.
/// Manages: tenants, global users, subscriptions, module licenses, platform health.
/// Only visible to users with is_global_admin = true.
/// </summary>
public class GlobalAdminModule : IModule, IModuleRibbon, IModulePanels
{
    public string Name => "Global Admin";
    public string PermissionCategory => "global_admin";
    public int SortOrder => 100; // Last tab — highest privilege

    public void RegisterRibbon(IRibbonBuilder ribbon)
    {
        ribbon.AddPage("Global Admin", SortOrder, page =>
        {
            page.AddGroup("Tenants", group =>
            {
                group.AddButton("New Tenant", null, null, () => { });
                group.AddButton("Suspend", null, null, () => { });
                group.AddButton("Activate", null, null, () => { });
                group.AddSeparator();
                group.AddButton("Provision Schema", null, null, () => { });
            });

            page.AddGroup("Users", group =>
            {
                group.AddButton("Reset Password", null, null, () => { });
                group.AddButton("Toggle Admin", null, null, () => { });
            });

            page.AddGroup("Licensing", group =>
            {
                group.AddButton("Grant Module", null, null, () => { });
                group.AddButton("Revoke Module", null, null, () => { });
                group.AddSeparator();
                group.AddButton("Change Plan", null, null, () => { });
            });

            page.AddGroup("Panels", group =>
            {
                group.AddCheckButton("Tenants", panelId: "GlobalTenantsPanel");
                group.AddCheckButton("Global Users", panelId: "GlobalUsersPanel");
                group.AddCheckButton("Subscriptions", panelId: "GlobalSubscriptionsPanel");
                group.AddCheckButton("Module Licenses", panelId: "GlobalLicensesPanel");
                group.AddCheckButton("Platform Dashboard", panelId: "PlatformDashboardPanel");
            });

            page.AddGroup("Data", group =>
            {
                group.AddButton("Refresh", null, null, () => { });
            });
        });
    }

    public void RegisterPanels(IPanelBuilder panels)
    {
        // Panels are created by MainWindow based on panelId check buttons
    }
}
