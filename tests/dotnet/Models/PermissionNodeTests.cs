using Central.Engine.Models;

namespace Central.Tests.Models;

public class PermissionNodeTests
{
    [Fact]
    public void Defaults()
    {
        var n = new PermissionNode();
        Assert.Equal("", n.Key);
        Assert.Equal("", n.ParentKey);
        Assert.Equal("", n.DisplayName);
        Assert.False(n.IsEnabled);
        Assert.Equal("", n.Module);
        Assert.Equal("", n.Permission);
    }

    [Fact]
    public void PropertyChanged_AllFields()
    {
        var n = new PermissionNode();
        var changed = new List<string>();
        n.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        n.Key = "devices:view";
        n.ParentKey = "devices";
        n.DisplayName = "View Devices";
        n.IsEnabled = true;
        n.Module = "devices";
        n.Permission = "View";

        Assert.Contains("Key", changed);
        Assert.Contains("ParentKey", changed);
        Assert.Contains("DisplayName", changed);
        Assert.Contains("IsEnabled", changed);
        Assert.Contains("Module", changed);
        Assert.Contains("Permission", changed);
    }
}
