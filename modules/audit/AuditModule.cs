using Central.Engine.Auth;
using Central.Engine.Modules;

namespace Central.Module.Audit;

/// <summary>
/// M365 Audit & GDPR Compliance module — forensic investigations,
/// finding analysis, document tracking, GDPR scoring, evidence export.
/// All data comes from audit-service (Rust) via the API gateway.
/// </summary>
public class AuditModule : IModule, IModuleRibbon, IModulePanels
{
    public string Name => "Audit";
    public string PermissionCategory => "admin";
    public int SortOrder => 85;

    public void RegisterRibbon(IRibbonBuilder ribbon)
    {
        ribbon.AddPage("Audit", SortOrder, page =>
        {
            page.AddGroup("Investigations", group =>
            {
                group.AddButton("New Investigation", P.AdminAudit, "AddItem_32x32", () => { });
                group.AddButton("Refresh", P.AdminAudit, "Refresh_16x16", () => { });
            });
            page.AddGroup("GDPR", group =>
            {
                group.AddButton("Compliance Score", P.AdminAudit, "ChartLine_16x16", () => { });
                group.AddButton("Article Breakdown", P.AdminAudit, "ListBox_16x16", () => { });
            });
            page.AddGroup("M365", group =>
            {
                group.AddButton("Search Logs", P.AdminAudit, "Find_16x16", () => { });
                group.AddButton("User Activity", P.AdminAudit, "BOUser_16x16", () => { });
            });
            page.AddGroup("Export", group =>
            {
                group.AddButton("Export Evidence", P.AdminAudit, "ExportToXLS_16x16", () => { });
            });
            page.AddGroup("Panels", group =>
            {
                group.AddCheckButton("Investigations", panelId: "InvestigationsPanel");
                group.AddCheckButton("Findings", panelId: "FindingsPanel");
                group.AddCheckButton("GDPR Dashboard", panelId: "GdprDashboardPanel");
                group.AddCheckButton("M365 Logs", panelId: "M365LogsPanel");
                group.AddCheckButton("Document Tracker", panelId: "DocumentTrackerPanel");
            });
        });
    }

    public void RegisterPanels(IPanelBuilder panels)
    {
        panels.AddPanel("investigations", "Investigations",    typeof(Views.InvestigationsPanel), typeof(object), P.AdminAudit, closedByDefault: true);
        panels.AddPanel("findings",       "Forensic Findings", typeof(Views.FindingsPanel),       typeof(object), P.AdminAudit, closedByDefault: true);
        panels.AddPanel("gdpr",           "GDPR Dashboard",    typeof(Views.GdprDashboardPanel),  typeof(object), P.AdminAudit, closedByDefault: true);
        panels.AddPanel("m365logs",       "M365 Logs",         typeof(Views.M365LogsPanel),       typeof(object), P.AdminAudit, closedByDefault: true);
        panels.AddPanel("doctracker",     "Document Tracker",  typeof(Views.DocumentTrackerPanel), typeof(object), P.AdminAudit, closedByDefault: true);
    }
}
