using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using WpfApp = System.Windows.Application;
using Renci.SshNet;
using Central.Core.Models;

namespace Central.Desktop.Services;

public static class PingService
{
    /// <summary>Strip CIDR suffix from IP (e.g. "10.14.152.1/32" → "10.14.152.1")</summary>
    private static string CleanIp(string ip) =>
        ip.Contains('/') ? ip.Split('/')[0] : ip;

    /// <summary>Try ping on primary IP, return true + ip if reachable.</summary>
    private static async Task<(bool ok, string ip, double? ms)> PingWithFallbackAsync(
        string managementIp, string? primaryIp)
    {
        var ips = new List<string>();
        if (!string.IsNullOrWhiteSpace(managementIp)) ips.Add(CleanIp(managementIp));
        if (!string.IsNullOrWhiteSpace(primaryIp))
        {
            var clean = CleanIp(primaryIp);
            if (!ips.Contains(clean)) ips.Add(clean);
        }

        foreach (var ip in ips)
        {
            try
            {
                using var ping = new Ping();
                var reply = await ping.SendPingAsync(ip, 3000);
                if (reply.Status == IPStatus.Success)
                    return (true, ip, (double?)reply.RoundtripTime);
            }
            catch { /* try next */ }
        }
        return (false, ips.FirstOrDefault() ?? "", null);
    }

    /// <summary>Resolve the best SSH IP: override > management > primary.</summary>
    public static string ResolveSshIp(SwitchRecord sw, string? primaryIp = null)
    {
        if (!string.IsNullOrWhiteSpace(sw.SshOverrideIp)) return CleanIp(sw.SshOverrideIp);
        if (!string.IsNullOrWhiteSpace(sw.ManagementIp)) return CleanIp(sw.ManagementIp);
        if (!string.IsNullOrWhiteSpace(primaryIp)) return CleanIp(primaryIp);
        return "";
    }

    /// <summary>Marshal a property update to the UI thread so DX grid refreshes.</summary>
    private static void OnUi(Action action)
    {
        var d = WpfApp.Current?.Dispatcher;
        if (d != null && !d.CheckAccess())
            d.Invoke(action);
        else
            action();
    }

    public static async Task PingAllAsync(IEnumerable<SwitchRecord> switches, Action<string>? onStatus = null,
        Func<string, string?>? primaryIpLookup = null)
    {
        var targets = switches.Where(s => !string.IsNullOrWhiteSpace(s.ManagementIp) ||
            (primaryIpLookup != null && !string.IsNullOrWhiteSpace(primaryIpLookup(s.Hostname)))).ToList();
        if (targets.Count == 0) return;

        onStatus?.Invoke($"Pinging {targets.Count} switches…");

        var tasks = targets.Select(async sw =>
        {
            OnUi(() => sw.IsPinging = true);
            try
            {
                var priIp = primaryIpLookup?.Invoke(sw.Hostname);
                var (ok, usedIp, ms) = await PingWithFallbackAsync(sw.ManagementIp, priIp);
                var ts = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                OnUi(() =>
                {
                    sw.LastPingOk = ok;
                    sw.LastPingMs = ms;
                    sw.LastPingAt = ts;
                });
            }
            catch
            {
                var ts = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                OnUi(() =>
                {
                    sw.LastPingOk = false;
                    sw.LastPingMs = null;
                    sw.LastPingAt = ts;
                });
            }
            finally
            {
                OnUi(() => sw.IsPinging = false);
            }
        });

        await Task.WhenAll(tasks);

        var ok = targets.Count(s => s.LastPingOk == true);
        onStatus?.Invoke($"Ping complete: {ok}/{targets.Count} reachable  ·  {DateTime.Now:HH:mm:ss}");
    }

    public static async Task PingOneAsync(SwitchRecord sw, string? primaryIp = null)
    {
        if (string.IsNullOrWhiteSpace(sw.ManagementIp) && string.IsNullOrWhiteSpace(primaryIp)) return;
        OnUi(() => sw.IsPinging = true);
        try
        {
            var (ok, usedIp, ms) = await PingWithFallbackAsync(sw.ManagementIp, primaryIp);
            var ts = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            OnUi(() =>
            {
                sw.LastPingOk = ok;
                sw.LastPingMs = ms;
                sw.LastPingAt = ts;
            });
        }
        catch
        {
            var ts = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            OnUi(() =>
            {
                sw.LastPingOk = false;
                sw.LastPingMs = null;
                sw.LastPingAt = ts;
            });
        }
        finally
        {
            OnUi(() => sw.IsPinging = false);
        }
    }

