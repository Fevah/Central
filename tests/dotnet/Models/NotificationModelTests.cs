using Central.Engine.Models;

namespace Central.Tests.Models;

public class NotificationModelTests
{
    [Fact]
    public void NotificationPreference_EventDescription_KnownTypes()
    {
        Assert.Equal("Sync failure alerts", new NotificationPreference { EventType = "sync_failure" }.EventDescription);
        Assert.Equal("Sync completion", new NotificationPreference { EventType = "sync_complete" }.EventDescription);
        Assert.Equal("Account lockout alerts", new NotificationPreference { EventType = "auth_lockout" }.EventDescription);
        Assert.Equal("Backup completion", new NotificationPreference { EventType = "backup_complete" }.EventDescription);
        Assert.Equal("Backup failure alerts", new NotificationPreference { EventType = "backup_failure" }.EventDescription);
        Assert.Equal("Data changed by another user", new NotificationPreference { EventType = "data_changed" }.EventDescription);
        Assert.Equal("Password expiry warnings", new NotificationPreference { EventType = "password_expiry" }.EventDescription);
        Assert.Equal("Webhook received", new NotificationPreference { EventType = "webhook_received" }.EventDescription);
    }

    [Fact]
    public void NotificationPreference_UnknownType_ReturnsRaw()
    {
        Assert.Equal("custom_event", new NotificationPreference { EventType = "custom_event" }.EventDescription);
    }

    [Fact]
    public void NotificationEventTypes_Has8()
    {
        Assert.Equal(8, NotificationEventTypes.All.Length);
    }

    [Fact]
    public void ActiveSession_Duration_Formats()
    {
        var s = new ActiveSession { StartedAt = DateTime.UtcNow.AddHours(-2).AddMinutes(-30) };
        Assert.Contains("02", s.Duration);
    }

    [Fact]
    public void ActiveSession_StatusColor()
    {
        Assert.Equal("#22C55E", new ActiveSession { IsActive = true }.StatusColor);
        Assert.Equal("#6B7280", new ActiveSession { IsActive = false }.StatusColor);
    }

    [Fact]
    public void ApiKeyRecord_PropertyChanged()
    {
        var key = new ApiKeyRecord();
        bool fired = false;
        key.PropertyChanged += (_, _) => fired = true;
        key.Name = "Service Key";
        Assert.True(fired);
    }

    [Fact]
    public void DashboardData_Defaults()
    {
        var d = new DashboardData();
        Assert.Equal(0, d.DeviceCount);
        Assert.Equal(0, d.SdOpenTickets);
        Assert.NotNull(d.RecentActivity);
        Assert.Empty(d.RecentActivity);
    }

    [Fact]
    public void ActivityItem_Defaults()
    {
        var a = new ActivityItem();
        Assert.Equal("", a.Time);
        Assert.Equal("", a.Icon);
        Assert.Equal("", a.Message);
    }

    [Fact]
    public void SavedFilter_IsShared_NullUserId()
    {
        var f = new SavedFilter { UserId = null };
        Assert.True(f.IsShared);
    }

    [Fact]
    public void SavedFilter_NotShared_HasUserId()
    {
        var f = new SavedFilter { UserId = 1 };
        Assert.False(f.IsShared);
    }

    [Fact]
    public void SavedFilter_PropertyChanged()
    {
        var f = new SavedFilter();
        bool fired = false;
        f.PropertyChanged += (_, _) => fired = true;
        f.FilterName = "My Filter";
        Assert.True(fired);
    }

    [Fact]
    public void AdUser_Defaults()
    {
        var u = new AdUser();
        Assert.Equal("", u.ObjectGuid);
        Assert.Equal("", u.DisplayName);
        Assert.False(u.IsImported);
    }

    [Fact]
    public void AdConfig_IsConfigured()
    {
        Assert.False(new AdConfig().IsConfigured);
        Assert.True(new AdConfig { Domain = "corp.local" }.IsConfigured);
    }

    [Fact]
    public void IconOverride_Defaults()
    {
        var o = new IconOverride();
        Assert.Equal("", o.Context);
        Assert.Equal("", o.ElementKey);
        Assert.Null(o.Color);
    }
}
