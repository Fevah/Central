using Central.Engine.Models;

namespace Central.Tests.Models;

public class TimeActivityModelsTests
{
    // ── TimeEntry ──

    [Fact]
    public void TimeEntry_Defaults()
    {
        var te = new TimeEntry();
        Assert.Equal("Development", te.ActivityType);
        Assert.Equal("", te.TaskTitle);
        Assert.Equal("", te.Notes);
    }

    [Fact]
    public void TimeEntry_EntryDateDisplay()
    {
        var te = new TimeEntry { EntryDate = new DateTime(2026, 3, 15) };
        Assert.Equal("2026-03-15", te.EntryDateDisplay);
    }

    [Fact]
    public void TimeEntry_PropertyChanged_Fires()
    {
        var te = new TimeEntry();
        var changed = new List<string>();
        te.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        te.Hours = 4.5m;
        Assert.Contains("Hours", changed);
    }

    // ── ActivityFeedItem ──

    [Theory]
    [InlineData("created", "+")]
    [InlineData("updated", "~")]
    [InlineData("commented", "💬")]
    [InlineData("status_changed", "→")]
    [InlineData("assigned", "👤")]
    [InlineData("deleted", "✕")]
    [InlineData("unknown", "·")]
    [InlineData("", "·")]
    public void ActivityFeedItem_ActionIcon_Correct(string action, string expected)
    {
        var afi = new ActivityFeedItem { Action = action };
        Assert.Equal(expected, afi.ActionIcon);
    }

    [Fact]
    public void ActivityFeedItem_TimeAgo_JustNow()
    {
        var afi = new ActivityFeedItem { CreatedAt = DateTime.UtcNow };
        Assert.Equal("just now", afi.TimeAgo);
    }

    [Fact]
    public void ActivityFeedItem_TimeAgo_Minutes()
    {
        var afi = new ActivityFeedItem { CreatedAt = DateTime.UtcNow.AddMinutes(-15) };
        Assert.Contains("m ago", afi.TimeAgo);
    }

    [Fact]
    public void ActivityFeedItem_TimeAgo_Hours()
    {
        var afi = new ActivityFeedItem { CreatedAt = DateTime.UtcNow.AddHours(-3) };
        Assert.Contains("h ago", afi.TimeAgo);
    }

    [Fact]
    public void ActivityFeedItem_TimeAgo_Days()
    {
        var afi = new ActivityFeedItem { CreatedAt = DateTime.UtcNow.AddDays(-5) };
        Assert.Contains("d ago", afi.TimeAgo);
    }

    // ── TaskViewConfig ──

    [Fact]
    public void TaskViewConfig_Defaults()
    {
        var tvc = new TaskViewConfig();
        Assert.Equal("Tree", tvc.ViewType);
        Assert.Equal("{}", tvc.ConfigJson);
        Assert.False(tvc.IsDefault);
    }

    [Fact]
    public void TaskViewConfig_PropertyChanged_Fires()
    {
        var tvc = new TaskViewConfig();
        var changed = new List<string>();
        tvc.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        tvc.Name = "My View";
        Assert.Contains("Name", changed);
    }
}
