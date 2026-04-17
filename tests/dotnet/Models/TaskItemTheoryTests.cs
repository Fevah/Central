using Central.Engine.Models;

namespace Central.Tests.Models;

/// <summary>Exhaustive Theory tests for all TaskItem status/priority/type combinations.</summary>
public class TaskItemTheoryTests
{
    // ── Status combinations ──

    [Theory]
    [InlineData("Open", "○", "#9CA3AF")]
    [InlineData("InProgress", "◐", "#3B82F6")]
    [InlineData("Review", "◑", "#F59E0B")]
    [InlineData("Done", "●", "#22C55E")]
    [InlineData("Blocked", "✕", "#EF4444")]
    [InlineData("Cancelled", "○", "#9CA3AF")]
    [InlineData("", "○", "#9CA3AF")]
    [InlineData("Pending", "○", "#9CA3AF")]
    public void StatusIcon_And_StatusColor(string status, string expectedIcon, string expectedColor)
    {
        var t = new TaskItem { Status = status };
        Assert.Equal(expectedIcon, t.StatusIcon);
        Assert.Equal(expectedColor, t.StatusColor);
    }

    // ── Priority combinations ──

    [Theory]
    [InlineData("Critical", "▲▲", "#EF4444")]
    [InlineData("High", "▲", "#F59E0B")]
    [InlineData("Medium", "─", "#9CA3AF")]
    [InlineData("Low", "▽", "#3B82F6")]
    [InlineData("None", "─", "#9CA3AF")]
    [InlineData("", "─", "#9CA3AF")]
    [InlineData("Urgent", "─", "#9CA3AF")]
    public void PriorityIcon_And_PriorityColor(string priority, string expectedIcon, string expectedColor)
    {
        var t = new TaskItem { Priority = priority };
        Assert.Equal(expectedIcon, t.PriorityIcon);
        Assert.Equal(expectedColor, t.PriorityColor);
    }

    // ── TaskType combinations ──

    [Theory]
    [InlineData("Epic", "⚡")]
    [InlineData("Story", "📖")]
    [InlineData("Task", "✓")]
    [InlineData("Bug", "🐛")]
    [InlineData("SubTask", "  ↳")]
    [InlineData("Milestone", "◆")]
    [InlineData("Feature", "✓")]
    [InlineData("", "✓")]
    public void TypeIcon_AllTypes(string taskType, string expectedIcon)
    {
        var t = new TaskItem { TaskType = taskType };
        Assert.Equal(expectedIcon, t.TypeIcon);
    }

    // ── Risk combinations ──

    [Theory]
    [InlineData("Critical", "#EF4444")]
    [InlineData("High", "#F59E0B")]
    [InlineData("Medium", "#FBBF24")]
    [InlineData("Low", "#22C55E")]
    [InlineData("None", "#9CA3AF")]
    [InlineData("", "#9CA3AF")]
    public void RiskColor_AllLevels(string risk, string expectedColor)
    {
        var t = new TaskItem { Risk = risk };
        Assert.Equal(expectedColor, t.RiskColor);
    }

    // ── Severity combinations ──

    [Theory]
    [InlineData("Blocker", "#EF4444")]
    [InlineData("Critical", "#EF4444")]
    [InlineData("Major", "#F59E0B")]
    [InlineData("Minor", "#FBBF24")]
    [InlineData("Cosmetic", "#9CA3AF")]
    [InlineData("None", "#9CA3AF")]
    [InlineData("", "#9CA3AF")]
    public void SeverityColor_AllLevels(string severity, string expectedColor)
    {
        var t = new TaskItem { Severity = severity };
        Assert.Equal(expectedColor, t.SeverityColor);
    }

    // ── IsComplete ──

    [Theory]
    [InlineData("Done", true)]
    [InlineData("Open", false)]
    [InlineData("InProgress", false)]
    [InlineData("Review", false)]
    [InlineData("Blocked", false)]
    public void IsComplete_ByStatus(string status, bool expected)
    {
        var t = new TaskItem { Status = status };
        Assert.Equal(expected, t.IsComplete);
    }

