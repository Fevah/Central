using Central.Engine.Net.Hierarchy;

namespace Central.Engine.Net.Links;

/// <summary>
/// A link-type catalog entry. Instances of <see cref="Link"/> FK here to
/// inherit a naming template + colour hint. Seeded for Immunocore with
/// the 7 types P2P / B2B / FW / DMZ / MLAG-Peer / Server-NIC / WAN.
///
/// <see cref="NamingTemplate"/> is a tokenised string expanded per-link
/// by the config builder (Phase 5e). Recognised tokens: <c>{site_a}</c>,
/// <c>{site_b}</c>, <c>{device_a}</c>, <c>{device_b}</c>,
/// <c>{port_a}</c>, <c>{port_b}</c>, <c>{vlan}</c>, <c>{description}</c>.
/// </summary>
public class LinkType : EntityBase
{
    public string TypeCode { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string? Description { get; set; }

    public string NamingTemplate { get; set; } = "{device_a}-to-{device_b}";

    /// <summary>
    /// How many <see cref="LinkEndpoint"/> rows the type expects.
    /// Always 2 in the seeded catalogue; higher values reserved for
    /// future hub-and-spoke shapes.
    /// </summary>
    public int RequiredEndpoints { get; set; } = 2;

    public string? ColorHint { get; set; }
}
