using Central.Engine.Services;

namespace Central.Tests.Models;

public class NotificationTypeTests
{
    [Theory]
    [InlineData(NotificationType.Info, "#3B82F6")]
    [InlineData(NotificationType.Success, "#22C55E")]
    [InlineData(NotificationType.Warning, "#F59E0B")]
    [InlineData(NotificationType.Error, "#EF4444")]
    public void Notification_Color_ByType(NotificationType type, string expectedColor)
    {
        var n = new Notification(type, "Test", "", null);
        Assert.Equal(expectedColor, n.Color);
    }

    [Fact]
    public void Notification_Icon_Info()
    {
        var n = new Notification(NotificationType.Info, "Test", "", null);
        Assert.Equal("\u2139", n.Icon); // info symbol
    }

    [Fact]
    public void Notification_Icon_Success()
    {
        var n = new Notification(NotificationType.Success, "Test", "", null);
        Assert.Equal("\u2713", n.Icon); // checkmark
    }

    [Fact]
    public void Notification_Icon_Warning()
    {
        var n = new Notification(NotificationType.Warning, "Test", "", null);
        Assert.Equal("\u26A0", n.Icon); // warning
    }

    [Fact]
    public void Notification_Icon_Error()
    {
        var n = new Notification(NotificationType.Error, "Test", "", null);
        Assert.Equal("\u2717", n.Icon); // cross mark
    }

    [Fact]
    public void Notification_Timestamp_IsRecent()
    {
        var before = DateTime.Now.AddSeconds(-1);
        var n = new Notification(NotificationType.Info, "Test", "msg", "source");
        var after = DateTime.Now.AddSeconds(1);

        Assert.True(n.Timestamp >= before && n.Timestamp <= after);
    }

    [Fact]
    public void Notification_Properties()
    {
        var n = new Notification(NotificationType.Error, "Title", "Message", "Source");
        Assert.Equal(NotificationType.Error, n.Type);
        Assert.Equal("Title", n.Title);
        Assert.Equal("Message", n.Message);
        Assert.Equal("Source", n.Source);
    }

    [Fact]
    public void Notification_NullSource()
    {
        var n = new Notification(NotificationType.Info, "T", "M", null);
        Assert.Null(n.Source);
    }
}
