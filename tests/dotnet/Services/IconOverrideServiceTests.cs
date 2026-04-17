using Central.Core.Models;
using Central.Core.Services;

namespace Central.Tests.Services;

public class IconOverrideServiceTests
{
    [Fact]
    public void Resolve_NoData_ReturnsNull()
    {
        var svc = new IconOverrideService();
        svc.Load(new List<IconOverride>(), new List<IconOverride>());

        Assert.Null(svc.Resolve("status.device", "Active"));
    }

    [Fact]
    public void Resolve_AdminDefault_Returned()
    {
        var svc = new IconOverrideService();
        svc.Load(
            new List<IconOverride>
            {
                new() { Context = "status.device", ElementKey = "Active", Color = "#00FF00" }
            },
            new List<IconOverride>());

        var result = svc.Resolve("status.device", "Active");
        Assert.NotNull(result);
        Assert.Equal("#00FF00", result!.Color);
    }

    [Fact]
    public void Resolve_UserOverride_TakesPriority()
    {
        var svc = new IconOverrideService();
        svc.Load(
            new List<IconOverride>
            {
                new() { Context = "status.device", ElementKey = "Active", Color = "#00FF00" }
            },
            new List<IconOverride>
            {
                new() { Context = "status.device", ElementKey = "Active", Color = "#FF0000" }
            });

        var result = svc.Resolve("status.device", "Active");
        Assert.Equal("#FF0000", result!.Color);
    }

    [Fact]
    public void ResolveColor_ReturnsJustColor()
    {
        var svc = new IconOverrideService();
        svc.Load(
            new List<IconOverride>
            {
                new() { Context = "status.device", ElementKey = "Active", Color = "#123456" }
            },
            new List<IconOverride>());

        Assert.Equal("#123456", svc.ResolveColor("status.device", "Active"));
    }

    [Fact]
    public void ResolveColor_NoMatch_ReturnsNull()
    {
        var svc = new IconOverrideService();
        svc.Load(new List<IconOverride>(), new List<IconOverride>());

        Assert.Null(svc.ResolveColor("status.device", "Active"));
    }

    [Fact]
    public void ResolveColorOrDefault_NoMatch_ReturnsFallback()
    {
        var svc = new IconOverrideService();
        svc.Load(new List<IconOverride>(), new List<IconOverride>());

        Assert.Equal("#22C55E", svc.ResolveColorOrDefault("status.device", "Active", "#22C55E"));
    }

    [Fact]
    public void ResolveColorOrDefault_HasMatch_ReturnsOverride()
    {
        var svc = new IconOverrideService();
        svc.Load(
            new List<IconOverride>(),
            new List<IconOverride>
            {
                new() { Context = "status.device", ElementKey = "Active", Color = "#AABBCC" }
            });

        Assert.Equal("#AABBCC", svc.ResolveColorOrDefault("status.device", "Active", "#22C55E"));
    }

    [Fact]
    public void ResolveIconName_ReturnsIconName()
    {
        var svc = new IconOverrideService();
        svc.Load(
            new List<IconOverride>
            {
                new() { Context = "device_type", ElementKey = "Core Switch", IconName = "switch-icon" }
            },
            new List<IconOverride>());

        Assert.Equal("switch-icon", svc.ResolveIconName("device_type", "Core Switch"));
    }

    [Fact]
    public void ResolveIconName_NoMatch_ReturnsNull()
    {
        var svc = new IconOverrideService();
        svc.Load(new List<IconOverride>(), new List<IconOverride>());
        Assert.Null(svc.ResolveIconName("device_type", "Core Switch"));
    }

    [Fact]
    public void IsLoaded_FalseBeforeLoad()
    {
        var svc = new IconOverrideService();
        Assert.False(svc.IsLoaded);
    }

    [Fact]
    public void IsLoaded_TrueAfterLoad()
    {
        var svc = new IconOverrideService();
        svc.Load(new List<IconOverride>(), new List<IconOverride>());
        Assert.True(svc.IsLoaded);
    }

    [Fact]
    public void Load_CaseInsensitive()
    {
        var svc = new IconOverrideService();
        svc.Load(
            new List<IconOverride>
            {
                new() { Context = "STATUS.DEVICE", ElementKey = "ACTIVE", Color = "#FF0000" }
            },
            new List<IconOverride>());

        // Lookup with different case should still match
        Assert.Equal("#FF0000", svc.ResolveColor("status.device", "active"));
    }

    [Fact]
    public void Load_OverwritesPreviousData()
    {
        var svc = new IconOverrideService();
        svc.Load(
            new List<IconOverride>
            {
                new() { Context = "ctx", ElementKey = "key", Color = "#111111" }
            },
            new List<IconOverride>());

        Assert.Equal("#111111", svc.ResolveColor("ctx", "key"));

        // Reload with different data
        svc.Load(
            new List<IconOverride>
            {
                new() { Context = "ctx", ElementKey = "key", Color = "#222222" }
            },
            new List<IconOverride>());

        Assert.Equal("#222222", svc.ResolveColor("ctx", "key"));
    }
}
