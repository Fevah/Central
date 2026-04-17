using System.Collections.Generic;

namespace Central.Engine.Models;

public class FWLink : NetworkLinkBase
{
    private int _id;
    private string _building = "", _linkId = "", _vlan = "";
    private string _switch = "", _switchPort = "", _switchIp = "";
    private string _firewall = "", _firewallPort = "", _firewallIp = "";
    private string _subnet = "", _status = "Active";

    public override int Id { get => _id; set { _id = value; N(); } }
    public string Building { get => _building; set { _building = value; N(); } }
    public override string LinkId { get => _linkId; set { _linkId = value; N(); } }
    public override string Vlan { get => _vlan; set { _vlan = value; N(); } }
    public override string DeviceA { get => _switch; set { _switch = value; N(); } }
    public override string DeviceB { get => _firewall; set { _firewall = value; N(); } }
    public string Switch { get => _switch; set { _switch = value; N(); } }
    public string SwitchPort { get => _switchPort; set { _switchPort = value; N(); } }
    public string SwitchIp { get => _switchIp; set { _switchIp = value; N(); } }
    public string Firewall { get => _firewall; set { _firewall = value; N(); } }
    public string FirewallPort { get => _firewallPort; set { _firewallPort = value; N(); } }
    public string FirewallIp { get => _firewallIp; set { _firewallIp = value; N(); } }
    public override string Subnet { get => _subnet; set { _subnet = value; N(); } }
    public override string Status { get => _status; set { _status = value; N(); } }

    public override List<string> BuildConfig(bool sideA)
    {
        var cmds = new List<string>();
        if (string.IsNullOrEmpty(_vlan)) return cmds;
        var myDevice = sideA ? _switch : _firewall;
        var peer = sideA ? _firewall : _switch;
        var myIp = (sideA ? _switchIp : _firewallIp)?.Split('/')[0] ?? "";
        var myPort = sideA ? _switchPort : _firewallPort;
        var desc = $"{myDevice} \u2192 {peer}";

        AddVlanAndL3(cmds, _vlan, $"FW-{peer}", desc, myIp, _subnet);
        AddPort(cmds, _vlan, myPort, desc);
        return cmds;
    }
}
