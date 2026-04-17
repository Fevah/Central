using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Central.Engine.Models;

namespace Central.Module.Projects;

/// <summary>View model for a single Kanban column with its cards.</summary>
public class KanbanColumnVM : INotifyPropertyChanged
{
    public BoardColumn Column { get; set; } = null!;
    public string ColumnName => Column.ColumnName;
    public string WipDisplay => Column.WipDisplay;
    public bool IsOverWip => Column.IsOverWip;
    public ObservableCollection<TaskItem> Cards { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;
    public void Refresh()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(WipDisplay)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsOverWip)));
    }
}

public partial class KanbanBoardPanel : System.Windows.Controls.UserControl
{
    private readonly ObservableCollection<KanbanColumnVM> _columns = new();

    public KanbanBoardPanel()
    {
        InitializeComponent();
        ColumnsHost.ItemsSource = _columns;
        SwimLaneSelector.ItemsSource = new[] { "(None)", "Assigned To", "Priority", "Type" };
    }

    // Events
    public event Func<int?, Task>? ProjectChanged;
    public event Func<int, string, string?, Task>? CardMoved; // taskId, columnName, statusMapping
    public event Action<TaskItem>? CardDoubleClicked;
    public event Action? AddCardRequested;
    public event Action? ConfigRequested;

    private List<BoardColumn> _boardColumns = new();
    private List<TaskItem> _allTasks = new();

    public void SetProjects(IEnumerable<TaskProject> projects)
        => ProjectSelector.ItemsSource = projects;

    public int? SelectedProjectId =>
        ProjectSelector.EditValue is TaskProject p && p.Id > 0 ? p.Id : null;

    /// <summary>Load the board with columns and tasks.</summary>
    public void LoadBoard(List<BoardColumn> columns, List<TaskItem> tasks)
    {
        _boardColumns = columns;
        _allTasks = tasks;
        RebuildBoard();
    }

    private void RebuildBoard()
    {
        _columns.Clear();
        foreach (var col in _boardColumns.OrderBy(c => c.SortOrder))
        {
            var vm = new KanbanColumnVM { Column = col };
            // Match tasks to column: by board_column name, or by status mapping if board_column is empty
            var matched = _allTasks.Where(t =>
                (!string.IsNullOrEmpty(t.BoardColumn) && t.BoardColumn == col.ColumnName) ||
                (string.IsNullOrEmpty(t.BoardColumn) && t.Status == col.StatusMapping))
                .OrderBy(t => t.SprintPriority).ThenBy(t => t.BacklogPriority);
            foreach (var task in matched)
                vm.Cards.Add(task);
            col.CurrentCount = vm.Cards.Count;
            vm.Refresh();
            _columns.Add(vm);
        }
    }

    // ── Drag and Drop ──

    private TaskItem? _draggedTask;

    private void Card_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is TaskItem task)
        {
            if (e.ClickCount >= 2)
            {
                // Double-click → show detail
                CardDoubleClicked?.Invoke(task);
                e.Handled = true;
                return;
            }
            _draggedTask = task;
            System.Windows.DragDrop.DoDragDrop(fe, new System.Windows.DataObject("TaskItem", task), System.Windows.DragDropEffects.Move);
        }
    }

    private void Column_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent("TaskItem") ? System.Windows.DragDropEffects.Move : System.Windows.DragDropEffects.None;
        e.Handled = true;
    }

    private async void Column_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (!e.Data.GetDataPresent("TaskItem")) return;
        if (sender is not FrameworkElement fe) return;

        var task = e.Data.GetData("TaskItem") as TaskItem;
        if (task == null) return;

        // Find target column from the ListBox Tag
        KanbanColumnVM? targetCol = null;
        var element = fe;
        while (element != null)
        {
            if (element.Tag is KanbanColumnVM col) { targetCol = col; break; }
            element = System.Windows.Media.VisualTreeHelper.GetParent(element) as FrameworkElement;
        }
        if (targetCol == null) return;

        // Remove from old column
        foreach (var col in _columns)
            col.Cards.Remove(task);

        // Update task
        task.BoardColumn = targetCol.Column.ColumnName;
        if (!string.IsNullOrEmpty(targetCol.Column.StatusMapping))
            task.Status = targetCol.Column.StatusMapping;

        // Add to new column
        targetCol.Cards.Add(task);

        // Update counts
        foreach (var col in _columns)
        {
            col.Column.CurrentCount = col.Cards.Count;
            col.Refresh();
        }

        // Persist
        if (CardMoved != null)
            await CardMoved(task.Id, targetCol.Column.ColumnName, targetCol.Column.StatusMapping);
    }

    // ── Events ──

    private void ProjectSelector_Changed(object sender, DevExpress.Xpf.Editors.EditValueChangedEventArgs e)
        => ProjectChanged?.Invoke(SelectedProjectId);

    private void SwimLane_Changed(object sender, DevExpress.Xpf.Editors.EditValueChangedEventArgs e)
    {
        // Future: group cards by swim lane field
        RebuildBoard();
    }

    private void AddCard_Click(object sender, RoutedEventArgs e) => AddCardRequested?.Invoke();
    private void Config_Click(object sender, RoutedEventArgs e) => ConfigRequested?.Invoke();
}
