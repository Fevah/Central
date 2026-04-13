using Central.Core.Models;

namespace Central.Tests.Models;

public class SwitchRecordTests
{
    // ── UptimeMinutes parsing ──

    [Theory]
    [InlineData("9 day 5 hour 15 minute", 9 * 1440 + 5 * 60 + 15)]
    [InlineData("1 day 0 hour 0 minute", 1440)]
    [InlineData("0 day 2 hour 30 minute", 150)]
    [InlineData("0 day 0 hour 45 minute", 45)]
    [InlineData("", 0)]
    [InlineData(null, 0)]
    [InlineData("100 days 23 hours 59 minutes", 100 * 1440 + 23 * 60 + 59)]
    public void UptimeMinutes_ParsesCorrectly(string? uptime, int expectedMinutes)
    {
        var sw = new SwitchRecord { Uptime = uptime ?? "" };
        Assert.Equal(expectedMinutes, sw.UptimeMinutes);
    }

    // ── UptimeDisplay formatting ──

    [Theory]
    [InlineData("9 day 5 hour 15 minute", "9d 5h 15m")]
    [InlineData("0 day 2 hour 30 minute", "2h 30m")]
    [InlineData("0 day 0 hour 45 minute", "45m")]
    [InlineData("", "")]
    public void UptimeDisplay_FormatsCorrectly(string uptime, string expectedDisplay)
    {
        var sw = new SwitchRecord { Uptime = uptime };
        Assert.Equal(expectedDisplay, sw.UptimeDisplay);
    }

    // ── LoopbackDisplay ──

    [Fact]
    public void LoopbackDisplay_WithPrefix()
    {
        var sw = new SwitchRecord { LoopbackIp = "10.0.0.1", LoopbackPrefix = 32 };
        Assert.Equal("10.0.0.1/32", sw.LoopbackDisplay);
    }

    [Fact]
    public void LoopbackDisplay_NoPrefix()
    {
        var sw = new SwitchRecord { LoopbackIp = "10.0.0.1", LoopbackPrefix = 0 };
        Assert.Equal("10.0.0.1", sw.LoopbackDisplay);
    }

    [Fact]
    public void LoopbackDisplay_Empty()
    {
        var sw = new SwitchRecord { LoopbackIp = "" };
        Assert.Equal("", sw.LoopbackDisplay);
    }

    // ── EffectiveSshIp ──

    [Fact]
    public void EffectiveSshIp_UsesOverrideWhenSet()
    {
        var sw = new SwitchRecord { ManagementIp = "10.11.152.2", SshOverrideIp = "192.168.1.1" };
        Assert.Equal("192.168.1.1", sw.EffectiveSshIp);
    }

    [Fact]
    public void EffectiveSshIp_FallsBackToManagementIp()
    {
        var sw = new SwitchRecord { ManagementIp = "10.11.152.2", SshOverrideIp = "" };
        Assert.Equal("10.11.152.2", sw.EffectiveSshIp);
    }

    [Fact]
    public void EffectiveSshIp_WhitespaceOverride_FallsBack()
    {
        var sw = new SwitchRecord { ManagementIp = "10.11.152.2", SshOverrideIp = "   " };
        Assert.Equal("10.11.152.2", sw.EffectiveSshIp);
    }

    // ── PingIcon / PingColor / PingStatus ──

    [Fact]
    public void PingColor_Ok_Green()
    {
        var sw = new SwitchRecord { LastPingOk = true };
        Assert.Equal("#22C55E", sw.PingColor);
    }

    [Fact]
    public void PingColor_Failed_Red()
    {
        var sw = new SwitchRecord { LastPingOk = false };
        Assert.Equal("#EF4444", sw.PingColor);
    }

    [Fact]
    public void PingColor_Unknown_Grey()
    {
        var sw = new SwitchRecord { LastPingOk = null };
        Assert.Equal("#6B7280", sw.PingColor);
    }

    [Fact]
    public void PingColor_IsPinging_Orange()
    {
        var sw = new SwitchRecord { IsPinging = true };
        Assert.Equal("Orange", sw.PingColor);
    }

    [Fact]
    public void PingStatus_Ok_ShowsLatency()
    {
        var sw = new SwitchRecord { LastPingOk = true, LastPingMs = 12.5 };
        Assert.Contains("12", sw.PingStatus);
        Assert.Contains("ms", sw.PingStatus);
    }

