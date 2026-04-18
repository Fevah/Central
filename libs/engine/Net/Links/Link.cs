using System.Text.Json.Nodes;
using Central.Engine.Net.Hierarchy;

namespace Central.Engine.Net.Links;

/// <summary>
/// A network link — one row in <c>net.link</c>, paired with exactly two
/// (or more, for future topologies) <see cref="LinkEndpoint"/> rows.
/// Replaces the three legacy tables <c>public.p2p_links</c>,
/// <c>public.b2b_links</c>, <c>public.fw_links</c>.
///
/// <see cref="ConfigJson"/> absorbs per-type extensions without forcing
/// schema churn:
/// <list type="bullet">
///   <item><b>B2B</b>: <c>tx</c>, <c>rx</c>, <c>media</c>, <c>speed</c></item>
///   <item><b>P2P</b>: <c>desc_a</c>, <c>desc_b</c></item>
///   <item><b>FW</b>: nothing yet</item>
/// </list>
///
/// <see cref="LegacyLinkKind"/> + <see cref="LegacyLinkId"/> preserve the
/// origin of imported rows — the Phase-5f byte-parity test joins back
/// through them to prove generated config is unchanged.
/// </summary>
public class Link : EntityBase
{
    public Guid LinkTypeId { get; set; }
    public Guid? BuildingId { get; set; }

    /// <summary>
    /// Stable human-readable identifier — the value of
    /// <c>p2p_links.link_id</c> / <c>b2b_links.link_id</c> /
    /// <c>fw_links.link_id</c> on imported rows. UNIQUE per tenant.
    /// </summary>
    public string LinkCode { get; set; } = "";

    public string? DisplayName { get; set; }
    public string? Description { get; set; }

    public Guid? VlanId { get; set; }
    public Guid? SubnetId { get; set; }

    public JsonObject ConfigJson { get; set; } = new();

    /// <summary>
    /// Which legacy table the row came from. Null for links created
    /// natively on <c>net.link</c> after the cutover.
    /// </summary>
    public string? LegacyLinkKind { get; set; }

    /// <summary>Row id on the legacy table; null for post-cutover links.</summary>
    public int? LegacyLinkId { get; set; }
}
