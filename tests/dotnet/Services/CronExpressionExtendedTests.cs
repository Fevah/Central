using Central.Engine.Services;

namespace Central.Tests.Services;

public class CronExpressionExtendedTests
{
    // ── Complex cron patterns ──

    [Fact]
    public void Parse_FirstAndFifteenth_OfMonth()
    {
        var cron = CronExpression.Parse("0 0 1,15 * *"); // midnight on 1st and 15th
        Assert.True(cron.Matches(new DateTime(2026, 4, 1, 0, 0, 0)));
        Assert.True(cron.Matches(new DateTime(2026, 4, 15, 0, 0, 0)));
        Assert.False(cron.Matches(new DateTime(2026, 4, 2, 0, 0, 0)));
        Assert.False(cron.Matches(new DateTime(2026, 4, 16, 0, 0, 0)));
    }

    [Fact]
    public void Parse_BusinessHours_WeekdaysOnly()
    {
        // Range/step combo not supported by parser, use comma list instead
        var cron = CronExpression.Parse("0 8,10,12,14,16 * * 1-5"); // every 2h 8am-4pm weekdays
        // Mon March 30, 2026 at 8am
        Assert.True(cron.Matches(new DateTime(2026, 3, 30, 8, 0, 0)));
        Assert.True(cron.Matches(new DateTime(2026, 3, 30, 10, 0, 0)));
        Assert.True(cron.Matches(new DateTime(2026, 3, 30, 12, 0, 0)));
        Assert.True(cron.Matches(new DateTime(2026, 3, 30, 14, 0, 0)));
        Assert.True(cron.Matches(new DateTime(2026, 3, 30, 16, 0, 0)));
        // 9am is NOT in the list
        Assert.False(cron.Matches(new DateTime(2026, 3, 30, 9, 0, 0)));
        // 18 is out of range
        Assert.False(cron.Matches(new DateTime(2026, 3, 30, 18, 0, 0)));
        // Saturday
        Assert.False(cron.Matches(new DateTime(2026, 3, 28, 10, 0, 0)));
    }

    [Fact]
    public void Parse_MultipleMonths()
    {
        var cron = CronExpression.Parse("0 0 1 1,4,7,10 *"); // quarterly: midnight on 1st of Jan/Apr/Jul/Oct
        Assert.True(cron.Matches(new DateTime(2026, 1, 1, 0, 0, 0)));
        Assert.True(cron.Matches(new DateTime(2026, 4, 1, 0, 0, 0)));
        Assert.True(cron.Matches(new DateTime(2026, 7, 1, 0, 0, 0)));
        Assert.True(cron.Matches(new DateTime(2026, 10, 1, 0, 0, 0)));
        Assert.False(cron.Matches(new DateTime(2026, 2, 1, 0, 0, 0)));
        Assert.False(cron.Matches(new DateTime(2026, 5, 1, 0, 0, 0)));
    }

    [Fact]
    public void Parse_WeekendOnly()
    {
        var cron = CronExpression.Parse("0 12 * * 0,6"); // noon on Sat/Sun
        // Saturday March 28, 2026
        Assert.True(cron.Matches(new DateTime(2026, 3, 28, 12, 0, 0)));
        // Sunday March 29, 2026
        Assert.True(cron.Matches(new DateTime(2026, 3, 29, 12, 0, 0)));
        // Monday
        Assert.False(cron.Matches(new DateTime(2026, 3, 30, 12, 0, 0)));
    }

    [Fact]
    public void Parse_Every5Minutes_WorkHours()
    {
        var cron = CronExpression.Parse("*/5 9-17 * * *");
        Assert.True(cron.Matches(new DateTime(2026, 1, 1, 9, 0, 0)));
        Assert.True(cron.Matches(new DateTime(2026, 1, 1, 9, 5, 0)));
        Assert.True(cron.Matches(new DateTime(2026, 1, 1, 17, 55, 0)));
        Assert.False(cron.Matches(new DateTime(2026, 1, 1, 8, 0, 0)));
        Assert.False(cron.Matches(new DateTime(2026, 1, 1, 18, 0, 0)));
        Assert.False(cron.Matches(new DateTime(2026, 1, 1, 9, 3, 0)));
    }

