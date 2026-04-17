using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Renci.SshNet;
using Renci.SshNet.Common;

namespace Central.Desktop.Services;

/// <summary>
/// SSH to PicOS switches and download running config.
/// PicOS does NOT support exec channel — only interactive shell works.
/// Sequence: connect → shell → "terminal length 0" → "conf" → "sh | display set"
/// All connection steps are logged to the ssh_logs DB table via DbRepository.
/// </summary>
public static class SshService
{
    public class SshResult
    {
        public bool Success { get; set; }
        public string Config { get; set; } = "";
        public string RawOutput { get; set; } = "";
        public string Error { get; set; } = "";
        public string LogEntries { get; set; } = "";
        public int DbLogId { get; set; }
        public string VersionOutput { get; set; } = "";
        public string InterfacesOutput { get; set; } = "";
        public string LldpOutput { get; set; } = "";
        public string OpticsOutput { get; set; } = "";
    }

    public static async Task<SshResult> DownloadConfigAsync(
        Data.DbRepository repo,
        Guid? switchId,
        string hostname,
        string host,
        int port,
        string username,
        string password,
        int timeoutMs = 30000)
    {
        var log = new StringBuilder();
        var result = new SshResult();

        void Log(string msg)
        {
            log.AppendLine($"[{DateTime.Now:HH:mm:ss.fff}] {msg}");
        }

        // Insert initial log row
        int logId = 0;
        try
        {
            logId = await repo.InsertSshLogAsync(switchId, hostname, host, username, port);
            result.DbLogId = logId;
        }
        catch (Exception ex)
        {
            Log($"WARNING: Could not create DB log entry: {ex.Message}");
        }

        async Task FinalizeLog()
        {
            result.LogEntries = log.ToString();
            if (logId > 0)
            {
                try
                {
                    var configLines = result.Success ? result.Config.Split('\n').Length : 0;
                    await repo.UpdateSshLogAsync(logId, result.Success, result.Error,
                        result.RawOutput, configLines, result.LogEntries);
                }
                catch { /* don't fail the operation because of logging */ }
            }
        }

        Log($"=== SSH to {username}@{host}:{port} (switch={hostname}, timeout={timeoutMs}ms) ===");

        if (string.IsNullOrWhiteSpace(host))
        {
            result.Error = "Host IP is empty";
            Log($"ABORT: {result.Error}");
            await FinalizeLog();
            return result;
        }
        if (string.IsNullOrWhiteSpace(password))
        {
            result.Error = "Password is empty";
            Log($"ABORT: {result.Error}");
            await FinalizeLog();
            return result;
        }

        await Task.Run(async () =>
        {
            // Build connection — password + keyboard-interactive
            ConnectionInfo connInfo;
            try
            {
                var pwAuth = new PasswordAuthenticationMethod(username, password);
                var kbAuth = new KeyboardInteractiveAuthenticationMethod(username);
                kbAuth.AuthenticationPrompt += (sender, e) =>
                {
                    foreach (var prompt in e.Prompts)
                    {
                        Log($"  Keyboard-interactive prompt: '{prompt.Request}'");
                        prompt.Response = password;
                    }
                };

                connInfo = new ConnectionInfo(host, port, username, pwAuth, kbAuth)
                {
                    Timeout = TimeSpan.FromMilliseconds(timeoutMs),
                    RetryAttempts = 1,
                };
                Log("ConnectionInfo created (password + keyboard-interactive)");
            }
            catch (Exception ex)
            {
                result.Error = $"Failed to build connection: {ex.Message}";
                Log($"ERROR: {result.Error}");
                return;
            }

            SshClient? client = null;
            try
            {
                client = new SshClient(connInfo);
                client.HostKeyReceived += (sender, e) =>
                {
                    var fp = BitConverter.ToString(e.FingerPrint).Replace("-", ":");
                    Log($"  HostKey: {e.HostKeyName} fingerprint={fp}");
                    e.CanTrust = true; // Auto-accept like PuTTY "Accept" button
                };

                Log("Connecting...");
                client.Connect();
                Log($"Connected={client.IsConnected}  Authenticated={client.ConnectionInfo.IsAuthenticated}");

                if (!client.IsConnected || !client.ConnectionInfo.IsAuthenticated)
                {
                    result.Error = "Connect() completed but not authenticated";
                    Log(result.Error);
                    return;
                }
            }
            catch (SshAuthenticationException ex)
            {
                result.Error = $"Auth failed: {ex.Message} ({username}@{host})";
                Log($"AUTH ERROR: {result.Error}");
                return;
            }
            catch (SshConnectionException ex)
            {
                result.Error = $"Connection error: {ex.Message} ({host}:{port})";
                Log($"CONN ERROR: {result.Error}");
                return;
            }
            catch (System.Net.Sockets.SocketException ex)
            {
                result.Error = $"Unreachable: {ex.SocketErrorCode} - {ex.Message} ({host}:{port})";
                Log($"SOCKET ERROR: {result.Error}");
                return;
            }
            catch (TimeoutException ex)
            {
                result.Error = $"Timeout: {ex.Message} ({host}:{port})";
                Log($"TIMEOUT: {result.Error}");
                return;
            }
            catch (Exception ex)
            {
                result.Error = $"{ex.GetType().Name}: {ex.Message}";
                Log($"ERROR: {result.Error}\n  Stack: {ex.StackTrace}");
                return;
            }

            // PicOS only supports interactive shell — no exec channel
            try
            {
                Log("Creating shell stream...");
                using var shell = client.CreateShellStream("xterm", 250, 50, 800, 600, 65536);
                Log("ShellStream created, waiting for initial prompt...");

                // Wait for login banner + prompt (e.g. "admin@MEP-94-CORE01>")
                var banner = WaitForPrompt(shell, 8000);
                Log($"  Banner ({banner.Length}c): {Trunc(Clean(banner), 500)}");
                result.RawOutput += $"[banner]\n{banner}\n";

                // Disable paging so full config comes through without --More--
                Log("  Send: terminal length 0");
                shell.WriteLine("terminal length 0");
                var pagingResp = WaitForPrompt(shell, 3000);
                Log($"  paging resp: {Trunc(Clean(pagingResp), 200)}");

                // Also try PicOS-specific paging disable
                Log("  Send: set cli screen-length 0");
                shell.WriteLine("set cli screen-length 0");
                var paging2 = WaitForPrompt(shell, 3000);
                Log($"  paging2 resp: {Trunc(Clean(paging2), 200)}");

                // Enter configuration mode: "admin@switch>" → "admin@switch#"
                Log("  Send: conf");
                shell.WriteLine("conf");
                var confResp = WaitForPrompt(shell, 5000);
                Log($"  conf resp: {Trunc(Clean(confResp), 200)}");
                result.RawOutput += $"[conf]\n{confResp}\n";

                // Check we're in config mode (prompt should end with #)
                var cleanConf = Clean(confResp).TrimEnd();
                if (cleanConf.EndsWith("#"))
                    Log("  Confirmed: in configuration mode (#)");
                else
                    Log($"  WARNING: prompt doesn't end with #, may not be in config mode");

                // Get config — try commands in order
                string[] configCmds = { "sh | display set", "show | display set", "show running-config" };
                foreach (var cmd in configCmds)
                {
                    Log($"  Send: {cmd}");
                    shell.WriteLine(cmd);

                    // Config output can be large — use longer timeout and bigger read
                    var output = WaitForPrompt(shell, 60000);
                    Log($"  Output ({output.Length}c): first 300={Trunc(Clean(output), 300)}");
                    result.RawOutput += $"[shell:{cmd}]\n{output}\n";

                    var parsed = ParseSetLines(output);
                    if (!string.IsNullOrEmpty(parsed))
                    {
                        var lineCount = parsed.Split('\n').Length;
                        result.Success = true;
                        result.Config = parsed;
                        Log($"  SUCCESS: {lineCount} set lines via \"{cmd}\"");
                        break;
                    }
                    Log($"  No 'set' lines found for \"{cmd}\"");
                }

                if (!result.Success)
                {
                    result.Error = "Connected + authenticated, but no 'set' lines returned by any command";
                    Log($"FAIL: {result.Error}");
                    Log($"  Full raw output length: {result.RawOutput.Length}");
                }

                // Stay in config mode (#) — "run" prefix needed for operational commands

                // --- run show version ---
                try
                {
                    Log("  Send: run show version");
                    shell.WriteLine("run show version");
                    var verOutput = WaitForPrompt(shell, 10000);
                    result.VersionOutput = StripAnsi(verOutput);
                    Log($"  Version output ({verOutput.Length}c): {Trunc(Clean(verOutput), 300)}");
                    result.RawOutput += $"[version]\n{verOutput}\n";
                }
                catch (Exception ex)
                {
                    Log($"  Version command failed: {ex.Message}");
                }

                // --- run show interface brief ---
                try
                {
                    Log("  Send: run show interface brief");
                    shell.WriteLine("run show interface brief");
                    var ifOutput = WaitForPrompt(shell, 15000);
                    result.InterfacesOutput = StripAnsi(ifOutput);
                    Log($"  Interfaces output ({ifOutput.Length}c): {Trunc(Clean(ifOutput), 300)}");
                    result.RawOutput += $"[interfaces]\n{ifOutput}\n";
                }
                catch (Exception ex)
                {
                    Log($"  Interfaces command failed: {ex.Message}");
                }

                // --- run show lldp neighbor ---
                try
                {
                    Log("  Send: run show lldp neighbor");
                    shell.WriteLine("run show lldp neighbor");
                    var lldpOutput = WaitForPrompt(shell, 15000);
                    result.LldpOutput = StripAnsi(lldpOutput);
                    Log($"  LLDP output ({lldpOutput.Length}c): {Trunc(Clean(lldpOutput), 300)}");
                    result.RawOutput += $"[lldp]\n{lldpOutput}\n";
                }
                catch (Exception ex)
                {
                    Log($"  LLDP command failed: {ex.Message}");
                }

                // --- run show interface diagnostics optics all ---
                try
                {
                    Log("  Send: run show interface diagnostics optics all");
                    shell.WriteLine("run show interface diagnostics optics all");
                    var opticsOutput = WaitForPrompt(shell, 20000);
                    result.OpticsOutput = StripAnsi(opticsOutput);
                    Log($"  Optics output ({opticsOutput.Length}c): {Trunc(Clean(opticsOutput), 300)}");
                    result.RawOutput += $"[optics]\n{opticsOutput}\n";
                }
                catch (Exception ex)
                {
                    Log($"  Optics command failed: {ex.Message}");
                }

                // Clean exit — exit config mode then CLI
                try
                {
                    shell.WriteLine("exit"); // exit config mode
                    System.Threading.Thread.Sleep(500);
                    shell.WriteLine("exit"); // exit CLI
                }
                catch { }
            }
            catch (Exception ex)
            {
                result.Error = $"Shell error: {ex.GetType().Name}: {ex.Message}";
                Log($"SHELL ERROR: {result.Error}\n  Stack: {ex.StackTrace}");
                result.RawOutput += $"[shell-error] {ex.Message}\n";
            }
            finally
            {
                try { client.Disconnect(); } catch { }
                client.Dispose();
                Log("=== Disconnected ===");
            }
        });

        await FinalizeLog();
        return result;
    }

