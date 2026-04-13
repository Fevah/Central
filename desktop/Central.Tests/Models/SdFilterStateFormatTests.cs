using Central.Core.Models;

namespace Central.Tests.Models;

public class SdFilterStateFormatTests
{
    // ── FormatLabel — day bucket ──

    [Fact]
    public void FormatLabel_DayBucket_ShortRange_ShowsDayOfWeek()
    {
        var state = new SdFilterState
        {
            Bucket = "day",
            RangeStart = new DateTime(2026, 3, 23), // Mon
            RangeEnd = new DateTime(2026, 3, 30)     // 7 days = <=14
        };
        var label = state.FormatLabel(new DateTime(2026, 3, 25)); // Wed
        Assert.Equal("Wed 25", label);
    }

    [Fact]
    public void FormatLabel_DayBucket_LongRange_ShowsMonthDay()
    {
        var state = new SdFilterState
        {
            Bucket = "day",
            RangeStart = new DateTime(2026, 3, 1),
            RangeEnd = new DateTime(2026, 3, 31) // 30 days > 14
        };
        var label = state.FormatLabel(new DateTime(2026, 3, 15));
        Assert.Equal("Mar 15", label);
    }

    // ── FormatLabel — week bucket ──

    [Fact]
    public void FormatLabel_WeekBucket()
    {
        var state = new SdFilterState { Bucket = "week" };
        var label = state.FormatLabel(new DateTime(2026, 3, 23));
        Assert.Equal("W/C Mar 23", label);
    }

    // ── FormatLabel — month bucket ──

    [Fact]
    public void FormatLabel_MonthBucket()
    {
        var state = new SdFilterState { Bucket = "month" };
        var label = state.FormatLabel(new DateTime(2026, 3, 1));
        Assert.Equal("Mar 26", label);
    }

    [Fact]
    public void FormatLabel_MonthBucket_December()
    {
        var state = new SdFilterState { Bucket = "month" };
        var label = state.FormatLabel(new DateTime(2025, 12, 1));
        Assert.Equal("Dec 25", label);
    }

    // ── Default() factory ──

    [Fact]
    public void Default_ReturnsThisWeek()
    {
        var state = SdFilterState.Default();
        Assert.Equal("day", state.Bucket);
        Assert.Equal(DayOfWeek.Monday, state.RangeStart.DayOfWeek);
        Assert.Equal(7, (state.RangeEnd - state.RangeStart).TotalDays);
    }

    [Fact]
    public void Default_RangeStartIsMonday()
    {
        var state = SdFilterState.Default();
        Assert.Equal(DayOfWeek.Monday, state.RangeStart.DayOfWeek);
    }

    [Fact]
    public void Default_RangeEndIsNextMonday()
    {
        var state = SdFilterState.Default();
        Assert.Equal(DayOfWeek.Monday, state.RangeEnd.DayOfWeek);
    }

    // ── Defaults for filter state properties ──

    [Fact]
    public void Defaults_AreCorrect()
    {
        var state = new SdFilterState();
        Assert.Equal("day", state.Bucket);
        Assert.Null(state.SelectedTechs);
        Assert.Null(state.SelectedGroups);
        Assert.True(state.ShowOpenLine);
        Assert.True(state.ShowResolutionLine);
        Assert.False(state.ShowTotalCreatedLine);
        Assert.False(state.ShowTotalClosedLine);
        Assert.True(state.ShowTargetLine);
        Assert.True(state.ShowKpiCards);
        Assert.False(state.ShowBarLabels);
        Assert.Equal(0, state.ChartType);
        Assert.Equal(0, state.BarStyle);
        Assert.Equal(0, state.ChartTheme);
    }

    [Fact]
    public void GridDefaults_AreCorrect()
    {
        var state = new SdFilterState();
        Assert.True(state.ShowGroupPanel);
        Assert.True(state.ShowAutoFilter);
        Assert.True(state.ShowTotalSummary);
        Assert.True(state.AlternateRows);
        Assert.True(state.ShowSearchPanel);
        Assert.True(state.ShowFilterPanel);
        Assert.Equal(0, state.GridStyle);
    }

    // ── Edge case: FormatLabel with exactly 14 days ──

    [Fact]
    public void FormatLabel_DayBucket_Exactly14Days_ShowsDayOfWeek()
    {
        var state = new SdFilterState
        {
            Bucket = "day",
            RangeStart = new DateTime(2026, 3, 1),
            RangeEnd = new DateTime(2026, 3, 15) // 14 days = <=14
        };
        var label = state.FormatLabel(new DateTime(2026, 3, 5));
        Assert.Equal("Thu 5", label);
    }

    [Fact]
    public void FormatLabel_DayBucket_15Days_ShowsMonthDay()
    {
        var state = new SdFilterState
        {
            Bucket = "day",
            RangeStart = new DateTime(2026, 3, 1),
            RangeEnd = new DateTime(2026, 3, 16) // 15 days > 14
        };
        var label = state.FormatLabel(new DateTime(2026, 3, 5));
        Assert.Equal("Mar 5", label);
    }
}
