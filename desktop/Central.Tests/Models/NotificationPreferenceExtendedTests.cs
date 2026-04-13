using Central.Core.Models;

namespace Central.Tests.Models;

public class NotificationPreferenceExtendedTests
{
    // ── Defaults ──

    [Fact]
    public void Defaults_AreCorrect()
    {
        var np = new NotificationPreference();
        Assert.Equal(0, np.Id);
        Assert.Equal(0, np.UserId);
        Assert.Equal("", np.EventType);
        Assert.Equal("toast", np.Channel);
        Assert.True(np.IsEnabled);
    }

    // ── PropertyChanged on all properties ──

    [Fact]
    public void PropertyChanged_Id_Fires()
    {
        var np = new NotificationPreference();
        string? changed = null;
        np.PropertyChanged += (_, e) => changed = e.PropertyName;
        np.Id = 5;
        Assert.Equal("Id", changed);
    }

    [Fact]
    public void PropertyChanged_UserId_Fires()
    {
        var np = new NotificationPreference();
        string? changed = null;
        np.PropertyChanged += (_, e) => changed = e.PropertyName;
        np.UserId = 10;
        Assert.Equal("UserId", changed);
    }

    [Fact]
    public void PropertyChanged_EventType_Fires()
    {
        var np = new NotificationPreference();
        string? changed = null;
        np.PropertyChanged += (_, e) => changed = e.PropertyName;
        np.EventType = "sync_failure";
        Assert.Equal("EventType", changed);
    }

    [Fact]
    public void PropertyChanged_Channel_Fires()
    {
        var np = new NotificationPreference();
        string? changed = null;
        np.PropertyChanged += (_, e) => changed = e.PropertyName;
        np.Channel = "email";
        Assert.Equal("Channel", changed);
    }

    [Fact]
    public void PropertyChanged_IsEnabled_Fires()
    {
        var np = new NotificationPreference();
        string? changed = null;
        np.PropertyChanged += (_, e) => changed = e.PropertyName;
        np.IsEnabled = false;
        Assert.Equal("IsEnabled", changed);
    }

    // ── EventDescription computed property ──

    [Theory]
    [InlineData("sync_failure", "Sync failure alerts")]
    [InlineData("sync_complete", "Sync completion")]
    [InlineData("auth_lockout", "Account lockout alerts")]
    [InlineData("backup_complete", "Backup completion")]
    [InlineData("backup_failure", "Backup failure alerts")]
    [InlineData("data_changed", "Data changed by another user")]
    [InlineData("password_expiry", "Password expiry warnings")]
    [InlineData("webhook_received", "Webhook received")]
    public void EventDescription_AllKnownTypes(string eventType, string expected)
    {
        var np = new NotificationPreference { EventType = eventType };
        Assert.Equal(expected, np.EventDescription);
    }

    [Theory]
    [InlineData("unknown_type")]
    [InlineData("custom_alert")]
    [InlineData("")]
    public void EventDescription_UnknownType_ReturnsRaw(string eventType)
    {
        var np = new NotificationPreference { EventType = eventType };
        Assert.Equal(eventType, np.EventDescription);
    }

    // ── NotificationEventTypes ──

    [Fact]
    public void NotificationEventTypes_ContainsAllKnownTypes()
    {
        Assert.Contains("sync_failure", NotificationEventTypes.All);
        Assert.Contains("sync_complete", NotificationEventTypes.All);
        Assert.Contains("auth_lockout", NotificationEventTypes.All);
        Assert.Contains("backup_complete", NotificationEventTypes.All);
        Assert.Contains("backup_failure", NotificationEventTypes.All);
        Assert.Contains("data_changed", NotificationEventTypes.All);
        Assert.Contains("password_expiry", NotificationEventTypes.All);
        Assert.Contains("webhook_received", NotificationEventTypes.All);
    }

    [Fact]
    public void NotificationEventTypes_ExactCount()
    {
        Assert.Equal(8, NotificationEventTypes.All.Length);
    }

    // ── ActiveSession extended tests ──

    [Fact]
    public void ActiveSession_Defaults()
    {
        var s = new ActiveSession();
        Assert.Equal(0, s.Id);
        Assert.Equal(0, s.UserId);
        Assert.Equal("", s.SessionToken);
        Assert.Equal("", s.AuthMethod);
        Assert.Null(s.IpAddress);
        Assert.Null(s.MachineName);
        Assert.Null(s.ExpiresAt);
        Assert.False(s.IsActive);
        Assert.Null(s.Username);
        Assert.Null(s.DisplayName);
    }

    [Fact]
    public void ActiveSession_Duration_FormatDaysHoursMinutes()
    {
        var s = new ActiveSession
        {
            StartedAt = DateTime.UtcNow.AddDays(-1).AddHours(-3).AddMinutes(-15)
        };
        // Duration format: d.hh:mm
        Assert.Contains("1.", s.Duration);
        Assert.Contains("03", s.Duration);
    }

    [Fact]
    public void ActiveSession_Duration_LessThanOneDay()
    {
        var s = new ActiveSession
        {
            StartedAt = DateTime.UtcNow.AddMinutes(-45)
        };
        Assert.Contains("0.", s.Duration);
        Assert.Contains("45", s.Duration);
    }

    [Fact]
    public void ActiveSession_StatusColor_Active_Green()
    {
        var s = new ActiveSession { IsActive = true };
        Assert.Equal("#22C55E", s.StatusColor);
    }

    [Fact]
    public void ActiveSession_StatusColor_Inactive_Grey()
    {
        var s = new ActiveSession { IsActive = false };
        Assert.Equal("#6B7280", s.StatusColor);
    }

    [Fact]
    public void ActiveSession_ExpiresAt_Nullable()
    {
        var s = new ActiveSession { ExpiresAt = DateTime.UtcNow.AddHours(1) };
        Assert.NotNull(s.ExpiresAt);
        s.ExpiresAt = null;
        Assert.Null(s.ExpiresAt);
    }
}
