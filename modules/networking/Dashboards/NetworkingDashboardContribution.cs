using System.Windows;
using Central.Engine.Auth;
using Central.Engine.Models;
using Central.Engine.Widgets;
using Npgsql;

namespace Central.Module.Networking.Dashboards;

/// <summary>
/// Networking module's dashboard contribution — switches, VLANs, BGP peer
/// counts, ping health. Added to <see cref="DashboardContributionRegistry"/>
/// at module load; removed when the tenant disables Networking.
/// </summary>
public class NetworkingDashboardContribution : IDashboardContribution
{
    public string SectionTitle => "Networking";
    public int SortOrder => 20;
    public string? RequiredPermission => P.SwitchesRead;

    public async Task<IEnumerable<UIElement>> BuildCardsAsync(string dsn, CancellationToken ct = default)
    {
        var cards = new List<UIElement>();

        int total = 0, online = 0, offline = 0;
        double avgPing = 0;
        int vlans = 0, bgpPeers = 0;

        await using var conn = new NpgsqlConnection(dsn);
        await conn.OpenAsync(ct);

        // Switch ping health
        try
        {
            await using var cmd = new NpgsqlCommand(@"
                SELECT COUNT(*),
                       COUNT(*) FILTER (WHERE last_ping_ok = true),
                       COUNT(*) FILTER (WHERE last_ping_ok = false OR last_ping_ok IS NULL),
                       COALESCE(AVG(last_ping_ms) FILTER (WHERE last_ping_ok = true), 0)
                FROM switches", conn);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            if (await r.ReadAsync(ct))
            {
                total = r.GetInt32(0);
                online = r.GetInt32(1);
                offline = r.GetInt32(2);
                avgPing = r.GetDouble(3);
            }
        }
        catch { /* switches table absent — tenant hasn't provisioned yet */ }

        // VLAN + BGP counts
        try
        {
            await using var cmd = new NpgsqlCommand(
                "SELECT (SELECT COUNT(*) FROM vlan_inventory), (SELECT COUNT(*) FROM bgp_neighbors)", conn);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            if (await r.ReadAsync(ct))
            {
                vlans = r.GetInt32(0);
                bgpPeers = r.GetInt32(1);
            }
        }
        catch { /* tables absent */ }

        cards.Add(KpiCardBuilder.Build("Switches",   total,    0, false));
        cards.Add(KpiCardBuilder.Build("Online",     online,   0, false));
        cards.Add(KpiCardBuilder.Build("Offline",    offline,  0, lowerIsBetter: true));
        cards.Add(KpiCardBuilder.Build("Avg Latency", avgPing, 0, true, "float1"));
        cards.Add(KpiCardBuilder.Build("VLANs",      vlans,    0, false));
        cards.Add(KpiCardBuilder.Build("BGP Peers",  bgpPeers, 0, false));

        return cards;
    }
}
