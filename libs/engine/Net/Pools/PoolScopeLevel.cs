namespace Central.Engine.Net.Pools;

/// <summary>
/// Which level of the geographic hierarchy a pool / block / allocation is
/// bound to. "Free" means the row exists but isn't attached to any
/// hierarchy entity yet — useful for pre-carving ranges before a
/// building comes online.
///
/// Persisted as text in the net.* schema rather than a Postgres enum so
/// a new tier (Room, Device, …) doesn't require a migration. Each table
/// carries a CHECK constraint restricting scope_level to the subset that
/// makes sense for it (e.g. vlan_block caps at Building, subnet goes
/// down to Room).
/// </summary>
public enum PoolScopeLevel
{
    Free,
    Region,
    Site,
    Building,
    Floor,
    Room,
    Device,
}
