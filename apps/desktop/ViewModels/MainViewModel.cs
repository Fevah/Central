using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;
using System.Windows.Input;
using Central.Engine.Auth;
using Central.Persistence;
using Central.Engine.Models;
using Central.Desktop.Services;

namespace Central.Desktop.ViewModels;

public enum ActivePanel { None, Devices, Switches, Admin, Asn, P2P, B2B, FW, Vlans, Mlag, Mstp, ServerAs, IpRanges, Servers, Master, Bgp, Tasks, SprintPlan, QA, MyTasks, Timesheet, Jobs, ServiceDesk, SdGroups, SdTechnicians, SdRequesters, GlobalTenants, GlobalUsers, GlobalSubscriptions, GlobalLicenses }

public class SiteSummary : INotifyPropertyChanged
{
    public string Building { get; set; } = "";
    public string Summary  { get; set; } = "";

    private bool _isSelected = true;
    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected))); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

public class MainViewModel : INotifyPropertyChanged
{
    private readonly DbRepository _repo;
    public DbRepository Repo => _repo;

    /// <summary>Active data service (DirectDb or Api). Falls back to Repo for methods not on IDataService.</summary>
    public Central.Engine.Data.IDataService? DataService => App.Connectivity?.ActiveDataService;

    public ObservableCollection<DeviceRecord>  Devices       { get; } = new();
    public ObservableCollection<SwitchRecord>  Switches      { get; } = new();
    public ObservableCollection<SiteSummary>   SiteSummaries { get; } = new();
    public ObservableCollection<LookupItem>    LookupItems   { get; } = new();
    public ObservableCollection<ConfigRange>  ConfigRanges  { get; } = new();
    public ObservableCollection<MasterDevice> MasterDevices  { get; } = new();
    public ObservableCollection<P2PLink>     P2PLinks      { get; } = new();
    public ObservableCollection<B2BLink>     B2BLinks      { get; } = new();
    public ObservableCollection<FWLink>      FWLinks       { get; } = new();
    public ObservableCollection<ServerAS>    ServerASList   { get; } = new();
    public ObservableCollection<IpRange>     IpRanges      { get; } = new();
    public ObservableCollection<MlagConfig>  MlagConfigs    { get; } = new();
    public ObservableCollection<MstpConfig>  MstpConfigs    { get; } = new();
    public ObservableCollection<VlanEntry>   VlanEntries    { get; } = new();
    public ObservableCollection<Server>     Servers        { get; } = new();
    public ObservableCollection<AppUser>       Users         { get; } = new();
    public ObservableCollection<RoleRecord>   Roles         { get; } = new();
    public ObservableCollection<string>       RoleNames     { get; } = new();
    public ObservableCollection<RoleSiteAccess> RoleSites   { get; } = new();
    public ObservableCollection<SshLogEntry>   SshLogs     { get; } = new();
    public ObservableCollection<AsnDefinition> AsnDefinitions { get; } = new();
    public ObservableCollection<TaskItem> TaskItems { get; } = new();
    public ObservableCollection<TaskProject> TaskProjects { get; } = new();
    public ObservableCollection<Sprint> Sprints { get; } = new();
    public ObservableCollection<AppLogEntry>  AppLogs        { get; } = new();
    public ObservableCollection<BgpRecord>    BgpRecords     { get; } = new();
    public ObservableCollection<BgpNeighborRecord> BgpNeighbors { get; } = new();
    public ObservableCollection<BgpNetworkRecord>  BgpNetworks  { get; } = new();

    // Dropdown option lists for grid editors
    public ObservableCollection<string> StatusOptions     { get; } = new();
    public ObservableCollection<string> DeviceTypeOptions { get; } = new();
    public ObservableCollection<string> BuildingOptions   { get; } = new();
    public ObservableCollection<string> RegionOptions     { get; } = new();
    public ObservableCollection<string> NicStatusOptions  { get; } = new();

    // Commands
    public ICommand? RefreshCommand           { get; set; }
    public ICommand? TestConnectionCommand    { get; set; }
    public ICommand? ExportDevicesCommand        { get; set; }
    public ICommand? OpenWebAppCommand        { get; set; }
    public ICommand? OpenDeviceCommand        { get; set; }
    public ICommand? GroupByBuildingCommand   { get; set; }
    public ICommand? GroupByTypeCommand       { get; set; }
    public ICommand? GroupByRegionCommand     { get; set; }
    public ICommand? ClearGroupsCommand       { get; set; }
    public ICommand? ClearFiltersCommand      { get; set; }
    public ICommand? ToggleDetailsCommand     { get; set; }  // generic Details toggle
    public ICommand? NewRecordCommand         { get; set; }  // generic New
    public ICommand? DeleteRecordCommand      { get; set; }  // generic Delete
    public ICommand? EditRecordCommand        { get; set; }  // generic Edit
    // kept for internal delegation
    public ICommand? AddDeviceCommand         { get; set; }
    public ICommand? DeleteDeviceCommand      { get; set; }
    public ICommand? AddLookupCommand         { get; set; }
    public ICommand? DeleteLookupCommand      { get; set; }

