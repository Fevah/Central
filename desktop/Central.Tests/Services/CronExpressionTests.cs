using Central.Core.Services;

namespace Central.Tests.Services;

public class CronExpressionTests
{
    [Fact]
    public void Parse_EveryMinute()
    {
        var cron = CronExpression.Parse("* * * * *");
        Assert.True(cron.Matches(new DateTime(2026, 3, 28, 10, 30, 0)));
        Assert.True(cron.Matches(new DateTime(2026, 1, 1, 0, 0, 0)));
    }

    [Fact]
    public void Parse_SpecificTime_Daily()
    {
        var cron = CronExpression.Parse("30 2 * * *"); // 02:30 daily
        Assert.True(cron.Matches(new DateTime(2026, 3, 28, 2, 30, 0)));
        Assert.False(cron.Matches(new DateTime(2026, 3, 28, 2, 31, 0)));
        Assert.False(cron.Matches(new DateTime(2026, 3, 28, 3, 30, 0)));
    }

    [Fact]
    public void Parse_Every6Hours()
    {
        var cron = CronExpression.Parse("0 */6 * * *"); // :00 every 6h
        Assert.True(cron.Matches(new DateTime(2026, 3, 28, 0, 0, 0)));
        Assert.True(cron.Matches(new DateTime(2026, 3, 28, 6, 0, 0)));
        Assert.True(cron.Matches(new DateTime(2026, 3, 28, 12, 0, 0)));
        Assert.True(cron.Matches(new DateTime(2026, 3, 28, 18, 0, 0)));
        Assert.False(cron.Matches(new DateTime(2026, 3, 28, 3, 0, 0)));
    }

    [Fact]
    public void Parse_Every15Minutes()
    {
        var cron = CronExpression.Parse("*/15 * * * *");
        Assert.True(cron.Matches(new DateTime(2026, 3, 28, 10, 0, 0)));
        Assert.True(cron.Matches(new DateTime(2026, 3, 28, 10, 15, 0)));
        Assert.True(cron.Matches(new DateTime(2026, 3, 28, 10, 30, 0)));
        Assert.True(cron.Matches(new DateTime(2026, 3, 28, 10, 45, 0)));
        Assert.False(cron.Matches(new DateTime(2026, 3, 28, 10, 10, 0)));
    }

    [Fact]
    public void Parse_WeekdaysOnly()
    {
        var cron = CronExpression.Parse("0 9 * * 1-5"); // 9am weekdays
        // 2026-03-28 is a Saturday (DayOfWeek = 6)
        Assert.False(cron.Matches(new DateTime(2026, 3, 28, 9, 0, 0)));
        // 2026-03-30 is a Monday (DayOfWeek = 1)
        Assert.True(cron.Matches(new DateTime(2026, 3, 30, 9, 0, 0)));
    }

    [Fact]
    public void Parse_HourlyRange()
    {
        var cron = CronExpression.Parse("0 9-17 * * 1-5"); // hourly 9-5 weekdays
        Assert.True(cron.Matches(new DateTime(2026, 3, 30, 9, 0, 0)));  // Mon 9am
        Assert.True(cron.Matches(new DateTime(2026, 3, 30, 17, 0, 0))); // Mon 5pm
        Assert.False(cron.Matches(new DateTime(2026, 3, 30, 8, 0, 0))); // Mon 8am (before range)
        Assert.False(cron.Matches(new DateTime(2026, 3, 30, 18, 0, 0))); // Mon 6pm (after range)
    }

    [Fact]
    public void Parse_MondayMidnight()
    {
        var cron = CronExpression.Parse("0 0 * * 1"); // Monday midnight
        Assert.True(cron.Matches(new DateTime(2026, 3, 30, 0, 0, 0)));  // Monday
        Assert.False(cron.Matches(new DateTime(2026, 3, 31, 0, 0, 0))); // Tuesday
    }

    [Fact]
    public void Parse_SpecificMonthDay()
    {
        var cron = CronExpression.Parse("0 0 1 * *"); // 1st of month midnight
        Assert.True(cron.Matches(new DateTime(2026, 4, 1, 0, 0, 0)));
        Assert.False(cron.Matches(new DateTime(2026, 4, 2, 0, 0, 0)));
    }

    [Fact]
    public void Parse_CommaList()
    {
        var cron = CronExpression.Parse("0 8,12,18 * * *"); // 8am, noon, 6pm
        Assert.True(cron.Matches(new DateTime(2026, 3, 28, 8, 0, 0)));
        Assert.True(cron.Matches(new DateTime(2026, 3, 28, 12, 0, 0)));
        Assert.True(cron.Matches(new DateTime(2026, 3, 28, 18, 0, 0)));
        Assert.False(cron.Matches(new DateTime(2026, 3, 28, 10, 0, 0)));
    }

    [Fact]
    public void GetNextOccurrence_FindsNext()
    {
        var cron = CronExpression.Parse("30 2 * * *"); // 02:30 daily
        var after = new DateTime(2026, 3, 28, 3, 0, 0); // after 3am
        var next = cron.GetNextOccurrence(after);
        Assert.NotNull(next);
        Assert.Equal(new DateTime(2026, 3, 29, 2, 30, 0), next.Value);
    }

    [Fact]
    public void GetNextOccurrence_SameDay()
    {
        var cron = CronExpression.Parse("30 14 * * *"); // 14:30 daily
        var after = new DateTime(2026, 3, 28, 10, 0, 0);
        var next = cron.GetNextOccurrence(after);
        Assert.NotNull(next);
        Assert.Equal(new DateTime(2026, 3, 28, 14, 30, 0), next.Value);
    }

    [Fact]
    public void TryParse_Invalid_ReturnsFalse()
    {
        Assert.False(CronExpression.TryParse("not a cron", out _));
        Assert.False(CronExpression.TryParse("* * *", out _)); // only 3 fields
        Assert.False(CronExpression.TryParse("", out _));
    }

    [Fact]
    public void TryParse_Valid_ReturnsTrue()
    {
        Assert.True(CronExpression.TryParse("*/5 * * * *", out var cron));
        Assert.NotNull(cron);
    }

    [Fact]
    public void ToString_Roundtrips()
    {
        var cron = CronExpression.Parse("0 */6 * * *");
        var str = cron.ToString();
        Assert.Contains("0", str);
    }
}
