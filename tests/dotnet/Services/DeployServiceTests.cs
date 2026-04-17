using Central.Core.Models;
using Central.Core.Services;

namespace Central.Tests.Services;

public class DeployServiceTests
{
    // ── P2P BuildConfig ──

    [Fact]
    public void BuildP2PCommands_SideA_GeneratesCorrectCommands()
    {
        var link = new P2PLink
        {
            Vlan = "1017", DeviceA = "CORE01", DeviceB = "CORE02",
            DeviceAIp = "10.5.17.1", DeviceBIp = "10.5.17.2",
            PortA = "xe-1/1/31", PortB = "xe-1/1/31",
            Subnet = "10.5.17.0/30"
        };
        var cmds = DeployService.BuildP2PCommands(link, sideA: true);
        Assert.NotEmpty(cmds);
        Assert.Contains(cmds, c => c.Contains("vlan-id 1017"));
        Assert.Contains(cmds, c => c.Contains("10.5.17.1"));
        Assert.Contains(cmds, c => c.Contains("xe-1/1/31"));
    }

    [Fact]
    public void BuildP2PCommands_SideB_GeneratesCorrectCommands()
    {
        var link = new P2PLink
        {
            Vlan = "1017", DeviceA = "CORE01", DeviceB = "CORE02",
            DeviceAIp = "10.5.17.1", DeviceBIp = "10.5.17.2",
            PortA = "xe-1/1/31", PortB = "xe-1/1/32",
            Subnet = "10.5.17.0/30"
        };
        var cmds = DeployService.BuildP2PCommands(link, sideA: false);
        Assert.Contains(cmds, c => c.Contains("10.5.17.2"));
        Assert.Contains(cmds, c => c.Contains("xe-1/1/32"));
    }

    [Fact]
    public void BuildP2PCommands_EmptyVlan_ReturnsEmpty()
    {
        var link = new P2PLink { Vlan = "" };
        var cmds = DeployService.BuildP2PCommands(link, sideA: true);
        Assert.Empty(cmds);
    }

    // ── B2B BuildConfig ──

    [Fact]
    public void BuildB2BCommands_WithBgp_IncludesBgpNeighbor()
    {
        var link = new B2BLink
        {
            Vlan = "1022", DeviceA = "CORE01", DeviceB = "CORE02",
            BuildingA = "MEP-91", BuildingB = "MEP-93",
            DeviceAIp = "10.5.22.1", DeviceBIp = "10.5.22.2",
            PortA = "xe-1/1/30", PortB = "xe-1/1/30",
            Subnet = "10.5.22.0/30", PeerAsn = "65113"
        };
        var cmds = DeployService.BuildB2BCommands(link, sideA: true);
        Assert.Contains(cmds, c => c.Contains("bgp neighbor"));
        Assert.Contains(cmds, c => c.Contains("65113"));
    }

    [Fact]
    public void BuildB2BCommands_EmptyVlan_ReturnsEmpty()
    {
        var link = new B2BLink { Vlan = "" };
        Assert.Empty(DeployService.BuildB2BCommands(link, sideA: true));
    }

    // ── FW BuildConfig ──

    [Fact]
    public void BuildFWCommands_SideA_GeneratesCorrectCommands()
    {
        var link = new FWLink
        {
            Vlan = "235", Switch = "CORE01", Firewall = "FW01",
            SwitchIp = "10.11.235.1", FirewallIp = "10.11.235.2",
            SwitchPort = "xe-1/1/25", FirewallPort = "eth1",
            Subnet = "10.11.235.0/30"
        };
        var cmds = DeployService.BuildFWCommands(link, sideA: true);
        Assert.Contains(cmds, c => c.Contains("vlan-id 235"));
        Assert.Contains(cmds, c => c.Contains("FW-FW01"));
    }

    [Fact]
    public void BuildFWCommands_EmptyVlan_ReturnsEmpty()
    {
        var link = new FWLink { Vlan = "" };
        Assert.Empty(DeployService.BuildFWCommands(link, sideA: true));
    }

    // ── ResolveCredentials ──

    [Fact]
    public void ResolveCredentials_FromSwitch()
    {
        var switches = new[]
        {
            new SwitchRecord { Hostname = "CORE01", ManagementIp = "10.11.152.1", SshUsername = "root", SshPort = 22, SshPassword = "secret" }
        };
        var creds = DeployService.ResolveCredentials("CORE01", switches, Array.Empty<DeviceRecord>(), "admin", "admin123", 22);
        Assert.Equal("10.11.152.1", creds.Ip);
        Assert.Equal("root", creds.Username);
        Assert.Equal("secret", creds.Password);
        Assert.True(creds.IsValid);
    }

    [Fact]
    public void ResolveCredentials_FallsBackToDevice()
    {
        var devices = new[]
        {
            new DeviceRecord { SwitchName = "SERVER01", ManagementIp = "10.11.120.10" }
        };
        var creds = DeployService.ResolveCredentials("SERVER01", Array.Empty<SwitchRecord>(), devices, "admin", "pass", 22);
        Assert.Equal("10.11.120.10", creds.Ip);
        Assert.Equal("admin", creds.Username);
    }

    [Fact]
    public void ResolveCredentials_NoMatch_DefaultsUsed()
    {
        var creds = DeployService.ResolveCredentials("UNKNOWN", Array.Empty<SwitchRecord>(), Array.Empty<DeviceRecord>(), "defaultuser", "defaultpass", 2222);
        Assert.Null(creds.Ip);
        Assert.Equal("defaultuser", creds.Username);
        Assert.Equal(2222, creds.Port);
        Assert.False(creds.IsValid);
    }

    [Fact]
    public void ResolveCredentials_SwitchOverrideIp_UsedFirst()
    {
        var switches = new[]
        {
            new SwitchRecord { Hostname = "CORE01", ManagementIp = "10.11.152.1", SshOverrideIp = "192.168.1.100", SshUsername = "admin", SshPassword = "p" }
        };
        var creds = DeployService.ResolveCredentials("CORE01", switches, Array.Empty<DeviceRecord>(), null, null, 22);
        Assert.Equal("192.168.1.100", creds.Ip);
    }

    // ── SshCredentials ──

    [Fact]
    public void SshCredentials_IsValid_WithIpAndPassword()
    {
        var c = new SshCredentials { Ip = "10.0.0.1", Password = "pass" };
        Assert.True(c.IsValid);
    }

    [Fact]
    public void SshCredentials_IsValid_NoIp_False()
    {
        var c = new SshCredentials { Ip = null, Password = "pass" };
        Assert.False(c.IsValid);
    }

    [Fact]
    public void SshCredentials_IsValid_NoPassword_False()
    {
        var c = new SshCredentials { Ip = "10.0.0.1", Password = null };
        Assert.False(c.IsValid);
    }

    [Fact]
    public void SshCredentials_Defaults()
    {
        var c = new SshCredentials();
        Assert.Equal(22, c.Port);
        Assert.Equal("admin", c.Username);
        Assert.Equal("", c.DeviceName);
    }
}
