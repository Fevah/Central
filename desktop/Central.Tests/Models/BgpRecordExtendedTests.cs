using Central.Core.Models;

namespace Central.Tests.Models;

/// <summary>
/// Extended tests for BgpRecord — covers PropertyChanged notifications
/// and detail collections.
/// </summary>
public class BgpRecordExtendedTests
{
    [Fact]
    public void AllProperties_FirePropertyChanged()
    {
        var bg = new BgpRecord();
        var changed = new List<string>();
        bg.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        bg.Id = 1;
        bg.Hostname = "CORE01";
        bg.Building = "MEP-91";
        bg.LocalAs = "65112";
        bg.RouterId = "10.11.0.2";
        bg.FastExternalFailover = true;
        bg.EbgpRequiresPolicy = false;
        bg.BestpathMultipathRelax = true;
        bg.RedistributeConnected = true;
        bg.MaxPaths = 4;
        bg.NeighborCount = 3;
        bg.NetworkCount = 5;
        bg.LastSynced = new DateTime(2026, 1, 15);

        Assert.Contains("Id", changed);
        Assert.Contains("Hostname", changed);
        Assert.Contains("Building", changed);
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

    [Fact]
    public void DetailNeighbors_IsObservableCollection()
    {
        var bg = new BgpRecord();
        Assert.NotNull(bg.DetailNeighbors);
        Assert.Empty(bg.DetailNeighbors);

        bg.DetailNeighbors.Add(new BgpNeighborRecord { NeighborIp = "10.5.17.2" });
        Assert.Single(bg.DetailNeighbors);
    }

    [Fact]
    public void DetailNetworks_IsObservableCollection()
    {
        var bg = new BgpRecord();
        Assert.NotNull(bg.DetailNetworks);
        Assert.Empty(bg.DetailNetworks);

        bg.DetailNetworks.Add(new BgpNetworkRecord { NetworkPrefix = "10.11.0.0/16" });
        Assert.Single(bg.DetailNetworks);
    }
}
