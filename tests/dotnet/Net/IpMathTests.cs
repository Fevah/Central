using Central.Persistence.Net;

namespace Central.Tests.Net;

/// <summary>
/// Pure-math tests for <see cref="IpMath"/> — no DB. These cover the
/// edge cases that bite IP allocation in production: /30 reserving
/// network+broadcast, /31 using both addresses (RFC 3021), /32 as a
/// single host, alignment for subnet carving, interval overlap logic.
/// </summary>
public class IpMathTests
{
    [Fact]
    public void ParseV4_Slash8()
    {
        var (net, bc, prefix) = IpMath.ParseV4("10.0.0.0/8");
        Assert.Equal(0x0A000000L, net);
        Assert.Equal(0x0AFFFFFFL, bc);
        Assert.Equal(8, prefix);
    }

    [Fact]
    public void ParseV4_Slash24()
    {
        var (net, bc, prefix) = IpMath.ParseV4("10.11.101.0/24");
        Assert.Equal(0x0A0B6500L, net);
        Assert.Equal(0x0A0B65FFL, bc);
        Assert.Equal(24, prefix);
    }

    [Fact]
    public void ParseV4_NormalisesNonAlignedInput()
    {
        // 10.11.101.42/24 → network is 10.11.101.0
        var (net, _, _) = IpMath.ParseV4("10.11.101.42/24");
        Assert.Equal("10.11.101.0", IpMath.ToIp(net));
    }

    [Fact]
    public void ParseV4_RejectsBadInput()
    {
        Assert.Throws<FormatException>(() => IpMath.ParseV4("not-a-cidr"));
        Assert.Throws<FormatException>(() => IpMath.ParseV4("10.0.0.0/33"));
        Assert.Throws<FormatException>(() => IpMath.ParseV4("10.0.0.0/"));
        Assert.Throws<FormatException>(() => IpMath.ParseV4("/24"));
    }

    [Fact]
    public void ToIp_RoundTrips()
    {
        Assert.Equal("0.0.0.0", IpMath.ToIp(0));
        Assert.Equal("255.255.255.255", IpMath.ToIp(0xFFFFFFFFL));
        Assert.Equal("192.168.1.1", IpMath.ToIp(0xC0A80101L));
    }

    [Fact]
    public void ToCidr_Round()
    {
        Assert.Equal("10.11.101.0/24", IpMath.ToCidr(0x0A0B6500L, 24));
    }

    [Fact]
    public void HostRange_Slash24_ReservesNetworkAndBroadcast()
    {
        var (first, last) = IpMath.HostRange(0x0A000000L, 0x0A0000FFL, 24);
        Assert.Equal(0x0A000001L, first);   // .1
        Assert.Equal(0x0A0000FEL, last);    // .254
    }

    [Fact]
    public void HostRange_Slash30_ReservesNetworkAndBroadcast()
    {
        // 10.0.0.0/30 -> .0 network, .1 usable, .2 usable, .3 broadcast
        var (first, last) = IpMath.HostRange(0x0A000000L, 0x0A000003L, 30);
        Assert.Equal(0x0A000001L, first);
        Assert.Equal(0x0A000002L, last);
    }

    [Fact]
    public void HostRange_Slash31_BothAddressesUsable()
    {
        // RFC 3021 — no broadcast on /31 point-to-point.
        var (first, last) = IpMath.HostRange(0x0A000000L, 0x0A000001L, 31);
        Assert.Equal(0x0A000000L, first);
        Assert.Equal(0x0A000001L, last);
    }

    [Fact]
    public void HostRange_Slash32_SingleHost()
    {
        var (first, last) = IpMath.HostRange(0x0A000001L, 0x0A000001L, 32);
        Assert.Equal(0x0A000001L, first);
        Assert.Equal(0x0A000001L, last);
    }

    [Fact]
    public void BlockSize_ForCommonPrefixes()
    {
        Assert.Equal(256L, IpMath.BlockSize(24));
        Assert.Equal(4L, IpMath.BlockSize(30));
        Assert.Equal(2L, IpMath.BlockSize(31));
        Assert.Equal(1L, IpMath.BlockSize(32));
        Assert.Equal(65536L, IpMath.BlockSize(16));
    }

