using Central.Engine.Models;

namespace Central.Tests.Models;

public class RibbonTreeItemTests
{
    [Theory]
    [InlineData("page", "📑")]
    [InlineData("group", "📁")]
    [InlineData("item", "🔘")]
    [InlineData("separator", "───")]
    [InlineData("unknown", "•")]
    [InlineData("", "•")]
    public void NodeIcon_ReturnsCorrectIcon(string nodeType, string expected)
    {
        var item = new RibbonTreeItem { NodeType = nodeType };
        Assert.Equal(expected, item.NodeIcon);
    }

    [Fact]
    public void DisplayText_CustomTextOverridesText()
    {
        var item = new RibbonTreeItem { Text = "Default", CustomText = "Custom" };
        Assert.Equal("Custom", item.DisplayText);
    }

    [Fact]
    public void DisplayText_NullCustomText_UsesText()
    {
        var item = new RibbonTreeItem { Text = "Default", CustomText = null };
        Assert.Equal("Default", item.DisplayText);
    }

    [Fact]
    public void DisplayText_EmptyCustomText_UsesText()
    {
        var item = new RibbonTreeItem { Text = "Default", CustomText = "" };
        Assert.Equal("Default", item.DisplayText);
    }

    [Fact]
    public void HiddenIcon_WhenHidden_ReturnsIcon()
    {
        var item = new RibbonTreeItem { IsHidden = true };
        Assert.NotEmpty(item.HiddenIcon);
    }

    [Fact]
    public void HiddenIcon_WhenVisible_ReturnsEmpty()
    {
        var item = new RibbonTreeItem { IsHidden = false };
        Assert.Equal("", item.HiddenIcon);
    }

    [Fact]
    public void PropertyChanged_NodeType_AlsoNotifiesNodeIcon()
    {
        var item = new RibbonTreeItem();
        var changed = new List<string>();
        item.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        item.NodeType = "page";
        Assert.Contains("NodeType", changed);
        Assert.Contains("NodeIcon", changed);
    }

    [Fact]
    public void PropertyChanged_CustomText_AlsoNotifiesDisplayText()
    {
        var item = new RibbonTreeItem();
        var changed = new List<string>();
        item.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        item.CustomText = "New";
        Assert.Contains("CustomText", changed);
        Assert.Contains("DisplayText", changed);
    }

    [Fact]
    public void PropertyChanged_IsHidden_AlsoNotifiesHiddenIcon()
    {
        var item = new RibbonTreeItem();
        var changed = new List<string>();
        item.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        item.IsHidden = true;
        Assert.Contains("IsHidden", changed);
        Assert.Contains("HiddenIcon", changed);
    }

    [Fact]
    public void PropertyChanged_IconName_AlsoNotifiesIconPreview()
    {
        var item = new RibbonTreeItem();
        var changed = new List<string>();
        item.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        item.IconName = "icon.svg";
        Assert.Contains("IconName", changed);
        Assert.Contains("IconPreview", changed);
    }

    [Fact]
    public void Defaults_AreCorrect()
    {
        var item = new RibbonTreeItem();
        Assert.Equal(0, item.Id);
        Assert.Equal(0, item.ParentId);
        Assert.Equal("", item.NodeType);
        Assert.Equal("", item.Text);
        Assert.Null(item.IconName);
        Assert.Null(item.CustomText);
        Assert.False(item.IsHidden);
        Assert.Equal("small", item.DisplayStyle);
        Assert.Null(item.LinkTarget);
        Assert.Equal("", item.ItemKey);
    }

    [Fact]
    public void DisplayStyle_CanBeSet()
    {
        var item = new RibbonTreeItem { DisplayStyle = "large" };
        Assert.Equal("large", item.DisplayStyle);
    }

    [Fact]
    public void LinkTarget_CanBeSet()
    {
        var item = new RibbonTreeItem { LinkTarget = "panel:IPAM" };
        Assert.Equal("panel:IPAM", item.LinkTarget);
    }
}
