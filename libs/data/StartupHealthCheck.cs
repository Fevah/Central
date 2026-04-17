namespace Central.Data;

/// <summary>
/// Startup health check — verifies critical DB tables and services are available.
/// Run at startup to catch missing migrations or configuration issues early.
/// </summary>
public class StartupHealthCheck
{
    /// <summary>Critical tables that must exist for the app to function.</summary>
    private static readonly string[] RequiredTables =
    [
        "app_users", "roles", "role_permissions", "role_sites",
        "switch_guide", "switches", "p2p_links", "b2b_links", "fw_links",
        "vlan_inventory", "bgp_config",
        "sd_requests", "sd_technicians", "sd_groups", "sd_requesters",
        "identity_providers", "auth_events",
        "sync_configs", "sync_entity_maps", "sync_field_maps",
        "audit_log", "saved_filters", "panel_customizations",
        "user_settings", "lookup_values", "permissions"
    ];

    public class HealthCheckResult
    {
        public bool IsHealthy { get; set; }
        public List<string> MissingTables { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public int TableCount { get; set; }
        public TimeSpan DbLatency { get; set; }
        public string Summary => IsHealthy
            ? $"Healthy — {TableCount} tables, {DbLatency.TotalMilliseconds:F0}ms latency"
            : $"Unhealthy — {MissingTables.Count} missing tables: {string.Join(", ", MissingTables.Take(5))}";
    }

    /// <summary>Run the health check against the database.</summary>
    public static async Task<HealthCheckResult> CheckAsync(string connectionString)
    {
        var result = new HealthCheckResult();
        var sw = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            await using var conn = new Npgsql.NpgsqlConnection(connectionString);
            await conn.OpenAsync();
            result.DbLatency = sw.Elapsed;

            // Get all existing tables
            var existingTables = new HashSet<string>();
            await using var cmd = new Npgsql.NpgsqlCommand(
                "SELECT tablename FROM pg_tables WHERE schemaname='public'", conn);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                existingTables.Add(r.GetString(0));
            result.TableCount = existingTables.Count;

            // Check required tables
            foreach (var table in RequiredTables)
            {
                if (!existingTables.Contains(table))
                    result.MissingTables.Add(table);
            }

            // Check for users
            await r.CloseAsync();
            await using var userCmd = new Npgsql.NpgsqlCommand("SELECT COUNT(*) FROM app_users", conn);
            var userCount = (long)(await userCmd.ExecuteScalarAsync())!;
            if (userCount == 0)
                result.Warnings.Add("No users in app_users — run migrations and create admin user");

            result.IsHealthy = result.MissingTables.Count == 0;
        }
        catch (Exception ex)
        {
            result.IsHealthy = false;
            result.Warnings.Add($"DB connection failed: {ex.Message}");
        }

        return result;
    }
}
