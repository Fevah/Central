using Central.Engine.Models;

namespace Central.Tests.Models;

/// <summary>Additional edge-case tests for TaskItem properties not covered in TaskModelsTests.</summary>
public class TaskItemEdgeCaseTests
{
    [Fact]
    public void StatusColor_Defaults_ForUnknown()
    {
        var t = new TaskItem { Status = "UnknownStatus" };
        Assert.Equal("#9CA3AF", t.StatusColor);
        Assert.Equal("○", t.StatusIcon);
    }

    [Fact]
    public void PriorityIcon_Unknown_ReturnsDash()
    {
        var t = new TaskItem { Priority = "Unknown" };
        Assert.Equal("─", t.PriorityIcon);
    }

    [Fact]
    public void PriorityColor_Unknown_ReturnsGrey()
    {
        var t = new TaskItem { Priority = "Extreme" };
        Assert.Equal("#9CA3AF", t.PriorityColor);
    }

    [Fact]
    public void TypeIcon_SubTask()
    {
        var t = new TaskItem { TaskType = "SubTask" };
        Assert.Equal("  ↳", t.TypeIcon);
    }

    [Fact]
    public void TypeIcon_Task()
    {
        var t = new TaskItem { TaskType = "Task" };
        Assert.Equal("✓", t.TypeIcon);
    }

    [Fact]
    public void TypeIcon_Unknown_DefaultCheckmark()
    {
        var t = new TaskItem { TaskType = "Custom" };
        Assert.Equal("✓", t.TypeIcon);
    }

    [Fact]
    public void RiskColor_Critical()
    {
        var t = new TaskItem { Risk = "Critical" };
        Assert.Equal("#EF4444", t.RiskColor);
    }

    [Fact]
    public void RiskColor_High()
    {
        var t = new TaskItem { Risk = "High" };
        Assert.Equal("#F59E0B", t.RiskColor);
    }

    [Fact]
    public void RiskColor_Medium()
    {
        var t = new TaskItem { Risk = "Medium" };
        Assert.Equal("#FBBF24", t.RiskColor);
    }

    [Fact]
    public void RiskColor_Low()
    {
        var t = new TaskItem { Risk = "Low" };
        Assert.Equal("#22C55E", t.RiskColor);
    }

    [Fact]
    public void RiskColor_Unknown_Grey()
    {
        var t = new TaskItem { Risk = "" };
        Assert.Equal("#9CA3AF", t.RiskColor);
    }

    [Fact]
    public void SeverityColor_Unknown_Grey()
    {
        var t = new TaskItem { Severity = "" };
        Assert.Equal("#9CA3AF", t.SeverityColor);
    }

    [Fact]
    public void IsOverdue_FutureDueDate_False()
    {
        var t = new TaskItem { DueDate = DateTime.Today.AddDays(7), Status = "Open" };
        Assert.False(t.IsOverdue);
    }

    [Fact]
    public void IsOverdue_TodayDueDate_False()
    {
        // Due date is today — should NOT be overdue (< not <=)
        var t = new TaskItem { DueDate = DateTime.Today, Status = "Open" };
        Assert.False(t.IsOverdue);
    }

    [Fact]
    public void ProgressDisplay_WithEstimate()
    {
        var t = new TaskItem { EstimatedHours = 8, ActualHours = 3 };
        Assert.Equal("3/8h", t.ProgressDisplay);
    }

    [Fact]
    public void ProgressDisplay_ActualOnly()
    {
        var t = new TaskItem { ActualHours = 5 };
        Assert.Equal("5h", t.ProgressDisplay);
    }

    [Fact]
    public void ProgressDisplay_NoHours_Empty()
    {
        var t = new TaskItem();
        Assert.Equal("", t.ProgressDisplay);
    }

    [Fact]
    public void DueDateDisplay_Null_Empty()
    {
        var t = new TaskItem { DueDate = null };
        Assert.Equal("", t.DueDateDisplay);
    }

    [Fact]
    public void DueDateDisplay_Formatted()
    {
        var t = new TaskItem { DueDate = new DateTime(2026, 12, 25) };
        Assert.Equal("2026-12-25", t.DueDateDisplay);
    }

    [Fact]
    public void PropertyChanged_Status_AlsoNotifies_StatusIcon_StatusColor_IsComplete()
    {
        var t = new TaskItem();
        var changed = new List<string>();
        t.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        t.Status = "Done";
        Assert.Contains("Status", changed);
        Assert.Contains("StatusIcon", changed);
        Assert.Contains("StatusColor", changed);
        Assert.Contains("IsComplete", changed);
    }

    [Fact]
    public void PropertyChanged_Priority_AlsoNotifies_PriorityIcon_PriorityColor()
    {
        var t = new TaskItem();
        var changed = new List<string>();
        t.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        t.Priority = "High";
        Assert.Contains("PriorityIcon", changed);
        Assert.Contains("PriorityColor", changed);
    }

    [Fact]
    public void PropertyChanged_TaskType_AlsoNotifies_TypeIcon()
    {
        var t = new TaskItem();
        var changed = new List<string>();
        t.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        t.TaskType = "Bug";
        Assert.Contains("TypeIcon", changed);
    }

    [Fact]
    public void PropertyChanged_DueDate_AlsoNotifies_IsOverdue()
    {
        var t = new TaskItem();
        var changed = new List<string>();
        t.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        t.DueDate = DateTime.Today;
        Assert.Contains("IsOverdue", changed);
    }

    [Fact]
    public void PropertyChanged_Risk_AlsoNotifies_RiskColor()
    {
        var t = new TaskItem();
        var changed = new List<string>();
        t.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        t.Risk = "High";
        Assert.Contains("RiskColor", changed);
    }

    [Fact]
    public void PropertyChanged_Severity_AlsoNotifies_SeverityColor()
    {
        var t = new TaskItem();
        var changed = new List<string>();
        t.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        t.Severity = "Major";
        Assert.Contains("SeverityColor", changed);
    }

    [Fact]
    public void Children_DefaultEmpty()
    {
        var t = new TaskItem();
        Assert.NotNull(t.Children);
        Assert.Empty(t.Children);
    }

    [Fact]
    public void Children_CanAddItems()
    {
        var parent = new TaskItem { Title = "Parent" };
        parent.Children.Add(new TaskItem { Title = "Child 1" });
        parent.Children.Add(new TaskItem { Title = "Child 2" });
        Assert.Equal(2, parent.Children.Count);
    }

    [Fact]
    public void TaskComment_Defaults()
    {
        var c = new TaskComment();
        Assert.Equal(0, c.Id);
        Assert.Equal(0, c.TaskId);
        Assert.Null(c.UserId);
        Assert.Equal("", c.UserName);
        Assert.Equal("", c.CommentText);
    }
}
