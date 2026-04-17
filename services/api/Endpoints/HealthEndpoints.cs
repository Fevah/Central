using System.Diagnostics;
using System.Reflection;
using Npgsql;
using Central.Data;

namespace Central.Api.Endpoints;

/// <summary>
/// Health check endpoints for monitoring (Prometheus, K8s, Grafana, uptime bots).
///
/// /api/health      — simple alive (anonymous)
/// /api/health/live — liveness probe (K8s — is the process alive?)
/// /api/health/ready — readiness probe (K8s — can it serve traffic?)
/// /api/health/detailed — full system status with all dependency checks
/// /api/health/metrics — Prometheus-compatible text metrics
/// </summary>
public static class HealthEndpoints
{
    private static readonly DateTime StartTime = DateTime.UtcNow;
    private static readonly string Version =
        Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";

    public static RouteGroupBuilder MapHealthEndpoints(this RouteGroupBuilder group)
    {
        // Simple health check (anonymous — monitoring tools)
        group.MapGet("/", () => Results.Ok(new
        {
            status = "healthy",
            version = Version,
            timestamp = DateTime.UtcNow,
            uptime = (DateTime.UtcNow - StartTime).ToString(@"d\.hh\:mm\:ss")
        }));

        // Liveness probe — returns OK if process is running (K8s restarts if this fails)
        group.MapGet("/live", () => Results.Ok(new
        {
            status = "alive",
            timestamp = DateTime.UtcNow
        }));

        // Readiness probe — checks all critical dependencies (K8s removes from LB if this fails)
        group.MapGet("/ready", async (DbConnectionFactory db) =>
        {
            var checks = new Dictionary<string, DependencyCheck>();

            // PostgreSQL
            checks["database"] = await CheckDatabaseAsync(db);

            // Determine overall status
            var allHealthy = checks.Values.All(c => c.Status == "healthy");
            var statusCode = allHealthy ? 200 : 503;

            return Results.Json(new
            {
                status = allHealthy ? "ready" : "not_ready",
                timestamp = DateTime.UtcNow,
                checks
            }, statusCode: statusCode);
        });

        // Detailed health (all dependencies + system info)
        group.MapGet("/detailed", async (DbConnectionFactory db) =>
        {
            var checks = new Dictionary<string, object>();

            // Database
            var dbCheck = await CheckDatabaseAsync(db);
            checks["database"] = dbCheck;

            // Table counts (quick data integrity check)
            try
            {
                await using var conn = new NpgsqlConnection(db.ConnectionString);
                await conn.OpenAsync();
                var counts = new Dictionary<string, long>();
                foreach (var table in new[] { "app_users", "switch_guide", "switches", "tasks", "sd_requests" })
                {
                    try
                    {
                        await using var cmd = new NpgsqlCommand($"SELECT COUNT(*) FROM {table}", conn);
                        counts[table] = (long)(await cmd.ExecuteScalarAsync())!;
                    }
                    catch { counts[table] = -1; }
                }
                checks["data_counts"] = counts;
            }
            catch { }

            // Memory / GC
            var gcInfo = GC.GetGCMemoryInfo();
            checks["memory"] = new
            {
                heap_mb = GC.GetTotalMemory(false) / 1024 / 1024,
                total_available_mb = gcInfo.TotalAvailableMemoryBytes / 1024 / 1024,
                gc_gen0 = GC.CollectionCount(0),
                gc_gen1 = GC.CollectionCount(1),
                gc_gen2 = GC.CollectionCount(2)
            };

            // System info
            checks["system"] = new
            {
                version = Version,
                runtime = Environment.Version.ToString(),
                os = Environment.OSVersion.ToString(),
                machine = Environment.MachineName,
                processors = Environment.ProcessorCount,
                uptime = (DateTime.UtcNow - StartTime).ToString(@"d\.hh\:mm\:ss"),
                started_at = StartTime
            };

            // Sync engine
            try
            {
                var syncEngine = Central.Core.Integration.SyncEngine.Instance;
                checks["sync_engine"] = new
                {
                    agent_types = syncEngine.GetAgentTypes(),
                    converter_types = syncEngine.GetConverterTypes()
                };
            }
            catch { }

            // Mediator diagnostics
            try
            {
                checks["mediator"] = Central.Core.Shell.Mediator.Instance.GetDiagnostics();
            }
            catch { }

            var overallHealthy = dbCheck.Status == "healthy";

            return Results.Ok(new
            {
                status = overallHealthy ? "healthy" : "degraded",
                timestamp = DateTime.UtcNow,
                checks
            });
        });

        // Prometheus-compatible text metrics
        group.MapGet("/metrics", async (DbConnectionFactory db) =>
        {
            var sb = new System.Text.StringBuilder();
            var uptime = (DateTime.UtcNow - StartTime).TotalSeconds;

            sb.AppendLine("# HELP central_up Whether the Central API is up (1=up, 0=down)");
            sb.AppendLine("# TYPE central_up gauge");
            sb.AppendLine("central_up 1");

            sb.AppendLine("# HELP central_uptime_seconds Time since API started");
            sb.AppendLine("# TYPE central_uptime_seconds gauge");
            sb.AppendLine($"central_uptime_seconds {uptime:F0}");

            sb.AppendLine("# HELP central_memory_bytes Managed heap memory in bytes");
            sb.AppendLine("# TYPE central_memory_bytes gauge");
            sb.AppendLine($"central_memory_bytes {GC.GetTotalMemory(false)}");

            sb.AppendLine("# HELP central_gc_collections_total GC collection count by generation");
            sb.AppendLine("# TYPE central_gc_collections_total counter");
            sb.AppendLine($"central_gc_collections_total{{generation=\"0\"}} {GC.CollectionCount(0)}");
            sb.AppendLine($"central_gc_collections_total{{generation=\"1\"}} {GC.CollectionCount(1)}");
            sb.AppendLine($"central_gc_collections_total{{generation=\"2\"}} {GC.CollectionCount(2)}");

            // DB connectivity
            try
            {
                var sw = Stopwatch.StartNew();
                await using var conn = new NpgsqlConnection(db.ConnectionString);
                await conn.OpenAsync();
                await using var cmd = new NpgsqlCommand("SELECT 1", conn);
                await cmd.ExecuteScalarAsync();
                sw.Stop();

                sb.AppendLine("# HELP central_db_up Database connectivity (1=connected, 0=down)");
                sb.AppendLine("# TYPE central_db_up gauge");
                sb.AppendLine("central_db_up 1");
                sb.AppendLine("# HELP central_db_latency_ms Database query latency in milliseconds");
                sb.AppendLine("# TYPE central_db_latency_ms gauge");
                sb.AppendLine($"central_db_latency_ms {sw.ElapsedMilliseconds}");

                // Table row counts
                sb.AppendLine("# HELP central_table_rows Row count per table");
                sb.AppendLine("# TYPE central_table_rows gauge");
                foreach (var table in new[] { "app_users", "switch_guide", "switches", "tasks" })
                {
                    try
                    {
                        await using var countCmd = new NpgsqlCommand($"SELECT COUNT(*) FROM {table}", conn);
                        var count = (long)(await countCmd.ExecuteScalarAsync())!;
                        sb.AppendLine($"central_table_rows{{table=\"{table}\"}} {count}");
                    }
                    catch { }
                }

                // Per-tenant metrics for sharding alerts
                try
                {
                    await using var tenantCmd = new NpgsqlCommand(
                        @"SELECT t.id::text, t.slug, t.sizing_model,
                                 COALESCE(s.storage_bytes, 0) AS storage_bytes,
                                 COALESCE(s.active_users, 0) AS active_users
                          FROM central_platform.tenants t
                          LEFT JOIN central_platform.tenant_shard_config s ON s.tenant_id = t.id
                          WHERE t.is_active = true", conn);
                    await using var tr = await tenantCmd.ExecuteReaderAsync();
                    sb.AppendLine("# HELP central_tenant_storage_bytes Storage used by each tenant");
                    sb.AppendLine("# TYPE central_tenant_storage_bytes gauge");
                    sb.AppendLine("# HELP central_tenant_active_users Active user count per tenant");
                    sb.AppendLine("# TYPE central_tenant_active_users gauge");
                    sb.AppendLine("# HELP central_tenant_sizing Sizing model per tenant (0=zoned, 1=dedicated)");
                    sb.AppendLine("# TYPE central_tenant_sizing gauge");
                    while (await tr.ReadAsync())
                    {
                        var tid = tr.GetString(0);
                        var slug = tr.GetString(1);
                        var sizing = tr.GetString(2);
                        var bytes = tr.GetInt64(3);
                        var users = tr.GetInt32(4);
                        sb.AppendLine($"central_tenant_storage_bytes{{tenant_id=\"{tid}\",slug=\"{slug}\"}} {bytes}");
                        sb.AppendLine($"central_tenant_active_users{{tenant_id=\"{tid}\",slug=\"{slug}\"}} {users}");
                        sb.AppendLine($"central_tenant_sizing{{tenant_id=\"{tid}\",slug=\"{slug}\"}} {(sizing == "dedicated" ? 1 : 0)}");
                    }
                }
                catch { /* platform schema may not exist yet */ }
            }
            catch
            {
                sb.AppendLine("# HELP central_db_up Database connectivity (1=connected, 0=down)");
                sb.AppendLine("# TYPE central_db_up gauge");
                sb.AppendLine("central_db_up 0");
            }

            return Results.Text(sb.ToString(), "text/plain; version=0.0.4; charset=utf-8");
        });

        return group;
    }

    private static async Task<DependencyCheck> CheckDatabaseAsync(DbConnectionFactory db)
    {
        try
        {
            var sw = Stopwatch.StartNew();
            await using var conn = new NpgsqlConnection(db.ConnectionString);
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand("SELECT 1", conn);
            await cmd.ExecuteScalarAsync();
            sw.Stop();
            return new DependencyCheck("healthy", sw.ElapsedMilliseconds, null);
        }
        catch (Exception ex)
        {
            return new DependencyCheck("unhealthy", null, ex.Message);
        }
    }

    private record DependencyCheck(string Status, long? LatencyMs, string? Error);
}
