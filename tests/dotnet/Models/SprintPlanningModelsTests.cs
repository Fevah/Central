using Central.Engine.Models;

namespace Central.Tests.Models;

public class SprintPlanningModelsTests
{
    // ── SprintAllocation ──

    [Fact]
    public void SprintAllocation_Defaults()
    {
        var sa = new SprintAllocation();
        Assert.Equal(0, sa.SprintId);
        Assert.Equal(0, sa.UserId);
        Assert.Equal("", sa.UserName);
        Assert.Null(sa.CapacityHours);
        Assert.Null(sa.CapacityPoints);
    }

    [Fact]
    public void SprintAllocation_PropertyChanged_Fires()
    {
        var sa = new SprintAllocation();
        var changed = new List<string>();
        sa.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        sa.CapacityHours = 40m;
        Assert.Contains("CapacityHours", changed);
    }

    [Fact]
    public void SprintAllocation_SetAllProperties()
    {
        var sa = new SprintAllocation
        {
            Id = 1, SprintId = 2, UserId = 3, UserName = "Dev1",
            CapacityHours = 32m, CapacityPoints = 20m
        };
        Assert.Equal(1, sa.Id);
        Assert.Equal(2, sa.SprintId);
        Assert.Equal(3, sa.UserId);
        Assert.Equal("Dev1", sa.UserName);
        Assert.Equal(32m, sa.CapacityHours);
        Assert.Equal(20m, sa.CapacityPoints);
    }

    // ── SprintBurndownPoint ──

    [Fact]
    public void SprintBurndownPoint_Defaults()
    {
        var bp = new SprintBurndownPoint();
        Assert.Equal(0, bp.PointsRemaining);
        Assert.Equal(0, bp.HoursRemaining);
        Assert.Null(bp.IdealPoints);
        Assert.Null(bp.IdealHours);
    }

    [Fact]
    public void SprintBurndownPoint_SetAllProperties()
    {
        var bp = new SprintBurndownPoint
        {
            Id = 1, SprintId = 2, SnapshotDate = new DateTime(2026, 1, 5),
            PointsRemaining = 30m, HoursRemaining = 60m,
            PointsCompleted = 10m, HoursCompleted = 20m,
            IdealPoints = 25m, IdealHours = 50m
        };
        Assert.Equal(30m, bp.PointsRemaining);
        Assert.Equal(60m, bp.HoursRemaining);
        Assert.Equal(10m, bp.PointsCompleted);
        Assert.Equal(25m, bp.IdealPoints);
    }
}
