using Central.Core.Models;

namespace Central.Tests.Tasks;

public class TaskModelsTests
{
    // ── TaskItem computed properties ──

    [Theory]
    [InlineData("Open", "○")]
    [InlineData("InProgress", "◐")]
    [InlineData("Review", "◑")]
    [InlineData("Done", "●")]
    [InlineData("Blocked", "✕")]
    public void TaskItem_StatusIcon_MapsCorrectly(string status, string expected)
    {
        var task = new TaskItem { Status = status };
        Assert.Equal(expected, task.StatusIcon);
    }

    [Theory]
    [InlineData("Critical", "#EF4444")]
    [InlineData("High", "#F59E0B")]
    [InlineData("Medium", "#9CA3AF")]
    [InlineData("Low", "#3B82F6")]
    public void TaskItem_PriorityColor_MapsCorrectly(string priority, string expected)
    {
        var task = new TaskItem { Priority = priority };
        Assert.Equal(expected, task.PriorityColor);
    }

    [Theory]
    [InlineData("Epic", "⚡")]
    [InlineData("Story", "📖")]
    [InlineData("Bug", "🐛")]
    [InlineData("Milestone", "◆")]
    public void TaskItem_TypeIcon_MapsCorrectly(string type, string expected)
    {
        var task = new TaskItem { TaskType = type };
        Assert.Equal(expected, task.TypeIcon);
    }

    [Fact]
    public void TaskItem_IsComplete_TrueWhenDone()
    {
        var task = new TaskItem { Status = "Done" };
        Assert.True(task.IsComplete);
    }

    [Fact]
    public void TaskItem_IsComplete_FalseWhenOpen()
    {
        var task = new TaskItem { Status = "Open" };
        Assert.False(task.IsComplete);
    }

    [Fact]
    public void TaskItem_IsOverdue_TrueWhenPastDueAndNotDone()
    {
        var task = new TaskItem { DueDate = DateTime.Today.AddDays(-1), Status = "Open" };
        Assert.True(task.IsOverdue);
    }

    [Fact]
    public void TaskItem_IsOverdue_FalseWhenDone()
    {
        var task = new TaskItem { DueDate = DateTime.Today.AddDays(-1), Status = "Done" };
        Assert.False(task.IsOverdue);
    }

    [Fact]
    public void TaskItem_IsOverdue_FalseWhenNoDueDate()
    {
        var task = new TaskItem { Status = "Open" };
        Assert.False(task.IsOverdue);
    }

    [Fact]
    public void TaskItem_ProgressPercent_100WhenDone()
    {
        var task = new TaskItem { Status = "Done" };
        Assert.Equal(100, task.ProgressPercent);
    }

    [Fact]
    public void TaskItem_ProgressPercent_CalculatesFromHours()
    {
        var task = new TaskItem { Status = "InProgress", EstimatedHours = 10, ActualHours = 5 };
        Assert.Equal(50, task.ProgressPercent);
    }

    [Fact]
    public void TaskItem_ProgressPercent_CapsAt99()
    {
        var task = new TaskItem { Status = "InProgress", EstimatedHours = 10, ActualHours = 15 };
        Assert.Equal(99, task.ProgressPercent);
    }

    [Fact]
    public void TaskItem_ProgressPercent_ZeroWithNoHours()
    {
        var task = new TaskItem { Status = "InProgress" };
        Assert.Equal(0, task.ProgressPercent);
    }

    [Theory]
    [InlineData("Blocker", "#EF4444")]
    [InlineData("Critical", "#EF4444")]
    [InlineData("Major", "#F59E0B")]
    [InlineData("Minor", "#FBBF24")]
    [InlineData("Cosmetic", "#9CA3AF")]
    public void TaskItem_SeverityColor_MapsCorrectly(string severity, string expected)
    {
        var task = new TaskItem { Severity = severity };
        Assert.Equal(expected, task.SeverityColor);
    }

