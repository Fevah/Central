using Central.Engine.Net.Hierarchy;

namespace Central.Engine.Net.Pools;

/// <summary>
/// An IP supernet (e.g. <c>10.0.0.0/8</c>) from which subnets are carved.
/// Network is kept as a CIDR string — parsing happens at the boundary via
/// <c>System.Net.IPNetwork</c>.
/// </summary>
public class IpPool : EntityBase
{
    public string PoolCode { get; set; } = "";
    public string DisplayName { get; set; } = "";

    /// <summary>CIDR notation. Example: <c>"10.0.0.0/8"</c>.</summary>
    public string Network { get; set; } = "";

    public IpAddressFamily AddressFamily { get; set; } = IpAddressFamily.V4;
}

/// <summary>
/// A subnet carved from an <see cref="IpPool"/>. The DB enforces the
/// no-overlap rule via a GIST EXCLUDE constraint — application code
/// doesn't need to do ad-hoc checks before insert.
///
/// <see cref="ParentSubnetId"/> lets us model nested /16 → /24 → /30
/// hierarchies without flattening the rollup view.
/// <see cref="VlanId"/> is an optional pointer to the VLAN this subnet
/// anchors (common for SVI / L3-interface patterns).
/// </summary>
public class Subnet : EntityBase
{
    public Guid PoolId { get; set; }
    public Guid? ParentSubnetId { get; set; }
    public string SubnetCode { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string Network { get; set; } = "";
    public PoolScopeLevel ScopeLevel { get; set; } = PoolScopeLevel.Free;
    public Guid? ScopeEntityId { get; set; }
    public Guid? VlanId { get; set; }
}

/// <summary>
/// A concrete IP from a <see cref="Subnet"/>. The DB's UNIQUE
/// <c>(organization_id, address)</c> index prevents double-allocation
/// across the tenant, even for IPs in different subnets.
/// <see cref="IsReserved"/> marks addresses the allocation service must
/// skip (gateway, broadcast, network, anycast anchors).
/// </summary>
public class IpAddress : EntityBase
{
    public Guid SubnetId { get; set; }
    public string Address { get; set; } = "";
    public string? AssignedToType { get; set; }
    public Guid? AssignedToId { get; set; }
    public bool IsReserved { get; set; }
    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;
}