    /// <summary>
    /// Send a list of PicOS set commands to a switch via interactive SSH shell.
    /// Enters config mode, sends each command, then commits.
    /// </summary>
    public static async Task<SshResult> SendCommandsAsync(
        Data.DbRepository repo,
        Guid? switchId,
        string hostname,
        string host,
        int port,
        string username,
        string password,
        List<string> commands,
        int timeoutMs = 30000)
    {
        var log = new StringBuilder();
        var result = new SshResult();

        void Log(string msg) => log.AppendLine($"[{DateTime.Now:HH:mm:ss.fff}] {msg}");

        int logId = 0;
        try { logId = await repo.InsertSshLogAsync(switchId, hostname, host, username, port); result.DbLogId = logId; }
        catch { /* log row optional */ }

        async Task FinalizeLog()
        {
            result.LogEntries = log.ToString();
            try { if (logId > 0) await repo.UpdateSshLogAsync(logId, result.Success, result.Error, result.RawOutput, commands.Count, result.LogEntries); } catch { }
        }

        await Task.Run(async () =>
        {
            try
            {
                Log($"Connecting to {host}:{port} as {username}...");
                var authMethods = new List<AuthenticationMethod>
                {
                    new PasswordAuthenticationMethod(username, password),
                    new KeyboardInteractiveAuthenticationMethod(username)
                };
                ((KeyboardInteractiveAuthenticationMethod)authMethods[1]).AuthenticationPrompt += (_, e) =>
                {
                    foreach (var p in e.Prompts) p.Response = password;
                };
                var connInfo = new ConnectionInfo(host, port, username, authMethods.ToArray());
                connInfo.Timeout = TimeSpan.FromMilliseconds(timeoutMs);
                using var client = new SshClient(connInfo);
                client.HostKeyReceived += (_, e) => { e.CanTrust = true; };
                client.Connect();
                Log("Connected.");

                using var shell = client.CreateShellStream("xterm", 250, 50, 800, 600, 65536);
                Log("Shell created, waiting for prompt...");
                var banner = WaitForPrompt(shell, 8000);
                Log($"  Banner: {Trunc(Clean(banner), 200)}");

                // Disable paging
                shell.WriteLine("terminal length 0");
                WaitForPrompt(shell, 3000);
                shell.WriteLine("set cli screen-length 0");
                WaitForPrompt(shell, 3000);

                // Enter config mode
                shell.WriteLine("conf");
                var confResp = WaitForPrompt(shell, 5000);
                Log($"  conf: {Trunc(Clean(confResp), 200)}");

                // Send each command
                int sent = 0;
                foreach (var cmd in commands)
                {
                    Log($"  Send: {cmd}");
                    shell.WriteLine(cmd);
                    var resp = WaitForPrompt(shell, 5000);
                    var cleanResp = StripAnsi(resp).Trim();
                    result.RawOutput += $"{cmd}\n{cleanResp}\n";

                    // Check for errors in response
                    if (cleanResp.Contains("error", StringComparison.OrdinalIgnoreCase) ||
                        cleanResp.Contains("invalid", StringComparison.OrdinalIgnoreCase) ||
                        cleanResp.Contains("unknown", StringComparison.OrdinalIgnoreCase))
                    {
                        Log($"  WARNING: {Trunc(cleanResp, 200)}");
                    }
                    sent++;
                }

                // Commit changes
                Log("  Send: commit");
                shell.WriteLine("commit");
                var commitResp = WaitForPrompt(shell, 15000);
                Log($"  commit: {Trunc(Clean(commitResp), 300)}");
                result.RawOutput += $"[commit]\n{commitResp}\n";

                result.Success = true;
                result.Config = string.Join("\n", commands);
                Log($"SUCCESS: {sent} commands sent and committed.");

                client.Disconnect();
            }
            catch (Exception ex)
            {
                result.Error = $"{ex.GetType().Name}: {ex.Message}";
                Log($"ERROR: {result.Error}");
            }
        });

        await FinalizeLog();
        return result;
    }

