using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Central.Engine.Models;

public class DeviceRecord : INotifyPropertyChanged
{
    private string _id             = "";
    private string _switchName     = "";
    private string _site           = "";
    private string _deviceType     = "";
    private string _building       = "";
    private string _region         = "";
    private string _status         = "";
    private string _ip             = "";
    private string _managementIp   = "";
    private string _mgmtL3Ip       = "";
    private string _loopbackIp     = "";
    private string _loopbackSubnet = "";
    private string _asn            = "";
    private string _mlagDomain     = "";
    private string _aeRange        = "";
    private string _floor          = "";
    private string _rack           = "";
    private string _model          = "";
    private string _serialNumber   = "";
    private string _uplinkSwitch   = "";
    private string _uplinkPort     = "";
    private string _notes          = "";
    private string _linkedHostname = "";

    public string Id             { get => _id;             set { _id             = value; OnPropertyChanged(); } }
    public string SwitchName     { get => _switchName;     set { _switchName     = value; OnPropertyChanged(); } }
    public string Site           { get => _site;           set { _site           = value; OnPropertyChanged(); } }
    public string DeviceType     { get => _deviceType;     set { _deviceType     = value; OnPropertyChanged(); } }
    public string Building       { get => _building;       set { _building       = value; OnPropertyChanged(); } }
    public string Region         { get => _region;         set { _region         = value; OnPropertyChanged(); } }
    public string Status         { get => _status;         set { _status         = value; OnPropertyChanged(); } }
    public string Ip             { get => _ip;             set { _ip             = value; OnPropertyChanged(); } }
    public string ManagementIp   { get => _managementIp;   set { _managementIp   = value; OnPropertyChanged(); } }
    public string MgmtL3Ip       { get => _mgmtL3Ip;       set { _mgmtL3Ip       = value; OnPropertyChanged(); } }
    public string LoopbackIp     { get => _loopbackIp;     set { _loopbackIp     = value; OnPropertyChanged(); } }
    public string LoopbackSubnet { get => _loopbackSubnet; set { _loopbackSubnet = value; OnPropertyChanged(); } }
    public string Asn            { get => _asn;            set { _asn            = value; OnPropertyChanged(); } }
    public string MlagDomain     { get => _mlagDomain;     set { _mlagDomain     = value; OnPropertyChanged(); } }
    public string AeRange        { get => _aeRange;        set { _aeRange        = value; OnPropertyChanged(); } }
    public string Floor          { get => _floor;          set { _floor          = value; OnPropertyChanged(); } }
    public string Rack           { get => _rack;           set { _rack           = value; OnPropertyChanged(); } }
    public string Model          { get => _model;          set { _model          = value; OnPropertyChanged(); } }
    public string SerialNumber   { get => _serialNumber;   set { _serialNumber   = value; OnPropertyChanged(); } }
    public string UplinkSwitch   { get => _uplinkSwitch;   set { _uplinkSwitch   = value; OnPropertyChanged(); } }
    public string UplinkPort     { get => _uplinkPort;     set { _uplinkPort     = value; OnPropertyChanged(); } }
    public string Notes          { get => _notes;          set { _notes          = value; OnPropertyChanged(); } }
    public string LinkedHostname { get => _linkedHostname; set { _linkedHostname = value; OnPropertyChanged(); } }

    public bool IsLinked => !string.IsNullOrEmpty(LinkedHostname);
    public bool IsActive  => Status == "Active";

    /// <summary>Status indicator colour for the device icon dot.
    /// Checks IconOverrideService first (admin/user configurable), falls back to code defaults.</summary>
    public string StatusColor =>
        Services.IconOverrideService.Instance.ResolveColorOrDefault("status.device", Status, Status switch
        {
            "Active" => "#22C55E",
            "RESERVED" => "#F59E0B",
            "Decommissioned" => "#EF4444",
            "Maintenance" => "#8B5CF6",
            _ => "#6B7280"
        });

    /// <summary>Detail links for master-detail expansion. Shows P2P+B2B+FW links involving this device.</summary>
    public System.Collections.ObjectModel.ObservableCollection<DeviceLinkSummary> DetailLinks { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>Flattened link summary for device master-detail expansion.</summary>
public class DeviceLinkSummary
{
    public string LinkType { get; set; } = "";   // P2P, B2B, FW
    public int LinkId { get; set; }
    public string RemoteDevice { get; set; } = "";
    public string LocalPort { get; set; } = "";
    public string RemotePort { get; set; } = "";
    public string Vlan { get; set; } = "";
    public string Subnet { get; set; } = "";
    public string Status { get; set; } = "";
}
