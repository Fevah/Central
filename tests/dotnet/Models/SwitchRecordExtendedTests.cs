using Central.Engine.Models;

namespace Central.Tests.Models;

public class SwitchRecordExtendedTests
{
    // ── UptimeMinutes edge cases ──

    [Theory]
    [InlineData("365 day 23 hour 59 minute", 365 * 1440 + 23 * 60 + 59)]
    [InlineData("0 day 0 hour 1 minute", 1)]
    [InlineData("1 day", 1440)]  // "1 day" parses as 1 followed by "day" unit
    [InlineData("abc def", 0)]  // Non-numeric
    [InlineData("5 weeks", 0)] // Unrecognized unit
    public void UptimeMinutes_EdgeCases(string uptime, int expected)
    {
        var sw = new SwitchRecord { Uptime = uptime };
        Assert.Equal(expected, sw.UptimeMinutes);
    }

    // ── UptimeDisplay edge cases ──

    [Theory]
    [InlineData("0 day 0 hour 1 minute", "1m")]
    [InlineData("0 day 1 hour 0 minute", "1h 0m")]
    [InlineData("1 day 0 hour 0 minute", "1d 0h 0m")]
    public void UptimeDisplay_EdgeCases(string uptime, string expected)
    {
        var sw = new SwitchRecord { Uptime = uptime };
        Assert.Equal(expected, sw.UptimeDisplay);
    }

    // ── PingIcon when pinging takes precedence ──

    [Fact]
    public void PingIcon_IsPinging_OverridesOkStatus()
    {
        var sw = new SwitchRecord { LastPingOk = true, IsPinging = true };
        Assert.Equal("⏳", sw.PingIcon);
        Assert.Equal("Orange", sw.PingColor);
    }

    // ── PingStatus format ──

    [Fact]
    public void PingStatus_HighLatency_ShowsRoundedMs()
    {
        var sw = new SwitchRecord { LastPingOk = true, LastPingMs = 123.456 };
        Assert.Contains("123", sw.PingStatus);
    }

    [Fact]
    public void PingStatus_SubMillisecond()
    {
        var sw = new SwitchRecord { LastPingOk = true, LastPingMs = 0.3 };
        Assert.Contains("0", sw.PingStatus);
    }

    // ── SshIcon patterns ──

    [Theory]
    [InlineData(true, "●")]
    [InlineData(false, "●")]
    [InlineData(null, "●")]
    public void SshIcon_AllStates(bool? sshOk, string expected)
    {
        var sw = new SwitchRecord { LastSshOk = sshOk };
        Assert.Equal(expected, sw.SshIcon);
    }

    [Fact]
    public void SshStatus_Unknown_ShowsDash()
    {
        var sw = new SwitchRecord { LastSshOk = null };
        Assert.Contains("—", sw.SshStatus);
    }

    // ── LoopbackDisplay edge cases ──

    [Fact]
    public void LoopbackDisplay_NullIp_Empty()
    {
        var sw = new SwitchRecord { LoopbackIp = null! };
        Assert.Equal("", sw.LoopbackDisplay);
    }

    // ── PropertyChanged for all properties ──

    [Fact]
    public void PropertyChanged_Hostname()
    {
        var sw = new SwitchRecord();
        var changed = new List<string>();
        sw.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        sw.Hostname = "NEW-SW";
        Assert.Contains("Hostname", changed);
    }

    [Fact]
    public void PropertyChanged_ManagementIp()
    {
        var sw = new SwitchRecord();
        var changed = new List<string>();
        sw.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        sw.ManagementIp = "10.0.0.1";
        Assert.Contains("ManagementIp", changed);
    }

    [Fact]
    public void PropertyChanged_LastPingMs_NotifiesPingLatency()
    {
        var sw = new SwitchRecord();
        var changed = new List<string>();
        sw.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        sw.LastPingMs = 5.0;
        Assert.Contains("LastPingMs", changed);
        Assert.Contains("PingLatency", changed);
    }

    [Fact]
    public void PropertyChanged_LoopbackPrefix_NotifiesLoopbackDisplay()
    {
        var sw = new SwitchRecord();
        var changed = new List<string>();
        sw.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        sw.LoopbackPrefix = 32;
        Assert.Contains("LoopbackPrefix", changed);
        Assert.Contains("LoopbackDisplay", changed);
    }

    // ── DetailInterfaces ──

    [Fact]
    public void DetailInterfaces_DefaultEmpty()
    {
        var sw = new SwitchRecord();
        Assert.NotNull(sw.DetailInterfaces);
        Assert.Empty(sw.DetailInterfaces);
    }

    // ── EffectiveSshIp edge case ──

    [Fact]
    public void EffectiveSshIp_BothEmpty_ReturnsEmpty()
    {
        var sw = new SwitchRecord { ManagementIp = "", SshOverrideIp = "" };
        Assert.Equal("", sw.EffectiveSshIp);
    }

    // ── Multiple state fields ──

    [Fact]
    public void AllStatusFields_Defaults()
    {
        var sw = new SwitchRecord();
        Assert.Null(sw.LastPingOk);
        Assert.Null(sw.LastPingMs);
        Assert.Null(sw.LastSshOk);
        Assert.Equal("", sw.LastPingAt);
        Assert.Equal("", sw.LastSshAt);
        Assert.False(sw.IsPinging);
        Assert.Equal("", sw.PicosVersion);
        Assert.Equal("", sw.HardwareModel);
        Assert.Equal("", sw.MacAddress);
        Assert.Equal("", sw.SerialNumber);
    }
}
