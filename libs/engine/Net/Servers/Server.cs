using Central.Engine.Net.Hierarchy;

namespace Central.Engine.Net.Servers;

/// <summary>
/// A physical / virtual server. Sibling concept to <c>Device</c>:
/// both consume ASN + loopback allocations, both have management-
/// plane fields with probe state, both live inside a building /
/// room / rack.
///
/// Differences from a switch: servers own <see cref="ServerNic"/>
/// children instead of ports; their NICs plug *into* a switch port
/// (on the other side of the cable).
/// </summary>
public class Server : EntityBase
{
    public Guid? ServerProfileId { get; set; }
    public Guid? BuildingId { get; set; }
    public Guid? RoomId { get; set; }
    public Guid? RackId { get; set; }
    public Guid? AsnAllocationId { get; set; }
    public Guid? LoopbackIpAddressId { get; set; }

    public string Hostname { get; set; } = "";
    public string? ServerCode { get; set; }
    public string? DisplayName { get; set; }

    public string? HardwareModel { get; set; }
    public string? SerialNumber { get; set; }
    public string? MacAddress { get; set; }
    public string? FirmwareVersion { get; set; }

    // Management plane — same shape as net.device so the UI can
    // render ping / SSH state for either entity the same way.
    public string? ManagementIp { get; set; }
    public string? SshUsername { get; set; }
    public int? SshPort { get; set; }
    public DateTime? LastPingAt { get; set; }
    public bool? LastPingOk { get; set; }
    public decimal? LastPingMs { get; set; }
    public DateTime? LastSshAt { get; set; }
    public bool? LastSshOk { get; set; }

    /// <summary>
    /// <c>public.servers.id</c> this row was migrated from. Dropped
    /// once Phase 11 retires the legacy table.
    /// </summary>
    public int? LegacyServerId { get; set; }
}
