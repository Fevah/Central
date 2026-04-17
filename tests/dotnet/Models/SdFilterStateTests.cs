using Central.Core.Models;

namespace Central.Tests.Models;

public class SdFilterStateTests
{
    [Fact]
    public void Default_ReturnsThisWeek()
    {
        var f = SdFilterState.Default();
        Assert.Equal("day", f.Bucket);
        Assert.Equal(DayOfWeek.Monday, f.RangeStart.DayOfWeek);
        Assert.Equal(7, (f.RangeEnd - f.RangeStart).TotalDays);
    }

    [Fact]
    public void FormatLabel_Day_ShortRange_ShowsDayAndDate()
    {
        var f = new SdFilterState
        {
            RangeStart = new DateTime(2026, 3, 23),
            RangeEnd = new DateTime(2026, 3, 30),
            Bucket = "day"
        };
        var label = f.FormatLabel(new DateTime(2026, 3, 25));
        Assert.Equal("Wed 25", label);
    }

    [Fact]
    public void FormatLabel_Day_LongRange_ShowsMonthAndDate()
    {
        var f = new SdFilterState
        {
            RangeStart = new DateTime(2026, 1, 1),
            RangeEnd = new DateTime(2026, 4, 1),
            Bucket = "day"
        };
        var label = f.FormatLabel(new DateTime(2026, 2, 15));
        Assert.Equal("Feb 15", label);
    }

    [Fact]
    public void FormatLabel_Week_ShowsWcPrefix()
    {
        var f = new SdFilterState { Bucket = "week" };
        var label = f.FormatLabel(new DateTime(2026, 3, 23));
        Assert.StartsWith("W/C ", label);
    }

    [Fact]
    public void FormatLabel_Month_ShowsMonthYear()
    {
        var f = new SdFilterState { Bucket = "month" };
        var label = f.FormatLabel(new DateTime(2026, 3, 1));
        Assert.Equal("Mar 26", label);
    }

    [Fact]
    public void SelectedTechs_Null_MeansAll()
    {
        var f = SdFilterState.Default();
        Assert.Null(f.SelectedTechs);
    }

    [Fact]
    public void SelectedGroups_Null_MeansAll()
    {
        var f = SdFilterState.Default();
        Assert.Null(f.SelectedGroups);
    }

    [Fact]
    public void Overlays_DefaultTrue()
    {
        var f = SdFilterState.Default();
        Assert.True(f.ShowOpenLine);
        Assert.True(f.ShowResolutionLine);
        Assert.True(f.ShowTargetLine);
    }

    [Fact]
    public void GridOptions_DefaultTrue()
    {
        var f = SdFilterState.Default();
        Assert.True(f.ShowGroupPanel);
        Assert.True(f.ShowAutoFilter);
        Assert.True(f.ShowTotalSummary);
        Assert.True(f.AlternateRows);
    }
}
