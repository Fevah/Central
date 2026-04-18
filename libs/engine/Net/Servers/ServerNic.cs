using Central.Engine.Net.Hierarchy;

namespace Central.Engine.Net.Servers;

/// <summary>
/// One physical NIC on a <see cref="Server"/>. Immunocore's 4-NIC
/// profile produces four rows per server, <see cref="NicIndex"/> 0..3.
///
/// <see cref="TargetPortId"/> points at the switch-side
/// <c>net.port</c> the NIC plugs into. <see cref="TargetDeviceId"/>
/// is a denormalised copy of that port's device — useful for "all
/// NICs landing on MEP-91-CORE02" queries without a join.
///
/// <see cref="MlagSide"/> records which MLAG peer side carries this
/// NIC. The server-creation flow's fan-out policy (e.g. "NICs 0 + 2
/// -&gt; side A, 1 + 3 -&gt; side B") chooses the value; the column
/// itself just records it.
/// </summary>
public class ServerNic : EntityBase
{
    public Guid ServerId { get; set; }
    public int NicIndex { get; set; }

    public Guid? TargetPortId { get; set; }
    public Guid? TargetDeviceId { get; set; }

    public Guid? IpAddressId { get; set; }
    public Guid? SubnetId { get; set; }
    public Guid? VlanId { get; set; }

    public MlagSide MlagSide { get; set; } = MlagSide.None;

    public string? NicName { get; set; }
    public string? MacAddress { get; set; }
    public bool AdminUp { get; set; }

    public DateTime? LastPingAt { get; set; }
    public bool? LastPingOk { get; set; }
}
