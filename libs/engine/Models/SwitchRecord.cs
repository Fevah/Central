using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Central.Engine.Models;

public class SwitchRecord : INotifyPropertyChanged
{
    private Guid    _id;
    private string  _hostname = "";
    private string  _site = "";
    private string  _role = "";
    private string  _loopbackIp = "";
    private int     _loopbackPrefix;
    private string  _managementIp = "";
    private string  _sshUsername = "";
    private int     _sshPort = 22;
    private bool?   _lastPingOk;
    private double? _lastPingMs;
    private bool?   _lastSshOk;
    private string  _lastPingAt = "";
    private string  _lastSshAt = "";
    private string  _picosVersion = "";
    private bool    _isPinging;

    public Guid     Id             { get => _id;             set { _id = value; OnPropertyChanged(); } }
    public string   Hostname       { get => _hostname;       set { _hostname = value; OnPropertyChanged(); } }
    public string   Site           { get => _site;           set { _site = value; OnPropertyChanged(); } }
    public string   Role           { get => _role;           set { _role = value; OnPropertyChanged(); } }
    public string   LoopbackIp     { get => _loopbackIp;     set { _loopbackIp = value; OnPropertyChanged(); OnPropertyChanged(nameof(LoopbackDisplay)); } }
    public int      LoopbackPrefix { get => _loopbackPrefix; set { _loopbackPrefix = value; OnPropertyChanged(); OnPropertyChanged(nameof(LoopbackDisplay)); } }
    public string   ManagementIp   { get => _managementIp;   set { _managementIp = value; OnPropertyChanged(); } }
    public string   SshUsername    { get => _sshUsername;    set { _sshUsername = value; OnPropertyChanged(); } }
    public int      SshPort        { get => _sshPort;        set { _sshPort = value; OnPropertyChanged(); } }

    private string _sshPassword = "";
    public string  SshPassword    { get => _sshPassword;    set { _sshPassword = value; OnPropertyChanged(); } }

    private string _sshOverrideIp = "";
    public string  SshOverrideIp  { get => _sshOverrideIp;  set { _sshOverrideIp = value; OnPropertyChanged(); } }

    /// <summary>Effective SSH target: override IP if set, else management IP.</summary>
    public string EffectiveSshIp => !string.IsNullOrWhiteSpace(SshOverrideIp) ? SshOverrideIp : ManagementIp;

    public string   PicosVersion   { get => _picosVersion;   set { _picosVersion = value; OnPropertyChanged(); } }

    private string _hardwareModel = "";
    public string  HardwareModel  { get => _hardwareModel;  set { _hardwareModel = value; OnPropertyChanged(); } }

    private string _macAddress = "";
    public string  MacAddress     { get => _macAddress;     set { _macAddress = value; OnPropertyChanged(); } }

    private string _serialNumber = "";
    public string  SerialNumber   { get => _serialNumber;   set { _serialNumber = value; OnPropertyChanged(); } }

    private string _uptime = "";
    public string  Uptime
    {
        get => _uptime;
        set { _uptime = value; OnPropertyChanged(); OnPropertyChanged(nameof(UptimeMinutes)); OnPropertyChanged(nameof(UptimeDisplay)); }
    }

    /// <summary>Total uptime in minutes for sorting. Parses "9 day 5 hour 15 minute" format.</summary>
    public int UptimeMinutes
    {
        get
        {
            if (string.IsNullOrWhiteSpace(_uptime)) return 0;
            int total = 0;
            var parts = _uptime.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length - 1; i++)
            {
                if (!int.TryParse(parts[i], out var num)) continue;
                var unit = parts[i + 1].ToLowerInvariant();
                if (unit.StartsWith("day")) total += num * 1440;
                else if (unit.StartsWith("hour")) total += num * 60;
                else if (unit.StartsWith("min")) total += num;
            }
            return total;
        }
    }

    /// <summary>Formatted uptime: "9d 5h 15m"</summary>
    public string UptimeDisplay
    {
        get
        {
            var mins = UptimeMinutes;
            if (mins <= 0) return "";
            var d = mins / 1440;
            var h = (mins % 1440) / 60;
            var m = mins % 60;
            if (d > 0) return $"{d}d {h}h {m}m";
            if (h > 0) return $"{h}h {m}m";
            return $"{m}m";
        }
    }

    public bool? LastPingOk
    {
        get => _lastPingOk;
        set { _lastPingOk = value; OnPropertyChanged(); OnPropertyChanged(nameof(PingStatus)); OnPropertyChanged(nameof(PingIcon)); OnPropertyChanged(nameof(PingColor)); OnPropertyChanged(nameof(PingLatency)); }
    }

    public double? LastPingMs
    {
        get => _lastPingMs;
        set { _lastPingMs = value; OnPropertyChanged(); OnPropertyChanged(nameof(PingLatency)); }
    }

    public bool? LastSshOk
    {
        get => _lastSshOk;
        set { _lastSshOk = value; OnPropertyChanged(); OnPropertyChanged(nameof(SshStatus)); OnPropertyChanged(nameof(SshIcon)); OnPropertyChanged(nameof(SshColor)); }
    }

    public string LastPingAt
    {
        get => _lastPingAt;
        set { _lastPingAt = value; OnPropertyChanged(); }
    }

    public string LastSshAt
    {
        get => _lastSshAt;
        set { _lastSshAt = value; OnPropertyChanged(); }
    }

    public bool IsPinging
    {
        get => _isPinging;
        set { _isPinging = value; OnPropertyChanged(); OnPropertyChanged(nameof(PingIcon)); OnPropertyChanged(nameof(PingColor)); }
    }

    // Icon indicators: green = ok, red = fail, grey = unknown
    public string PingIcon => IsPinging ? "⏳" :
                              LastPingOk == true  ? "●" :
                              LastPingOk == false ? "●" : "●";

    public string PingColor => IsPinging ? "Orange" :
                               LastPingOk == true  ? "#22C55E" :
                               LastPingOk == false ? "#EF4444" : "#6B7280";

    public string SshIcon  => LastSshOk == true  ? "●" :
                              LastSshOk == false ? "●" : "●";

    public string SshColor => LastSshOk == true  ? "#22C55E" :
                              LastSshOk == false ? "#EF4444" : "#6B7280";

    public string PingStatus => LastPingOk == true  ? $"✓ {LastPingMs:F0} ms" :
                                LastPingOk == false ? "✗ Unreachable" : "—";

    public string PingLatency => LastPingMs.HasValue ? $"{LastPingMs:F0} ms" : "";

    public string SshStatus  => LastSshOk == true  ? "✓ OK" :
                                LastSshOk == false ? "✗ Failed" : "—";

    public string LoopbackDisplay => string.IsNullOrEmpty(LoopbackIp) ? "" :
                                     LoopbackPrefix > 0 ? $"{LoopbackIp}/{LoopbackPrefix}" : LoopbackIp;

    /// <summary>Detail interfaces for master-detail expansion. Populated on demand by the shell.</summary>
    public System.Collections.ObjectModel.ObservableCollection<SwitchInterface> DetailInterfaces { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