    // ── ProgressPercent ──

    [Fact]
    public void ProgressPercent_Done_Returns100()
    {
        var t = new TaskItem { Status = "Done" };
        Assert.Equal(100, t.ProgressPercent);
    }

    [Fact]
    public void ProgressPercent_HalfwayThrough()
    {
        var t = new TaskItem { Status = "InProgress", EstimatedHours = 10, ActualHours = 5 };
        Assert.Equal(50, t.ProgressPercent);
    }

    [Fact]
    public void ProgressPercent_OverEstimate_CapsAt99()
    {
        var t = new TaskItem { Status = "InProgress", EstimatedHours = 10, ActualHours = 20 };
        Assert.Equal(99, t.ProgressPercent);
    }

    [Fact]
    public void ProgressPercent_NoEstimate_Returns0()
    {
        var t = new TaskItem { Status = "InProgress", EstimatedHours = null, ActualHours = 5 };
        Assert.Equal(0, t.ProgressPercent);
    }

    [Fact]
    public void ProgressPercent_NoHours_Returns0()
    {
        var t = new TaskItem { Status = "Open" };
        Assert.Equal(0, t.ProgressPercent);
    }

    // ── PointsDisplay ──

    [Fact]
    public void PointsDisplay_WithPoints()
    {
        var t = new TaskItem { Points = 5 };
        Assert.Equal("5pts", t.PointsDisplay);
    }

    [Fact]
    public void PointsDisplay_NoPoints_Empty()
    {
        var t = new TaskItem { Points = null };
        Assert.Equal("", t.PointsDisplay);
    }

    // ── Date displays ──

    [Fact]
    public void StartDateDisplay_WithDate()
    {
        var t = new TaskItem { StartDate = new DateTime(2026, 6, 1) };
        Assert.Equal("2026-06-01", t.StartDateDisplay);
    }

    [Fact]
    public void StartDateDisplay_Null_Empty()
    {
        var t = new TaskItem { StartDate = null };
        Assert.Equal("", t.StartDateDisplay);
    }

    [Fact]
    public void FinishDateDisplay_WithDate()
    {
        var t = new TaskItem { FinishDate = new DateTime(2026, 6, 30) };
        Assert.Equal("2026-06-30", t.FinishDateDisplay);
    }

    [Fact]
    public void FinishDateDisplay_Null_Empty()
    {
        var t = new TaskItem { FinishDate = null };
        Assert.Equal("", t.FinishDateDisplay);
    }

    // ── PropertyChanged cascades for Phase 1 fields ──

    [Fact]
    public void PropertyChanged_StartDate_NotifiesStartDateDisplay()
    {
        var t = new TaskItem();
        var changed = new List<string>();
        t.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        t.StartDate = DateTime.Today;
        Assert.Contains("StartDateDisplay", changed);
    }

    [Fact]
    public void PropertyChanged_FinishDate_NotifiesFinishDateDisplay()
    {
        var t = new TaskItem();
        var changed = new List<string>();
        t.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        t.FinishDate = DateTime.Today;
        Assert.Contains("FinishDateDisplay", changed);
    }

    // ── CustomValues ──

    [Fact]
    public void CustomValues_DefaultEmpty()
    {
        var t = new TaskItem();
        Assert.NotNull(t.CustomValues);
        Assert.Empty(t.CustomValues);
    }

    [Fact]
    public void CustomValues_CanAdd()
    {
        var t = new TaskItem();
        t.CustomValues["env"] = "Production";
        Assert.Equal("Production", t.CustomValues["env"]);
    }

    // ── Baseline dates ──

    [Fact]
    public void BaselineDates_DefaultNull()
    {
        var t = new TaskItem();
        Assert.Null(t.BaselineStartDate);
        Assert.Null(t.BaselineFinishDate);
    }
}
