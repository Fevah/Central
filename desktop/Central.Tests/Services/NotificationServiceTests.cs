using Central.Core.Models;
using Central.Core.Services;

namespace Central.Tests.Services;

public class NotificationServiceTests
{
    [Fact]
    public void NotifyEvent_NoPrefs_DefaultsToToast()
    {
        var svc = new NotificationService();
        Notification? received = null;
        svc.NotificationReceived += n => received = n;

        svc.NotifyEvent("sync_failure", "Sync Failed");

        Assert.NotNull(received);
        Assert.Equal("Sync Failed", received!.Title);
    }

    [Fact]
    public void NotifyEvent_ChannelNone_Suppressed()
    {
        var svc = new NotificationService();
        svc.LoadPreferences(new[]
        {
            new NotificationPreference { EventType = "sync_failure", Channel = "none", IsEnabled = true }
        });

        Notification? received = null;
        svc.NotificationReceived += n => received = n;

        svc.NotifyEvent("sync_failure", "Sync Failed");

        Assert.Null(received); // suppressed
    }

    [Fact]
    public void NotifyEvent_ChannelToast_ShowsToast()
    {
        var svc = new NotificationService();
        svc.LoadPreferences(new[]
        {
            new NotificationPreference { EventType = "backup_complete", Channel = "toast", IsEnabled = true }
        });

        Notification? received = null;
        svc.NotificationReceived += n => received = n;

        svc.NotifyEvent("backup_complete", "Backup Done");

        Assert.NotNull(received);
        Assert.Equal("Backup Done", received!.Title);
    }

    [Fact]
    public void NotifyEvent_ChannelEmail_TriggersEmailEvent()
    {
        var svc = new NotificationService();
        svc.LoadPreferences(new[]
        {
            new NotificationPreference { EventType = "auth_lockout", Channel = "email", IsEnabled = true }
        });

        string? emailEventType = null;
        svc.EmailRequested += (et, _, _) => emailEventType = et;

        Notification? toastReceived = null;
        svc.NotificationReceived += n => toastReceived = n;

        svc.NotifyEvent("auth_lockout", "Account Locked");

        Assert.Equal("auth_lockout", emailEventType); // email triggered
        Assert.Null(toastReceived); // no toast for email-only
    }

    [Fact]
    public void NotifyEvent_ChannelBoth_ToastAndEmail()
    {
        var svc = new NotificationService();
        svc.LoadPreferences(new[]
        {
            new NotificationPreference { EventType = "sync_failure", Channel = "both", IsEnabled = true }
        });

        string? emailEventType = null;
        svc.EmailRequested += (et, _, _) => emailEventType = et;

        Notification? toastReceived = null;
        svc.NotificationReceived += n => toastReceived = n;

        svc.NotifyEvent("sync_failure", "Sync Failed", "Details here");

        Assert.NotNull(toastReceived); // toast shown
        Assert.Equal("sync_failure", emailEventType); // email also triggered
    }

    [Fact]
    public void NotifyEvent_Disabled_Suppressed()
    {
        var svc = new NotificationService();
        svc.LoadPreferences(new[]
        {
            new NotificationPreference { EventType = "data_changed", Channel = "toast", IsEnabled = false }
        });

        Notification? received = null;
        svc.NotificationReceived += n => received = n;

        svc.NotifyEvent("data_changed", "Data Changed");

        Assert.Null(received); // disabled
    }

    [Fact]
    public void LoadPreferences_OverwritesPrevious()
    {
        var svc = new NotificationService();

        svc.LoadPreferences(new[]
        {
            new NotificationPreference { EventType = "sync_failure", Channel = "none", IsEnabled = true }
        });

        // First call — suppressed
        Notification? received = null;
        svc.NotificationReceived += n => received = n;
        svc.NotifyEvent("sync_failure", "Test");
        Assert.Null(received);

        // Reload with toast
        svc.LoadPreferences(new[]
        {
            new NotificationPreference { EventType = "sync_failure", Channel = "toast", IsEnabled = true }
        });

        svc.NotifyEvent("sync_failure", "Test2");
        Assert.NotNull(received);
        Assert.Equal("Test2", received!.Title);
    }

    [Fact]
    public void Recent_KeepsLast50()
    {
        var svc = new NotificationService();
        for (int i = 0; i < 60; i++)
            svc.Info($"Test {i}");
        Assert.Equal(50, svc.Recent.Count);
        Assert.Equal("Test 59", svc.Recent[0].Title); // newest first
    }
}
