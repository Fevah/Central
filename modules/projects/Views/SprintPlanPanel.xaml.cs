using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Central.Engine.Models;

namespace Central.Module.Projects;

public partial class SprintPlanPanel : System.Windows.Controls.UserControl
{
    public SprintPlanPanel()
    {
        InitializeComponent();
    }

    public DevExpress.Xpf.Grid.GridControl Grid => SprintGrid;

    // Events
    public event Func<TaskItem, Task>? SaveTask;
    public event Func<int?, Task>? ProjectChanged;
    public event Func<int, Task>? SprintChanged;
    public event Func<Sprint, Task>? CreateSprint;
    public event Func<int, Task>? CloseSprint;

    public void SetProjects(IEnumerable<TaskProject> projects)
        => ProjectSelector.ItemsSource = projects;

    public void SetSprints(IEnumerable<Sprint> sprints)
        => SprintSelector.ItemsSource = sprints;

    public int? SelectedProjectId =>
        ProjectSelector.EditValue is TaskProject p && p.Id > 0 ? p.Id : null;

    public int? SelectedSprintId =>
        SprintSelector.EditValue is Sprint s && s.Id > 0 ? s.Id : null;

    public void UpdateCapacityBar(decimal totalPoints, decimal capacityPoints, int itemCount, string? goal, Sprint? sprint)
    {
        SprintNameText.Text = sprint?.Name ?? "No sprint selected";
        SprintDatesText.Text = sprint?.DateRange ?? "";
        GoalText.Text = !string.IsNullOrEmpty(goal) ? $"Goal: {goal}" : "";
        ItemCountText.Text = $"{itemCount} items";

        var pct = capacityPoints > 0 ? Math.Min(100, (double)(totalPoints / capacityPoints * 100)) : 0;
        CapacityBar.Value = pct;
        CapacityBar.Foreground = pct > 100
            ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(239, 68, 68))  // red
            : pct > 80
                ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(245, 158, 11)) // amber
                : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(59, 130, 246)); // blue
        CapacityText.Text = $"{totalPoints:n1} / {capacityPoints:n1} pts";
    }

    private void ProjectSelector_Changed(object sender, DevExpress.Xpf.Editors.EditValueChangedEventArgs e)
        => ProjectChanged?.Invoke(SelectedProjectId);

    private void SprintSelector_Changed(object sender, DevExpress.Xpf.Editors.EditValueChangedEventArgs e)
    {
        if (SelectedSprintId.HasValue)
            SprintChanged?.Invoke(SelectedSprintId.Value);
    }

    private void NewSprint_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (SelectedProjectId == null) return;
        var sprint = new Sprint
        {
            ProjectId = SelectedProjectId.Value,
            Name = $"Sprint {DateTime.Now:yyyy-MM-dd}",
            StartDate = DateTime.Today,
            EndDate = DateTime.Today.AddDays(14),
            Status = "Planning"
        };
        CreateSprint?.Invoke(sprint);
    }

    private void CloseSprint_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (SelectedSprintId.HasValue)
            CloseSprint?.Invoke(SelectedSprintId.Value);
    }

    private async void SprintView_ValidateRow(object sender, DevExpress.Xpf.Grid.GridRowValidationEventArgs e)
    {
        if (e.Row is TaskItem task && SaveTask != null)
        {
            try { await SaveTask(task); }
            catch (Exception ex) { e.ErrorContent = ex.Message; e.IsValid = false; }
        }
    }

    private void SprintView_InvalidRowException(object sender, DevExpress.Xpf.Grid.InvalidRowExceptionEventArgs e)
        => e.ExceptionMode = DevExpress.Xpf.Grid.ExceptionMode.NoAction;
}
