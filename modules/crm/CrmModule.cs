using Central.Core.Auth;
using Central.Core.Modules;
using Central.Core.Shell;

namespace Central.Module.CRM;

/// <summary>
/// CRM module — accounts, contacts, deals, leads, activities, quotes, products, dashboard.
/// Context tab: blue.
/// </summary>
public class CrmModule : IModule, IModuleRibbon, IModulePanels
{
    public string Name => "CRM";
    public string PermissionCategory => "crm";
    public int SortOrder => 40;

    public void RegisterRibbon(IRibbonBuilder ribbon)
    {
        ribbon.AddPage("CRM", SortOrder, page =>
        {
            page.AddGroup("Actions", group =>
            {
                group.AddButton("New Account",  P.CrmWrite, "AddItem_16x16",
                    () => PanelMessageBus.Publish(new NavigateToPanelMessage("CrmAccountsPanel", "action:new")));
                group.AddButton("New Contact",  P.ContactsWrite, "AddItem_16x16",
                    () => PanelMessageBus.Publish(new NavigateToPanelMessage("CrmContactsPanel", "action:new")));
                group.AddButton("New Deal",     P.CrmWrite, "AddItem_16x16",
                    () => PanelMessageBus.Publish(new NavigateToPanelMessage("CrmDealsPanel", "action:new")));
                group.AddButton("New Lead",     P.CrmWrite, "AddItem_16x16",
                    () => PanelMessageBus.Publish(new NavigateToPanelMessage("CrmLeadsPanel", "action:new")));
            });
            page.AddGroup("Data", group =>
            {
                group.AddButton("Refresh",      P.CrmRead, "Refresh_16x16",
                    () => PanelMessageBus.Publish(new RefreshPanelMessage("*")));
                group.AddButton("Export",       P.ContactsExport, "ExportFile_16x16",
                    () => PanelMessageBus.Publish(new NavigateToPanelMessage("CrmAccountsPanel", "action:export")));
            });
            page.AddGroup("Panels", group =>
            {
                group.AddCheckButton("Dashboard",   panelId: "CrmDashboardPanel");
                group.AddCheckButton("Accounts",    panelId: "CrmAccountsPanel");
                group.AddCheckButton("Contacts",    panelId: "CrmContactsPanel");
                group.AddCheckButton("Deals",       panelId: "CrmDealsPanel");
                group.AddCheckButton("Pipeline",    panelId: "CrmPipelinePanel");
                group.AddCheckButton("Leads",       panelId: "CrmLeadsPanel");
                group.AddCheckButton("Activities",  panelId: "CrmActivitiesPanel");
                group.AddCheckButton("Quotes",      panelId: "CrmQuotesPanel");
                group.AddCheckButton("Products",    panelId: "CrmProductsPanel");
            });
        });
    }

    public void RegisterPanels(IPanelBuilder panels)
    {
        // Panel registration happens in MainWindow — using XAML DockLayoutManager pattern
        // like the rest of the app. This method kept for future programmatic registration.
    }
}
