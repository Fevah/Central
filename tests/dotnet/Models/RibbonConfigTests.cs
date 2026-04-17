using Central.Engine.Models;

namespace Central.Tests.Models;

public class RibbonConfigTests
{
    // ── RibbonPageConfig ──

    [Fact]
    public void RibbonPageConfig_Defaults()
    {
        var page = new RibbonPageConfig();
        Assert.Equal("", page.Header);
        Assert.Equal(0, page.SortOrder);
        Assert.Null(page.RequiredPermission);
        Assert.Null(page.IconName);
        Assert.True(page.IsVisible);
        Assert.False(page.IsSystem);
    }

    [Fact]
    public void RibbonPageConfig_PropertyChanged_Header()
    {
        var page = new RibbonPageConfig();
        string? changedProp = null;
        page.PropertyChanged += (_, e) => changedProp = e.PropertyName;

        page.Header = "Devices";
        Assert.Equal("Header", changedProp);
        Assert.Equal("Devices", page.Header);
    }

    [Fact]
    public void RibbonPageConfig_PropertyChanged_IsVisible()
    {
        var page = new RibbonPageConfig();
        string? changedProp = null;
        page.PropertyChanged += (_, e) => changedProp = e.PropertyName;

        page.IsVisible = false;
        Assert.Equal("IsVisible", changedProp);
        Assert.False(page.IsVisible);
    }

    [Fact]
    public void RibbonPageConfig_PropertyChanged_SortOrder()
    {
        var page = new RibbonPageConfig();
        bool fired = false;
        page.PropertyChanged += (_, _) => fired = true;

        page.SortOrder = 5;
        Assert.True(fired);
        Assert.Equal(5, page.SortOrder);
    }

    // ── RibbonGroupConfig ──

    [Fact]
    public void RibbonGroupConfig_Defaults()
    {
        var group = new RibbonGroupConfig();
        Assert.Equal(0, group.Id);
        Assert.Equal(0, group.PageId);
        Assert.Equal("", group.Header);
        Assert.Equal(0, group.SortOrder);
        Assert.True(group.IsVisible);
        Assert.Null(group.PageHeader);
    }

    [Fact]
    public void RibbonGroupConfig_PropertyChanged_Header()
    {
        var group = new RibbonGroupConfig();
        string? changedProp = null;
        group.PropertyChanged += (_, e) => changedProp = e.PropertyName;

        group.Header = "Actions";
        Assert.Equal("Header", changedProp);
    }

    [Fact]
    public void RibbonGroupConfig_PropertyChanged_PageId()
    {
        var group = new RibbonGroupConfig();
        bool fired = false;
        group.PropertyChanged += (_, _) => fired = true;

        group.PageId = 42;
        Assert.True(fired);
    }

    // ── RibbonItemConfig ──

    [Fact]
    public void RibbonItemConfig_Defaults()
    {
        var item = new RibbonItemConfig();
        Assert.Equal("", item.Content);
        Assert.Equal("button", item.ItemType);
        Assert.Equal(0, item.SortOrder);
        Assert.Null(item.Permission);
        Assert.Null(item.Glyph);
        Assert.Null(item.LargeGlyph);
        Assert.Null(item.IconId);
        Assert.Null(item.CommandType);
        Assert.Null(item.CommandParam);
        Assert.Null(item.Tooltip);
        Assert.True(item.IsVisible);
        Assert.False(item.IsSystem);
    }

    [Fact]
    public void RibbonItemConfig_PropertyChanged_Content()
    {
        var item = new RibbonItemConfig();
        string? changedProp = null;
        item.PropertyChanged += (_, e) => changedProp = e.PropertyName;

        item.Content = "New Device";
        Assert.Equal("Content", changedProp);
    }

    [Fact]
    public void RibbonItemConfig_PropertyChanged_AllSetters()
    {
        var item = new RibbonItemConfig();
        var changedProps = new List<string>();
        item.PropertyChanged += (_, e) => changedProps.Add(e.PropertyName!);

        item.Id = 1;
        item.GroupId = 2;
        item.Content = "Test";
        item.ItemType = "toggle";
        item.SortOrder = 3;
        item.Permission = "admin:edit";
        item.Glyph = "icon.svg";
        item.LargeGlyph = "icon32.svg";
        item.IconId = 99;
        item.CommandType = "action";
        item.CommandParam = "doStuff";
        item.Tooltip = "Click me";
        item.IsVisible = false;
        item.IsSystem = true;

        Assert.Contains("Id", changedProps);
        Assert.Contains("GroupId", changedProps);
        Assert.Contains("Content", changedProps);
        Assert.Contains("ItemType", changedProps);
        Assert.Contains("SortOrder", changedProps);
        Assert.Contains("Permission", changedProps);
        Assert.Contains("Glyph", changedProps);
        Assert.Contains("LargeGlyph", changedProps);
        Assert.Contains("IconId", changedProps);
        Assert.Contains("CommandType", changedProps);
        Assert.Contains("CommandParam", changedProps);
        Assert.Contains("Tooltip", changedProps);
        Assert.Contains("IsVisible", changedProps);
        Assert.Contains("IsSystem", changedProps);
    }

