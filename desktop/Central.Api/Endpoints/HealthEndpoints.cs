using System.Diagnostics;
using System.Reflection;
using Npgsql;
using Central.Data;

namespace Central.Api.Endpoints;

/// <summary>
/// Health check endpoints for monitoring (Prometheus, Grafana, uptime bots).
/// /api/health — simple alive check
/// /api/health/detailed — full system status with DB, SignalR, uptime, version
/// </summary>
public static class HealthEndpoints
{
    private static readonly DateTime StartTime = DateTime.UtcNow;

    public static RouteGroupBuilder MapHealthEndpoints(this RouteGroupBuilder group)
    {
        // Simple health check (no auth — monitoring tools need this)
        group.MapGet("/", () => Results.Ok(new
        {
            status = "healthy",
            timestamp = DateTime.UtcNow,
            uptime = (DateTime.UtcNow - StartTime).ToString(@"d\.hh\:mm\:ss")
        }));

        // Readiness probe — checks DB connectivity (for k8s/container orchestrators)
        group.MapGet("/ready", async (DbConnectionFactory db) =>
        {
            try
            {
                await using var conn = new NpgsqlConnection(db.ConnectionString);
                await conn.OpenAsync();
                await using var cmd = new NpgsqlCommand("SELECT 1", conn);
                await cmd.ExecuteScalarAsync();
                return Results.Ok(new { status = "ready" });
            }
            catch
            {
                return Results.StatusCode(503); // Service Unavailable
            }
        });

        // Liveness probe — always returns OK if process is running
        group.MapGet("/live", () => Results.Ok(new { status = "alive" }));

        // Detailed health (requires auth)
        group.MapGet("/detailed", async (DbConnectionFactory db) =>
        {
            var checks = new Dictionary<string, object>();

            // Database
            try
            {
                var sw = Stopwatch.StartNew();
                await using var conn = new NpgsqlConnection(db.ConnectionString);
                await conn.OpenAsync();
                await using var cmd = new NpgsqlCommand("SELECT 1", conn);
                await cmd.ExecuteScalarAsync();
                checks["database"] = new { status = "healthy", latency_ms = sw.ElapsedMilliseconds };
            }
            catch (Exception ex)
            {
                checks["database"] = new { status = "unhealthy", error = ex.Message };
            }

            // Table counts (quick check)
            try
            {
                await using var conn = new NpgsqlConnection(db.ConnectionString);
                await conn.OpenAsync();
                var counts = new Dictionary<string, long>();
                foreach (var table in new[] { "app_users", "switch_guide", "switches", "sd_requests", "sync_configs", "identity_providers" })
                {
                    try
                    {
                        await using var cmd = new NpgsqlCommand($"SELECT COUNT(*) FROM {table}", conn);
                        counts[table] = (long)(await cmd.ExecuteScalarAsync())!;
                    }
                    catch { counts[table] = -1; }
                }
                checks["data"] = counts;
            }
            catch { }

            // System info
            checks["system"] = new
            {
                version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0",
                runtime = Environment.Version.ToString(),
                os = Environment.OSVersion.ToString(),
                machine = Environment.MachineName,
                processors = Environment.ProcessorCount,
                memory_mb = GC.GetTotalMemory(false) / 1024 / 1024,
                uptime = (DateTime.UtcNow - StartTime).ToString(@"d\.hh\:mm\:ss"),
                started_at = StartTime
            };

            // Sync engine
            var syncEngine = Central.Core.Integration.SyncEngine.Instance;
            checks["sync_engine"] = new
            {
                agent_types = syncEngine.GetAgentTypes(),
                converter_types = syncEngine.GetConverterTypes()
            };

            // Mediator diagnostics
            checks["mediator"] = Central.Core.Shell.Mediator.Instance.GetDiagnostics();

            var overallStatus = checks.Values
                .OfType<object>()
                .All(c => c.ToString()?.Contains("unhealthy") != true) ? "healthy" : "degraded";

            return Results.Ok(new { status = overallStatus, checks });
        });

        return group;
    }
}
