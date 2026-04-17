using Central.Core.Models;

namespace Central.Desktop.Services;

/// <summary>
/// Mode-aware SSH proxy. Routes SSH operations to either:
/// - Local SSH (direct Renci.SshNet from desktop) — standalone mode
/// - API SSH (REST call to server) — multi-user mode
///
/// Checks App.Settings "api.use_server_ssh" to determine mode.
/// </summary>
public static class SshProxy
{
    /// <summary>True if SSH should go through the API server.</summary>
    public static bool UseServerSsh =>
        App.Settings?.Get<bool>("api.use_server_ssh") == true
        && !string.IsNullOrEmpty(App.Settings?.Get<string>("api.url"));

    /// <summary>Ping a switch — routes to local or API.</summary>
    public static async Task<(bool Ok, double Ms, string Message)> PingAsync(SwitchRecord sw)
    {
        if (UseServerSsh && sw.Id != Guid.Empty)
        {
            try
            {
                var client = CreateApiClient();
                var result = await client.PingSwitchAsync(sw.Id);
                if (result.HasValue)
                {
                    var ok = result.Value.GetProperty("ok").GetBoolean();
                    var ms = result.Value.GetProperty("latencyMs").GetDouble();
                    var msg = result.Value.GetProperty("message").GetString() ?? "";
                    return (ok, ms, msg);
                }
            }
            catch (Exception ex)
            {
                return (false, 0, $"API error: {ex.Message}");
            }
        }

        // Fallback to local ping
        try
        {
            using var ping = new System.Net.NetworkInformation.Ping();
            var ip = !string.IsNullOrEmpty(sw.SshOverrideIp) ? sw.SshOverrideIp : sw.ManagementIp;
            if (string.IsNullOrEmpty(ip)) return (false, 0, "No management IP");
            var reply = await ping.SendPingAsync(ip, 5000);
            var ok = reply.Status == System.Net.NetworkInformation.IPStatus.Success;
            return (ok, ok ? reply.RoundtripTime : 0, ok ? "Success" : reply.Status.ToString());
        }
        catch (Exception ex) { return (false, 0, ex.Message); }
    }

    /// <summary>Download running config — routes to local or API.</summary>
    public static async Task<(bool Ok, string Config, string Message)> DownloadConfigAsync(SwitchRecord sw)
    {
        if (UseServerSsh && sw.Id != Guid.Empty)
        {
            try
            {
                var client = CreateApiClient();
                var result = await client.DownloadConfigAsync(sw.Id);
                if (result.HasValue)
                {
                    var ok = result.Value.GetProperty("ok").GetBoolean();
                    var config = result.Value.TryGetProperty("config", out var c) ? c.GetString() ?? "" : "";
                    var msg = result.Value.GetProperty("message").GetString() ?? "";
                    return (ok, config, msg);
                }
            }
            catch (Exception ex) { return (false, "", $"API error: {ex.Message}"); }
        }

        // Fallback to local SSH via SshService
        var sshResult = await SshService.DownloadConfigAsync(
            null, sw.Id, sw.Hostname,
            !string.IsNullOrEmpty(sw.SshOverrideIp) ? sw.SshOverrideIp : sw.ManagementIp,
            sw.SshPort > 0 ? sw.SshPort : 22,
            !string.IsNullOrEmpty(sw.SshUsername) ? sw.SshUsername : "admin",
            sw.SshPassword ?? "");
        return (sshResult.Success, sshResult.Config, sshResult.Error ?? "");
    }

    /// <summary>Sync BGP config — routes to local or API.</summary>
    public static async Task<(bool Ok, string Message)> SyncBgpAsync(SwitchRecord sw)
    {
        if (UseServerSsh && sw.Id != Guid.Empty)
        {
            try
            {
                var client = CreateApiClient();
                var result = await client.SyncBgpAsync(sw.Id);
                if (result.HasValue)
                {
                    var ok = result.Value.GetProperty("ok").GetBoolean();
                    var msg = result.Value.GetProperty("message").GetString() ?? "";
                    return (ok, msg);
                }
            }
            catch (Exception ex) { return (false, $"API error: {ex.Message}"); }
        }

        return (false, "Local BGP sync not implemented in proxy — use direct SwitchSyncService");
    }

    /// <summary>Deploy config lines to a switch — routes to local or API.</summary>
    public static async Task<(bool Ok, string Error)> DeployConfigAsync(SwitchRecord sw, string config)
    {
        // Deploy always uses local SSH for now — API deploy endpoint would need to accept arbitrary commands
        // which is a security concern. Local deploy is intentional.
        try
        {
            var host = !string.IsNullOrEmpty(sw.SshOverrideIp) ? sw.SshOverrideIp : sw.ManagementIp;
            if (string.IsNullOrEmpty(host)) return (false, "No management IP");

            using var client = new Renci.SshNet.SshClient(
                host, sw.SshPort > 0 ? sw.SshPort : 22,
                !string.IsNullOrEmpty(sw.SshUsername) ? sw.SshUsername : "admin",
                sw.SshPassword ?? "");
            client.ConnectionInfo.Timeout = TimeSpan.FromSeconds(30);
            await Task.Run(() => client.Connect());

            foreach (var line in config.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (line.StartsWith("#") || line.StartsWith("!")) continue;
                using var cmd = client.RunCommand(line);
            }

            using var save = client.RunCommand("write memory");
            client.Disconnect();
            return (true, "");
        }
        catch (Exception ex) { return (false, ex.Message); }
    }

    private static Central.Api.Client.CentralApiClient CreateApiClient()
    {
        var apiUrl = App.Settings?.Get<string>("api.url") ?? "http://192.168.56.203:8000";
        var client = new Central.Api.Client.CentralApiClient(apiUrl);
        // Auto-login
        var username = Central.Core.Auth.AuthContext.Instance.CurrentUser?.Username ?? Environment.UserName;
        client.LoginAsync(username).Wait(5000);
        return client;
    }
}
