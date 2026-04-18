using System.Collections.Generic;
using System.Text;

namespace Central.Engine.Net.Servers;

/// <summary>
/// Server-side sibling of <c>LinkNamingService</c> and
/// <c>DeviceNamingService</c>. Expands a
/// <see cref="ServerProfile.NamingTemplate"/> into a concrete
/// hostname for a new server.
///
/// <para>Recognised tokens:</para>
/// <list type="bullet">
///   <item><c>{region_code}</c>, <c>{site_code}</c>, <c>{building_code}</c>, <c>{rack_code}</c></item>
///   <item><c>{profile_code}</c> — e.g. "Server4NIC"</item>
///   <item><c>{instance}</c> — zero-padded ordinal</item>
/// </list>
///
/// <para>Same defensive semantics as the link / device services:
/// unknown tokens pass through verbatim, empty values collapse to
/// empty string, unmatched braces emit the tail unchanged, no DB
/// access.</para>
/// </summary>
public static class ServerNamingService
{
    public static string Expand(string template, ServerNamingContext ctx)
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
                sb.Append(template, open, template.Length - open);
                break;
            }
            var name = template.Substring(open + 1, close - open - 1);
            if (tokens.TryGetValue(name, out var value))
                sb.Append(value);
            else
                sb.Append(template, open, close - open + 1);
            i = close + 1;
        }
        return sb.ToString();
    }

    private static Dictionary<string, string> Tokens(ServerNamingContext ctx) => new()
    {
        ["region_code"]   = ctx.RegionCode ?? "",
        ["site_code"]     = ctx.SiteCode ?? "",
        ["building_code"] = ctx.BuildingCode ?? "",
        ["rack_code"]     = ctx.RackCode ?? "",
        ["profile_code"]  = ctx.ProfileCode ?? "",
        ["instance"]      = FormatInstance(ctx.Instance, ctx.InstancePadding),
    };

    private static string FormatInstance(int? instance, int padding)
    {
        if (instance is null) return "";
        if (padding <= 0) return instance.Value.ToString();
        return instance.Value.ToString($"D{padding}");
    }
}

/// <summary>
/// Inputs for <see cref="ServerNamingService.Expand"/>. Instance is
/// zero-padded to <see cref="InstancePadding"/> digits (default 2,
/// matching the seeded template's "SRV01" convention).
/// </summary>
public record ServerNamingContext
{
    public string? RegionCode    { get; init; }
    public string? SiteCode      { get; init; }
    public string? BuildingCode  { get; init; }
    public string? RackCode      { get; init; }
    public string? ProfileCode   { get; init; }
    public int?    Instance      { get; init; }

    /// <summary>Default 2 for "SRV01" style. Set 0 to disable zero-pad.</summary>
    public int InstancePadding { get; init; } = 2;
}
