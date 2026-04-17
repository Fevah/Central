using Central.Engine.Net.Hierarchy;

namespace Central.Engine.Net.Pools;

/// <summary>
/// Reusable VLAN pattern ("Servers", "Management", "DMZ"). When an
/// operator drops a template onto a scope, the allocation service
/// creates a <see cref="Vlan"/> row that inherits the template's
/// <see cref="VlanRole"/> + <see cref="Description"/> — keeps
/// generated configs consistent across sites.
/// </summary>
public class VlanTemplate : EntityBase
{
    public string TemplateCode { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string VlanRole { get; set; } = "";
    public string? Description { get; set; }
    public bool IsDefault { get; set; }
}

/// <summary>
/// A range of VLAN IDs [<see cref="VlanFirst"/>, <see cref="VlanLast"/>]
/// inside the 802.1Q space (1-4094). Blocks carve ranges out of pools;
/// individual VLAN rows are issued from blocks.
/// </summary>
public class VlanPool : EntityBase
{
    public string PoolCode { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public int VlanFirst { get; set; }
    public int VlanLast { get; set; }
}

/// <summary>
/// A sub-range of a <see cref="VlanPool"/> bound to a region / site /
/// building. Immunocore uses /21 blocks (2048 VLANs per building) but
/// the DB doesn't enforce the size — other customers can pick their
/// own block granularity.
/// </summary>
public class VlanBlock : EntityBase
{
    public Guid PoolId { get; set; }
    public string BlockCode { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public int VlanFirst { get; set; }
    public int VlanLast { get; set; }
    public PoolScopeLevel ScopeLevel { get; set; } = PoolScopeLevel.Free;
    public Guid? ScopeEntityId { get; set; }
}

/// <summary>
/// A specific VLAN ID issued from a <see cref="VlanBlock"/>. The UNIQUE
/// <c>(block_id, vlan_id)</c> index guarantees no double allocation
/// within a block; the block's ownership of the range (enforced at the
/// application layer) gives the "VLAN unique in its scope" invariant.
/// </summary>
public class Vlan : EntityBase
{
    public Guid BlockId { get; set; }
    public Guid? TemplateId { get; set; }
    public int VlanId { get; set; }
    public string DisplayName { get; set; } = "";
    public string? Description { get; set; }
    public PoolScopeLevel ScopeLevel { get; set; } = PoolScopeLevel.Free;
    public Guid? ScopeEntityId { get; set; }
}
