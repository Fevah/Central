using Central.Core.Services;

namespace Central.Tests.Services;

/// <summary>Additional CronExpression tests for GetNextOccurrence and ToString.</summary>
public class CronExpressionNextOccurrenceTests
{
    [Fact]
    public void GetNextOccurrence_EveryMinute()
    {
        var cron = CronExpression.Parse("* * * * *");
        var after = new DateTime(2026, 3, 30, 10, 30, 0);
        var next = cron.GetNextOccurrence(after);
        Assert.NotNull(next);
        Assert.Equal(new DateTime(2026, 3, 30, 10, 31, 0), next.Value);
    }

    [Fact]
    public void GetNextOccurrence_SpecificTime()
    {
        var cron = CronExpression.Parse("30 14 * * *"); // 2:30 PM daily
        var after = new DateTime(2026, 3, 30, 15, 0, 0);
        var next = cron.GetNextOccurrence(after);
        Assert.NotNull(next);
        Assert.Equal(new DateTime(2026, 3, 31, 14, 30, 0), next.Value);
    }

    [Fact]
    public void GetNextOccurrence_MonthlyOnFirst()
    {
        var cron = CronExpression.Parse("0 0 1 * *"); // midnight on 1st
        var after = new DateTime(2026, 3, 2, 0, 0, 0);
        var next = cron.GetNextOccurrence(after);
        Assert.NotNull(next);
        Assert.Equal(new DateTime(2026, 4, 1, 0, 0, 0), next.Value);
    }

    [Fact]
    public void GetNextOccurrence_ReturnsNull_IfNotFoundInWindow()
    {
        // Feb 30 never exists, so day-of-month 30 in February + specific month
        var cron = CronExpression.Parse("0 0 31 2 *"); // Feb 31st never exists
        var after = new DateTime(2026, 1, 1);
        var next = cron.GetNextOccurrence(after, maxSearchMinutes: 60 * 24 * 365);
        // Should be null because Feb has no 31st
        Assert.Null(next);
    }

    [Fact]
    public void Matches_SpecificDateTime()
    {
        var cron = CronExpression.Parse("0 12 25 12 *"); // noon on Dec 25th
        Assert.True(cron.Matches(new DateTime(2026, 12, 25, 12, 0, 0)));
        Assert.False(cron.Matches(new DateTime(2026, 12, 25, 13, 0, 0)));
        Assert.False(cron.Matches(new DateTime(2026, 12, 24, 12, 0, 0)));
    }

    [Fact]
    public void Matches_Wildcard()
    {
        var cron = CronExpression.Parse("* * * * *");
        Assert.True(cron.Matches(new DateTime(2026, 6, 15, 8, 30, 0)));
        Assert.True(cron.Matches(new DateTime(2026, 1, 1, 0, 0, 0)));
    }

    [Fact]
    public void Parse_CommaList()
    {
        var cron = CronExpression.Parse("0,30 * * * *"); // :00 and :30
        Assert.True(cron.Matches(new DateTime(2026, 1, 1, 5, 0, 0)));
        Assert.True(cron.Matches(new DateTime(2026, 1, 1, 5, 30, 0)));
        Assert.False(cron.Matches(new DateTime(2026, 1, 1, 5, 15, 0)));
    }

    [Fact]
    public void Parse_Range()
    {
        var cron = CronExpression.Parse("0 9-17 * * *"); // 9am to 5pm on the hour
        Assert.True(cron.Matches(new DateTime(2026, 1, 1, 9, 0, 0)));
        Assert.True(cron.Matches(new DateTime(2026, 1, 1, 17, 0, 0)));
        Assert.False(cron.Matches(new DateTime(2026, 1, 1, 8, 0, 0)));
        Assert.False(cron.Matches(new DateTime(2026, 1, 1, 18, 0, 0)));
    }

    [Fact]
    public void Parse_Step_FromWildcard()
    {
        var cron = CronExpression.Parse("*/15 * * * *"); // every 15 mins
        Assert.True(cron.Matches(new DateTime(2026, 1, 1, 10, 0, 0)));
        Assert.True(cron.Matches(new DateTime(2026, 1, 1, 10, 15, 0)));
        Assert.True(cron.Matches(new DateTime(2026, 1, 1, 10, 30, 0)));
        Assert.True(cron.Matches(new DateTime(2026, 1, 1, 10, 45, 0)));
        Assert.False(cron.Matches(new DateTime(2026, 1, 1, 10, 10, 0)));
    }

    [Fact]
    public void Parse_Step_FromNumber()
    {
        var cron = CronExpression.Parse("5/20 * * * *"); // :05, :25, :45
        Assert.True(cron.Matches(new DateTime(2026, 1, 1, 10, 5, 0)));
        Assert.True(cron.Matches(new DateTime(2026, 1, 1, 10, 25, 0)));
        Assert.True(cron.Matches(new DateTime(2026, 1, 1, 10, 45, 0)));
        Assert.False(cron.Matches(new DateTime(2026, 1, 1, 10, 0, 0)));
    }

    [Fact]
    public void Parse_DayOfWeek()
    {
        var cron = CronExpression.Parse("0 0 * * 1"); // Monday midnight
        var monday = new DateTime(2026, 3, 30, 0, 0, 0); // March 30 2026 is Monday
        var tuesday = new DateTime(2026, 3, 31, 0, 0, 0);
        Assert.True(cron.Matches(monday));
        Assert.False(cron.Matches(tuesday));
    }

    [Fact]
    public void Parse_InvalidFieldCount_Throws()
    {
        Assert.Throws<FormatException>(() => CronExpression.Parse("* * *")); // only 3 fields
        Assert.Throws<FormatException>(() => CronExpression.Parse("* * * * * *")); // 6 fields
    }

    [Fact]
    public void TryParse_Valid_ReturnsTrue()
    {
        Assert.True(CronExpression.TryParse("0 * * * *", out var result));
        Assert.NotNull(result);
    }

    [Fact]
    public void TryParse_Invalid_ReturnsFalse()
    {
        Assert.False(CronExpression.TryParse("invalid", out var result));
        Assert.Null(result);
    }

    [Fact]
    public void ToString_ReconstructsExpression()
    {
        var cron = CronExpression.Parse("0 12 * * *");
        var str = cron.ToString();
        Assert.Contains("0", str);
        Assert.Contains("12", str);
    }

    [Fact]
    public void GetNextOccurrence_SkipsMonthEfficiently()
    {
        // Test that month-skipping optimization works
        var cron = CronExpression.Parse("0 0 1 6 *"); // June 1st midnight
        var after = new DateTime(2026, 1, 15);
        var next = cron.GetNextOccurrence(after);
        Assert.NotNull(next);
        Assert.Equal(6, next.Value.Month);
        Assert.Equal(1, next.Value.Day);
    }

    [Fact]
    public void GetNextOccurrence_WeekdayFilter()
    {
        var cron = CronExpression.Parse("0 9 * * 1-5"); // 9am weekdays
        var saturday = new DateTime(2026, 3, 28, 10, 0, 0); // Saturday
        var next = cron.GetNextOccurrence(saturday);
        Assert.NotNull(next);
        Assert.True(next.Value.DayOfWeek >= DayOfWeek.Monday && next.Value.DayOfWeek <= DayOfWeek.Friday);
    }
}
