using Central.Core.Widgets;

namespace Central.Tests.Widgets;

public class WidgetCommandTests
{
    // ── WidgetCommandAttribute ──

    [Fact]
    public void WidgetCommandAttribute_StoresNameGroupDescription()
    {
        var attr = new WidgetCommandAttribute("Add {Type}", "Edit", "Add a new {Type}");
        Assert.Equal("Add {Type}", attr.Name);
        Assert.Equal("Edit", attr.GroupName);
        Assert.Equal("Add a new {Type}", attr.Description);
    }

    [Fact]
    public void WidgetCommandAttribute_EmptyDescription()
    {
        var attr = new WidgetCommandAttribute("Save", "Data");
        Assert.Equal("Save", attr.Name);
        Assert.Equal("Data", attr.GroupName);
        Assert.Equal("", attr.Description);
    }

    [Fact]
    public void WidgetCommandAttribute_CommandParameter_Null_ByDefault()
    {
        var attr = new WidgetCommandAttribute("Test", "Group");
        Assert.Null(attr.CommandParameter);
    }

    [Fact]
    public void WidgetCommandAttribute_CommandParameter_CanBeSet()
    {
        var attr = new WidgetCommandAttribute("Delete", "Edit")
        {
            CommandParameter = "devices:delete"
        };
        Assert.Equal("devices:delete", attr.CommandParameter);
    }

    [Fact]
    public void WidgetCommandAttribute_IsAttribute()
    {
        Assert.True(typeof(WidgetCommandAttribute).IsSubclassOf(typeof(Attribute)));
    }

    [Fact]
    public void WidgetCommandAttribute_TargetsProperty()
    {
        var usage = typeof(WidgetCommandAttribute)
            .GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .OfType<AttributeUsageAttribute>()
            .FirstOrDefault();
        Assert.NotNull(usage);
        Assert.True(usage!.ValidOn.HasFlag(AttributeTargets.Property));
    }

    // ── WidgetCommandData — Apply text replacements ──

    [Fact]
    public void Apply_NoReplacements_ReturnsOriginal()
    {
        var data = new WidgetCommandData();
        Assert.Equal("Hello World", data.Apply("Hello World"));
    }

    [Fact]
    public void Apply_SingleReplacement()
    {
        var data = new WidgetCommandData();
        data.TextReplacements["Type"] = "Device";
        Assert.Equal("Add Device", data.Apply("Add {Type}"));
    }

    [Fact]
    public void Apply_MultipleReplacements()
    {
        var data = new WidgetCommandData();
        data.TextReplacements["Type"] = "P2P Link";
        data.TextReplacements["TypePlural"] = "P2P Links";
        Assert.Equal("Delete P2P Link", data.Apply("Delete {Type}"));
        Assert.Equal("Export P2P Links", data.Apply("Export {TypePlural}"));
    }

    [Fact]
    public void Apply_NoMatchingPlaceholder_ReturnsUnchanged()
    {
        var data = new WidgetCommandData();
        data.TextReplacements["Type"] = "Device";
        Assert.Equal("No placeholders here", data.Apply("No placeholders here"));
    }

    [Fact]
    public void Apply_EmptyTemplate_ReturnsEmpty()
    {
        var data = new WidgetCommandData();
        data.TextReplacements["Type"] = "Device";
        Assert.Equal("", data.Apply(""));
    }

    [Fact]
    public void Apply_MultipleSamePlaceholder()
    {
        var data = new WidgetCommandData();
        data.TextReplacements["Type"] = "Switch";
        Assert.Equal("Switch to Switch", data.Apply("{Type} to {Type}"));
    }

    [Fact]
    public void Apply_PartialPlaceholder_NotReplaced()
    {
        var data = new WidgetCommandData();
        data.TextReplacements["Type"] = "Device";
        Assert.Equal("{Typ", data.Apply("{Typ"));
    }

    [Fact]
    public void Apply_EmptyValue_RemovesPlaceholder()
    {
        var data = new WidgetCommandData();
        data.TextReplacements["Type"] = "";
        Assert.Equal("Add ", data.Apply("Add {Type}"));
    }

    [Fact]
    public void TextReplacements_DefaultEmpty()
    {
        var data = new WidgetCommandData();
        Assert.NotNull(data.TextReplacements);
        Assert.Empty(data.TextReplacements);
    }

    [Fact]
    public void Apply_SpecialCharacters()
    {
        var data = new WidgetCommandData();
        data.TextReplacements["Type"] = "P2P (Link)";
        Assert.Equal("Add P2P (Link)", data.Apply("Add {Type}"));
    }
}
