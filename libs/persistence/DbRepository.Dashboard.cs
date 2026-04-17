using Npgsql;
using Central.Engine.Models;

namespace Central.Persistence;

public partial class DbRepository
{
    public async Task<DashboardData> GetDashboardDataAsync()
    {
        var data = new DashboardData();
        await using var conn = await OpenConnectionAsync();

        data.DeviceCount = await CountAsync(conn, "switch_guide", "WHERE is_deleted IS NOT TRUE");
        data.SwitchCount = await CountAsync(conn, "switches");
        data.UserCount = await CountAsync(conn, "app_users", "WHERE is_active = true");
        data.LinkCount = await CountAsync(conn, "p2p_links", "WHERE is_deleted IS NOT TRUE")
                       + await CountAsync(conn, "b2b_links", "WHERE is_deleted IS NOT TRUE")
                       + await CountAsync(conn, "fw_links", "WHERE is_deleted IS NOT TRUE");
        data.VlanCount = await CountAsync(conn, "vlan_inventory");
        data.OpenTasks = await CountAsync(conn, "tasks", "WHERE status NOT IN ('Done','Closed','Cancelled')");

        data.SdOpenTickets = await CountAsync(conn, "sd_requests", "WHERE status NOT IN ('Resolved','Closed')");
        data.SdClosedToday = await CountAsync(conn, "sd_requests",
            "WHERE status IN ('Resolved','Closed') AND resolved_at >= CURRENT_DATE");
        data.SdPrevClosedToday = await CountAsync(conn, "sd_requests",
            "WHERE status IN ('Resolved','Closed') AND resolved_at >= CURRENT_DATE - 1 AND resolved_at < CURRENT_DATE");

        data.SdAvgResolutionHours = await ScalarDoubleAsync(conn,
            "SELECT COALESCE(AVG(EXTRACT(EPOCH FROM (resolved_at - created_at)) / 3600), 0) FROM sd_requests WHERE resolved_at >= NOW() - INTERVAL '30 days' AND resolved_at IS NOT NULL");
        data.SdPrevAvgResolutionHours = await ScalarDoubleAsync(conn,
            "SELECT COALESCE(AVG(EXTRACT(EPOCH FROM (resolved_at - created_at)) / 3600), 0) FROM sd_requests WHERE resolved_at >= NOW() - INTERVAL '60 days' AND resolved_at < NOW() - INTERVAL '30 days' AND resolved_at IS NOT NULL");

        data.SyncConfigCount = await CountAsync(conn, "sync_configs", "WHERE is_enabled = true");
        data.SyncFailures = await CountAsync(conn, "sync_configs", "WHERE last_sync_status = 'failed'");
        data.AuthEvents24h = await CountAsync(conn, "auth_events", "WHERE timestamp >= NOW() - INTERVAL '24 hours'");
        data.PrevAuthEvents24h = await CountAsync(conn, "auth_events", "WHERE timestamp >= NOW() - INTERVAL '48 hours' AND timestamp < NOW() - INTERVAL '24 hours'");
        data.FailedLogins24h = await CountAsync(conn, "auth_events", "WHERE timestamp >= NOW() - INTERVAL '24 hours' AND success = false");
        data.PrevFailedLogins24h = await CountAsync(conn, "auth_events", "WHERE timestamp >= NOW() - INTERVAL '48 hours' AND timestamp < NOW() - INTERVAL '24 hours' AND success = false");

        data.RecentActivity = await GetRecentActivityAsync(conn);
        return data;
    }

    private static async Task<int> CountAsync(NpgsqlConnection conn, string table, string where = "")
    {
        try
        {
            await using var cmd = new NpgsqlCommand($"SELECT COUNT(*) FROM {table} {where}", conn);
            return (int)(long)(await cmd.ExecuteScalarAsync())!;
        }
        catch { return 0; }
    }

    private static async Task<double> ScalarDoubleAsync(NpgsqlConnection conn, string sql)
    {
        try
        {
            await using var cmd = new NpgsqlCommand(sql, conn);
            var result = await cmd.ExecuteScalarAsync();
            return result is double d ? d : result is decimal m ? (double)m : 0;
        }
        catch { return 0; }
    }

    private static async Task<List<ActivityItem>> GetRecentActivityAsync(NpgsqlConnection conn)
    {
        var items = new List<ActivityItem>();
        try
        {
            await using var cmd = new NpgsqlCommand(
                @"(SELECT timestamp AS ts, CASE WHEN success THEN '>' ELSE 'x' END AS icon,
                       event_type || ': ' || COALESCE(username, 'unknown') || CASE WHEN NOT success THEN ' - ' || COALESCE(error_message, 'failed') ELSE '' END AS msg
                   FROM auth_events ORDER BY timestamp DESC LIMIT 10)
                  UNION ALL
                  (SELECT started_at AS ts,
                       CASE status WHEN 'success' THEN 'o' WHEN 'failed' THEN 'x' ELSE '.' END AS icon,
                       'Sync: ' || COALESCE(entity_name, 'config ' || sync_config_id) || ' - ' || records_read || ' read, ' || records_created || ' created' AS msg
                   FROM sync_log ORDER BY started_at DESC LIMIT 10)
                  ORDER BY ts DESC LIMIT 20", conn);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                items.Add(new ActivityItem
                {
                    Time = r.GetDateTime(0).ToString("HH:mm"),
                    Icon = r.GetString(1),
                    Message = r.GetString(2)
                });
        }
        catch { }
        return items;
    }
}
