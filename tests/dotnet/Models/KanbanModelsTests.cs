using Central.Engine.Models;

namespace Central.Tests.Models;

public class KanbanModelsTests
{
    // ── BoardColumn ──

    [Fact]
    public void BoardColumn_Defaults()
    {
        var bc = new BoardColumn();
        Assert.Equal("Default", bc.BoardName);
        Assert.Equal("", bc.ColumnName);
        Assert.Null(bc.WipLimit);
        Assert.Equal(0, bc.CurrentCount);
    }

    [Fact]
    public void BoardColumn_IsOverWip_NoLimit_False()
    {
        var bc = new BoardColumn { WipLimit = null, CurrentCount = 100 };
        Assert.False(bc.IsOverWip);
    }

    [Fact]
    public void BoardColumn_IsOverWip_UnderLimit_False()
    {
        var bc = new BoardColumn { WipLimit = 5, CurrentCount = 3 };
        Assert.False(bc.IsOverWip);
    }

    [Fact]
    public void BoardColumn_IsOverWip_AtLimit_False()
    {
        var bc = new BoardColumn { WipLimit = 5, CurrentCount = 5 };
        Assert.False(bc.IsOverWip);
    }

    [Fact]
    public void BoardColumn_IsOverWip_OverLimit_True()
    {
        var bc = new BoardColumn { WipLimit = 5, CurrentCount = 6 };
        Assert.True(bc.IsOverWip);
    }

    [Fact]
    public void BoardColumn_WipDisplay_WithLimit()
    {
        var bc = new BoardColumn { WipLimit = 10, CurrentCount = 7 };
        Assert.Equal("7/10", bc.WipDisplay);
    }

    [Fact]
    public void BoardColumn_WipDisplay_NoLimit()
    {
        var bc = new BoardColumn { WipLimit = null, CurrentCount = 3 };
        Assert.Equal("3", bc.WipDisplay);
    }

    [Fact]
    public void BoardColumn_HeaderDisplay_WithLimit()
    {
        var bc = new BoardColumn { ColumnName = "In Progress", WipLimit = 5, CurrentCount = 3 };
        Assert.Equal("In Progress (3/5)", bc.HeaderDisplay);
    }

    [Fact]
    public void BoardColumn_HeaderDisplay_NoLimit()
    {
        var bc = new BoardColumn { ColumnName = "Done", WipLimit = null, CurrentCount = 12 };
        Assert.Equal("Done (12)", bc.HeaderDisplay);
    }

    [Fact]
    public void BoardColumn_PropertyChanged_WipLimit_NotifiesRelated()
    {
        var bc = new BoardColumn();
        var changed = new List<string>();
        bc.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        bc.WipLimit = 5;
        Assert.Contains("WipLimit", changed);
        Assert.Contains("IsOverWip", changed);
        Assert.Contains("WipDisplay", changed);
    }

    [Fact]
    public void BoardColumn_PropertyChanged_CurrentCount_NotifiesRelated()
    {
        var bc = new BoardColumn();
        var changed = new List<string>();
        bc.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        bc.CurrentCount = 3;
        Assert.Contains("CurrentCount", changed);
        Assert.Contains("IsOverWip", changed);
        Assert.Contains("WipDisplay", changed);
    }

    // ── BoardLane ──

    [Fact]
    public void BoardLane_Defaults()
    {
        var bl = new BoardLane();
        Assert.Equal("Default", bl.BoardName);
        Assert.Equal("", bl.LaneName);
        Assert.Equal("", bl.LaneField);
    }

    [Fact]
    public void BoardLane_PropertyChanged_Fires()
    {
        var bl = new BoardLane();
        var changed = new List<string>();
        bl.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        bl.LaneName = "Priority";
        Assert.Contains("LaneName", changed);
    }
}
