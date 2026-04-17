using Central.Engine.Models;

namespace Central.Tests.Models;

public class SshLogEntryTests
{
    // ── Defaults ──

    [Fact]
    public void Defaults_AreCorrect()
    {
        var e = new SshLogEntry();
        Assert.Equal(0, e.Id);
        Assert.Null(e.SwitchId);
        Assert.Equal("", e.Hostname);
        Assert.Equal("", e.HostIp);
        Assert.False(e.Success);
        Assert.Equal("", e.Username);
        Assert.Equal(22, e.Port); // default SSH port
        Assert.Equal("", e.Error);
        Assert.Equal("", e.RawOutput);
        Assert.Equal(0, e.ConfigLines);
        Assert.Equal("", e.LogEntries);
    }

    // ── Duration computed property ──

    [Fact]
    public void Duration_WithFinishedAt_CalculatesCorrectly()
    {
        var start = new DateTime(2026, 3, 15, 10, 0, 0);
        var finish = new DateTime(2026, 3, 15, 10, 0, 5);
        var e = new SshLogEntry { StartedAt = start, FinishedAt = finish };
        Assert.Equal("5.0s", e.Duration);
    }

    [Fact]
    public void Duration_WithFractionalSeconds()
    {
        var start = new DateTime(2026, 3, 15, 10, 0, 0);
        var finish = start.AddSeconds(2.5);
        var e = new SshLogEntry { StartedAt = start, FinishedAt = finish };
        Assert.Equal("2.5s", e.Duration);
    }

    [Fact]
    public void Duration_WithoutFinishedAt_ReturnsDash()
    {
        var e = new SshLogEntry { StartedAt = DateTime.Now, FinishedAt = null };
        Assert.Equal("\u2014", e.Duration); // em dash
    }

    [Fact]
    public void Duration_LongOperation()
    {
        var start = new DateTime(2026, 3, 15, 10, 0, 0);
        var finish = start.AddMinutes(2).AddSeconds(30);
        var e = new SshLogEntry { StartedAt = start, FinishedAt = finish };
        Assert.Equal("150.0s", e.Duration);
    }

    [Fact]
    public void Duration_ZeroSeconds()
    {
        var start = new DateTime(2026, 3, 15, 10, 0, 0);
        var e = new SshLogEntry { StartedAt = start, FinishedAt = start };
        Assert.Equal("0.0s", e.Duration);
    }

    // ── StatusIcon ──

    [Fact]
    public void StatusIcon_Success_CheckMark()
    {
        var e = new SshLogEntry { Success = true };
        Assert.Equal("\u2713", e.StatusIcon);
    }

    [Fact]
    public void StatusIcon_Failure_CrossMark()
    {
        var e = new SshLogEntry { Success = false };
        Assert.Equal("\u2717", e.StatusIcon);
    }

    // ── PropertyChanged ──

    [Fact]
    public void PropertyChanged_Hostname_Fires()
    {
        var e = new SshLogEntry();
        string? changed = null;
        e.PropertyChanged += (_, args) => changed = args.PropertyName;
        e.Hostname = "MEP-91-CORE02";
        Assert.Equal("Hostname", changed);
    }

    [Fact]
    public void PropertyChanged_HostIp_Fires()
    {
        var e = new SshLogEntry();
        string? changed = null;
        e.PropertyChanged += (_, args) => changed = args.PropertyName;
        e.HostIp = "10.11.152.2";
        Assert.Equal("HostIp", changed);
    }

    [Fact]
    public void PropertyChanged_Success_Fires()
    {
        var e = new SshLogEntry();
        string? changed = null;
        e.PropertyChanged += (_, args) => changed = args.PropertyName;
        e.Success = true;
        Assert.Equal("Success", changed);
    }

    [Fact]
    public void PropertyChanged_Port_Fires()
    {
        var e = new SshLogEntry();
        string? changed = null;
        e.PropertyChanged += (_, args) => changed = args.PropertyName;
        e.Port = 2222;
        Assert.Equal("Port", changed);
    }

    [Fact]
    public void PropertyChanged_Error_Fires()
    {
        var e = new SshLogEntry();
        string? changed = null;
        e.PropertyChanged += (_, args) => changed = args.PropertyName;
        e.Error = "Connection refused";
        Assert.Equal("Error", changed);
    }

    [Fact]
    public void PropertyChanged_RawOutput_Fires()
    {
        var e = new SshLogEntry();
        string? changed = null;
        e.PropertyChanged += (_, args) => changed = args.PropertyName;
        e.RawOutput = "set system hostname MEP-91";
        Assert.Equal("RawOutput", changed);
    }

    [Fact]
    public void PropertyChanged_ConfigLines_Fires()
    {
        var e = new SshLogEntry();
        string? changed = null;
        e.PropertyChanged += (_, args) => changed = args.PropertyName;
        e.ConfigLines = 500;
        Assert.Equal("ConfigLines", changed);
    }

    [Fact]
    public void PropertyChanged_SwitchId_Fires()
    {
        var e = new SshLogEntry();
        string? changed = null;
        e.PropertyChanged += (_, args) => changed = args.PropertyName;
        e.SwitchId = Guid.NewGuid();
        Assert.Equal("SwitchId", changed);
    }

    [Fact]
    public void AllProperties_FirePropertyChanged()
    {
        var e = new SshLogEntry();
        var changed = new List<string>();
        e.PropertyChanged += (_, args) => changed.Add(args.PropertyName!);

        e.Id = 1;
        e.SwitchId = Guid.NewGuid();
        e.Hostname = "switch1";
        e.HostIp = "10.0.0.1";
        e.StartedAt = DateTime.Now;
        e.FinishedAt = DateTime.Now;
        e.Success = true;
        e.Username = "admin";
        e.Port = 22;
        e.Error = "";
        e.RawOutput = "output";
        e.ConfigLines = 100;
        e.LogEntries = "log";

        Assert.Equal(13, changed.Count);
    }

    // ── SwitchId nullable ──

    [Fact]
    public void SwitchId_CanBeNull()
    {
        var e = new SshLogEntry { SwitchId = Guid.NewGuid() };
        e.SwitchId = null;
        Assert.Null(e.SwitchId);
    }
}
