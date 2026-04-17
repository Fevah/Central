using System.Net;

namespace Central.Persistence.Net;

/// <summary>
/// IPv6 CIDR arithmetic in terms of <see cref="UInt128"/>. Sibling to
/// <see cref="IpMath"/>, which handles IPv4 in <c>long</c>. The shapes
/// are similar enough that splitting looks redundant — but IPv6 drops
/// the broadcast concept, uses 128-bit keys, and has no RFC 3021
/// /31 corner case, so merging them into generic code would force the
/// v4 path to pay UInt128 cost for every address.
///
/// .NET's <see cref="UInt128"/> (System.Numerics, net7+) supports
/// bitwise ops, shifts, +, -, &lt;, etc. — everything the carver needs.
/// </summary>
internal static class IpMath6
{
    /// <summary>
    /// Parse a CIDR string like <c>"2001:db8::/32"</c> into the
    /// <c>(network, last, prefix)</c> triple. "last" is the last
    /// address in the block — IPv6 has no broadcast, but we keep the
    /// same tuple shape as <see cref="IpMath.ParseV4"/> for symmetry.
    /// </summary>
    public static (UInt128 network, UInt128 last, int prefix) ParseV6(string cidr)
    {
        var slash = cidr.IndexOf('/');
        if (slash <= 0 || slash == cidr.Length - 1)
            throw new FormatException($"Invalid CIDR: '{cidr}'");

        var addr = IPAddress.Parse(cidr[..slash].Trim());
        if (addr.AddressFamily != System.Net.Sockets.AddressFamily.InterNetworkV6)
            throw new FormatException($"Not an IPv6 CIDR: '{cidr}'");
        if (!int.TryParse(cidr[(slash + 1)..].Trim(), out var prefix) || prefix is < 0 or > 128)
            throw new FormatException($"Invalid prefix length in '{cidr}'");

        var value = ToUInt128(addr);
        var mask = prefix == 0 ? UInt128.Zero : (UInt128.MaxValue << (128 - prefix));
        var network = value & mask;
        var last = network | ~mask;
        return (network, last, prefix);
    }

    /// <summary>
    /// Convert a numeric IPv6 value back to a canonical
    /// colon-notation string via <see cref="IPAddress.ToString"/>.
    /// </summary>
    public static string ToIp(UInt128 value)
    {
        var bytes = new byte[16];
        var v = value;
        for (var i = 15; i >= 0; i--)
        {
            bytes[i] = (byte)(v & 0xFF);
            v >>= 8;
        }
        return new IPAddress(bytes).ToString();
    }

    /// <summary>CIDR representation — canonical IPv6 + "/N".</summary>
    public static string ToCidr(UInt128 network, int prefix)
        => $"{ToIp(network)}/{prefix}";

    /// <summary>
    /// Host-usable range for an IPv6 subnet. Unlike IPv4 we do NOT
    /// reserve any addresses — RFC 4291 abandons the IPv4 reservation
    /// model. Every address in the block is usable, including the
    /// "network" address. A /127 is a point-to-point link (RFC 6164),
    /// /128 is a single host — both trivially valid.
    /// </summary>
    public static (UInt128 firstUsable, UInt128 lastUsable) HostRange(UInt128 network, UInt128 last)
        => (network, last);

    /// <summary>
    /// Block size as a <see cref="UInt128"/>: <c>2^(128 - prefix)</c>.
    /// A /64 is <c>2^64</c> — far beyond anything that fits in a
    /// <c>long</c>, hence the UInt128 arithmetic.
    /// </summary>
    public static UInt128 BlockSize(int prefix)
        => prefix == 0 ? UInt128.Zero : UInt128.One << (128 - prefix);

    /// <summary>
    /// Round up to the next <paramref name="stride"/>-aligned value.
    /// <paramref name="stride"/> must be a power of two (it always is
    /// in practice: <see cref="BlockSize"/> output).
    /// </summary>
    public static UInt128 AlignUp(UInt128 address, UInt128 stride)
    {
        if (stride == UInt128.Zero) return address;
        var rem = address % stride;
        return rem == UInt128.Zero ? address : address + (stride - rem);
    }

    /// <summary>Inclusive interval overlap.</summary>
    public static bool Overlaps(UInt128 a1, UInt128 a2, UInt128 b1, UInt128 b2)
        => a1 <= b2 && b1 <= a2;

    internal static UInt128 ToUInt128(IPAddress addr)
    {
        var bytes = addr.GetAddressBytes();
        if (bytes.Length != 16)
            throw new ArgumentException("IPv6 address required.", nameof(addr));
        UInt128 v = 0;
        for (var i = 0; i < 16; i++)
            v = (v << 8) | bytes[i];
        return v;
    }

    internal static UInt128 IpToUInt128(string ip) => ToUInt128(IPAddress.Parse(ip));
}