    /// <summary>
    /// Tests SSH login for all switches in parallel (connect + auth only, no commands).
    /// </summary>
    public static async Task TestSshAllAsync(IEnumerable<SwitchRecord> switches, Action<string>? onStatus = null,
        Func<string, string?>? primaryIpLookup = null,
        string? defaultUsername = null, string? defaultPassword = null, int defaultPort = 22)
    {
        var targets = switches.Where(s =>
            (!string.IsNullOrWhiteSpace(s.ManagementIp) || (primaryIpLookup != null && !string.IsNullOrWhiteSpace(primaryIpLookup(s.Hostname)))) &&
            (!string.IsNullOrWhiteSpace(s.SshPassword) || !string.IsNullOrWhiteSpace(defaultPassword))).ToList();
        if (targets.Count == 0) return;

        onStatus?.Invoke($"Testing SSH on {targets.Count} switches…");

        var tasks = targets.Select(async sw => await TestSshOneAsync(sw, primaryIpLookup?.Invoke(sw.Hostname), defaultUsername, defaultPassword, defaultPort));
        await Task.WhenAll(tasks);

        var ok = targets.Count(s => s.LastSshOk == true);
        onStatus?.Invoke($"SSH test: {ok}/{targets.Count} authenticated  ·  {DateTime.Now:HH:mm:ss}");
    }

    /// <summary>
    /// Tests SSH login on a single switch (connect + auth, disconnect immediately).
    /// </summary>
    public static async Task TestSshOneAsync(SwitchRecord sw, string? primaryIp = null,
        string? defaultUsername = null, string? defaultPassword = null, int defaultPort = 22)
    {
        var ip = ResolveSshIp(sw, primaryIp);
        var password = !string.IsNullOrWhiteSpace(sw.SshPassword) ? sw.SshPassword : defaultPassword;
        if (string.IsNullOrWhiteSpace(ip) || string.IsNullOrWhiteSpace(password)) return;

        var username = !string.IsNullOrWhiteSpace(sw.SshUsername) ? sw.SshUsername
                     : !string.IsNullOrWhiteSpace(defaultUsername) ? defaultUsername : "admin";
        var port = sw.SshPort > 0 ? sw.SshPort : defaultPort;

        // Build list of IPs to try: override > management > primary
        var ipsToTry = new List<string>();
        if (!string.IsNullOrWhiteSpace(sw.SshOverrideIp)) ipsToTry.Add(CleanIp(sw.SshOverrideIp));
        if (!string.IsNullOrWhiteSpace(sw.ManagementIp)) { var c = CleanIp(sw.ManagementIp); if (!ipsToTry.Contains(c)) ipsToTry.Add(c); }
        if (!string.IsNullOrWhiteSpace(primaryIp)) { var c = CleanIp(primaryIp); if (!ipsToTry.Contains(c)) ipsToTry.Add(c); }

        await Task.Run(() =>
        {
            foreach (var tryIp in ipsToTry)
            {
                try
                {
                    var pwAuth = new PasswordAuthenticationMethod(username, password);
                    var kbAuth = new KeyboardInteractiveAuthenticationMethod(username);
                    kbAuth.AuthenticationPrompt += (_, e) =>
                    {
                        foreach (var prompt in e.Prompts)
                            prompt.Response = password;
                    };

                    var connInfo = new ConnectionInfo(tryIp, port, username, pwAuth, kbAuth)
                    {
                        Timeout = TimeSpan.FromSeconds(10),
                    };

                    using var client = new SshClient(connInfo);
                    client.HostKeyReceived += (_, e) => { e.CanTrust = true; };
                    client.Connect();

                    var ok = client.IsConnected && client.ConnectionInfo.IsAuthenticated;
                    if (ok)
                    {
                        var ts = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                        OnUi(() => { sw.LastSshOk = true; sw.LastSshAt = ts; });
                        client.Disconnect();
                        return; // success — stop trying
                    }
                    client.Disconnect();
                }
                catch { /* try next IP */ }
            }
            // All IPs failed
            var failTs = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            OnUi(() => { sw.LastSshOk = false; sw.LastSshAt = failTs; });
        });
    }
}
