using Central.Core.Services;

namespace Central.Tests.Services;

public class CronEdgeCaseTests
{
    [Fact]
    public void Parse_SundayZero()
    {
        var cron = CronExpression.Parse("0 0 * * 0"); // Sunday midnight
        Assert.True(cron.Matches(new DateTime(2026, 3, 29, 0, 0, 0))); // Sunday
        Assert.False(cron.Matches(new DateTime(2026, 3, 30, 0, 0, 0))); // Monday
    }

    [Fact]
    public void Parse_MultipleCommaValues()
    {
        var cron = CronExpression.Parse("0,15,30,45 * * * *");
        Assert.True(cron.Matches(new DateTime(2026, 1, 1, 10, 0, 0)));
        Assert.True(cron.Matches(new DateTime(2026, 1, 1, 10, 15, 0)));
        Assert.True(cron.Matches(new DateTime(2026, 1, 1, 10, 30, 0)));
        Assert.True(cron.Matches(new DateTime(2026, 1, 1, 10, 45, 0)));
        Assert.False(cron.Matches(new DateTime(2026, 1, 1, 10, 10, 0)));
    }

    [Fact]
    public void Parse_FirstDayOfMonth()
    {
        var cron = CronExpression.Parse("0 0 1 * *");
        Assert.True(cron.Matches(new DateTime(2026, 6, 1, 0, 0, 0)));
        Assert.False(cron.Matches(new DateTime(2026, 6, 2, 0, 0, 0)));
    }

    [Fact]
    public void Parse_SpecificMonth()
    {
        var cron = CronExpression.Parse("0 0 1 1 *"); // Jan 1st midnight
        Assert.True(cron.Matches(new DateTime(2026, 1, 1, 0, 0, 0)));
        Assert.False(cron.Matches(new DateTime(2026, 2, 1, 0, 0, 0)));
    }

    [Fact]
    public void GetNextOccurrence_SkipsNonMatchingMonths()
    {
        var cron = CronExpression.Parse("0 0 1 6 *"); // June 1st
        var next = cron.GetNextOccurrence(new DateTime(2026, 3, 1, 0, 0, 0));
        Assert.NotNull(next);
        Assert.Equal(6, next!.Value.Month);
    }

    [Fact]
    public void Parse_Step_FromNonZero()
    {
        var cron = CronExpression.Parse("5/15 * * * *"); // :05, :20, :35, :50
        Assert.True(cron.Matches(new DateTime(2026, 1, 1, 0, 5, 0)));
        Assert.True(cron.Matches(new DateTime(2026, 1, 1, 0, 20, 0)));
        Assert.True(cron.Matches(new DateTime(2026, 1, 1, 0, 35, 0)));
        Assert.True(cron.Matches(new DateTime(2026, 1, 1, 0, 50, 0)));
        Assert.False(cron.Matches(new DateTime(2026, 1, 1, 0, 0, 0)));
    }

    [Fact]
    public void Parse_InvalidFieldCount_Throws()
    {
        Assert.Throws<FormatException>(() => CronExpression.Parse("* * *"));
        Assert.Throws<FormatException>(() => CronExpression.Parse("* * * * * *"));
    }
}
