using System.Diagnostics;
using System.Text.Json;
using Central.Core.Models;

namespace Central.Core.Services;

/// <summary>Wraps podman CLI for container management.</summary>
public static class PodmanService
{
    private static async Task<string> RunAsync(string args, int timeoutMs = 10000)
    {
        var psi = new ProcessStartInfo("podman", args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var proc = Process.Start(psi);
        if (proc == null) return "";

        var output = await proc.StandardOutput.ReadToEndAsync();
        using var cts = new CancellationTokenSource(timeoutMs);
        await proc.WaitForExitAsync(cts.Token);
        return output.Trim();
    }

    public static async Task<List<ContainerInfo>> GetContainersAsync()
    {
        try
        {
            var json = await RunAsync("ps -a --format json");
            if (string.IsNullOrEmpty(json)) return new();

            var containers = JsonSerializer.Deserialize<List<JsonElement>>(json) ?? new();
            return containers.Select(c => new ContainerInfo
            {
                Name = GetStr(c, "Names") ?? GetStr(c, "Name") ?? "",
                Image = GetStr(c, "Image") ?? "",
                Status = GetStr(c, "Status") ?? "",
                State = GetStr(c, "State") ?? "",
                Created = GetStr(c, "Created") ?? GetStr(c, "CreatedAt") ?? "",
                Ports = GetStr(c, "Ports") ?? "",
            }).ToList();
        }
        catch { return new(); }
    }

    public static async Task<string> GetPodStatusAsync()
    {
        try { return await RunAsync("pod ps --format '{{.Name}} {{.Status}}'"); }
        catch { return "podman not available"; }
    }

    public static Task StartContainerAsync(string name) => RunAsync($"start {name}");
    public static Task StopContainerAsync(string name) => RunAsync($"stop {name}");
    public static Task RestartContainerAsync(string name) => RunAsync($"restart {name}");

    public static async Task<string> GetLogsAsync(string name, int lines = 100)
    {
        try { return await RunAsync($"logs --tail {lines} {name}", 15000); }
        catch (Exception ex) { return $"Error: {ex.Message}"; }
    }

    private static string? GetStr(JsonElement el, string prop)
    {
        if (el.TryGetProperty(prop, out var val))
        {
            if (val.ValueKind == JsonValueKind.String) return val.GetString();
            if (val.ValueKind == JsonValueKind.Array)
            {
                var items = new List<string>();
                foreach (var item in val.EnumerateArray())
                    if (item.ValueKind == JsonValueKind.String) items.Add(item.GetString() ?? "");
                return string.Join(", ", items);
            }
        }
        return null;
    }
}