    // ── UserRibbonOverride ──

    [Fact]
    public void UserRibbonOverride_Defaults()
    {
        var ov = new UserRibbonOverride();
        Assert.Equal(0, ov.Id);
        Assert.Equal(0, ov.UserId);
        Assert.Equal("", ov.ItemKey);
        Assert.Null(ov.CustomIcon);
        Assert.Null(ov.CustomText);
        Assert.False(ov.IsHidden);
        Assert.Null(ov.SortOrder);
    }

    [Fact]
    public void UserRibbonOverride_SetProperties()
    {
        var ov = new UserRibbonOverride
        {
            Id = 5,
            UserId = 10,
            ItemKey = "Home/Actions/New",
            CustomIcon = "custom-icon.svg",
            CustomText = "Create New",
            IsHidden = true,
            SortOrder = 3
        };

        Assert.Equal(5, ov.Id);
        Assert.Equal(10, ov.UserId);
        Assert.Equal("Home/Actions/New", ov.ItemKey);
        Assert.Equal("custom-icon.svg", ov.CustomIcon);
        Assert.Equal("Create New", ov.CustomText);
        Assert.True(ov.IsHidden);
        Assert.Equal(3, ov.SortOrder);
    }

    // ── RibbonTreeItem ──

    [Fact]
    public void RibbonTreeItem_DisplayText_DefaultsToText()
    {
        var item = new RibbonTreeItem { Text = "Devices" };
        Assert.Equal("Devices", item.DisplayText);
    }

    [Fact]
    public void RibbonTreeItem_DisplayText_PrefersCustomText()
    {
        var item = new RibbonTreeItem { Text = "Devices", CustomText = "My Devices" };
        Assert.Equal("My Devices", item.DisplayText);
    }

    [Fact]
    public void RibbonTreeItem_DisplayText_EmptyCustomText_FallsBackToText()
    {
        var item = new RibbonTreeItem { Text = "Devices", CustomText = "" };
        Assert.Equal("Devices", item.DisplayText);
    }

    [Theory]
    [InlineData("page", "\U0001f4d1")]
    [InlineData("group", "\U0001f4c1")]
    [InlineData("item", "\U0001f518")]
    [InlineData("separator", "\u2500\u2500\u2500")]
    [InlineData("unknown", "\u2022")]
    public void RibbonTreeItem_NodeIcon_ByType(string nodeType, string expectedIcon)
    {
        var item = new RibbonTreeItem { NodeType = nodeType };
        Assert.Equal(expectedIcon, item.NodeIcon);
    }

    [Fact]
    public void RibbonTreeItem_HiddenIcon_WhenHidden()
    {
        var item = new RibbonTreeItem { IsHidden = true };
        Assert.NotEqual("", item.HiddenIcon);
    }

    [Fact]
    public void RibbonTreeItem_HiddenIcon_WhenVisible()
    {
        var item = new RibbonTreeItem { IsHidden = false };
        Assert.Equal("", item.HiddenIcon);
    }

    [Fact]
    public void RibbonTreeItem_PropertyChanged_NodeType_AlsoNotifiesNodeIcon()
    {
        var item = new RibbonTreeItem();
        var changedProps = new List<string>();
        item.PropertyChanged += (_, e) => changedProps.Add(e.PropertyName!);

        item.NodeType = "page";

        Assert.Contains("NodeType", changedProps);
        Assert.Contains("NodeIcon", changedProps);
    }

    [Fact]
    public void RibbonTreeItem_PropertyChanged_CustomText_AlsoNotifiesDisplayText()
    {
        var item = new RibbonTreeItem();
        var changedProps = new List<string>();
        item.PropertyChanged += (_, e) => changedProps.Add(e.PropertyName!);

        item.CustomText = "Override";

        Assert.Contains("CustomText", changedProps);
        Assert.Contains("DisplayText", changedProps);
    }

    [Fact]
    public void RibbonTreeItem_PropertyChanged_IsHidden_AlsoNotifiesHiddenIcon()
    {
        var item = new RibbonTreeItem();
        var changedProps = new List<string>();
        item.PropertyChanged += (_, e) => changedProps.Add(e.PropertyName!);

        item.IsHidden = true;

        Assert.Contains("IsHidden", changedProps);
        Assert.Contains("HiddenIcon", changedProps);
    }

    [Fact]
    public void RibbonTreeItem_PropertyChanged_IconName_AlsoNotifiesIconPreview()
    {
        var item = new RibbonTreeItem();
        var changedProps = new List<string>();
        item.PropertyChanged += (_, e) => changedProps.Add(e.PropertyName!);

        item.IconName = "some-icon";

        Assert.Contains("IconName", changedProps);
        Assert.Contains("IconPreview", changedProps);
    }

    [Fact]
    public void RibbonTreeItem_DisplayStyle_DefaultsToSmall()
    {
        var item = new RibbonTreeItem();
        Assert.Equal("small", item.DisplayStyle);
    }

    [Fact]
    public void RibbonTreeItem_LinkTarget_DefaultsToNull()
    {
        var item = new RibbonTreeItem();
        Assert.Null(item.LinkTarget);
    }
}
