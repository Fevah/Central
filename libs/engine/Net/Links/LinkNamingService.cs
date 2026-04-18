using System.Collections.Generic;
using System.Text;

namespace Central.Engine.Net.Links;

/// <summary>
/// Expands a <see cref="LinkType.NamingTemplate"/> into a concrete
/// <c>link_code</c> for a specific link instance. The template
/// language is a simple brace-substitution — no conditionals, no
/// escaping — so a config-generating service can produce a stable,
/// human-predictable link code from an entity graph.
///
/// <para>Recognised tokens:</para>
/// <list type="bullet">
///   <item><c>{site_a}</c>, <c>{site_b}</c> — A/B endpoint building codes</item>
///   <item><c>{device_a}</c>, <c>{device_b}</c> — A/B hostnames</item>
///   <item><c>{port_a}</c>, <c>{port_b}</c> — A/B interface names</item>
///   <item><c>{role_a}</c>, <c>{role_b}</c> — A/B device role codes</item>
///   <item><c>{vlan}</c> — VLAN id as decimal string</item>
///   <item><c>{subnet}</c> — subnet CIDR in dotted-quad form</item>
///   <item><c>{description}</c> — the link's <c>Description</c> field</item>
///   <item><c>{link_code}</c> — the existing code (for re-formatting)</item>
/// </list>
///
/// <para>Unknown tokens pass through unchanged so a typo like
/// <c>{devic_a}</c> stays visible in the output rather than
/// silently disappearing. Empty values substitute to the empty
/// string — callers collapse duplicate separators afterwards if
/// they want.</para>
///
/// <para>No DB access lives here — the caller provides the entity
/// graph as a <see cref="LinkNamingContext"/>. Keeps the service
/// pure so unit tests run without a database.</para>
/// </summary>
public static class LinkNamingService
{
    /// <summary>
    /// Expand <paramref name="template"/> against <paramref name="ctx"/>.
    /// Unknown tokens (anything between <c>{…}</c> not listed above)
    /// are preserved verbatim.
    /// </summary>
    public static string Expand(string template, LinkNamingContext ctx)
    {
        if (string.IsNullOrEmpty(template)) return "";
        var tokens = Tokens(ctx);

        var sb = new StringBuilder(template.Length + 32);
        var i = 0;
        while (i < template.Length)
        {
            var open = template.IndexOf('{', i);
            if (open < 0)
            {
                sb.Append(template, i, template.Length - i);
                break;
            }
            sb.Append(template, i, open - i);
            var close = template.IndexOf('}', open + 1);
            if (close < 0)
            {
                // Unmatched opening brace — bail; emit the rest verbatim.
                sb.Append(template, open, template.Length - open);
                break;
            }
            var name = template.Substring(open + 1, close - open - 1);
            if (tokens.TryGetValue(name, out var value))
                sb.Append(value);
            else
                sb.Append(template, open, close - open + 1);   // verbatim, braces included
            i = close + 1;
        }
        return sb.ToString();
    }

    private static Dictionary<string, string> Tokens(LinkNamingContext ctx) => new()
    {
        ["site_a"]      = ctx.SiteA ?? "",
        ["site_b"]      = ctx.SiteB ?? "",
        ["device_a"]    = ctx.DeviceA ?? "",
        ["device_b"]    = ctx.DeviceB ?? "",
        ["port_a"]      = ctx.PortA ?? "",
        ["port_b"]      = ctx.PortB ?? "",
        ["role_a"]      = ctx.RoleA ?? "",
        ["role_b"]      = ctx.RoleB ?? "",
        ["vlan"]        = ctx.VlanId?.ToString() ?? "",
        ["subnet"]      = ctx.Subnet ?? "",
        ["description"] = ctx.Description ?? "",
        ["link_code"]   = ctx.LinkCode ?? "",
    };
}

/// <summary>
/// The data bundle <see cref="LinkNamingService.Expand"/> substitutes
/// into a template. Every field is optional; unset fields expand to
/// empty string. Callers populate what they have — a P2P link knows
/// device_a and device_b; a WAN link may only have device_a and
/// description.
/// </summary>
public record LinkNamingContext
{
    public string? SiteA       { get; init; }
    public string? SiteB       { get; init; }
    public string? DeviceA     { get; init; }
    public string? DeviceB     { get; init; }
    public string? PortA       { get; init; }
    public string? PortB       { get; init; }
    public string? RoleA       { get; init; }
    public string? RoleB       { get; init; }
    public int?    VlanId      { get; init; }
    public string? Subnet      { get; init; }
    public string? Description { get; init; }
    public string? LinkCode    { get; init; }
}
