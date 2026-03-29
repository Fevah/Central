using Central.Core.Modules;
using Central.Core.Shell;

namespace Central.Module.ServiceDesk;

public class ServiceDeskModule : IModule, IModuleRibbon, IModulePanels
{
    public string Name => "Service Desk";
    public string PermissionCategory => "servicedesk";
    public int SortOrder => 70;

    public void RegisterRibbon(IRibbonBuilder ribbon)
    {
        ribbon.AddPage("Service Desk", SortOrder, "servicedesk:read", page =>
        {
            page.AddGroup("Sync", group =>
            {
                group.AddButton("Read", "servicedesk:sync", "Download_16x16",
                    () => PanelMessageBus.Publish(new NavigateToPanelMessage("servicedesk", "sync")));
                group.AddSeparator();
                group.AddButton("Refresh", null, "Refresh_16x16",
                    () => PanelMessageBus.Publish(new RefreshPanelMessage("servicedesk")));
            });
            page.AddGroup("Write Back", group =>
            {
                group.AddButton("Update Status", "servicedesk:write", "Apply_16x16",
                    () => PanelMessageBus.Publish(new NavigateToPanelMessage("servicedesk", "write:status")));
                group.AddButton("Update Priority", "servicedesk:write", "Priority_16x16",
                    () => PanelMessageBus.Publish(new NavigateToPanelMessage("servicedesk", "write:priority")));
                group.AddButton("Assign Tech", "servicedesk:write", "BOUser_16x16",
                    () => PanelMessageBus.Publish(new NavigateToPanelMessage("servicedesk", "write:technician")));
                group.AddButton("Add Note", "servicedesk:write", "EditComment_16x16",
                    () => PanelMessageBus.Publish(new NavigateToPanelMessage("servicedesk", "write:note")));
            });
            page.AddGroup("View", group =>
            {
                group.AddButton("Open Tickets", null, "Filter_16x16",
                    () => PanelMessageBus.Publish(new NavigateToPanelMessage("servicedesk", "filter:Open")));
                group.AddButton("My Tickets", null, "BOUser_16x16",
                    () => PanelMessageBus.Publish(new NavigateToPanelMessage("servicedesk", "filter:MyTickets")));
                group.AddButton("All Tickets", null, "ListBullets_16x16",
                    () => PanelMessageBus.Publish(new NavigateToPanelMessage("servicedesk", "filter:All")));
            });
            page.AddGroup("Dashboards", group =>
            {
                group.AddButton("Overview", null, "ChartType_Bar_16x16",
                    () => PanelMessageBus.Publish(new NavigateToPanelMessage("servicedesk", "panel:overview")));
                group.AddButton("Tech Closures", null, "ChartType_StackedBar_16x16",
                    () => PanelMessageBus.Publish(new NavigateToPanelMessage("servicedesk", "panel:closures")));
                group.AddButton("Aging", null, "ChartType_SideBySideBar_16x16",
                    () => PanelMessageBus.Publish(new NavigateToPanelMessage("servicedesk", "panel:aging")));
            });
            page.AddGroup("Data", group =>
            {
                group.AddButton("Groups", null, "BOFolder_16x16",
                    () => PanelMessageBus.Publish(new NavigateToPanelMessage("servicedesk", "panel:groups")));
                group.AddButton("Technicians", null, "BOEmployee_16x16",
                    () => PanelMessageBus.Publish(new NavigateToPanelMessage("servicedesk", "panel:technicians")));
                group.AddButton("Requesters", null, "BOContact_16x16",
                    () => PanelMessageBus.Publish(new NavigateToPanelMessage("servicedesk", "panel:requesters")));
                group.AddButton("Teams", null, "BOTeam_16x16",
                    () => PanelMessageBus.Publish(new NavigateToPanelMessage("servicedesk", "panel:teams")));
                group.AddButton("Group Categories", null, "TreeView_16x16",
                    () => PanelMessageBus.Publish(new NavigateToPanelMessage("servicedesk", "panel:groupcats")));
            });
            page.AddGroup("Panels", group =>
            {
                group.AddButton("SD Settings", null, "Preferences_16x16",
                    () => PanelMessageBus.Publish(new NavigateToPanelMessage("servicedesk", "panel:settings")));
                group.AddButton("Details", null, "BOFileAttachment_16x16",
                    () => PanelMessageBus.Publish(new NavigateToPanelMessage("servicedesk", "panel:details")));
            });
        });
    }

    public void RegisterPanels(IPanelBuilder panels)
    {
        panels.AddPanel("ServiceDeskRequests", "Service Desk",
            typeof(Views.RequestGridPanel), typeof(object),
            "servicedesk:read", Central.Core.Modules.DockPosition.Document, true);
        panels.AddPanel("SdOverview", "SD Overview",
            typeof(Views.OverviewChartPanel), typeof(object),
            "servicedesk:read", Central.Core.Modules.DockPosition.Document, true);
        panels.AddPanel("SdTechClosures", "SD Tech Closures",
            typeof(Views.TechClosuresPanel), typeof(object),
            "servicedesk:read", Central.Core.Modules.DockPosition.Document, true);
        panels.AddPanel("SdAging", "SD Aging",
            typeof(Views.AgingChartPanel), typeof(object),
            "servicedesk:read", Central.Core.Modules.DockPosition.Document, true);
        panels.AddPanel("SdTeams", "SD Teams",
            typeof(Views.TeamsPanel), typeof(object),
            "servicedesk:write", Central.Core.Modules.DockPosition.Document, true);
        panels.AddPanel("SdGroups", "SD Groups",
            typeof(Views.SdGroupsPanel), typeof(object),
            "servicedesk:read", Central.Core.Modules.DockPosition.Document, true);
        panels.AddPanel("SdTechnicians", "SD Technicians",
            typeof(Views.SdTechniciansPanel), typeof(object),
            "servicedesk:read", Central.Core.Modules.DockPosition.Document, true);
        panels.AddPanel("SdRequesters", "SD Requesters",
            typeof(Views.SdRequestersPanel), typeof(object),
            "servicedesk:read", Central.Core.Modules.DockPosition.Document, true);
    }
}
