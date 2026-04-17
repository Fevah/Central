using System.Reflection;
using Central.Engine.Integration;
using Central.Engine.Services;
using Central.Persistence;

namespace Central.Api.Endpoints;

/// <summary>
/// Complete platform status API — returns full system overview.
/// Used by: monitoring dashboards, admin panels, external healthchecks.
/// </summary>
public static class StatusEndpoints
{
    private static readonly DateTime ApiStartTime = DateTime.UtcNow;

    public static RouteGroupBuilder MapStatusEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", async (DbConnectionFactory db) =>
        {
            var repo = new DbRepository(db.ConnectionString);
            var health = await StartupHealthCheck.CheckAsync(db.ConnectionString);
            var dashboard = await repo.GetDashboardDataAsync();
            var syncConfigs = await repo.GetSyncConfigsAsync();
            var apiKeys = await repo.GetApiKeysAsync();
            var idProviders = await repo.GetIdentityProvidersAsync();

            return Results.Ok(new
            {
                platform = new
                {
                    name = "Central",
                    version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0",
                    runtime = $".NET {Environment.Version}",
                    os = Environment.OSVersion.ToString(),
                    machine = Environment.MachineName,
                    uptime = (DateTime.UtcNow - ApiStartTime).ToString(@"d\.hh\:mm\:ss"),
                    started_at = ApiStartTime,
                    processors = Environment.ProcessorCount,
                    memory_mb = GC.GetTotalMemory(false) / 1024 / 1024
                },
                database = new
                {
                    status = health.IsHealthy ? "healthy" : "degraded",
                    latency_ms = health.DbLatency.TotalMilliseconds,
                    table_count = health.TableCount,
                    missing_tables = health.MissingTables,
                    warnings = health.Warnings
                },
                data = new
                {
                    devices = dashboard.DeviceCount,
                    switches = dashboard.SwitchCount,
                    users = dashboard.UserCount,
                    links = dashboard.LinkCount,
                    vlans = dashboard.VlanCount,
                    open_tasks = dashboard.OpenTasks,
                    sd_open_tickets = dashboard.SdOpenTickets,
                    sd_closed_today = dashboard.SdClosedToday
                },
                auth = new
                {
                    identity_providers = idProviders.Count,
                    active_providers = idProviders.Count(p => p.IsEnabled),
                    api_keys = apiKeys.Count,
                    active_api_keys = apiKeys.Count(k => k.IsActive),
                    auth_events_24h = dashboard.AuthEvents24h,
                    failed_logins_24h = dashboard.FailedLogins24h
                },
                sync = new
                {
                    configs = syncConfigs.Count,
                    enabled = syncConfigs.Count(c => c.IsEnabled),
                    failures = syncConfigs.Count(c => c.LastSyncStatus == "failed"),
                    agent_types = SyncEngine.Instance.GetAgentTypes(),
                    converter_types = SyncEngine.Instance.GetConverterTypes()
                },
                mediator = Central.Engine.Shell.Mediator.Instance.GetDiagnostics()
            });
        });

        return group;
    }
}
