using Central.Engine.Auth;
using Central.Engine.Modules;
using Central.Engine.Shell;
using Central.Engine.Widgets;
using Central.Module.Projects.Dashboards;

namespace Central.Module.Projects;

public class ProjectsModule : IModule, IModuleRibbon, IModulePanels
{
    public string Name => "Projects";
    public string PermissionCategory => "tasks";
    public int SortOrder => 60;

    public ProjectsModule()
    {
        DashboardContributionRegistry.Register(new ProjectsDashboardContribution());
    }

    public void RegisterRibbon(IRibbonBuilder ribbon)
    {
        ribbon.AddPage("Projects", SortOrder, page =>
        {
            page.AddGroup("Actions", group =>
            {
                group.AddButton("Add Task",    P.TasksWrite,  "AddItem_32x32", () => { });
                group.AddButton("Add SubTask", P.TasksWrite,  "AddItem_16x16", () => { });
                group.AddButton("Add Bug",     P.TasksWrite,  "Bug_16x16",     () => { });
                group.AddButton("Delete",      P.TasksDelete, "DeleteItem_16x16", () => { });
            });
            page.AddGroup("Sprint", group =>
            {
                group.AddButton("New Sprint",   P.SprintsWrite, "Planning_16x16",   () => { });
                group.AddButton("Close Sprint", P.SprintsWrite, "Status_16x16",     () => { });
                group.AddButton("Snapshot Burndown", P.SprintsWrite, "ChartLine_16x16", () => { });
            });
            page.AddGroup("Scheduling", group =>
            {
                group.AddButton("Save Baseline",  P.TasksWrite, "Save_16x16",   () => { });
                group.AddButton("Zoom to Fit",    P.TasksRead,  "ZoomIn_16x16", () => { });
            });
            page.AddGroup("View", group =>
            {
                group.AddButton("Refresh", P.TasksRead, "Refresh_16x16",
                    () => PanelMessageBus.Publish(new RefreshPanelMessage("tasks")));
            });
            page.AddGroup("Panels", group =>
            {
                group.AddCheckButton("Tasks",         panelId: "TasksPanel");
                group.AddCheckButton("Backlog",       panelId: "BacklogPanel");
                group.AddCheckButton("Sprint Plan",   panelId: "SprintPlanningPanel");
                group.AddCheckButton("Burndown",      panelId: "SprintBurndownDocPanel");
                group.AddCheckButton("Kanban",        panelId: "KanbanBoardDocPanel");
                group.AddCheckButton("Gantt",         panelId: "GanttDocPanel");
                group.AddCheckButton("QA / Bugs",     panelId: "QADocPanel");
                group.AddCheckButton("QA Dashboard",  panelId: "QADashboardDocPanel");
                group.AddCheckButton("Reports",       panelId: "ReportBuilderDocPanel");
                group.AddCheckButton("Dashboard",     panelId: "TaskDashboardDocPanel");
                group.AddCheckButton("Timesheet",     panelId: "TimesheetDocPanel");
                group.AddCheckButton("Activity",      panelId: "ActivityFeedDocPanel");
                group.AddCheckButton("My Tasks",      panelId: "MyTasksDocPanel");
                group.AddCheckButton("Portfolio",     panelId: "PortfolioDocPanel");
                group.AddCheckButton("Import",        panelId: "TaskImportDocPanel");
            });
        });
    }

    public void RegisterPanels(IPanelBuilder panels)
    {
        panels.AddPanel("tasks",          "Tasks",          typeof(TaskTreePanel),       typeof(object), P.TasksRead, closedByDefault: false);
        panels.AddPanel("backlog",        "Product Backlog", typeof(TaskBacklogPanel),   typeof(object), P.TasksRead, closedByDefault: true);
        panels.AddPanel("sprintplan",     "Sprint Planning", typeof(SprintPlanPanel),    typeof(object), P.SprintsRead, closedByDefault: true);
        panels.AddPanel("burndown",       "Sprint Burndown", typeof(SprintBurndownPanel),typeof(object), P.SprintsRead, closedByDefault: true);
        panels.AddPanel("kanban",         "Kanban Board",    typeof(KanbanBoardPanel),   typeof(object), P.TasksRead, closedByDefault: true);
        panels.AddPanel("gantt",          "Gantt Chart",     typeof(GanttPanel),         typeof(object), P.TasksRead, closedByDefault: true);
        panels.AddPanel("qa",             "QA / Bugs",       typeof(QAPanel),            typeof(object), P.TasksRead, closedByDefault: true);
        panels.AddPanel("qadash",         "QA Dashboard",    typeof(QADashboardPanel),   typeof(object), P.TasksRead, closedByDefault: true);
        panels.AddPanel("reports",        "Report Builder",  typeof(ReportBuilderPanel), typeof(object), P.TasksRead, closedByDefault: true);
        panels.AddPanel("taskdash",       "Task Dashboard",  typeof(TaskDashboardPanel), typeof(object), P.TasksRead, closedByDefault: true);
        panels.AddPanel("timesheet",      "Timesheet",       typeof(TimesheetPanel),     typeof(object), P.TasksRead, closedByDefault: true);
        panels.AddPanel("actfeed",        "Activity Feed",   typeof(ActivityFeedPanel),  typeof(object), P.TasksRead, closedByDefault: true);
        panels.AddPanel("mytasks",        "My Tasks",        typeof(MyTasksPanel),       typeof(object), P.TasksRead, closedByDefault: true);
        panels.AddPanel("portfolio",      "Portfolio",        typeof(PortfolioPanel),    typeof(object), P.ProjectsRead, closedByDefault: true);
        panels.AddPanel("taskimport",     "Task Import",     typeof(TaskImportPanel),    typeof(object), P.TasksWrite, closedByDefault: true);
    }
}
