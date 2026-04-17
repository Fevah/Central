using Central.Engine.Auth;
using Central.Engine.Modules;
using Central.Engine.Shell;

namespace Central.Module.Global;

/// <summary>
/// Global module — platform-required functionality that every tenant has:
/// the landing dashboard, per-tenant administration (users, roles, lookups,
/// backups, jobs, etc.), and (gated on global_admin claim) the cross-tenant
/// platform admin (tenants, licensing, platform audit).
///
/// Merged 2026-04-17 from the former separate modules (admin, dashboard,
/// global-admin). Each former module's code lives in its own subfolder
/// (Admin/, Dashboard/, Platform/) so it's still organised; the assembly
/// is one unit because none of these three are independently togglable —
/// they're all "always on". Tenant-togglable modules (Networking,
/// Projects, CRM, ServiceDesk, Devices) live outside.
/// </summary>
public class GlobalModule : IModule, IModuleRibbon, IModulePanels
{
    public string Name => "Global";
    public string PermissionCategory => "admin";
    public int SortOrder => 1;

    public void RegisterRibbon(IRibbonBuilder ribbon)
    {
        // ══════ Home tab (landing page + notifications) ═════════════════════
        ribbon.AddPage("Home", 1, page =>
        {
            page.AddGroup("Dashboard", group =>
            {
                group.AddButton("Refresh", P.DevicesRead, "Refresh_16x16",
                    () => PanelMessageBus.Publish(new RefreshPanelMessage("DashboardPanel")));
            });
            page.AddGroup("Panels", group =>
            {
                group.AddCheckButton("Dashboard",     panelId: "DashboardPanel");
                group.AddCheckButton("Notifications", panelId: "NotificationCenterPanel");
            });
        });

        // ══════ Admin tab (per-tenant admin) ═══════════════════════════════
        ribbon.AddPage("Admin", 90, page =>
        {
            page.AddGroup("Actions", group =>
            {
                group.AddButton("New",    P.AdminUsers, null, () => { });
                group.AddButton("Edit",   P.AdminUsers, null, () => { });
                group.AddButton("Delete", P.AdminUsers, null, () => { });
            });
            page.AddGroup("Panels", group =>
            {
                group.AddCheckButton("Roles & Permissions", panelId: "RolesPanel");
                group.AddCheckButton("Users",               panelId: "UsersPanel");
                group.AddCheckButton("Lookup Values",       panelId: "LookupsPanel");
                group.AddCheckButton("Audit Log",           panelId: "AuditPanel");
                group.AddCheckButton("AD Browser",          panelId: "AdBrowserPanel");
                group.AddCheckButton("Icon Defaults",       panelId: "IconDefaultsPanel");
                group.AddCheckButton("My Icons",            panelId: "IconOverridesPanel");
                group.AddCheckButton("Locations",           panelId: "LocationsPanel");
                group.AddCheckButton("Reference Config",    panelId: "ReferenceConfigPanel");
            });
            page.AddGroup("System", group =>
            {
                group.AddCheckButton("Migrations",          panelId: "MigrationsPanel");
                group.AddCheckButton("DB Backup",           panelId: "BackupPanel");
                group.AddCheckButton("Podman",              panelId: "PodmanPanel");
                group.AddCheckButton("Schedule",            panelId: "SchedulerPanel");
                group.AddCheckButton("Sync Engine",         panelId: "SyncConfigPanel");
            });
            page.AddGroup("Identity", group =>
            {
                group.AddCheckButton("Identity Providers",  panelId: "IdentityProvidersPanel");
                group.AddCheckButton("Auth Events",         panelId: "AuthEventsPanel");
                group.AddCheckButton("API Keys",            panelId: "ApiKeysPanel");
                group.AddCheckButton("Audit Log",           panelId: "AuditLogPanel");
                group.AddCheckButton("Sessions",            panelId: "SessionsPanel");
                group.AddCheckButton("My Notifications",    panelId: "NotificationPrefsPanel");
            });
            page.AddGroup("Maintenance", group =>
            {
                group.AddCheckButton("Purge Deleted",       panelId: "PurgePanel");
            });
            page.AddGroup("Data", group =>
            {
                group.AddButton("Refresh", P.AdminAudit, null, () => { });
            });
        });

        // ══════ Global Admin tab (platform-level — global_admin claim) ═════
        ribbon.AddPage("Global Admin", 100, "global_admin:read", page =>
        {
            page.AddGroup("Panels", group =>
            {
                group.AddCheckButton("Tenants",             panelId: "GlobalTenantsPanel");
                group.AddCheckButton("Global Users",        panelId: "GlobalUsersPanel");
                group.AddCheckButton("Subscriptions",       panelId: "GlobalSubscriptionsPanel");
                group.AddCheckButton("Module Licenses",     panelId: "GlobalLicensesPanel");
                group.AddCheckButton("Platform Dashboard",  panelId: "PlatformDashboardPanel");
                group.AddCheckButton("Audit Log",           panelId: "GlobalAdminAuditPanel");
            });
            page.AddGroup("Data", group =>
            {
                group.AddButton("Refresh All", null, null, () => { });
            });
        });
    }

    public void RegisterPanels(IPanelBuilder panels)
    {
        // Panels wired via apps/desktop/MainWindow.xaml (XAML-defined DockLayoutManager).
    }
}