    [Fact]
    public void AlignUp_LeavesAlignedInputsAlone()
    {
        Assert.Equal(0L, IpMath.AlignUp(0, 4));
        Assert.Equal(4L, IpMath.AlignUp(4, 4));
        Assert.Equal(256L, IpMath.AlignUp(256, 256));
    }

    [Fact]
    public void AlignUp_BumpsNonAlignedInputs()
    {
        Assert.Equal(4L, IpMath.AlignUp(1, 4));
        Assert.Equal(4L, IpMath.AlignUp(3, 4));
        Assert.Equal(8L, IpMath.AlignUp(5, 4));
        Assert.Equal(256L, IpMath.AlignUp(200, 256));
    }

    [Fact]
    public void Overlaps_DetectsContainment()
    {
        // [0, 9] contains [3, 5]
        Assert.True(IpMath.Overlaps(0, 9, 3, 5));
        Assert.True(IpMath.Overlaps(3, 5, 0, 9));
    }

    [Fact]
    public void Overlaps_DetectsTouchingEndpoints()
    {
        // Endpoints coinciding count as overlap — we're picking
        // unique CIDRs, not scheduling meetings.
        Assert.True(IpMath.Overlaps(0, 5, 5, 9));
        Assert.True(IpMath.Overlaps(5, 9, 0, 5));
    }

    [Fact]
    public void Overlaps_SeparateRanges()
    {
        Assert.False(IpMath.Overlaps(0, 3, 4, 7));
        Assert.False(IpMath.Overlaps(4, 7, 0, 3));
    }

    [Fact]
    public void FindFreeAligned_EmptyPool_ReturnsPoolStart()
    {
        // Pool 10.0.0.0/24, target /30, no existing
        var result = IpAllocationService.FindFreeAligned(
            0x0A000000L, 0x0A0000FFL, 30,
            new List<(long, long)>());
        Assert.Equal(0x0A000000L, result);
    }

    [Fact]
    public void FindFreeAligned_SkipsPastExisting()
    {
        // Pool 10.0.0.0/24, /30 stride = 4. Existing 10.0.0.0/30
        // (0..3) — next candidate is 10.0.0.4.
        var result = IpAllocationService.FindFreeAligned(
            0x0A000000L, 0x0A0000FFL, 30,
            new List<(long, long)> { (0x0A000000L, 0x0A000003L) });
        Assert.Equal(0x0A000004L, result);
    }

    [Fact]
    public void FindFreeAligned_FindsGapBetweenExisting()
    {
        // Pool /24, /30 stride = 4. Existing .0-.3 and .8-.11.
        // Free candidate at .4.
        var result = IpAllocationService.FindFreeAligned(
            0x0A000000L, 0x0A0000FFL, 30,
            new List<(long, long)>
            {
                (0x0A000000L, 0x0A000003L),
                (0x0A000008L, 0x0A00000BL),
            });
        Assert.Equal(0x0A000004L, result);
    }

    [Fact]
    public void FindFreeAligned_ReturnsNullWhenExhausted()
    {
        // Pool /30 (one /30), existing covers entirety.
        var result = IpAllocationService.FindFreeAligned(
            0x0A000000L, 0x0A000003L, 30,
            new List<(long, long)> { (0x0A000000L, 0x0A000003L) });
        Assert.Null(result);
    }

    [Fact]
    public void FindFreeAligned_StrideAlignsCandidatePastOddBlocker()
    {
        // Pool /24, /28 stride = 16. An existing /29 at .4..11 is
        // off-alignment for /28 — the next /28 candidate is .16
        // because .0..15 overlaps with the blocker.
        var result = IpAllocationService.FindFreeAligned(
            0x0A000000L, 0x0A0000FFL, 28,
            new List<(long, long)> { (0x0A000004L, 0x0A00000BL) });
        Assert.Equal(0x0A000010L, result);   // .16
    }
}
