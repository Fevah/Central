using System.Text.Json.Nodes;
using Central.Engine.Net.Hierarchy;

namespace Central.Engine.Net.Devices;

/// <summary>
/// A physical or breakout port on a <see cref="Device"/>. Interface
/// names follow the PicOS / Junos shape (<c>xe-1/1/N</c>,
/// <c>ge-1/1/N</c>, <c>xe-1/1/31.2</c> for a breakout sub-port).
///
/// A breakout sub-port has its parent's id in
/// <see cref="BreakoutParentId"/>. LAG members carry their LAG's id in
/// <see cref="AggregateEthernetId"/> — the aggregate itself is the
/// <see cref="AggregateEthernet"/> row on the same device.
/// </summary>
public class Port : EntityBase
{
    public Guid DeviceId { get; set; }
    public Guid? ModuleId { get; set; }
    public Guid? BreakoutParentId { get; set; }
    public Guid? AggregateEthernetId { get; set; }

    public string InterfaceName { get; set; } = "";

    /// <summary>
    /// Interface prefix — <c>"xe"</c> (10/25/40/100G SFP+),
    /// <c>"ge"</c> (1G), <c>"et"</c> (100G), <c>"fe"</c> (100M).
    /// Discriminates speed families vendor-neutrally.
    /// </summary>
    public string InterfacePrefix { get; set; } = "xe";

    /// <summary>Current port speed in Mbps. Null means "default for prefix / model".</summary>
    public long? SpeedMbps { get; set; }

    public bool AdminUp { get; set; }
    public string? Description { get; set; }

    public PortMode PortMode { get; set; } = PortMode.Unset;

    /// <summary>Native / untagged VLAN for trunk or access ports; 1-4094.</summary>
    public int? NativeVlanId { get; set; }

    /// <summary>
    /// Free-form port-level config — QoS bindings, storm-control
    /// thresholds, LLDP overrides — as JSON. Extended without schema
    /// churn by just writing new keys.
    /// </summary>
    public JsonObject ConfigJson { get; set; } = new();
}
