using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Central.Engine.Models;

namespace Central.Module.Projects;

public partial class TaskBacklogPanel : System.Windows.Controls.UserControl
{
    public TaskBacklogPanel()
    {
        InitializeComponent();
        CategoryFilter.ItemsSource = new[] { "", "Feature", "Enhancement", "TechDebt", "Bug", "Ops" };
    }

    public DevExpress.Xpf.Grid.TreeListControl Tree => BacklogTree;

    // Events
    public event Func<TaskItem, Task>? SaveTask;
    public event Func<int?, Task>? ProjectChanged;
    public event Func<int, int, Task>? CommitToSprint;     // taskId, sprintId
    public event Func<int, Task>? UncommitFromSprint;       // taskId
    public event Func<int, int, Task>? UpdatePriority;      // taskId, newPriority

    public void SetProjects(IEnumerable<TaskProject> projects)
    {
        ProjectSelector.ItemsSource = projects;
    }

    public void SetSprints(IEnumerable<Sprint> sprints)
    {
        SprintSelector.ItemsSource = sprints;
    }

    public int? SelectedProjectId
    {
        get
        {
            if (ProjectSelector.EditValue is TaskProject p && p.Id > 0) return p.Id;
            return null;
        }
    }

    private void ProjectSelector_Changed(object sender, DevExpress.Xpf.Editors.EditValueChangedEventArgs e)
    {
        ProjectChanged?.Invoke(SelectedProjectId);
    }

    private async void CommitToSprint_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (SprintSelector.EditValue is not Sprint sprint || sprint.Id == 0) return;
        var handles = BacklogTreeView.GetSelectedRowHandles();
        if (handles == null) return;
        foreach (var handle in handles)
        {
            if (BacklogTree.GetRow(handle) is TaskItem task && CommitToSprint != null)
            {
                task.CommittedTo = sprint.Id;
                task.CommittedToName = sprint.Name;
                await CommitToSprint(task.Id, sprint.Id);
            }
        }
    }

    private async void Uncommit_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        var handles = BacklogTreeView.GetSelectedRowHandles();
        if (handles == null) return;
        foreach (var handle in handles)
        {
            if (BacklogTree.GetRow(handle) is TaskItem task && UncommitFromSprint != null)
            {
                task.CommittedTo = null;
                task.CommittedToName = "";
                await UncommitFromSprint(task.Id);
            }
        }
    }

    private async void BacklogTreeView_ValidateNode(object sender, DevExpress.Xpf.Grid.TreeList.TreeListNodeValidationEventArgs e)
    {
        if (e.Row is TaskItem task && SaveTask != null)
        {
            try { await SaveTask(task); }
            catch (Exception ex) { e.ErrorContent = ex.Message; e.IsValid = false; }
        }
    }

    private void BacklogTreeView_InvalidNodeException(object sender, DevExpress.Xpf.Grid.TreeList.TreeListInvalidNodeExceptionEventArgs e)
        => e.ExceptionMode = DevExpress.Xpf.Grid.ExceptionMode.NoAction;

    private void BacklogTreeView_DragRecordOver(object sender, DevExpress.Xpf.Grid.DragDrop.TreeListDragOverEventArgs e)
    {
        e.AllowDrop = true;
    }

    private void Filter_Changed(object sender, DevExpress.Xpf.Editors.EditValueChangedEventArgs e)
    {
        var cat = CategoryFilter.EditValue as string ?? "";
        BacklogTree.FilterString = !string.IsNullOrEmpty(cat) ? $"[Category] = '{cat}'" : "";
        BacklogCountText.Text = $"{BacklogTree.VisibleRowCount} items";
    }
}
