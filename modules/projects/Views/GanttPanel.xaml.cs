using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Central.Engine.Models;

namespace Central.Module.Projects;

public partial class GanttPanel : System.Windows.Controls.UserControl
{
    public GanttPanel()
    {
        InitializeComponent();
    }

    public DevExpress.Xpf.Gantt.GanttControl Gantt => GanttChart;

    // Events
    public event Func<int?, Task>? ProjectChanged;
    public event Func<int, string, Task>? SaveBaselineRequested;

    public void SetProjects(IEnumerable<TaskProject> projects)
        => ProjectSelector.ItemsSource = projects;

    public int? SelectedProjectId =>
        ProjectSelector.EditValue is TaskProject p && p.Id > 0 ? p.Id : null;

    /// <summary>Load tasks into the Gantt chart.</summary>
    public void LoadGantt(List<TaskItem> tasks, List<GanttPredecessorLink> links)
    {
        foreach (var task in tasks)
        {
            if (task.StartDate == null && task.FinishDate == null)
            {
                task.StartDate = task.CreatedAt;
                task.FinishDate = task.DueDate ?? task.CreatedAt.AddDays(1);
            }
            else if (task.StartDate == null)
                task.StartDate = task.FinishDate!.Value.AddDays(-1);
            else if (task.FinishDate == null)
                task.FinishDate = task.StartDate!.Value.AddDays(1);

            // Milestones: same start/finish
            if (task.IsMilestone)
                task.FinishDate = task.StartDate;
        }

        GanttChart.ItemsSource = tasks;
        // Dependencies stored in task_dependencies — GanttControl renders links automatically
        // when PredecessorLinksPath is configured, but we rely on the tree view for now
    }

    private void ProjectSelector_Changed(object sender, DevExpress.Xpf.Editors.EditValueChangedEventArgs e)
        => ProjectChanged?.Invoke(SelectedProjectId);

    private void ZoomIn_Click(object sender, System.Windows.RoutedEventArgs e)
        => GanttChartView.ZoomIn(null);

    private void ZoomOut_Click(object sender, System.Windows.RoutedEventArgs e)
        => GanttChartView.ZoomOut(null);

    private void FitAll_Click(object sender, System.Windows.RoutedEventArgs e)
        => GanttChartView.FitDataToWidth(0);

    private void Today_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        // Scroll to today by fitting a range around today
        try { GanttChartView.FitRangeToWidth(DateTime.Today.AddDays(-3), DateTime.Today.AddDays(30)); }
        catch { /* GanttView may not have data loaded yet */ }
    }

    private void SaveBaseline_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (SelectedProjectId.HasValue)
            SaveBaselineRequested?.Invoke(SelectedProjectId.Value, $"Baseline {DateTime.Now:yyyy-MM-dd HH:mm}");
    }

    private void ShowBaseline_Changed(object sender, DevExpress.Xpf.Editors.EditValueChangedEventArgs e)
    {
        GanttChartView.ShowBaseline = ShowBaselineCheck.IsChecked == true;
    }

    private void CriticalPath_Changed(object sender, DevExpress.Xpf.Editors.EditValueChangedEventArgs e)
    {
        // CriticalPath not available in DX 25.2 GanttView — visual indicator only
        // Future: highlight critical path tasks via row style
    }
}