    /// <summary>
    /// Read from shell stream until we see a PicOS prompt (ends with > or # or $).
    /// Also stops if no data for 2 seconds after receiving some data.
    /// </summary>
    private static string WaitForPrompt(ShellStream shell, int timeoutMs)
    {
        var sb = new StringBuilder();
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        var lastDataTime = DateTime.UtcNow;
        const int quietMs = 2000; // Stop if no data for 2s after we started receiving

        while (DateTime.UtcNow < deadline)
        {
            if (shell.DataAvailable)
            {
                var chunk = shell.Read();
                sb.Append(chunk);
                lastDataTime = DateTime.UtcNow;

                // Check if the last line looks like a prompt
                var current = sb.ToString();
                var lastNewline = current.TrimEnd().LastIndexOf('\n');
                var lastLine = lastNewline >= 0
                    ? current[(lastNewline + 1)..]
                    : current;
                lastLine = StripAnsi(lastLine).Trim();

                // PicOS prompts: "admin@MEP-94-CORE01>" or "admin@MEP-94-CORE01#"
                // Must be short (not a config line) and end with prompt char
                if (lastLine.Length > 0 && lastLine.Length < 100 &&
                    !lastLine.StartsWith("set ") &&
                    (lastLine.EndsWith(">") || lastLine.EndsWith("#") || lastLine.EndsWith("$")))
                {
                    // Brief pause to catch any trailing data
                    System.Threading.Thread.Sleep(200);
                    if (shell.DataAvailable)
                    {
                        sb.Append(shell.Read());
                    }
                    return sb.ToString();
                }

                // Check for --More-- paging prompt and send space
                if (lastLine.Contains("--More--") || lastLine.Contains("--more--"))
                {
                    shell.Write(" ");
                }
            }
            else
            {
                // If we've been receiving data and it stopped, assume done
                if (sb.Length > 0 && (DateTime.UtcNow - lastDataTime).TotalMilliseconds > quietMs)
                    return sb.ToString();

                System.Threading.Thread.Sleep(50);
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// Extract lines starting with "set " from raw output, stripping ANSI codes.
    /// </summary>
    private static string ParseSetLines(string output)
    {
        var clean = StripAnsi(output);
        var lines = clean.Split('\n');
        var configLines = new List<string>();
        foreach (var line in lines)
        {
            var trimmed = line.TrimEnd('\r').Trim();
            if (trimmed.StartsWith("set "))
                configLines.Add(trimmed);
        }
        return string.Join("\n", configLines);
    }

    private static string StripAnsi(string s) =>
        Regex.Replace(s, @"\x1B(?:[@-Z\\-_]|\[[0-?]*[ -/]*[@-~])", "");

    private static string Clean(string s) =>
        StripAnsi(s).Replace("\r\n", "\\n").Replace("\n", "\\n").Replace("\r", "\\r");

    private static string Trunc(string s, int max) =>
        string.IsNullOrEmpty(s) ? "(empty)" : s.Length <= max ? s : s[..max] + "...";

    // ── BGP Config Parser ───────────────────────────────────────────────

    public class BgpParsed
    {
        public string LocalAs { get; set; } = "";
        public string RouterId { get; set; } = "";
        public bool FastExternalFailover { get; set; }
        public bool EbgpRequiresPolicy { get; set; }
        public bool BestpathMultipathRelax { get; set; }
        public bool RedistributeConnected { get; set; }
        public int MaxPaths { get; set; } = 4;
        public List<(string Ip, string RemoteAs, bool Bfd, string Description)> Neighbors { get; set; } = new();
        public List<string> Networks { get; set; } = new();
    }

    /// <summary>Parse BGP config from PicOS running-config set lines.</summary>
    public static BgpParsed ParseBgpConfig(string configText)
    {
        var bgp = new BgpParsed();
        var neighborDescs = new Dictionary<string, string>();
        var neighborAs = new Dictionary<string, string>();
        var neighborBfd = new HashSet<string>();

        foreach (var rawLine in configText.Split('\n'))
        {
            var line = rawLine.Trim();
            if (!line.StartsWith("set protocols bgp ")) continue;
            var rest = line["set protocols bgp ".Length..];

            // local-as
            if (rest.StartsWith("local-as "))
            {
                bgp.LocalAs = rest["local-as ".Length..].Trim('"', ' ');
            }
            // router-id
            else if (rest.StartsWith("router-id "))
            {
                bgp.RouterId = rest["router-id ".Length..].Trim();
            }
            // fast-external-failover
            else if (rest.StartsWith("fast-external-failover"))
            {
                bgp.FastExternalFailover = rest.Contains("true");
            }
            // ebgp-requires-policy
            else if (rest.StartsWith("ebgp-requires-policy"))
            {
                bgp.EbgpRequiresPolicy = !rest.Contains("false");
            }
            // bestpath as-path multipath-relax
            else if (rest.StartsWith("bestpath as-path multipath-relax"))
            {
                bgp.BestpathMultipathRelax = true;
            }
            // neighbor
            else if (rest.StartsWith("neighbor "))
            {
                var parts = rest["neighbor ".Length..].Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2) continue;
                var ip = parts[0];

                // remote-as "65132" bfd
                if (parts[1] == "remote-as")
                {
                    neighborAs[ip] = parts.Length > 2 ? parts[2].Trim('"') : "";
                    // Check if bfd is at end of same line
                    if (rest.Contains(" bfd", StringComparison.OrdinalIgnoreCase))
                        neighborBfd.Add(ip);
                }
                // bfd (standalone)
                else if (parts[1] == "bfd")
                {
                    neighborBfd.Add(ip);
                }
                // description
                else if (parts[1] == "description")
                {
                    var descStart = rest.IndexOf("description ", StringComparison.Ordinal) + "description ".Length;
                    neighborDescs[ip] = rest[descStart..].Trim('"', ' ');
                }
            }
            // ipv4-unicast network
            else if (rest.StartsWith("ipv4-unicast network "))
            {
                var net = rest["ipv4-unicast network ".Length..].Trim();
                if (!string.IsNullOrEmpty(net))
                    bgp.Networks.Add(net);
            }
            // ipv4-unicast redistribute connected
            else if (rest.StartsWith("ipv4-unicast redistribute connected"))
            {
                bgp.RedistributeConnected = true;
            }
            // ipv4-unicast multipath ebgp maximum-paths
            else if (rest.StartsWith("ipv4-unicast multipath ebgp maximum-paths "))
            {
                if (int.TryParse(rest["ipv4-unicast multipath ebgp maximum-paths ".Length..].Trim(), out var mp))
                    bgp.MaxPaths = mp;
            }
        }

        // Assemble neighbors
        foreach (var kvp in neighborAs)
        {
            neighborDescs.TryGetValue(kvp.Key, out var desc);
            bgp.Neighbors.Add((kvp.Key, kvp.Value, neighborBfd.Contains(kvp.Key), desc ?? ""));
        }

        return bgp;
    }
}
