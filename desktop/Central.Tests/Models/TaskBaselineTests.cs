using Central.Core.Models;

namespace Central.Tests.Models;

public class TaskBaselineTests
{
    [Fact]
    public void TaskBaseline_Defaults()
    {
        var b = new TaskBaseline();
        Assert.Equal(0, b.Id);
        Assert.Equal(0, b.TaskId);
        Assert.Equal("", b.BaselineName);
        Assert.Null(b.StartDate);
        Assert.Null(b.FinishDate);
        Assert.Null(b.Points);
        Assert.Null(b.Hours);
    }

    [Fact]
    public void TaskBaseline_SetProperties()
    {
        var b = new TaskBaseline
        {
            Id = 1,
            TaskId = 42,
            BaselineName = "Sprint 1 Baseline",
            StartDate = new DateTime(2026, 3, 1),
            FinishDate = new DateTime(2026, 3, 14),
            Points = 13m,
            Hours = 40m,
            SavedAt = new DateTime(2026, 3, 1, 9, 0, 0)
        };
        Assert.Equal(42, b.TaskId);
        Assert.Equal("Sprint 1 Baseline", b.BaselineName);
        Assert.Equal(13m, b.Points);
    }

    [Fact]
    public void GanttPredecessorLink_Defaults()
    {
        var l = new GanttPredecessorLink();
        Assert.Equal(0, l.PredecessorTaskId);
        Assert.Equal(0, l.SuccessorTaskId);
        Assert.Equal(0, l.LinkType);
        Assert.Equal(0, l.Lag);
    }

    [Fact]
    public void GanttPredecessorLink_FinishToStart()
    {
        var l = new GanttPredecessorLink { LinkType = 0 };
        Assert.Equal(0, l.LinkType);
    }

    [Fact]
    public void GanttPredecessorLink_StartToFinish()
    {
        var l = new GanttPredecessorLink { LinkType = 3 };
        Assert.Equal(3, l.LinkType);
    }

    [Fact]
    public void GanttPredecessorLink_WithLag()
    {
        var l = new GanttPredecessorLink
        {
            PredecessorTaskId = 1,
            SuccessorTaskId = 2,
            LinkType = 0,
            Lag = 3
        };
        Assert.Equal(3, l.Lag);
    }

    [Fact]
    public void SprintBurndownPoint_Defaults()
    {
        var p = new SprintBurndownPoint();
        Assert.Equal(0, p.SprintId);
        Assert.Equal(0m, p.PointsRemaining);
        Assert.Equal(0m, p.HoursRemaining);
        Assert.Equal(0m, p.PointsCompleted);
        Assert.Equal(0m, p.HoursCompleted);
        Assert.Null(p.IdealPoints);
        Assert.Null(p.IdealHours);
    }

    [Fact]
    public void SprintAllocation_PropertyChanged()
    {
        var a = new SprintAllocation();
        var changed = new List<string>();
        a.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        a.SprintId = 1;
        a.UserId = 42;
        a.UserName = "John";
        a.CapacityHours = 40m;
        a.CapacityPoints = 20m;

        Assert.Contains("SprintId", changed);
        Assert.Contains("UserId", changed);
        Assert.Contains("UserName", changed);
        Assert.Contains("CapacityHours", changed);
        Assert.Contains("CapacityPoints", changed);
    }
}
