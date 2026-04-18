using Central.Engine.Net.Hierarchy;

namespace Central.Engine.Net.Servers;

/// <summary>
/// A server-type catalog entry. The profile carries the fan-out count
/// (<see cref="NicCount"/>), the default loopback prefix, and the
/// tokenised hostname template the server-creation flow expands per
/// new row.
///
/// <see cref="NamingTemplate"/> recognised tokens: <c>{region_code}</c>,
/// <c>{site_code}</c>, <c>{building_code}</c>, <c>{rack_code}</c>,
/// <c>{profile_code}</c>, <c>{instance}</c>.
/// </summary>
public class ServerProfile : EntityBase
{
    public string ProfileCode { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string? Description { get; set; }

    /// <summary>
    /// How many <see cref="ServerNic"/> rows a server of this profile
    /// should have. Immunocore's Server4NIC profile = 4; other
    /// customers may pick 2 (single pair) or 6/8 (multi-fabric).
    /// </summary>
    public int NicCount { get; set; } = 4;

    /// <summary>Suggested prefix length for the loopback allocation (32 for IPv4, 128 for IPv6).</summary>
    public int DefaultLoopbackPrefix { get; set; } = 32;

    public string NamingTemplate { get; set; } = "{building_code}-SRV{instance}";

    public string? ColorHint { get; set; }
}
