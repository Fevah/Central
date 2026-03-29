using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Central.Core.Models;

public class MasterDevice : INotifyPropertyChanged
{
    private Guid _id;
    private string _deviceName = "", _deviceType = "", _region = "", _building = "", _status = "";
    private string _primaryIp = "", _managementIp = "", _loopbackIp = "", _loopbackSubnet = "", _mgmtL3Ip = "";
    private string _asn = "", _mlagDomain = "", _aeRange = "", _model = "", _serialNumber = "";
    private string _uplinkSwitch = "", _uplinkPort = "", _notes = "";
    private string _mstpPriority = "", _mlagPeer = "";
    private int _p2pLinkCount, _b2bLinkCount, _fwLinkCount;
    private bool _hasConfig;

    public Guid Id { get => _id; set { _id = value; N(); } }
    public string DeviceName { get => _deviceName; set { _deviceName = value; N(); } }
    public string DeviceType { get => _deviceType; set { _deviceType = value; N(); } }
    public string Region { get => _region; set { _region = value; N(); } }
    public string Building { get => _building; set { _building = value; N(); } }
    public string Status { get => _status; set { _status = value; N(); } }
    public string PrimaryIp { get => _primaryIp; set { _primaryIp = value; N(); } }
    public string ManagementIp { get => _managementIp; set { _managementIp = value; N(); } }
    public string LoopbackIp { get => _loopbackIp; set { _loopbackIp = value; N(); } }
    public string LoopbackSubnet { get => _loopbackSubnet; set { _loopbackSubnet = value; N(); } }
    public string MgmtL3Ip { get => _mgmtL3Ip; set { _mgmtL3Ip = value; N(); } }
    public string Asn { get => _asn; set { _asn = value; N(); } }
    public string MlagDomain { get => _mlagDomain; set { _mlagDomain = value; N(); } }
    public string AeRange { get => _aeRange; set { _aeRange = value; N(); } }
    public string Model { get => _model; set { _model = value; N(); } }
    public string SerialNumber { get => _serialNumber; set { _serialNumber = value; N(); } }
    public string UplinkSwitch { get => _uplinkSwitch; set { _uplinkSwitch = value; N(); } }
    public string UplinkPort { get => _uplinkPort; set { _uplinkPort = value; N(); } }
    public string Notes { get => _notes; set { _notes = value; N(); } }
    public int P2PLinkCount { get => _p2pLinkCount; set { _p2pLinkCount = value; N(); } }
    public int B2BLinkCount { get => _b2bLinkCount; set { _b2bLinkCount = value; N(); } }
    public int FWLinkCount { get => _fwLinkCount; set { _fwLinkCount = value; N(); } }
    public string MstpPriority { get => _mstpPriority; set { _mstpPriority = value; N(); } }
    public string MlagPeer { get => _mlagPeer; set { _mlagPeer = value; N(); } }
    public bool HasConfig { get => _hasConfig; set { _hasConfig = value; N(); } }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void N([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
