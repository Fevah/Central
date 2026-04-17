using System.Collections.Generic;

namespace Central.Engine.Models;

public class P2PLink : NetworkLinkBase
{
    private int _id;
    private string _region = "", _building = "", _linkId = "", _vlan = "";
    private string _deviceA = "", _portA = "", _deviceAIp = "", _descA = "", _liveDescA = "";
    private string _deviceB = "", _portB = "", _deviceBIp = "", _descB = "", _liveDescB = "";
    private string _subnet = "", _status = "Active";

    public override int Id { get => _id; set { _id = value; N(); } }
    public string Region { get => _region; set { _region = value; N(); } }
    public string Building { get => _building; set { _building = value; N(); } }
    public override string LinkId { get => _linkId; set { _linkId = value; N(); } }
    public override string Vlan { get => _vlan; set { _vlan = value; N(); } }
    public override string DeviceA { get => _deviceA; set { _deviceA = value; N(); } }
    public string PortA { get => _portA; set { _portA = value; N(); } }
    public string DeviceAIp { get => _deviceAIp; set { _deviceAIp = value; N(); } }
    public string DescA { get => _descA; set { _descA = value; N(); } }
    public string LiveDescA { get => _liveDescA; set { _liveDescA = value; N(); } }
    public override string DeviceB { get => _deviceB; set { _deviceB = value; N(); } }
    public string PortB { get => _portB; set { _portB = value; N(); } }
    public string DeviceBIp { get => _deviceBIp; set { _deviceBIp = value; N(); } }
    public string DescB { get => _descB; set { _descB = value; N(); } }
    public string LiveDescB { get => _liveDescB; set { _liveDescB = value; N(); } }
    public override string Subnet { get => _subnet; set { _subnet = value; N(); } }
    public override string Status { get => _status; set { _status = value; N(); } }

    // Mismatch flags — true when live description differs from planned
    private bool _mismatchA;
    private bool _mismatchB;
    public bool MismatchA { get => _mismatchA; set { _mismatchA = value; N(); N(nameof(DescAColor)); } }
    public bool MismatchB { get => _mismatchB; set { _mismatchB = value; N(); N(nameof(DescBColor)); } }
    public string DescAColor => _mismatchA ? "#EF4444" : "#D4D4D4";
    public string DescBColor => _mismatchB ? "#EF4444" : "#D4D4D4";

    public override List<string> BuildConfig(bool sideA)
    {
        var cmds = new List<string>();
        if (string.IsNullOrEmpty(_vlan)) return cmds;
        var myDevice = sideA ? _deviceA : _deviceB;
        var peer = sideA ? _deviceB : _deviceA;
        var myIp = (sideA ? _deviceAIp : _deviceBIp)?.Split('/')[0] ?? "";
        var myPort = sideA ? _portA : _portB;
        var desc = $"{myDevice} \u2192 {peer}";

        AddVlanAndL3(cmds, _vlan, desc, desc, myIp, _subnet);
        AddPort(cmds, _vlan, myPort, desc);
        return cmds;
    }
}

/// <summary>Shared helpers for link config generation.</summary>
public static class LinkHelper
{
    public static string ExtractPrefix(string? subnet)
    {
        if (string.IsNullOrEmpty(subnet)) return "30";
        var parts = subnet.Split('/');
        return parts.Length == 2 ? parts[1] : "30";
    }
}
