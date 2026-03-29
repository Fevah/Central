using System.Management;
using System.Security.Cryptography;
using System.Text;

namespace Central.Protection;

/// <summary>
/// Generates a hardware fingerprint for license binding.
/// Combines: CPU ID, disk serial, machine name, MAC address → SHA256 hash.
/// The fingerprint is deterministic per machine but different across machines.
/// </summary>
public static class HardwareFingerprint
{
    /// <summary>Generate the hardware fingerprint for this machine.</summary>
    public static string Generate()
    {
        var components = new StringBuilder();
        components.Append(GetCpuId());
        components.Append('|');
        components.Append(GetDiskSerial());
        components.Append('|');
        components.Append(Environment.MachineName);
        components.Append('|');
        components.Append(GetMacAddress());

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(components.ToString()));
        return Convert.ToBase64String(hash);
    }

    /// <summary>Get a shortened fingerprint (first 16 chars) for display.</summary>
    public static string GenerateShort() => Generate()[..16];

    private static string GetCpuId()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT ProcessorId FROM Win32_Processor");
            foreach (var obj in searcher.Get())
                return obj["ProcessorId"]?.ToString() ?? "unknown-cpu";
        }
        catch { }
        return "unknown-cpu";
    }

    private static string GetDiskSerial()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT SerialNumber FROM Win32_DiskDrive WHERE Index=0");
            foreach (var obj in searcher.Get())
                return obj["SerialNumber"]?.ToString()?.Trim() ?? "unknown-disk";
        }
        catch { }
        return "unknown-disk";
    }

    private static string GetMacAddress()
    {
        try
        {
            var nics = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up
                         && n.NetworkInterfaceType != System.Net.NetworkInformation.NetworkInterfaceType.Loopback)
                .OrderBy(n => n.Id)
                .FirstOrDefault();
            return nics?.GetPhysicalAddress().ToString() ?? "unknown-mac";
        }
        catch { return "unknown-mac"; }
    }
}
