using Central.Engine.Net.Hierarchy;

namespace Central.Engine.Net.Devices;

/// <summary>
/// The real switch / router / firewall entity. Replaces the legacy
/// <c>public.switches</c> table; while Phase 4 is in flight a dual-write
/// trigger keeps the two tables in sync and <see cref="LegacySwitchId"/>
/// points at the mirror row.
///
/// The management-plane columns (<see cref="ManagementIp"/>,
/// <see cref="SshUsername"/>, <see cref="LastPingOk"/>, …) mirror the
/// shape of <c>public.switches</c> exactly so the dual-write trigger
/// ships values without translation.
/// </summary>
public class Device : EntityBase
{
    public Guid? DeviceRoleId { get; set; }
    public Guid? BuildingId { get; set; }
    public Guid? RoomId { get; set; }
    public Guid? RackId { get; set; }
    public Guid? AsnAllocationId { get; set; }

    public string Hostname { get; set; } = "";
    public string? DeviceCode { get; set; }
    public string? DisplayName { get; set; }

    public string? HardwareModel { get; set; }
    public string? SerialNumber { get; set; }
    public string? MacAddress { get; set; }

    /// <summary>
    /// Vendor-neutral firmware/software version label (PicOS version,
    /// Junos revision, FRR build, …). <c>public.switches.picos_version</c>
    /// maps here.
    /// </summary>
    public string? FirmwareVersion { get; set; }

    public string? ManagementIp { get; set; }
    public string? SshUsername { get; set; }
    public int? SshPort { get; set; }
    public bool ManagementVrf { get; set; }
    public bool InbandEnabled { get; set; }

    public DateTime? LastPingAt { get; set; }
    public bool? LastPingOk { get; set; }
    public decimal? LastPingMs { get; set; }
    public DateTime? LastSshAt { get; set; }
    public bool? LastSshOk { get; set; }

    /// <summary>
    /// Points at the <c>public.switches.id</c> this device was migrated
    /// from. Dual-write trigger uses it to mirror updates during Phase 4.
    /// Phase 11 drops the column.
    /// </summary>
    public Guid? LegacySwitchId { get; set; }
}
