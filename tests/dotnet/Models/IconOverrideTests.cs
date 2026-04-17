using Central.Core.Models;

namespace Central.Tests.Models;

public class IconOverrideTests
{
    [Fact]
    public void Defaults()
    {
        var o = new IconOverride();
        Assert.Equal(0, o.Id);
        Assert.Equal("", o.Context);
        Assert.Equal("", o.ElementKey);
        Assert.Null(o.IconName);
        Assert.Null(o.IconId);
        Assert.Null(o.Color);
    }

    [Fact]
    public void SetProperties()
    {
        var o = new IconOverride
        {
            Id = 5,
            Context = "ribbon",
            ElementKey = "Save",
            IconName = "floppy-disk",
            IconId = 42,
            Color = "#3B82F6"
        };
        Assert.Equal(5, o.Id);
        Assert.Equal("ribbon", o.Context);
        Assert.Equal("Save", o.ElementKey);
        Assert.Equal("floppy-disk", o.IconName);
        Assert.Equal(42, o.IconId);
        Assert.Equal("#3B82F6", o.Color);
    }
}
