using Microsoft.AspNetCore.SignalR;
using Npgsql;
using Renci.SshNet;
using Central.Api.Hubs;
using Central.Data;

namespace Central.Api.Services;

/// <summary>
/// Server-side SSH operations. Credentials stay on the server.
/// Progress streamed to WPF clients via SignalR NotificationHub.
/// </summary>
public class SshOperationsService
{
    private readonly DbConnectionFactory _db;
    private readonly IHubContext<NotificationHub> _hub;
    private readonly ILogger<SshOperationsService> _logger;

    public SshOperationsService(DbConnectionFactory db, IHubContext<NotificationHub> hub, ILogger<SshOperationsService> logger)
    {
        _db = db;
        _hub = hub;
        _logger = logger;
    }

    // ── Ping ──────────────────────────────────────────────────────────────

    public async Task<PingResult> PingSwitchAsync(Guid switchId)
    {
        var sw = await GetSwitchInfoAsync(switchId);
        if (sw == null) return new PingResult(false, 0, "Switch not found");

        await BroadcastProgress(sw.Hostname, "Pinging...", 0);

        try
        {
            using var ping = new System.Net.NetworkInformation.Ping();
            var reply = await ping.SendPingAsync(sw.ManagementIp, 5000);
            var ok = reply.Status == System.Net.NetworkInformation.IPStatus.Success;
            var ms = ok ? (double)reply.RoundtripTime : 0;

            // Update DB
            await UpdatePingResultAsync(sw.Hostname, ok, ms);
            await BroadcastProgress(sw.Hostname, ok ? $"Reachable ({ms}ms)" : "Unreachable", 100);

            return new PingResult(ok, ms, ok ? "Success" : reply.Status.ToString());
        }
        catch (Exception ex)
        {
            await BroadcastProgress(sw.Hostname, $"Ping failed: {ex.Message}", 100);
            return new PingResult(false, 0, ex.Message);
        }
    }

    // ── Config Download ───────────────────────────────────────────────────

    public async Task<ConfigDownloadResult> DownloadConfigAsync(Guid switchId, string? operatorName = null)
    {
        var sw = await GetSwitchInfoAsync(switchId);
        if (sw == null) return new ConfigDownloadResult(false, "", "Switch not found");

        await BroadcastProgress(sw.Hostname, "Connecting via SSH...", 10);

        try
        {
            var (host, port, username, password) = ResolveCredentials(sw);

            using var client = new SshClient(host, port, username, password);
            client.ConnectionInfo.Timeout = TimeSpan.FromSeconds(30);
            await Task.Run(() => client.Connect());

            await BroadcastProgress(sw.Hostname, "Downloading configuration...", 40);

            using var cmd = client.RunCommand("show running-configuration");
            var config = cmd.Result ?? "";

            if (string.IsNullOrWhiteSpace(config))
            {
                // Try PicOS alternative
                using var cmd2 = client.RunCommand("show configuration");
                config = cmd2.Result ?? "";
            }

            client.Disconnect();

            if (string.IsNullOrWhiteSpace(config))
                return new ConfigDownloadResult(false, "", "Empty config returned");

            await BroadcastProgress(sw.Hostname, "Saving to database...", 70);

            // Get previous config for drift detection
            var previousConfig = await GetLatestRunningConfigAsync(switchId);

            // Save to running_configs table
            var lineCount = config.Split('\n').Length;
            await SaveRunningConfigAsync(switchId, config, operatorName ?? "api", lineCount);

            // Config drift detection (Phase 6.6)
            if (!string.IsNullOrEmpty(previousConfig) && previousConfig != config)
            {
                var prevLines = previousConfig.Split('\n');
                var newLines = config.Split('\n');
                var changed = prevLines.Except(newLines).Count() + newLines.Except(prevLines).Count();
                if (changed > 0)
                {
                    _logger.LogWarning("Config drift detected on {Switch}: {Changed} lines changed", sw.Hostname, changed);
                    await _hub.Clients.All.SendAsync("ConfigDrift", sw.Hostname, changed);
                }
            }

            await BroadcastProgress(sw.Hostname, $"Config downloaded ({lineCount} lines)", 100);
            await _hub.Clients.All.SendAsync("DataChanged", "running_configs", "INSERT", switchId.ToString());

            return new ConfigDownloadResult(true, config, $"{lineCount} lines");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Config download failed for {Switch}", sw.Hostname);
            await BroadcastProgress(sw.Hostname, $"Failed: {ex.Message}", 100);
            return new ConfigDownloadResult(false, "", ex.Message);
        }
    }

