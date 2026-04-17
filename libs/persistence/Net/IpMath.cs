using System.Net;

namespace Central.Persistence.Net;

/// <summary>
/// IPv4 CIDR arithmetic in terms of <c>long</c>. IPv4 addresses fit in
/// <c>uint</c> but <c>long</c> sidesteps sign-bit annoyances when we do
/// <c>last - first + 1</c> on a supernet near the top of the space.
///
/// IPv6 will go in a sibling helper when Phase 3g lands — the address
/// families share no code (128-bit math is a different shape) so
/// splitting keeps both simple.
/// </summary>
internal static class IpMath
{
    /// <summary>
    /// Parse a CIDR string like <c>"10.0.0.0/8"</c> into the
    /// <c>(network, broadcast, prefix)</c> triple.
    /// Throws <see cref="FormatException"/> on malformed input.
    /// </summary>
    public static (long network, long broadcast, int prefix) ParseV4(string cidr)
    {
        var slash = cidr.IndexOf('/');
        if (slash <= 0 || slash == cidr.Length - 1)
            throw new FormatException($"Invalid CIDR: '{cidr}'");

        var addr = IPAddress.Parse(cidr[..slash].Trim());
        if (addr.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
            throw new FormatException($"Not an IPv4 CIDR: '{cidr}'");
        if (!int.TryParse(cidr[(slash + 1)..].Trim(), out var prefix) || prefix is < 0 or > 32)
            throw new FormatException($"Invalid prefix length in '{cidr}'");

        var bytes = addr.GetAddressBytes();
        long addrLong = ((long)bytes[0] << 24) | ((long)bytes[1] << 16) | ((long)bytes[2] << 8) | bytes[3];
        long mask = prefix == 0 ? 0L : unchecked((long)(0xFFFFFFFFL << (32 - prefix)) & 0xFFFFFFFFL);
        long network = addrLong & mask;
        long broadcast = network | (~mask & 0xFFFFFFFFL);
        return (network, broadcast, prefix);
    }

    /// <summary>
    /// Convert a numeric IPv4 value back to dotted-quad.
    /// </summary>
    public static string ToIp(long value)
    {
        var v = (uint)(value & 0xFFFFFFFFL);
        return $"{(v >> 24) & 0xFF}.{(v >> 16) & 0xFF}.{(v >> 8) & 0xFF}.{v & 0xFF}";
    }

    /// <summary>
    /// CIDR string for a network of <paramref name="prefix"/> at
    /// <paramref name="network"/>.
    /// </summary>
    public static string ToCidr(long network, int prefix)
        => $"{ToIp(network)}/{prefix}";

    /// <summary>
    /// Host-usable range inside a CIDR. For /30 and larger we reserve
    /// the network address and the broadcast address (RFC 1812).
    /// /31 is a special point-to-point case (RFC 3021) — both
    /// addresses are usable. /32 is a single host. Returns
    /// <c>(first, last)</c>; for an empty range (impossible for valid
    /// CIDRs) returns a pair where first &gt; last.
    /// </summary>
    public static (long firstUsable, long lastUsable) HostRange(long network, long broadcast, int prefix)
    {
        if (prefix >= 31)
            return (network, broadcast);
        return (network + 1, broadcast - 1);
    }

    /// <summary>
    /// Block size in host count for a prefix length. /24 -&gt; 256,
    /// /30 -&gt; 4, /32 -&gt; 1.
    /// </summary>
    public static long BlockSize(int prefix)
        => 1L << (32 - prefix);

    /// <summary>
    /// Round <paramref name="address"/> up to the next
    /// <paramref name="stride"/>-aligned value. If already aligned,
    /// returns <paramref name="address"/> unchanged. Used by the
    /// subnet carver so a candidate /30 always starts at a .0 / .4 /
    /// .8 / … address.
    /// </summary>
    public static long AlignUp(long address, long stride)
    {
        var rem = address % stride;
        return rem == 0 ? address : address + (stride - rem);
    }

    /// <summary>
    /// True if <c>[a1, a2]</c> overlaps <c>[b1, b2]</c>. Inclusive on
    /// both ends. Ranges are treated as single-segment intervals;
    /// "touching" at endpoints counts as overlap.
    /// </summary>
    public static bool Overlaps(long a1, long a2, long b1, long b2)
        => a1 <= b2 && b1 <= a2;
}
