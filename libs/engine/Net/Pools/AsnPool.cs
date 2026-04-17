using Central.Engine.Net.Hierarchy;

namespace Central.Engine.Net.Pools;

/// <summary>
/// Top of the ASN hierarchy. Defines an ASN range [<see cref="AsnFirst"/>,
/// <see cref="AsnLast"/>] from which blocks can be carved and allocations
/// handed out.
///
/// 64-bit in the model even though RFC-compliant ASNs top out at
/// 4,294,967,294 — matches the bigint column and keeps the door open
/// for future protocols.
/// </summary>
public class AsnPool : EntityBase
{
    public string PoolCode { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public long AsnFirst { get; set; }
    public long AsnLast { get; set; }
    public AsnKind AsnKind { get; set; } = AsnKind.Private2;
}

/// <summary>
/// A sub-range carved out of an <see cref="AsnPool"/> and bound to some
/// level of the hierarchy (via <see cref="PoolScopeLevel"/> and
/// <see cref="ScopeEntityId"/>). "Free" blocks exist but aren't yet
/// attached to any region / site / building.
/// </summary>
public class AsnBlock : EntityBase
{
    public Guid PoolId { get; set; }
    public string BlockCode { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public long AsnFirst { get; set; }
    public long AsnLast { get; set; }
    public PoolScopeLevel ScopeLevel { get; set; } = PoolScopeLevel.Free;
    public Guid? ScopeEntityId { get; set; }
}

/// <summary>
/// A single ASN handed out from an <see cref="AsnBlock"/>. The
/// allocation service guarantees (via the UNIQUE
/// <c>(organization_id, asn)</c> index) that no two concurrent
/// transactions can commit with the same value.
/// </summary>
public class AsnAllocation : EntityBase
{
    public Guid BlockId { get; set; }
    public long Asn { get; set; }
    public string AllocatedToType { get; set; } = "";   // Device / Server / Building
    public Guid AllocatedToId { get; set; }
    public DateTime AllocatedAt { get; set; } = DateTime.UtcNow;
}