    [Fact]
    public void PingStatus_Failed_ShowsUnreachable()
    {
        var sw = new SwitchRecord { LastPingOk = false };
        Assert.Contains("Unreachable", sw.PingStatus);
    }

    [Fact]
    public void PingStatus_Unknown_ShowsDash()
    {
        var sw = new SwitchRecord { LastPingOk = null };
        Assert.Contains("\u2014", sw.PingStatus);
    }

    [Fact]
    public void PingLatency_HasValue_Formatted()
    {
        var sw = new SwitchRecord { LastPingMs = 3.7 };
        Assert.Contains("ms", sw.PingLatency);
    }

    [Fact]
    public void PingLatency_NoValue_Empty()
    {
        var sw = new SwitchRecord { LastPingMs = null };
        Assert.Equal("", sw.PingLatency);
    }

    // ── SshStatus / SshColor ──

    [Fact]
    public void SshColor_Ok_Green()
    {
        var sw = new SwitchRecord { LastSshOk = true };
        Assert.Equal("#22C55E", sw.SshColor);
    }

    [Fact]
    public void SshColor_Failed_Red()
    {
        var sw = new SwitchRecord { LastSshOk = false };
        Assert.Equal("#EF4444", sw.SshColor);
    }

    [Fact]
    public void SshColor_Unknown_Grey()
    {
        var sw = new SwitchRecord { LastSshOk = null };
        Assert.Equal("#6B7280", sw.SshColor);
    }

    [Fact]
    public void SshStatus_Ok()
    {
        var sw = new SwitchRecord { LastSshOk = true };
        Assert.Contains("OK", sw.SshStatus);
    }

    [Fact]
    public void SshStatus_Failed()
    {
        var sw = new SwitchRecord { LastSshOk = false };
        Assert.Contains("Failed", sw.SshStatus);
    }

    // ── PropertyChanged cascades ──

    [Fact]
    public void LastPingOk_Change_NotifiesPingStatus_PingIcon_PingColor()
    {
        var sw = new SwitchRecord();
        var changed = new List<string>();
        sw.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        sw.LastPingOk = true;

        Assert.Contains("LastPingOk", changed);
        Assert.Contains("PingStatus", changed);
        Assert.Contains("PingIcon", changed);
        Assert.Contains("PingColor", changed);
    }

    [Fact]
    public void LastSshOk_Change_NotifiesSshStatus_SshIcon_SshColor()
    {
        var sw = new SwitchRecord();
        var changed = new List<string>();
        sw.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        sw.LastSshOk = true;

        Assert.Contains("LastSshOk", changed);
        Assert.Contains("SshStatus", changed);
        Assert.Contains("SshIcon", changed);
        Assert.Contains("SshColor", changed);
    }

    [Fact]
    public void IsPinging_Change_NotifiesPingIcon_PingColor()
    {
        var sw = new SwitchRecord();
        var changed = new List<string>();
        sw.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        sw.IsPinging = true;

        Assert.Contains("IsPinging", changed);
        Assert.Contains("PingIcon", changed);
        Assert.Contains("PingColor", changed);
    }

    [Fact]
    public void LoopbackIp_Change_NotifiesLoopbackDisplay()
    {
        var sw = new SwitchRecord();
        var changed = new List<string>();
        sw.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        sw.LoopbackIp = "10.0.0.1";

        Assert.Contains("LoopbackIp", changed);
        Assert.Contains("LoopbackDisplay", changed);
    }

    [Fact]
    public void Uptime_Change_NotifiesUptimeMinutes_UptimeDisplay()
    {
        var sw = new SwitchRecord();
        var changed = new List<string>();
        sw.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        sw.Uptime = "1 day 0 hour 0 minute";

        Assert.Contains("Uptime", changed);
        Assert.Contains("UptimeMinutes", changed);
        Assert.Contains("UptimeDisplay", changed);
    }

    [Fact]
    public void SshPort_Defaults_To22()
    {
        var sw = new SwitchRecord();
        Assert.Equal(22, sw.SshPort);
    }

    [Fact]
    public void PingIcon_IsPinging_ShowsHourglass()
    {
        var sw = new SwitchRecord { IsPinging = true };
        Assert.Equal("\u23F3", sw.PingIcon);
    }
}
