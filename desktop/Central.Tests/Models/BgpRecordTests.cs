using Central.Core.Models;

namespace Central.Tests.Models;

public class BgpRecordTests
{
    [Fact]
    public void BgpRecord_Defaults()
    {
        var bgp = new BgpRecord();
        Assert.Equal(0, bgp.Id);
        Assert.Equal("", bgp.Building);
        Assert.Equal("", bgp.Hostname);
        Assert.Equal("", bgp.LocalAs);
        Assert.Equal("", bgp.RouterId);
        Assert.False(bgp.FastExternalFailover);
        Assert.False(bgp.EbgpRequiresPolicy);
        Assert.False(bgp.BestpathMultipathRelax);
        Assert.False(bgp.RedistributeConnected);
        Assert.Equal(4, bgp.MaxPaths);
        Assert.Equal(0, bgp.NeighborCount);
        Assert.Equal(0, bgp.NetworkCount);
        Assert.Null(bgp.LastSynced);
        Assert.NotNull(bgp.Neighbors);
        Assert.NotNull(bgp.Networks);
        Assert.NotNull(bgp.DetailNeighbors);
        Assert.NotNull(bgp.DetailNetworks);
    }

    [Fact]
    public void BgpRecord_PropertyChanged_AllFields()
    {
        var bgp = new BgpRecord();
        var changed = new List<string>();
        bgp.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        bgp.Id = 1;
        bgp.SwitchId = Guid.NewGuid();
        bgp.Building = "MEP-91";
        bgp.Hostname = "CORE02";
        bgp.LocalAs = "65112";
        bgp.RouterId = "10.0.255.1";
        bgp.FastExternalFailover = true;
        bgp.EbgpRequiresPolicy = true;
        bgp.BestpathMultipathRelax = true;
        bgp.RedistributeConnected = true;
        bgp.MaxPaths = 8;
        bgp.NeighborCount = 3;
        bgp.NetworkCount = 5;
        bgp.LastSynced = DateTime.UtcNow;

        Assert.Contains("Id", changed);
        Assert.Contains("SwitchId", changed);
        Assert.Contains("Building", changed);
        Assert.Contains("Hostname", changed);
        Assert.Contains("LocalAs", changed);
        Assert.Contains("RouterId", changed);
        Assert.Contains("FastExternalFailover", changed);
        Assert.Contains("EbgpRequiresPolicy", changed);
        Assert.Contains("BestpathMultipathRelax", changed);
        Assert.Contains("RedistributeConnected", changed);
        Assert.Contains("MaxPaths", changed);
        Assert.Contains("NeighborCount", changed);
        Assert.Contains("NetworkCount", changed);
        Assert.Contains("LastSynced", changed);
    }

    // ── BgpNeighborRecord ──

    [Fact]
    public void BgpNeighborRecord_Defaults()
    {
        var n = new BgpNeighborRecord();
        Assert.Equal("", n.NeighborIp);
        Assert.Equal("", n.RemoteAs);
        Assert.Equal("", n.Description);
        Assert.False(n.BfdEnabled);
        Assert.True(n.Ipv4Unicast); // defaults to true
    }

    [Fact]
    public void BgpNeighborRecord_PropertyChanged()
    {
        var n = new BgpNeighborRecord();
        var changed = new List<string>();
        n.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        n.NeighborIp = "10.5.17.2";
        n.RemoteAs = "65121";
        n.BfdEnabled = true;

        Assert.Contains("NeighborIp", changed);
        Assert.Contains("RemoteAs", changed);
        Assert.Contains("BfdEnabled", changed);
    }

    // ── BgpNetworkRecord ──

    [Fact]
    public void BgpNetworkRecord_Defaults()
    {
        var n = new BgpNetworkRecord();
        Assert.Equal("", n.NetworkPrefix);
        Assert.Equal(0, n.BgpId);
    }

    [Fact]
    public void BgpNetworkRecord_PropertyChanged()
    {
        var n = new BgpNetworkRecord();
        bool fired = false;
        n.PropertyChanged += (_, _) => fired = true;

        n.NetworkPrefix = "10.11.0.0/16";
        Assert.True(fired);
    }
}
