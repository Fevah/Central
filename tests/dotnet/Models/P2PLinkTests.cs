using Central.Engine.Models;

namespace Central.Tests.Models;

public class P2PLinkTests
{
    [Fact]
    public void PropertyChanged_AllFieldsFire()
    {
        var p = new P2PLink();
        var changed = new List<string>();
        p.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        p.Id = 1;
        p.Region = "North";
        p.Building = "MEP-91";
        p.LinkId = "P2P-001";
        p.Vlan = "1017";
        p.DeviceA = "CORE01";
        p.PortA = "xe-1/1/31";
        p.DeviceAIp = "10.0.0.1";
        p.DescA = "desc-a";
        p.LiveDescA = "live-a";
        p.DeviceB = "CORE02";
        p.PortB = "xe-1/1/32";
        p.DeviceBIp = "10.0.0.2";
        p.DescB = "desc-b";
        p.LiveDescB = "live-b";
        p.Subnet = "/30";
        p.Status = "Active";
        p.MismatchA = true;
        p.MismatchB = true;

        Assert.Contains("Id", changed);
        Assert.Contains("Region", changed);
        Assert.Contains("Building", changed);
        Assert.Contains("LinkId", changed);
        Assert.Contains("Vlan", changed);
        Assert.Contains("DeviceA", changed);
        Assert.Contains("PortA", changed);
        Assert.Contains("DeviceAIp", changed);
        Assert.Contains("DescA", changed);
        Assert.Contains("LiveDescA", changed);
        Assert.Contains("DeviceB", changed);
        Assert.Contains("PortB", changed);
        Assert.Contains("DeviceBIp", changed);
        Assert.Contains("DescB", changed);
        Assert.Contains("LiveDescB", changed);
        Assert.Contains("Subnet", changed);
        Assert.Contains("Status", changed);
        Assert.Contains("MismatchA", changed);
        Assert.Contains("MismatchB", changed);
    }

    [Fact]
    public void Defaults()
    {
        var p = new P2PLink();
        Assert.Equal(0, p.Id);
        Assert.Equal("", p.LinkId);
        Assert.Equal("Active", p.Status);
        Assert.False(p.MismatchA);
        Assert.False(p.MismatchB);
    }

    [Fact]
    public void DescAColor_Green_NoMismatch()
    {
        var p = new P2PLink { MismatchA = false };
        Assert.Equal("#D4D4D4", p.DescAColor);
    }

    [Fact]
    public void DescAColor_Red_OnMismatch()
    {
        var p = new P2PLink { MismatchA = true };
        Assert.Equal("#EF4444", p.DescAColor);
    }

    [Fact]
    public void DescBColor_Red_OnMismatch()
    {
        var p = new P2PLink { MismatchB = true };
        Assert.Equal("#EF4444", p.DescBColor);
    }

    [Fact]
    public void MismatchA_FiresDescAColorChanged()
    {
        var p = new P2PLink();
        var changed = new List<string>();
        p.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        p.MismatchA = true;
        Assert.Contains("DescAColor", changed);
    }

    [Fact]
    public void BuildConfig_SideA_GeneratesCommands()
    {
        var p = new P2PLink
        {
            Vlan = "1017",
            DeviceA = "CORE01", DeviceB = "CORE02",
            DeviceAIp = "10.5.17.1", DeviceBIp = "10.5.17.2",
            PortA = "xe-1/1/31", PortB = "xe-1/1/32",
            Subnet = "/30"
        };

        var cmds = p.BuildConfig(sideA: true);
        Assert.Contains(cmds, c => c.Contains("set vlans vlan-id 1017"));
        Assert.Contains(cmds, c => c.Contains("10.5.17.1") && c.Contains("30"));
        Assert.Contains(cmds, c => c.Contains("xe-1/1/31"));
    }

    [Fact]
    public void BuildConfig_SideB_UsesCorrectIp()
    {
        var p = new P2PLink
        {
            Vlan = "1017",
            DeviceA = "CORE01", DeviceB = "CORE02",
            DeviceAIp = "10.5.17.1", DeviceBIp = "10.5.17.2",
            PortA = "xe-1/1/31", PortB = "xe-1/1/32",
            Subnet = "/30"
        };

        var cmds = p.BuildConfig(sideA: false);
        Assert.Contains(cmds, c => c.Contains("10.5.17.2"));
        Assert.Contains(cmds, c => c.Contains("xe-1/1/32"));
    }

    [Fact]
    public void BuildConfig_EmptyVlan_ReturnsEmpty()
    {
        var p = new P2PLink { Vlan = "" };
        Assert.Empty(p.BuildConfig(sideA: true));
    }

    [Fact]
    public void Validate_IncompleteLink()
    {
        var p = new P2PLink();
        var warnings = p.Validate();
        Assert.True(warnings.Count >= 3);
    }

    [Fact]
    public void Validate_CompleteLink()
    {
        var p = new P2PLink
        {
            DeviceA = "CORE01", DeviceB = "CORE02",
            Vlan = "1017", Subnet = "/30"
        };
        Assert.Empty(p.Validate());
        Assert.True(p.IsComplete);
    }
}
