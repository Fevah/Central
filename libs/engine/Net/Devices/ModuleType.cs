namespace Central.Engine.Net.Devices;

/// <summary>
/// What kind of hardware <see cref="Module"/> a row represents. Stored
/// as text in the DB (CHECK-constrained) rather than a Postgres enum so
/// a new hardware type (e.g. "SmartNIC") doesn't need a migration.
/// </summary>
public enum ModuleType
{
    Linecard,
    Transceiver,
    PSU,
    Fan,
    Other,
}