    [Fact]
    public void Parse_LastMinuteOfDay()
    {
        var cron = CronExpression.Parse("59 23 * * *");
        Assert.True(cron.Matches(new DateTime(2026, 1, 1, 23, 59, 0)));
        Assert.False(cron.Matches(new DateTime(2026, 1, 1, 23, 58, 0)));
    }

    [Fact]
    public void Parse_EveryMinuteOnSunday()
    {
        var cron = CronExpression.Parse("* * * * 0");
        // Sunday March 29, 2026
        Assert.True(cron.Matches(new DateTime(2026, 3, 29, 0, 0, 0)));
        Assert.True(cron.Matches(new DateTime(2026, 3, 29, 12, 30, 0)));
        // Monday
        Assert.False(cron.Matches(new DateTime(2026, 3, 30, 12, 30, 0)));
    }

    // ── GetNextOccurrence edge cases ──

    [Fact]
    public void GetNextOccurrence_CrossYear()
    {
        var cron = CronExpression.Parse("0 0 1 1 *"); // Jan 1st midnight
        var next = cron.GetNextOccurrence(new DateTime(2026, 6, 1, 0, 0, 0));
        Assert.NotNull(next);
        Assert.Equal(new DateTime(2027, 1, 1, 0, 0, 0), next.Value);
    }

    [Fact]
    public void GetNextOccurrence_WeekdaySkipsWeekend()
    {
        var cron = CronExpression.Parse("0 9 * * 1-5");
        // After Friday March 27, 2026 at 10am → should be Monday March 30
        var next = cron.GetNextOccurrence(new DateTime(2026, 3, 27, 10, 0, 0));
        Assert.NotNull(next);
        Assert.Equal(DayOfWeek.Monday, next!.Value.DayOfWeek);
    }

    [Fact]
    public void GetNextOccurrence_EveryHour_SameDay()
    {
        var cron = CronExpression.Parse("0 * * * *");
        var next = cron.GetNextOccurrence(new DateTime(2026, 1, 1, 10, 30, 0));
        Assert.NotNull(next);
        Assert.Equal(new DateTime(2026, 1, 1, 11, 0, 0), next.Value);
    }

    [Fact]
    public void GetNextOccurrence_MidnightNextDay()
    {
        var cron = CronExpression.Parse("0 0 * * *");
        var next = cron.GetNextOccurrence(new DateTime(2026, 1, 1, 23, 30, 0));
        Assert.NotNull(next);
        Assert.Equal(new DateTime(2026, 1, 2, 0, 0, 0), next.Value);
    }

    // ── ToString ──

    [Fact]
    public void ToString_SpecificExpression_ContainsValues()
    {
        var cron = CronExpression.Parse("30 2 * * *");
        var s = cron.ToString();
        Assert.Contains("30", s);
        Assert.Contains("2", s);
    }

    // ── TryParse ──

    [Fact]
    public void TryParse_TooManyFields_ReturnsFalse()
    {
        Assert.False(CronExpression.TryParse("* * * * * *", out _));
    }

    [Fact]
    public void TryParse_EmptyString_ReturnsFalse()
    {
        Assert.False(CronExpression.TryParse("", out _));
    }

    [Fact]
    public void TryParse_WhitespaceOnly_ReturnsFalse()
    {
        Assert.False(CronExpression.TryParse("   ", out _));
    }

    [Fact]
    public void TryParse_ValidExpression_ReturnsResult()
    {
        Assert.True(CronExpression.TryParse("0 0 1,15 * *", out var cron));
        Assert.NotNull(cron);
        Assert.True(cron!.Matches(new DateTime(2026, 4, 15, 0, 0, 0)));
    }
}
