using Central.Persistence.Net;

namespace Central.Tests.Net;

/// <summary>
/// Pure-math tests for <see cref="IpMath6"/>. IPv6 has no broadcast
/// reservation, no RFC 3021 corner case, and 128-bit arithmetic —
/// all covered here. Integration tests exercise the allocator's
/// end-to-end v6 path separately.
/// </summary>
public class IpMath6Tests
{
    [Fact]
    public void ParseV6_Slash32()
    {
        var (net, last, prefix) = IpMath6.ParseV6("2001:db8::/32");
        Assert.Equal(32, prefix);
        Assert.Equal("2001:db8::", IpMath6.ToIp(net));
        Assert.Equal("2001:db8:ffff:ffff:ffff:ffff:ffff:ffff", IpMath6.ToIp(last));
    }

    [Fact]
    public void ParseV6_Slash64_CommonCase()
    {
        var (net, last, prefix) = IpMath6.ParseV6("2001:db8:1:2::/64");
        Assert.Equal(64, prefix);
        Assert.Equal("2001:db8:1:2::", IpMath6.ToIp(net));
        // Last address in a /64 has all low-order bits set.
        Assert.Equal("2001:db8:1:2:ffff:ffff:ffff:ffff", IpMath6.ToIp(last));
    }

    [Fact]
    public void ParseV6_NormalisesNonAlignedInput()
    {
        // Host bits on the input are masked off by the parse.
        var (net, _, _) = IpMath6.ParseV6("2001:db8::dead:beef/64");
        Assert.Equal("2001:db8::", IpMath6.ToIp(net));
    }

    [Fact]
    public void ParseV6_RejectsBadInput()
    {
        Assert.Throws<FormatException>(() => IpMath6.ParseV6("not-a-cidr"));
        Assert.Throws<FormatException>(() => IpMath6.ParseV6("2001:db8::/129"));
        Assert.Throws<FormatException>(() => IpMath6.ParseV6("10.0.0.0/24"));     // v4 rejected here
    }

    [Fact]
    public void ToIp_RoundTrips()
    {
        Assert.Equal("::", IpMath6.ToIp(UInt128.Zero));
        Assert.Equal("::1", IpMath6.ToIp(UInt128.One));
        Assert.Equal("ffff:ffff:ffff:ffff:ffff:ffff:ffff:ffff", IpMath6.ToIp(UInt128.MaxValue));
    }

    [Fact]
    public void HostRange_UsesEveryAddress()
    {
        // Unlike IPv4, IPv6 reserves nothing — network address is
        // valid, broadcast doesn't exist.
        var (net, last, _) = IpMath6.ParseV6("2001:db8::/126");
        var (first, lastUsable) = IpMath6.HostRange(net, last);
        Assert.Equal(net, first);
        Assert.Equal(last, lastUsable);
    }

    [Fact]
    public void BlockSize_ForCommonPrefixes()
    {
        Assert.Equal(UInt128.One << 64, IpMath6.BlockSize(64));   // /64 -> 2^64
        Assert.Equal(UInt128.One << 48, IpMath6.BlockSize(80));   // /80 -> 2^48
        Assert.Equal(UInt128.One, IpMath6.BlockSize(128));         // /128 -> 1
        Assert.Equal(UInt128.Zero, IpMath6.BlockSize(0));          // /0 sentinel
    }

    [Fact]
    public void AlignUp_LeavesAlignedInputsAlone()
    {
        Assert.Equal(UInt128.Zero, IpMath6.AlignUp(UInt128.Zero, IpMath6.BlockSize(64)));
        Assert.Equal(IpMath6.BlockSize(64), IpMath6.AlignUp(IpMath6.BlockSize(64), IpMath6.BlockSize(64)));
    }

    [Fact]
    public void AlignUp_BumpsNonAlignedInputs()
    {
        var stride = IpMath6.BlockSize(64);
        Assert.Equal(stride, IpMath6.AlignUp(UInt128.One, stride));
        Assert.Equal(stride * 2, IpMath6.AlignUp(stride + UInt128.One, stride));
    }

    [Fact]
    public void Overlaps_DetectsContainment()
    {
        UInt128 a = 0, b = 10, c = 3, d = 5;
        Assert.True(IpMath6.Overlaps(a, b, c, d));
    }

    [Fact]
    public void Overlaps_SeparateRanges()
    {
        UInt128 a = 0, b = 3, c = 4, d = 7;
        Assert.False(IpMath6.Overlaps(a, b, c, d));
    }

    [Fact]
    public void FindFreeAlignedV6_EmptyPool_ReturnsPoolStart()
    {
        var (net, last, _) = IpMath6.ParseV6("2001:db8::/48");
        var result = IpAllocationService.FindFreeAlignedV6(net, last, 64,
            new List<(UInt128, UInt128)>());
        Assert.Equal(net, result);
    }

    [Fact]
    public void FindFreeAlignedV6_SkipsPastExisting()
    {
        // Pool /48, target /64. Existing = first /64. Next candidate
        // is the second /64.
        var (poolNet, poolLast, _) = IpMath6.ParseV6("2001:db8::/48");
        var (firstNet, firstLast, _) = IpMath6.ParseV6("2001:db8::/64");
        var result = IpAllocationService.FindFreeAlignedV6(poolNet, poolLast, 64,
            new List<(UInt128, UInt128)> { (firstNet, firstLast) });
        Assert.Equal("2001:db8:0:1::", IpMath6.ToIp(result!.Value));
    }

    [Fact]
    public void FindFreeAlignedV6_ReturnsNullWhenExhausted()
    {
        // Pool = one /64 exactly; already occupied.
        var (net, last, _) = IpMath6.ParseV6("2001:db8::/64");
        var result = IpAllocationService.FindFreeAlignedV6(net, last, 64,
            new List<(UInt128, UInt128)> { (net, last) });
        Assert.Null(result);
    }
}
