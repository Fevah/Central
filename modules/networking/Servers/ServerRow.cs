namespace Central.Module.Networking.Servers;

/// <summary>
/// Flat row bound to the DevExpress GridControl in
/// <c>ServerGridPanel</c>. Projection of <c>net.server</c> + the
/// joined building_code + server_profile_code + live NIC count.
/// Grid view only; mutations go through the server detail dialog
/// (future) or the REST endpoints landed in Phase 6c.
/// </summary>
public class ServerRow
{
    public Guid Id { get; set; }

    public string Hostname { get; set; } = "";
    public string BuildingCode { get; set; } = "";
    public string ProfileCode { get; set; } = "";

    public long? Asn { get; set; }
    public string LoopbackIp { get; set; } = "";

    /// <summary>How many <c>net.server_nic</c> rows exist for this server.</summary>
    public int NicCount { get; set; }

    public string Status { get; set; } = "";
    public string Lock { get; set; } = "";
    public int Version { get; set; }

    public string? ManagementIp { get; set; }
    public bool? LastPingOk { get; set; }
    public decimal? LastPingMs { get; set; }
}
