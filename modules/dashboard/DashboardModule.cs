using Central.Core.Auth;
using Central.Core.Modules;
using Central.Core.Shell;

namespace Central.Module.Dashboard;

/// <summary>
/// Dashboard module — unified KPI overview with device/switch/task health,
/// real-time status indicators, and quick actions.
/// Visible to all authenticated users (devices:read minimum).
/// </summary>
public class DashboardModule : IModule, IModuleRibbon, IModulePanels
{
    public string Name => "Dashboard";
    public string PermissionCategory => "devices";
    public int SortOrder => 1; // First tab — dashboard is the landing page

    public void RegisterRibbon(IRibbonBuilder ribbon)
    {
        ribbon.AddPage("Home", SortOrder, page =>
        {
            page.AddGroup("Dashboard", group =>
            {
                group.AddButton("Refresh", P.DevicesRead, "Refresh_16x16",
                    () => PanelMessageBus.Publish(new RefreshPanelMessage("DashboardPanel")));
            });
            page.AddGroup("Panels", group =>
            {
                group.AddCheckButton("Dashboard", panelId: "DashboardPanel");
                group.AddCheckButton("Notifications", panelId: "NotificationCenterPanel");
            });
        });
    }

    public void RegisterPanels(IPanelBuilder panels)
    {
        // Panel views will be wired in MainWindow (panels are still XAML-defined there)
    }
}
