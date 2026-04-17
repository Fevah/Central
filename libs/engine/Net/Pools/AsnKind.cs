namespace Central.Engine.Net.Pools;

/// <summary>
/// ASN address family. Private2 is the 16-bit private range
/// (64512-65534); Private4 is the 32-bit private range
/// (4200000000-4294967294). Public is anything else — used when a
/// customer's network really does BGP on the public internet.
/// </summary>
public enum AsnKind
{
    Private2,
    Private4,
    Public,
}
