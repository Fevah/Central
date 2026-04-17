using Central.Engine.Auth;
using Central.Engine.Modules;

namespace Central.Module.Admin;

/// <summary>
/// Administration module — users, roles, permissions, lookups, AD, backup,
/// migrations, reference numbers, container management, scheduler.
/// Based on TotalLink's AdminModule.cs pattern — modernised for Central.
/// </summary>
public class AdminModule : IModule, IModuleRibbon, IModulePanels
{
    public string Name => "Admin";
    public string PermissionCategory => "admin";
    public int SortOrder => 90;

    public void RegisterRibbon(IRibbonBuilder ribbon)
    {
        ribbon.AddPage("Admin", SortOrder, page =>
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
    }

    public void RegisterPanels(IPanelBuilder panels)
    {
    }
}
