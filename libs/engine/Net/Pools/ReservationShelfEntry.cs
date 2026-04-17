using Central.Engine.Net.Hierarchy;

namespace Central.Engine.Net.Pools;

/// <summary>
/// A parked number waiting out its cool-down before it can be re-issued.
/// The allocation service checks the shelf before handing out a freshly-
/// computed "next free" value; if the shelf contains an entry for the
/// same resource and <see cref="AvailableAfter"/> is still in the future,
/// the allocator skips it.
///
/// <see cref="ResourceKey"/> is a canonical string form of the value
/// being shelved ("65121" for ASN, "10.11.101.0/24" for subnet, "101"
/// for VLAN, etc.) so one table can cover every resource type.
/// </summary>
public class ReservationShelfEntry : EntityBase
{
    public ShelfResourceType ResourceType { get; set; }
    public string ResourceKey { get; set; } = "";
    public Guid? PoolId { get; set; }
    public Guid? BlockId { get; set; }
    public DateTime RetiredAt { get; set; } = DateTime.UtcNow;
    public DateTime AvailableAfter { get; set; } = DateTime.UtcNow;
    public string? RetiredReason { get; set; }
}
