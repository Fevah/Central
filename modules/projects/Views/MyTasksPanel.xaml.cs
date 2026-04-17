using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Central.Engine.Models;

namespace Central.Module.Projects;

public partial class MyTasksPanel : System.Windows.Controls.UserControl
{
    public MyTasksPanel()
    {
        InitializeComponent();
        GroupBySelector.ItemsSource = new[] { "(None)", "Project", "Due Date", "Priority", "Status" };
        StatusFilter.ItemsSource = new[] { "", "Open", "InProgress", "Review", "Done", "Blocked" };
    }

    public DevExpress.Xpf.Grid.GridControl Grid => MyTasksGrid;
    public DevExpress.Xpf.Grid.TableView View => MyTasksView;

    public event Func<TaskItem, Task>? SaveTask;

    private void GroupBy_Changed(object sender, DevExpress.Xpf.Editors.EditValueChangedEventArgs e)
    {
        var groupBy = GroupBySelector.EditValue as string ?? "";
        MyTasksGrid.ClearGrouping();
        var field = groupBy switch
        {
            "Project" => "ProjectName",
            "Priority" => "Priority",
            "Status" => "Status",
            "Due Date" => "DueDateDisplay",
            _ => null
        };
        if (field != null)
        {
            var col = MyTasksGrid.Columns[field];
            if (col != null) MyTasksGrid.GroupBy(col);
        }
    }

    private void Filter_Changed(object sender, DevExpress.Xpf.Editors.EditValueChangedEventArgs e)
    {
        var status = StatusFilter.EditValue as string ?? "";
        MyTasksGrid.FilterString = !string.IsNullOrEmpty(status) ? $"[Status] = '{status}'" : "";
        MyTaskCountText.Text = $"{MyTasksGrid.VisibleRowCount} tasks";
    }

    private async void MyTasksView_ValidateRow(object sender, DevExpress.Xpf.Grid.GridRowValidationEventArgs e)
    {
        if (e.Row is TaskItem task && SaveTask != null)
        {
            try { await SaveTask(task); }
            catch (Exception ex) { e.ErrorContent = ex.Message; e.IsValid = false; }
        }
    }

    private void MyTasksView_InvalidRowException(object sender, DevExpress.Xpf.Grid.InvalidRowExceptionEventArgs e)
        => e.ExceptionMode = DevExpress.Xpf.Grid.ExceptionMode.NoAction;
}
