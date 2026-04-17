using Central.Core.Models;

namespace Central.Tests.Models;

public class IpRangeTests
{
    [Fact]
    public void Defaults()
    {
        var r = new IpRange();
        Assert.Equal(0, r.Id);
        Assert.Equal("", r.Region);
        Assert.Equal("", r.PoolName);
        Assert.Equal("", r.Block);
        Assert.Equal("", r.Purpose);
        Assert.Equal("", r.Notes);
        Assert.Equal("Active", r.Status);
    }

    [Fact]
    public void PropertyChanged_AllFields()
    {
        var r = new IpRange();
        var changed = new List<string>();
        r.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        r.Id = 1;
        r.Region = "UK";
        r.PoolName = "Servers";
        r.Block = "10.0.0.0/24";
        r.Purpose = "Production servers";
        r.Notes = "Allocated 2026-01";
        r.Status = "Reserved";

        Assert.Contains("Id", changed);
        Assert.Contains("Region", changed);
        Assert.Contains("PoolName", changed);
        Assert.Contains("Block", changed);
        Assert.Contains("Purpose", changed);
        Assert.Contains("Notes", changed);
        Assert.Contains("Status", changed);
    }
}
