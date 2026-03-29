using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Central.Core.Models;

public class Server : INotifyPropertyChanged
{
    private int _id;
    private string _building = "", _serverName = "", _serverAs = "", _loopbackIp = "";
    private string _nic1Ip = "", _nic1Router = "", _nic1Subnet = "", _nic1Status = "";
    private string _nic2Ip = "", _nic2Router = "", _nic2Subnet = "", _nic2Status = "";
    private string _nic3Ip = "", _nic3Router = "", _nic3Subnet = "", _nic3Status = "";
    private string _nic4Ip = "", _nic4Router = "", _nic4Subnet = "", _nic4Status = "";
    private string _status = "RESERVED";

    public int Id { get => _id; set { _id = value; N(); } }
    public string Building { get => _building; set { _building = value; N(); } }
    public string ServerName { get => _serverName; set { _serverName = value; N(); } }
    public string ServerAs { get => _serverAs; set { _serverAs = value; N(); } }
    public string LoopbackIp { get => _loopbackIp; set { _loopbackIp = value; N(); } }
    public string Nic1Ip { get => _nic1Ip; set { _nic1Ip = value; N(); } }
    public string Nic1Router { get => _nic1Router; set { _nic1Router = value; N(); } }
    public string Nic1Subnet { get => _nic1Subnet; set { _nic1Subnet = value; N(); } }
    public string Nic1Status { get => _nic1Status; set { _nic1Status = value; N(); } }
    public string Nic2Ip { get => _nic2Ip; set { _nic2Ip = value; N(); } }
    public string Nic2Router { get => _nic2Router; set { _nic2Router = value; N(); } }
    public string Nic2Subnet { get => _nic2Subnet; set { _nic2Subnet = value; N(); } }
    public string Nic2Status { get => _nic2Status; set { _nic2Status = value; N(); } }
    public string Nic3Ip { get => _nic3Ip; set { _nic3Ip = value; N(); } }
    public string Nic3Router { get => _nic3Router; set { _nic3Router = value; N(); } }
    public string Nic3Subnet { get => _nic3Subnet; set { _nic3Subnet = value; N(); } }
    public string Nic3Status { get => _nic3Status; set { _nic3Status = value; N(); } }
    public string Nic4Ip { get => _nic4Ip; set { _nic4Ip = value; N(); } }
    public string Nic4Router { get => _nic4Router; set { _nic4Router = value; N(); } }
    public string Nic4Subnet { get => _nic4Subnet; set { _nic4Subnet = value; N(); } }
    public string Nic4Status { get => _nic4Status; set { _nic4Status = value; N(); } }
    public string Status { get => _status; set { _status = value; N(); } }

    /// <summary>Detail: NIC breakdown for master-detail expansion. Auto-populated.</summary>
    public System.Collections.ObjectModel.ObservableCollection<ServerNicDetail> DetailNics { get; } = new();

    public void PopulateNicDetails()
    {
        DetailNics.Clear();
        if (!string.IsNullOrEmpty(Nic1Ip)) DetailNics.Add(new ServerNicDetail { Nic = "NIC 1", Ip = Nic1Ip, Router = Nic1Router, Subnet = Nic1Subnet, Status = Nic1Status });
        if (!string.IsNullOrEmpty(Nic2Ip)) DetailNics.Add(new ServerNicDetail { Nic = "NIC 2", Ip = Nic2Ip, Router = Nic2Router, Subnet = Nic2Subnet, Status = Nic2Status });
        if (!string.IsNullOrEmpty(Nic3Ip)) DetailNics.Add(new ServerNicDetail { Nic = "NIC 3", Ip = Nic3Ip, Router = Nic3Router, Subnet = Nic3Subnet, Status = Nic3Status });
        if (!string.IsNullOrEmpty(Nic4Ip)) DetailNics.Add(new ServerNicDetail { Nic = "NIC 4", Ip = Nic4Ip, Router = Nic4Router, Subnet = Nic4Subnet, Status = Nic4Status });
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void N([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

public class ServerNicDetail
{
    public string Nic { get; set; } = "";
    public string Ip { get; set; } = "";
    public string Router { get; set; } = "";
    public string Subnet { get; set; } = "";
    public string Status { get; set; } = "";
}
