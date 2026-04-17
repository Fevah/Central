using Central.Engine.Models;

namespace Central.Tests.Models;

public class BuilderSectionTests
{
    // ── ConfigLine record ──

    [Fact]
    public void ConfigLine_Properties()
    {
        var line = new ConfigLine("set system hostname \"CORE01\"", "system");
        Assert.Equal("set system hostname \"CORE01\"", line.Text);
        Assert.Equal("system", line.SectionKey);
    }

    [Fact]
    public void ConfigLine_Equality()
    {
        var a = new ConfigLine("set x", "y");
        var b = new ConfigLine("set x", "y");
        Assert.Equal(a, b);
    }

    [Fact]
    public void ConfigLine_Inequality()
    {
        var a = new ConfigLine("set x", "y");
        var b = new ConfigLine("set z", "y");
        Assert.NotEqual(a, b);
    }

    // ── BuilderSection ──

    [Fact]
    public void BuilderSection_Defaults()
    {
        var sec = new BuilderSection();
        Assert.Equal("", sec.Key);
        Assert.Equal("", sec.DisplayName);
        Assert.True(sec.IsEnabled);
        Assert.Equal("#888888", sec.ColorHex);
        Assert.Equal(0, sec.LineCount);
        Assert.Empty(sec.Items);
    }

    [Fact]
    public void BuilderSection_PropertyChanged_IsEnabled()
    {
        var sec = new BuilderSection();
        string? changed = null;
        sec.PropertyChanged += (_, e) => changed = e.PropertyName;

        sec.IsEnabled = false;
        Assert.Equal("IsEnabled", changed);
    }

    [Fact]
    public void BuilderSection_PropertyChanged_LineCount()
    {
        var sec = new BuilderSection();
        bool fired = false;
        sec.PropertyChanged += (_, _) => fired = true;

        sec.LineCount = 42;
        Assert.True(fired);
        Assert.Equal(42, sec.LineCount);
    }

    [Fact]
    public void BuilderSection_Items_IsObservable()
    {
        var sec = new BuilderSection();
        sec.Items.Add(new BuilderItem { Key = "101", DisplayText = "VLAN 101" });
        Assert.Single(sec.Items);
    }

    // ── BuilderItem ──

    [Fact]
    public void BuilderItem_Defaults()
    {
        var item = new BuilderItem();
        Assert.Equal("", item.Key);
        Assert.Equal("", item.DisplayText);
        Assert.True(item.IsEnabled);
    }

    [Fact]
    public void BuilderItem_PropertyChanged_IsEnabled()
    {
        var item = new BuilderItem();
        string? changed = null;
        item.PropertyChanged += (_, e) => changed = e.PropertyName;

        item.IsEnabled = false;
        Assert.Equal("IsEnabled", changed);
    }

    [Fact]
    public void BuilderItem_PropertyChanged_DisplayText()
    {
        var item = new BuilderItem();
        bool fired = false;
        item.PropertyChanged += (_, _) => fired = true;

        item.DisplayText = "VLAN 101 -- IT";
        Assert.True(fired);
    }
}