    [Fact]
    public void TaskItem_PointsDisplay_FormatsCorrectly()
    {
        var task = new TaskItem { Points = 5.0m };
        Assert.Equal("5.0pts", task.PointsDisplay);
    }

    [Fact]
    public void TaskItem_PointsDisplay_EmptyWhenNull()
    {
        var task = new TaskItem();
        Assert.Equal("", task.PointsDisplay);
    }

    [Fact]
    public void TaskItem_CustomValues_DefaultEmpty()
    {
        var task = new TaskItem();
        Assert.NotNull(task.CustomValues);
        Assert.Empty(task.CustomValues);
    }

    [Fact]
    public void TaskItem_DateDisplays_FormatsCorrectly()
    {
        var task = new TaskItem { StartDate = new DateTime(2026, 3, 15), FinishDate = new DateTime(2026, 4, 1) };
        Assert.Equal("2026-03-15", task.StartDateDisplay);
        Assert.Equal("2026-04-01", task.FinishDateDisplay);
    }

    [Fact]
    public void TaskItem_DateDisplays_EmptyWhenNull()
    {
        var task = new TaskItem();
        Assert.Equal("", task.StartDateDisplay);
        Assert.Equal("", task.FinishDateDisplay);
    }

    // ── Portfolio / Programme / Project ──

    [Fact]
    public void TaskProject_DisplayName_ShowsArchived()
    {
        var p = new TaskProject { Name = "Test", Archived = true };
        Assert.Equal("Test (archived)", p.DisplayName);
    }

    [Fact]
    public void TaskProject_DisplayName_PlainWhenActive()
    {
        var p = new TaskProject { Name = "Test", Archived = false };
        Assert.Equal("Test", p.DisplayName);
    }

    [Fact]
    public void Sprint_DateRange_FormatsCorrectly()
    {
        var s = new Sprint { StartDate = new DateTime(2026, 3, 1), EndDate = new DateTime(2026, 3, 14) };
        Assert.Equal("2026-03-01 → 2026-03-14", s.DateRange);
    }

    [Fact]
    public void Sprint_DateRange_EmptyWhenNoDates()
    {
        var s = new Sprint();
        Assert.Equal("", s.DateRange);
    }

    [Fact]
    public void Sprint_DisplayName_IncludesStatus()
    {
        var s = new Sprint { Name = "Sprint 1", Status = "Active" };
        Assert.Equal("Sprint 1 (Active)", s.DisplayName);
    }

    // ── TaskLink / TaskDependency ──

    [Fact]
    public void TaskLink_LinkDisplay_FormatsCorrectly()
    {
        var link = new TaskLink { LinkType = "blocks", TargetTitle = "Deploy to Prod" };
        Assert.Equal("blocks: Deploy to Prod", link.LinkDisplay);
    }

    [Fact]
    public void TaskDependency_DepDisplay_WithLag()
    {
        var dep = new TaskDependency { DepType = "FS", LagDays = 2, PredecessorTitle = "Design" };
        Assert.Equal("FS+2d: Design", dep.DepDisplay);
    }

    [Fact]
    public void TaskDependency_DepDisplay_NoLag()
    {
        var dep = new TaskDependency { DepType = "FF", LagDays = 0, PredecessorTitle = "Test" };
        Assert.Equal("FF: Test", dep.DepDisplay);
    }

    // ── BoardColumn ──

    [Fact]
    public void BoardColumn_IsOverWip_TrueWhenExceeded()
    {
        var col = new BoardColumn { WipLimit = 3, CurrentCount = 5 };
        Assert.True(col.IsOverWip);
    }

    [Fact]
    public void BoardColumn_IsOverWip_FalseWhenUnder()
    {
        var col = new BoardColumn { WipLimit = 5, CurrentCount = 3 };
        Assert.False(col.IsOverWip);
    }

    [Fact]
    public void BoardColumn_IsOverWip_FalseWhenNoLimit()
    {
        var col = new BoardColumn { WipLimit = null, CurrentCount = 100 };
        Assert.False(col.IsOverWip);
    }

