using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Central.Core.Models;

public class BgpRecord : INotifyPropertyChanged
{
    private int _id;
    private Guid _switchId;
    private string _building = "";
    private string _hostname = "";
    private string _localAs = "";
    private string _routerId = "";
    private bool _fastExternalFailover;
    private bool _ebgpRequiresPolicy;
    private bool _bestpathMultipathRelax;
    private bool _redistributeConnected;
    private int _maxPaths = 4;
    private int _neighborCount;
    private int _networkCount;
    private DateTime? _lastSynced;

    public int Id { get => _id; set { _id = value; N(); } }
    public Guid SwitchId { get => _switchId; set { _switchId = value; N(); } }
    public string Building { get => _building; set { _building = value; N(); } }
    public string Hostname { get => _hostname; set { _hostname = value; N(); } }
    public string LocalAs { get => _localAs; set { _localAs = value; N(); } }
    public string RouterId { get => _routerId; set { _routerId = value; N(); } }
    public bool FastExternalFailover { get => _fastExternalFailover; set { _fastExternalFailover = value; N(); } }
    public bool EbgpRequiresPolicy { get => _ebgpRequiresPolicy; set { _ebgpRequiresPolicy = value; N(); } }
    public bool BestpathMultipathRelax { get => _bestpathMultipathRelax; set { _bestpathMultipathRelax = value; N(); } }
    public bool RedistributeConnected { get => _redistributeConnected; set { _redistributeConnected = value; N(); } }
    public int MaxPaths { get => _maxPaths; set { _maxPaths = value; N(); } }
    public int NeighborCount { get => _neighborCount; set { _neighborCount = value; N(); } }
    public int NetworkCount { get => _networkCount; set { _networkCount = value; N(); } }
    public DateTime? LastSynced { get => _lastSynced; set { _lastSynced = value; N(); } }

    public List<BgpNeighborRecord> Neighbors { get; set; } = new();
    public List<BgpNetworkRecord> Networks { get; set; } = new();

    /// <summary>Detail neighbors for master-detail expansion. Populated on demand.</summary>
    public System.Collections.ObjectModel.ObservableCollection<BgpNeighborRecord> DetailNeighbors { get; } = new();
    /// <summary>Detail networks for master-detail expansion. Populated on demand.</summary>
    public System.Collections.ObjectModel.ObservableCollection<BgpNetworkRecord> DetailNetworks { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;
    private void N([CallerMemberName] string? p = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
}

public class BgpNeighborRecord : INotifyPropertyChanged
{
    private int _id;
    private int _bgpId;
    private string _neighborIp = "";
    private string _remoteAs = "";
    private string _description = "";
    private bool _bfdEnabled;
    private bool _ipv4Unicast = true;

    public int Id { get => _id; set { _id = value; N(); } }
    public int BgpId { get => _bgpId; set { _bgpId = value; N(); } }
    public string NeighborIp { get => _neighborIp; set { _neighborIp = value; N(); } }
    public string RemoteAs { get => _remoteAs; set { _remoteAs = value; N(); } }
    public string Description { get => _description; set { _description = value; N(); } }
    public bool BfdEnabled { get => _bfdEnabled; set { _bfdEnabled = value; N(); } }
    public bool Ipv4Unicast { get => _ipv4Unicast; set { _ipv4Unicast = value; N(); } }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void N([CallerMemberName] string? p = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
}

public class BgpNetworkRecord : INotifyPropertyChanged
{
    private int _id;
    private int _bgpId;
    private string _networkPrefix = "";

    public int Id { get => _id; set { _id = value; N(); } }
    public int BgpId { get => _bgpId; set { _bgpId = value; N(); } }
    public string NetworkPrefix { get => _networkPrefix; set { _networkPrefix = value; N(); } }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void N([CallerMemberName] string? p = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
}
