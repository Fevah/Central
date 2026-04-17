using Central.Core.Models;

namespace Central.Tests.Models;

public class MlagMstpTests
{
    // ── MlagConfig ──

    [Fact]
    public void MlagConfig_Defaults()
    {
        var m = new MlagConfig();
        Assert.Equal(0, m.Id);
        Assert.Equal("", m.Building);
        Assert.Equal("", m.DomainType);
        Assert.Equal("", m.MlagDomain);
        Assert.Equal("", m.SwitchA);
        Assert.Equal("", m.SwitchB);
        Assert.Equal("Active", m.Status);
        Assert.Equal("", m.PeerLinkAe);
        Assert.Equal("", m.PeerLinkSubnet);
        Assert.Equal("", m.Notes);
    }

    [Fact]
    public void MlagConfig_PropertyChanged_AllFields()
    {
        var m = new MlagConfig();
        var changed = new List<string>();
        m.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        m.Id = 1;
        m.Building = "MEP-91";
        m.DomainType = "mlag";
        m.MlagDomain = "domain1";
        m.SwitchA = "CORE01";
        m.SwitchB = "CORE02";
        m.B2BPartner = "MEP-92";
        m.Status = "Active";
        m.PeerLinkAe = "ae-1";
        m.PhysicalMembers = "xe-1/1/31,xe-1/1/32";
        m.PeerVlan = "4000";
        m.TrunkVlans = "1-500";
        m.SharedDomainMac = "aa:bb:cc:dd:ee:ff";
        m.PeerLinkSubnet = "10.0.0.0/30";
        m.Node0Ip = "10.0.0.1";
        m.Node1Ip = "10.0.0.2";
        m.Node0IpLink2 = "10.0.1.1";
        m.Node1IpLink2 = "10.0.1.2";
        m.Notes = "Test notes";

        Assert.Contains("Id", changed);
        Assert.Contains("Building", changed);
        Assert.Contains("DomainType", changed);
        Assert.Contains("MlagDomain", changed);
        Assert.Contains("SwitchA", changed);
        Assert.Contains("SwitchB", changed);
        Assert.Contains("B2BPartner", changed);
        Assert.Contains("Status", changed);
        Assert.Contains("PeerLinkAe", changed);
        Assert.Contains("PhysicalMembers", changed);
        Assert.Contains("PeerVlan", changed);
        Assert.Contains("TrunkVlans", changed);
        Assert.Contains("SharedDomainMac", changed);
        Assert.Contains("PeerLinkSubnet", changed);
        Assert.Contains("Node0Ip", changed);
        Assert.Contains("Node1Ip", changed);
        Assert.Contains("Node0IpLink2", changed);
        Assert.Contains("Node1IpLink2", changed);
        Assert.Contains("Notes", changed);
    }

    // ── MstpConfig ──

    [Fact]
    public void MstpConfig_Defaults()
    {
        var m = new MstpConfig();
        Assert.Equal(0, m.Id);
        Assert.Equal("", m.Building);
        Assert.Equal("", m.DeviceName);
        Assert.Equal("", m.DeviceRole);
        Assert.Equal("", m.MstpPriority);
        Assert.Equal("", m.Notes);
        Assert.Equal("Active", m.Status);
    }

    [Fact]
    public void MstpConfig_PropertyChanged_AllFields()
    {
        var m = new MstpConfig();
        var changed = new List<string>();
        m.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        m.Id = 1;
        m.Building = "MEP-91";
        m.DeviceName = "CORE02";
        m.DeviceRole = "core";
        m.MstpPriority = "6000";
        m.Notes = "Master bridge";
        m.Status = "Active";

        Assert.Contains("Id", changed);
        Assert.Contains("Building", changed);
        Assert.Contains("DeviceName", changed);
        Assert.Contains("DeviceRole", changed);
        Assert.Contains("MstpPriority", changed);
        Assert.Contains("Notes", changed);
        Assert.Contains("Status", changed);
    }
}
