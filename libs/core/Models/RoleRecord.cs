using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Central.Core.Models;

public class RoleRecord : INotifyPropertyChanged
{
    private int    _id;
    private string _name = "";
    private string _description = "";
    private int    _priority;
    private bool   _isSystem;

    // Devices permissions
    private bool _devicesView;
    private bool _devicesEdit;
    private bool _devicesDelete;
    private bool _devicesViewReserved = true;

    // Switches permissions
    private bool _switchesView;
    private bool _switchesEdit;
    private bool _switchesDelete;

    // Admin permissions
    private bool _adminView;
    private bool _adminEdit;
    private bool _adminDelete;

    // Links permissions
    private bool _linksView;
    private bool _linksEdit;
    private bool _linksDelete;

    // BGP/Routing permissions
    private bool _bgpView;
    private bool _bgpEdit;
    private bool _bgpSync;

    // VLANs permissions
    private bool _vlansView;
    private bool _vlansEdit;

    // Tasks permissions
    private bool _tasksView;
    private bool _tasksEdit;
    private bool _tasksDelete;

    // ServiceDesk permissions
    private bool _serviceDeskView;
    private bool _serviceDeskEdit;
    private bool _serviceDeskSync;

    public int    Id          { get => _id;          set { _id = value; OnPropertyChanged(); } }
    public string Name        { get => _name;        set { _name = value; OnPropertyChanged(); } }
    public string Description { get => _description; set { _description = value; OnPropertyChanged(); } }
    public int    Priority    { get => _priority;    set { _priority = value; OnPropertyChanged(); } }
    public bool   IsSystem    { get => _isSystem;    set { _isSystem = value; OnPropertyChanged(); } }

    public bool DevicesView        { get => _devicesView;        set { _devicesView = value; OnPropertyChanged(); } }
    public bool DevicesEdit        { get => _devicesEdit;        set { _devicesEdit = value; OnPropertyChanged(); } }
    public bool DevicesDelete      { get => _devicesDelete;      set { _devicesDelete = value; OnPropertyChanged(); } }
    public bool DevicesViewReserved { get => _devicesViewReserved; set { _devicesViewReserved = value; OnPropertyChanged(); } }

    public bool SwitchesView   { get => _switchesView;   set { _switchesView = value; OnPropertyChanged(); } }
    public bool SwitchesEdit   { get => _switchesEdit;   set { _switchesEdit = value; OnPropertyChanged(); } }
    public bool SwitchesDelete { get => _switchesDelete;  set { _switchesDelete = value; OnPropertyChanged(); } }

    public bool AdminView   { get => _adminView;   set { _adminView = value; OnPropertyChanged(); } }
    public bool AdminEdit   { get => _adminEdit;   set { _adminEdit = value; OnPropertyChanged(); } }
    public bool AdminDelete { get => _adminDelete;  set { _adminDelete = value; OnPropertyChanged(); } }

    public bool LinksView   { get => _linksView;   set { _linksView = value; OnPropertyChanged(); } }
    public bool LinksEdit   { get => _linksEdit;   set { _linksEdit = value; OnPropertyChanged(); } }
    public bool LinksDelete { get => _linksDelete;  set { _linksDelete = value; OnPropertyChanged(); } }

    public bool BgpView { get => _bgpView; set { _bgpView = value; OnPropertyChanged(); } }
    public bool BgpEdit { get => _bgpEdit; set { _bgpEdit = value; OnPropertyChanged(); } }
    public bool BgpSync { get => _bgpSync; set { _bgpSync = value; OnPropertyChanged(); } }

    public bool VlansView { get => _vlansView; set { _vlansView = value; OnPropertyChanged(); } }
    public bool VlansEdit { get => _vlansEdit; set { _vlansEdit = value; OnPropertyChanged(); } }

    public bool TasksView   { get => _tasksView;   set { _tasksView = value; OnPropertyChanged(); } }
    public bool TasksEdit   { get => _tasksEdit;   set { _tasksEdit = value; OnPropertyChanged(); } }
    public bool TasksDelete { get => _tasksDelete;  set { _tasksDelete = value; OnPropertyChanged(); } }

    public bool ServiceDeskView { get => _serviceDeskView; set { _serviceDeskView = value; OnPropertyChanged(); } }
    public bool ServiceDeskEdit { get => _serviceDeskEdit; set { _serviceDeskEdit = value; OnPropertyChanged(); } }
    public bool ServiceDeskSync { get => _serviceDeskSync; set { _serviceDeskSync = value; OnPropertyChanged(); } }

    /// <summary>Permission summary for grid display.</summary>
    public string PermissionSummary
    {
        get
        {
            var count = 0;
            if (DevicesView) count++; if (DevicesEdit) count++; if (DevicesDelete) count++;
            if (SwitchesView) count++; if (SwitchesEdit) count++; if (SwitchesDelete) count++;
            if (AdminView) count++; if (AdminEdit) count++; if (AdminDelete) count++;
            if (LinksView) count++; if (LinksEdit) count++; if (LinksDelete) count++;
            if (BgpView) count++; if (BgpEdit) count++; if (BgpSync) count++;
            if (VlansView) count++; if (VlansEdit) count++;
            if (TasksView) count++; if (TasksEdit) count++; if (TasksDelete) count++;
            if (ServiceDeskView) count++; if (ServiceDeskEdit) count++; if (ServiceDeskSync) count++;
            return $"{count}/22 permissions";
        }
    }

    /// <summary>Detail: users assigned to this role. Populated on expand.</summary>
    public System.Collections.ObjectModel.ObservableCollection<RoleUserDetail> DetailUsers { get; } = new();

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>User detail for role master-detail expansion.</summary>
public class RoleUserDetail
{
    public string Username { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public bool IsActive { get; set; }
    public string LastLogin { get; set; } = "";
}
