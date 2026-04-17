namespace Central.Engine.Net.Hierarchy;

/// <summary>
/// A building within a site (e.g. MEP-91). Once <see cref="EntityBase.Status"/>
/// reaches <see cref="EntityStatus.Active"/>, pool allocations (ASN block,
/// /16 subnet, loopback ranges, server ASN) are HardLocked.
/// </summary>
public class Building : EntityBase
{
    public Guid SiteId { get; set; }
    public Guid? BuildingProfileId { get; set; }

    /// <summary>Short code unique within the site (e.g. "MEP-91").</summary>
    public string BuildingCode { get; set; } = "";

    public string DisplayName { get; set; } = "";

    public int? BuildingNumber { get; set; }

    /// <summary>True for reserved "shell" buildings like "UK-RES01" / "EXP-01".</summary>
    public bool IsReserved { get; set; }

    // Phase-3 pool FKs (nullable until that phase lands)
    public Guid? AssignedSlash16SubnetId { get; set; }
    public Guid? AssignedAsnBlockId { get; set; }
    public Guid? AssignedLoopbackSwitchBlockId { get; set; }
    public Guid? AssignedLoopbackServerBlockId { get; set; }
    public Guid? ServerAsnAllocationId { get; set; }

    public int? MaxFloors { get; set; }
    public int? MaxDevicesTotal { get; set; }

    /// <summary>Explicit list of partner building IDs for B2B mesh.</summary>
    public Guid[] B2bPartners { get; set; } = Array.Empty<Guid>();
}
