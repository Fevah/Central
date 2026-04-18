using Central.Engine.Net.Hierarchy;

namespace Central.Engine.Net.Links;

/// <summary>
/// One end of a <see cref="Link"/>. Two per link (endpoint_order 0 = A,
/// 1 = B); UNIQUE <c>(link_id, endpoint_order)</c> keeps pairs
/// unambiguous.
///
/// Every FK is optional because imports land before every port has
/// a resolved <c>net.port</c> row. <see cref="InterfaceName"/> is a
/// free-text copy of the raw interface string — the ports-sync
/// service (Phase 5d) populates <see cref="PortId"/> later when it
/// can match the string back to a known port.
/// </summary>
public class LinkEndpoint : EntityBase
{
    public Guid LinkId { get; set; }

    /// <summary>0 = A side, 1 = B side.</summary>
    public int EndpointOrder { get; set; }

    public Guid? DeviceId { get; set; }
    public Guid? PortId { get; set; }
    public Guid? IpAddressId { get; set; }
    public Guid? VlanId { get; set; }

    public string? InterfaceName { get; set; }
    public string? Description { get; set; }
}
