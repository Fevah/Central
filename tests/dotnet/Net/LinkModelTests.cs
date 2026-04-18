using Central.Engine.Net;
using Central.Engine.Net.Links;

namespace Central.Tests.Net;

public class LinkModelTests
{
    [Fact]
    public void LinkType_DefaultsAreSafe()
    {
        var t = new LinkType();
        // Always-2 endpoints matches the seeded catalogue. If we ever
        // support hub-and-spoke links this test should be revisited
        // alongside the schema CHECK >= 2.
        Assert.Equal(2, t.RequiredEndpoints);
        Assert.Equal("{device_a}-to-{device_b}", t.NamingTemplate);
        Assert.Equal(EntityStatus.Planned, t.Status);
    }

    [Fact]
    public void Link_DefaultsToEmptyConfigJson()
    {
        var l = new Link();
        Assert.NotNull(l.ConfigJson);
        Assert.Empty(l.ConfigJson);   // per-type extensions are opt-in
        Assert.Null(l.LegacyLinkKind);
        Assert.Null(l.LegacyLinkId);
    }

    [Fact]
    public void LinkEndpoint_DefaultsToASideNoResolution()
    {
        var e = new LinkEndpoint();
        Assert.Equal(0, e.EndpointOrder);    // 0 = A side
        // All FKs null until the ports-sync service fills them in.
        Assert.Null(e.DeviceId);
        Assert.Null(e.PortId);
        Assert.Null(e.IpAddressId);
        Assert.Null(e.VlanId);
        Assert.Null(e.InterfaceName);
    }

    [Fact]
    public void Link_ConfigJsonRoundtripsB2BExtensions()
    {
        var l = new Link();
        l.ConfigJson["tx"]    = "200km";
        l.ConfigJson["rx"]    = "200km";
        l.ConfigJson["media"] = "single-mode";
        l.ConfigJson["speed"] = "100G";

        Assert.Equal("200km",       (string?)l.ConfigJson["tx"]);
        Assert.Equal("single-mode", (string?)l.ConfigJson["media"]);
    }

    [Fact]
    public void Link_LegacyPointersOptional()
    {
        // Links created natively after cutover carry no legacy link.
        var l = new Link { LinkCode = "NEW-LINK-001" };
        Assert.Null(l.LegacyLinkKind);
        Assert.Null(l.LegacyLinkId);
    }
}
