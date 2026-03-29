using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Central.Core.Models;

public class SwitchInterface : INotifyPropertyChanged
{
    private string _interfaceName = "";
    private string _adminStatus = "";
    private string _linkStatus = "";
    private string _speed = "";
    private string _mtu = "";
    private string _description = "";
    private string _lldpHost = "";
    private string _lldpPort = "";
    private string _moduleType = "";
    private string _txPower = "";
    private string _rxPower = "";
    private string _opticsTemp = "";
    private string _rxColor = "#6B7280";

    public int Id { get; set; }
    public Guid SwitchId { get; set; }
    public DateTime CapturedAt { get; set; }

    public string InterfaceName { get => _interfaceName; set { _interfaceName = value; OnPropertyChanged(); } }
    public string AdminStatus { get => _adminStatus; set { _adminStatus = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusColor)); } }
    public string LinkStatus { get => _linkStatus; set { _linkStatus = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusColor)); } }
    public string Speed { get => _speed; set { _speed = value; OnPropertyChanged(); } }
    public string Mtu { get => _mtu; set { _mtu = value; OnPropertyChanged(); } }
    public string Description { get => _description; set { _description = value; OnPropertyChanged(); } }
    public string LldpHost { get => _lldpHost; set { _lldpHost = value; OnPropertyChanged(); } }
    public string LldpPort { get => _lldpPort; set { _lldpPort = value; OnPropertyChanged(); } }
    public string ModuleType { get => _moduleType; set { _moduleType = value; OnPropertyChanged(); } }
    public string TxPower { get => _txPower; set { _txPower = value; OnPropertyChanged(); } }
    public string RxPower { get => _rxPower; set { _rxPower = value; OnPropertyChanged(); } }
    public string OpticsTemp { get => _opticsTemp; set { _opticsTemp = value; OnPropertyChanged(); } }
    public string RxColor { get => _rxColor; set { _rxColor = value; OnPropertyChanged(); } }

    public string StatusColor => LinkStatus.Contains("up", StringComparison.OrdinalIgnoreCase) ? "#22C55E" :
                                 LinkStatus.Contains("down", StringComparison.OrdinalIgnoreCase) ? "#EF4444" : "#6B7280";

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    /// <summary>Parse "run show interface brief" output into interface list.</summary>
    public static List<SwitchInterface> Parse(Guid switchId, string output)
    {
        var list = new List<SwitchInterface>();
        var lines = output.Split('\n');
        var headerFound = false;

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd('\r').Trim();
            if (string.IsNullOrEmpty(line)) continue;

            // Detect header line — contains "Interface" and "Status" or "Link"
            if (!headerFound)
            {
                if (line.Contains("Interface") && (line.Contains("Status") || line.Contains("Link")))
                    headerFound = true;
                continue;
            }

            // Skip separator lines
            if (line.StartsWith("---") || line.StartsWith("===")) continue;
            // Skip prompt lines
            if (line.Contains("#") && line.Contains("@")) continue;

            // Parse space-separated columns (2+ spaces as delimiter)
            // PicOS format: Interface  Management  Status  Flow Control  Duplex  Speed  Description
            // Note: "Flow Control" is two words but usually shows as "Disabled"/"Enabled" in its own column
            var parts = System.Text.RegularExpressions.Regex.Split(line, @"\s{2,}");
            if (parts.Length >= 2)
            {
                var iface = new SwitchInterface
                {
                    SwitchId = switchId,
                    CapturedAt = DateTime.UtcNow,
                    InterfaceName = parts[0].Trim(),
                };

                // Detect PicOS 7-column format vs generic
                // PicOS: Interface | Management | Status | FlowCtrl | Duplex | Speed | Description
                if (parts.Length >= 6)
                {
                    iface.AdminStatus = parts[1].Trim();  // Management (Enabled/Disabled)
                    iface.LinkStatus  = parts[2].Trim();   // Status (Up/Down)
                    // parts[3] = Flow Control — skip
                    // parts[4] = Duplex — skip
                    iface.Speed       = parts.Length >= 6 ? parts[5].Trim() : "";  // Speed
                    iface.Description = parts.Length >= 7 ? parts[6].Trim() : "";  // Description
                    iface.Mtu         = "";  // Not in PicOS brief output
                }
                else
                {
                    // Generic fallback for other formats
                    if (parts.Length >= 2) iface.AdminStatus = parts[1].Trim();
                    if (parts.Length >= 3) iface.LinkStatus = parts[2].Trim();
                    if (parts.Length >= 4) iface.Speed = parts[3].Trim();
                    if (parts.Length >= 5) iface.Description = parts[4].Trim();
                }

                // Only add if it looks like an interface name
                if (iface.InterfaceName.Contains("-") || iface.InterfaceName.Contains("/")
                    || iface.InterfaceName.StartsWith("lo") || iface.InterfaceName.StartsWith("vlan"))
                {
                    list.Add(iface);
                }
            }
        }
        return list;
    }

    /// <summary>Merge latest optics readings into interface list for grid display.</summary>
    public static void MergeOptics(List<SwitchInterface> interfaces, List<InterfaceOptics> optics)
    {
        if (optics == null || optics.Count == 0) return;
        var lookup = interfaces.ToDictionary(i => i.InterfaceName, i => i, StringComparer.OrdinalIgnoreCase);
        foreach (var o in optics)
        {
            if (lookup.TryGetValue(o.InterfaceName, out var iface))
            {
                iface.ModuleType = o.ModuleType;
                iface.TxPower = o.TxPowerDbm.HasValue ? $"{o.TxPowerDbm:F2}" : "";
                iface.RxPower = o.RxPowerDbm.HasValue ? $"{o.RxPowerDbm:F2}" : "";
                iface.OpticsTemp = o.TempC.HasValue ? $"{o.TempC:F1}°C" : "";
                iface.RxColor = !o.RxPowerDbm.HasValue ? "#6B7280" :
                    o.RxPowerDbm <= -30 ? "#EF4444" :
                    o.RxPowerDbm <= -20 ? "#F59E0B" : "#22C55E";
            }
        }
    }

    /// <summary>
    /// Parse "run show lldp neighbor" output and merge LLDP host/port into existing interface list.
    /// PicOS format: Local Interface  Chassis Id  Port info  System Name
    /// </summary>
    public static void MergeLldp(List<SwitchInterface> interfaces, string lldpOutput)
    {
        if (string.IsNullOrWhiteSpace(lldpOutput)) return;

        var lookup = interfaces.ToDictionary(i => i.InterfaceName, i => i, StringComparer.OrdinalIgnoreCase);
        var lines = lldpOutput.Split('\n');
        var headerFound = false;

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd('\r').Trim();
            if (string.IsNullOrEmpty(line)) continue;

            // Detect header — contains "Local Interface" or "Local Port"
            if (!headerFound)
            {
                if (line.Contains("Local") && (line.Contains("Interface") || line.Contains("Port")))
                    headerFound = true;
                continue;
            }

            if (line.StartsWith("---") || line.StartsWith("===")) continue;
            if (line.Contains("#") && line.Contains("@")) continue;

            var parts = System.Text.RegularExpressions.Regex.Split(line, @"\s{2,}");
            if (parts.Length < 3) continue;

            var localIface = parts[0].Trim();
            // PicOS LLDP format: LocalPort | ChassisId | PortId | Management Address | Host Name | Capability
            // parts[1] = Chassis Id (MAC) — skip
            var portInfo   = parts.Length >= 3 ? parts[2].Trim() : "";
            // parts[3] = Management Address (IP) — skip, we want hostname
            var hostName   = parts.Length >= 5 ? parts[4].Trim() : "";
            // Fallback: if only 4 columns, parts[3] might be the hostname (non-IP)
            if (string.IsNullOrEmpty(hostName) && parts.Length >= 4)
            {
                var candidate = parts[3].Trim();
                if (!System.Text.RegularExpressions.Regex.IsMatch(candidate, @"^\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}$"))
                    hostName = candidate;
            }

            if (lookup.TryGetValue(localIface, out var iface))
            {
                iface.LldpHost = hostName;
                iface.LldpPort = portInfo;
            }
        }
    }
}
