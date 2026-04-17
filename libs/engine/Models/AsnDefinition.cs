using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Central.Engine.Models;

public class AsnDefinition : INotifyPropertyChanged
{
    private int _id;
    private string _asn = "";
    private string _description = "";
    private string _asnType = "";
    private int _sortOrder;
    private int _deviceCount;
    private string _devices = "";

    public int Id { get => _id; set { _id = value; N(); } }
    public string Asn { get => _asn; set { _asn = value; N(); } }
    public string Description { get => _description; set { _description = value; N(); } }
    public string AsnType { get => _asnType; set { _asnType = value; N(); } }
    public int SortOrder { get => _sortOrder; set { _sortOrder = value; N(); } }
    public int DeviceCount { get => _deviceCount; set { _deviceCount = value; N(); } }
    public string Devices { get => _devices; set { _devices = value; N(); } }

    /// <summary>Display text for the dropdown: "65112 — Core switches (3 devices)"</summary>
    public string DisplayText => string.IsNullOrEmpty(Description)
        ? $"{Asn}  ({DeviceCount})"
        : $"{Asn} — {Description}  ({DeviceCount})";

    /// <summary>Detail: devices bound to this ASN. Populated on master row expand.</summary>
    public System.Collections.ObjectModel.ObservableCollection<AsnBoundDevice> DetailDevices { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;
    private void N([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

/// <summary>Device bound to an ASN for master-detail expansion.</summary>
public class AsnBoundDevice
{
    public string SwitchName { get; set; } = "";
    public string Building { get; set; } = "";
    public string DeviceType { get; set; } = "";
    public string PrimaryIp { get; set; } = "";
}
