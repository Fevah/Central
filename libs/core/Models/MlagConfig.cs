using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Central.Core.Models;

public class MlagConfig : INotifyPropertyChanged
{
    private int _id;
    private string _building = "", _domainType = "", _mlagDomain = "";
    private string _switchA = "", _switchB = "", _b2bPartner = "", _status = "Active";
    private string _peerLinkAe = "", _physicalMembers = "", _peerVlan = "", _trunkVlans = "";
    private string _sharedDomainMac = "", _peerLinkSubnet = "";
    private string _node0Ip = "", _node1Ip = "", _node0IpLink2 = "", _node1IpLink2 = "", _notes = "";

    public int Id { get => _id; set { _id = value; N(); } }
    public string Building { get => _building; set { _building = value; N(); } }
    public string DomainType { get => _domainType; set { _domainType = value; N(); } }
    public string MlagDomain { get => _mlagDomain; set { _mlagDomain = value; N(); } }
    public string SwitchA { get => _switchA; set { _switchA = value; N(); } }
    public string SwitchB { get => _switchB; set { _switchB = value; N(); } }
    public string B2BPartner { get => _b2bPartner; set { _b2bPartner = value; N(); } }
    public string Status { get => _status; set { _status = value; N(); } }
    public string PeerLinkAe { get => _peerLinkAe; set { _peerLinkAe = value; N(); } }
    public string PhysicalMembers { get => _physicalMembers; set { _physicalMembers = value; N(); } }
    public string PeerVlan { get => _peerVlan; set { _peerVlan = value; N(); } }
    public string TrunkVlans { get => _trunkVlans; set { _trunkVlans = value; N(); } }
    public string SharedDomainMac { get => _sharedDomainMac; set { _sharedDomainMac = value; N(); } }
    public string PeerLinkSubnet { get => _peerLinkSubnet; set { _peerLinkSubnet = value; N(); } }
    public string Node0Ip { get => _node0Ip; set { _node0Ip = value; N(); } }
    public string Node1Ip { get => _node1Ip; set { _node1Ip = value; N(); } }
    public string Node0IpLink2 { get => _node0IpLink2; set { _node0IpLink2 = value; N(); } }
    public string Node1IpLink2 { get => _node1IpLink2; set { _node1IpLink2 = value; N(); } }
    public string Notes { get => _notes; set { _notes = value; N(); } }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void N([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}
