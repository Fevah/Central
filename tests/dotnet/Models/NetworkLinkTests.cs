using Central.Engine.Models;

namespace Central.Tests.Models;

public class NetworkLinkTests
{
    // ── LinkHelper ──

    [Theory]
    [InlineData("10.0.0.0/30", "30")]
    [InlineData("172.16.0.0/24", "24")]
    [InlineData("/31", "31")]
    [InlineData("", "30")]
    [InlineData(null, "30")]
    [InlineData("10.0.0.0", "30")] // no slash
    public void LinkHelper_ExtractPrefix(string? subnet, string expected)
    {
        Assert.Equal(expected, LinkHelper.ExtractPrefix(subnet));
    }

    // ── P2PLink ──

    [Fact]
    public void P2PLink_BuildConfig_SideA_GeneratesCommands()
    {
        var link = new P2PLink
        {
            Vlan = "1017",
            DeviceA = "CORE01",
            DeviceB = "CORE02",
            DeviceAIp = "10.5.17.1",
            DeviceBIp = "10.5.17.2",
            PortA = "xe-1/1/31",
            PortB = "xe-1/1/31",
            Subnet = "/30"
        };

        var cmds = link.BuildConfig(sideA: true);

        Assert.Contains(cmds, c => c.Contains("set vlans vlan-id 1017"));
        Assert.Contains(cmds, c => c.Contains("l3-interface") && c.Contains("vlan-1017"));
        Assert.Contains(cmds, c => c.Contains("address 10.5.17.1 prefix-length 30"));
        Assert.Contains(cmds, c => c.Contains("xe-1/1/31"));
    }

    [Fact]
    public void P2PLink_BuildConfig_SideB_UsesCorrectIpAndPort()
    {
        var link = new P2PLink
        {
            Vlan = "1017",
            DeviceA = "CORE01",
            DeviceB = "CORE02",
            DeviceAIp = "10.5.17.1",
            DeviceBIp = "10.5.17.2",
            PortA = "xe-1/1/31",
            PortB = "xe-1/1/32",
            Subnet = "/30"
        };

        var cmds = link.BuildConfig(sideA: false);

        Assert.Contains(cmds, c => c.Contains("address 10.5.17.2 prefix-length 30"));
        Assert.Contains(cmds, c => c.Contains("xe-1/1/32"));
    }

    [Fact]
    public void P2PLink_BuildConfig_EmptyVlan_ReturnsEmpty()
    {
        var link = new P2PLink { Vlan = "" };
        Assert.Empty(link.BuildConfig(sideA: true));
    }

    [Fact]
    public void P2PLink_MismatchA_AffectsColor()
    {
        var link = new P2PLink();
        Assert.Equal("#D4D4D4", link.DescAColor); // default

        var changedProps = new List<string>();
        link.PropertyChanged += (_, e) => changedProps.Add(e.PropertyName!);

        link.MismatchA = true;
        Assert.Equal("#EF4444", link.DescAColor); // red
        Assert.Contains("DescAColor", changedProps);
    }

    [Fact]
    public void P2PLink_MismatchB_AffectsColor()
    {
        var link = new P2PLink { MismatchB = true };
        Assert.Equal("#EF4444", link.DescBColor);

        link.MismatchB = false;
        Assert.Equal("#D4D4D4", link.DescBColor);
    }

    // ── B2BLink ──

    [Fact]
    public void B2BLink_BuildConfig_SideA_IncludesBgpNeighbor()
    {
        var link = new B2BLink
        {
            Vlan = "1022",
            BuildingA = "MEP-91",
            BuildingB = "MEP-93",
            DeviceA = "CORE01",
            DeviceB = "CORE02",
            DeviceAIp = "10.5.22.1",
            DeviceBIp = "10.5.22.2",
            PortA = "xe-1/1/30",
            PortB = "xe-1/1/30",
            Subnet = "/30",
            PeerAsn = "65113"
        };

        var cmds = link.BuildConfig(sideA: true);

        Assert.Contains(cmds, c => c.Contains("set vlans vlan-id 1022"));
        Assert.Contains(cmds, c => c.Contains("B2B-MEP-93"));
        Assert.Contains(cmds, c => c.Contains("address 10.5.22.1 prefix-length 30"));
        Assert.Contains(cmds, c => c.Contains("bgp neighbor 10.5.22.2 remote-as \"65113\" bfd"));
    }

    [Fact]
    public void B2BLink_BuildConfig_NoPeerAsn_SkipsBgp()
    {
        var link = new B2BLink
        {
            Vlan = "1022",
            BuildingA = "MEP-91",
            BuildingB = "MEP-93",
            DeviceA = "CORE01",
            DeviceB = "CORE02",
            DeviceAIp = "10.5.22.1",
            DeviceBIp = "10.5.22.2",
            Subnet = "/30",
            PeerAsn = "" // empty
        };

        var cmds = link.BuildConfig(sideA: true);
        Assert.DoesNotContain(cmds, c => c.Contains("bgp neighbor"));
    }

    [Fact]
    public void B2BLink_BuildConfig_EmptyVlan_ReturnsEmpty()
    {
        var link = new B2BLink { Vlan = "" };
        Assert.Empty(link.BuildConfig(sideA: true));
    }

    // ── FWLink ──

