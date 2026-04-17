using Central.Engine.Models;

namespace Central.Tests.Models;

public class SdFilterStateExtendedTests
{
    [Fact]
    public void FormatLabel_Month_Bucket()
    {
        var fs = new SdFilterState { Bucket = "month" };
        var label = fs.FormatLabel(new DateTime(2026, 3, 1));
        Assert.Equal("Mar 26", label);
    }

    [Fact]
    public void FormatLabel_Week_Bucket()
    {
        var fs = new SdFilterState { Bucket = "week" };
        var label = fs.FormatLabel(new DateTime(2026, 3, 30));
        Assert.Contains("W/C", label);
        Assert.Contains("Mar", label);
    }

    [Fact]
    public void FormatLabel_Day_ShortRange_DayName()
    {
        var fs = new SdFilterState
        {
            Bucket = "day",
            RangeStart = new DateTime(2026, 3, 23),
            RangeEnd = new DateTime(2026, 3, 30) // 7 days
        };
        var label = fs.FormatLabel(new DateTime(2026, 3, 25)); // Wednesday
        Assert.Contains("Wed", label);
    }

    [Fact]
    public void FormatLabel_Day_LongRange_MonthDay()
    {
        var fs = new SdFilterState
        {
            Bucket = "day",
            RangeStart = new DateTime(2026, 1, 1),
            RangeEnd = new DateTime(2026, 3, 1) // > 14 days
        };
        var label = fs.FormatLabel(new DateTime(2026, 1, 15));
        Assert.Contains("Jan", label);
    }

    [Fact]
    public void Default_SetsCorrectWeek()
    {
        var fs = SdFilterState.Default();
        Assert.Equal("day", fs.Bucket);
        Assert.True(fs.RangeEnd > fs.RangeStart);
        Assert.Equal(7, (fs.RangeEnd - fs.RangeStart).TotalDays);
    }

    [Fact]
    public void Default_AllDisplayOptions_HaveDefaults()
    {
        var fs = new SdFilterState();
        Assert.True(fs.ShowOpenLine);
        Assert.True(fs.ShowResolutionLine);
        Assert.False(fs.ShowTotalCreatedLine);
        Assert.True(fs.ShowTargetLine);
        Assert.True(fs.ShowKpiCards);
        Assert.False(fs.ShowBarLabels);
    }

    [Fact]
    public void Default_GridOptions_HaveDefaults()
    {
        var fs = new SdFilterState();
        Assert.True(fs.ShowGroupPanel);
        Assert.True(fs.ShowAutoFilter);
        Assert.True(fs.ShowTotalSummary);
        Assert.True(fs.AlternateRows);
        Assert.True(fs.ShowSearchPanel);
        Assert.True(fs.ShowFilterPanel);
    }
}
