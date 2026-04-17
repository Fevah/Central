using Central.Engine.Models;

namespace Central.Tests.Models;

public class ConfigRangeTests
{
    [Fact]
    public void Defaults()
    {
        var r = new ConfigRange();
        Assert.Equal(0, r.Id);
        Assert.Equal("", r.Category);
        Assert.Equal("", r.Name);
        Assert.Equal("", r.RangeStart);
        Assert.Equal("", r.RangeEnd);
        Assert.Equal("", r.Description);
        Assert.Equal(0, r.SortOrder);
    }

    [Fact]
    public void PropertyChanged_AllFields()
    {
        var r = new ConfigRange();
        var changed = new List<string>();
        r.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        r.Id = 1;
        r.Category = "VLAN";
        r.Name = "IT Range";
        r.RangeStart = "100";
        r.RangeEnd = "200";
        r.Description = "IT VLANs";
        r.SortOrder = 1;

        Assert.Contains("Id", changed);
        Assert.Contains("Category", changed);
        Assert.Contains("Name", changed);
        Assert.Contains("RangeStart", changed);
        Assert.Contains("RangeEnd", changed);
        Assert.Contains("Description", changed);
        Assert.Contains("SortOrder", changed);
    }
}
