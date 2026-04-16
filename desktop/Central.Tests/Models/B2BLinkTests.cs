using Central.Core.Models;

namespace Central.Tests.Models;

public class B2BLinkTests
{
    // ── PropertyChanged ──

    [Fact]
    public void PropertyChanged_AllFieldsFire()
    {
        var b = new B2BLink();
        var changed = new List<string>();
        b.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        b.Id = 1;
        b.LinkId = "B2B-001";
        b.Vlan = "1017";
        b.BuildingA = "MEP-91";
        b.DeviceA = "CORE01";
        b.PortA = "xe-1/1/31";
        b.ModuleA = "mod1";
        b.DeviceAIp = "10.0.0.1/30";
        b.BuildingB = "MEP-92";
        b.DeviceB = "CORE02";
        b.PortB = "xe-1/1/32";
        b.ModuleB = "mod2";
        b.DeviceBIp = "10.0.0.2/30";
        b.Tx = "1310nm";
        b.Rx = "1310nm";
        b.Media = "SMF";
        b.Speed = "10G";
        b.Subnet = "/30";
        b.Status = "Active";
        b.PeerAsn = "65112";

        Assert.Contains("Id", changed);
        Assert.Contains("LinkId", changed);
        Assert.Contains("Vlan", changed);
        Assert.Contains("BuildingA", changed);
        Assert.Contains("DeviceA", changed);
        Assert.Contains("PortA", changed);
        Assert.Contains("ModuleA", changed);
        Assert.Contains("DeviceAIp", changed);
        Assert.Contains("BuildingB", changed);
        Assert.Contains("DeviceB", changed);
        Assert.Contains("PortB", changed);
        Assert.Contains("ModuleB", changed);
        Assert.Contains("DeviceBIp", changed);
        Assert.Contains("Tx", changed);
        Assert.Contains("Rx", changed);
        Assert.Contains("Media", changed);
        Assert.Contains("Speed", changed);
        Assert.Contains("Subnet", changed);
        Assert.Contains("Status", changed);
    }

    [Fact]
    public void Defaults_AreEmpty()
    {
        var b = new B2BLink();
        Assert.Equal(0, b.Id);
        Assert.Equal("", b.LinkId);
        Assert.Equal("", b.Vlan);
        Assert.Equal("Active", b.Status);
        Assert.Equal("", b.PeerAsn);
    }

    // ── BuildConfig ──

    [Fact]
    public void BuildConfig_SideA_GeneratesVlanAndPort()
    {
        var b = new B2BLink
        {
            Vlan = "1017", BuildingB = "MEP-92",
            DeviceA = "CORE01", PortA = "xe-1/1/31", DeviceAIp = "10.5.17.1/30",
            DeviceB = "CORE02", PortB = "xe-1/1/32", DeviceBIp = "10.5.17.2/30",
            Subnet = "/30", LinkId = "Link-001", PeerAsn = "65121"
        };

        var cmds = b.BuildConfig(sideA: true);

        Assert.Contains(cmds, c => c.Contains("set vlans vlan-id 1017"));
        Assert.Contains(cmds, c => c.Contains("vlan-1017"));
        Assert.Contains(cmds, c => c.Contains("10.5.17.1") && c.Contains("30"));
        Assert.Contains(cmds, c => c.Contains("xe-1/1/31"));
    }

    [Fact]
    public void BuildConfig_SideA_AddsBgpNeighbor()
    {
        var b = new B2BLink
        {
            Vlan = "1017", BuildingB = "MEP-92",
            DeviceA = "CORE01", DeviceAIp = "10.5.17.1/30",
            DeviceB = "CORE02", DeviceBIp = "10.5.17.2/30",
            Subnet = "/30", LinkId = "Link-001", PeerAsn = "65121"
        };

        var cmds = b.BuildConfig(sideA: true);

        Assert.Contains(cmds, c => c.Contains("bgp neighbor 10.5.17.2") && c.Contains("65121"));
    }

    [Fact]
    public void BuildConfig_EmptyVlan_ReturnsEmpty()
    {
        var b = new B2BLink { Vlan = "" };
        Assert.Empty(b.BuildConfig(sideA: true));
    }

    [Fact]
    public void BuildConfig_NoPeerAsn_SkipsBgp()
    {
        var b = new B2BLink
        {
            Vlan = "1017", BuildingB = "MEP-92",
            DeviceA = "CORE01", DeviceAIp = "10.5.17.1/30",
            DeviceB = "CORE02", DeviceBIp = "10.5.17.2/30",
            Subnet = "/30", LinkId = "Link-001", PeerAsn = ""
        };

        var cmds = b.BuildConfig(sideA: true);
        Assert.DoesNotContain(cmds, c => c.Contains("bgp neighbor"));
    }

    // ── Validation (inherited from NetworkLinkBase) ──

    [Fact]
    public void Validate_IncompleteLink_HasWarnings()
    {
        var b = new B2BLink();
        var warnings = b.Validate();
        Assert.Contains(warnings, w => w.Contains("Device A"));
        Assert.Contains(warnings, w => w.Contains("Device B"));
        Assert.Contains(warnings, w => w.Contains("VLAN"));
    }

    [Fact]
    public void Validate_CompleteLink_NoWarnings()
    {
        var b = new B2BLink
        {
            DeviceA = "CORE01", DeviceB = "CORE02",
            Vlan = "1017", Subnet = "/30"
        };
        Assert.Empty(b.Validate());
        Assert.True(b.IsComplete);
    }

    [Fact]
    public void ConfigA_JoinsCommands()
    {
        var b = new B2BLink
        {
            Vlan = "1017", BuildingB = "MEP-92",
            DeviceA = "CORE01", DeviceAIp = "10.5.17.1/30",
            DeviceB = "CORE02", DeviceBIp = "10.5.17.2/30",
            Subnet = "/30", LinkId = "Link-001"
        };

        Assert.Contains("set vlans", b.ConfigA);
    }

    [Fact]
    public void GenerateDetailConfig_PopulatesBothSides()
    {
        var b = new B2BLink
        {
            Vlan = "1017", BuildingB = "MEP-92",
            DeviceA = "CORE01", PortA = "xe-1/1/31", DeviceAIp = "10.5.17.1/30",
            DeviceB = "CORE02", PortB = "xe-1/1/32", DeviceBIp = "10.5.17.2/30",
            Subnet = "/30", LinkId = "Link-001"
        };
        b.GenerateDetailConfig();

        Assert.NotEmpty(b.DetailConfigLines);
        Assert.Contains(b.DetailConfigLines, l => l.Side == "CORE01");
        Assert.Contains(b.DetailConfigLines, l => l.Side == "CORE02");
    }
}
