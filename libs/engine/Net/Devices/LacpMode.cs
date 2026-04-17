namespace Central.Engine.Net.Devices;

/// <summary>
/// LACP mode for an <see cref="AggregateEthernet"/> bundle. Lowercased
/// in the DB to match PicOS / Junos config syntax.
/// </summary>
public enum LacpMode
{
    Active,
    Passive,
    Static,
}
