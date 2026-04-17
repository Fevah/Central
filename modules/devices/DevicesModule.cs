using Central.Engine.Auth;
using Central.Engine.Modules;
using Central.Engine.Shell;
using Central.Engine.Widgets;
using Central.Module.Devices.Dashboards;

namespace Central.Module.Devices;

/// <summary>
/// IPAM module — device inventory grid + detail panel.
/// First module extracted from MainWindow monolith.
///
/// Follows TotalLink's AdminModule.cs pattern:
/// - Implements IModule for metadata
/// - Implements IModuleRibbon for ribbon registration
/// - Implements IModulePanels for panel registration
/// - Will implement Autofac.Module for DI registration (Phase 2 complete)
/// </summary>
public class DevicesModule : IModule, IModuleRibbon, IModulePanels
{
    public string Name => "Devices";
    public string PermissionCategory => "devices";
    public int SortOrder => 10;

    public DevicesModule()
    {
        DashboardContributionRegistry.Register(new DevicesDashboardContribution());
    }

    public void RegisterRibbon(IRibbonBuilder ribbon)
    {
        ribbon.AddPage("Devices", SortOrder, page =>
        {
            page.AddGroup("Actions", group =>
            {
                group.AddButton("New Device",    P.DevicesWrite,  "AddItem_16x16",
                    () => PanelMessageBus.Publish(new NavigateToPanelMessage("devices", "action:new")));
                group.AddButton("Delete Device", P.DevicesDelete, "Delete_16x16",
                    () => PanelMessageBus.Publish(new NavigateToPanelMessage("devices", "action:delete")));
            });
            page.AddGroup("Data", group =>
            {
                group.AddButton("Refresh", P.DevicesRead, "Refresh_16x16",
                    () => PanelMessageBus.Publish(new RefreshPanelMessage("devices")));
                group.AddButton("Export",  P.DevicesExport, "ExportFile_16x16",
                    () => PanelMessageBus.Publish(new NavigateToPanelMessage("devices", "action:export")));
            });
            page.AddGroup("Panels", group =>
            {
                group.AddCheckButton("IPAM",    panelId: "DevicesPanel");
                group.AddCheckButton("Details",  panelId: "DeviceDetailPanel");
            });
        });
    }

    public void RegisterPanels(IPanelBuilder panels)
    {
        // Panel views and ViewModels will be registered here once extracted
        // panels.AddPanel("DevicesPanel", "IPAM", typeof(DeviceGridPanel), typeof(DeviceListViewModel),
        //     P.DevicesRead, DockPosition.Document);
        // panels.AddPanel("DeviceDetailPanel", "Device Details", typeof(DeviceDetailPanel), typeof(DeviceDetailViewModel),
        //     P.DevicesRead, DockPosition.Right, closedByDefault: false);
    }
}
