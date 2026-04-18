using System.Collections.Generic;
using System.Text;

namespace Central.Engine.Net.Devices;

/// <summary>
/// Device-side sibling of <c>LinkNamingService</c>. Expands a
/// <see cref="DeviceRole.NamingTemplate"/> into a concrete hostname
/// using attributes from the hierarchy + instance counter.
///
/// <para>Recognised tokens:</para>
/// <list type="bullet">
///   <item><c>{region_code}</c> — <c>net.region.region_code</c></item>
///   <item><c>{site_code}</c> — <c>net.site.site_code</c></item>
///   <item><c>{building_code}</c> — <c>net.building.building_code</c></item>
///   <item><c>{rack_code}</c> — <c>net.rack.rack_code</c></item>
///   <item><c>{role_code}</c> — <c>net.device_role.role_code</c></item>
///   <item><c>{instance}</c> — zero-padded numeric suffix (e.g. "02")</item>
/// </list>
///
/// <para>Semantics match <c>LinkNamingService</c> deliberately:</para>
/// <list type="bullet">
///   <item>Unknown tokens pass through verbatim — typos stay visible.</item>
///   <item>Empty values substitute to the empty string — callers collapse
///     double-separators if they want.</item>
///   <item>Unmatched opening braces emit the tail unchanged.</item>
///   <item>No DB access — the caller supplies a
///     <see cref="DeviceNamingContext"/> record.</item>
/// </list>
/// </summary>
public static class DeviceNamingService
{
    /// <summary>
    /// Expand <paramref name="template"/> against <paramref name="ctx"/>.
    /// </summary>
    public static string Expand(string template, DeviceNamingContext ctx)
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

    private static Dictionary<string, string> Tokens(DeviceNamingContext ctx) => new()
    {
        ["region_code"]   = ctx.RegionCode ?? "",
        ["site_code"]     = ctx.SiteCode ?? "",
        ["building_code"] = ctx.BuildingCode ?? "",
        ["rack_code"]     = ctx.RackCode ?? "",
        ["role_code"]     = ctx.RoleCode ?? "",
        ["instance"]      = FormatInstance(ctx.Instance, ctx.InstancePadding),
    };

    /// <summary>
    /// Zero-pad the instance number to <paramref name="padding"/> digits.
    /// Default 2 matches PicOS convention ("MEP-91-CORE02" not
    /// "MEP-91-CORE2"). Padding = 0 disables zero-pad.
    /// </summary>
    private static string FormatInstance(int? instance, int padding)
    {
        if (instance is null) return "";
        if (padding <= 0) return instance.Value.ToString();
        return instance.Value.ToString($"D{padding}");
    }
}

/// <summary>
/// Inputs to <see cref="DeviceNamingService.Expand"/>. Every field is
/// optional; unset fields expand to empty string. <see cref="Instance"/>
/// is the 1-based ordinal of the device within its role + scope
/// (resolved by the caller — typically "count of active devices with
/// matching role in the building, plus one").
/// </summary>
public record DeviceNamingContext
{
    public string? RegionCode    { get; init; }
    public string? SiteCode      { get; init; }
    public string? BuildingCode  { get; init; }
    public string? RackCode      { get; init; }
    public string? RoleCode      { get; init; }
    public int?    Instance      { get; init; }

    /// <summary>
    /// Zero-pad width for <see cref="Instance"/>. Defaults to 2 so the
    /// seeded templates produce "MEP-91-CORE02" instead of
    /// "MEP-91-CORE2". Set to 0 to disable.
    /// </summary>
    public int InstancePadding { get; init; } = 2;
}
