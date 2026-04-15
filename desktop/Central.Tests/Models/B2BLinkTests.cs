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

    // ── Description template ──

    [Fact]
    public void DescriptionA_UsesDefaultTemplate()
    {
        B2BLink.DescriptionTemplate = "B2B-{LinkNum}-{LocalDevice}-{LocalPort} > {RemoteDevice}-{RemotePort}";

        var b = new B2BLink
        {
            LinkId = "MEP-91-to-MEP-92-Link-001",
            DeviceA = "CORE01", PortA = "xe-1/1/31",
            DeviceB = "CORE02", PortB = "xe-1/1/32"
        };

        Assert.Equal("B2B-001-CORE01-xe-1/1/31 > CORE02-xe-1/1/32", b.DescriptionA);
    }

    [Fact]
    public void DescriptionB_SwapsSides()
    {
        B2BLink.DescriptionTemplate = "B2B-{LinkNum}-{LocalDevice}-{LocalPort} > {RemoteDevice}-{RemotePort}";

        var b = new B2BLink
        {
            LinkId = "MEP-91-to-MEP-92-Link-001",
            DeviceA = "CORE01", PortA = "xe-1/1/31",
            DeviceB = "CORE02", PortB = "xe-1/1/32"
        };

        Assert.Equal("B2B-001-CORE02-xe-1/1/32 > CORE01-xe-1/1/31", b.DescriptionB);
    }

    [Fact]
    public void Description_CustomTemplate()
    {
        var saved = B2BLink.DescriptionTemplate;
        try
        {
            B2BLink.DescriptionTemplate = "{LinkId} VLAN {Vlan}";

            var b = new B2BLink { LinkId = "LNK-001", Vlan = "1017" };

            Assert.Equal("LNK-001 VLAN 1017", b.DescriptionA);
        }
        finally { B2BLink.DescriptionTemplate = saved; }
    }

    [Fact]
    public void Description_ExtractsLinkNum_FromSuffix()
    {
        B2BLink.DescriptionTemplate = "{LinkNum}";

        var b = new B2BLink { LinkId = "MEP-91-Core01-to-MEP-92-Core01-Link-003" };
        Assert.Equal("003", b.DescriptionA);
    }

    [Fact]
    public void Description_LinkNum_NoHyphen_Returns000()
    {
        B2BLink.DescriptionTemplate = "{LinkNum}";

        var b = new B2BLink { LinkId = "SIMPLE" };
        // No hyphen → last segment after '-' not found → still extracts from lastIndexOf
        // "SIMPLE" has no '-', so idx < 0 → linkNum stays "000"
        Assert.Equal("000", b.DescriptionA);
    }

    [Fact]
    public void Description_EmptyLinkId_Returns000()
    {
        B2BLink.DescriptionTemplate = "{LinkNum}";

        var b = new B2BLink { LinkId = "" };
        Assert.Equal("000", b.DescriptionA);
    }

    // ── Live description match ──

    [Fact]
    public void DescriptionMatchA_TrueWhenEmpty()
    {
        var b = new B2BLink();
        Assert.True(b.DescriptionMatchA); // empty live = not yet synced → match
    }

    [Fact]
    public void DescriptionMatchA_TrueWhenMatches()
    {
        B2BLink.DescriptionTemplate = "B2B-{LinkNum}-{LocalDevice}-{LocalPort} > {RemoteDevice}-{RemotePort}";

        var b = new B2BLink
        {
            LinkId = "Link-001",
            DeviceA = "CORE01", PortA = "xe-1/1/31",
            DeviceB = "CORE02", PortB = "xe-1/1/32"
        };
        b.LiveDescriptionA = b.DescriptionA;

        Assert.True(b.DescriptionMatchA);
    }

    [Fact]
    public void DescriptionMatchA_FalseOnMismatch()
    {
        B2BLink.DescriptionTemplate = "B2B-{LinkNum}";

        var b = new B2BLink { LinkId = "Link-001" };
        b.LiveDescriptionA = "Something completely different";

        Assert.False(b.DescriptionMatchA);
    }

    [Fact]
    public void DescriptionMatchA_CaseInsensitive()
    {
        B2BLink.DescriptionTemplate = "B2B-{LinkNum}";

        var b = new B2BLink { LinkId = "Link-001" };
        b.LiveDescriptionA = "b2b-001"; // lowercase

        Assert.True(b.DescriptionMatchA);
    }

    [Fact]
    public void LiveDescriptionA_FiresMatchNotification()
    {
        var b = new B2BLink();
        var changed = new List<string>();
        b.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        b.LiveDescriptionA = "test";

        Assert.Contains("DescriptionMatchA", changed);
    }

    [Fact]
    public void LiveDescriptionB_FiresMatchNotification()
    {
        var b = new B2BLink();
        var changed = new List<string>();
        b.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        b.LiveDescriptionB = "test";

        Assert.Contains("DescriptionMatchB", changed);
    }

    // ── BuildConfig ──

    [Fact]
    public void BuildConfig_SideA_GeneratesVlanAndPort()
    {
        B2BLink.DescriptionTemplate = "B2B-{LinkNum}";

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
        B2BLink.DescriptionTemplate = "B2B-{LinkNum}";

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
        B2BLink.DescriptionTemplate = "B2B-{LinkNum}";

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
        B2BLink.DescriptionTemplate = "B2B-{LinkNum}";

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
        B2BLink.DescriptionTemplate = "B2B-{LinkNum}";

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
