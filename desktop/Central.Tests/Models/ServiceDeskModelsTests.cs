using Central.Core.Models;

namespace Central.Tests.Models;

public class ServiceDeskModelsTests
{
    // ─── SdRequest ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Resolved", true)]
    [InlineData("Closed",   true)]
    [InlineData("Open",     false)]
    [InlineData("In Progress", false)]
    public void SdRequest_IsClosed_MapsFromStatus(string status, bool expected)
    {
        Assert.Equal(expected, new SdRequest { Status = status }.IsClosed);
    }

    [Theory]
    [InlineData("Open",            "#3B82F6")]
    [InlineData("In Progress",     "#F59E0B")]
    [InlineData("On Hold",         "#8B5CF6")]
    [InlineData("Awaiting Response","#A855F7")]
    [InlineData("Resolved",        "#22C55E")]
    [InlineData("Closed",          "#22C55E")]
    [InlineData("Cancelled",       "#EF4444")]
    [InlineData("Archive",         "#4B5563")]
    [InlineData("Unknown",         "#9CA3AF")]
    public void SdRequest_StatusColor_MapsByStatus(string status, string expected)
    {
        Assert.Equal(expected, new SdRequest { Status = status }.StatusColor);
    }

    [Theory]
    [InlineData("High",   "#EF4444")]
    [InlineData("Urgent", "#EF4444")]
    [InlineData("Medium", "#F59E0B")]
    [InlineData("Normal", "#F59E0B")]
    [InlineData("Low",    "#22C55E")]
    [InlineData("",       "#9CA3AF")]
    public void SdRequest_PriorityColor_MapsByPriority(string priority, string expected)
    {
        Assert.Equal(expected, new SdRequest { Priority = priority }.PriorityColor);
    }

    [Fact]
    public void SdRequest_IsOverdue_WhenPastDueAndOpen()
    {
        var r = new SdRequest { Status = "Open", DueBy = DateTime.UtcNow.AddHours(-1) };
        Assert.True(r.IsOverdue);
    }

    [Fact]
    public void SdRequest_IsOverdue_FalseWhenClosed()
    {
        var r = new SdRequest { Status = "Closed", DueBy = DateTime.UtcNow.AddHours(-1) };
        Assert.False(r.IsOverdue);
    }

    [Fact]
    public void SdRequest_IsOverdue_FalseWhenCancelled()
    {
        var r = new SdRequest { Status = "Cancelled", DueBy = DateTime.UtcNow.AddHours(-1) };
        Assert.False(r.IsOverdue);
    }

    [Fact]
    public void SdRequest_IsOverdue_FalseWhenNoDueBy()
    {
        Assert.False(new SdRequest { Status = "Open", DueBy = null }.IsOverdue);
    }

    [Fact]
    public void SdRequest_OverdueIcon_WarningWhenOverdue()
    {
        var r = new SdRequest { Status = "Open", DueBy = DateTime.UtcNow.AddHours(-1) };
        Assert.Equal("⚠", r.OverdueIcon);
    }

    [Fact]
    public void SdRequest_OverdueIcon_EmptyWhenNotOverdue()
    {
        Assert.Equal("", new SdRequest { Status = "Open", DueBy = DateTime.UtcNow.AddHours(1) }.OverdueIcon);
    }

    [Fact]
    public void SdRequest_RowColor_TransparentByDefault()
    {
        Assert.Equal("#00000000", new SdRequest().RowColor);
    }

    [Fact]
    public void SdRequest_AcceptChanges_ClearsDirtyAndSnapshotsValues()
    {
        var r = new SdRequest { Status = "Open", Priority = "High" };
        r.AcceptChanges();
        Assert.False(r.IsDirty);
        Assert.Equal("Open", r.OriginalStatus);
        Assert.Equal("High", r.OriginalPriority);
    }

    [Fact]
    public void SdRequest_BecomesDirty_WhenTrackedFieldChangesAfterAccept()
    {
        var r = new SdRequest { Status = "Open", Priority = "Low" };
        r.AcceptChanges();
        r.Status = "In Progress";
        Assert.True(r.IsDirty);
        Assert.Equal("#33F59E0B", r.RowColor);
    }

    [Fact]
    public void SdRequest_RevertsToClean_WhenChangedBackToOriginal()
    {
        var r = new SdRequest { Status = "Open" };
        r.AcceptChanges();
        r.Status = "In Progress";
        Assert.True(r.IsDirty);
        r.Status = "Open";
        Assert.False(r.IsDirty);
    }

    [Fact]
    public void SdRequest_StaysClean_BeforeAcceptChanges()
    {
        var r = new SdRequest();
        r.Status = "Open";
        r.Priority = "High";
        Assert.False(r.IsDirty);
    }

    // ─── SdGroupCategory ────────────────────────────────────────────────────

    [Fact]
    public void SdGroupCategory_MemberCount_MatchesList()
    {
        var c = new SdGroupCategory();
        c.Members.Add("Infrastructure");
        c.Members.Add("Networks");
        Assert.Equal(2, c.MemberCount);
    }

    [Fact]
    public void SdGroupCategory_MemberCount_ZeroByDefault()
    {
        Assert.Equal(0, new SdGroupCategory().MemberCount);
    }

    // ─── SdAgingBucket ──────────────────────────────────────────────────────

    [Fact]
    public void SdAgingBucket_Total_SumsAllBuckets()
    {
        var b = new SdAgingBucket { Days0to1 = 1, Days1to2 = 2, Days2to4 = 3, Days4to7 = 4, Days7Plus = 5 };
        Assert.Equal(15, b.Total);
    }

    [Fact]
    public void SdAgingBucket_Total_ZeroWhenEmpty()
    {
        Assert.Equal(0, new SdAgingBucket().Total);
    }

    // ─── SdWeeklyTotal / SdTechDaily day labels ────────────────────────────

    [Fact]
    public void SdWeeklyTotal_DayLabel_FormatsAsShortDayName()
    {
        var monday = new DateTime(2026, 4, 13); // Monday
        var w = new SdWeeklyTotal { Day = monday };
        Assert.Equal("Mon", w.DayLabel);
    }

    [Fact]
    public void SdTechDaily_DayLabel_FormatsAsShortDayName()
    {
        var friday = new DateTime(2026, 4, 17); // Friday
        var t = new SdTechDaily { Day = friday };
        Assert.Equal("Fri", t.DayLabel);
    }
}
