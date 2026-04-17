using Central.Core.Models;

namespace Central.Tests.Models;

public class FWLinkTests
{
    [Fact]
    public void PropertyChanged_AllFieldsFire()
    {
        var f = new FWLink();
        var changed = new List<string>();
        f.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        f.Id = 1;
        f.Building = "MEP-91";
        f.LinkId = "FW-001";
        f.Vlan = "235";
        f.Switch = "CORE01";
        f.SwitchPort = "xe-1/1/20";
        f.SwitchIp = "10.11.235.1";
        f.Firewall = "PA-3050";
        f.FirewallPort = "eth1/1";
        f.FirewallIp = "10.11.235.2";
        f.Subnet = "/30";
        f.Status = "Active";

        Assert.Contains("Id", changed);
        Assert.Contains("Building", changed);
        Assert.Contains("LinkId", changed);
        Assert.Contains("Vlan", changed);
        Assert.Contains("Subnet", changed);
        Assert.Contains("Status", changed);
    }

    [Fact]
    public void Defaults()
    {
        var f = new FWLink();
        Assert.Equal(0, f.Id);
        Assert.Equal("", f.LinkId);
        Assert.Equal("Active", f.Status);
    }

    [Fact]
    public void DeviceA_MapToSwitch()
    {
        var f = new FWLink { Switch = "CORE01" };
        Assert.Equal("CORE01", f.DeviceA);
    }

    [Fact]
    public void DeviceB_MapToFirewall()
    {
        var f = new FWLink { Firewall = "PA-3050" };
        Assert.Equal("PA-3050", f.DeviceB);
    }

    [Fact]
    public void BuildConfig_SideA_GeneratesCommands()
    {
        var f = new FWLink
        {
            Vlan = "235",
            Switch = "CORE01", SwitchPort = "xe-1/1/20", SwitchIp = "10.11.235.1",
            Firewall = "PA-3050", FirewallPort = "eth1/1", FirewallIp = "10.11.235.2",
            Subnet = "/30"
        };

        var cmds = f.BuildConfig(sideA: true);
        Assert.Contains(cmds, c => c.Contains("set vlans vlan-id 235"));
        Assert.Contains(cmds, c => c.Contains("FW-PA-3050"));
        Assert.Contains(cmds, c => c.Contains("10.11.235.1") && c.Contains("30"));
        Assert.Contains(cmds, c => c.Contains("xe-1/1/20"));
    }

    [Fact]
    public void BuildConfig_SideB_UsesFirewallSide()
    {
        var f = new FWLink
        {
            Vlan = "235",
            Switch = "CORE01", SwitchPort = "xe-1/1/20", SwitchIp = "10.11.235.1",
            Firewall = "PA-3050", FirewallPort = "eth1/1", FirewallIp = "10.11.235.2",
            Subnet = "/30"
        };

        var cmds = f.BuildConfig(sideA: false);
        Assert.Contains(cmds, c => c.Contains("10.11.235.2"));
        Assert.Contains(cmds, c => c.Contains("eth1/1"));
    }

    [Fact]
    public void BuildConfig_EmptyVlan_ReturnsEmpty()
    {
        var f = new FWLink { Vlan = "" };
        Assert.Empty(f.BuildConfig(sideA: true));
    }

    [Fact]
    public void Validate_Complete()
    {
        var f = new FWLink
        {
            DeviceA = "CORE01", DeviceB = "PA-3050",
            Vlan = "235", Subnet = "/30"
        };
        Assert.Empty(f.Validate());
    }
}
