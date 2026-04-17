using Central.Engine.Net.Hierarchy;

namespace Central.Engine.Net.Devices;

/// <summary>
/// A pluggable hardware module inside a chassis <see cref="Device"/> —
/// linecard, transceiver, PSU, fan, or "other". Small fixed-form
/// switches have no module rows; chassis switches have one row per
/// physical slot.
/// </summary>
public class Module : EntityBase
{
    public Guid DeviceId { get; set; }

    /// <summary>Physical slot identifier — <c>"fpc0"</c>, <c>"psu0"</c>, <c>"sfp1"</c>.</summary>
    public string Slot { get; set; } = "";

    public ModuleType ModuleType { get; set; } = ModuleType.Linecard;
    public string? Model { get; set; }
    public string? SerialNumber { get; set; }
    public string? PartNumber { get; set; }
}
