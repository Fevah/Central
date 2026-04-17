using Central.Engine.Models;

namespace Central.Tests.Models;

public class LookupItemTests
{
    // ── Defaults ──

    [Fact]
    public void Defaults_AreCorrect()
    {
        var item = new LookupItem();
        Assert.Equal(0, item.Id);
        Assert.Equal("", item.Category);
        Assert.Equal("", item.Value);
        Assert.Equal(0, item.SortOrder);
        Assert.Equal("", item.GridName);
        Assert.Equal("", item.Module);
    }

    // ── ParentId always null (flat list for TreeList) ──

    [Fact]
    public void ParentId_AlwaysNull()
    {
        var item = new LookupItem { Id = 42 };
        Assert.Null(item.ParentId);
    }

    [Fact]
    public void ParentId_NullEvenWithAllFieldsSet()
    {
        var item = new LookupItem
        {
            Id = 1, Category = "Status", Value = "Active",
            SortOrder = 5, GridName = "Devices", Module = "IPAM"
        };
        Assert.Null(item.ParentId);
    }

    // ── PropertyChanged ──

    [Fact]
    public void PropertyChanged_Fires_OnId()
    {
        var item = new LookupItem();
        string? changed = null;
        item.PropertyChanged += (_, e) => changed = e.PropertyName;
        item.Id = 10;
        Assert.Equal("Id", changed);
    }

    [Fact]
    public void PropertyChanged_Fires_OnCategory()
    {
        var item = new LookupItem();
        string? changed = null;
        item.PropertyChanged += (_, e) => changed = e.PropertyName;
        item.Category = "Building";
        Assert.Equal("Category", changed);
    }

    [Fact]
    public void PropertyChanged_Fires_OnValue()
    {
        var item = new LookupItem();
        string? changed = null;
        item.PropertyChanged += (_, e) => changed = e.PropertyName;
        item.Value = "MEP-91";
        Assert.Equal("Value", changed);
    }

    [Fact]
    public void PropertyChanged_Fires_OnSortOrder()
    {
        var item = new LookupItem();
        string? changed = null;
        item.PropertyChanged += (_, e) => changed = e.PropertyName;
        item.SortOrder = 3;
        Assert.Equal("SortOrder", changed);
    }

    [Fact]
    public void PropertyChanged_Fires_OnGridName()
    {
        var item = new LookupItem();
        string? changed = null;
        item.PropertyChanged += (_, e) => changed = e.PropertyName;
        item.GridName = "IPAM";
        Assert.Equal("GridName", changed);
    }

    [Fact]
    public void PropertyChanged_Fires_OnModule()
    {
        var item = new LookupItem();
        string? changed = null;
        item.PropertyChanged += (_, e) => changed = e.PropertyName;
        item.Module = "Devices";
        Assert.Equal("Module", changed);
    }

    // ── All properties fire correctly ──

    [Fact]
    public void AllProperties_FirePropertyChanged()
    {
        var item = new LookupItem();
        var changed = new List<string>();
        item.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        item.Id = 1;
        item.Category = "Type";
        item.Value = "Server";
        item.SortOrder = 2;
        item.GridName = "Assets";
        item.Module = "Admin";

        Assert.Equal(6, changed.Count);
        Assert.Contains("Id", changed);
        Assert.Contains("Category", changed);
        Assert.Contains("Value", changed);
        Assert.Contains("SortOrder", changed);
        Assert.Contains("GridName", changed);
        Assert.Contains("Module", changed);
    }
}
