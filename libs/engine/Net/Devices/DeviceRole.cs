using Central.Engine.Net.Hierarchy;

namespace Central.Engine.Net.Devices;

/// <summary>
/// A role type for devices in this tenant's catalog (Core, L1Core,
/// L2Core, MAN, STOR, SW, FW, …). Role drives defaults in the device
/// editor (ASN kind, loopback prefix, UI colour) but doesn't enforce
/// them — operators can override per-device.
/// </summary>
public class DeviceRole : EntityBase
{
    public string RoleCode { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string? Description { get; set; }

    /// <summary>Which <see cref="Central.Engine.Net.Pools.AsnKind"/> the device editor suggests.</summary>
    public string? DefaultAsnKind { get; set; }

    /// <summary>Suggested loopback prefix (typically 32 for v4, 128 for v6).</summary>
    public int? DefaultLoopbackPrefix { get; set; }

    /// <summary>UI accent colour hint ("blue" / "green" / "red" / …).</summary>
    public string? ColorHint { get; set; }
}
