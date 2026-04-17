using Central.Engine.Models;

namespace Central.Tests.Models;

public class AppLogEntryTests
{
    [Fact]
    public void Defaults()
    {
        var e = new AppLogEntry();
        Assert.Equal(0, e.Id);
        Assert.Equal("Error", e.Level);
        Assert.Equal("", e.Tag);
        Assert.Equal("", e.Source);
        Assert.Equal("", e.Message);
        Assert.Equal("", e.Detail);
        Assert.Equal("", e.Username);
    }

    [Fact]
    public void DisplayTime_FormatsCorrectly()
    {
        var e = new AppLogEntry { Timestamp = new DateTime(2026, 3, 30, 14, 30, 45, DateTimeKind.Utc) };
        var display = e.DisplayTime;
        // Should convert to local time and format with seconds
        Assert.Matches(@"\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}", display);
    }

    [Fact]
    public void PropertyChanged_AllFields()
    {
        var e = new AppLogEntry();
        var changed = new List<string>();
        e.PropertyChanged += (_, e2) => changed.Add(e2.PropertyName!);

        e.Id = 1;
        e.Timestamp = DateTime.UtcNow;
        e.Level = "Warning";
        e.Tag = "SSH";
        e.Source = "SshService";
        e.Message = "Connection failed";
        e.Detail = "Timeout after 5s";
        e.Username = "admin";

        Assert.Contains("Id", changed);
        Assert.Contains("Timestamp", changed);
        Assert.Contains("Level", changed);
        Assert.Contains("Tag", changed);
        Assert.Contains("Source", changed);
        Assert.Contains("Message", changed);
        Assert.Contains("Detail", changed);
        Assert.Contains("Username", changed);
    }
}
