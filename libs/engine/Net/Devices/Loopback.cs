using Central.Engine.Net.Hierarchy;

namespace Central.Engine.Net.Devices;

/// <summary>
/// A loopback interface on a device. <see cref="LoopbackNumber"/> is
/// the suffix (0 for lo0, 1 for lo1, …). <see cref="IpAddressId"/>
/// points at the <see cref="Central.Engine.Net.Pools.IpAddress"/> row
/// that backs the interface — allocation goes through
/// <c>IpAllocationService</c> so the /32 is tracked as used at the
/// subnet level.
/// </summary>
public class Loopback : EntityBase
{
    public Guid DeviceId { get; set; }
    public int LoopbackNumber { get; set; }
    public Guid? IpAddressId { get; set; }
    public string? Description { get; set; }
}
