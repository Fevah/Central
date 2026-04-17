using Central.Engine.Models;

namespace Central.Tests.Models;

public class MlagConfigTests
{
    [Fact]
    public void PropertyChanged_AllFieldsFire()
    {
        var m = new MlagConfig();
        var changed = new List<string>();
        m.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        m.Id = 1;
        m.Building = "MEP-91";
        m.DomainType = "MLAG";
        m.MlagDomain = "domain-1";
        m.SwitchA = "CORE01";
        m.SwitchB = "CORE02";
        m.B2BPartner = "CORE02";
        m.Status = "Active";
        m.PeerLinkAe = "ae-1";
        m.PhysicalMembers = "xe-1/1/31,xe-1/1/32";
        m.PeerVlan = "4094";
        m.TrunkVlans = "1-500";
        m.SharedDomainMac = "00:11:22:33:44:55";
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

    [Fact]
    public void Defaults()
    {
        var m = new MlagConfig();
        Assert.Equal(0, m.Id);
        Assert.Equal("", m.Building);
        Assert.Equal("Active", m.Status);
        Assert.Equal("", m.Notes);
    }
}
