using System.Windows;
using Central.Engine.Auth;
using Central.Engine.Models;
using Central.Engine.Widgets;
using Npgsql;

namespace Central.Module.Networking.Dashboards;

/// <summary>
/// Devices module's dashboard contribution — total / active / reserved counts.
/// </summary>
public class DevicesDashboardContribution : IDashboardContribution
{
    public string SectionTitle => "Devices";
    public int SortOrder => 10;
    public string? RequiredPermission => P.DevicesRead;

    public async Task<IEnumerable<UIElement>> BuildCardsAsync(string dsn, CancellationToken ct = default)
    {
        int total = 0, active = 0, reserved = 0;

        try
        {
            await using var conn = new NpgsqlConnection(dsn);
            await conn.OpenAsync(ct);
            await using var cmd = new NpgsqlCommand(@"
                SELECT COUNT(*),
                       COUNT(*) FILTER (WHERE status = 'Active' OR status IS NULL),
                       COUNT(*) FILTER (WHERE status = 'Reserved')
                FROM switch_guide WHERE is_deleted IS NOT TRUE", conn);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            if (await r.ReadAsync(ct))
            {
                total = r.GetInt32(0);
                active = r.GetInt32(1);
                reserved = r.GetInt32(2);
            }
        }
        catch { /* table absent */ }

        return new UIElement[]
        {
            KpiCardBuilder.Build("Total Devices", total,    0, false),
            KpiCardBuilder.Build("Active",         active,   0, false),
            KpiCardBuilder.Build("Reserved",       reserved, 0, false),
        };
    }
}
