namespace Central.Engine.Net.Devices;

/// <summary>
/// L2 posture for a <see cref="Port"/>.
///
/// <list type="bullet">
///   <item><see cref="Unset"/> — no L2 role assigned; port is dark.</item>
///   <item><see cref="Access"/> — untagged traffic on a single VLAN.</item>
///   <item><see cref="Trunk"/> — tagged traffic for many VLANs, optional native VLAN.</item>
///   <item><see cref="Routed"/> — L3 port, no switching (SVI / P2P).</item>
/// </list>
/// </summary>
public enum PortMode
{
    Unset,
    Access,
    Trunk,
    Routed,
}
