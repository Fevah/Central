namespace Central.Engine.Net.Pools;

/// <summary>
/// Discriminator for <see cref="ReservationShelfEntry"/>. The shelf is
/// one table for every kind of retired number — the resource_type column
/// tells the allocation service which pool to cross-check before re-
/// issuing the <see cref="ReservationShelfEntry.ResourceKey"/>.
/// </summary>
public enum ShelfResourceType
{
    Asn,
    Ip,
    Subnet,
    Vlan,
    Mlag,
    Mstp,
}
