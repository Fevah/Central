using Microsoft.AspNetCore.SignalR;
using Npgsql;
using Central.Api.Hubs;
using Central.Persistence;

namespace Central.Api.Services;

/// <summary>
/// Background service that checks job_schedules table every 30s and dispatches due jobs.
/// Each job type is handled by SshOperationsService. Results logged to job_history.
/// SignalR broadcasts progress to all connected clients.
/// </summary>
public class JobSchedulerService : BackgroundService
{
    private readonly DbConnectionFactory _db;
    private readonly SshOperationsService _ssh;
    private readonly IHubContext<NotificationHub> _hub;
    private readonly ILogger<JobSchedulerService> _logger;

    public JobSchedulerService(DbConnectionFactory db, SshOperationsService ssh, IHubContext<NotificationHub> hub, ILogger<JobSchedulerService> logger)
    {
        _db = db;
        _ssh = ssh;
        _hub = hub;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        // Wait for app to fully start
        await Task.Delay(10_000, ct);
        _logger.LogInformation("JobScheduler: started, checking every 30s");

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await CheckAndRunDueJobsAsync(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "JobScheduler: error in check loop");
            }

            await Task.Delay(30_000, ct);
        }
    }

    private async Task CheckAndRunDueJobsAsync(CancellationToken ct)
    {
        await using var conn = new NpgsqlConnection(_db.ConnectionString);
        await conn.OpenAsync(ct);

        // Find enabled jobs that are due (interval-based OR cron-based)
        await using var cmd = new NpgsqlCommand(@"
            SELECT id, job_type, name, interval_minutes, COALESCE(schedule_cron, '') as schedule_cron
            FROM job_schedules
            WHERE is_enabled = true
              AND (next_run_at IS NULL OR next_run_at <= now())
            ORDER BY next_run_at
            LIMIT 5", conn);

        var dueJobs = new List<(int Id, string Type, string Name, int IntervalMin, string Cron)>();
        await using var rdr = await cmd.ExecuteReaderAsync(ct);
        while (await rdr.ReadAsync(ct))
            dueJobs.Add((rdr.GetInt32(0), rdr.GetString(1), rdr.GetString(2), rdr.GetInt32(3), rdr.GetString(4)));
        await rdr.CloseAsync();

        // Filter cron-based jobs: only run if the current minute matches the cron expression
        var now = DateTime.Now;
        dueJobs = dueJobs.Where(j =>
        {
            if (string.IsNullOrEmpty(j.Cron)) return true; // interval-based — already filtered by SQL
            return Central.Engine.Services.CronExpression.TryParse(j.Cron, out var cron) && cron!.Matches(now);
        }).ToList();

        foreach (var job in dueJobs)
        {
            if (ct.IsCancellationRequested) break;

            _logger.LogInformation("JobScheduler: running {JobType} '{Name}'", job.Type, job.Name);

            // Update next_run_at immediately to prevent re-trigger
            DateTime nextRun;
            if (!string.IsNullOrEmpty(job.Cron) && Central.Engine.Services.CronExpression.TryParse(job.Cron, out var cronExpr))
            {
                nextRun = cronExpr!.GetNextOccurrence(DateTime.Now) ?? DateTime.Now.AddMinutes(job.IntervalMin);
            }
            else
            {
                nextRun = DateTime.Now.AddMinutes(job.IntervalMin);
            }
            await using var updateCmd = new NpgsqlCommand(
                "UPDATE job_schedules SET last_run_at = now(), next_run_at = @next WHERE id = @id", conn);
            updateCmd.Parameters.AddWithValue("id", job.Id);
            updateCmd.Parameters.AddWithValue("next", nextRun);
            await updateCmd.ExecuteNonQueryAsync(ct);

            // Create history entry
            var historyId = await CreateHistoryEntryAsync(conn, job.Id, job.Type);

            try
            {
                var result = await RunJobAsync(job.Type, ct);
                await CompleteHistoryEntryAsync(conn, historyId, "Success", result.Summary, result.Total, result.Succeeded, result.Failed, "");

                await _hub.Clients.All.SendAsync("DataChanged", "job_history", "INSERT", historyId.ToString(), ct);
            }
            catch (Exception ex)
            {
                await CompleteHistoryEntryAsync(conn, historyId, "Failed", "", 0, 0, 0, ex.Message);
                _logger.LogWarning(ex, "JobScheduler: {JobType} failed", job.Type);
            }
        }
    }

    private async Task<JobResult> RunJobAsync(string jobType, CancellationToken ct)
    {
        return jobType switch
        {
            "ping_scan" => await RunPingScanAsync(),
            "config_backup" => await RunConfigBackupAsync(),
            "bgp_sync" => await RunBgpSyncAsync(),
            _ => new JobResult("Unknown job type", 0, 0, 0)
        };
    }

    private async Task<JobResult> RunPingScanAsync()
    {
        var reachable = await _ssh.PingAllSwitchesAsync();
        return new JobResult($"{reachable} switches reachable", reachable, reachable, 0);
    }

    private async Task<JobResult> RunConfigBackupAsync()
    {
        // Get all switches with management IPs
        await using var conn = new NpgsqlConnection(_db.ConnectionString);
        await conn.OpenAsync();
        var switches = new List<Guid>();
        await using var cmd = new NpgsqlCommand("SELECT id FROM switches WHERE management_ip IS NOT NULL AND last_ping_ok = true", conn);
        await using var rdr = await cmd.ExecuteReaderAsync();
        while (await rdr.ReadAsync()) switches.Add(rdr.GetGuid(0));
        await rdr.CloseAsync();

        int succeeded = 0, failed = 0;
        foreach (var sid in switches)
        {
            var result = await _ssh.DownloadConfigAsync(sid, "scheduler");
            if (result.Ok) succeeded++; else failed++;
        }
        return new JobResult($"{succeeded}/{switches.Count} configs backed up", switches.Count, succeeded, failed);
    }

    private async Task<JobResult> RunBgpSyncAsync()
    {
        await using var conn = new NpgsqlConnection(_db.ConnectionString);
        await conn.OpenAsync();
        var switches = new List<Guid>();
        await using var cmd = new NpgsqlCommand("SELECT DISTINCT switch_id FROM bgp_config", conn);
        await using var rdr = await cmd.ExecuteReaderAsync();
        while (await rdr.ReadAsync()) switches.Add(rdr.GetGuid(0));
        await rdr.CloseAsync();

        int succeeded = 0, failed = 0;
        foreach (var sid in switches)
        {
            var result = await _ssh.SyncBgpAsync(sid, "scheduler");
            if (result.Ok) succeeded++; else failed++;
        }
        return new JobResult($"{succeeded}/{switches.Count} BGP synced", switches.Count, succeeded, failed);
    }

    private async Task<int> CreateHistoryEntryAsync(NpgsqlConnection conn, int scheduleId, string jobType)
    {
        await using var cmd = new NpgsqlCommand(
            "INSERT INTO job_history (schedule_id, job_type, triggered_by) VALUES (@sid, @type, 'scheduler') RETURNING id", conn);
        cmd.Parameters.AddWithValue("sid", scheduleId);
        cmd.Parameters.AddWithValue("type", jobType);
        return (int)(await cmd.ExecuteScalarAsync())!;
    }

    private async Task CompleteHistoryEntryAsync(NpgsqlConnection conn, int historyId, string status, string summary, int total, int succeeded, int failed, string error)
    {
        await using var cmd = new NpgsqlCommand(@"
            UPDATE job_history SET completed_at = now(), status = @status, result_summary = @summary,
                items_total = @total, items_succeeded = @ok, items_failed = @fail, error_message = @err
            WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("id", historyId);
        cmd.Parameters.AddWithValue("status", status);
        cmd.Parameters.AddWithValue("summary", summary);
        cmd.Parameters.AddWithValue("total", total);
        cmd.Parameters.AddWithValue("ok", succeeded);
        cmd.Parameters.AddWithValue("fail", failed);
        cmd.Parameters.AddWithValue("err", error);
        await cmd.ExecuteNonQueryAsync();
    }

    private record JobResult(string Summary, int Total, int Succeeded, int Failed);
}