    [Fact]
    public void BoardColumn_WipDisplay_WithLimit()
    {
        var col = new BoardColumn { WipLimit = 5, CurrentCount = 3 };
        Assert.Equal("3/5", col.WipDisplay);
    }

    [Fact]
    public void BoardColumn_WipDisplay_NoLimit()
    {
        var col = new BoardColumn { WipLimit = null, CurrentCount = 7 };
        Assert.Equal("7", col.WipDisplay);
    }

    // ── CustomColumn ──

    [Fact]
    public void CustomColumn_GetDropListOptions_ParsesJson()
    {
        var col = new CustomColumn { Config = """{"options": ["High", "Medium", "Low"]}""" };
        var opts = col.GetDropListOptions();
        Assert.Equal(3, opts.Length);
        Assert.Equal("High", opts[0]);
    }

    [Fact]
    public void CustomColumn_GetDropListOptions_EmptyOnInvalidJson()
    {
        var col = new CustomColumn { Config = "not json" };
        Assert.Empty(col.GetDropListOptions());
    }

    [Fact]
    public void CustomColumn_GetDropListOptions_EmptyOnNull()
    {
        var col = new CustomColumn { Config = "" };
        Assert.Empty(col.GetDropListOptions());
    }

    [Fact]
    public void CustomColumn_GetAggregationType_ParsesJson()
    {
        var col = new CustomColumn { Config = """{"aggregation": "Sum"}""" };
        Assert.Equal("Sum", col.GetAggregationType());
    }

    [Fact]
    public void CustomColumn_GetAggregationType_NullOnMissing()
    {
        var col = new CustomColumn { Config = """{"options": []}""" };
        Assert.Null(col.GetAggregationType());
    }

    // ── TaskCustomValue ──

    [Theory]
    [InlineData("Text", "hello", null, null, "hello")]
    [InlineData("Number", null, 42.5, null, "42.50")]
    [InlineData("Hours", null, 8.0, null, "8.00")]
    public void TaskCustomValue_DisplayValue_FormatsCorrectly(string type, string? text, double? num, string? dateStr, string expected)
    {
        var v = new TaskCustomValue
        {
            ColumnType = type,
            ValueText = text,
            ValueNumber = num.HasValue ? (decimal)num.Value : null,
        };
        Assert.Equal(expected, v.DisplayValue);
    }

    // ── TimeEntry ──

    [Fact]
    public void TimeEntry_EntryDateDisplay_FormatsCorrectly()
    {
        var e = new TimeEntry { EntryDate = new DateTime(2026, 3, 29) };
        Assert.Equal("2026-03-29", e.EntryDateDisplay);
    }

    // ── ActivityFeedItem ──

    [Theory]
    [InlineData("created", "+")]
    [InlineData("status_changed", "→")]
    [InlineData("assigned", "👤")]
    [InlineData("deleted", "✕")]
    public void ActivityFeedItem_ActionIcon_MapsCorrectly(string action, string expected)
    {
        var item = new ActivityFeedItem { Action = action };
        Assert.Equal(expected, item.ActionIcon);
    }

    // ── ReportFilter ──

    [Fact]
    public void ReportFilter_Defaults()
    {
        var f = new ReportFilter();
        Assert.Equal("=", f.Operator);
        Assert.Equal("AND", f.Logic);
    }

    // ── SavedReport ──

    [Fact]
    public void SavedReport_DisplayPath_WithFolder()
    {
        var r = new SavedReport { Folder = "Team", Name = "Sprint Report" };
        Assert.Equal("Team/Sprint Report", r.DisplayPath);
    }

    [Fact]
    public void SavedReport_DisplayPath_NoFolder()
    {
        var r = new SavedReport { Folder = "", Name = "Quick Report" };
        Assert.Equal("Quick Report", r.DisplayPath);
    }

    // ── GanttPredecessorLink ──

    [Fact]
    public void GanttPredecessorLink_DefaultFinishToStart()
    {
        var link = new GanttPredecessorLink();
        Assert.Equal(0, link.LinkType); // 0 = FinishToStart
    }
}
