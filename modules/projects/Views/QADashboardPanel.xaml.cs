using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Central.Engine.Models;

namespace Central.Module.Projects;

public record ChartDataPoint(string Label, int Count);
public record DateDataPoint(DateTime Date, int Count);

public partial class QADashboardPanel : System.Windows.Controls.UserControl
{
    public QADashboardPanel()
    {
        InitializeComponent();
    }

    public event Func<int?, Task>? ProjectChanged;
    public event Func<int?, Task<List<TaskItem>>>? LoadBugs;

    public void SetProjects(IEnumerable<TaskProject> projects)
    {
        var items = new List<TaskProject> { new() { Id = 0, Name = "(All Projects)" } };
        items.AddRange(projects);
        ProjectSelector.ItemsSource = items;
    }

    public int? SelectedProjectId =>
        ProjectSelector.EditValue is TaskProject p && p.Id > 0 ? p.Id : null;

    /// <summary>Refresh all charts with the given bug data.</summary>
    public void RefreshCharts(List<TaskItem> bugs)
    {
        // Severity chart
        var severityData = new[] { "Blocker", "Critical", "Major", "Minor", "Cosmetic" }
            .Select(s => new ChartDataPoint(s, bugs.Count(b => b.Severity == s && b.Status != "Closed" && b.Status != "Verified")))
            .ToList();
        SeveritySeries.DataSource = severityData;

        // Aging chart (open bugs by age bucket)
        var now = DateTime.Today;
        var openBugs = bugs.Where(b => b.Status != "Closed" && b.Status != "Verified" && b.Status != "Resolved").ToList();
        var agingData = new List<ChartDataPoint>
        {
            new("0-1d", openBugs.Count(b => (now - b.CreatedAt.Date).Days <= 1)),
            new("2-3d", openBugs.Count(b => { var d = (now - b.CreatedAt.Date).Days; return d >= 2 && d <= 3; })),
            new("4-7d", openBugs.Count(b => { var d = (now - b.CreatedAt.Date).Days; return d >= 4 && d <= 7; })),
            new("1-2w", openBugs.Count(b => { var d = (now - b.CreatedAt.Date).Days; return d >= 8 && d <= 14; })),
            new("2-4w", openBugs.Count(b => { var d = (now - b.CreatedAt.Date).Days; return d >= 15 && d <= 28; })),
            new("4w+", openBugs.Count(b => (now - b.CreatedAt.Date).Days > 28)),
        };
        AgingSeries.DataSource = agingData;

        // Resolution rate (last 30 days)
        var thirtyDaysAgo = now.AddDays(-30);
        var openedByDay = bugs.Where(b => b.CreatedAt >= thirtyDaysAgo)
            .GroupBy(b => b.CreatedAt.Date)
            .Select(g => new DateDataPoint(g.Key, g.Count()))
            .OrderBy(d => d.Date).ToList();
        var closedByDay = bugs.Where(b => b.CompletedAt.HasValue && b.CompletedAt >= thirtyDaysAgo)
            .GroupBy(b => b.CompletedAt!.Value.Date)
            .Select(g => new DateDataPoint(g.Key, g.Count()))
            .OrderBy(d => d.Date).ToList();
        OpenedSeries.DataSource = openedByDay;
        ClosedSeries.DataSource = closedByDay;

        // Top assignees (open bugs)
        var assigneeData = openBugs
            .Where(b => !string.IsNullOrEmpty(b.AssignedToName))
            .GroupBy(b => b.AssignedToName)
            .Select(g => new ChartDataPoint(g.Key, g.Count()))
            .OrderByDescending(c => c.Count)
            .Take(10).ToList();
        AssigneeSeries.DataSource = assigneeData;
    }

    private void ProjectSelector_Changed(object sender, DevExpress.Xpf.Editors.EditValueChangedEventArgs e)
        => ProjectChanged?.Invoke(SelectedProjectId);

    private async void Refresh_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (LoadBugs != null)
        {
            var bugs = await LoadBugs(SelectedProjectId);
            RefreshCharts(bugs);
        }
    }
}
