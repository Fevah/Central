using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Central.Core.Models;

public class VlanEntry : INotifyPropertyChanged
{
    private int _id;
    private string _block = "", _vlanId = "", _name = "", _networkAddress = "";
    private string _subnet = "", _gateway = "", _usableRange = "", _status = "Active";
    private string _site = "";
    private bool _blockLocked;
    private bool _isBlocked;
    private bool _isDefault;

    public int Id { get => _id; set { _id = value; N(); } }
    public string Block { get => _block; set { _block = value; N(); } }
    public bool BlockLocked
    {
        get => _blockLocked;
        set { _blockLocked = value; N(); N(nameof(BlockLockedText)); N(nameof(RowColor)); }
    }

    /// <summary>True when another VLAN in the same /21 block is locked — this VLAN is blocked</summary>
    public bool IsBlocked
    {
        get => _isBlocked;
        set { _isBlocked = value; N(); N(nameof(RowColor)); N(nameof(BlockLockedText)); }
    }

    public bool IsDefault
    {
        get => _isDefault;
        set { _isDefault = value; N(); N(nameof(RowColor)); }
    }

    public string BlockLockedText => _blockLocked ? "/21 LOCKED" : _isBlocked ? "Blocked /21" : "";

    /// <summary>True when this VLAN is the first in its /21 block (can hold /21)</summary>
    public bool IsBlockRoot
    {
        get
        {
            if (!int.TryParse(_vlanId, out var n)) return false;
            return n <= 255 && n % 8 == 0;
        }
    }

    /// <summary>Row color: red=blocked, amber=root downgraded, green=default, transparent=normal</summary>
    public string RowColor
    {
        get
        {
            if (_isBlocked) return BlockedColor;
            if (IsBlockRoot && _subnet == "/24") return RootDowngradedColor;
            if (_isDefault) return DefaultVlanColor;
            return "Transparent";
        }
    }

    // Configurable colors — set from admin settings at startup
    public static string BlockedColor = "#55EF4444";
    public static string RootDowngradedColor = "#55FF9800";
    public static string BlockLockedColor = "#331565C0";
    public static string DefaultVlanColor = "#3322C55E";
    private int _vlanIdSort = 99999;

    public string VlanId
    {
        get => _vlanId;
        set { _vlanId = value; N(); }
    }

    /// <summary>Explicit sort order from DB — editable so user can reorder</summary>
    public int VlanIdSort
    {
        get => _vlanIdSort;
        set { _vlanIdSort = value; N(); }
    }
    public string Name { get => _name; set { _name = value; N(); } }
    public string NetworkAddress { get => _networkAddress; set { _networkAddress = value; N(); } }
    public string Subnet { get => _subnet; set { _subnet = value; N(); N(nameof(RowColor)); N(nameof(BlockLocked)); N(nameof(IsBlocked)); } }
    public string Gateway { get => _gateway; set { _gateway = value; N(); } }
    public string UsableRange { get => _usableRange; set { _usableRange = value; N(); } }
    public string Status { get => _status; set { _status = value; N(); } }

    /// <summary>Building/site name (e.g. "MEP-91") — used to resolve 10.x. addresses</summary>
    public string Site
    {
        get => _site;
        set
        {
            _site = value;
            N();
            N(nameof(SiteNetwork));
            N(nameof(SiteGateway));
            N(nameof(SitePrefix));
        }
    }

    /// <summary>10.{building}.x.x prefix for this site</summary>
    public string SitePrefix
    {
        get
        {
            if (string.IsNullOrEmpty(_site)) return "";
            var bnum = BuildingNumberMap.GetValueOrDefault(_site, "");
            return string.IsNullOrEmpty(bnum) ? "" : $"10.{bnum}";
        }
    }

    /// <summary>Network address with 10.x replaced by actual building octet</summary>
    public string SiteNetwork
    {
        get
        {
            if (string.IsNullOrEmpty(_site) || string.IsNullOrEmpty(_networkAddress)) return "";
            var bnum = BuildingNumberMap.GetValueOrDefault(_site, "");
            return string.IsNullOrEmpty(bnum) ? "" : _networkAddress.Replace("10.x.", $"10.{bnum}.");
        }
    }

    /// <summary>Gateway with 10.x replaced by actual building octet</summary>
    public string SiteGateway
    {
        get
        {
            if (string.IsNullOrEmpty(_site) || string.IsNullOrEmpty(_gateway)) return "";
            var bnum = BuildingNumberMap.GetValueOrDefault(_site, "");
            return string.IsNullOrEmpty(bnum) ? "" : _gateway.Replace("10.x.", $"10.{bnum}.");
        }
    }

    // MEP-{NN} → second octet mapping
    // MEP-91 → 11, MEP-92 → 12, MEP-93 → 13, MEP-94 → 14, MEP-96 → 16
    public static readonly Dictionary<string, string> BuildingNumberMap = new(System.StringComparer.OrdinalIgnoreCase)
    {
        ["MEP-91"] = "11",
        ["MEP-92"] = "12",
        ["MEP-93"] = "13",
        ["MEP-94"] = "14",
        ["MEP-95"] = "15",
        ["MEP-96"] = "16",
        ["MEP-97"] = "17",
        ["MEP-98"] = "18",
        ["MEP-99"] = "19",
        ["GBG"] = "30",
        ["RAD"] = "50",
        ["EXP-01"] = "31",
        ["EXP-02"] = "32",
        ["EXP-03"] = "33",
        ["EXP-04"] = "34",
        ["UK-RES01"] = "41",
        ["UK-RES02"] = "42",
        ["EU-RES01"] = "51",
        ["EU-RES02"] = "52",
        ["EU-RES03"] = "53",
        ["EU-RES04"] = "54",
        ["US-RES01"] = "61",
        ["US-RES02"] = "62",
        ["US-RES03"] = "63",
        ["US-RES04"] = "64",
    };

    /// <summary>Detail: other sites with this same VLAN ID. Populated on expand.</summary>
    public System.Collections.ObjectModel.ObservableCollection<VlanSiteDetail> DetailSites { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;
    private void N([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
}

/// <summary>Site detail for VLAN master-detail expansion.</summary>
public class VlanSiteDetail
{
    public string Building { get; set; } = "";
    public string VlanName { get; set; } = "";
    public string Gateway { get; set; } = "";
    public string Subnet { get; set; } = "";
    public string Status { get; set; } = "";
}