    [Fact]
    public void FWLink_BuildConfig_SwitchSide()
    {
        var link = new FWLink
        {
            Vlan = "235",
            Switch = "CORE01",
            Firewall = "FW01",
            SwitchIp = "10.11.235.1",
            FirewallIp = "10.11.235.2",
            SwitchPort = "xe-1/1/10",
            FirewallPort = "eth0",
            Subnet = "/24"
        };

        var cmds = link.BuildConfig(sideA: true);

        Assert.Contains(cmds, c => c.Contains("set vlans vlan-id 235"));
        Assert.Contains(cmds, c => c.Contains("FW-FW01"));
        Assert.Contains(cmds, c => c.Contains("address 10.11.235.1 prefix-length 24"));
        Assert.Contains(cmds, c => c.Contains("xe-1/1/10"));
    }

    [Fact]
    public void FWLink_BuildConfig_FirewallSide()
    {
        var link = new FWLink
        {
            Vlan = "235",
            Switch = "CORE01",
            Firewall = "FW01",
            SwitchIp = "10.11.235.1",
            FirewallIp = "10.11.235.2",
            SwitchPort = "xe-1/1/10",
            FirewallPort = "eth0",
            Subnet = "/24"
        };

        var cmds = link.BuildConfig(sideA: false);

        Assert.Contains(cmds, c => c.Contains("address 10.11.235.2 prefix-length 24"));
        Assert.Contains(cmds, c => c.Contains("eth0"));
    }

    [Fact]
    public void FWLink_BuildConfig_EmptyVlan_ReturnsEmpty()
    {
        var link = new FWLink { Vlan = "" };
        Assert.Empty(link.BuildConfig(sideA: true));
    }

    // ── NetworkLinkBase.Validate ──

    [Fact]
    public void Validate_AllFieldsSet_ReturnsEmpty()
    {
        var link = new P2PLink
        {
            DeviceA = "CORE01",
            DeviceB = "CORE02",
            Vlan = "1017",
            Subnet = "/30"
        };

        Assert.Empty(link.Validate());
        Assert.True(link.IsComplete);
    }

    [Fact]
    public void Validate_MissingDeviceA_ReturnsWarning()
    {
        var link = new P2PLink { DeviceA = "", DeviceB = "CORE02", Vlan = "1017", Subnet = "/30" };
        var warnings = link.Validate();
        Assert.Contains(warnings, w => w.Contains("Device A"));
        Assert.False(link.IsComplete);
    }

    [Fact]
    public void Validate_MissingAllFields_Returns4Warnings()
    {
        var link = new P2PLink { DeviceA = "", DeviceB = "", Vlan = "", Subnet = "" };
        Assert.Equal(4, link.Validate().Count);
    }

    [Fact]
    public void ValidationIcon_Complete()
    {
        var link = new P2PLink { DeviceA = "A", DeviceB = "B", Vlan = "100", Subnet = "/24" };
        Assert.Contains("\u2713", link.ValidationIcon);
    }

    [Fact]
    public void ValidationIcon_Incomplete()
    {
        var link = new P2PLink();
        Assert.Contains("\u26A0", link.ValidationIcon);
    }

    [Fact]
    public void ValidationColor_Complete_Green()
    {
        var link = new P2PLink { DeviceA = "A", DeviceB = "B", Vlan = "100", Subnet = "/24" };
        Assert.Equal("#22C55E", link.ValidationColor);
    }

    [Fact]
    public void ValidationColor_Incomplete_Amber()
    {
        var link = new P2PLink();
        Assert.Equal("#F59E0B", link.ValidationColor);
    }

    [Fact]
    public void ConfigA_And_ConfigB_ReturnJoinedCommands()
    {
        var link = new P2PLink
        {
            Vlan = "100",
            DeviceA = "SW1",
            DeviceB = "SW2",
            DeviceAIp = "10.0.0.1",
            DeviceBIp = "10.0.0.2",
            PortA = "xe-1/1/1",
            PortB = "xe-1/1/2",
            Subnet = "/30"
        };

        Assert.False(string.IsNullOrEmpty(link.ConfigA));
        Assert.False(string.IsNullOrEmpty(link.ConfigB));
        Assert.Contains("10.0.0.1", link.ConfigA);
        Assert.Contains("10.0.0.2", link.ConfigB);
    }

    [Fact]
    public void GenerateDetailConfig_PopulatesBothSides()
    {
        var link = new P2PLink
        {
            Vlan = "100",
            DeviceA = "SW1",
            DeviceB = "SW2",
            DeviceAIp = "10.0.0.1",
            DeviceBIp = "10.0.0.2",
            PortA = "xe-1/1/1",
            PortB = "xe-1/1/2",
            Subnet = "/30"
        };

        link.GenerateDetailConfig();

        Assert.True(link.DetailConfigLines.Count > 0);
        Assert.Contains(link.DetailConfigLines, l => l.Side == "SW1");
        Assert.Contains(link.DetailConfigLines, l => l.Side == "SW2");
    }

    // ── PropertyChanged ──

    [Fact]
    public void P2PLink_PropertyChanged_Fires()
    {
        var link = new P2PLink();
        bool fired = false;
        link.PropertyChanged += (_, _) => fired = true;
        link.DeviceA = "CORE01";
        Assert.True(fired);
    }

    [Fact]
    public void B2BLink_PropertyChanged_Fires()
    {
        var link = new B2BLink();
        bool fired = false;
        link.PropertyChanged += (_, _) => fired = true;
        link.BuildingA = "MEP-91";
        Assert.True(fired);
    }

    [Fact]
    public void FWLink_PropertyChanged_Fires()
    {
        var link = new FWLink();
        bool fired = false;
        link.PropertyChanged += (_, _) => fired = true;
        link.Firewall = "FW01";
        Assert.True(fired);
    }
}
