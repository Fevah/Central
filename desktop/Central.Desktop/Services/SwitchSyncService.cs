using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Central.Data;
using Central.Core.Models;

namespace Central.Desktop.Services;

/// <summary>
/// Handles SSH-based sync operations for switches — config download, BGP sync, etc.
/// Extracted from MainWindow code-behind for testability and reuse.
/// </summary>
public class SwitchSyncService
{
    private readonly DbRepository _repo;

    public SwitchSyncService(DbRepository repo)
    {
        _repo = repo;
    }

    /// <summary>Result of a sync operation.</summary>
    public class SyncResult
    {
        public bool Success { get; init; }
        public string Message { get; init; } = "";
        public static SyncResult Ok(string msg) => new() { Success = true, Message = msg };
        public static SyncResult Fail(string msg) => new() { Success = false, Message = msg };
    }

    // ── BGP Sync ────────────────────────────────────────────────────────

    /// <summary>Sync BGP config from a single switch via SSH.</summary>
    public async Task<SyncResult> SyncBgpAsync(SwitchRecord sw)
    {
        var host = ExtractHost(sw.ManagementIp);
        if (string.IsNullOrEmpty(host))
            return SyncResult.Fail($"{sw.Hostname}: no management IP");

        var result = await SshService.DownloadConfigAsync(
            _repo, sw.Id, sw.Hostname, host,
            sw.SshPort > 0 ? sw.SshPort : 22,
            string.IsNullOrEmpty(sw.SshUsername) ? "admin" : sw.SshUsername,
            sw.SshPassword ?? "admin123");

        if (!result.Success)
            return SyncResult.Fail($"{sw.Hostname}: {result.Error}");

        var parsed = SshService.ParseBgpConfig(result.Config);
        if (string.IsNullOrEmpty(parsed.LocalAs))
            return SyncResult.Fail($"{sw.Hostname}: no BGP config found");

        await _repo.SyncBgpFromConfigAsync(
            sw.Id, parsed.LocalAs, parsed.RouterId,
            parsed.FastExternalFailover, parsed.EbgpRequiresPolicy,
            parsed.BestpathMultipathRelax, parsed.RedistributeConnected, parsed.MaxPaths,
            parsed.Neighbors, parsed.Networks);

        return SyncResult.Ok($"{sw.Hostname}: AS {parsed.LocalAs}, {parsed.Neighbors.Count} neighbors, {parsed.Networks.Count} networks");
    }

    /// <summary>Sync BGP from all switches with management IPs. Reports progress via callback.</summary>
    public async Task<(int success, int fail)> SyncAllBgpAsync(
        IEnumerable<SwitchRecord> switches,
        Action<string>? onProgress = null)
    {
        var list = switches.Where(s => !string.IsNullOrEmpty(s.ManagementIp)).ToList();
        int success = 0, fail = 0;

        foreach (var sw in list)
        {
            var result = await SyncBgpAsync(sw);
            if (result.Success)
            {
                success++;
                onProgress?.Invoke($"BGP sync: {success}/{list.Count} done…");
            }
            else
            {
                fail++;
                onProgress?.Invoke($"BGP sync: {result.Message}");
            }
        }
        return (success, fail);
    }

    // ── Full Config Sync ──────────────────────────────────────────────

    /// <summary>Result of a full config sync — includes parsed sub-results.</summary>
    public class ConfigSyncResult
    {
        public bool Success { get; init; }
        public string Message { get; init; } = "";
        public string Config { get; init; } = "";
        public SshService.SshResult? SshResult { get; init; }
        public SwitchVersion? Version { get; init; }
        public List<SwitchInterface> Interfaces { get; init; } = new();
        public int ConfigLineCount => Config.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;
    }

