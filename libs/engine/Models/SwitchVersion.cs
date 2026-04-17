using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Central.Engine.Models;

public class SwitchVersion : INotifyPropertyChanged
{
    public int Id { get; set; }
    public Guid SwitchId { get; set; }
    public DateTime CapturedAt { get; set; }
    public string MacAddress { get; set; } = "";
    public string HardwareModel { get; set; } = "";
    public string SerialNumber { get; set; } = "";
    public string Uptime { get; set; } = "";
    public string LinuxVersion { get; set; } = "";
    public string LinuxDate { get; set; } = "";
    public string L2L3Version { get; set; } = "";
    public string L2L3Date { get; set; } = "";
    public string OvsVersion { get; set; } = "";
    public string OvsDate { get; set; } = "";
    public string RawOutput { get; set; } = "";

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    /// <summary>Parse "run show version" output into fields.</summary>
    public static SwitchVersion Parse(Guid switchId, string output)
    {
        var v = new SwitchVersion { SwitchId = switchId, CapturedAt = DateTime.UtcNow, RawOutput = output };
        foreach (var line in output.Split('\n'))
        {
            var trimmed = line.Trim().TrimEnd('\r');
            var parts = trimmed.Split(new[] { ':' }, 2);
            if (parts.Length != 2) continue;
            var key = parts[0].Trim().ToLowerInvariant();
            var val = parts[1].Trim();

            // MAC address variants
            if (key.Contains("mac") && (key.Contains("address") || key.Contains("ethernet")))
                v.MacAddress = val;
            // Hardware model
            else if (key.Contains("model") && (key.Contains("hardware") || !key.Contains("version")))
                v.HardwareModel = val;
            // Serial number
            else if (key.Contains("serial"))
                v.SerialNumber = val;
            // Uptime
            else if (key.Contains("uptime"))
                v.Uptime = val;
            // Linux version
            else if (key.Contains("linux") && key.Contains("version"))
                v.LinuxVersion = val;
            else if (key.Contains("linux") && key.Contains("released"))
                v.LinuxDate = val;
            // Software / L2/L3 / OS version
            else if ((key.Contains("software") || key.Contains("l2/l3") || key == "os" || (key.Contains("os") && !key.Contains("ovs"))) && key.Contains("version"))
                v.L2L3Version = val;
            else if ((key.Contains("software") || key.Contains("l2/l3") || key.Contains("os")) && key.Contains("released"))
                v.L2L3Date = val;
            else if (key == "os")
                v.L2L3Version = val;
            // OVS/OF version
            else if (key.Contains("ovs") && key.Contains("version"))
                v.OvsVersion = val;
            else if (key.Contains("ovs") && key.Contains("released"))
                v.OvsDate = val;
        }
        return v;
    }
}
