using Central.Engine.Net.Hierarchy;

namespace Central.Engine.Net.Devices;

/// <summary>
/// A LAG / port-channel on a <see cref="Device"/>. Member ports set
/// their <see cref="Port.AggregateEthernetId"/> to this row's id.
/// </summary>
public class AggregateEthernet : EntityBase
{
    public Guid DeviceId { get; set; }

    /// <summary>Aggregate name — <c>"ae-0"</c>, <c>"ae-12"</c>.</summary>
    public string AeName { get; set; } = "";

    public LacpMode LacpMode { get; set; } = LacpMode.Active;

    /// <summary>Minimum LACP links for the aggregate to be considered up.</summary>
    public int MinLinks { get; set; } = 1;

    public string? Description { get; set; }
}
