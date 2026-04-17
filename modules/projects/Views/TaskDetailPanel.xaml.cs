using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using Central.Engine.Models;

namespace Central.Module.Projects;

public partial class TaskDetailPanel : System.Windows.Controls.UserControl
{
    public TaskDetailPanel() => InitializeComponent();

    public event Func<int, string, Task>? AddCommentRequested;

    public void ShowTask(TaskItem? task, List<TaskComment>? comments = null)
    {
        if (task == null) { DetailContent.Visibility = Visibility.Collapsed; return; }

        DetailContent.Visibility = Visibility.Visible;
        TaskStatusIcon.Text = task.StatusIcon;
        TaskStatusIcon.Foreground = new System.Windows.Media.SolidColorBrush(
            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(task.StatusColor));
        TaskTitle.Text = task.Title;
        TaskType.Text = task.TaskType;
        TaskPriority.Text = task.Priority;
        TaskPriorityBorder.Background = new System.Windows.Media.SolidColorBrush(
            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(task.PriorityColor));
        TaskBuilding.Text = task.Building ?? "";
        TaskAssigned.Text = task.AssignedToName ?? "Unassigned";
        TaskCreatedBy.Text = task.CreatedByName ?? "";
        TaskDueDate.Text = task.DueDateDisplay;
        TaskHours.Text = task.ProgressDisplay;
        TaskTags.Text = task.Tags ?? "";
        TaskCreatedAt.Text = task.CreatedAt.ToString("yyyy-MM-dd HH:mm");
        TaskDescription.Text = task.Description ?? "";

        CommentsList.ItemsSource = comments ?? new List<TaskComment>();
    }

    private async void AddComment_Click(object sender, RoutedEventArgs e)
    {
        var text = NewCommentBox.Text?.Trim();
        if (string.IsNullOrEmpty(text)) return;
        if (AddCommentRequested != null)
        {
            var taskId = 0; // Host passes the current task ID
            await AddCommentRequested(taskId, text);
            NewCommentBox.Text = "";
        }
    }
}
