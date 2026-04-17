using Central.Engine.Models;
using IntegrationModel = Central.Engine.Models.Integration;

namespace Central.Tests.Models;

public class IntegrationModelTests
{
    [Fact]
    public void StatusIcon_Enabled()
    {
        var i = new IntegrationModel { IsEnabled = true };
        Assert.Equal("✅", i.StatusIcon);
    }

    [Fact]
    public void StatusIcon_Disabled()
    {
        var i = new IntegrationModel { IsEnabled = false };
        Assert.Equal("⛔", i.StatusIcon);
    }

    [Fact]
    public void StatusText_Enabled()
    {
        var i = new IntegrationModel { IsEnabled = true };
        Assert.Equal("Enabled", i.StatusText);
    }

    [Fact]
    public void StatusText_Disabled()
    {
        var i = new IntegrationModel { IsEnabled = false };
        Assert.Equal("Disabled", i.StatusText);
    }

    [Fact]
    public void PropertyChanged_IsEnabled_NotifiesStatusIcon()
    {
        var i = new IntegrationModel();
        var changed = new List<string>();
        i.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        i.IsEnabled = true;
        Assert.Contains("IsEnabled", changed);
        Assert.Contains("StatusIcon", changed);
    }

    [Fact]
    public void Defaults()
    {
        var i = new IntegrationModel();
        Assert.Equal("", i.Name);
        Assert.Equal("", i.DisplayName);
        Assert.Equal("oauth2", i.IntegrationType);
        Assert.Equal("", i.BaseUrl);
        Assert.False(i.IsEnabled);
        Assert.Equal("{}", i.ConfigJson);
    }

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
    public void IntegrationLogEntry_Defaults()
    {
        var log = new IntegrationLogEntry();
        Assert.Equal("", log.Action);
        Assert.Equal("", log.Status);
        Assert.Null(log.Message);
        Assert.Null(log.DurationMs);
    }
}