    // ── BGP Sync ──────────────────────────────────────────────────────────

    public async Task<BgpSyncResult> SyncBgpAsync(Guid switchId, string? operatorName = null)
    {
        var sw = await GetSwitchInfoAsync(switchId);
        if (sw == null) return new BgpSyncResult(false, "Switch not found", 0, 0);

        await BroadcastProgress(sw.Hostname, "Connecting for BGP sync...", 10);

        try
        {
            var (host, port, username, password) = ResolveCredentials(sw);

            using var client = new SshClient(host, port, username, password);
            client.ConnectionInfo.Timeout = TimeSpan.FromSeconds(30);
            await Task.Run(() => client.Connect());

            await BroadcastProgress(sw.Hostname, "Downloading BGP configuration...", 30);

            using var cmd = client.RunCommand("show running-configuration");
            var config = cmd.Result ?? "";
            client.Disconnect();

            if (string.IsNullOrWhiteSpace(config))
                return new BgpSyncResult(false, "Empty config", 0, 0);

            await BroadcastProgress(sw.Hostname, "Parsing BGP data...", 60);

            // Parse BGP from config
            var bgpLines = config.Split('\n')
                .Where(l => l.TrimStart().StartsWith("set protocols bgp"))
                .ToList();

            int neighborCount = bgpLines.Count(l => l.Contains("neighbor") && l.Contains("remote-as"));
            int networkCount = bgpLines.Count(l => l.Contains("network"));

            await BroadcastProgress(sw.Hostname, "Saving BGP data...", 80);

            // Store parsed BGP data
            await SaveBgpSyncAsync(switchId, bgpLines, operatorName ?? "api");

            await BroadcastProgress(sw.Hostname, $"BGP synced ({neighborCount} neighbors, {networkCount} networks)", 100);
            await _hub.Clients.All.SendAsync("DataChanged", "bgp_config", "UPDATE", switchId.ToString());

            return new BgpSyncResult(true, "Success", neighborCount, networkCount);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "BGP sync failed for {Switch}", sw.Hostname);
            await BroadcastProgress(sw.Hostname, $"Failed: {ex.Message}", 100);
            return new BgpSyncResult(false, ex.Message, 0, 0);
        }
    }

    // ── Batch Ping All ────────────────────────────────────────────────────

    public async Task<int> PingAllSwitchesAsync()
    {
        await using var conn = new NpgsqlConnection(_db.ConnectionString);
        await conn.OpenAsync();

        var switches = new List<(Guid Id, string Hostname, string Ip)>();
        await using var cmd = new NpgsqlCommand("SELECT id, hostname, management_ip::text FROM switches WHERE management_ip IS NOT NULL", conn);
        await using var rdr = await cmd.ExecuteReaderAsync();
        while (await rdr.ReadAsync())
            switches.Add((rdr.GetGuid(0), rdr.GetString(1), rdr.IsDBNull(2) ? "" : rdr.GetString(2)));
        await rdr.CloseAsync();

        int reachable = 0;
        var total = switches.Count;

        for (int i = 0; i < switches.Count; i++)
        {
            var (id, hostname, ip) = switches[i];
            if (string.IsNullOrEmpty(ip)) continue;

            await BroadcastProgress(hostname, $"Pinging {i + 1}/{total}...", (int)((double)i / total * 100));

            try
            {
                using var ping = new System.Net.NetworkInformation.Ping();
                var reply = await ping.SendPingAsync(ip, 3000);
                var ok = reply.Status == System.Net.NetworkInformation.IPStatus.Success;
                var ms = ok ? (double)reply.RoundtripTime : 0;
                await UpdatePingResultAsync(hostname, ok, ms);
                if (ok) reachable++;
            }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Ping batch error: {ex.Message}"); }
        }

        await _hub.Clients.All.SendAsync("DataChanged", "switches", "UPDATE", "all");
        return reachable;
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private async Task BroadcastProgress(string hostname, string status, int progress)
        => await _hub.Clients.All.SendAsync("SyncProgress", hostname, status, progress);

    private record SwitchInfo(Guid Id, string Hostname, string ManagementIp, string? SshOverrideIp, int SshPort, string SshUsername, string SshPassword);

    private async Task<SwitchInfo?> GetSwitchInfoAsync(Guid switchId)
    {
        await using var conn = new NpgsqlConnection(_db.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT id, hostname, COALESCE(management_ip::text,''), COALESCE(ssh_override_ip,''), COALESCE(ssh_port,22), COALESCE(ssh_username,'admin'), COALESCE(ssh_password,'') FROM switches WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("id", switchId);
        await using var rdr = await cmd.ExecuteReaderAsync();
        if (!await rdr.ReadAsync()) return null;

        return new SwitchInfo(
            rdr.GetGuid(0), rdr.GetString(1), rdr.GetString(2),
            rdr.IsDBNull(3) ? null : rdr.GetString(3),
            rdr.GetInt32(4), rdr.GetString(5), rdr.GetString(6));
    }

    private static (string Host, int Port, string Username, string Password) ResolveCredentials(SwitchInfo sw)
    {
        var host = !string.IsNullOrEmpty(sw.SshOverrideIp) ? sw.SshOverrideIp : sw.ManagementIp;
        // Decrypt password if encrypted at rest
        var password = Central.Core.Auth.CredentialEncryptor.IsEncrypted(sw.SshPassword)
            ? Central.Core.Auth.CredentialEncryptor.Decrypt(sw.SshPassword)
            : sw.SshPassword;
        return (host, sw.SshPort, sw.SshUsername, password);
    }

    private async Task UpdatePingResultAsync(string hostname, bool ok, double ms)
    {
        await using var conn = new NpgsqlConnection(_db.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "UPDATE switches SET last_ping_ok = @ok, last_ping_ms = @ms, last_ping_at = now() WHERE hostname = @host", conn);
        cmd.Parameters.AddWithValue("ok", ok);
        cmd.Parameters.AddWithValue("ms", (decimal)ms);
        cmd.Parameters.AddWithValue("host", hostname);
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task<string?> GetLatestRunningConfigAsync(Guid switchId)
    {
        await using var conn = new NpgsqlConnection(_db.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT config_text FROM running_configs WHERE switch_id = @sid ORDER BY downloaded_at DESC LIMIT 1", conn);
        cmd.Parameters.AddWithValue("sid", switchId);
        var result = await cmd.ExecuteScalarAsync();
        return result as string;
    }

    private async Task SaveRunningConfigAsync(Guid switchId, string config, string operatorName, int lineCount)
    {
        await using var conn = new NpgsqlConnection(_db.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "INSERT INTO running_configs (switch_id, config_text, downloaded_by, line_count) VALUES (@sid, @cfg, @op, @lines)", conn);
        cmd.Parameters.AddWithValue("sid", switchId);
        cmd.Parameters.AddWithValue("cfg", config);
        cmd.Parameters.AddWithValue("op", operatorName);
        cmd.Parameters.AddWithValue("lines", lineCount);
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task SaveBgpSyncAsync(Guid switchId, List<string> bgpLines, string operatorName)
    {
        // Parse and update bgp_config table
        await using var conn = new NpgsqlConnection(_db.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "UPDATE bgp_config SET last_synced = now() WHERE switch_id = @sid", conn);
        cmd.Parameters.AddWithValue("sid", switchId);
        await cmd.ExecuteNonQueryAsync();
    }
}

public record PingResult(bool Ok, double LatencyMs, string Message);
public record ConfigDownloadResult(bool Ok, string Config, string Message);
public record BgpSyncResult(bool Ok, string Message, int NeighborCount, int NetworkCount);
