using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Central.Engine.Models;

namespace Central.Module.Projects;

public partial class TaskDashboardPanel : System.Windows.Controls.UserControl
{
    public TaskDashboardPanel()
    {
        InitializeComponent();
    }

    public event Func<int?, Task>? ProjectChanged;
    public event Func<int?, Task<List<TaskItem>>>? LoadAllTasks;
    public event Func<Task<List<Sprint>>>? LoadAllSprints;

    public void SetProjects(IEnumerable<TaskProject> projects)
    {
        var items = new List<TaskProject> { new() { Id = 0, Name = "(All Projects)" } };
        items.AddRange(projects);
        ProjectSelector.ItemsSource = items;
    }

    public void SetDashboards(IEnumerable<Dashboard> dashboards)
        => DashboardSelector.ItemsSource = dashboards;

    public int? SelectedProjectId =>
        ProjectSelector.EditValue is TaskProject p && p.Id > 0 ? p.Id : null;

    /// <summary>Refresh all charts with task data.</summary>
    public void RefreshCharts(List<TaskItem> tasks, List<Sprint> sprints)
    {
        // Status pie
        var statusData = new[] { "Open", "InProgress", "Review", "Done", "Blocked" }
            .Select(s => new ChartDataPoint(s, tasks.Count(t => t.Status == s)))
            .Where(d => d.Count > 0).ToList();
        StatusPieSeries.DataSource = statusData;

        // Points by type
        var typeData = new[] { "Epic", "Story", "Task", "Bug", "SubTask", "Milestone" }
            .Select(tp => new ChartDataPoint(tp, (int)tasks.Where(t => t.TaskType == tp).Sum(t => t.Points ?? 0)))
            .Where(d => d.Count > 0).ToList();
        TypeBarSeries.DataSource = typeData;

        // Created over last 30 days
        var now = DateTime.Today;
        var thirtyDaysAgo = now.AddDays(-30);
        var createdData = tasks.Where(t => t.CreatedAt >= thirtyDaysAgo)
            .GroupBy(t => t.CreatedAt.Date)
            .Select(g => new DateDataPoint(g.Key, g.Count()))
            .OrderBy(d => d.Date).ToList();
        CreatedLineSeries.DataSource = createdData;

        // Sprint velocity (last 10 closed sprints)
        var velocityData = sprints
            .Where(s => s.Status == "Closed" && s.VelocityPoints.HasValue)
            .OrderByDescending(s => s.EndDate)
            .Take(10)
            .OrderBy(s => s.EndDate)
            .Select(s => new ChartDataPoint(s.Name, (int)(s.VelocityPoints ?? 0)))
            .ToList();
        VelocitySeries.DataSource = velocityData;
    }

    private void DashboardSelector_Changed(object sender, DevExpress.Xpf.Editors.EditValueChangedEventArgs e)
    {
        // Future: load dashboard layout from JSON
    }

    private void ProjectSelector_Changed(object sender, DevExpress.Xpf.Editors.EditValueChangedEventArgs e)
        => ProjectChanged?.Invoke(SelectedProjectId);

    private async void Refresh_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (LoadAllTasks != null && LoadAllSprints != null)
        {
            var tasks = await LoadAllTasks(SelectedProjectId);
            var sprints = await LoadAllSprints();
            RefreshCharts(tasks, sprints);
        }
    }
}
