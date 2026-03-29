using Central.Core.Models;

namespace Central.Core.Services;

/// <summary>
/// Resolves SSH credentials and delegates config building to model BuildConfig().
/// Pure logic — no UI dependencies.
/// </summary>
public static class DeployService
{
    /// <summary>Build config commands for one side of any link type.</summary>
    public static List<string> BuildCommands(NetworkLinkBase link, bool sideA) =>
        link.BuildConfig(sideA);

    /// <summary>Build PicOS set commands for one side of a P2P link.</summary>
    public static List<string> BuildP2PCommands(P2PLink link, bool sideA) =>
        link.BuildConfig(sideA);

    /// <summary>Build PicOS set commands for one side of a B2B link.</summary>
    public static List<string> BuildB2BCommands(B2BLink link, bool sideA) =>
        link.BuildConfig(sideA);

    /// <summary>Build PicOS set commands for one side of a FW link.</summary>
    public static List<string> BuildFWCommands(FWLink link, bool sideA) =>
        link.BuildConfig(sideA);

    /// <summary>Resolve SSH credentials for a device name.</summary>
    public static SshCredentials ResolveCredentials(
        string deviceName,
        IEnumerable<SwitchRecord> switches,
        IEnumerable<DeviceRecord> devices,
        string? defaultUser, string? defaultPass, int defaultPort)
    {
        var sw = switches.FirstOrDefault(s =>
            string.Equals(s.Hostname, deviceName, StringComparison.OrdinalIgnoreCase));

        string? ip = null;
        if (sw != null)
            ip = sw.EffectiveSshIp?.Split('/')[0];

        if (string.IsNullOrEmpty(ip))
        {
            var dev = devices.FirstOrDefault(d =>
                string.Equals(d.SwitchName, deviceName, StringComparison.OrdinalIgnoreCase));
            ip = dev?.ManagementIp?.Split('/')[0];
            if (string.IsNullOrEmpty(ip))
                ip = dev?.Ip?.Split('/')[0];
        }

        var username = (sw != null && !string.IsNullOrWhiteSpace(sw.SshUsername)) ? sw.SshUsername
                     : !string.IsNullOrWhiteSpace(defaultUser) ? defaultUser : "admin";
        var port = (sw != null && sw.SshPort > 0) ? sw.SshPort : defaultPort;
        var password = (sw != null && !string.IsNullOrWhiteSpace(sw.SshPassword)) ? sw.SshPassword : defaultPass;

        return new SshCredentials
        {
            SwitchId = sw?.Id != Guid.Empty ? sw?.Id : null,
            DeviceName = deviceName,
            Ip = ip,
            Port = port,
            Username = username ?? "admin",
            Password = password
        };
    }
}

public class SshCredentials
{
    public Guid? SwitchId { get; set; }
    public string DeviceName { get; set; } = "";
    public string? Ip { get; set; }
    public int Port { get; set; } = 22;
    public string Username { get; set; } = "admin";
    public string? Password { get; set; }

    public bool IsValid => !string.IsNullOrEmpty(Ip) && !string.IsNullOrEmpty(Password);
}
