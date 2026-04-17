using Central.Engine.Models;
using IntegrationModel = Central.Engine.Models.Integration;

namespace Central.Tests.Models;

public class IntegrationModelExtendedTests
{
    // ── Integration defaults ──

    [Fact]
    public void Integration_Defaults()
    {
        var i = new IntegrationModel();
        Assert.Equal(0, i.Id);
        Assert.Equal("", i.Name);
        Assert.Equal("", i.DisplayName);
        Assert.Equal("oauth2", i.IntegrationType);
        Assert.Equal("", i.BaseUrl);
        Assert.False(i.IsEnabled);
        Assert.Equal("{}", i.ConfigJson);
    }

    // ── StatusIcon ──

    [Fact]
    public void Integration_StatusIcon_Enabled()
    {
        var i = new IntegrationModel { IsEnabled = true };
        Assert.Equal("\u2705", i.StatusIcon);
    }

    [Fact]
    public void Integration_StatusIcon_Disabled()
    {
        var i = new IntegrationModel { IsEnabled = false };
        Assert.Equal("\u26d4", i.StatusIcon);
    }

    // ── StatusText ──

    [Fact]
    public void Integration_StatusText_Enabled()
    {
        var i = new IntegrationModel { IsEnabled = true };
        Assert.Equal("Enabled", i.StatusText);
    }

    [Fact]
    public void Integration_StatusText_Disabled()
    {
        var i = new IntegrationModel { IsEnabled = false };
        Assert.Equal("Disabled", i.StatusText);
    }

    // ── PropertyChanged ──

    [Fact]
    public void Integration_PropertyChanged_AllProperties()
    {
        var i = new IntegrationModel();
        var changed = new List<string>();
        i.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        i.Id = 1;
        i.Name = "manage_engine";
        i.DisplayName = "ManageEngine";
        i.IntegrationType = "oauth2";
        i.BaseUrl = "https://sdp.corp.local";
        i.IsEnabled = true;
        i.ConfigJson = "{}";

        Assert.Contains("Id", changed);
        Assert.Contains("Name", changed);
        Assert.Contains("DisplayName", changed);
        Assert.Contains("IntegrationType", changed);
        Assert.Contains("BaseUrl", changed);
        Assert.Contains("IsEnabled", changed);
        Assert.Contains("ConfigJson", changed);
    }

    [Fact]
    public void Integration_IsEnabled_FiresStatusIcon()
    {
        var i = new IntegrationModel();
        var changed = new List<string>();
        i.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        i.IsEnabled = true;

        Assert.Contains("IsEnabled", changed);
        Assert.Contains("StatusIcon", changed);
    }

    // ── IntegrationCredential ──

    [Fact]
    public void IntegrationCredential_Defaults()
    {
        var c = new IntegrationCredential();
        Assert.Equal(0, c.Id);
        Assert.Equal(0, c.IntegrationId);
        Assert.Equal("", c.Key);
        Assert.Equal("", c.Value);
        Assert.Null(c.ExpiresAt);
    }

    [Fact]
    public void IntegrationCredential_ExpiresAt_CanBeSet()
    {
        var expires = DateTime.UtcNow.AddHours(1);
        var c = new IntegrationCredential { ExpiresAt = expires };
        Assert.Equal(expires, c.ExpiresAt);
    }

    [Fact]
    public void IntegrationCredential_ExpiresAt_CanBeNull()
    {
        var c = new IntegrationCredential { ExpiresAt = DateTime.UtcNow };
        c.ExpiresAt = null;
        Assert.Null(c.ExpiresAt);
    }

    // ── IntegrationLogEntry ──

    [Fact]
    public void IntegrationLogEntry_Defaults()
    {
        var e = new IntegrationLogEntry();
        Assert.Equal(0, e.Id);
        Assert.Equal(0, e.IntegrationId);
        Assert.Equal("", e.Action);
        Assert.Equal("", e.Status);
        Assert.Null(e.Message);
        Assert.Null(e.DurationMs);
    }

    [Fact]
    public void IntegrationLogEntry_SetProperties()
    {
        var e = new IntegrationLogEntry
        {
            Id = 1,
            IntegrationId = 5,
            Action = "sync_requests",
            Status = "success",
            Message = "Synced 150 requests",
            DurationMs = 2500,
            CreatedAt = new DateTime(2026, 3, 30, 10, 0, 0)
        };
        Assert.Equal(1, e.Id);
        Assert.Equal("sync_requests", e.Action);
        Assert.Equal("success", e.Status);
        Assert.Equal("Synced 150 requests", e.Message);
        Assert.Equal(2500, e.DurationMs);
    }
}