    /// <summary>
    /// SSH to a switch, download config + version + interfaces + optics.
    /// Saves everything to DB. Returns parsed results for UI to display.
    /// </summary>
    public async Task<ConfigSyncResult> SyncFullConfigAsync(
        SwitchRecord sw, string host, int port, string username, string password, string operatorName)
    {
        var sshResult = await SshService.DownloadConfigAsync(_repo, sw.Id, sw.Hostname, host, port, username, password);

        if (!sshResult.Success)
            return new ConfigSyncResult { Success = false, Message = $"Failed: {sshResult.Error}  (SSH Log #{sshResult.DbLogId})", SshResult = sshResult };

        if (string.IsNullOrWhiteSpace(sshResult.Config))
            return new ConfigSyncResult { Success = false, Message = $"Connected but no 'set' lines returned  (SSH Log #{sshResult.DbLogId})", SshResult = sshResult };

        // Save config
        await _repo.SaveRunningConfigAsync(sw.Id, sshResult.Config, host, operatorName);
        await _repo.AddAuditLogAsync(sw.Id, operatorName, "Config Sync", "config", null, $"{sshResult.Config.Split('\n').Length} lines from {host}", "");

        // Parse + save version
        SwitchVersion? version = null;
        if (!string.IsNullOrWhiteSpace(sshResult.VersionOutput))
        {
            try
            {
                version = SwitchVersion.Parse(sw.Id, sshResult.VersionOutput);
                await _repo.SaveSwitchVersionAsync(version);
                await _repo.AddAuditLogAsync(sw.Id, operatorName, "Version Sync", "version", null, $"{version.HardwareModel} / {version.L2L3Version}", "");
            }
            catch (Exception ex) { AppLogger.LogException("SyncConfig", ex, "SaveSwitchVersion"); }
        }

        // Parse + save interfaces + optics
        var interfaces = new List<SwitchInterface>();
        if (!string.IsNullOrWhiteSpace(sshResult.InterfacesOutput))
        {
            try
            {
                interfaces = SwitchInterface.Parse(sw.Id, sshResult.InterfacesOutput);
                SwitchInterface.MergeLldp(interfaces, sshResult.LldpOutput);

                try
                {
                    var optics = InterfaceOptics.Parse(sw.Id, sshResult.OpticsOutput);
                    if (optics.Count > 0)
                    {
                        await _repo.SaveInterfaceOpticsAsync(sw.Id, optics);
                        SwitchInterface.MergeOptics(interfaces, optics);
                        await _repo.AddAuditLogAsync(sw.Id, operatorName, "Optics Sync", "optics", null, $"{optics.Count} readings", "");
                    }
                    else
                    {
                        var latest = await _repo.GetLatestOpticsAsync(sw.Id);
                        SwitchInterface.MergeOptics(interfaces, latest);
                    }
                }
                catch (Exception ex) { AppLogger.LogException("SyncConfig", ex, "SaveInterfaceOptics"); }

                if (interfaces.Count > 0)
                {
                    await _repo.SaveSwitchInterfacesAsync(sw.Id, interfaces);
                    await _repo.AddAuditLogAsync(sw.Id, operatorName, "Interface Sync", "interfaces", null, $"{interfaces.Count} interfaces", "");
                }
            }
            catch (Exception ex) { AppLogger.LogException("SyncConfig", ex, "SaveSwitchInterfaces"); }
        }

        var parts = new List<string> { $"{sshResult.Config.Split('\n').Length} config lines" };
        if (version != null) parts.Add("version");
        if (interfaces.Count > 0) parts.Add($"{interfaces.Count} interfaces");

        return new ConfigSyncResult
        {
            Success = true,
            Message = $"Synced {string.Join(" + ", parts)}  ·  {DateTime.Now:HH:mm:ss}  (Log #{sshResult.DbLogId})",
            Config = sshResult.Config,
            SshResult = sshResult,
            Version = version,
            Interfaces = interfaces,
        };
    }

    /// <summary>Resolve SSH credentials for a switch with fallback defaults.</summary>
    public static (string host, int port, string username, string? password) ResolveCredentials(
        SwitchRecord sw, string? mgmtIp, string? defaultUser, string? defaultPass, int defaultPort)
    {
        var host = mgmtIp?.Split('/')[0] ?? "";
        var port = sw.SshPort > 0 ? sw.SshPort : defaultPort;
        var username = !string.IsNullOrWhiteSpace(sw.SshUsername) ? sw.SshUsername
                     : !string.IsNullOrWhiteSpace(defaultUser) ? defaultUser : "admin";
        var password = !string.IsNullOrWhiteSpace(sw.SshPassword) ? sw.SshPassword : defaultPass;
        return (host, port, username, password);
    }

    /// <summary>Ping candidate IPs and return the first reachable one.</summary>
    public static async Task<string?> FindReachableIpAsync(List<string> candidateIps)
    {
        foreach (var candidate in candidateIps)
        {
            try
            {
                using var ping = new System.Net.NetworkInformation.Ping();
                var reply = await ping.SendPingAsync(candidate, 3000);
                if (reply.Status == System.Net.NetworkInformation.IPStatus.Success)
                {
                    var reply2 = await ping.SendPingAsync(candidate, 3000);
                    if (reply2.Status == System.Net.NetworkInformation.IPStatus.Success)
                        return candidate;
                }
            }
            catch { /* try next */ }
        }
        return null;
    }

    // ── Helpers ─────────────────────────────────────────────────────────

    /// <summary>Extract clean IP from management_ip (strip /32, CIDR suffix).</summary>
    public static string ExtractHost(string? managementIp)
    {
        if (string.IsNullOrEmpty(managementIp)) return "";
        return managementIp.Replace("/32", "").Split('/')[0];
    }
}
