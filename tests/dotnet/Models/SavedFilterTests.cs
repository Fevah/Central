using Central.Core.Models;

namespace Central.Tests.Models;

public class SavedFilterTests
{
    [Fact]
    public void IsShared_True_WhenUserIdNull()
    {
        var f = new SavedFilter { UserId = null };
        Assert.True(f.IsShared);
    }

    [Fact]
    public void IsShared_False_WhenUserIdSet()
    {
        var f = new SavedFilter { UserId = 42 };
        Assert.False(f.IsShared);
    }

    [Fact]
    public void PropertyChanged_AllFields()
    {
        var f = new SavedFilter();
        var changed = new List<string>();
        f.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        f.Id = 1;
        f.UserId = 5;
        f.PanelName = "devices";
        f.FilterName = "Active Only";
        f.FilterExpr = "[Status] = 'Active'";
        f.IsDefault = true;
        f.SortOrder = 3;

        Assert.Contains("Id", changed);
        Assert.Contains("UserId", changed);
        Assert.Contains("PanelName", changed);
        Assert.Contains("FilterName", changed);
        Assert.Contains("FilterExpr", changed);
        Assert.Contains("IsDefault", changed);
        Assert.Contains("SortOrder", changed);
    }

    [Fact]
    public void Defaults()
    {
        var f = new SavedFilter();
        Assert.Equal(0, f.Id);
        Assert.Null(f.UserId);
        Assert.Equal("", f.PanelName);
        Assert.Equal("", f.FilterName);
        Assert.Equal("", f.FilterExpr);
        Assert.False(f.IsDefault);
        Assert.Equal(0, f.SortOrder);
    }
}
