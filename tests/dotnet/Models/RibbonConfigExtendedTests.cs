using Central.Core.Models;

namespace Central.Tests.Models;

public class RibbonConfigExtendedTests
{
    // ── RibbonPageConfig ──

    [Fact]
    public void RibbonPageConfig_Defaults()
    {
        var p = new RibbonPageConfig();
        Assert.Equal(0, p.Id);
        Assert.Equal("", p.Header);
        Assert.Equal(0, p.SortOrder);
        Assert.Null(p.RequiredPermission);
        Assert.Null(p.IconName);
        Assert.True(p.IsVisible);
        Assert.False(p.IsSystem);
    }

    [Fact]
    public void RibbonPageConfig_PropertyChanged_AllProperties()
    {
        var p = new RibbonPageConfig();
        var changed = new List<string>();
        p.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        p.Id = 1;
        p.Header = "Home";
        p.SortOrder = 0;
        p.RequiredPermission = "devices:read";
        p.IconName = "home";
        p.IsVisible = false;
        p.IsSystem = true;

        Assert.Equal(7, changed.Count);
        Assert.Contains("Id", changed);
        Assert.Contains("Header", changed);
        Assert.Contains("SortOrder", changed);
        Assert.Contains("RequiredPermission", changed);
        Assert.Contains("IconName", changed);
        Assert.Contains("IsVisible", changed);
        Assert.Contains("IsSystem", changed);
    }

    // ── RibbonGroupConfig ──

    [Fact]
    public void RibbonGroupConfig_Defaults()
    {
        var g = new RibbonGroupConfig();
        Assert.Equal(0, g.Id);
        Assert.Equal(0, g.PageId);
        Assert.Equal("", g.Header);
        Assert.Equal(0, g.SortOrder);
        Assert.True(g.IsVisible);
        Assert.Null(g.PageHeader);
    }

    [Fact]
    public void RibbonGroupConfig_PropertyChanged_AllProperties()
    {
        var g = new RibbonGroupConfig();
        var changed = new List<string>();
        g.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        g.Id = 1;
        g.PageId = 5;
        g.Header = "Actions";
        g.SortOrder = 0;
        g.IsVisible = false;

        Assert.Equal(5, changed.Count);
        Assert.Contains("Id", changed);
        Assert.Contains("PageId", changed);
        Assert.Contains("Header", changed);
        Assert.Contains("SortOrder", changed);
        Assert.Contains("IsVisible", changed);
    }

    // ── RibbonItemConfig ──

    [Fact]
    public void RibbonItemConfig_Defaults()
    {
        var i = new RibbonItemConfig();
        Assert.Equal(0, i.Id);
        Assert.Equal(0, i.GroupId);
        Assert.Equal("", i.Content);
        Assert.Equal("button", i.ItemType);
        Assert.Equal(0, i.SortOrder);
        Assert.Null(i.Permission);
        Assert.Null(i.Glyph);
        Assert.Null(i.LargeGlyph);
        Assert.Null(i.IconId);
        Assert.Null(i.CommandType);
        Assert.Null(i.CommandParam);
        Assert.Null(i.Tooltip);
        Assert.True(i.IsVisible);
        Assert.False(i.IsSystem);
        Assert.Null(i.GroupHeader);
        Assert.Null(i.PageHeader);
    }

    [Fact]
    public void RibbonItemConfig_PropertyChanged_AllProperties()
    {
        var i = new RibbonItemConfig();
        var changed = new List<string>();
        i.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        i.Id = 1;
        i.GroupId = 5;
        i.Content = "New Device";
        i.ItemType = "splitButton";
        i.SortOrder = 1;
        i.Permission = "devices:write";
        i.Glyph = "icon.svg";
        i.LargeGlyph = "icon_large.svg";
        i.IconId = 42;
        i.CommandType = "panel";
        i.CommandParam = "IPAM";
        i.Tooltip = "Create a new device";
        i.IsVisible = false;
        i.IsSystem = true;

        Assert.Equal(14, changed.Count);
        Assert.Contains("Id", changed);
        Assert.Contains("GroupId", changed);
        Assert.Contains("Content", changed);
        Assert.Contains("ItemType", changed);
        Assert.Contains("Permission", changed);
        Assert.Contains("Glyph", changed);
        Assert.Contains("LargeGlyph", changed);
        Assert.Contains("IconId", changed);
        Assert.Contains("CommandType", changed);
        Assert.Contains("CommandParam", changed);
        Assert.Contains("Tooltip", changed);
        Assert.Contains("IsVisible", changed);
        Assert.Contains("IsSystem", changed);
    }

    // ── UserRibbonOverride ──

    [Fact]
    public void UserRibbonOverride_Defaults()
    {
        var o = new UserRibbonOverride();
        Assert.Equal(0, o.Id);
        Assert.Equal(0, o.UserId);
        Assert.Equal("", o.ItemKey);
        Assert.Null(o.CustomIcon);
        Assert.Null(o.CustomText);
        Assert.False(o.IsHidden);
        Assert.Null(o.SortOrder);
    }

    [Fact]
    public void UserRibbonOverride_SetProperties()
    {
        var o = new UserRibbonOverride
        {
            Id = 1,
            UserId = 42,
            ItemKey = "Home/Actions/NewDevice",
            CustomIcon = "custom_icon.svg",
            CustomText = "Create Device",
            IsHidden = false,
            SortOrder = 5
        };
        Assert.Equal(1, o.Id);
        Assert.Equal(42, o.UserId);
        Assert.Equal("Home/Actions/NewDevice", o.ItemKey);
        Assert.Equal("custom_icon.svg", o.CustomIcon);
        Assert.Equal("Create Device", o.CustomText);
        Assert.False(o.IsHidden);
        Assert.Equal(5, o.SortOrder);
    }

    [Fact]
    public void UserRibbonOverride_IsHidden_True()
    {
        var o = new UserRibbonOverride { IsHidden = true };
        Assert.True(o.IsHidden);
    }
}
