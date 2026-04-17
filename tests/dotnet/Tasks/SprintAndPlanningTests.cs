using Central.Engine.Models;

namespace Central.Tests.Tasks;

public class SprintAndPlanningTests
{
    // ── SprintAllocation ──

    [Fact]
    public void SprintAllocation_PropertyChanged_Fires()
    {
        var alloc = new SprintAllocation();
        var changed = new List<string>();
        alloc.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        alloc.CapacityHours = 40;
        alloc.CapacityPoints = 20;
        Assert.Contains("CapacityHours", changed);
        Assert.Contains("CapacityPoints", changed);
    }

    // ── SprintBurndownPoint ──

    [Fact]
    public void SprintBurndownPoint_Defaults()
    {
        var p = new SprintBurndownPoint();
        Assert.Equal(0m, p.PointsRemaining);
        Assert.Equal(0m, p.HoursRemaining);
        Assert.Null(p.IdealPoints);
    }

    // ── BoardColumn computed ──

    [Fact]
    public void BoardColumn_HeaderDisplay_WithWip()
    {
        var col = new BoardColumn { ColumnName = "In Progress", WipLimit = 5, CurrentCount = 3 };
        Assert.Equal("In Progress (3/5)", col.HeaderDisplay);
    }

    [Fact]
    public void BoardColumn_HeaderDisplay_NoWip()
    {
        var col = new BoardColumn { ColumnName = "Backlog", WipLimit = null, CurrentCount = 12 };
        Assert.Equal("Backlog (12)", col.HeaderDisplay);
    }

    [Fact]
    public void BoardColumn_PropertyChanged_CascadesToComputed()
    {
        var col = new BoardColumn { WipLimit = 3 };
        var changed = new List<string>();
        col.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        col.CurrentCount = 5;
        Assert.Contains("CurrentCount", changed);
        Assert.Contains("IsOverWip", changed);
        Assert.Contains("WipDisplay", changed);
    }

    // ── BoardLane ──

    [Fact]
    public void BoardLane_Defaults()
    {
        var lane = new BoardLane();
        Assert.Equal("Default", lane.BoardName);
        Assert.Equal("", lane.LaneName);
    }

    // ── TaskBaseline ──

    [Fact]
    public void TaskBaseline_Defaults()
    {
        var b = new TaskBaseline();
        Assert.Equal("", b.BaselineName);
        Assert.Null(b.StartDate);
        Assert.Null(b.FinishDate);
    }

    // ── GanttPredecessorLink ──

    [Fact]
    public void GanttPredecessorLink_FinishToStartIsZero()
    {
        var link = new GanttPredecessorLink { PredecessorTaskId = 1, SuccessorTaskId = 2, LinkType = 0 };
        Assert.Equal(0, link.LinkType); // FS
        Assert.Equal(0, link.Lag);
    }

    // ── TaskViewConfig ──

    [Fact]
    public void TaskViewConfig_Defaults()
    {
        var v = new TaskViewConfig();
        Assert.Equal("Tree", v.ViewType);
        Assert.Equal("{}", v.ConfigJson);
        Assert.False(v.IsDefault);
    }

    // ── TimeEntry ──

    [Fact]
    public void TimeEntry_PropertyChanged_Fires()
    {
        var entry = new TimeEntry();
        var changed = new List<string>();
        entry.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        entry.Hours = 4.5m;
        entry.ActivityType = "Testing";
        Assert.Contains("Hours", changed);
        Assert.Contains("ActivityType", changed);
    }

    // ── ActivityFeedItem ──

    [Fact]
    public void ActivityFeedItem_TimeAgo_JustNow()
    {
        var item = new ActivityFeedItem { CreatedAt = DateTime.UtcNow };
        Assert.Equal("just now", item.TimeAgo);
    }

    [Fact]
    public void ActivityFeedItem_TimeAgo_Minutes()
    {
        var item = new ActivityFeedItem { CreatedAt = DateTime.UtcNow.AddMinutes(-15) };
        Assert.Equal("15m ago", item.TimeAgo);
    }

    [Fact]
    public void ActivityFeedItem_TimeAgo_Hours()
    {
        var item = new ActivityFeedItem { CreatedAt = DateTime.UtcNow.AddHours(-3) };
        Assert.Equal("3h ago", item.TimeAgo);
    }

    [Fact]
    public void ActivityFeedItem_TimeAgo_Days()
    {
        var item = new ActivityFeedItem { CreatedAt = DateTime.UtcNow.AddDays(-5) };
        Assert.Equal("5d ago", item.TimeAgo);
    }

    // ── ReportQuery ──

    [Fact]
    public void ReportQuery_Defaults()
    {
        var q = new ReportQuery();
        Assert.Equal("task", q.EntityType);
        Assert.Equal("ASC", q.SortDirection);
        Assert.Empty(q.Columns);
        Assert.Empty(q.Filters);
    }

    // ── DashboardTile ──

    [Fact]
    public void DashboardTile_Defaults()
    {
        var t = new DashboardTile();
        Assert.Equal("Bar", t.ChartType);
        Assert.Equal(1, t.RowSpan);
        Assert.Equal(1, t.ColSpan);
        Assert.Equal("#3B82F6", t.Color);
    }

    // ── Dashboard ──

    [Fact]
    public void Dashboard_PropertyChanged()
    {
        var d = new Dashboard();
        var changed = new List<string>();
        d.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        d.Name = "Sprint Health";
        d.Template = "sprint_health";
        Assert.Contains("Name", changed);
        Assert.Contains("Template", changed);
    }

    // ── Portfolio / Programme ──

    [Fact]
    public void Portfolio_PropertyChanged()
    {
        var p = new Portfolio();
        var changed = new List<string>();
        p.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        p.Archived = true;
        Assert.Contains("Archived", changed);
    }

    [Fact]
    public void Programme_Defaults()
    {
        var p = new Programme();
        Assert.Equal("", p.Name);
        Assert.Null(p.PortfolioId);
    }

    // ── ProjectMember ──

    [Fact]
    public void ProjectMember_DefaultRole()
    {
        var m = new ProjectMember();
        Assert.Equal("Member", m.Role);
    }

    // ── Release ──

    [Fact]
    public void Release_DefaultStatus()
    {
        var r = new Release();
        Assert.Equal("Planned", r.Status);
    }
}
