namespace Central.Engine.Net.Pools;

/// <summary>
/// IP address family for <see cref="IpPool"/>. Stored as "v4" / "v6" in
/// the DB; these are the only values we plan to support.
/// </summary>
public enum IpAddressFamily
{
    V4,
    V6,
}
