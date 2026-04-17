using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Central.Core.Models;
using DevExpress.Xpf.Grid;
using DevExpress.Xpf.Editors.Settings;

namespace Central.Module.Tasks.Views;

public partial class TaskTreePanel : System.Windows.Controls.UserControl
{
    public TaskTreePanel()
    {
        InitializeComponent();
        StatusFilter.ItemsSource = new[] { "", "Open", "InProgress", "Review", "Done", "Blocked" };
        PriorityFilter.ItemsSource = new[] { "", "Critical", "High", "Medium", "Low" };
        TypeFilter.ItemsSource = new[] { "", "Epic", "Story", "Task", "Bug", "SubTask", "Milestone" };
    }

    public DevExpress.Xpf.Grid.TreeListControl Tree => TaskTree;
    public DevExpress.Xpf.Grid.TreeListView View => TaskTreeView;

    // Events for host wiring
    public event Func<TaskItem, Task>? SaveTask;
    public event Action? AddTaskRequested;
    public event Action? AddSubTaskRequested;
    public event Func<int?, Task>? ProjectChanged;

    /// <summary>Bind the project selector dropdown.</summary>
    public void SetProjects(IEnumerable<TaskProject> projects)
    {
        var items = new List<TaskProject> { new() { Id = 0, Name = "(All Projects)" } };
        items.AddRange(projects);
        ProjectFilter.ItemsSource = items;
    }

    /// <summary>Bind the sprint selector dropdown for the selected project.</summary>
    public void SetSprints(IEnumerable<Sprint> sprints)
    {
        var items = new List<Sprint> { new() { Id = 0, Name = "(All Sprints)" } };
        items.AddRange(sprints);
        SprintFilter.ItemsSource = items;
    }

    /// <summary>Get the currently selected project ID (null = all).</summary>
    public int? SelectedProjectId
    {
        get
        {
            if (ProjectFilter.EditValue is TaskProject p && p.Id > 0) return p.Id;
            return null;
        }
    }

    private async void TaskTreeView_ValidateNode(object sender, DevExpress.Xpf.Grid.TreeList.TreeListNodeValidationEventArgs e)
    {
        if (e.Row is TaskItem task && SaveTask != null)
        {
            try { await SaveTask(task); }
            catch (Exception ex) { e.ErrorContent = ex.Message; e.IsValid = false; }
        }
    }

    private void TaskTreeView_InvalidNodeException(object sender, DevExpress.Xpf.Grid.TreeList.TreeListInvalidNodeExceptionEventArgs e)
        => e.ExceptionMode = DevExpress.Xpf.Grid.ExceptionMode.NoAction;

    private void AddTask_Click(object sender, System.Windows.RoutedEventArgs e) => AddTaskRequested?.Invoke();
    private void AddSubTask_Click(object sender, System.Windows.RoutedEventArgs e) => AddSubTaskRequested?.Invoke();

    private void ProjectFilter_Changed(object sender, DevExpress.Xpf.Editors.EditValueChangedEventArgs e)
    {
        var pid = SelectedProjectId;
        ProjectChanged?.Invoke(pid);
        ApplyFilters();
    }

    private void Filter_Changed(object sender, DevExpress.Xpf.Editors.EditValueChangedEventArgs e)
    {
        ApplyFilters();
    }

    /// <summary>
    /// Dynamically add custom columns from project configuration.
    /// Columns are added after the static XAML columns.
    /// </summary>
    public void LoadCustomColumns(List<CustomColumn> columns)
    {
        // Remove previously added custom columns (tagged with "Custom_" prefix)
        var toRemove = TaskTree.Columns
            .OfType<TreeListColumn>()
            .Where(c => c.Tag is string s && s == "CustomColumn")
            .ToList();
        foreach (var col in toRemove)
            TaskTree.Columns.Remove(col);

        // Add new custom columns
        foreach (var cc in columns)
        {
            var treeCol = new TreeListColumn
            {
                FieldName = $"Custom_{cc.Name}",
                Header = cc.Name,
                Width = cc.ColumnType is "Number" or "Hours" ? 70 : 120,
                Tag = "CustomColumn",
                AllowEditing = DevExpress.Utils.DefaultBoolean.True,
                UnboundDataType = cc.ColumnType switch
                {
                    "Number" or "Hours" => typeof(decimal),
                    "Date" or "DateTime" => typeof(DateTime),
                    _ => typeof(string)
                }
            };

            // Set type-aware editor
            switch (cc.ColumnType)
            {
                case "Number":
                    treeCol.EditSettings = new SpinEditSettings { MinValue = -999999, MaxValue = 999999, IsFloatValue = true };
                    break;
                case "Hours":
                    treeCol.EditSettings = new SpinEditSettings { MinValue = 0, MaxValue = 99999, IsFloatValue = true, Mask = "n1" };
                    break;
                case "Date":
                    treeCol.EditSettings = new DateEditSettings { DisplayFormat = "yyyy-MM-dd", Mask = "yyyy-MM-dd" };
                    break;
                case "DateTime":
                    treeCol.EditSettings = new DateEditSettings { DisplayFormat = "yyyy-MM-dd HH:mm", Mask = "yyyy-MM-dd HH:mm" };
                    break;
                case "DropList":
                    var options = cc.GetDropListOptions();
                    treeCol.EditSettings = new ComboBoxEditSettings { IsTextEditable = false, ItemsSource = options };
                    break;
                default: // Text, RichText, People
                    break;
            }

            TaskTree.Columns.Add(treeCol);
        }
    }

    /// <summary>Set custom column values on TaskItem.CustomValues dictionary for display.</summary>
    public void SetCustomValues(Dictionary<int, Dictionary<string, string>> values)
    {
        // Store values on each TaskItem for binding
        if (TaskTree.ItemsSource is System.Collections.IEnumerable items)
        {
            foreach (var item in items)
            {
                if (item is TaskItem task && values.TryGetValue(task.Id, out var vals))
                    task.CustomValues = vals;
            }
        }
    }

    private void ApplyFilters()
    {
        var status = StatusFilter.EditValue as string ?? "";
        var priority = PriorityFilter.EditValue as string ?? "";
        var type = TypeFilter.EditValue as string ?? "";
        var sprint = SprintFilter.EditValue is Sprint s && s.Id > 0 ? s.Name : "";

        var conditions = new List<string>();
        if (!string.IsNullOrEmpty(status)) conditions.Add($"[Status] = '{status}'");
        if (!string.IsNullOrEmpty(priority)) conditions.Add($"[Priority] = '{priority}'");
        if (!string.IsNullOrEmpty(type)) conditions.Add($"[TaskType] = '{type}'");
        if (!string.IsNullOrEmpty(sprint)) conditions.Add($"[SprintName] = '{sprint}'");

        TaskTree.FilterString = conditions.Count > 0 ? string.Join(" AND ", conditions) : "";
        TaskCountText.Text = $"{TaskTree.VisibleRowCount} tasks";
    }
}
