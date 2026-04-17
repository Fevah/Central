namespace Central.Engine.Net;

/// <summary>
/// Lifecycle state for every entity in the networking engine.
/// Mirrors <c>net.entity_status</c> in PostgreSQL.
///
/// Transition rules:
/// <list type="bullet">
///   <item>Allocation of any downstream number (ASN / IP / VLAN / MLAG) forces
///     transition out of <see cref="Planned"/>.</item>
///   <item>Entering <see cref="Active"/> auto-applies <see cref="LockState.HardLock"/>
///     to every numbering attribute (ASN, loopback, Router-ID, MLAG domain,
///     MSTP priority, B2B-exposed subnet).</item>
///   <item><see cref="Retired"/> releases numbers back to the pool after a
///     configurable cool-down (default 90 days).</item>
/// </list>
/// </summary>
public enum EntityStatus
{
    Planned,
    Reserved,
    Active,
    Deprecated,
    Retired,
}
