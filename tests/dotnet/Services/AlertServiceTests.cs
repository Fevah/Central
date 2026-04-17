using Central.Engine.Services;

namespace Central.Tests.Services;

public class AlertServiceTests
{
    [Fact]
    public void PingFailed_AddsToRecent()
    {
        var svc = AlertService.Instance;
        var initialCount = svc.Recent.Count;
        svc.PingFailed("TEST-SW01", "10.0.0.1");
        Assert.True(svc.Recent.Count > initialCount);
        var last = svc.Recent[0];
        Assert.Equal(AlertSeverity.Warning, last.Severity);
        Assert.Equal("Ping", last.Category);
        Assert.Contains("TEST-SW01", last.Title);
    }

    [Fact]
    public void PingRecovered_AddsInfoAlert()
    {
        var svc = AlertService.Instance;
        svc.PingRecovered("TEST-SW02", 5.3);
        var last = svc.Recent[0];
        Assert.Equal(AlertSeverity.Info, last.Severity);
        Assert.Contains("recovered", last.Title);
        Assert.Contains("5", last.Detail);
    }

    [Fact]
    public void SshFailed_AddsErrorAlert()
    {
        var svc = AlertService.Instance;
        svc.SshFailed("TEST-SW03", "Connection refused");
        var last = svc.Recent[0];
        Assert.Equal(AlertSeverity.Error, last.Severity);
        Assert.Equal("SSH", last.Category);
    }

    [Fact]
    public void ConfigDrift_AddsWarningAlert()
    {
        var svc = AlertService.Instance;
        svc.ConfigDrift("TEST-SW04", 15);
        var last = svc.Recent[0];
        Assert.Equal(AlertSeverity.Warning, last.Severity);
        Assert.Contains("15", last.Detail);
    }

    [Fact]
    public void BgpPeerDown_AddsErrorAlert()
    {
        var svc = AlertService.Instance;
        svc.BgpPeerDown("TEST-SW05", "10.5.17.2", "65112");
        var last = svc.Recent[0];
        Assert.Equal(AlertSeverity.Error, last.Severity);
        Assert.Equal("BGP", last.Category);
        Assert.Contains("10.5.17.2", last.Title);
    }

    [Fact]
    public void AlertRaised_EventFires()
    {
        var svc = AlertService.Instance;
        Alert? received = null;
        svc.AlertRaised += a => received = a;
        svc.PingFailed("EVENT-TEST", "1.2.3.4");
        Assert.NotNull(received);
        Assert.Equal("EVENT-TEST", received!.Hostname);
    }

    [Fact]
    public void Alert_Defaults()
    {
        var a = new Alert();
        Assert.Equal("", a.Category);
        Assert.Equal("", a.Title);
        Assert.Equal("", a.Detail);
        Assert.Null(a.Hostname);
        Assert.True((DateTime.UtcNow - a.Timestamp).TotalSeconds < 5);
    }
}
