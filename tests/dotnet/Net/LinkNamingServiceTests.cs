using Central.Engine.Net.Links;

namespace Central.Tests.Net;

/// <summary>
/// Pure tests for <see cref="LinkNamingService"/>. No DB — the
/// service takes a <see cref="LinkNamingContext"/> record and a
/// template string, returns the expansion.
/// </summary>
public class LinkNamingServiceTests
{
    [Fact]
    public void ExpandsP2PTemplate()
    {
        // The seeded P2P template, against a fully-populated context.
        var result = LinkNamingService.Expand(
            "{device_a}_{port_a}_p2p_{device_b}_{port_b}",
            new LinkNamingContext
            {
                DeviceA = "MEP-91-CORE02", PortA = "xe-1/1/1",
                DeviceB = "MEP-91-L1-CORE02", PortB = "xe-1/1/2",
            });
        Assert.Equal("MEP-91-CORE02_xe-1/1/1_p2p_MEP-91-L1-CORE02_xe-1/1/2", result);
    }

    [Fact]
    public void ExpandsB2BTemplateWithSites()
    {
        var result = LinkNamingService.Expand(
            "{site_a}_{device_a}_b2b_{site_b}_{device_b}",
            new LinkNamingContext
            {
                SiteA = "MEP-91", DeviceA = "MEP-91-CORE02",
                SiteB = "MEP-92", DeviceB = "MEP-92-CORE01",
            });
        Assert.Equal("MEP-91_MEP-91-CORE02_b2b_MEP-92_MEP-92-CORE01", result);
    }

    [Fact]
    public void MissingTokensCollapseToEmpty()
    {
        // WAN template uses {description}; if omitted we get the
        // surrounding punctuation but no expansion.
        var result = LinkNamingService.Expand(
            "{device_a}_wan_{description}",
            new LinkNamingContext { DeviceA = "MEP-91-CORE02" });
        Assert.Equal("MEP-91-CORE02_wan_", result);
    }

    [Fact]
    public void UnknownTokensPassThroughVerbatim()
    {
        // Typos ({devic_a}) stay visible instead of silently
        // disappearing — surfaces the mistake at first output.
        var result = LinkNamingService.Expand(
            "{devic_a}-{device_b}",
            new LinkNamingContext { DeviceA = "A", DeviceB = "B" });
        Assert.Equal("{devic_a}-B", result);
    }

    [Fact]
    public void UnmatchedBraceEmitsTailVerbatim()
    {
        var result = LinkNamingService.Expand(
            "{device_a}-rest{of_the_thing",
            new LinkNamingContext { DeviceA = "A" });
        Assert.Equal("A-rest{of_the_thing", result);
    }

    [Fact]
    public void VlanIdStringifies()
    {
        var result = LinkNamingService.Expand(
            "p2p-v{vlan}",
            new LinkNamingContext { VlanId = 101 });
        Assert.Equal("p2p-v101", result);
    }

    [Fact]
    public void EmptyTemplateReturnsEmpty()
    {
        Assert.Equal("", LinkNamingService.Expand("", new LinkNamingContext()));
    }

    [Fact]
    public void NoBracesReturnsInputUnchanged()
    {
        Assert.Equal("just a literal name",
            LinkNamingService.Expand("just a literal name", new LinkNamingContext()));
    }

    [Fact]
    public void ConsecutiveTokensDoNotCollide()
    {
        var result = LinkNamingService.Expand(
            "{device_a}{device_b}",
            new LinkNamingContext { DeviceA = "A", DeviceB = "B" });
        Assert.Equal("AB", result);
    }
}
