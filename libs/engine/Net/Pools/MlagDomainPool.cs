using Central.Engine.Net.Hierarchy;

namespace Central.Engine.Net.Pools;

/// <summary>
/// A range of MLAG domain IDs available for allocation. MLAG domains
/// share the 1-4094 space with VLANs (same 802.1Q field on many
/// vendors) but are tracked separately since collisions are on the
/// MLAG-peer L2 domain, not the fabric VLAN.
/// </summary>
public class MlagDomainPool : EntityBase
{
    public string PoolCode { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public int DomainFirst { get; set; }
    public int DomainLast { get; set; }
}

/// <summary>
/// A specific MLAG domain handed out from a
/// <see cref="MlagDomainPool"/>. UNIQUE
/// <c>(organization_id, domain_id)</c> — two buildings sharing a
/// campus-wide domain ID would collide.
/// </summary>
public class MlagDomain : EntityBase
{
    public Guid PoolId { get; set; }
    public int DomainId { get; set; }
    public string DisplayName { get; set; } = "";
    public PoolScopeLevel ScopeLevel { get; set; } = PoolScopeLevel.Building;
    public Guid? ScopeEntityId { get; set; }
}
