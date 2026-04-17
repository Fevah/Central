using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Central.Engine.Models;

namespace Central.Module.Projects;

public partial class QAPanel : System.Windows.Controls.UserControl
{
    public QAPanel()
    {
        InitializeComponent();
        SeverityFilter.ItemsSource = new[] { "", "Blocker", "Critical", "Major", "Minor", "Cosmetic" };
        StatusFilter.ItemsSource = new[] { "", "New", "Triaged", "InProgress", "Resolved", "Verified", "Closed" };
        BugPriorityFilter.ItemsSource = new[] { "", "Critical", "High", "Medium", "Low" };
    }

    public DevExpress.Xpf.Grid.GridControl Grid => BugGrid;
    public DevExpress.Xpf.Grid.TableView View => BugView;

    // Events
    public event Func<TaskItem, Task>? SaveBug;
    public event Func<int?, Task>? ProjectChanged;
    public event Action? NewBugRequested;
    public event Func<List<int>, string, string, Task>? BatchTriage; // taskIds, severity, bugPriority

    public void SetProjects(IEnumerable<TaskProject> projects)
    {
        var items = new List<TaskProject> { new() { Id = 0, Name = "(All Projects)" } };
        items.AddRange(projects);
        ProjectSelector.ItemsSource = items;
    }

    public int? SelectedProjectId =>
        ProjectSelector.EditValue is TaskProject p && p.Id > 0 ? p.Id : null;

    private void ProjectSelector_Changed(object sender, DevExpress.Xpf.Editors.EditValueChangedEventArgs e)
        => ProjectChanged?.Invoke(SelectedProjectId);

    private void NewBug_Click(object sender, System.Windows.RoutedEventArgs e)
        => NewBugRequested?.Invoke();

    private async void BatchTriage_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        var handles = BugView.GetSelectedRowHandles();
        if (handles == null || handles.Length == 0) return;
        var ids = new List<int>();
        foreach (var h in handles)
        {
            if (BugGrid.GetRow(h) is TaskItem t) ids.Add(t.Id);
        }
        if (ids.Count == 0) return;

        // Default triage: set to Triaged status with Medium priority if not set
        if (BatchTriage != null)
            await BatchTriage(ids, "Major", "Medium");
    }

    private async void BugView_ValidateRow(object sender, DevExpress.Xpf.Grid.GridRowValidationEventArgs e)
    {
        if (e.Row is TaskItem task && SaveBug != null)
        {
            try { await SaveBug(task); }
            catch (Exception ex) { e.ErrorContent = ex.Message; e.IsValid = false; }
        }
    }

    private void BugView_InvalidRowException(object sender, DevExpress.Xpf.Grid.InvalidRowExceptionEventArgs e)
        => e.ExceptionMode = DevExpress.Xpf.Grid.ExceptionMode.NoAction;

    private void Filter_Changed(object sender, DevExpress.Xpf.Editors.EditValueChangedEventArgs e)
    {
        var severity = SeverityFilter.EditValue as string ?? "";
        var status = StatusFilter.EditValue as string ?? "";
        var bugPri = BugPriorityFilter.EditValue as string ?? "";

        var conditions = new List<string>();
        if (!string.IsNullOrEmpty(severity)) conditions.Add($"[Severity] = '{severity}'");
        if (!string.IsNullOrEmpty(status)) conditions.Add($"[Status] = '{status}'");
        if (!string.IsNullOrEmpty(bugPri)) conditions.Add($"[BugPriority] = '{bugPri}'");

        BugGrid.FilterString = conditions.Count > 0 ? string.Join(" AND ", conditions) : "";
        BugCountText.Text = $"{BugGrid.VisibleRowCount} bugs";
    }
}