    private ActivePanel _activePanel;
    public ActivePanel ActivePanel
    {
        get => _activePanel;
        set
        {
            _activePanel = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ActivePanelName));
            OnPropertyChanged(nameof(IsLinkPanelActive));
            OnPropertyChanged(nameof(IsSwitchPanelActive));
            OnPropertyChanged(nameof(IsAdminPanelActive));
            OnPropertyChanged(nameof(IsGlobalAdminPanelActive));
            OnPropertyChanged(nameof(IsGlobalTenantsPanelActive));
            OnPropertyChanged(nameof(IsGlobalUsersPanelActive));
            OnPropertyChanged(nameof(IsGlobalSubsPanelActive));
            OnPropertyChanged(nameof(IsGlobalLicensesPanelActive));
        }
    }

    /// <summary>Context tab visibility — Links context tab shows for P2P/B2B/FW panels.</summary>
    public bool IsLinkPanelActive => ActivePanel is ActivePanel.P2P or ActivePanel.B2B or ActivePanel.FW;
    /// <summary>Context tab visibility — Switch context tab shows for Switches panel.</summary>
    public bool IsSwitchPanelActive => ActivePanel == ActivePanel.Switches;
    /// <summary>Context tab visibility — Admin context tab shows for Admin panel.</summary>
    public bool IsAdminPanelActive => ActivePanel == ActivePanel.Admin;

    // Global Admin context tabs — dynamic per active panel
    public bool IsGlobalAdminPanelActive => ActivePanel is ActivePanel.GlobalTenants or ActivePanel.GlobalUsers
        or ActivePanel.GlobalSubscriptions or ActivePanel.GlobalLicenses;
    public bool IsGlobalTenantsPanelActive => ActivePanel == ActivePanel.GlobalTenants;
    public bool IsGlobalUsersPanelActive => ActivePanel == ActivePanel.GlobalUsers;
    public bool IsGlobalSubsPanelActive => ActivePanel == ActivePanel.GlobalSubscriptions;
    public bool IsGlobalLicensesPanelActive => ActivePanel == ActivePanel.GlobalLicenses;

    /// <summary>Human-readable name of the active panel for status bar display.</summary>
    public string ActivePanelName => ActivePanel switch
    {
        ActivePanel.Devices => "IPAM Devices",
        ActivePanel.Switches => "Configured Switches",
        ActivePanel.Admin => "Administration",
        ActivePanel.Asn => "ASN Definitions",
        ActivePanel.P2P => "P2P Links",
        ActivePanel.B2B => "B2B Links",
        ActivePanel.FW => "FW Links",
        ActivePanel.Vlans => "VLANs",
        ActivePanel.Mlag => "MLAG",
        ActivePanel.Mstp => "MSTP",
        ActivePanel.ServerAs => "Server AS",
        ActivePanel.IpRanges => "IP Ranges",
        ActivePanel.Servers => "Servers",
        ActivePanel.Master => "Master Devices",
        _ => ""
    };

    private string _statusText = "Ready";
    public string StatusText
    {
        get => _statusText;
        set { _statusText = value; OnPropertyChanged(); }
    }

    private bool _isLoading;
    public bool IsLoading
    {
        get => _isLoading;
        set { _isLoading = value; OnPropertyChanged(); }
    }

    private string _connectionString;
    public string ConnectionString
    {
        get => _connectionString;
        set { _connectionString = value; OnPropertyChanged(); }
    }

    private string _deviceCountText = "";
    public string DeviceCountText
    {
        get => _deviceCountText;
        set { _deviceCountText = value; OnPropertyChanged(); }
    }

    // DB status: "Green", "Yellow", "Red"
    private string _dbStatus = "Red";
    public string DbStatus
    {
        get => _dbStatus;
        set { _dbStatus = value; OnPropertyChanged(); }
    }

    private string _dbStatusTooltip = "Not connected";
    public string DbStatusTooltip
    {
        get => _dbStatusTooltip;
        set { _dbStatusTooltip = value; OnPropertyChanged(); }
    }

    private DeviceRecord? _selectedDevice;
    public DeviceRecord? SelectedDevice
    {
        get => _selectedDevice;
        set { _selectedDevice = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasSelectedDevice)); }
    }

    public bool HasSelectedDevice => _selectedDevice != null;

    private bool _isAssetPanelOpen;
    public bool IsDetailsPanelOpen
    {
        get => _isAssetPanelOpen;
        set { _isAssetPanelOpen = value; OnPropertyChanged(); OnPropertyChanged(nameof(AssetPanelButtonContent)); }
    }

    public string AssetPanelButtonContent => IsDetailsPanelOpen ? "Hide Details" : "Show Details";

    private bool _hideReserved;
    public bool HideReserved
    {
        get => _hideReserved;
        set { _hideReserved = value; OnPropertyChanged(); }
    }

    private bool _isScanEnabled;
    public bool IsScanEnabled
    {
        get => _isScanEnabled;
        set { _isScanEnabled = value; OnPropertyChanged(); }
    }

    private int _scanIntervalMinutes = 5;
    public int ScanIntervalMinutes
    {
        get => _scanIntervalMinutes;
        set { _scanIntervalMinutes = value > 0 ? value : 1; OnPropertyChanged(); }
    }

    private SwitchRecord? _selectedSwitch;
    public SwitchRecord? SelectedSwitch
    {
        get => _selectedSwitch;
        set { _selectedSwitch = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasSelectedSwitch)); }
    }

    public bool HasSelectedSwitch => _selectedSwitch != null;

    private LookupItem? _selectedLookup;
    public LookupItem? SelectedLookup
    {
        get => _selectedLookup;
        set { _selectedLookup = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasSelectedLookup)); }
    }

    public bool HasSelectedLookup => _selectedLookup != null;

    private AppUser? _selectedUser;
    public AppUser? SelectedUser
    {
        get => _selectedUser;
        set { _selectedUser = value; OnPropertyChanged(); }
    }

    private RoleRecord? _selectedRole;
    public RoleRecord? SelectedRole
    {
        get => _selectedRole;
        set { _selectedRole = value; OnPropertyChanged(); }
    }

    private bool _isUsersPanelOpen;
    public bool IsUsersPanelOpen
    {
        get => _isUsersPanelOpen;
        set { _isUsersPanelOpen = value; OnPropertyChanged(); }
    }

    private bool _isDevicesPanelOpen = true;
    public bool IsDevicesPanelOpen
    {
        get => _isDevicesPanelOpen;
        set { _isDevicesPanelOpen = value; OnPropertyChanged(); }
    }

    private bool _isSwitchesPanelOpen = true;
    public bool IsSwitchesPanelOpen
    {
        get => _isSwitchesPanelOpen;
        set { _isSwitchesPanelOpen = value; OnPropertyChanged(); }
    }

    private bool _isRolesPanelOpen = true;
    public bool IsRolesPanelOpen
    {
        get => _isRolesPanelOpen;
        set { _isRolesPanelOpen = value; OnPropertyChanged(); }
    }

    private bool _isLookupsPanelOpen;
    public bool IsLookupsPanelOpen
    {
        get => _isLookupsPanelOpen;
        set { _isLookupsPanelOpen = value; OnPropertyChanged(); }
    }

    private bool _isMasterPanelOpen;
    public bool IsMasterPanelOpen { get => _isMasterPanelOpen; set { _isMasterPanelOpen = value; OnPropertyChanged(); } }

    private bool _isHierarchyPanelOpen;
    public bool IsHierarchyPanelOpen { get => _isHierarchyPanelOpen; set { _isHierarchyPanelOpen = value; OnPropertyChanged(); } }

    private bool _isPoolsPanelOpen;
    public bool IsPoolsPanelOpen { get => _isPoolsPanelOpen; set { _isPoolsPanelOpen = value; OnPropertyChanged(); } }

    private bool _isAsnPanelOpen, _isP2PPanelOpen, _isB2BPanelOpen, _isFWPanelOpen;
    private bool _isVlansPanelOpen, _isMlagPanelOpen, _isMstpPanelOpen;
    private bool _isServerAsPanelOpen, _isIpRangesPanelOpen, _isServersPanelOpen;

    public bool IsAsnPanelOpen { get => _isAsnPanelOpen; set { _isAsnPanelOpen = value; OnPropertyChanged(); } }
    public bool IsP2PPanelOpen { get => _isP2PPanelOpen; set { _isP2PPanelOpen = value; OnPropertyChanged(); } }
    public bool IsB2BPanelOpen { get => _isB2BPanelOpen; set { _isB2BPanelOpen = value; OnPropertyChanged(); } }
    public bool IsFWPanelOpen { get => _isFWPanelOpen; set { _isFWPanelOpen = value; OnPropertyChanged(); } }
    public bool IsVlansPanelOpen { get => _isVlansPanelOpen; set { _isVlansPanelOpen = value; OnPropertyChanged(); } }

    // VLAN site filtering
    private bool _showDefaultVlans = true;
    public bool ShowDefaultVlans { get => _showDefaultVlans; set { _showDefaultVlans = value; OnPropertyChanged(); } }
    public bool IsMlagPanelOpen { get => _isMlagPanelOpen; set { _isMlagPanelOpen = value; OnPropertyChanged(); } }
    public bool IsMstpPanelOpen { get => _isMstpPanelOpen; set { _isMstpPanelOpen = value; OnPropertyChanged(); } }
    public bool IsServerAsPanelOpen { get => _isServerAsPanelOpen; set { _isServerAsPanelOpen = value; OnPropertyChanged(); } }
    public bool IsIpRangesPanelOpen { get => _isIpRangesPanelOpen; set { _isIpRangesPanelOpen = value; OnPropertyChanged(); } }
    public bool IsServersPanelOpen { get => _isServersPanelOpen; set { _isServersPanelOpen = value; OnPropertyChanged(); } }

    /// <summary>Phase-6f net.server grid panel (distinct from IsServersPanelOpen which drives the legacy public.servers grid).</summary>
    private bool _isNetServersPanelOpen;
    public bool IsNetServersPanelOpen { get => _isNetServersPanelOpen; set { _isNetServersPanelOpen = value; OnPropertyChanged(); } }

    /// <summary>Phase-8 Change Sets governance panel — driven by the Rust
    /// networking-engine over HTTP via <c>NetworkingEngineClient</c>.</summary>
    private bool _isChangeSetsPanelOpen;
    public bool IsChangeSetsPanelOpen { get => _isChangeSetsPanelOpen; set { _isChangeSetsPanelOpen = value; OnPropertyChanged(); } }

    private bool _isSettingsPanelOpen;
    public bool IsSettingsPanelOpen
    {
        get => _isSettingsPanelOpen;
        set { _isSettingsPanelOpen = value; OnPropertyChanged(); }
    }

    private bool _isSshLogsPanelOpen;
    public bool IsSshLogsPanelOpen
    {
        get => _isSshLogsPanelOpen;
        set { _isSshLogsPanelOpen = value; OnPropertyChanged(); }
    }

    private bool _isBuilderPanelOpen;
    public bool IsBuilderPanelOpen
    {
        get => _isBuilderPanelOpen;
        set { _isBuilderPanelOpen = value; OnPropertyChanged(); }
    }

    private bool _isBgpPanelOpen;
    public bool IsBgpPanelOpen
    {
        get => _isBgpPanelOpen;
        set { _isBgpPanelOpen = value; OnPropertyChanged(); }
    }

    private bool _showLiveDescriptions;
    public bool ShowLiveDescriptions
    {
        get => _showLiveDescriptions;
        set { _showLiveDescriptions = value; OnPropertyChanged(); }
    }

    private DeviceRecord? _builderDevice;
    public DeviceRecord? BuilderDevice
    {
        get => _builderDevice;
        set { _builderDevice = value; OnPropertyChanged(); }
    }

    public ObservableCollection<BuilderSection> BuilderSections { get; } = new();

    private bool _isDiagramPanelOpen;
    public bool IsDiagramPanelOpen
    {
        get => _isDiagramPanelOpen;
        set { _isDiagramPanelOpen = value; OnPropertyChanged(); }
    }

    private bool _isAppLogPanelOpen;
    public bool IsAppLogPanelOpen
    {
        get => _isAppLogPanelOpen;
        set { _isAppLogPanelOpen = value; OnPropertyChanged(); }
    }

    private bool _isJobsPanelOpen;
    public bool IsJobsPanelOpen
    {
        get => _isJobsPanelOpen;
        set { _isJobsPanelOpen = value; OnPropertyChanged(); }
    }

    private bool _isDeployPanelOpen;
    public bool IsDeployPanelOpen
    {
        get => _isDeployPanelOpen;
        set { _isDeployPanelOpen = value; OnPropertyChanged(); }
    }

    private bool _isRibbonConfigPanelOpen;
    public bool IsRibbonConfigPanelOpen
    {
        get => _isRibbonConfigPanelOpen;
        set { _isRibbonConfigPanelOpen = value; OnPropertyChanged(); }
    }

    private bool _isServiceDeskPanelOpen;
    public bool IsServiceDeskPanelOpen
    {
        get => _isServiceDeskPanelOpen;
        set { _isServiceDeskPanelOpen = value; OnPropertyChanged(); }
    }

    private bool _isSdOverviewPanelOpen;
    public bool IsSdOverviewPanelOpen
    {
        get => _isSdOverviewPanelOpen;
        set { _isSdOverviewPanelOpen = value; OnPropertyChanged(); }
    }

    private bool _isSdClosuresPanelOpen;
    public bool IsSdClosuresPanelOpen
    {
        get => _isSdClosuresPanelOpen;
        set { _isSdClosuresPanelOpen = value; OnPropertyChanged(); }
    }

    private bool _isSdAgingPanelOpen;
    public bool IsSdAgingPanelOpen
    {
        get => _isSdAgingPanelOpen;
        set { _isSdAgingPanelOpen = value; OnPropertyChanged(); }
    }

    private bool _isSdTeamsPanelOpen;
    public bool IsSdTeamsPanelOpen
    {
        get => _isSdTeamsPanelOpen;
        set { _isSdTeamsPanelOpen = value; OnPropertyChanged(); }
    }

    private bool _isSdGroupsPanelOpen;
    public bool IsSdGroupsPanelOpen
    {
        get => _isSdGroupsPanelOpen;
        set { _isSdGroupsPanelOpen = value; OnPropertyChanged(); }
    }

    private bool _isSdTechniciansPanelOpen;
    public bool IsSdTechniciansPanelOpen
    {
        get => _isSdTechniciansPanelOpen;
        set { _isSdTechniciansPanelOpen = value; OnPropertyChanged(); }
    }

    private bool _isSdRequestersPanelOpen;
    public bool IsSdRequestersPanelOpen
    {
        get => _isSdRequestersPanelOpen;
        set { _isSdRequestersPanelOpen = value; OnPropertyChanged(); }
    }

    // ── Global Admin panels ──
    public ObservableCollection<Dictionary<string, object?>> GlobalTenants { get; } = new();
    public ObservableCollection<Dictionary<string, object?>> GlobalUsers { get; } = new();
    public ObservableCollection<Dictionary<string, object?>> GlobalSubscriptions { get; } = new();
    public ObservableCollection<Dictionary<string, object?>> GlobalLicenses { get; } = new();

    private bool _isGlobalTenantsPanelOpen, _isGlobalUsersPanelOpen, _isGlobalSubscriptionsPanelOpen, _isGlobalLicensesPanelOpen, _isPlatformDashboardPanelOpen;
    public bool IsGlobalTenantsPanelOpen { get => _isGlobalTenantsPanelOpen; set { _isGlobalTenantsPanelOpen = value; OnPropertyChanged(); } }
    public bool IsGlobalUsersPanelOpen { get => _isGlobalUsersPanelOpen; set { _isGlobalUsersPanelOpen = value; OnPropertyChanged(); } }
    public bool IsGlobalSubscriptionsPanelOpen { get => _isGlobalSubscriptionsPanelOpen; set { _isGlobalSubscriptionsPanelOpen = value; OnPropertyChanged(); } }
    public bool IsGlobalLicensesPanelOpen { get => _isGlobalLicensesPanelOpen; set { _isGlobalLicensesPanelOpen = value; OnPropertyChanged(); } }
    public bool IsPlatformDashboardPanelOpen { get => _isPlatformDashboardPanelOpen; set { _isPlatformDashboardPanelOpen = value; OnPropertyChanged(); } }

    private bool _isIntegrationsPanelOpen;
    public bool IsIntegrationsPanelOpen
    {
        get => _isIntegrationsPanelOpen;
        set { _isIntegrationsPanelOpen = value; OnPropertyChanged(); }
    }

    private bool _isTasksPanelOpen;
    public bool IsTasksPanelOpen
    {
        get => _isTasksPanelOpen;
        set { _isTasksPanelOpen = value; OnPropertyChanged(); }
    }

    private bool _isBacklogPanelOpen;
    public bool IsBacklogPanelOpen
    {
        get => _isBacklogPanelOpen;
        set { _isBacklogPanelOpen = value; OnPropertyChanged(); }
    }

    private bool _isSprintPlanPanelOpen;
    public bool IsSprintPlanPanelOpen
    {
        get => _isSprintPlanPanelOpen;
        set { _isSprintPlanPanelOpen = value; OnPropertyChanged(); }
    }

    private bool _isBurndownPanelOpen;
    public bool IsBurndownPanelOpen
    {
        get => _isBurndownPanelOpen;
        set { _isBurndownPanelOpen = value; OnPropertyChanged(); }
    }

    private bool _isKanbanPanelOpen;
    public bool IsKanbanPanelOpen
    {
        get => _isKanbanPanelOpen;
        set { _isKanbanPanelOpen = value; OnPropertyChanged(); }
    }

    private bool _isGanttPanelOpen;
    public bool IsGanttPanelOpen
    {
        get => _isGanttPanelOpen;
        set { _isGanttPanelOpen = value; OnPropertyChanged(); }
    }

    private bool _isQAPanelOpen;
    public bool IsQAPanelOpen
    {
        get => _isQAPanelOpen;
        set { _isQAPanelOpen = value; OnPropertyChanged(); }
    }

    private bool _isQADashboardPanelOpen;
    public bool IsQADashboardPanelOpen
    {
        get => _isQADashboardPanelOpen;
        set { _isQADashboardPanelOpen = value; OnPropertyChanged(); }
    }

    private bool _isReportBuilderPanelOpen;
    public bool IsReportBuilderPanelOpen
    {
        get => _isReportBuilderPanelOpen;
        set { _isReportBuilderPanelOpen = value; OnPropertyChanged(); }
    }

    private bool _isTaskDashboardPanelOpen;
    public bool IsTaskDashboardPanelOpen
    {
        get => _isTaskDashboardPanelOpen;
        set { _isTaskDashboardPanelOpen = value; OnPropertyChanged(); }
    }

    private bool _isTimesheetPanelOpen;
    public bool IsTimesheetPanelOpen
    {
        get => _isTimesheetPanelOpen;
        set { _isTimesheetPanelOpen = value; OnPropertyChanged(); }
    }

    private bool _isActivityFeedPanelOpen;
    public bool IsActivityFeedPanelOpen
    {
        get => _isActivityFeedPanelOpen;
        set { _isActivityFeedPanelOpen = value; OnPropertyChanged(); }
    }

    private bool _isMyTasksPanelOpen;
    public bool IsMyTasksPanelOpen
    {
        get => _isMyTasksPanelOpen;
        set { _isMyTasksPanelOpen = value; OnPropertyChanged(); }
    }

    private bool _isPortfolioPanelOpen;
    public bool IsPortfolioPanelOpen
    {
        get => _isPortfolioPanelOpen;
        set { _isPortfolioPanelOpen = value; OnPropertyChanged(); }
    }

    private bool _isTaskImportPanelOpen;
    public bool IsTaskImportPanelOpen
    {
        get => _isTaskImportPanelOpen;
        set { _isTaskImportPanelOpen = value; OnPropertyChanged(); }
    }

    private bool _isTaskDetailPanelOpen;
    public bool IsTaskDetailPanelOpen
    {
        get => _isTaskDetailPanelOpen;
        set { _isTaskDetailPanelOpen = value; OnPropertyChanged(); }
    }

    private string _runningConfigText = "";
    public string RunningConfigText
    {
        get => _runningConfigText;
        set { _runningConfigText = value; OnPropertyChanged(); }
    }

    public ObservableCollection<SwitchInterface> SwitchInterfaces { get; } = new();

    private ConfigRange? _selectedRange;
    public ConfigRange? SelectedRange
    {
        get => _selectedRange;
        set { _selectedRange = value; OnPropertyChanged(); }
    }

    private AsnDefinition? _selectedAsn;
    public AsnDefinition? SelectedAsn
    {
        get => _selectedAsn;
        set { _selectedAsn = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasSelectedAsn)); }
    }
    public bool HasSelectedAsn => _selectedAsn != null;

    // ── Permission properties (read from UserSession) ────────────────────
    public bool   CanViewDevices   => AuthContext.Instance.CanView("devices");
    public bool   CanEditDevices   => AuthContext.Instance.CanEdit("devices");
    public bool   CanDeleteDevices => AuthContext.Instance.CanDelete("devices");
    public bool   CanViewSwitches  => AuthContext.Instance.CanView("switches");
    public bool   CanEditSwitches  => AuthContext.Instance.CanEdit("switches");
    public bool   CanDeleteSwitches=> AuthContext.Instance.CanDelete("switches");
    public bool   CanViewAdmin     => AuthContext.Instance.CanView("admin");
    public bool   CanEditAdmin     => AuthContext.Instance.CanEdit("admin");
    public bool   CanDeleteAdmin   => AuthContext.Instance.CanDelete("admin");
    public bool   IsAdmin          => AuthContext.Instance.IsAdmin;
    public string CurrentUserDisplay => AuthContext.Instance.CurrentUser?.DisplayName ?? "Unknown";
    public string CurrentUserRole    => AuthContext.Instance.CurrentUser?.RoleName ?? "";

    /// <summary>Connection/auth mode text for status bar.</summary>
    public string ConnectionModeText => AuthContext.Instance.AuthState switch
    {
        AuthStates.Windows => "Online",
        AuthStates.Password => "Online",
        AuthStates.EntraId => "Entra ID",
        AuthStates.Okta => "Okta",
        AuthStates.Saml => "SSO",
        AuthStates.ApiToken => "API",
        AuthStates.Offline => "Offline",
        _ => "Not Connected"
    };

    /// <summary>Connection/auth mode colour for status bar indicator dot.</summary>
    public string ConnectionModeColor => AuthContext.Instance.AuthState switch
    {
        AuthStates.Windows or AuthStates.Password => "#22C55E",    // Green — direct DB
        AuthStates.EntraId or AuthStates.Okta or AuthStates.Saml => "#3B82F6", // Blue — external IdP
        AuthStates.ApiToken => "#8B5CF6",  // Purple — API mode
        AuthStates.Offline => "#F59E0B",   // Amber — offline
        _ => "#6B7280"                     // Grey — not authenticated
    };

    public MainViewModel()
    {
        _connectionString =
            Environment.GetEnvironmentVariable("CENTRAL_DSN") ??
            "Host=localhost;Port=5432;Database=central;Username=central;Password=central";
        _repo = new DbRepository(_connectionString);
    }

    public async Task CheckDbStatusAsync()
    {
        DbStatus = "Yellow";
        DbStatusTooltip = "Checking connection…";
        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            bool ok = await _repo.TestConnectionAsync();
            sw.Stop();
            if (ok)
            {
                DbStatus = "Green";
                DbStatusTooltip = $"Connected  ·  {sw.ElapsedMilliseconds} ms";
            }
            else
            {
                DbStatus = "Red";
                DbStatusTooltip = "Connection failed";
            }
        }
        catch (Exception ex)
        {
            DbStatus = "Red";
            DbStatusTooltip = $"Error: {ex.Message}";
        }
    }

    // ── Deferred loading state ──────────────────────────────────────────

    private readonly HashSet<string> _loadedPanels = new();
    private bool _deferredBgRunning;

    /// <summary>Load critical path data (Devices, Switches, Lookups) then kick off background load.</summary>
    public async Task LoadAllAsync()
    {
        IsLoading = true;
        StatusText = "Loading…";
        DbStatus = "Yellow";
        DbStatusTooltip = "Loading data…";
        _loadedPanels.Clear();
        try
        {
            // Timeout protection — don't hang forever if DB goes away mid-load
            using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(30));

            var sites = AuthContext.Instance.AllowedSites.Count > 0 ? AuthContext.Instance.AllowedSites.ToList() : null;
            var noRes = !AuthContext.Instance.CanViewReserved;
            var useApi = DataService != null && App.Connectivity?.Mode == Central.Engine.Data.DataServiceMode.Api;

            // ── Critical path: Devices + Switches + Lookups ──
            List<DeviceRecord> devices;
            List<SwitchRecord> switches;
            List<LookupItem> lookupItems;
            Dictionary<string, List<string>> lookups;

            if (useApi)
            {
                devices = await DataService!.GetDevicesAsync<DeviceRecord>(sites?.ToArray());
                switches = await DataService.GetSwitchesAsync<SwitchRecord>();
                lookupItems = await DataService.GetLookupsAsync<LookupItem>();
                lookups = lookupItems.GroupBy(l => l.Category)
                    .ToDictionary(g => g.Key, g => g.Select(i => i.Value).ToList());
            }
            else
            {
                devices = await _repo.GetDevicesAsync(sites, noRes);
                switches = await _repo.GetSwitchesAsync(sites);
                lookups = await _repo.GetLookupsAsync();
                lookupItems = await _repo.GetLookupItemsAsync();
            }

            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                Devices.Clear();
                foreach (var d in devices) Devices.Add(d);
                Switches.Clear();
                foreach (var s in switches) Switches.Add(s);
                LookupItems.Clear();
                foreach (var l in lookupItems) LookupItems.Add(l);
                RefreshLookupOptions(lookups);

                // Apply configurable colors from lookup_values
                foreach (var c in lookupItems.Where(l => l.Category == "color"))
                {
                    var parts = c.Value.Split('=', 2);
                    if (parts.Length != 2) continue;
                    switch (parts[0])
                    {
                        case "vlan_blocked": VlanEntry.BlockedColor = parts[1]; break;
                        case "vlan_root_downgraded": VlanEntry.RootDowngradedColor = parts[1]; break;
                        case "vlan_block_locked": VlanEntry.BlockLockedColor = parts[1]; break;
                        case "vlan_default": VlanEntry.DefaultVlanColor = parts[1]; break;
                    }
                }

                SiteSummaries.Clear();
                foreach (var grp in devices.GroupBy(d => d.Building).OrderBy(g => g.Key))
                {
                    var active   = grp.Count(d => d.Status == "Active");
                    var reserved = grp.Count(d => d.Status != "Active");
                    SiteSummaries.Add(new SiteSummary { Building = grp.Key, Summary = $"{active} active · {reserved} reserved" });
                }
                DeviceCountText = $"{devices.Count} devices";
            });

            _loadedPanels.Add("devices");
            _loadedPanels.Add("switches");
            _loadedPanels.Add("lookups");

            DbStatus = "Green";
            DbStatusTooltip = $"Connected  ·  {devices.Count} devices loaded";
            StatusText = $"Loaded {devices.Count} devices · {switches.Count} configured  ·  {DateTime.Now:HH:mm:ss}";

            // ── Background: load remaining panels ──
            _ = LoadDeferredInBackgroundAsync();
        }
        catch (OperationCanceledException)
        {
            DbStatus = "Red";
            DbStatusTooltip = "Load timed out — database may be unreachable";
            StatusText = "Load timed out — check database connectivity";
            if (App.Connectivity != null)
            {
                App.IsDbOnline = false;
                App.Connectivity.StartRetryLoop(intervalSeconds: 10);
            }
        }
        catch (Exception ex)
        {
            AppLogger.LogException("DB", ex, "MainViewModel.LoadAllAsync");
            DbStatus = "Red";
            DbStatusTooltip = $"Error: {ex.Message}";
            StatusText = $"Error: {ex.Message}";

            // If it's a connection error, start retry loop
            if (ex is Npgsql.NpgsqlException && App.Connectivity != null)
            {
                App.IsDbOnline = false;
                App.Connectivity.StartRetryLoop(intervalSeconds: 10);
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    // ── Targeted reload methods for SignalR real-time updates ──────────────
    // Each method checks DataService mode — API mode uses typed HTTP calls,
    // DirectDb mode uses DbRepository. This is the Phase 7 migration path.

    public bool UseApi => DataService != null && App.Connectivity?.Mode == Central.Engine.Data.DataServiceMode.Api;

    private void ReplaceCollection<T>(ObservableCollection<T> target, List<T> source)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            target.Clear();
            foreach (var item in source) target.Add(item);
        });
    }

    public async Task ReloadDevicesAsync()
    {
        var sites = AuthContext.Instance.AllowedSites.Count > 0 ? AuthContext.Instance.AllowedSites.ToArray() : null;
        var devices = UseApi
            ? await DataService!.GetDevicesAsync<DeviceRecord>(sites)
            : await _repo.GetDevicesAsync(sites?.ToList(), !AuthContext.Instance.CanViewReserved);
        ReplaceCollection(Devices, devices);
        DeviceCountText = $"{devices.Count} devices";
    }

    public async Task ReloadSwitchesAsync()
    {
        var switches = UseApi
            ? await DataService!.GetSwitchesAsync<SwitchRecord>()
            : await _repo.GetSwitchesAsync(AuthContext.Instance.AllowedSites.Count > 0 ? AuthContext.Instance.AllowedSites.ToList() : null);
        ReplaceCollection(Switches, switches);
    }

    public async Task ReloadP2PLinksAsync()
    {
        var links = UseApi
            ? await DataService!.GetP2PLinksAsync<P2PLink>()
            : await _repo.GetP2PLinksAsync();
        ReplaceCollection(P2PLinks, links);
    }

    public async Task ReloadB2BLinksAsync()
    {
        var links = UseApi
            ? await DataService!.GetB2BLinksAsync<B2BLink>()
            : await _repo.GetB2BLinksAsync();
        ReplaceCollection(B2BLinks, links);
    }

    public async Task ReloadFWLinksAsync()
    {
        var links = UseApi
            ? await DataService!.GetFWLinksAsync<FWLink>()
            : await _repo.GetFWLinksAsync();
        ReplaceCollection(FWLinks, links);
    }

    public async Task ReloadVlansAsync()
    {
        if (UseApi)
        {
            var sites = SiteSummaries.Where(s => s.IsSelected).Select(s => s.Building).ToArray();
            var vlans = await DataService!.GetVlansAsync<VlanEntry>(sites.Length > 0 ? sites : null);
            ReplaceCollection(VlanEntries, vlans);
        }
        else
        {
            var selectedSites = SiteSummaries.Where(s => s.IsSelected).Select(s => s.Building).ToList();
            var noRes = !AuthContext.Instance.CanViewReserved;
            var vlans = await _repo.GetVlanInventoryAsync(noRes, selectedSites.Count > 0 ? selectedSites : null, ShowDefaultVlans);
            ReplaceCollection(VlanEntries, vlans);
        }
    }

    public async Task ReloadBgpAsync()
    {
        var bgpRecs = UseApi
            ? await DataService!.GetBgpConfigsAsync<BgpRecord>()
            : await _repo.GetBgpRecordsAsync(AuthContext.Instance.AllowedSites.Count > 0 ? AuthContext.Instance.AllowedSites.ToList() : null);
        ReplaceCollection(BgpRecords, bgpRecs);
    }

    public async Task ReloadUsersAsync()
    {
        var users = UseApi
            ? await DataService!.GetUsersAsync<AppUser>()
            : await _repo.GetAllUsersAsync();
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            Users.Clear();
            foreach (var u in users) Users.Add(u);
        });
    }

    public async Task ReloadRolesAsync()
    {
        var roles = UseApi
            ? await DataService!.GetRolesAsync<RoleRecord>()
            : await _repo.GetAllRolesAsync();
        ReplaceCollection(Roles, roles);
    }

    public async Task ReloadLookupsAsync()
    {
        var items = UseApi
            ? await DataService!.GetLookupsAsync<LookupItem>()
            : await _repo.GetLookupItemsAsync();
        ReplaceCollection(LookupItems, items);
    }

    public async Task ReloadAsnAsync()
    {
        // ASN not on IDataService yet — always use Repo
        var items = await _repo.GetAsnDefinitionsAsync();
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            AsnDefinitions.Clear();
            foreach (var a in items) AsnDefinitions.Add(a);
        });
    }

    public async Task ReloadServersAsync()
    {
        var items = await _repo.GetServersAsync();
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            Servers.Clear();
            foreach (var s in items) Servers.Add(s);
        });
    }

    public async Task ReloadConfigRangesAsync()
    {
        var items = await _repo.GetConfigRangesAsync();
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            ConfigRanges.Clear();
            foreach (var c in items) ConfigRanges.Add(c);
        });
    }

    public async Task LoadTasksAsync(int? projectId = null)
    {
        try
        {
            var tasks = await _repo.GetTasksAsync(projectId);
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                TaskItems.Clear();
                foreach (var t in tasks) TaskItems.Add(t);
            });
        }
        catch (Exception ex) { StatusText = $"Tasks load error: {ex.Message}"; }
    }

    public async Task LoadTaskProjectsAsync()
    {
        try
        {
            var projects = await _repo.GetTaskProjectsAsync();
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                TaskProjects.Clear();
                foreach (var p in projects) TaskProjects.Add(p);
            });
        }
        catch (Exception ex) { StatusText = $"Projects load error: {ex.Message}"; }
    }

    public async Task LoadSprintsAsync(int? projectId = null)
    {
        try
        {
            var sprints = await _repo.GetSprintsAsync(projectId);
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                Sprints.Clear();
                foreach (var s in sprints) Sprints.Add(s);
            });
        }
        catch (Exception ex) { StatusText = $"Sprints load error: {ex.Message}"; }
    }

    public async Task SaveTaskAsync(TaskItem task)
    {
        try
        {
            await _repo.UpsertTaskAsync(task);
            StatusText = $"Task saved: {task.Title}  ·  {DateTime.Now:HH:mm:ss}";
            Central.Engine.Shell.PanelMessageBus.Publish(
                new Central.Engine.Shell.DataModifiedMessage("tasks", "Task", "Update"));
        }
        catch (Exception ex) { StatusText = $"Task save error: {ex.Message}"; }
    }

    public async Task DeleteTaskAsync(TaskItem task)
    {
        try
        {
            if (task.Id > 0)
                await _repo.DeleteTaskAsync(task.Id);
            StatusText = $"Task deleted: {task.Title}";
            Central.Engine.Shell.PanelMessageBus.Publish(
                new Central.Engine.Shell.DataModifiedMessage("tasks", "Task", "Delete"));
        }
        catch (Exception ex) { StatusText = $"Task delete error: {ex.Message}"; }
    }

    public async Task RefreshSelectedSwitchDetailsAsync()
    {
        if (SelectedSwitch == null) return;
        var cfg = await _repo.GetLatestRunningConfigAsync(SelectedSwitch.Id);
        if (cfg != null) RunningConfigText = cfg;
    }

    /// <summary>Background-load all deferred panels sequentially (non-blocking).</summary>
    private async Task LoadDeferredInBackgroundAsync()
    {
        if (_deferredBgRunning) return;
        _deferredBgRunning = true;
        try
        {
            await LoadPanelDataAsync("p2p");
            await LoadPanelDataAsync("b2b");
            await LoadPanelDataAsync("fw");
            await LoadPanelDataAsync("vlans");
            await LoadPanelDataAsync("asn");
            await LoadPanelDataAsync("master");
            await LoadPanelDataAsync("serveras");
            await LoadPanelDataAsync("ipranges");
            await LoadPanelDataAsync("servers");
            await LoadPanelDataAsync("mlag");
            await LoadPanelDataAsync("mstp");
            await LoadPanelDataAsync("configranges");
            await LoadPanelDataAsync("bgp");
            await LoadPanelDataAsync("admin");
        }
        catch (Exception ex)
        {
            AppLogger.LogException("DB", ex, "LoadDeferredInBackgroundAsync");
        }
        finally
        {
            _deferredBgRunning = false;
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
                StatusText = $"All data loaded  ·  {DateTime.Now:HH:mm:ss}");
        }
    }

    /// <summary>Load data for a specific panel if not already loaded.</summary>
    public async Task LoadPanelDataAsync(string panelKey, bool force = false)
    {
        if (!force && _loadedPanels.Contains(panelKey)) return;
        _loadedPanels.Add(panelKey);

        var sites = AuthContext.Instance.AllowedSites.Count > 0 ? AuthContext.Instance.AllowedSites.ToList() : null;
        var noRes = !AuthContext.Instance.CanViewReserved;

        try
        {
            switch (panelKey)
            {
                case "p2p":
                    var p2p = await _repo.GetP2PLinksAsync(sites, noRes);
                    Dispatch(() => { P2PLinks.Clear(); foreach (var x in p2p) P2PLinks.Add(x); });
                    break;
                case "b2b":
                    var b2b = await _repo.GetB2BLinksAsync(sites, noRes);
                    Dispatch(() => { B2BLinks.Clear(); foreach (var x in b2b) B2BLinks.Add(x); });
                    break;
                case "fw":
                    var fw = await _repo.GetFWLinksAsync(sites, noRes);
                    Dispatch(() => { FWLinks.Clear(); foreach (var x in fw) FWLinks.Add(x); });
                    break;
                case "vlans":
                    var vlanSites = SiteSummaries.Where(s => s.IsSelected).Select(s => s.Building).ToList();
                    // Auto-provision site VLAN rows from Default template
                    foreach (var vs in vlanSites)
                        await _repo.GenerateSiteVlansAsync(vs);
                    var vlans = await _repo.GetVlanInventoryAsync(noRes, vlanSites.Count > 0 ? vlanSites : null, ShowDefaultVlans);
                    Dispatch(() => { VlanEntries.Clear(); foreach (var x in vlans) VlanEntries.Add(x); PropagateBlockLocked(); });
                    break;
                case "asn":
                    var asn = await _repo.GetAsnDefinitionsAsync();
                    Dispatch(() => { AsnDefinitions.Clear(); foreach (var x in asn) AsnDefinitions.Add(x); });
                    break;
                case "master":
                    var master = await _repo.GetMasterDevicesAsync(sites, noRes);
                    Dispatch(() => { MasterDevices.Clear(); foreach (var x in master) MasterDevices.Add(x); });
                    break;
                case "serveras":
                    var sa = await _repo.GetServerASAsync(sites, noRes);
                    Dispatch(() => { ServerASList.Clear(); foreach (var x in sa) ServerASList.Add(x); });
                    break;
                case "ipranges":
                    var ir = await _repo.GetIpRangesAsync(noRes);
                    Dispatch(() => { IpRanges.Clear(); foreach (var x in ir) IpRanges.Add(x); });
                    break;
                case "servers":
                    var srv = await _repo.GetServersAsync(sites, noRes);
                    Dispatch(() => { Servers.Clear(); foreach (var x in srv) Servers.Add(x); });
                    break;
                case "mlag":
                    var mlag = await _repo.GetMlagConfigAsync(sites, noRes);
                    Dispatch(() => { MlagConfigs.Clear(); foreach (var x in mlag) MlagConfigs.Add(x); });
                    break;
                case "mstp":
                    var mstp = await _repo.GetMstpConfigAsync(sites, noRes);
                    Dispatch(() => { MstpConfigs.Clear(); foreach (var x in mstp) MstpConfigs.Add(x); });
                    break;
                case "bgp":
                    var bgpRecs = await _repo.GetBgpRecordsAsync(sites);
                    Dispatch(() => { BgpRecords.Clear(); foreach (var x in bgpRecs) BgpRecords.Add(x); });
                    break;
                case "configranges":
                    if (!AuthContext.Instance.IsAdmin) break;
                    var cr = await _repo.GetConfigRangesAsync();
                    Dispatch(() => { ConfigRanges.Clear(); foreach (var x in cr) ConfigRanges.Add(x); });
                    break;
                case "admin":
                    if (!AuthContext.Instance.IsAdmin) break;
                    var users = await _repo.GetAllUsersAsync();
                    var roles = await _repo.GetAllRolesAsync();
                    var roleNames = await _repo.GetRoleNamesAsync();
                    var sshLogs = await _repo.GetSshLogsAsync();
                    var appLogs = await _repo.GetAppLogsAsync();
                    Dispatch(() =>
                    {
                        Users.Clear(); foreach (var u in users) Users.Add(u);
                        Roles.Clear(); foreach (var r in roles) Roles.Add(r);
                        RoleNames.Clear(); foreach (var rn in roleNames) RoleNames.Add(rn);
                        SshLogs.Clear(); foreach (var sl in sshLogs) SshLogs.Add(sl);
                        AppLogs.Clear(); foreach (var al in appLogs) AppLogs.Add(al);
                    });
                    break;
                case "global_tenants":
                    var tenants = await _repo.GetGlobalTenantsAsync();
                    Dispatch(() => { GlobalTenants.Clear(); foreach (var t in tenants) GlobalTenants.Add(t); });
                    break;
                case "global_users":
                    var gUsers = await _repo.GetGlobalUsersAsync();
                    Dispatch(() => { GlobalUsers.Clear(); foreach (var u in gUsers) GlobalUsers.Add(u); });
                    break;
                case "global_subscriptions":
                    var subs = await _repo.GetGlobalSubscriptionsAsync();
                    Dispatch(() => { GlobalSubscriptions.Clear(); foreach (var s in subs) GlobalSubscriptions.Add(s); });
                    break;
                case "global_licenses":
                    var lics = await _repo.GetGlobalLicensesAsync();
                    Dispatch(() => { GlobalLicenses.Clear(); foreach (var l in lics) GlobalLicenses.Add(l); });
                    break;
            }
        }
        catch (Exception ex)
        {
            AppLogger.LogException("DB", ex, $"LoadPanelDataAsync({panelKey})");
            _loadedPanels.Remove(panelKey); // allow retry
        }
    }

    /// <summary>Ensure a panel's data is loaded when the panel is first activated.</summary>
    public async Task EnsurePanelLoadedAsync(ActivePanel panel)
    {
        var key = panel switch
        {
            ActivePanel.P2P      => "p2p",
            ActivePanel.B2B      => "b2b",
            ActivePanel.FW       => "fw",
            ActivePanel.Vlans    => "vlans",
            ActivePanel.Asn      => "asn",
            ActivePanel.Master   => "master",
            ActivePanel.ServerAs => "serveras",
            ActivePanel.IpRanges => "ipranges",
            ActivePanel.Servers  => "servers",
            ActivePanel.Mlag     => "mlag",
            ActivePanel.Mstp     => "mstp",
            ActivePanel.Admin    => "admin",
            ActivePanel.GlobalTenants => "global_tenants",
            ActivePanel.GlobalUsers => "global_users",
            ActivePanel.GlobalSubscriptions => "global_subscriptions",
            ActivePanel.GlobalLicenses => "global_licenses",
            _ => null
        };
        if (key != null) await LoadPanelDataAsync(key);
    }

    /// <summary>When a /21 block has BlockLocked=true on any VLAN, mark all siblings as IsBlocked.</summary>
    public void PropagateBlockLocked()
    {
        // Group by /21 block, find which blocks have a locked VLAN
        var groups = VlanEntries
            .Where(v => !string.IsNullOrEmpty(v.Block))
            .GroupBy(v => v.Block);

        foreach (var grp in groups)
        {
            var lockedVlan = grp.FirstOrDefault(v => v.BlockLocked);
            foreach (var v in grp)
            {
                // Mark siblings as blocked if there's a locked VLAN in the block (but not the locked VLAN itself)
                v.IsBlocked = lockedVlan != null && !v.BlockLocked;
            }
        }
    }

    private static void Dispatch(Action action)
        => System.Windows.Application.Current.Dispatcher.Invoke(action);

    private void RefreshLookupOptions(Dictionary<string, List<string>> lookups)
    {
        RefreshCollection(StatusOptions,     lookups.GetValueOrDefault("status",      new()));
        RefreshCollection(DeviceTypeOptions, lookups.GetValueOrDefault("device_type", new()));
        RefreshCollection(BuildingOptions,   lookups.GetValueOrDefault("building",    new()));
        RefreshCollection(RegionOptions,     lookups.GetValueOrDefault("region",      new()));
        RefreshCollection(NicStatusOptions,  lookups.GetValueOrDefault("nic_status",  new()));
    }

    private static void RefreshCollection(ObservableCollection<string> col, List<string> newItems)
    {
        col.Clear();
        foreach (var item in newItems) col.Add(item);
    }

    public async Task SaveDeviceAsync(DeviceRecord device)
    {
        // Validate required fields
        var errors = Central.Engine.Widgets.GridValidationHelper.Validate(device,
            ("SwitchName", "Device name is required"),
            ("Building", "Building is required"));
        if (errors.Count > 0)
        {
            StatusText = $"Validation: {errors[0].Error}";
            return;
        }

        try
        {
            bool isNew = string.IsNullOrEmpty(device.Id);
            if (UseApi)
                await DataService!.UpsertDeviceAsync(device);
            else if (isNew)
                await _repo.InsertDeviceAsync(device);
            else
                await _repo.UpdateDeviceAsync(device);

            StatusText = $"Saved: {device.SwitchName}  ·  {DateTime.Now:HH:mm:ss}";
            Central.Engine.Shell.PanelMessageBus.Publish(
                new Central.Engine.Shell.DataModifiedMessage("devices", "Device", isNew ? "Add" : "Update"));
            _ = Central.Engine.Services.AuditService.Instance.LogAsync(
                isNew ? "Create" : "Update", "Device", device.Id, device.SwitchName);
        }
        catch (Exception ex)
        {
            AppLogger.LogException("Device", ex, "MainViewModel.SaveDeviceAsync");
            StatusText = $"Save error: {ex.Message}";
            System.Windows.MessageBox.Show(ex.Message, "Save Error",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    public async Task DeleteDeviceAsync(DeviceRecord device)
    {
        if (string.IsNullOrEmpty(device.Id)) { Devices.Remove(device); return; }
        try
        {
            if (UseApi && int.TryParse(device.Id, out var devId))
                await DataService!.SoftDeleteDeviceAsync(devId);
            else
                await _repo.DeleteDeviceAsync(device.Id);
            Devices.Remove(device);
            StatusText = $"Deleted: {device.SwitchName}  ·  {DateTime.Now:HH:mm:ss}";
            Central.Engine.Shell.PanelMessageBus.Publish(
                new Central.Engine.Shell.DataModifiedMessage("devices", "Device", "Delete"));
            _ = Central.Engine.Services.AuditService.Instance.LogDeleteAsync("Device", device.Id ?? "", device.SwitchName);

            // Record undo — re-add on undo, re-delete on redo
            var snapshot = device;
            Central.Engine.Services.UndoService.Instance.RecordRemove(
                Devices, snapshot, Devices.Count,
                $"Delete device {device.SwitchName}");
        }
        catch (Exception ex)
        {
            StatusText = $"Delete error: {ex.Message}";
            System.Windows.MessageBox.Show(ex.Message, "Delete Error",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    public async Task SaveLookupAsync(LookupItem item)
    {
        try
        {
            await _repo.UpsertLookupAsync(item);
            // Refresh dropdown options
            var lookups = await _repo.GetLookupsAsync();
            System.Windows.Application.Current.Dispatcher.Invoke(() => RefreshLookupOptions(lookups));
            StatusText = $"Lookup saved: {item.Category} / {item.Value}";
            Central.Engine.Shell.PanelMessageBus.Publish(
                new Central.Engine.Shell.DataModifiedMessage("admin", "Lookup", "Update"));
        }
        catch (Exception ex)
        {
            StatusText = $"Lookup save error: {ex.Message}";
            System.Windows.MessageBox.Show(ex.Message, "Save Error",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    public async Task DeleteLookupAsync(LookupItem item)
    {
        try
        {
            if (item.Id > 0) await _repo.DeleteLookupAsync(item.Id);
            LookupItems.Remove(item);
            Central.Engine.Services.UndoService.Instance.RecordRemove(
                LookupItems, item, LookupItems.Count, $"Delete lookup {item.Category}/{item.Value}");
            var lookups = await _repo.GetLookupsAsync();
            System.Windows.Application.Current.Dispatcher.Invoke(() => RefreshLookupOptions(lookups));
            StatusText = $"Lookup deleted: {item.Category} / {item.Value}";
        }
        catch (Exception ex)
        {
            StatusText = $"Lookup delete error: {ex.Message}";
            System.Windows.MessageBox.Show(ex.Message, "Delete Error",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    // ── Config Range CRUD (Settings panel) ──────────────────────────────

    public async Task SaveConfigRangeAsync(ConfigRange item)
    {
        try
        {
            await _repo.UpsertConfigRangeAsync(item);
            StatusText = $"Range saved: {item.Category} / {item.Name}";
            Central.Engine.Shell.PanelMessageBus.Publish(
                new Central.Engine.Shell.DataModifiedMessage("admin", "ConfigRange", "Update"));
        }
        catch (Exception ex)
        {
            StatusText = $"Range save error: {ex.Message}";
        }
    }

    public async Task DeleteConfigRangeAsync(ConfigRange item)
    {
        try
        {
            if (item.Id > 0) await _repo.DeleteConfigRangeAsync(item.Id);
            ConfigRanges.Remove(item);
            StatusText = $"Range deleted: {item.Category} / {item.Name}";
        }
        catch (Exception ex)
        {
            StatusText = $"Range delete error: {ex.Message}";
            System.Windows.MessageBox.Show(ex.Message, "Delete Error",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    // ── Server NIC status save ──────────────────────────────────────────

    public async Task SaveServerNicStatusAsync(Server s)
    {
        try
        {
            await _repo.UpdateServerNicStatusAsync(s);
            StatusText = $"NIC status saved: {s.ServerName}";
            Central.Engine.Shell.PanelMessageBus.Publish(
                new Central.Engine.Shell.DataModifiedMessage("devices", "Server", "Update"));
        }
        catch (Exception ex) { StatusText = $"Server save error: {ex.Message}"; }
    }

    // ── User CRUD (Admin only) ─────────────────────────────────────────

    public async Task SaveUserAsync(AppUser user)
    {
        try
        {
            await _repo.UpsertUserAsync(user);
            StatusText = $"User saved: {user.Username}  ·  {DateTime.Now:HH:mm:ss}";
            Central.Engine.Shell.PanelMessageBus.Publish(
                new Central.Engine.Shell.DataModifiedMessage("admin", "User", "Update"));
        }
        catch (Exception ex)
        {
            StatusText = $"User save error: {ex.Message}";
            MessageBox.Show(ex.Message, "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public async Task DeleteUserAsync(AppUser user)
    {
        if (user.Id == 0) { Users.Remove(user); return; }
        try
        {
            var deleted = await _repo.DeleteUserAsync(user.Id);
            if (!deleted) { StatusText = $"Cannot delete protected user: {user.Username}"; return; }
            Users.Remove(user);
            StatusText = $"User deleted: {user.Username}  ·  {DateTime.Now:HH:mm:ss}";
            Central.Engine.Shell.PanelMessageBus.Publish(
                new Central.Engine.Shell.DataModifiedMessage("admin", "User", "Delete"));
            _ = Central.Engine.Services.AuditService.Instance.LogDeleteAsync("User", user.Id.ToString(), user.Username);
            Central.Engine.Services.UndoService.Instance.RecordRemove(
                Users, user, Users.Count, $"Delete user {user.Username}");
        }
        catch (Exception ex)
        {
            StatusText = $"User delete error: {ex.Message}";
            MessageBox.Show(ex.Message, "Delete Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ── Role CRUD (Admin only) ──────────────────────────────────────────

    public async Task SaveRoleAsync(RoleRecord role)
    {
        try
        {
            await _repo.UpsertRoleAsync(role);
            // Refresh role names list for user dropdowns
            var names = await _repo.GetRoleNamesAsync();
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                RoleNames.Clear();
                foreach (var n in names) RoleNames.Add(n);
            });
            StatusText = $"Role saved: {role.Name}  ·  {DateTime.Now:HH:mm:ss}";
            Central.Engine.Shell.PanelMessageBus.Publish(
                new Central.Engine.Shell.DataModifiedMessage("admin", "Role", "Update"));
        }
        catch (Exception ex)
        {
            StatusText = $"Role save error: {ex.Message}";
            MessageBox.Show(ex.Message, "Save Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public async Task DeleteRoleAsync(RoleRecord role)
    {
        if (role.Id == 0) { Roles.Remove(role); return; }
        try
        {
            await _repo.DeleteRoleAsync(role.Id, role.Name);
            Roles.Remove(role);
            RoleNames.Remove(role.Name);
            StatusText = $"Role deleted: {role.Name}  ·  {DateTime.Now:HH:mm:ss}";
            Central.Engine.Shell.PanelMessageBus.Publish(
                new Central.Engine.Shell.DataModifiedMessage("admin", "Role", "Delete"));
            Central.Engine.Services.UndoService.Instance.RecordRemove(
                Roles, role, Roles.Count, $"Delete role {role.Name}");
        }
        catch (Exception ex)
        {
            StatusText = $"Role delete error: {ex.Message}";
            MessageBox.Show(ex.Message, "Delete Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    public async Task LoadRoleSitesAsync(string roleName)
    {
        try
        {
            await _repo.SeedRoleSitesAsync(roleName);
            var sites = await _repo.GetRoleSitesAsync(roleName);
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                RoleSites.Clear();
                foreach (var s in sites) RoleSites.Add(s);
            });
        }
        catch (Exception ex)
        {
            StatusText = $"Error loading site access: {ex.Message}";
        }
    }

    public async Task SaveRoleSiteAsync(string roleName, RoleSiteAccess site)
    {
        try
        {
            await _repo.UpsertRoleSiteAsync(roleName, site.Building, site.Allowed);

            // If editing the current user's role, refresh allowed sites immediately
            if (string.Equals(roleName, AuthContext.Instance.CurrentUser?.RoleName, StringComparison.OrdinalIgnoreCase))
            {
                var permRepo = new Central.Persistence.Repositories.PermissionRepository(App.Dsn);
                var newSites = await permRepo.GetAllowedSitesAsync(roleName);
                AuthContext.Instance.UpdateAllowedSites(newSites);
                await ReloadDevicesAsync();
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Error saving site access: {ex.Message}";
        }
    }

    public async Task TestConnectionAsync()
    {
        StatusText = "Testing connection…";
        bool ok = await _repo.TestConnectionAsync();
        StatusText = ok ? "✓ Connected to database" : "✗ Could not connect — check DSN";
    }

    // ── Connectivity helpers (moved from MainWindow.xaml.cs) ──

    public string? LookupPrimaryIp(string hostname) =>
        Devices.FirstOrDefault(d => string.Equals(d.SwitchName, hostname, StringComparison.OrdinalIgnoreCase))?.Ip;

    public string GetSshDefault(string name) =>
        ConfigRanges.FirstOrDefault(r => r.Category == "ssh" && r.Name == name)?.RangeStart ?? "";

    public async Task PingAllAsync()
    {
        await PingService.PingAllAsync(Switches, s => StatusText = s, LookupPrimaryIp);
        foreach (var sw in Switches.Where(s => s.LastPingOk.HasValue))
        {
            try { await Repo.UpdatePingResultAsync(sw.Hostname, sw.LastPingOk!.Value, sw.LastPingMs); }
            catch { /* best-effort */ }
        }
    }

    public async Task PingSelectedAsync(SwitchRecord? sw)
    {
        if (sw == null) return;
        StatusText = $"Pinging {sw.Hostname}…";
        await PingService.PingOneAsync(sw, LookupPrimaryIp(sw.Hostname));
        try { await Repo.UpdatePingResultAsync(sw.Hostname, sw.LastPingOk!.Value, sw.LastPingMs); }
        catch { /* best-effort */ }
        StatusText = sw.LastPingOk == true
            ? $"{sw.Hostname}: {sw.LastPingMs:F0} ms"
            : $"{sw.Hostname}: Unreachable";
    }

    public async Task RunPingScanAsync()
    {
        if (Switches.Count == 0) return;
        var snapshot = Switches.ToList();
        // Capture previous state for alert detection
        var prevState = snapshot.ToDictionary(s => s.Hostname, s => s.LastPingOk);

        await PingService.PingAllAsync(snapshot, s => StatusText = s, LookupPrimaryIp);
        foreach (var sw in snapshot.Where(s => s.LastPingOk.HasValue))
        {
            try { await Repo.UpdatePingResultAsync(sw.Hostname, sw.LastPingOk!.Value, sw.LastPingMs); }
            catch { /* best-effort */ }

            // Alert on state change
            var wasPingOk = prevState.GetValueOrDefault(sw.Hostname);
            if (sw.LastPingOk == false && wasPingOk != false)
                Central.Engine.Services.AlertService.Instance.PingFailed(sw.Hostname, sw.ManagementIp);
            else if (sw.LastPingOk == true && wasPingOk == false)
                Central.Engine.Services.AlertService.Instance.PingRecovered(sw.Hostname, sw.LastPingMs ?? 0);
        }
        var reachable = snapshot.Where(s => s.LastPingOk == true && !string.IsNullOrWhiteSpace(s.SshPassword)).ToList();
        if (reachable.Count > 0)
        {
            await PingService.TestSshAllAsync(reachable, s => StatusText = s, LookupPrimaryIp,
                GetSshDefault("Default SSH Username"), GetSshDefault("Default SSH Password"),
                int.TryParse(GetSshDefault("Default SSH Port"), out var sshPort) ? sshPort : 22);
            foreach (var sw in reachable.Where(s => s.LastSshOk.HasValue))
            {
                try { await Repo.UpdateSshResultAsync(sw.Hostname, sw.LastSshOk!.Value); }
                catch { /* best-effort */ }
            }
        }
    }

    // ── SSH Sync helpers (moved from MainWindow.xaml.cs) ──

    public List<string> GetCandidateIps(SwitchRecord sw, string? mgmtIp)
    {
        var ips = new List<string>();
        if (!string.IsNullOrWhiteSpace(sw.SshOverrideIp)) ips.Add(sw.SshOverrideIp.Split('/')[0]);
        if (!string.IsNullOrWhiteSpace(mgmtIp)) { var c = mgmtIp.Split('/')[0]; if (!ips.Contains(c)) ips.Add(c); }
        var primaryIp = LookupPrimaryIp(sw.Hostname);
        if (!string.IsNullOrWhiteSpace(primaryIp)) { var c = primaryIp.Split('/')[0]; if (!ips.Contains(c)) ips.Add(c); }
        return ips;
    }

    public (string user, string? pass, int port) ResolveSshCredentials(SwitchRecord sw)
    {
        var defaultUser = GetSshDefault("Default SSH Username");
        var defaultPass = GetSshDefault("Default SSH Password");
        var defaultPort = int.TryParse(GetSshDefault("Default SSH Port"), out var dp) ? dp : 22;
        var username = !string.IsNullOrWhiteSpace(sw.SshUsername) ? sw.SshUsername
                     : !string.IsNullOrWhiteSpace(defaultUser) ? defaultUser : "admin";
        var port = sw.SshPort > 0 ? sw.SshPort : defaultPort;
        var password = !string.IsNullOrWhiteSpace(sw.SshPassword) ? sw.SshPassword : defaultPass;
        return (username, password, port);
    }

    public async Task<string?> FindReachableIpAsync(List<string> candidateIps)
    {
        foreach (var candidate in candidateIps)
        {
            try
            {
                using var ping = new System.Net.NetworkInformation.Ping();
                var reply = await ping.SendPingAsync(candidate, 3000);
                if (reply.Status == System.Net.NetworkInformation.IPStatus.Success)
                {
                    var reply2 = await ping.SendPingAsync(candidate, 3000);
                    if (reply2.Status == System.Net.NetworkInformation.IPStatus.Success)
                        return candidate;
                }
            }
            catch { }
        }
        return null;
    }

    public async Task ProcessSyncResultAsync(SwitchRecord sw, Services.SshService.SshResult result, Action<SwitchVersion>? onVersion = null)
    {
        var opName = AuthContext.Instance.CurrentUser?.DisplayName ?? Environment.UserName;
        var config = result.Config!;
        await Repo.SaveRunningConfigAsync(sw.Id, config, "", opName);
        RunningConfigText = config;

        if (!string.IsNullOrWhiteSpace(result.VersionOutput))
        {
            try
            {
                var ver = SwitchVersion.Parse(sw.Id, result.VersionOutput);
                await Repo.SaveSwitchVersionAsync(ver);
                sw.PicosVersion = ver.L2L3Version; sw.HardwareModel = ver.HardwareModel;
                sw.MacAddress = ver.MacAddress; sw.SerialNumber = ver.SerialNumber; sw.Uptime = ver.Uptime;
                onVersion?.Invoke(ver);
            }
            catch (Exception vex) { AppLogger.LogException("Sync", vex, "Version"); }
        }

        if (!string.IsNullOrWhiteSpace(result.InterfacesOutput))
        {
            try
            {
                var ifaces = SwitchInterface.Parse(sw.Id, result.InterfacesOutput);
                SwitchInterface.MergeLldp(ifaces, result.LldpOutput);
                try
                {
                    var optics = InterfaceOptics.Parse(sw.Id, result.OpticsOutput);
                    if (optics.Count > 0) { await Repo.SaveInterfaceOpticsAsync(sw.Id, optics); SwitchInterface.MergeOptics(ifaces, optics); }
                    else { var latest = await Repo.GetLatestOpticsAsync(sw.Id); SwitchInterface.MergeOptics(ifaces, latest); }
                }
                catch (Exception oex) { AppLogger.LogException("Sync", oex, "Optics"); }
                if (ifaces.Count > 0)
                {
                    await Repo.SaveSwitchInterfacesAsync(sw.Id, ifaces);
                    SwitchInterfaces.Clear();
                    foreach (var i in ifaces) SwitchInterfaces.Add(i);
                }
            }
            catch (Exception iex) { AppLogger.LogException("Sync", iex, "Interfaces"); }
        }
    }

    // ── Detail panel data loading (moved from MainWindow.xaml.cs) ──

    public SwitchVersion? CurrentVersion { get; private set; }
    public List<object>? CurrentAuditLog { get; private set; }
    public List<object>? CurrentBackups { get; private set; }
    public List<ConfigVersionEntry>? CurrentConfigVersions { get; private set; }

    /// <summary>Load all detail data for the selected switch.</summary>
    public async Task LoadSwitchDetailAsync()
    {
        if (SelectedSwitch is not { } sw || sw.Id == Guid.Empty)
        {
            RunningConfigText = "";
            CurrentVersion = null;
            CurrentAuditLog = null;
            CurrentBackups = null;
            CurrentConfigVersions = null;
            SwitchInterfaces.Clear();
            return;
        }

        var cfg = await Repo.GetLatestRunningConfigAsync(sw.Id);
        RunningConfigText = cfg ?? "(no config downloaded yet)";

        CurrentVersion = await Repo.GetLatestSwitchVersionAsync(sw.Id);

        var ifaces = await Repo.GetSwitchInterfacesAsync(sw.Id);
        try { var optics = await Repo.GetLatestOpticsAsync(sw.Id); SwitchInterface.MergeOptics(ifaces, optics); }
        catch { }
        SwitchInterfaces.Clear();
        foreach (var i in ifaces) SwitchInterfaces.Add(i);

        await RefreshAuditAndBackupsAsync(sw.Id);
    }

    public async Task RefreshAuditAndBackupsAsync(Guid switchId)
    {
        var auditLog = await Repo.GetAuditLogAsync(switchId);
        CurrentAuditLog = auditLog.Select(a => (object)new
        {
            Timestamp = a.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"),
            a.Operator, a.Action, a.Field, a.OldValue, a.NewValue, a.Description
        }).ToList();

        var backups = await Repo.GetConfigBackupsAsync(switchId);
        CurrentBackups = backups.Select(b => (object)new
        {
            b.Id,
            CreatedAt = b.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss"),
            b.Operator, b.Description, b.LineCount
        }).ToList();

        var versions = await Repo.GetConfigVersionsAsync(switchId);
        CurrentConfigVersions = versions;
    }

    public async Task BackupCurrentConfigAsync()
    {
        if (SelectedSwitch is not { } sw || sw.Id == Guid.Empty) return;
        if (string.IsNullOrWhiteSpace(RunningConfigText) || RunningConfigText.StartsWith("(")) return;

        var op = Central.Engine.Auth.AuthContext.Instance.CurrentUser?.DisplayName ?? Environment.UserName;
        var lineCount = RunningConfigText.Split('\n').Length;
        var desc = $"Manual backup — {lineCount} lines — {DateTime.Now:yyyy-MM-dd HH:mm}";
        await Repo.SaveConfigBackupAsync(sw.Id, RunningConfigText, sw.EffectiveSshIp, op, desc);
        await RefreshAuditAndBackupsAsync(sw.Id);
        StatusText = $"Backup saved  ·  {DateTime.Now:HH:mm:ss}";
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
