using Central.Engine.Models;

namespace Central.Tests.Models;

public class NetworkLinkExtendedTests
{
    // ── LinkHelper ──

    [Theory]
    [InlineData("10.5.17.0/30", "30")]
    [InlineData("10.0.0.0/24", "24")]
    [InlineData("192.168.1.0/16", "16")]
    [InlineData("", "30")]
    [InlineData(null, "30")]
    [InlineData("10.0.0.0", "30")]  // no slash
    public void LinkHelper_ExtractPrefix(string? subnet, string expected)
    {
        Assert.Equal(expected, LinkHelper.ExtractPrefix(subnet));
    }

    // ── P2P Validate ──

    [Fact]
    public void P2PLink_Validate_Complete_NoWarnings()
    {
        var link = new P2PLink
        {
            DeviceA = "CORE01", DeviceB = "CORE02",
            Vlan = "1017", Subnet = "10.5.17.0/30"
        };
        Assert.Empty(link.Validate());
        Assert.True(link.IsComplete);
    }

    [Fact]
    public void P2PLink_Validate_MissingDeviceA_Warning()
    {
        var link = new P2PLink { DeviceA = "", DeviceB = "CORE02", Vlan = "1017", Subnet = "10.0.0.0/30" };
        var warnings = link.Validate();
        Assert.Contains(warnings, w => w.Contains("Device A"));
    }

    [Fact]
    public void P2PLink_Validate_MissingVlan_Warning()
    {
        var link = new P2PLink { DeviceA = "A", DeviceB = "B", Vlan = "", Subnet = "10.0.0.0/30" };
        var warnings = link.Validate();
        Assert.Contains(warnings, w => w.Contains("VLAN"));
    }

    [Fact]
    public void P2PLink_Validate_MissingSubnet_Warning()
    {
        var link = new P2PLink { DeviceA = "A", DeviceB = "B", Vlan = "100", Subnet = "" };
        var warnings = link.Validate();
        Assert.Contains(warnings, w => w.Contains("Subnet"));
    }

    [Fact]
    public void P2PLink_ValidationIcon_Complete_Check()
    {
        var link = new P2PLink { DeviceA = "A", DeviceB = "B", Vlan = "100", Subnet = "10/30" };
        Assert.Equal("✓", link.ValidationIcon);
        Assert.Equal("#22C55E", link.ValidationColor);
    }

    [Fact]
    public void P2PLink_ValidationIcon_Incomplete_Warning()
    {
        var link = new P2PLink { DeviceA = "", DeviceB = "", Vlan = "", Subnet = "" };
        Assert.Equal("⚠", link.ValidationIcon);
        Assert.Equal("#F59E0B", link.ValidationColor);
    }

    [Fact]
    public void P2PLink_ValidationTooltip_Incomplete_ContainsWarnings()
    {
        var link = new P2PLink { DeviceA = "", DeviceB = "", Vlan = "", Subnet = "" };
        Assert.Contains("Device A", link.ValidationTooltip);
    }

    [Fact]
    public void P2PLink_ValidationTooltip_Complete_Ready()
    {
        var link = new P2PLink { DeviceA = "A", DeviceB = "B", Vlan = "1", Subnet = "s" };
        Assert.Contains("Ready", link.ValidationTooltip);
    }

    // ── P2P MismatchA/B ──

    [Fact]
    public void P2PLink_MismatchA_Color()
    {
        var link = new P2PLink { MismatchA = true };
        Assert.Equal("#EF4444", link.DescAColor);
    }

    [Fact]
    public void P2PLink_NoMismatch_DefaultColor()
    {
        var link = new P2PLink { MismatchA = false };
        Assert.Equal("#D4D4D4", link.DescAColor);
    }

    [Fact]
    public void P2PLink_MismatchB_Color()
    {
        var link = new P2PLink { MismatchB = true };
        Assert.Equal("#EF4444", link.DescBColor);
    }

    // ── GenerateDetailConfig ──

    [Fact]
    public void P2PLink_GenerateDetailConfig_PopulatesLines()
    {
        var link = new P2PLink
        {
            DeviceA = "SW1", DeviceB = "SW2", Vlan = "100",
            PortA = "xe-1/1/1", PortB = "xe-1/1/2",
            DeviceAIp = "10.0.0.1", DeviceBIp = "10.0.0.2",
            Subnet = "10.0.0.0/30"
        };
        link.GenerateDetailConfig();
        Assert.NotEmpty(link.DetailConfigLines);
        Assert.Contains(link.DetailConfigLines, l => l.Side == "SW1");
        Assert.Contains(link.DetailConfigLines, l => l.Side == "SW2");
    }

    [Fact]
    public void B2BLink_GenerateDetailConfig_IncludesBgp()
    {
        var link = new B2BLink
        {
            DeviceA = "SW1", DeviceB = "SW2", Vlan = "1022",
            BuildingA = "MEP-91", BuildingB = "MEP-93",
            DeviceAIp = "10.0.0.1", DeviceBIp = "10.0.0.2",
            PortA = "xe-1/1/30", PortB = "xe-1/1/30",
            Subnet = "10.0.0.0/30", PeerAsn = "65113"
        };
        link.GenerateDetailConfig();
        Assert.Contains(link.DetailConfigLines, l => l.Command.Contains("bgp"));
    }

    // ── FWLink ──

    [Fact]
    public void FWLink_BuildConfig_SideA_HasFWPrefix()
    {
        var link = new FWLink
        {
            Vlan = "235", Switch = "CORE01", Firewall = "FW01",
            SwitchIp = "10.0.0.1", FirewallIp = "10.0.0.2",
            SwitchPort = "xe-1/1/25", FirewallPort = "eth1",
            Subnet = "10.0.0.0/30"
        };
        var cmds = link.BuildConfig(sideA: true);
        Assert.Contains(cmds, c => c.Contains("FW-FW01"));
    }

    [Fact]
    public void FWLink_BuildConfig_SideB_HasFWPrefix()
    {
        var link = new FWLink
        {
            Vlan = "235", Switch = "CORE01", Firewall = "FW01",
            SwitchIp = "10.0.0.1", FirewallIp = "10.0.0.2",
            SwitchPort = "xe-1/1/25", FirewallPort = "eth1",
            Subnet = "10.0.0.0/30"
        };
        var cmds = link.BuildConfig(sideA: false);
        Assert.Contains(cmds, c => c.Contains("FW-CORE01"));
    }

    // ── ConfigA / ConfigB ──

    [Fact]
    public void P2PLink_ConfigA_ReturnsJoinedString()
    {
        var link = new P2PLink
        {
            DeviceA = "SW1", DeviceB = "SW2", Vlan = "100",
            PortA = "xe-1/1/1", DeviceAIp = "10.0.0.1",
            Subnet = "10.0.0.0/30"
        };
        Assert.NotEmpty(link.ConfigA);
        Assert.Contains("vlan-id 100", link.ConfigA);
    }

    [Fact]
    public void P2PLink_ConfigB_ReturnsJoinedString()
    {
        var link = new P2PLink
        {
            DeviceA = "SW1", DeviceB = "SW2", Vlan = "100",
            PortB = "xe-1/1/2", DeviceBIp = "10.0.0.2",
            Subnet = "10.0.0.0/30"
        };
        Assert.NotEmpty(link.ConfigB);
    }

    // ── LinkConfigLine ──

    [Fact]
    public void LinkConfigLine_Defaults()
    {
        var lcl = new LinkConfigLine();
        Assert.Equal("", lcl.Side);
        Assert.Equal("", lcl.Command);
    }
}
