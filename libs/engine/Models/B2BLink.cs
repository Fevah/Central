using System.Collections.Generic;

namespace Central.Engine.Models;

public class B2BLink : NetworkLinkBase
{
    private int _id;
    private string _linkId = "", _vlan = "";
    private string _buildingA = "", _deviceA = "", _portA = "", _moduleA = "", _deviceAIp = "";
    private string _buildingB = "", _deviceB = "", _portB = "", _moduleB = "", _deviceBIp = "";
    private string _tx = "", _rx = "", _media = "", _speed = "", _subnet = "", _status = "Active";

    public override int Id { get => _id; set { _id = value; N(); } }
    public override string LinkId { get => _linkId; set { _linkId = value; N(); } }
    public override string Vlan { get => _vlan; set { _vlan = value; N(); } }
    public string BuildingA { get => _buildingA; set { _buildingA = value; N(); } }
    public override string DeviceA { get => _deviceA; set { _deviceA = value; N(); } }
    public string PortA { get => _portA; set { _portA = value; N(); } }
    public string ModuleA { get => _moduleA; set { _moduleA = value; N(); } }
    public string DeviceAIp { get => _deviceAIp; set { _deviceAIp = value; N(); } }
    public string BuildingB { get => _buildingB; set { _buildingB = value; N(); } }
    public override string DeviceB { get => _deviceB; set { _deviceB = value; N(); } }
    public string PortB { get => _portB; set { _portB = value; N(); } }
    public string ModuleB { get => _moduleB; set { _moduleB = value; N(); } }
    public string DeviceBIp { get => _deviceBIp; set { _deviceBIp = value; N(); } }
    public string Tx { get => _tx; set { _tx = value; N(); } }
    public string Rx { get => _rx; set { _rx = value; N(); } }
    public string Media { get => _media; set { _media = value; N(); } }
    public string Speed { get => _speed; set { _speed = value; N(); } }
    public override string Subnet { get => _subnet; set { _subnet = value; N(); } }
    public override string Status { get => _status; set { _status = value; N(); } }

    /// <summary>Peer device's ASN for BGP neighbor config. Set by code-behind.</summary>
    public string PeerAsn { get; set; } = "";

    public override List<string> BuildConfig(bool sideA)
    {
        var cmds = new List<string>();
        if (string.IsNullOrEmpty(_vlan)) return cmds;
        var myDevice = sideA ? _deviceA : _deviceB;
        var peer = sideA ? _deviceB : _deviceA;
        var peerBldg = sideA ? _buildingB : _buildingA;
        var myIp = (sideA ? _deviceAIp : _deviceBIp)?.Split('/')[0] ?? "";
        var peerIp = (sideA ? _deviceBIp : _deviceAIp)?.Split('/')[0] ?? "";
        var myPort = sideA ? _portA : _portB;
        var desc = $"{myDevice} \u2192 {peer}";

        AddVlanAndL3(cmds, _vlan, $"B2B-{peerBldg}", desc, myIp, _subnet);
        AddPort(cmds, _vlan, myPort, desc);
        AddBgpNeighbor(cmds, peerIp, PeerAsn, peer);
        return cmds;
    }
}
