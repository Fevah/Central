using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using DevExpress.Xpf.Bars;
using DevExpress.Xpf.Grid;
using Central.Core.Auth;
using Central.Core.Models;
using Central.Core.Services;
using Central.Core.Shell;
using Central.Data;
using Central.Desktop.Services;
using Central.Desktop.ViewModels;
using DevExpress.Xpf.Diagram;

namespace Central.Desktop;

public partial class MainWindow
{
    private MainViewModel VM => (MainViewModel)DataContext;
    private bool _deletePending;
    private LayoutService? _layout;
    private LinkEditorHelper? _linkEditor;
    private SwitchSyncService? _syncService;
    private bool _isRolesActive = true;  // true when RolesPanel is the active doc tab
    private bool _isUsersActive;         // true when UsersPanel is the active doc tab
    private bool _isLookupsActive;       // true when LookupsPanel is the active doc tab
    private bool _isSettingsActive;      // true when SettingsPanel is the active doc tab
    private System.Windows.Threading.DispatcherTimer? _scanTimer;

    private static void UpdateSplash(string status, int pct)
    {
        try { App.Splash?.UpdateStatus(status, pct); } catch { }
    }

    private void CloseSplash()
    {
        try
        {
            if (App.Splash != null)
            {
                App.Splash.Close();
                App.Splash = null;
                Opacity = 1;
            }
        }
        catch { }
    }

    public MainWindow()
    {
        InitializeComponent();
        Loaded  += MainWindow_Loaded;
        Closing += MainWindow_Closing;
        SetupSettingsCog();

        // Global: Delete/Backspace clears any ComboBoxEdit to null/empty
        EventManager.RegisterClassHandler(
            typeof(DevExpress.Xpf.Editors.ComboBoxEdit),
            PreviewKeyDownEvent,
            new System.Windows.Input.KeyEventHandler(Global_ComboBoxEdit_PreviewKeyDown));

        // Keyboard shortcuts
        InputBindings.Add(new KeyBinding(new RelayCommand(async () => { await VM.LoadAllAsync(); BindComboSources(); VM.StatusText = "Data refreshed"; }),
            Key.R, ModifierKeys.Control));
        InputBindings.Add(new KeyBinding(new RelayCommand(async () => { await VM.LoadAllAsync(); BindComboSources(); VM.StatusText = "Data refreshed"; }),
            Key.F5, ModifierKeys.None));
        InputBindings.Add(new KeyBinding(new RelayCommand(() => NewButton_ItemClick(this, null!)),
            Key.N, ModifierKeys.Control));
        InputBindings.Add(new KeyBinding(new RelayCommand(async () => await DeleteActiveRow()),
            Key.Delete, ModifierKeys.None));
        InputBindings.Add(new KeyBinding(new RelayCommand(ExportDevices),
            Key.E, ModifierKeys.Control));
        InputBindings.Add(new KeyBinding(new RelayCommand(() => PrintPreview_ItemClick(this, null!)),
            Key.P, ModifierKeys.Control));
        InputBindings.Add(new KeyBinding(new RelayCommand(ToggleGlobalSearch),
            Key.F, ModifierKeys.Control));
        InputBindings.Add(new KeyBinding(new RelayCommand(ShowKeyboardHelp),
            Key.F1, ModifierKeys.None));
        InputBindings.Add(new KeyBinding(new RelayCommand(CycleNextPanel),
            Key.Tab, ModifierKeys.Control));
        InputBindings.Add(new KeyBinding(new RelayCommand(() => VM.IsDetailsPanelOpen = !VM.IsDetailsPanelOpen),
            Key.D, ModifierKeys.Control));
        InputBindings.Add(new KeyBinding(new RelayCommand(CommitCurrentRow),
            Key.S, ModifierKeys.Control));
        InputBindings.Add(new KeyBinding(new RelayCommand(ShowGoToDialog),
            Key.G, ModifierKeys.Control));
        InputBindings.Add(new KeyBinding(new RelayCommand(ShowImportWizard),
            Key.I, ModifierKeys.Control));
        InputBindings.Add(new KeyBinding(new RelayCommand(() =>
            Central.Core.Services.UndoService.Instance.Undo()),
            Key.Z, ModifierKeys.Control));
        InputBindings.Add(new KeyBinding(new RelayCommand(() =>
            Central.Core.Services.UndoService.Instance.Redo()),
            Key.Y, ModifierKeys.Control));

        // Double-click on device/switch row opens detail panel
        // Double-click on any main grid opens detail panel + wire filter change for row count
        foreach (var grid in new[] { DeviceGridPanel.Grid, SwitchGridPanel.Grid,
            P2PGridPanel.Grid, B2BGridPanel.Grid, FWGridPanel.Grid,
            VlanGridPanel.Grid, BgpGridPanel.Grid, AsnGridPanel.Grid })
        {
            grid.MouseDoubleClick += (_, _) => { VM.IsDetailsPanelOpen = true; };
            grid.FilterChanged += (_, _) => UpdateActiveRowCount();
        }

        VM.RefreshCommand         = new RelayCommand(async () => { await VM.LoadAllAsync(); BindComboSources(); ForceVlanSort(); _ = VM.RunPingScanAsync(); });
        VM.TestConnectionCommand  = new RelayCommand(async () => await VM.TestConnectionAsync());
        VM.ExportDevicesCommand      = new RelayCommand(ExportDevices);
        VM.OpenWebAppCommand      = new RelayCommand(OpenWebApp);
        VM.OpenDeviceCommand      = new RelayCommand<DeviceRecord>(OpenDevice);
        VM.GroupByBuildingCommand = new RelayCommand(() => GroupBy("Building"));
        VM.GroupByTypeCommand     = new RelayCommand(() => GroupBy("DeviceType"));
        VM.GroupByRegionCommand   = new RelayCommand(() => GroupBy("Region"));
        VM.ClearGroupsCommand     = new RelayCommand(ClearGroups);
        VM.ClearFiltersCommand    = new RelayCommand(() => {
            VM.HideReserved = false;
            DeviceGridPanel.SearchBox.EditValue = null;
            foreach (var s in VM.SiteSummaries) s.IsSelected = true;
            DeviceGridPanel.Grid.FilterString = null;
            VM.DeviceCountText = $"{DeviceGridPanel.Grid.VisibleRowCount} devices";
        });

        // Toggle details panel: pin docked ↔ auto-hide to tab
        VM.ToggleDetailsCommand = new RelayCommand(() =>
        {
            VM.IsDetailsPanelOpen = !VM.IsDetailsPanelOpen;
        });

        DockManager.DockItemActivated += (_, e) => OnActivePanelChanged(e.Item);
        DockManager.DockItemClosed += (_, e) =>
        {
            if (e.Item == DevicesPanel) VM.IsDevicesPanelOpen = false;
            if (e.Item == SwitchesPanel) VM.IsSwitchesPanelOpen = false;
            if (e.Item == RolesPanel) VM.IsRolesPanelOpen = false;
            if (e.Item == UsersPanel) VM.IsUsersPanelOpen = false;
            if (e.Item == LookupsPanel) VM.IsLookupsPanelOpen = false;
            if (e.Item == AsnPanel) VM.IsAsnPanelOpen = false;
            if (e.Item == MasterPanel) VM.IsMasterPanelOpen = false;
            if (e.Item == P2PPanel) VM.IsP2PPanelOpen = false;
            if (e.Item == B2BPanel) VM.IsB2BPanelOpen = false;
            if (e.Item == FWPanel) VM.IsFWPanelOpen = false;
            if (e.Item == ServerAsPanel) VM.IsServerAsPanelOpen = false;
            if (e.Item == IpRangesPanel) VM.IsIpRangesPanelOpen = false;
            if (e.Item == VlansPanel) VM.IsVlansPanelOpen = false;
            if (e.Item == MlagPanel) VM.IsMlagPanelOpen = false;
            if (e.Item == MstpPanel) VM.IsMstpPanelOpen = false;
            if (e.Item == ServersPanel) VM.IsServersPanelOpen = false;
            if (e.Item == SettingsPanel) VM.IsSettingsPanelOpen = false;
            if (e.Item == SshLogsPanel) VM.IsSshLogsPanelOpen = false;
            if (e.Item == AppLogPanel) VM.IsAppLogPanelOpen = false;
            if (e.Item == DiagramPanel) VM.IsDiagramPanelOpen = false;
            if (e.Item == BuilderPanel) VM.IsBuilderPanelOpen = false;
            if (e.Item == BgpPanel) VM.IsBgpPanelOpen = false;
            if (e.Item == JobsPanel) VM.IsJobsPanelOpen = false;
            if (e.Item == TasksPanel) VM.IsTasksPanelOpen = false;
            if (e.Item == BacklogPanel) VM.IsBacklogPanelOpen = false;
            if (e.Item == SprintPlanningPanel) VM.IsSprintPlanPanelOpen = false;
            if (e.Item == SprintBurndownDocPanel) VM.IsBurndownPanelOpen = false;
            if (e.Item == KanbanBoardDocPanel) VM.IsKanbanPanelOpen = false;
            if (e.Item == GanttDocPanel) VM.IsGanttPanelOpen = false;
            if (e.Item == QADocPanel) VM.IsQAPanelOpen = false;
            if (e.Item == QADashboardDocPanel) VM.IsQADashboardPanelOpen = false;
            if (e.Item == ReportBuilderDocPanel) VM.IsReportBuilderPanelOpen = false;
            if (e.Item == TaskDashboardDocPanel) VM.IsTaskDashboardPanelOpen = false;
            if (e.Item == TimesheetDocPanel) VM.IsTimesheetPanelOpen = false;
            if (e.Item == ActivityFeedDocPanel) VM.IsActivityFeedPanelOpen = false;
            if (e.Item == MyTasksDocPanel) VM.IsMyTasksPanelOpen = false;
            if (e.Item == PortfolioDocPanel) VM.IsPortfolioPanelOpen = false;
            if (e.Item == TaskImportDocPanel) VM.IsTaskImportPanelOpen = false;
            if (e.Item == TaskDetailDocPanel) VM.IsTaskDetailPanelOpen = false;
            if (e.Item == RibbonConfigPanel) VM.IsRibbonConfigPanelOpen = false;
            if (e.Item == IntegrationsPanel) VM.IsIntegrationsPanelOpen = false;
            if (e.Item == ServiceDeskPanel) VM.IsServiceDeskPanelOpen = false;
            if (e.Item == SdOverviewPanel) VM.IsSdOverviewPanelOpen = false;
            if (e.Item == SdTechClosuresPanel) VM.IsSdClosuresPanelOpen = false;
            if (e.Item == SdAgingPanel) VM.IsSdAgingPanelOpen = false;
            if (e.Item == SdTeamsPanel) VM.IsSdTeamsPanelOpen = false;
            if (e.Item == SdGroupsPanel) VM.IsSdGroupsPanelOpen = false;
            if (e.Item == SdTechniciansPanel) VM.IsSdTechniciansPanelOpen = false;
            if (e.Item == SdRequestersPanel) VM.IsSdRequestersPanelOpen = false;
            if (e.Item == GlobalTenantsPanel) VM.IsGlobalTenantsPanelOpen = false;
            if (e.Item == GlobalUsersPanel) VM.IsGlobalUsersPanelOpen = false;
            if (e.Item == GlobalSubscriptionsPanel) VM.IsGlobalSubscriptionsPanelOpen = false;
            if (e.Item == GlobalLicensesPanel) VM.IsGlobalLicensesPanelOpen = false;
            if (e.Item == PlatformDashboardPanel) VM.IsPlatformDashboardPanelOpen = false;
            // ComparePanel toggled via DockController directly, no ribbon toggle
        };
        DeviceGridPanel.Grid.CurrentItemChanged     += DevicesGrid_CurrentItemChanged;
        DeviceGridPanel.SaveDevice                 += async (dev) => await VM.SaveDeviceAsync(dev);
        DeviceGridPanel.SearchChanged              += _ => UpdateDevicesFilter();
        DeviceGridPanel.LoadDetailLinks            += async dev =>
        {
            try
            {
                var deviceName = dev.SwitchName;
                dev.DetailLinks.Clear();
                // P2P links where this device is A or B
                foreach (var link in VM.P2PLinks)
                {
                    if (string.Equals(link.DeviceA, deviceName, StringComparison.OrdinalIgnoreCase))
                        dev.DetailLinks.Add(new DeviceLinkSummary { LinkType = "P2P", LinkId = link.Id, RemoteDevice = link.DeviceB, LocalPort = link.PortA, RemotePort = link.PortB, Vlan = link.Vlan, Subnet = link.Subnet, Status = link.Status });
                    else if (string.Equals(link.DeviceB, deviceName, StringComparison.OrdinalIgnoreCase))
                        dev.DetailLinks.Add(new DeviceLinkSummary { LinkType = "P2P", LinkId = link.Id, RemoteDevice = link.DeviceA, LocalPort = link.PortB, RemotePort = link.PortA, Vlan = link.Vlan, Subnet = link.Subnet, Status = link.Status });
                }
                foreach (var link in VM.B2BLinks)
                {
                    if (string.Equals(link.DeviceA, deviceName, StringComparison.OrdinalIgnoreCase))
                        dev.DetailLinks.Add(new DeviceLinkSummary { LinkType = "B2B", LinkId = link.Id, RemoteDevice = link.DeviceB, LocalPort = link.PortA, RemotePort = link.PortB, Vlan = link.Vlan, Subnet = link.Subnet, Status = link.Status });
                    else if (string.Equals(link.DeviceB, deviceName, StringComparison.OrdinalIgnoreCase))
                        dev.DetailLinks.Add(new DeviceLinkSummary { LinkType = "B2B", LinkId = link.Id, RemoteDevice = link.DeviceA, LocalPort = link.PortB, RemotePort = link.PortA, Vlan = link.Vlan, Subnet = link.Subnet, Status = link.Status });
                }
                foreach (var link in VM.FWLinks)
                {
                    if (string.Equals(link.DeviceA, deviceName, StringComparison.OrdinalIgnoreCase))
                        dev.DetailLinks.Add(new DeviceLinkSummary { LinkType = "FW", LinkId = link.Id, RemoteDevice = link.DeviceB, LocalPort = link.SwitchPort, RemotePort = link.FirewallPort, Vlan = link.Vlan, Subnet = link.Subnet, Status = link.Status });
                    else if (string.Equals(link.DeviceB, deviceName, StringComparison.OrdinalIgnoreCase))
                        dev.DetailLinks.Add(new DeviceLinkSummary { LinkType = "FW", LinkId = link.Id, RemoteDevice = link.DeviceA, LocalPort = link.FirewallPort, RemotePort = link.SwitchPort, Vlan = link.Vlan, Subnet = link.Subnet, Status = link.Status });
                }
            }
            catch (Exception ex) { AppLogger.LogException("Detail", ex, $"LoadDetailLinks:{dev.SwitchName}"); }
        };
        SwitchGridPanel.Grid.CurrentItemChanged   += SwitchGrid_CurrentItemChanged;
        SwitchGridPanel.LoadDetailInterfaces += async sw =>
        {
            try
            {
                var ifaces = await VM.Repo.GetSwitchInterfacesAsync(sw.Id);
                if (ifaces.Count > 0)
                {
                    var optics = await VM.Repo.GetLatestOpticsAsync(sw.Id);
                    SwitchInterface.MergeOptics(ifaces, optics);
                }
                sw.DetailInterfaces.Clear();
                foreach (var i in Central.Core.Extensions.NaturalSortExtensions.OrderByNatural(ifaces, x => x.InterfaceName))
                    sw.DetailInterfaces.Add(i);
            }
            catch (Exception ex) { AppLogger.LogException("Detail", ex, $"LoadInterfaces:{sw.Hostname}"); }
        };
        BgpGridPanel.LoadDetailData += async bgp =>
        {
            try
            {
                var neighbors = await VM.Repo.GetBgpNeighborsAsync(bgp.SwitchId);
                var networks = await VM.Repo.GetBgpNetworksAsync(bgp.SwitchId);
                bgp.DetailNeighbors.Clear();
                foreach (var n in neighbors) bgp.DetailNeighbors.Add(n);
                bgp.DetailNetworks.Clear();
                foreach (var n in networks) bgp.DetailNetworks.Add(n);
            }
            catch (Exception ex) { AppLogger.LogException("BGP", ex, $"LoadDetail:{bgp.Hostname}"); }
        };
        // Admin/Users/Lookups handlers → Module.Admin UserControls
        // Users validation handled by UsersPanel UserControl
        RolesGridPanel.SaveRole += async (role) => await VM.SaveRoleAsync(role);
        RolesGridPanel.LoadDetailUsers += async role =>
        {
            role.DetailUsers.Clear();
            foreach (var user in VM.Users)
            {
                if (string.Equals(user.Role, role.Name, StringComparison.OrdinalIgnoreCase))
                    role.DetailUsers.Add(new RoleUserDetail
                    {
                        Username = user.Username,
                        DisplayName = user.DisplayName,
                        IsActive = user.IsActive,
                        LastLogin = user.LastLoginAt?.ToString("yyyy-MM-dd HH:mm") ?? ""
                    });
            }
        };
        RolesGridPanel.SiteAccessToggled += async (site) =>
        {
            if (VM.SelectedRole == null) return;
            await VM.SaveRoleSiteAsync(VM.SelectedRole.Name, site);
        };
        SettingsView.ValidateRow       += SettingsView_ValidateRow;
        SettingsView.InvalidRowException += SettingsView_InvalidRowException;
        ServersView.ValidateRow        += ServersView_ValidateRow;
        ServersView.InvalidRowException += ServersView_InvalidRowException;
        // VLANs ValidateRow handled by VlanGridPanel internally
        AsnGridPanel.View.ValidateRow            += AsnView_ValidateRow;
        P2PGridPanel.SaveLink += async (link) =>
        {
            var result = VM.UseApi
                ? await SafeApiWrite(() => VM.DataService!.UpsertP2PLinkAsync(link), "P2P")
                : await VM.Repo.SafeWriteAsync(() => VM.Repo.UpsertP2PLinkAsync(link), "P2PView_ValidateRow");
            VM.StatusText = result.Success ? $"P2P link saved: {link.LinkId}" : $"Save failed: {result.Error}";
            if (result.Success) Central.Core.Shell.PanelMessageBus.Publish(
                new Central.Core.Shell.DataModifiedMessage("p2p", "P2PLink", "Update"));
        };
        P2PGridPanel.View.ShownEditor += P2PView_ShownEditor;
        B2BGridPanel.SaveLink += async (link) =>
        {
            var result = VM.UseApi
                ? await SafeApiWrite(() => VM.DataService!.UpsertB2BLinkAsync(link), "B2B")
                : await VM.Repo.SafeWriteAsync(() => VM.Repo.UpsertB2BLinkAsync(link), "B2BView_ValidateRow");
            VM.StatusText = result.Success ? $"B2B link saved: {link.LinkId}" : $"Save failed: {result.Error}";
            if (result.Success) Central.Core.Shell.PanelMessageBus.Publish(
                new Central.Core.Shell.DataModifiedMessage("b2b", "B2BLink", "Update"));
        };
        B2BGridPanel.View.ShownEditor += B2BView_ShownEditor;
        FWGridPanel.SaveLink += async (link) =>
        {
            var result = VM.UseApi
                ? await SafeApiWrite(() => VM.DataService!.UpsertFWLinkAsync(link), "FW")
                : await VM.Repo.SafeWriteAsync(() => VM.Repo.UpsertFWLinkAsync(link), "FWView_ValidateRow");
            VM.StatusText = result.Success ? $"FW link saved: {link.LinkId}" : $"Save failed: {result.Error}";
            if (result.Success) Central.Core.Shell.PanelMessageBus.Publish(
                new Central.Core.Shell.DataModifiedMessage("fw", "FWLink", "Update"));
        };
        FWGridPanel.View.ShownEditor += FWView_ShownEditor;
        AsnGridPanel.View.InvalidRowException    += AsnView_InvalidRowException;
        AsnGridPanel.View.ShownEditor            += AsnView_ShownEditor;
        AsnGridPanel.View.CellValueChanged       += AsnView_CellValueChanged;
        AsnGridPanel.Grid.CurrentItemChanged     += AsnGrid_CurrentItemChanged;
        AsnGridPanel.LoadDetailDevices += async asn =>
        {
            // Find devices in VM that have this ASN assigned
            asn.DetailDevices.Clear();
            foreach (var dev in VM.Devices)
            {
                if (string.Equals(dev.Asn, asn.Asn, StringComparison.OrdinalIgnoreCase))
                    asn.DetailDevices.Add(new AsnBoundDevice
                    {
                        SwitchName = dev.SwitchName,
                        Building = dev.Building,
                        DeviceType = dev.DeviceType,
                        PrimaryIp = dev.Ip
                    });
            }
        };
        RolesGridPanel.Grid.CurrentItemChanged   += RolesGrid_CurrentItemChanged;

        // Diagram panel events
        DiagramGridPanel.RefreshRequested += async () => await BuildNetworkDiagramAsync();
        DiagramGridPanel.FitRequested += () => DiagramGridPanel.Diagram.FitToItems(DiagramGridPanel.Diagram.Items.ToList());
        DiagramGridPanel.TreeLayoutRequested += () => DiagramGridPanel.Diagram.ApplyTreeLayout();
        DiagramGridPanel.SugiyamaLayoutRequested += () => DiagramGridPanel.Diagram.ApplySugiyamaLayout();

        // Detail tabs button wiring (handlers removed from UC XAML, wired here)
        DetailTabsPanel.SyncButton.Click += SyncConfigButton_Click;
        DetailTabsPanel.CompareButton.Click += CompareConfigButton_Click;
        DetailTabsPanel.DeleteVersionButton.Click += DeleteConfigVersionButton_Click;
        DetailTabsPanel.VersionsList.MouseDoubleClick += ConfigVersionsList_MouseDoubleClick;

        VM.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.HideReserved))
                UpdateDevicesFilter();
            if (e.PropertyName == nameof(MainViewModel.ShowLiveDescriptions))
                _ = ToggleLiveDescriptionsAsync();
            if (e.PropertyName == nameof(MainViewModel.IsDevicesPanelOpen))
                ToggleDockPanel(DevicesPanel, VM.IsDevicesPanelOpen);
            if (e.PropertyName == nameof(MainViewModel.IsSwitchesPanelOpen))
                ToggleDockPanel(SwitchesPanel, VM.IsSwitchesPanelOpen);
            if (e.PropertyName == nameof(MainViewModel.IsRolesPanelOpen))
                ToggleDockPanel(RolesPanel, VM.IsRolesPanelOpen);
            if (e.PropertyName == nameof(MainViewModel.IsUsersPanelOpen))
                ToggleDockPanel(UsersPanel, VM.IsUsersPanelOpen);
            if (e.PropertyName == nameof(MainViewModel.IsLookupsPanelOpen))
                ToggleDockPanel(LookupsPanel, VM.IsLookupsPanelOpen);
            if (e.PropertyName == nameof(MainViewModel.IsSettingsPanelOpen))
                ToggleDockPanel(SettingsPanel, VM.IsSettingsPanelOpen);
            if (e.PropertyName == nameof(MainViewModel.IsMasterPanelOpen))
                ToggleDockPanel(MasterPanel, VM.IsMasterPanelOpen);
            if (e.PropertyName == nameof(MainViewModel.IsAsnPanelOpen))
                ToggleDockPanel(AsnPanel, VM.IsAsnPanelOpen);
            if (e.PropertyName == nameof(MainViewModel.IsP2PPanelOpen))
                ToggleDockPanel(P2PPanel, VM.IsP2PPanelOpen);
            if (e.PropertyName == nameof(MainViewModel.IsB2BPanelOpen))
                ToggleDockPanel(B2BPanel, VM.IsB2BPanelOpen);
            if (e.PropertyName == nameof(MainViewModel.IsFWPanelOpen))
                ToggleDockPanel(FWPanel, VM.IsFWPanelOpen);
            if (e.PropertyName == nameof(MainViewModel.IsVlansPanelOpen))
                ToggleDockPanel(VlansPanel, VM.IsVlansPanelOpen);
            if (e.PropertyName == nameof(MainViewModel.IsMlagPanelOpen))
                ToggleDockPanel(MlagPanel, VM.IsMlagPanelOpen);
            if (e.PropertyName == nameof(MainViewModel.IsMstpPanelOpen))
                ToggleDockPanel(MstpPanel, VM.IsMstpPanelOpen);
            if (e.PropertyName == nameof(MainViewModel.IsServerAsPanelOpen))
                ToggleDockPanel(ServerAsPanel, VM.IsServerAsPanelOpen);
            if (e.PropertyName == nameof(MainViewModel.IsIpRangesPanelOpen))
                ToggleDockPanel(IpRangesPanel, VM.IsIpRangesPanelOpen);
            if (e.PropertyName == nameof(MainViewModel.IsServersPanelOpen))
                ToggleDockPanel(ServersPanel, VM.IsServersPanelOpen);
            if (e.PropertyName == nameof(MainViewModel.IsSshLogsPanelOpen))
                ToggleDockPanel(SshLogsPanel, VM.IsSshLogsPanelOpen);
            if (e.PropertyName == nameof(MainViewModel.IsAppLogPanelOpen))
                ToggleDockPanel(AppLogPanel, VM.IsAppLogPanelOpen);
            if (e.PropertyName == nameof(MainViewModel.IsBuilderPanelOpen))
            {
                ToggleDockPanel(BuilderPanel, VM.IsBuilderPanelOpen);
                if (VM.IsBuilderPanelOpen) WireBuilderCombo();
            }
            if (e.PropertyName == nameof(MainViewModel.IsBgpPanelOpen))
            {
                ToggleDockPanel(BgpPanel, VM.IsBgpPanelOpen);
                if (VM.IsBgpPanelOpen) _ = VM.LoadPanelDataAsync("bgp");
            }
            if (e.PropertyName == nameof(MainViewModel.IsJobsPanelOpen))
            {
                ToggleDockPanel(JobsPanel, VM.IsJobsPanelOpen);
                if (VM.IsJobsPanelOpen) _ = LoadJobsDataAsync();
            }
            if (e.PropertyName == nameof(MainViewModel.IsDeployPanelOpen))
                ToggleDockPanel(DeployPanel, VM.IsDeployPanelOpen);
            if (e.PropertyName == nameof(MainViewModel.IsTasksPanelOpen))
            {
                ToggleDockPanel(TasksPanel, VM.IsTasksPanelOpen);
                if (VM.IsTasksPanelOpen && VM.TaskItems.Count == 0)
                {
                    _ = Task.Run(async () =>
                    {
                        await VM.LoadTaskProjectsAsync();
                        System.Windows.Application.Current.Dispatcher.Invoke(() => TaskTreeGridPanel.SetProjects(VM.TaskProjects));
                        await VM.LoadTasksAsync();
                    });
                }
            }
            if (e.PropertyName == nameof(MainViewModel.IsBacklogPanelOpen))
            {
                ToggleDockPanel(BacklogPanel, VM.IsBacklogPanelOpen);
                if (VM.IsBacklogPanelOpen)
                {
                    _ = Task.Run(async () =>
                    {
                        if (VM.TaskProjects.Count == 0) await VM.LoadTaskProjectsAsync();
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            BacklogGridPanel.SetProjects(VM.TaskProjects);
                            BacklogGridPanel.SetSprints(VM.Sprints);
                        });
                    });
                }
            }
            if (e.PropertyName == nameof(MainViewModel.IsSprintPlanPanelOpen))
            {
                ToggleDockPanel(SprintPlanningPanel, VM.IsSprintPlanPanelOpen);
                if (VM.IsSprintPlanPanelOpen)
                {
                    _ = Task.Run(async () =>
                    {
                        if (VM.TaskProjects.Count == 0) await VM.LoadTaskProjectsAsync();
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            SprintPlanGridPanel.SetProjects(VM.TaskProjects);
                            SprintPlanGridPanel.SetSprints(VM.Sprints);
                        });
                    });
                }
            }
            if (e.PropertyName == nameof(MainViewModel.IsBurndownPanelOpen))
            {
                ToggleDockPanel(SprintBurndownDocPanel, VM.IsBurndownPanelOpen);
                if (VM.IsBurndownPanelOpen)
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        SprintBurndownChartPanel.SetSprints(VM.Sprints));
                }
            }
            if (e.PropertyName == nameof(MainViewModel.IsKanbanPanelOpen))
            {
                ToggleDockPanel(KanbanBoardDocPanel, VM.IsKanbanPanelOpen);
                if (VM.IsKanbanPanelOpen)
                {
                    _ = Task.Run(async () =>
                    {
                        if (VM.TaskProjects.Count == 0) await VM.LoadTaskProjectsAsync();
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                            KanbanBoardViewPanel.SetProjects(VM.TaskProjects));
                    });
                }
            }
            if (e.PropertyName == nameof(MainViewModel.IsGanttPanelOpen))
            {
                ToggleDockPanel(GanttDocPanel, VM.IsGanttPanelOpen);
                if (VM.IsGanttPanelOpen)
                {
                    _ = Task.Run(async () =>
                    {
                        if (VM.TaskProjects.Count == 0) await VM.LoadTaskProjectsAsync();
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                            GanttViewPanel.SetProjects(VM.TaskProjects));
                    });
                }
            }
            if (e.PropertyName == nameof(MainViewModel.IsQAPanelOpen))
            {
                ToggleDockPanel(QADocPanel, VM.IsQAPanelOpen);
                if (VM.IsQAPanelOpen)
                {
                    _ = Task.Run(async () =>
                    {
                        if (VM.TaskProjects.Count == 0) await VM.LoadTaskProjectsAsync();
                        var bugs = await VM.Repo.GetBugsAsync();
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            QAGridPanel.SetProjects(VM.TaskProjects);
                            QAGridPanel.Grid.ItemsSource = bugs;
                        });
                    });
                }
            }
            if (e.PropertyName == nameof(MainViewModel.IsQADashboardPanelOpen))
            {
                ToggleDockPanel(QADashboardDocPanel, VM.IsQADashboardPanelOpen);
                if (VM.IsQADashboardPanelOpen)
                {
                    _ = Task.Run(async () =>
                    {
                        if (VM.TaskProjects.Count == 0) await VM.LoadTaskProjectsAsync();
                        var bugs = await VM.Repo.GetBugsAsync();
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            QADashboardViewPanel.SetProjects(VM.TaskProjects);
                            QADashboardViewPanel.RefreshCharts(bugs);
                        });
                    });
                }
            }
            if (e.PropertyName == nameof(MainViewModel.IsTimesheetPanelOpen))
            {
                ToggleDockPanel(TimesheetDocPanel, VM.IsTimesheetPanelOpen);
            }
            if (e.PropertyName == nameof(MainViewModel.IsActivityFeedPanelOpen))
            {
                ToggleDockPanel(ActivityFeedDocPanel, VM.IsActivityFeedPanelOpen);
                if (VM.IsActivityFeedPanelOpen)
                {
                    _ = Task.Run(async () =>
                    {
                        if (VM.TaskProjects.Count == 0) await VM.LoadTaskProjectsAsync();
                        var feed = await VM.Repo.GetActivityFeedAsync();
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            ActivityFeedViewPanel.SetProjects(VM.TaskProjects);
                            ActivityFeedViewPanel.LoadItems(feed);
                        });
                    });
                }
            }
            if (e.PropertyName == nameof(MainViewModel.IsMyTasksPanelOpen))
            {
                ToggleDockPanel(MyTasksDocPanel, VM.IsMyTasksPanelOpen);
                if (VM.IsMyTasksPanelOpen)
                {
                    _ = Task.Run(async () =>
                    {
                        var userId = Central.Core.Auth.AuthContext.Instance.CurrentUser?.Id;
                        var tasks = await VM.Repo.GetTasksAsync();
                        var myTasks = userId.HasValue ? tasks.Where(t => t.AssignedTo == userId.Value).ToList() : tasks;
                        System.Windows.Application.Current.Dispatcher.Invoke(() => MyTasksViewPanel.Grid.ItemsSource = myTasks);
                    });
                }
            }
            if (e.PropertyName == nameof(MainViewModel.IsPortfolioPanelOpen))
            {
                ToggleDockPanel(PortfolioDocPanel, VM.IsPortfolioPanelOpen);
                if (VM.IsPortfolioPanelOpen)
                {
                    _ = Task.Run(async () =>
                    {
                        var nodes = await BuildPortfolioTreeAsync();
                        System.Windows.Application.Current.Dispatcher.Invoke(() => PortfolioViewPanel.LoadData(nodes));
                    });
                }
            }
            if (e.PropertyName == nameof(MainViewModel.IsTaskDetailPanelOpen))
            {
                ToggleDockPanel(TaskDetailDocPanel, VM.IsTaskDetailPanelOpen);
            }
            if (e.PropertyName == nameof(MainViewModel.IsTaskImportPanelOpen))
            {
                ToggleDockPanel(TaskImportDocPanel, VM.IsTaskImportPanelOpen);
                if (VM.IsTaskImportPanelOpen)
                {
                    if (VM.TaskProjects.Count == 0) _ = VM.LoadTaskProjectsAsync();
                    TaskImportViewPanel.SetProjects(VM.TaskProjects);
                }
            }
            if (e.PropertyName == nameof(MainViewModel.IsReportBuilderPanelOpen))
            {
                ToggleDockPanel(ReportBuilderDocPanel, VM.IsReportBuilderPanelOpen);
                if (VM.IsReportBuilderPanelOpen)
                {
                    _ = Task.Run(async () =>
                    {
                        var reports = await VM.Repo.GetSavedReportsAsync();
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                            ReportBuilderViewPanel.SetReports(reports));
                    });
                }
            }
            if (e.PropertyName == nameof(MainViewModel.IsTaskDashboardPanelOpen))
            {
                ToggleDockPanel(TaskDashboardDocPanel, VM.IsTaskDashboardPanelOpen);
                if (VM.IsTaskDashboardPanelOpen)
                {
                    _ = Task.Run(async () =>
                    {
                        if (VM.TaskProjects.Count == 0) await VM.LoadTaskProjectsAsync();
                        var dashboards = await VM.Repo.GetDashboardsAsync();
                        var tasks = await VM.Repo.GetTasksAsync();
                        var sprints = await VM.Repo.GetSprintsAsync();
                        System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        {
                            TaskDashboardViewPanel.SetProjects(VM.TaskProjects);
                            TaskDashboardViewPanel.SetDashboards(dashboards);
                            TaskDashboardViewPanel.RefreshCharts(tasks, sprints);
                        });
                    });
                }
            }
            if (e.PropertyName == nameof(MainViewModel.IsRibbonConfigPanelOpen))
            {
                ToggleDockPanel(RibbonConfigPanel, VM.IsRibbonConfigPanelOpen);
                if (VM.IsRibbonConfigPanelOpen) _ = LoadRibbonConfigDataAsync();
            }
            if (e.PropertyName == nameof(MainViewModel.IsServiceDeskPanelOpen))
            {
                ToggleDockPanel(ServiceDeskPanel, VM.IsServiceDeskPanelOpen);
                if (VM.IsServiceDeskPanelOpen) _ = LoadServiceDeskAsync();
            }
            if (e.PropertyName == nameof(MainViewModel.IsSdOverviewPanelOpen))
            {
                ToggleDockPanel(SdOverviewPanel, VM.IsSdOverviewPanelOpen);
                if (VM.IsSdOverviewPanelOpen) { ToggleSdSettings(); _ = LoadSdOverviewAsync(); }
            }
            if (e.PropertyName == nameof(MainViewModel.IsSdClosuresPanelOpen))
            {
                ToggleDockPanel(SdTechClosuresPanel, VM.IsSdClosuresPanelOpen);
                if (VM.IsSdClosuresPanelOpen) { ToggleSdSettings(); _ = LoadSdClosuresAsync(); }
            }
            if (e.PropertyName == nameof(MainViewModel.IsSdAgingPanelOpen))
            {
                ToggleDockPanel(SdAgingPanel, VM.IsSdAgingPanelOpen);
                if (VM.IsSdAgingPanelOpen) { ToggleSdSettings(); _ = LoadSdAgingAsync(); }
            }
            if (e.PropertyName == nameof(MainViewModel.IsSdTeamsPanelOpen))
            {
                ToggleDockPanel(SdTeamsPanel, VM.IsSdTeamsPanelOpen);
                if (VM.IsSdTeamsPanelOpen) _ = LoadSdTeamsAsync();
            }
            if (e.PropertyName == nameof(MainViewModel.IsSdGroupsPanelOpen))
            {
                ToggleDockPanel(SdGroupsPanel, VM.IsSdGroupsPanelOpen);
                if (VM.IsSdGroupsPanelOpen) _ = LoadSdGroupsAsync();
            }
            if (e.PropertyName == nameof(MainViewModel.IsSdTechniciansPanelOpen))
            {
                ToggleDockPanel(SdTechniciansPanel, VM.IsSdTechniciansPanelOpen);
                if (VM.IsSdTechniciansPanelOpen) _ = LoadSdTechniciansAsync();
            }
            if (e.PropertyName == nameof(MainViewModel.IsSdRequestersPanelOpen))
            {
                ToggleDockPanel(SdRequestersPanel, VM.IsSdRequestersPanelOpen);
                if (VM.IsSdRequestersPanelOpen) _ = LoadSdRequestersAsync();
            }
            // ── Global Admin panels ──
            if (e.PropertyName == nameof(MainViewModel.IsGlobalTenantsPanelOpen))
            {
                ToggleDockPanel(GlobalTenantsPanel, VM.IsGlobalTenantsPanelOpen);
                if (VM.IsGlobalTenantsPanelOpen) LoadGlobalAdminPanelAsync("global_tenants");
            }
            if (e.PropertyName == nameof(MainViewModel.IsGlobalUsersPanelOpen))
            {
                ToggleDockPanel(GlobalUsersPanel, VM.IsGlobalUsersPanelOpen);
                if (VM.IsGlobalUsersPanelOpen) LoadGlobalAdminPanelAsync("global_users");
            }
            if (e.PropertyName == nameof(MainViewModel.IsGlobalSubscriptionsPanelOpen))
            {
                ToggleDockPanel(GlobalSubscriptionsPanel, VM.IsGlobalSubscriptionsPanelOpen);
                if (VM.IsGlobalSubscriptionsPanelOpen) LoadGlobalAdminPanelAsync("global_subscriptions");
            }
            if (e.PropertyName == nameof(MainViewModel.IsGlobalLicensesPanelOpen))
            {
                ToggleDockPanel(GlobalLicensesPanel, VM.IsGlobalLicensesPanelOpen);
                if (VM.IsGlobalLicensesPanelOpen) LoadGlobalAdminPanelAsync("global_licenses");
            }
            if (e.PropertyName == nameof(MainViewModel.IsPlatformDashboardPanelOpen))
            {
                ToggleDockPanel(PlatformDashboardPanel, VM.IsPlatformDashboardPanelOpen);
                if (VM.IsPlatformDashboardPanelOpen) _ = LoadPlatformDashboardAsync();
            }
            if (e.PropertyName == nameof(MainViewModel.IsIntegrationsPanelOpen))
            {
                ToggleDockPanel(IntegrationsPanel, VM.IsIntegrationsPanelOpen);
                if (VM.IsIntegrationsPanelOpen) _ = LoadIntegrationsDataAsync();
            }
            if (e.PropertyName == nameof(MainViewModel.IsDiagramPanelOpen))
            {
                ToggleDockPanel(DiagramPanel, VM.IsDiagramPanelOpen);
                if (VM.IsDiagramPanelOpen) _ = BuildNetworkDiagramAsync();
            }
            if (e.PropertyName == nameof(MainViewModel.IsDetailsPanelOpen))
                ToggleDetailsPanel(VM.IsDetailsPanelOpen);
            if (e.PropertyName == nameof(MainViewModel.ShowDefaultVlans))
                _ = RefreshVlanSiteDataAsync();
        };
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // Freeze the UI during load — no panel pop-in/out
        DockManager.Visibility = Visibility.Hidden;
        try
        {

        VM.ActivePanel = ActivePanel.Devices;

        UpdateSplash("Authenticating...", 90);

        // Wait for session init (user, permissions, allowed sites) to complete
        try { await App.SessionReady; }
        catch (Exception ex)
        {
            AppLogger.LogException("Session", ex, "MainWindow.Loaded");
            VM.DbStatus = "Red";
            VM.DbStatusTooltip = $"Login failed: {ex.Message}";
            VM.StatusText = $"Login error: {ex.Message}";
        }

        try { PopulateThemeGallery(); } catch (Exception ex) { AppLogger.LogException("Theme", ex, "PopulateThemeGallery"); }
        try { PopulateBackstage(); } catch (Exception ex) { AppLogger.LogException("Backstage", ex, "PopulateBackstage"); }
        try { PopulateSettingsPanel(); } catch (Exception ex) { AppLogger.LogException("Settings", ex, "PopulateSettingsPanel"); }

        // Restore saved theme
        if (_layout != null)
        {
            try
            {
            var savedTheme = await _layout.GetPreferenceAsync(PreferenceKeys.Theme);
            if (!string.IsNullOrEmpty(savedTheme))
            {
                DevExpress.Xpf.Core.ThemeManager.ApplicationThemeName = savedTheme;
                // Update checked state in gallery
                foreach (var group in ThemeGalleryItem.Gallery.Groups)
                    foreach (var item in group.Items)
                        item.IsChecked = (item.Tag as string) == savedTheme;
            }
            } catch (Exception ex) { AppLogger.LogException("Theme", ex, "RestoreTheme"); }
        }

        // If DB was offline at startup, show status and wait for reconnect
        if (!App.IsDbOnline)
        {
            VM.DbStatus = "Red";
            VM.DbStatusTooltip = "Database offline — retrying every 10s";
            VM.StatusText = "Database offline — app will load data when connection is restored";

            // Listen for reconnect — load data when DB comes back
            if (App.Connectivity != null)
            {
                App.Connectivity.ConnectionChanged += async (_, connected) =>
                {
                    if (!connected) return;
                    App.IsDbOnline = true;

                    // Re-run session init now that DB is available
                    try
                    {
                        await App.SessionReady; // already done (offline path)
                        // Re-init session with real DB data
                        var repo = VM.Repo;
                        var windowsUser = Environment.UserName;
                        var user = await repo.GetUserByUsernameAsync(windowsUser);
                        if (user != null)
                        {
                            var permRepo = new Central.Data.Repositories.PermissionRepository(App.Dsn);
                            var permCodes = await permRepo.GetPermissionCodesForRoleAsync(user.Role);
                            var sites = await permRepo.GetAllowedSitesAsync(user.Role);
                            var authUser = await permRepo.GetUserByUsernameAsync(windowsUser);
                            if (authUser != null)
                                AuthContext.Instance.SetSession(authUser, permCodes, sites);
                        }
                        else
                        {
                            AuthContext.Instance.SetOfflineAdmin(windowsUser);
                        }
                    }
                    catch (Exception ex)
                    {
                        AppLogger.LogException("Session", ex, "ConnectivityManager.Reconnect");
                    }

                    // Now load data on the UI thread
                    await Dispatcher.InvokeAsync(async () =>
                    {
                        try
                        {
                            await VM.LoadAllAsync();
                            BindComboSources();
                            ForceVlanSort();
                            _ = VM.RunPingScanAsync();
                        }
                        catch (Exception ex)
                        {
                            AppLogger.LogException("DB", ex, "Reconnect.LoadAllAsync");
                            VM.DbStatus = "Red";
                            VM.StatusText = $"Reconnect load failed: {ex.Message}";
                        }
                    });
                };
            }
        }
        else
        {
            try
            {
                UpdateSplash("Loading data...", 92);
                await VM.LoadAllAsync();
            }
            catch (Exception ex)
            {
                AppLogger.LogException("DB", ex, "MainWindow.LoadAllAsync");
                VM.DbStatus = "Red";
                VM.DbStatusTooltip = $"Load failed: {ex.Message}";
                VM.StatusText = $"Error: {ex.Message}";
            }
        }
        UpdateSplash("Binding controls...", 94);
        BindComboSources();
        await Task.Yield(); // Let splash repaint
        UpdateSplash("Loading ribbon icons...", 95);
        await PreloadIconOverridesAsync();
        await Task.Yield();
        UpdateSplash("Building ribbon...", 96);
        WireModuleRibbon();
        await Task.Yield();
        UpdateSplash("Applying ribbon customization...", 97);
        await ApplyDbRibbonOverridesAsync();
        await Task.Yield();
        WireModulePanelVisibility();
        WireServiceDeskDelegates();
        WireGridCustomizers();
        InitializeLinkEngine();
        WireUndoRedo();
        EnableMultiSelect();
        ForceVlanSort();

        // Enable floating on ALL panels — drag to second monitor
        EnableGlobalFloating();

        // Hide panels and disable editing based on role
        if (!AuthContext.Instance.CanView("switches"))
            DockManager.DockController.Close(SwitchesPanel);
        if (!AuthContext.Instance.IsAdmin)
            DockManager.DockController.Close(RolesPanel);

        // Apply ribbon permissions based on current user's role
        ApplyRibbonPermissions();

        // Panels that start closed
        DockManager.DockController.Close(UsersPanel);
        DockManager.DockController.Close(LookupsPanel);
        DockManager.DockController.Close(SettingsPanel);
        DockManager.DockController.Close(MasterPanel);
        DockManager.DockController.Close(AsnPanel);
        DockManager.DockController.Close(P2PPanel);
        DockManager.DockController.Close(B2BPanel);
        DockManager.DockController.Close(FWPanel);
        DockManager.DockController.Close(VlansPanel);
        DockManager.DockController.Close(MlagPanel);
        DockManager.DockController.Close(MstpPanel);
        DockManager.DockController.Close(ServerAsPanel);
        DockManager.DockController.Close(IpRangesPanel);
        DockManager.DockController.Close(ServersPanel);
        DockManager.DockController.Close(SshLogsPanel);
        DockManager.DockController.Close(ComparePanel);
        DockManager.DockController.Close(DiagramPanel);
        DockManager.DockController.Close(DeployPanel);
        DockManager.DockController.Close(BuilderPanel);
        DockManager.DockController.Close(BgpPanel);
        DockManager.DockController.Close(JobsPanel);
        DockManager.DockController.Close(TasksPanel);
        DockManager.DockController.Close(BacklogPanel);
        DockManager.DockController.Close(SprintPlanningPanel);
        DockManager.DockController.Close(SprintBurndownDocPanel);
        DockManager.DockController.Close(KanbanBoardDocPanel);
        DockManager.DockController.Close(GanttDocPanel);
        DockManager.DockController.Close(QADocPanel);
        DockManager.DockController.Close(QADashboardDocPanel);
        DockManager.DockController.Close(ReportBuilderDocPanel);
        DockManager.DockController.Close(TaskDashboardDocPanel);
        DockManager.DockController.Close(TimesheetDocPanel);
        DockManager.DockController.Close(ActivityFeedDocPanel);
        DockManager.DockController.Close(MyTasksDocPanel);
        DockManager.DockController.Close(PortfolioDocPanel);
        DockManager.DockController.Close(TaskImportDocPanel);
        DockManager.DockController.Close(TaskDetailDocPanel);
        DockManager.DockController.Close(RibbonConfigPanel);
        DockManager.DockController.Close(IntegrationsPanel);
        DockManager.DockController.Close(ServiceDeskPanel);
        DockManager.DockController.Close(SdOverviewPanel);
        DockManager.DockController.Close(SdTechClosuresPanel);
        DockManager.DockController.Close(SdAgingPanel);
        DockManager.DockController.Close(SdTeamsPanel);
        DockManager.DockController.Close(SdGroupsPanel);
        DockManager.DockController.Close(SdTechniciansPanel);
        DockManager.DockController.Close(SdRequestersPanel);
        DockManager.DockController.Close(SdGroupCatsPanel);
        DockManager.DockController.Hide(SdSettingsLayoutPanel);
        DockManager.DockController.Close(GlobalTenantsPanel);
        DockManager.DockController.Close(GlobalUsersPanel);
        DockManager.DockController.Close(GlobalSubscriptionsPanel);
        DockManager.DockController.Close(GlobalLicensesPanel);
        DockManager.DockController.Close(PlatformDashboardPanel);
        DetailsPanel.Visibility = Visibility.Collapsed;
        VM.IsDetailsPanelOpen = false;

        // Disable grid editing for users without edit permission
        if (!AuthContext.Instance.CanEdit("devices"))
        {
            DeviceGridPanel.View.AllowEditing = false;
            DeviceGridPanel.View.NewItemRowPosition = DevExpress.Xpf.Grid.NewItemRowPosition.None;
        }
        if (!AuthContext.Instance.CanEdit("admin"))
        {
            LookupsGridPanel.View.AllowEditing = false;
            LookupsGridPanel.View.NewItemRowPosition = DevExpress.Xpf.Grid.NewItemRowPosition.None;
            UsersGridPanel.View.AllowEditing = false;
            UsersGridPanel.View.NewItemRowPosition = DevExpress.Xpf.Grid.NewItemRowPosition.None;
        }

        // Init layout service and restore saved layouts
        UpdateSplash("Restoring layout...", 98);
        if (AuthContext.Instance.CurrentUser?.Id > 0)
        {
            _layout = new LayoutService(VM.Repo, AuthContext.Instance.CurrentUser.Id);
            await _layout.RestoreWindowBoundsAsync(this);
            foreach (var (grid, key) in GetGridLayoutMap())
                await _layout.RestoreGridLayoutAsync(grid, key);
            await RestoreDetailTabOrderAsync();
            await _layout.RestoreDockLayoutAsync(DockManager, PreferenceKeys.DockLayout);

            // Re-apply floating after layout restore (saved layout may have AllowFloat=false)
            EnableGlobalFloating();

            // Restore preferences
            var hide = await _layout.GetPreferenceAsync(PreferenceKeys.HideReserved);
            if (hide == "true") VM.HideReserved = true;

            // Restore panel open states
            var panelStates = await _layout.GetPreferenceAsync(PreferenceKeys.PanelStates);
            bool detailsOpen = false;
            if (!string.IsNullOrEmpty(panelStates))
            {
                var states = System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.Dictionary<string, bool>>(panelStates);
                if (states != null)
                {
                    if (states.TryGetValue("devices", out var devices)) VM.IsDevicesPanelOpen = devices;
                    if (states.TryGetValue("switches", out var sw)) VM.IsSwitchesPanelOpen = sw;
                    if (states.TryGetValue("roles", out var roles)) VM.IsRolesPanelOpen = roles;
                    if (states.TryGetValue("users", out var users)) VM.IsUsersPanelOpen = users;
                    if (states.TryGetValue("lookups", out var lookups)) VM.IsLookupsPanelOpen = lookups;
                    if (states.TryGetValue("settings", out var settings)) VM.IsSettingsPanelOpen = settings;
                    if (states.TryGetValue("master", out var master)) VM.IsMasterPanelOpen = master;
                    if (states.TryGetValue("asn", out var asn)) VM.IsAsnPanelOpen = asn;
                    if (states.TryGetValue("p2p", out var p2p)) VM.IsP2PPanelOpen = p2p;
                    if (states.TryGetValue("b2b", out var b2b)) VM.IsB2BPanelOpen = b2b;
                    if (states.TryGetValue("fw", out var fw)) VM.IsFWPanelOpen = fw;
                    if (states.TryGetValue("vlans", out var vlans)) VM.IsVlansPanelOpen = vlans;
                    if (states.TryGetValue("mlag", out var mlag)) VM.IsMlagPanelOpen = mlag;
                    if (states.TryGetValue("mstp", out var mstp)) VM.IsMstpPanelOpen = mstp;
                    if (states.TryGetValue("serveras", out var sas)) VM.IsServerAsPanelOpen = sas;
                    if (states.TryGetValue("ipranges", out var ipr)) VM.IsIpRangesPanelOpen = ipr;
                    if (states.TryGetValue("servers", out var srv)) VM.IsServersPanelOpen = srv;
                    if (states.TryGetValue("sshlogs", out var ssl)) VM.IsSshLogsPanelOpen = ssl;
                    if (states.TryGetValue("jobs", out var jobs)) VM.IsJobsPanelOpen = jobs;
                    if (states.TryGetValue("deploy", out var deploy)) VM.IsDeployPanelOpen = deploy;
                    if (states.TryGetValue("tasks", out var tasks)) VM.IsTasksPanelOpen = tasks;
                    if (states.TryGetValue("backlog", out var backlog)) VM.IsBacklogPanelOpen = backlog;
                    if (states.TryGetValue("sprintplan", out var sprintplan)) VM.IsSprintPlanPanelOpen = sprintplan;
                    if (states.TryGetValue("burndown", out var burndown)) VM.IsBurndownPanelOpen = burndown;
                    if (states.TryGetValue("kanban", out var kanban)) VM.IsKanbanPanelOpen = kanban;
                    if (states.TryGetValue("gantt", out var gantt)) VM.IsGanttPanelOpen = gantt;
                    if (states.TryGetValue("qa", out var qa)) VM.IsQAPanelOpen = qa;
                    if (states.TryGetValue("qadash", out var qadash)) VM.IsQADashboardPanelOpen = qadash;
                    if (states.TryGetValue("reports", out var reports)) VM.IsReportBuilderPanelOpen = reports;
                    if (states.TryGetValue("taskdash", out var taskdash)) VM.IsTaskDashboardPanelOpen = taskdash;
                    if (states.TryGetValue("timesheet", out var timesheet)) VM.IsTimesheetPanelOpen = timesheet;
                    if (states.TryGetValue("actfeed", out var actfeed)) VM.IsActivityFeedPanelOpen = actfeed;
                    if (states.TryGetValue("mytasks", out var mytasks)) VM.IsMyTasksPanelOpen = mytasks;
                    if (states.TryGetValue("portfolio", out var portfolio)) VM.IsPortfolioPanelOpen = portfolio;
                    if (states.TryGetValue("taskimport", out var taskimport)) VM.IsTaskImportPanelOpen = taskimport;
                    if (states.TryGetValue("taskdetail", out var taskdetail)) VM.IsTaskDetailPanelOpen = taskdetail;
                    if (states.TryGetValue("ribboncfg", out var rcfg)) VM.IsRibbonConfigPanelOpen = rcfg;
                    if (states.TryGetValue("servicedesk", out var sd)) VM.IsServiceDeskPanelOpen = sd;
                    if (states.TryGetValue("sdoverview", out var sdo)) VM.IsSdOverviewPanelOpen = sdo;
                    if (states.TryGetValue("sdclosures", out var sdc)) VM.IsSdClosuresPanelOpen = sdc;
                    if (states.TryGetValue("sdaging", out var sda)) VM.IsSdAgingPanelOpen = sda;
                    if (states.TryGetValue("sdteams", out var sdt)) VM.IsSdTeamsPanelOpen = sdt;
                    if (states.TryGetValue("sdgroups", out var sdg)) VM.IsSdGroupsPanelOpen = sdg;
                    if (states.TryGetValue("sdtechnicians", out var sdtn)) VM.IsSdTechniciansPanelOpen = sdtn;
                    if (states.TryGetValue("sdrequesters", out var sdrq)) VM.IsSdRequestersPanelOpen = sdrq;
                    if (states.TryGetValue("integrations", out var intg)) VM.IsIntegrationsPanelOpen = intg;
                    if (states.TryGetValue("global_tenants", out var gt)) VM.IsGlobalTenantsPanelOpen = gt;
                    if (states.TryGetValue("global_users", out var gu)) VM.IsGlobalUsersPanelOpen = gu;
                    if (states.TryGetValue("global_subscriptions", out var gs)) VM.IsGlobalSubscriptionsPanelOpen = gs;
                    if (states.TryGetValue("global_licenses", out var gl)) VM.IsGlobalLicensesPanelOpen = gl;
                    if (states.TryGetValue("platform_dashboard", out var pd)) VM.IsPlatformDashboardPanelOpen = pd;
                    if (states.TryGetValue("details", out var d)) detailsOpen = d;
                }
            }
            // Apply details panel state after dock layout is restored
            VM.IsDetailsPanelOpen = detailsOpen;

            // Restore site sidebar selections
            var siteSel = await _layout.GetPreferenceAsync(PreferenceKeys.SiteSelections);
            if (!string.IsNullOrEmpty(siteSel))
            {
                var selections = System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.Dictionary<string, bool>>(siteSel);
                if (selections != null)
                {
                    foreach (var site in VM.SiteSummaries)
                        if (selections.TryGetValue(site.Building, out var sel))
                            site.IsSelected = sel;
                }
                UpdateDevicesFilter();
            }

            // Restore Devices search text
            var search = await _layout.GetPreferenceAsync(PreferenceKeys.DevicesSearch);
            if (!string.IsNullOrEmpty(search))
            {
                DeviceGridPanel.SearchBox.EditValue = search;
                UpdateDevicesFilter();
            }

            // Restore active ribbon tab
            var tabIdx = await _layout.GetPreferenceAsync(PreferenceKeys.ActiveRibbonTab);
            if (int.TryParse(tabIdx, out var idx) && idx >= 0
                && Ribbon.ActualCategories.Count > 0
                && idx < Ribbon.ActualCategories[0].Pages.Count)
            {
                Ribbon.SelectedPage = Ribbon.ActualCategories[0].Pages[idx];
            }

            // Restore active document tab
            var activeDocName = await _layout.GetPreferenceAsync(PreferenceKeys.ActiveDocTab);
            if (!string.IsNullOrEmpty(activeDocName))
            {
                var panel = this.FindName(activeDocName) as DevExpress.Xpf.Docking.DocumentPanel;
                if (panel != null)
                    DockManager.Activate(panel);
            }

            // Restore grid filters (non-site filters like column filters)
            var filtersJson = await _layout.GetPreferenceAsync(PreferenceKeys.GridFilters);
            if (!string.IsNullOrEmpty(filtersJson))
            {
                var filters = System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.Dictionary<string, string>>(filtersJson);
                if (filters != null)
                    foreach (var (grid, key) in GetFilterMap())
                        if (filters.TryGetValue(key, out var f)) grid.FilterString = f;
            }

            // Re-apply site + reserved filters on top (ensures Switches grid gets filtered too)
            UpdateDevicesFilter();
        }

        // Restore scan settings
        var scanEnabled = await _layout.GetPreferenceAsync(PreferenceKeys.ScanEnabled);
        var scanInterval = await _layout.GetPreferenceAsync(PreferenceKeys.ScanInterval);
        if (int.TryParse(scanInterval, out var mins) && mins > 0)
            VM.ScanIntervalMinutes = mins;
        VM.IsScanEnabled = scanEnabled == "true";

        // Force B2B editing after all layout restores
        B2BGridPanel.View.AllowEditing = true;

        // Reveal UI — all layout is ready, no more pop-in/out
        DockManager.Visibility = Visibility.Visible;

        // Close splash and reveal MainWindow — everything is loaded
        CloseSplash();

        // Auto-ping all switches once on startup
        _ = VM.RunPingScanAsync();

        // Start scan timer if enabled
        if (VM.IsScanEnabled)
            StartScanTimer();

        // Wire real-time SignalR updates (if API server is configured)
        WireSignalRDataChanged();

        // Auto-connect to API server + SignalR on startup (if configured)
        _ = TryAutoConnectApiAsync();

        // Wire notification toasts
        WireNotifications();

        // Wire cross-panel navigation messages
        WirePanelMessages();

        // Wire context menus to all grids
        WireGridContextMenus();

        // Wire jobs panel buttons
        WireJobsPanel();

        // Wire tasks panel
        TaskTreeGridPanel.Tree.ItemsSource = VM.TaskItems;
        TaskTreeGridPanel.SaveTask += async (task) => await VM.SaveTaskAsync(task);
        TaskTreeGridPanel.ProjectChanged += async (projectId) =>
        {
            await VM.LoadSprintsAsync(projectId);
            TaskTreeGridPanel.SetSprints(VM.Sprints);
            await VM.LoadTasksAsync(projectId);
            // Load custom columns for the selected project
            if (projectId.HasValue)
            {
                var customCols = await VM.Repo.GetCustomColumnsAsync(projectId.Value);
                var customVals = await VM.Repo.GetAllCustomValuesAsync(projectId.Value);
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    TaskTreeGridPanel.LoadCustomColumns(customCols);
                    TaskTreeGridPanel.SetCustomValues(customVals);
                });
            }
        };
        TaskTreeGridPanel.AddTaskRequested += async () =>
        {
            await Central.Core.Services.CommandGuard.RunAsync("AddTask", async () =>
            {
                var task = new TaskItem { Title = "New Task", Status = "Open", Priority = "Medium", TaskType = "Task",
                    ProjectId = TaskTreeGridPanel.SelectedProjectId, CreatedAt = DateTime.Now, UpdatedAt = DateTime.Now };
                await VM.SaveTaskAsync(task);
                VM.TaskItems.Insert(0, task);
                try { TaskTreeGridPanel.Tree.CurrentItem = task; } catch { }
            });
        };
        TaskTreeGridPanel.AddSubTaskRequested += async () =>
        {
            await Central.Core.Services.CommandGuard.RunAsync("AddSubTask", async () =>
            {
                var parent = TaskTreeGridPanel.Tree.CurrentItem as TaskItem;
                if (parent == null) { VM.StatusText = "Select a parent task first"; return; }
                var task = new TaskItem { Title = "New Sub-Task", ParentId = parent.Id, Status = "Open", Priority = "Medium", TaskType = "SubTask",
                    ProjectId = parent.ProjectId, CreatedAt = DateTime.Now, UpdatedAt = DateTime.Now };
                await VM.SaveTaskAsync(task);
                VM.TaskItems.Add(task);
                try { TaskTreeGridPanel.Tree.CurrentItem = task; } catch { }
            });
        };

        // Wire task tree selection → detail panel
        TaskTreeGridPanel.Tree.CurrentItemChanged += (s, e) =>
        {
            if (VM.IsTaskDetailPanelOpen && TaskTreeGridPanel.Tree.CurrentItem is Central.Core.Models.TaskItem task)
                TaskDetailViewPanel.ShowTask(task);
        };

        // Wire backlog panel
        BacklogGridPanel.Tree.ItemsSource = VM.TaskItems;
        BacklogGridPanel.SaveTask += async (task) => await VM.SaveTaskAsync(task);
        BacklogGridPanel.ProjectChanged += async (pid) =>
        {
            await VM.LoadSprintsAsync(pid);
            BacklogGridPanel.SetSprints(VM.Sprints);
            await VM.LoadTasksAsync(pid);
        };
        BacklogGridPanel.CommitToSprint += async (taskId, sprintId) => await VM.Repo.CommitToSprintAsync(taskId, sprintId);
        BacklogGridPanel.UncommitFromSprint += async (taskId) => await VM.Repo.UncommitFromSprintAsync(taskId);

        // Wire sprint plan panel
        SprintPlanGridPanel.Grid.ItemsSource = VM.TaskItems;
        SprintPlanGridPanel.SaveTask += async (task) => await VM.SaveTaskAsync(task);
        SprintPlanGridPanel.ProjectChanged += async (pid) =>
        {
            await VM.LoadSprintsAsync(pid);
            SprintPlanGridPanel.SetSprints(VM.Sprints);
        };
        SprintPlanGridPanel.SprintChanged += async (sprintId) =>
        {
            await VM.LoadTasksAsync();
            var sprint = VM.Sprints.FirstOrDefault(s => s.Id == sprintId);
            var stats = await VM.Repo.GetSprintStatsAsync(sprintId);
            var allocs = await VM.Repo.GetSprintAllocationsAsync(sprintId);
            var capacityPts = allocs.Sum(a => a.CapacityPoints ?? 0);
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                // Filter grid to sprint items
                SprintPlanGridPanel.Grid.FilterString = $"[CommittedTo] = {sprintId} OR [SprintId] = {sprintId}";
                SprintPlanGridPanel.UpdateCapacityBar(stats.totalPoints, capacityPts > 0 ? capacityPts : stats.totalPoints, stats.itemCount, sprint?.Goal, sprint);
            });
        };
        SprintPlanGridPanel.CreateSprint += async (sprint) =>
        {
            await VM.Repo.UpsertSprintAsync(sprint);
            await VM.LoadSprintsAsync(sprint.ProjectId);
            System.Windows.Application.Current.Dispatcher.Invoke(() => SprintPlanGridPanel.SetSprints(VM.Sprints));
            VM.StatusText = $"Sprint created: {sprint.Name}";
        };
        SprintPlanGridPanel.CloseSprint += async (sprintId) =>
        {
            await VM.Repo.CloseSprintAsync(sprintId);
            await VM.LoadSprintsAsync();
            System.Windows.Application.Current.Dispatcher.Invoke(() => SprintPlanGridPanel.SetSprints(VM.Sprints));
            VM.StatusText = $"Sprint closed";
        };

        // Wire burndown panel
        SprintBurndownChartPanel.SprintChanged += async (sprintId) =>
        {
            var data = await VM.Repo.GetSprintBurndownAsync(sprintId);
            var sprint = VM.Sprints.FirstOrDefault(s => s.Id == sprintId);
            System.Windows.Application.Current.Dispatcher.Invoke(() => SprintBurndownChartPanel.LoadBurndown(data, sprint));
        };
        SprintBurndownChartPanel.SnapshotRequested += async (sprintId) =>
        {
            await VM.Repo.SnapshotSprintBurndownAsync(sprintId);
            var data = await VM.Repo.GetSprintBurndownAsync(sprintId);
            var sprint = VM.Sprints.FirstOrDefault(s => s.Id == sprintId);
            System.Windows.Application.Current.Dispatcher.Invoke(() => SprintBurndownChartPanel.LoadBurndown(data, sprint));
            VM.StatusText = $"Burndown snapshot saved";
        };

        // Wire kanban board panel
        KanbanBoardViewPanel.ProjectChanged += async (pid) =>
        {
            if (!pid.HasValue) return;
            var columns = await VM.Repo.GetBoardColumnsAsync(pid.Value);
            await VM.LoadTasksAsync(pid);
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
                KanbanBoardViewPanel.LoadBoard(columns, VM.TaskItems.ToList()));
        };
        KanbanBoardViewPanel.CardMoved += async (taskId, colName, status) =>
        {
            await VM.Repo.MoveTaskToColumnAsync(taskId, colName, status);
            VM.StatusText = $"Task moved to {colName}";
        };
        KanbanBoardViewPanel.CardDoubleClicked += (task) =>
        {
            VM.IsTaskDetailPanelOpen = true;
            TaskDetailViewPanel.ShowTask(task);
        };
        KanbanBoardViewPanel.AddCardRequested += () =>
        {
            VM.IsTasksPanelOpen = true;
        };

        // Wire Gantt panel
        GanttViewPanel.ProjectChanged += async (pid) =>
        {
            if (!pid.HasValue) return;
            var tasks = (await VM.Repo.GetTasksAsync(pid)).Where(t => t.StartDate.HasValue || t.FinishDate.HasValue || t.IsMilestone).ToList();
            var links = await VM.Repo.GetGanttLinksAsync(pid);
            System.Windows.Application.Current.Dispatcher.Invoke(() => GanttViewPanel.LoadGantt(tasks, links));
        };
        GanttViewPanel.SaveBaselineRequested += async (projectId, name) =>
        {
            var cnt = await VM.Repo.SaveProjectBaselineAsync(projectId, name);
            VM.StatusText = $"Baseline saved: {name} ({cnt} tasks)";
        };

        // Wire QA panel
        QAGridPanel.SaveBug += async (task) => await VM.SaveTaskAsync(task);
        QAGridPanel.ProjectChanged += async (pid) =>
        {
            var bugs = await VM.Repo.GetBugsAsync(pid);
            System.Windows.Application.Current.Dispatcher.Invoke(() => QAGridPanel.Grid.ItemsSource = bugs);
        };
        QAGridPanel.NewBugRequested += () =>
        {
            _ = Central.Core.Services.CommandGuard.RunAsync("AddBug", async () =>
            {
                var bug = new TaskItem
                {
                    Title = "New Bug", Status = "New", Priority = "Medium", TaskType = "Bug",
                    Severity = "Major", BugPriority = "Medium", Category = "Bug",
                    ProjectId = QAGridPanel.SelectedProjectId,
                    CreatedAt = DateTime.Now, UpdatedAt = DateTime.Now
                };
                await VM.SaveTaskAsync(bug);
                var bugs = await VM.Repo.GetBugsAsync(QAGridPanel.SelectedProjectId);
                System.Windows.Application.Current.Dispatcher.Invoke(() => QAGridPanel.Grid.ItemsSource = bugs);
            });
        };
        QAGridPanel.BatchTriage += async (ids, sev, pri) =>
        {
            await VM.Repo.BatchTriageBugsAsync(ids, sev, pri);
            var bugs = await VM.Repo.GetBugsAsync(QAGridPanel.SelectedProjectId);
            System.Windows.Application.Current.Dispatcher.Invoke(() => QAGridPanel.Grid.ItemsSource = bugs);
            VM.StatusText = $"Triaged {ids.Count} bugs";
        };

        // Wire QA dashboard
        QADashboardViewPanel.ProjectChanged += async (pid) =>
        {
            var bugs = await VM.Repo.GetBugsAsync(pid);
            System.Windows.Application.Current.Dispatcher.Invoke(() => QADashboardViewPanel.RefreshCharts(bugs));
        };
        QADashboardViewPanel.LoadBugs += async (pid) => await VM.Repo.GetBugsAsync(pid);

        // Wire report builder
        ReportBuilderViewPanel.RunQuery += async (query) =>
        {
            // Execute query against tasks and return as DataTable
            var tasks = await VM.Repo.GetTasksAsync();
            var dt = new System.Data.DataTable("Results");
            // Build columns dynamically
            var props = typeof(TaskItem).GetProperties()
                .Where(p => p.PropertyType == typeof(string) || p.PropertyType == typeof(int) || p.PropertyType == typeof(int?)
                    || p.PropertyType == typeof(decimal) || p.PropertyType == typeof(decimal?)
                    || p.PropertyType == typeof(DateTime) || p.PropertyType == typeof(DateTime?) || p.PropertyType == typeof(bool))
                .ToList();
            foreach (var p in props) dt.Columns.Add(p.Name, Nullable.GetUnderlyingType(p.PropertyType) ?? p.PropertyType);

            // Apply filters
            var filtered = tasks.AsEnumerable();
            foreach (var f in query.Filters)
            {
                var prop = props.FirstOrDefault(p => p.Name == f.Field);
                if (prop == null) continue;
                filtered = filtered.Where(t =>
                {
                    var val = prop.GetValue(t)?.ToString() ?? "";
                    return f.Operator switch
                    {
                        "=" => val.Equals(f.Value, StringComparison.OrdinalIgnoreCase),
                        "!=" => !val.Equals(f.Value, StringComparison.OrdinalIgnoreCase),
                        "contains" => val.Contains(f.Value, StringComparison.OrdinalIgnoreCase),
                        "isNull" => string.IsNullOrEmpty(val),
                        "isNotNull" => !string.IsNullOrEmpty(val),
                        _ => true
                    };
                });
            }

            foreach (var task in filtered)
            {
                var row = dt.NewRow();
                foreach (var p in props) row[p.Name] = p.GetValue(task) ?? DBNull.Value;
                dt.Rows.Add(row);
            }
            return dt;
        };
        ReportBuilderViewPanel.SaveReport += async (report) =>
        {
            await VM.Repo.UpsertSavedReportAsync(report);
            VM.StatusText = $"Report saved: {report.Name}";
        };

        // Wire task dashboard
        TaskDashboardViewPanel.ProjectChanged += async (pid) =>
        {
            var tasks = await VM.Repo.GetTasksAsync(pid);
            var sprints = await VM.Repo.GetSprintsAsync(pid);
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
                TaskDashboardViewPanel.RefreshCharts(tasks, sprints));
        };
        TaskDashboardViewPanel.LoadAllTasks += async (pid) => await VM.Repo.GetTasksAsync(pid);
        TaskDashboardViewPanel.LoadAllSprints += async () => await VM.Repo.GetSprintsAsync();

        // Wire timesheet
        TimesheetViewPanel.SaveEntry += async (entry) =>
        {
            await VM.Repo.UpsertTimeEntryAsync(entry);
            VM.StatusText = $"Time entry saved: {entry.Hours}h";
        };
        TimesheetViewPanel.WeekChanged += async (from, to) =>
        {
            var userId = Central.Core.Auth.AuthContext.Instance.CurrentUser?.Id;
            var entries = await VM.Repo.GetTimeEntriesAsync(userId, from, to);
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                TimesheetViewPanel.Grid.ItemsSource = entries;
                TimesheetViewPanel.UpdateTotal(entries.Sum(e => e.Hours));
            });
        };

        // Wire activity feed
        ActivityFeedViewPanel.ProjectChanged += async (pid) =>
        {
            var feed = await VM.Repo.GetActivityFeedAsync(pid);
            System.Windows.Application.Current.Dispatcher.Invoke(() => ActivityFeedViewPanel.LoadItems(feed));
        };
        ActivityFeedViewPanel.LoadFeed += async (pid) => await VM.Repo.GetActivityFeedAsync(pid);

        // Wire my tasks
        MyTasksViewPanel.SaveTask += async (task) => await VM.SaveTaskAsync(task);

        // Wire portfolio
        PortfolioViewPanel.LoadPortfolio += async () => await BuildPortfolioTreeAsync();

        // Wire task import
        TaskImportViewPanel.ParseFile += async (filePath) =>
        {
            return await Task.Run(() => Central.Core.Services.TaskFileParser.ParseFile(filePath));
        };
        TaskImportViewPanel.ImportTasks += async (tasks, updateExisting) =>
        {
            int count = 0;
            foreach (var task in tasks)
            {
                if (updateExisting && !string.IsNullOrEmpty(task.Title))
                {
                    // Check for existing by title in same project
                    var existing = VM.TaskItems.FirstOrDefault(t => t.Title == task.Title && t.ProjectId == task.ProjectId);
                    if (existing != null) task.Id = existing.Id;
                }
                await VM.SaveTaskAsync(task);
                count++;
            }
            await VM.LoadTasksAsync();
            return count;
        };
        }
        catch (Exception ex)
        {
            AppLogger.LogException("Startup", ex, "MainWindow_Loaded");
            VM.StatusText = $"Startup error: {ex.Message}";
        }
        finally
        {
            // Always reveal UI even if startup had errors
            DockManager.Visibility = Visibility.Visible;
            CloseSplash();
        }
    }

    private async void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_layout == null) return;
        // Block shutdown until save completes
        e.Cancel = true;
        StopScanTimer();
        try
        {
            await SaveAllLayoutAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Save layout error: {ex.Message}");
        }
        _layout = null; // prevent re-entry
        System.Windows.Application.Current.Shutdown();
    }

    /// <summary>Central map of all grids to their layout preference keys.</summary>
    private IEnumerable<(DevExpress.Xpf.Grid.GridControl grid, string key)> GetGridLayoutMap()
    {
        yield return (DeviceGridPanel.Grid, PreferenceKeys.DevicesGrid);
        yield return (SwitchGridPanel.Grid, PreferenceKeys.SwitchGrid);
        yield return (UsersGridPanel.Grid, PreferenceKeys.UsersGrid);
        yield return (RolesGridPanel.Grid, PreferenceKeys.RolesGrid);
        yield return (SettingsGrid, PreferenceKeys.SettingsGrid);
        yield return (MasterGridPanel.Grid, PreferenceKeys.MasterGrid);
        yield return (P2PGridPanel.Grid, PreferenceKeys.P2PGrid);
        yield return (VlanGridPanel.Grid, PreferenceKeys.VlansGrid);
        yield return (MlagGridPanel.Grid, PreferenceKeys.MlagGrid);
        yield return (MstpGridPanel.Grid, PreferenceKeys.MstpGrid);
        yield return (ServerAsGridPanel.Grid, PreferenceKeys.ServerAsGrid);
        yield return (IpRangesGridPanel.Grid, PreferenceKeys.IpRangesGrid);
        yield return (ServersGrid, PreferenceKeys.ServersGrid);
        yield return (DetailTabsPanel.Interfaces, PreferenceKeys.InterfacesGrid);
        yield return (B2BGridPanel.Grid, "layout.b2b");
        yield return (FWGridPanel.Grid, "layout.fw");
        yield return (AsnGridPanel.Grid, "layout.asn");
        yield return (BgpGridPanel.Grid, "layout.bgp");
        yield return (RequestGridPanel.Grid, "layout.sd_requests");
    }

    private IEnumerable<(DevExpress.Xpf.Grid.GridControl grid, string key)> GetFilterMap()
    {
        yield return (DeviceGridPanel.Grid, "devices");
        yield return (MasterGridPanel.Grid, "master");
        yield return (P2PGridPanel.Grid, "p2p");
        yield return (B2BGridPanel.Grid, "b2b");
        yield return (FWGridPanel.Grid, "fw");
        yield return (VlanGridPanel.Grid, "vlans");
        yield return (MlagGridPanel.Grid, "mlag");
        yield return (MstpGridPanel.Grid, "mstp");
        yield return (ServerAsGridPanel.Grid, "serveras");
        yield return (IpRangesGridPanel.Grid, "ipranges");
        yield return (ServersGrid, "servers");
        yield return (SwitchGridPanel.Grid, "switches");
    }

    /// <summary>Save everything — called on close and from Save Layout button.</summary>
    private async Task SaveAllLayoutAsync()
    {
        if (_layout == null) return;
        await _layout.SaveWindowBoundsAsync(this);
        foreach (var (grid, key) in GetGridLayoutMap())
            await _layout.SaveGridLayoutAsync(grid, key);
        await SaveDetailTabOrderAsync();

        // Ensure DetailsPanel is visible so DX serializes its dock position
        var wasHidden = DetailsPanel.Visibility == Visibility.Collapsed;
        if (wasHidden) DetailsPanel.Visibility = Visibility.Visible;
        await _layout.SaveDockLayoutAsync(DockManager, PreferenceKeys.DockLayout);
        if (wasHidden) DetailsPanel.Visibility = Visibility.Collapsed;
        await _layout.SavePreferenceAsync(PreferenceKeys.HideReserved, VM.HideReserved ? "true" : "false");
        await SavePanelStatesAsync();

        // Save site sidebar selections
        var siteSelections = new System.Collections.Generic.Dictionary<string, bool>();
        foreach (var s in VM.SiteSummaries)
            siteSelections[s.Building] = s.IsSelected;
        await _layout.SavePreferenceAsync(PreferenceKeys.SiteSelections,
            System.Text.Json.JsonSerializer.Serialize(siteSelections));

        // Save Devices search text
        var searchText = DeviceGridPanel.SearchBox.EditValue as string ?? "";
        await _layout.SavePreferenceAsync(PreferenceKeys.DevicesSearch, searchText);

        // Save active ribbon tab index
        if (Ribbon.SelectedPage != null && Ribbon.ActualCategories.Count > 0)
        {
            var idx = Ribbon.ActualCategories[0].Pages.IndexOf(Ribbon.SelectedPage);
            await _layout.SavePreferenceAsync(PreferenceKeys.ActiveRibbonTab, idx >= 0 ? idx.ToString() : "0");
        }

        // Save active document tab
        var allDocPanels = new DevExpress.Xpf.Docking.DocumentPanel[]
        {
            DevicesPanel, MasterPanel, AsnPanel, P2PPanel, B2BPanel, FWPanel,
            VlansPanel, MlagPanel, MstpPanel, ServerAsPanel, IpRangesPanel,
            ServersPanel, SwitchesPanel, RolesPanel, UsersPanel, LookupsPanel, SettingsPanel, SshLogsPanel
        };
        var activeDoc = allDocPanels.FirstOrDefault(p => p.IsActive);
        if (activeDoc != null)
            await _layout.SavePreferenceAsync(PreferenceKeys.ActiveDocTab, activeDoc.Name ?? "");

        // Save grid filter strings
        var filters = new System.Collections.Generic.Dictionary<string, string>();
        foreach (var (grid, key) in GetFilterMap())
            if (!string.IsNullOrEmpty(grid.FilterString)) filters[key] = grid.FilterString;
        if (filters.Count > 0)
            await _layout.SavePreferenceAsync(PreferenceKeys.GridFilters,
                System.Text.Json.JsonSerializer.Serialize(filters));
        else
            await _layout.SavePreferenceAsync(PreferenceKeys.GridFilters, "");

        // Save scan settings
        await _layout.SavePreferenceAsync(PreferenceKeys.ScanEnabled, VM.IsScanEnabled ? "true" : "false");
        await _layout.SavePreferenceAsync(PreferenceKeys.ScanInterval, VM.ScanIntervalMinutes.ToString());

        // Stop scan timer
        StopScanTimer();
    }

    private void EnableMultiSelect()
    {
        var grids = new[] { DeviceGridPanel.Grid, SwitchGridPanel.Grid, AsnGridPanel.Grid, P2PGridPanel.Grid, B2BGridPanel.Grid, FWGridPanel.Grid,
                            VlanGridPanel.Grid, MlagGridPanel.Grid, MstpGridPanel.Grid, ServerAsGridPanel.Grid, IpRangesGridPanel.Grid, ServersGrid, MasterGridPanel.Grid };
        foreach (var g in grids)
        {
            if (g?.View is DevExpress.Xpf.Grid.TableView tv)
                tv.MultiSelectMode = DevExpress.Xpf.Grid.TableViewSelectMode.Row;
        }
    }

    // Track old→new changes per VLAN row for audit logging
    // _vlanPendingChanges moved to VlanGridPanel

    // VLAN handlers → Module.VLANs/Views/VlanGridPanel.xaml.cs

    private void ForceVlanSort()
    {
        var col = VlanGridPanel.Grid.Columns["VlanIdSort"];
        if (col == null) return;
        VlanGridPanel.Grid.ClearSorting();
        col.SortIndex = 0;
        col.SortOrder = DevExpress.Data.ColumnSortOrder.Ascending;
        // Expand all /21 block groups
        VlanGridPanel.Grid.ExpandAllGroups();
    }

    private void BindComboSources()
    {
        // Init/refresh helpers with current data
        _linkEditor = new LinkEditorHelper(VM.Devices, VM.Switches, VM.BuildingOptions, VM.Repo);
        _syncService ??= new SwitchSyncService(VM.Repo);

        DeviceGridPanel.BindComboSources(VM.StatusOptions, VM.DeviceTypeOptions,
            VM.BuildingOptions, VM.RegionOptions, VM.AsnDefinitions);
        UsersGridPanel.BindComboSources(VM.RoleNames);
        UsersGridPanel.LoadDetailPermissions += async user =>
        {
            try
            {
                var perms = await VM.Repo.GetPermissionGrantsForRoleAsync(user.Role);
                user.DetailPermissions.Clear();
                foreach (var p in perms)
                    user.DetailPermissions.Add(new UserPermissionDetail
                    {
                        Code = p.Code, Name = p.Name, Category = p.Category, Granted = true
                    });
            }
            catch (Exception ex) { AppLogger.LogException("Users", ex, $"LoadPermissions:{user.Username}"); }
        };

        // Module panel combo sources
        MasterGridPanel.BindComboSources(VM.StatusOptions, VM.AsnDefinitions);
        AsnGridPanel.BindComboSources(VM.Devices);
        P2PGridPanel.BindStatusCombo(VM.StatusOptions);
        B2BGridPanel.BindComboSources(VM.BuildingOptions, VM.StatusOptions);
        FWGridPanel.BindComboSources(VM.BuildingOptions, VM.StatusOptions);
        VlanGridPanel.BindComboSources(new[] { "/24", "/21" }, VM.StatusOptions);
        VlanGridPanel.AllVlanEntries = VM.VlanEntries;
        VlanGridPanel.BlockLockedChanged += () => VM.PropagateBlockLocked();
        VlanGridPanel.LoadDetailSites += async vlan =>
        {
            vlan.DetailSites.Clear();
            foreach (var v in VM.VlanEntries)
            {
                if (v.VlanId == vlan.VlanId && v.Id != vlan.Id)
                    vlan.DetailSites.Add(new VlanSiteDetail
                    {
                        Building = v.Site,
                        VlanName = v.Name,
                        Gateway = v.Gateway,
                        Subnet = v.Subnet,
                        Status = v.Status
                    });
            }
        };
        VlanGridPanel.SaveVlan += async vlan =>
        {
            var result = await VM.Repo.SafeWriteAsync(() => VM.Repo.UpsertVlanEntryAsync(vlan), "VlanSave");
            VM.StatusText = result.Success ? $"VLAN saved: {vlan.VlanId}" : $"VLAN save failed: {result.Error}";
            if (result.Success) Central.Core.Shell.PanelMessageBus.Publish(
                new Central.Core.Shell.DataModifiedMessage("vlans", "VLAN", "Update"));
        };
        MlagGridPanel.BindComboSources(VM.StatusOptions);
        MstpGridPanel.BindComboSources(VM.StatusOptions);
        ServerAsGridPanel.BindComboSources(VM.StatusOptions);
        IpRangesGridPanel.BindComboSources(VM.StatusOptions);

        // B2B building combos
        
        // P2P/B2B port dropdowns wired dynamically in ShownEditor handlers
    }

    /// <summary>
    /// Add module-registered ribbon pages to the DX RibbonControl.
    /// Phase 2: adds alongside existing static pages.
    /// Phase 3: replaces static pages entirely.
    /// </summary>
    private void WireModuleRibbon()
    {
        var auth = AuthContext.Instance;
        foreach (var page in App.RibbonBuilder.GetVisiblePages())
        {
            // Merge into existing static page, or create new
            var existing = DefaultRibbonCategory.Pages.OfType<DevExpress.Xpf.Ribbon.RibbonPage>()
                .FirstOrDefault(p => string.Equals(p.Caption?.ToString(), page.Header, StringComparison.OrdinalIgnoreCase));

            var ribbonPage = existing ?? new DevExpress.Xpf.Ribbon.RibbonPage { Caption = page.Header };

            foreach (var group in page.Groups)
            {
                // Skip if group already exists in static page
                if (existing != null)
                {
                    var existingGroup = ribbonPage.Groups.OfType<DevExpress.Xpf.Ribbon.RibbonPageGroup>()
                        .FirstOrDefault(g => string.Equals(g.Caption?.ToString(), group.Header, StringComparison.OrdinalIgnoreCase));
                    if (existingGroup != null) continue;
                }

                var ribbonGroup = new DevExpress.Xpf.Ribbon.RibbonPageGroup { Caption = group.Header, Tag = "dynamic" };

                foreach (var registration in group.Items)
                {
                    switch (registration)
                    {
                        case RibbonButtonRegistration btn:
                            if (!string.IsNullOrEmpty(btn.Permission) && !auth.HasPermission(btn.Permission)) continue;
                            var barBtn = new BarButtonItem { Content = btn.Content };
                            // Large style applied via XAML RibbonStyle="Large" — code equivalent not needed
                            if (!string.IsNullOrEmpty(btn.ToolTip)) barBtn.Description = btn.ToolTip;
                            AssignGlyph(barBtn, btn.Glyph, btn.LargeGlyph);
                            var click = btn.OnClick;
                            barBtn.ItemClick += (_, _) => click();
                            ribbonGroup.Items.Add(barBtn);
                            break;

                        case RibbonSplitButtonRegistration split:
                            if (!string.IsNullOrEmpty(split.Permission) && !auth.HasPermission(split.Permission)) continue;
                            var barSplit = new BarSplitButtonItem { Content = split.Content };
                            // Large style applied via XAML RibbonStyle="Large" — code equivalent not needed
                            AssignGlyph(barSplit, split.Glyph, split.LargeGlyph);
                            if (split.OnClick != null)
                            {
                                var primary = split.OnClick;
                                barSplit.ItemClick += (_, _) => primary();
                            }
                            var popup = new PopupMenu();
                            foreach (var sub in split.SubItems)
                            {
                                if (!string.IsNullOrEmpty(sub.Permission) && !auth.HasPermission(sub.Permission)) continue;
                                var subBtn = new BarButtonItem { Content = sub.Content };
                                AssignGlyph(subBtn, sub.Glyph, sub.LargeGlyph);
                                var subClick = sub.OnClick;
                                subBtn.ItemClick += (_, _) => subClick();
                                popup.Items.Add(subBtn);
                            }
                            barSplit.PopupControl = popup;
                            ribbonGroup.Items.Add(barSplit);
                            break;

                        case RibbonCheckButtonRegistration chk:
                            var panel = FindDockPanel(chk.PanelId);
                            if (panel == null) continue;
                            var barChk = new BarCheckItem
                            {
                                Content = chk.Content,
                                IsChecked = panel.Visibility == Visibility.Visible
                            };
                            AssignGlyph(barChk, chk.Glyph, null);
                            var p = panel;
                            barChk.CheckedChanged += (_, _) =>
                            {
                                try
                                {
                                    if (barChk.IsChecked == true)
                                    {
                                        DockManager.DockController.Restore(p);
                                        // Sync VM boolean so PropertyChanged fires data load
                                        SyncPanelOpenState(p, true);
                                    }
                                    else
                                    {
                                        DockManager.DockController.Close(p);
                                    }
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"Panel toggle error: {ex.Message}");
                                    // Panel may not exist in saved layout — safe to ignore
                                }
                            };
                            ribbonGroup.Items.Add(barChk);
                            break;

                        case RibbonToggleRegistration tog:
                            if (!string.IsNullOrEmpty(tog.Permission) && !auth.HasPermission(tog.Permission)) continue;
                            var barTog = new BarCheckItem { Content = tog.Content };
                            AssignGlyph(barTog, tog.Glyph, null);
                            var toggle = tog.OnToggle;
                            barTog.CheckedChanged += (_, _) => toggle(barTog.IsChecked == true);
                            ribbonGroup.Items.Add(barTog);
                            break;

                        case RibbonSeparatorRegistration:
                            ribbonGroup.Items.Add(new BarItemSeparator());
                            break;
                    }
                }

                if (ribbonGroup.Items.Count > 0)
                    ribbonPage.Groups.Add(ribbonGroup);
            }

            // Only add if it's a new page (not existing static)
            if (existing == null && ribbonPage.Groups.Count > 0)
            {
                // Insert by sort order so modules appear in the correct ribbon position
                // Static pages: Home=0, Devices=10, Switches=20, Builder=30
                // Dynamic pages insert based on their module SortOrder
                int insertIndex = DefaultRibbonCategory.Pages.Count; // default: append
                for (int i = 0; i < DefaultRibbonCategory.Pages.Count; i++)
                {
                    var existingPage = DefaultRibbonCategory.Pages[i] as DevExpress.Xpf.Ribbon.RibbonPage;
                    if (existingPage?.Tag is int existingOrder && page.SortOrder < existingOrder)
                    {
                        insertIndex = i;
                        break;
                    }
                }
                ribbonPage.Tag = page.SortOrder;
                DefaultRibbonCategory.Pages.Insert(insertIndex, ribbonPage);
            }
        }
    }

    /// <summary>Assign DXImage glyph to a bar item. Tries small glyph first, then large.</summary>
    private static void AssignGlyph(BarItem item, string? glyph, string? largeGlyph)
    {
        try
        {
            // Check preloaded overrides FIRST — skip DX default if override exists
            var content = item.Content?.ToString() ?? "";
            if (_preloadedOverrides.TryGetValue(content, out var overrideIcon))
            {
                var img = ResolveGlyphImage(overrideIcon);
                if (img != null) { item.Glyph = img; item.LargeGlyph = img; return; }
            }

            // Default DX icon
            if (!string.IsNullOrEmpty(largeGlyph))
                item.LargeGlyph = ResolveGlyphImage(largeGlyph);
            if (!string.IsNullOrEmpty(glyph))
                item.Glyph = ResolveGlyphImage(glyph);
        }
        catch { /* glyph not found — silently skip */ }
    }

    /// <summary>Sync module ribbon registrations to DB tables so admin panel can edit them.</summary>
    /// <summary>Sync the LIVE ribbon (static XAML + dynamic modules) to DB tables.</summary>
    private async Task SyncModuleRibbonToDbAsync()
    {
        try
        {
            // Walk the actual rendered DX RibbonControl — captures BOTH static and dynamic pages
            int pageSortOrder = 0;
            foreach (var dxPage in DefaultRibbonCategory.Pages.OfType<DevExpress.Xpf.Ribbon.RibbonPage>())
            {
                var pageCaption = dxPage.Caption?.ToString() ?? "";
                if (string.IsNullOrEmpty(pageCaption)) continue;

                var pages = await VM.Repo.GetRibbonPagesAsync();
                var dbPage = pages.FirstOrDefault(p => string.Equals(p.Header, pageCaption, StringComparison.OrdinalIgnoreCase));
                if (dbPage == null)
                {
                    dbPage = new Central.Core.Models.RibbonPageConfig
                    {
                        Header = pageCaption, SortOrder = pageSortOrder * 10, IsSystem = true, IsVisible = dxPage.IsVisible
                    };
                    await VM.Repo.UpsertRibbonPageAsync(dbPage);
                }
                pageSortOrder++;

                int groupSortOrder = 0;
                foreach (var dxGroup in dxPage.Groups.OfType<DevExpress.Xpf.Ribbon.RibbonPageGroup>())
                {
                    var groupCaption = dxGroup.Caption?.ToString() ?? "";
                    if (string.IsNullOrEmpty(groupCaption)) continue;

                    var groups = await VM.Repo.GetRibbonGroupsAsync(dbPage.Id);
                    var dbGroup = groups.FirstOrDefault(g => string.Equals(g.Header, groupCaption, StringComparison.OrdinalIgnoreCase));
                    if (dbGroup == null)
                    {
                        dbGroup = new Central.Core.Models.RibbonGroupConfig
                        {
                            PageId = dbPage.Id, Header = groupCaption, SortOrder = groupSortOrder * 10, IsVisible = true
                        };
                        await VM.Repo.UpsertRibbonGroupAsync(dbGroup);
                    }
                    groupSortOrder++;

                    int itemSort = 0;
                    foreach (var dxItem in dxGroup.Items)
                    {
                        string content, itemType = "button";
                        string? permission = null;

                        if (dxItem is BarItemSeparator)
                        {
                            content = "───";
                            itemType = "separator";
                        }
                        else if (dxItem is BarItem barItem)
                        {
                            content = barItem.Content?.ToString() ?? "";
                            if (string.IsNullOrEmpty(content)) continue;
                            if (dxItem is BarCheckItem) itemType = "check";
                            else if (dxItem is BarSplitButtonItem) itemType = "split";
                        }
                        else continue;

                        await VM.Repo.UpsertRibbonItemAsync(new Central.Core.Models.RibbonItemConfig
                        {
                            GroupId = dbGroup.Id, Content = content, ItemType = itemType,
                            SortOrder = itemSort++, Permission = permission, IsSystem = true, IsVisible = true
                        });
                    }
                }
            }
            // ALSO sync module registrations (captures pages not visible in DX due to permissions)
            foreach (var pageReg in App.RibbonBuilder.Pages)
            {
                var pages = await VM.Repo.GetRibbonPagesAsync();
                var dbPage = pages.FirstOrDefault(p => string.Equals(p.Header, pageReg.Header, StringComparison.OrdinalIgnoreCase));
                if (dbPage == null)
                {
                    dbPage = new Central.Core.Models.RibbonPageConfig
                    {
                        Header = pageReg.Header, SortOrder = pageReg.SortOrder,
                        RequiredPermission = pageReg.RequiredPermission, IsSystem = true, IsVisible = true
                    };
                    await VM.Repo.UpsertRibbonPageAsync(dbPage);
                }

                foreach (var groupReg in pageReg.Groups)
                {
                    var groups = await VM.Repo.GetRibbonGroupsAsync(dbPage.Id);
                    var dbGroup = groups.FirstOrDefault(g => string.Equals(g.Header, groupReg.Header, StringComparison.OrdinalIgnoreCase));
                    if (dbGroup == null)
                    {
                        dbGroup = new Central.Core.Models.RibbonGroupConfig
                        {
                            PageId = dbPage.Id, Header = groupReg.Header, IsVisible = true
                        };
                        await VM.Repo.UpsertRibbonGroupAsync(dbGroup);
                    }

                    int sort = 0;
                    foreach (var itemReg in groupReg.Items)
                    {
                        string content, itemType = "button";
                        string? perm = null;

                        switch (itemReg)
                        {
                            case Central.Core.Shell.RibbonButtonRegistration btn:
                                content = btn.Content; perm = btn.Permission; break;
                            case Central.Core.Shell.RibbonCheckButtonRegistration chk:
                                content = chk.Content; itemType = "check"; break;
                            case Central.Core.Shell.RibbonToggleRegistration tog:
                                content = tog.Content; itemType = "toggle"; perm = tog.Permission; break;
                            case Central.Core.Shell.RibbonSplitButtonRegistration split:
                                content = split.Content; itemType = "split"; perm = split.Permission; break;
                            case Central.Core.Shell.RibbonSeparatorRegistration:
                                content = "───"; itemType = "separator"; break;
                            default: continue;
                        }

                        // Check if already exists
                        var items = await VM.Repo.GetRibbonItemsAsync(dbGroup.Id);
                        if (!items.Any(i => string.Equals(i.Content, content, StringComparison.OrdinalIgnoreCase)))
                        {
                            await VM.Repo.UpsertRibbonItemAsync(new Central.Core.Models.RibbonItemConfig
                            {
                                GroupId = dbGroup.Id, Content = content, ItemType = itemType,
                                SortOrder = sort, Permission = perm, IsSystem = true, IsVisible = true
                            });
                        }
                        sort++;
                    }
                }
            }
        }
        catch (Exception ex) { AppLogger.LogException("Ribbon", ex, "SyncModuleRibbonToDbAsync"); }
    }

    /// <summary>Apply DB ribbon_items overrides (icon, visibility) to already-built ribbon buttons.</summary>
    private async Task ApplyDbRibbonOverridesAsync()
    {
        try
        {
            // Build unified override map: admin_ribbon_defaults + ribbon_items (admin defaults win for static buttons)
            var adminDefaults = await VM.Repo.GetAdminRibbonDefaultsAsync();
            var adminMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (itemKey, icon, text, hidden) in adminDefaults)
            {
                if (!string.IsNullOrEmpty(icon))
                {
                    adminMap[itemKey] = icon;
                    if (itemKey.Contains('/')) adminMap[itemKey.Split('/').Last()] = icon;
                }
            }

            var dbItems = await VM.Repo.GetRibbonItemsAsync();
            foreach (var item in dbItems.Where(i => !string.IsNullOrEmpty(i.Glyph)))
                adminMap[item.Content] = item.Glyph;

            // Walk all ribbon buttons and apply admin overrides
            foreach (var page in DefaultRibbonCategory.Pages.OfType<DevExpress.Xpf.Ribbon.RibbonPage>())
            {
                var pageCaption = page.Caption?.ToString() ?? "";

                foreach (var group in page.Groups.OfType<DevExpress.Xpf.Ribbon.RibbonPageGroup>())
                {
                    foreach (var item in group.Items.OfType<BarItem>())
                    {
                        var content = item.Content?.ToString() ?? "";

                        // Check admin map (admin_ribbon_defaults + ribbon_items)
                        if (adminMap.TryGetValue(content, out var adminIcon))
                        {
                            var img = ResolveGlyphImage(adminIcon);
                            if (img != null) { item.Glyph = img; item.LargeGlyph = img; }
                            continue;
                        }

                        var match = dbItems.FirstOrDefault(d =>
                            string.Equals(d.Content, content, StringComparison.OrdinalIgnoreCase));
                        if (match == null) continue;

                        if (!string.IsNullOrEmpty(match.Glyph))
                        {
                            var img = ResolveGlyphImage(match.Glyph);
                            if (img != null) { item.Glyph = img; item.LargeGlyph = img; }
                        }
                        if (!string.IsNullOrEmpty(match.LargeGlyph))
                        {
                            var img = ResolveGlyphImage(match.LargeGlyph);
                            if (img != null) item.LargeGlyph = img;
                        }

                        // Apply visibility override
                        if (!match.IsVisible)
                            item.IsVisible = false;
                    }
                }
            }
            // Apply per-user overrides (icon, rename, hide) — pages, groups, AND items
            var userId = AuthContext.Instance.CurrentUser?.Id ?? 0;
            if (userId > 0)
            {
                var userOverrides = await VM.Repo.GetUserRibbonOverridesAsync(userId);
                if (userOverrides.Count > 0)
                {
                    var ovMap = userOverrides.ToDictionary(o => o.ItemKey, o => o, StringComparer.OrdinalIgnoreCase);

                    foreach (var page in DefaultRibbonCategory.Pages.OfType<DevExpress.Xpf.Ribbon.RibbonPage>())
                    {
                        var pageCaption = page.Caption?.ToString() ?? "";

                        // Page-level override
                        if (ovMap.TryGetValue(pageCaption, out var pageOv))
                        {
                            if (!string.IsNullOrEmpty(pageOv.CustomText))
                                page.Caption = pageOv.CustomText;
                            if (pageOv.IsHidden)
                                page.IsVisible = false;
                        }

                        foreach (var group in page.Groups.OfType<DevExpress.Xpf.Ribbon.RibbonPageGroup>())
                        {
                            var groupCaption = group.Caption?.ToString() ?? "";

                            // Group-level override
                            if (ovMap.TryGetValue($"{pageCaption}/{groupCaption}", out var groupOv))
                            {
                                if (!string.IsNullOrEmpty(groupOv.CustomText))
                                    group.Caption = groupOv.CustomText;
                            }

                            foreach (var item in group.Items.OfType<BarItem>())
                            {
                                var content = item.Content?.ToString() ?? "";
                                var key = $"{pageCaption}/{groupCaption}/{content}";
                                // Match by full path first, then by just content name (for tree-saved overrides)
                                if (!ovMap.TryGetValue(key, out var ov))
                                    if (!ovMap.TryGetValue(content, out ov))
                                        continue;

                                if (!string.IsNullOrEmpty(ov.CustomIcon))
                                {
                                    var img = ResolveGlyphImage(ov.CustomIcon);
                                    if (img != null) { item.Glyph = img; item.LargeGlyph = img; }
                                }
                                if (!string.IsNullOrEmpty(ov.CustomText))
                                    item.Content = ov.CustomText;
                                if (ov.IsHidden)
                                    item.IsVisible = false;
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex) { AppLogger.LogException("Ribbon", ex, "ApplyDbRibbonOverridesAsync"); }
    }

    /// <summary>Resolve a glyph name to a BitmapImage. Checks DB icon library first, then DX built-in.</summary>
    /// <summary>Local PNG cache for instant icon loading without DB calls.</summary>
    private static readonly Dictionary<string, System.Windows.Media.ImageSource> _iconCache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Preloaded icon overrides — content name → icon name. Used by AssignGlyph to skip DX defaults.</summary>
    private static Dictionary<string, string> _preloadedOverrides = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Preload 3-layer icon overrides: Admin defaults → User overrides → Module defaults.
    /// Applied BEFORE ribbon build so icons render correctly on first paint — no flash.</summary>
    private async Task PreloadIconOverridesAsync()
    {
        _preloadedOverrides.Clear();
        try
        {
            // Layer 1: Admin defaults (lowest priority — set by admin, applies to all users)
            var adminDefaults = await VM.Repo.GetAdminRibbonDefaultsAsync();
            foreach (var (itemKey, icon, text, hidden) in adminDefaults)
            {
                if (!string.IsNullOrEmpty(icon))
                {
                    var key = itemKey.Contains('/') ? itemKey.Split('/').Last() : itemKey;
                    _preloadedOverrides[key] = icon;
                    _preloadedOverrides[itemKey] = icon;
                }
            }

            // Layer 2: ribbon_items with glyph set (admin per-item override)
            var dbItems = await VM.Repo.GetRibbonItemsAsync();
            foreach (var item in dbItems.Where(i => !string.IsNullOrEmpty(i.Glyph)))
                _preloadedOverrides[item.Content] = item.Glyph;

            // Layer 3: User overrides (highest priority — per-user customization)
            var userId = AuthContext.Instance.CurrentUser?.Id ?? 0;
            if (userId > 0)
            {
                var userOvs = await VM.Repo.GetUserRibbonOverridesAsync(userId);
                foreach (var ov in userOvs.Where(o => !string.IsNullOrEmpty(o.CustomIcon)))
                {
                    var key = ov.ItemKey.Contains('/') ? ov.ItemKey.Split('/').Last() : ov.ItemKey;
                    _preloadedOverrides[key] = ov.CustomIcon!;
                    _preloadedOverrides[ov.ItemKey] = ov.CustomIcon!;
                }
            }
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Icon preload error: {ex.Message}"); }
    }

    /// <summary>Public wrapper so dialogs can resolve ribbon glyph images.</summary>
    public static System.Windows.Media.ImageSource? ResolveGlyphImageStatic(string name) => ResolveGlyphImage(name);

    /// <summary>Reload the live global action override map and refresh the ribbon.</summary>
    public async Task ReloadGlobalActionOverridesAsync()
    {
        // Stub — full implementation will reload overrides from DB and re-inject icons
        await Task.CompletedTask;
        Central.Core.Services.GlobalActionService.Instance.RaiseOverridesChanged();
    }

    private static System.Windows.Media.ImageSource? ResolveGlyphImage(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;

        // 1. In-memory cache (instant)
        if (_iconCache.TryGetValue(name, out var cached)) return cached;

        // 2. Local disk PNG cache (no DB call)
        var diskPath = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Central", "icon_cache", $"{SanitizeFileName(name)}.png");
        if (System.IO.File.Exists(diskPath))
        {
            try
            {
                var bmp = new System.Windows.Media.Imaging.BitmapImage();
                bmp.BeginInit();
                bmp.UriSource = new Uri(diskPath);
                bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                bmp.EndInit();
                bmp.Freeze();
                _iconCache[name] = bmp;
                return bmp;
            }
            catch { }
        }

        // 3. DB icon library — load PNG 32px (pre-rendered, no SVG overhead)
        var iconSvc = IconService.Instance;
        if (iconSvc.IsLoaded)
        {
            var match = iconSvc.AllIcons.FirstOrDefault(i =>
                string.Equals(i.Name, name, StringComparison.OrdinalIgnoreCase));
            if (match != null)
            {
                try
                {
                    using var conn = new Npgsql.NpgsqlConnection(App.Dsn);
                    conn.Open();
                    // Prefer pre-rendered PNG 32px, fallback to icon_data, last resort SVG render
                    using var cmd = new Npgsql.NpgsqlCommand("SELECT png_32, icon_data, svg_source FROM icon_library WHERE id=@id", conn);
                    cmd.Parameters.AddWithValue("id", match.Id);
                    using var rdr = cmd.ExecuteReader();
                    if (rdr.Read())
                    {
                        byte[]? pngData = !rdr.IsDBNull(0) ? (byte[])rdr[0] : !rdr.IsDBNull(1) ? (byte[])rdr[1] : null;

                        if (pngData != null)
                        {
                            // Cache PNG to disk for next time
                            try
                            {
                                System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(diskPath)!);
                                System.IO.File.WriteAllBytes(diskPath, pngData);
                            }
                            catch { }

                            var bmp = new System.Windows.Media.Imaging.BitmapImage();
                            bmp.BeginInit();
                            bmp.StreamSource = new System.IO.MemoryStream(pngData);
                            bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                            bmp.EndInit();
                            bmp.Freeze();
                            _iconCache[name] = bmp;
                            return bmp;
                        }

                        // Last resort: SVG render
                        if (!rdr.IsDBNull(2))
                        {
                            var svgText = rdr.GetString(2);
                            var img = Services.SvgHelper.RenderSvgToImageSource(svgText);
                            if (img != null) { _iconCache[name] = img; return img; }
                        }
                    }
                }
                catch { }
            }
        }

        // Fallback: DX built-in image
        try
        {
            return new System.Windows.Media.Imaging.BitmapImage(
                new Uri($"pack://application:,,,/DevExpress.Images.v25.2;component/Images/{name}.png", UriKind.Absolute));
        }
        catch { return null; }
    }

    private static string SanitizeFileName(string name)
        => string.Join("_", name.Split(System.IO.Path.GetInvalidFileNameChars()));

    /// <summary>Find a DockPanel by name (for module panel toggles).</summary>
    private DevExpress.Xpf.Docking.BaseLayoutItem? FindDockPanel(string panelId)
    {
        // Search by x:Name — try exact, then with Panel suffix
        var item = FindName(panelId) as DevExpress.Xpf.Docking.BaseLayoutItem
                ?? FindName(panelId + "Panel") as DevExpress.Xpf.Docking.BaseLayoutItem;
        return item;
    }

    /// <summary>Sync a VM boolean when a dock panel is opened via DockController.Restore (check buttons).</summary>
    private void SyncPanelOpenState(DevExpress.Xpf.Docking.BaseLayoutItem panel, bool isOpen)
    {
        if (panel == GlobalTenantsPanel) VM.IsGlobalTenantsPanelOpen = isOpen;
        else if (panel == GlobalUsersPanel) VM.IsGlobalUsersPanelOpen = isOpen;
        else if (panel == GlobalSubscriptionsPanel) VM.IsGlobalSubscriptionsPanelOpen = isOpen;
        else if (panel == GlobalLicensesPanel) VM.IsGlobalLicensesPanelOpen = isOpen;
        else if (panel == PlatformDashboardPanel) VM.IsPlatformDashboardPanelOpen = isOpen;
        else if (panel == RibbonConfigPanel) VM.IsRibbonConfigPanelOpen = isOpen;
        else if (panel == ServiceDeskPanel) VM.IsServiceDeskPanelOpen = isOpen;
        else if (panel == IntegrationsPanel) VM.IsIntegrationsPanelOpen = isOpen;
    }

    /// <summary>Hide panels the user can't access based on module permissions.</summary>
    private void WireModulePanelVisibility()
    {
        var auth = AuthContext.Instance;
        foreach (var panel in App.PanelBuilder.Panels)
        {
            if (string.IsNullOrEmpty(panel.Permission)) continue;
            if (auth.HasPermission(panel.Permission)) continue;

            var dockItem = FindDockPanel(panel.Id);
            if (dockItem != null)
                DockManager.DockController.Close(dockItem);
        }
    }

    // ── SignalR real-time data refresh ─────────────────────────────────────

    /// <summary>
    /// Wire ConnectivityManager.DataChanged to refresh the relevant grid
    /// when another user modifies data via the API server.
    /// Table name from pg_notify → reload the matching collection.
    /// </summary>
    /// <summary>
    /// Auto-connect to API server + SignalR on startup if api.url and api.auto_connect are configured.
    /// Falls back silently to DirectDb if API is unreachable.
    /// </summary>
    private async Task TryAutoConnectApiAsync()
    {
        var autoConnect = App.Settings?.Get<bool>("api.auto_connect") == true;
        var apiUrl = App.Settings?.Get<string>("api.url");
        if (!autoConnect || string.IsNullOrEmpty(apiUrl) || App.Connectivity == null) return;

        try
        {
            VM.StatusText = $"Connecting to API server at {apiUrl}...";
            var apiClient = new Central.Api.Client.CentralApiClient(apiUrl);
            var login = await apiClient.LoginAsync(AuthContext.Instance.CurrentUser?.Username ?? Environment.UserName);
            if (login != null)
            {
                App.Connectivity.RegisterApi(new Central.Api.Client.ApiDataService(apiClient));
                App.Connectivity.SwitchMode(Central.Core.Data.DataServiceMode.Api);
                App.Connectivity.ApiUrl = apiUrl;

                await App.Connectivity.ConnectSignalRAsync($"{apiUrl.TrimEnd('/')}/hubs/notify", login.Token);
                VM.StatusText = $"API connected ({login.Role}) + SignalR active";
                NotificationService.Instance.Success("API connected", $"Multi-user mode active via {apiUrl}");
            }
            else
            {
                VM.StatusText = "API login failed — using direct DB";
            }
        }
        catch (Exception ex)
        {
            VM.StatusText = $"API unreachable — using direct DB ({ex.Message})";
            AppLogger.LogException("API", ex, "TryAutoConnectApiAsync");
        }
    }

    private void WireSignalRDataChanged()
    {
        if (App.Connectivity == null) return;

        App.Connectivity.DataChanged += (table, op, id) =>
        {
            // Dispatch to UI thread — SignalR callbacks fire on threadpool
            Dispatcher.InvokeAsync(async () =>
            {
                try
                {
                    switch (table)
                    {
                        case "switch_guide":
                            await VM.ReloadDevicesAsync();
                            NotificationService.Instance.Info($"Devices {op.ToLower()}d by another user");
                            break;
                        case "switches":
                            await VM.ReloadSwitchesAsync();
                            NotificationService.Instance.Info($"Switches {op.ToLower()}d");
                            break;
                        case "p2p_links":
                            await VM.ReloadP2PLinksAsync();
                            NotificationService.Instance.Info($"P2P links {op.ToLower()}d");
                            break;
                        case "b2b_links":
                            await VM.ReloadB2BLinksAsync();
                            NotificationService.Instance.Info($"B2B links {op.ToLower()}d");
                            break;
                        case "fw_links":
                            await VM.ReloadFWLinksAsync();
                            NotificationService.Instance.Info($"FW links {op.ToLower()}d");
                            break;
                        case "vlan_inventory":
                            await VM.ReloadVlansAsync();
                            NotificationService.Instance.Info($"VLANs {op.ToLower()}d");
                            break;
                        case "bgp_config":
                        case "bgp_neighbors":
                        case "bgp_networks":
                            await VM.ReloadBgpAsync();
                            NotificationService.Instance.Info($"BGP {op.ToLower()}d");
                            break;
                        case "app_users":
                            await VM.ReloadUsersAsync();
                            break;
                        case "roles":
                        case "role_permissions":
                            await VM.ReloadRolesAsync();
                            break;
                        case "role_sites":
                            await VM.ReloadRolesAsync();
                            // Refresh current user's allowed sites + reload devices with new filter
                            try
                            {
                                var permRepo = new Central.Data.Repositories.PermissionRepository(App.Dsn);
                                var newSites = await permRepo.GetAllowedSitesAsync(AuthContext.Instance.CurrentUser?.RoleName ?? "");
                                AuthContext.Instance.UpdateAllowedSites(newSites);
                                await VM.ReloadDevicesAsync();
                            }
                            catch { }
                            break;
                        case "lookup_values":
                            await VM.ReloadLookupsAsync();
                            break;
                        case "switch_interfaces":
                        case "running_configs":
                            if (VM.SelectedSwitch != null)
                                await VM.RefreshSelectedSwitchDetailsAsync();
                            break;
                        case "tasks":
                        case "task_comments":
                            if (VM.IsTasksPanelOpen) await VM.LoadTasksAsync();
                            NotificationService.Instance.Info($"Tasks {op.ToLower()}d by another user");
                            break;
                        case "sprints":
                        case "sprint_allocations":
                        case "sprint_burndown":
                            await VM.LoadSprintsAsync();
                            NotificationService.Instance.Info($"Sprint data {op.ToLower()}d by another user");
                            break;
                        case "task_projects":
                            await VM.LoadTaskProjectsAsync();
                            NotificationService.Instance.Info($"Project {op.ToLower()}d by another user");
                            break;
                        case "portfolios":
                        case "programmes":
                        case "releases":
                            NotificationService.Instance.Info($"Portfolio data {op.ToLower()}d by another user");
                            break;
                        case "task_links":
                        case "task_dependencies":
                            NotificationService.Instance.Info($"Task links {op.ToLower()}d by another user");
                            break;
                        case "board_columns":
                        case "board_lanes":
                            NotificationService.Instance.Info($"Kanban board {op.ToLower()}d by another user");
                            break;
                        case "custom_columns":
                        case "task_custom_values":
                            NotificationService.Instance.Info($"Custom columns {op.ToLower()}d by another user");
                            break;
                        case "time_entries":
                            NotificationService.Instance.Info($"Time entry {op.ToLower()}d by another user");
                            break;
                        case "saved_reports":
                        case "dashboards":
                            NotificationService.Instance.Info($"Report/dashboard {op.ToLower()}d by another user");
                            break;
                        case "workflow_approvals":
                            NotificationService.Instance.Warning($"Approval request {op.ToLower()}d");
                            break;
                        case "asn_definitions":
                            await VM.ReloadAsnAsync();
                            break;
                        case "servers":
                            await VM.ReloadServersAsync();
                            break;
                        case "ribbon_pages":
                        case "ribbon_groups":
                        case "ribbon_items":
                            NotificationService.Instance.Info("Ribbon config changed — restart to apply");
                            break;
                        case "saved_filters":
                            NotificationService.Instance.Info("Saved filter updated");
                            break;
                        case "config_ranges":
                            await VM.ReloadConfigRangesAsync();
                            break;
                        case "sd_requests":
                            if (VM.IsServiceDeskPanelOpen) await LoadServiceDeskAsync();
                            NotificationService.Instance.Info("Service Desk data updated");
                            break;
                        case "sd_technicians":
                        case "sd_requesters":
                        case "sd_groups":
                        case "sd_teams":
                            NotificationService.Instance.Info($"SD {table} updated");
                            break;
                        case "identity_providers":
                        case "idp_domain_mappings":
                        case "claim_mappings":
                            if (_idpLoaded) { _idpLoaded = false; }
                            NotificationService.Instance.Info("Identity provider config changed");
                            break;
                        case "auth_events":
                            if (_authEventsLoaded) { _authEventsLoaded = false; }
                            break;
                        case "appointments":
                        case "appointment_resources":
                            if (_schedulerLoaded) { _schedulerLoaded = false; }
                            NotificationService.Instance.Info("Appointment updated by another user");
                            break;
                        case "countries":
                        case "regions":
                            if (_locationsLoaded) { _locationsLoaded = false; }
                            break;
                        case "reference_config":
                            if (_referenceConfigLoaded) { _referenceConfigLoaded = false; }
                            break;
                        case "backup_history":
                            if (_backupLoaded) { _backupLoaded = false; }
                            break;
                        case "icon_defaults":
                        case "user_icon_overrides":
                            if (_iconDefaultsLoaded) { _iconDefaultsLoaded = false; _iconOverridesLoaded = false; }
                            NotificationService.Instance.Info("Icon defaults changed — panel will refresh on next open");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    AppLogger.LogException("SignalR", ex, $"DataChanged:{table}:{op}");
                }
            });
        };
    }

    private void DeployLinkButton_ItemClick(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)
    {
        // Deploy from whichever link panel is active
        var (grid, _) = GetActiveGrid();
        if (grid == P2PGridPanel.Grid) PreviewDeployConfig(grid, "p2p");
        else if (grid == B2BGridPanel.Grid) PreviewDeployConfig(grid, "b2b");
        else if (grid == FWGridPanel.Grid) PreviewDeployConfig(grid, "fw");
        else VM.StatusText = "Switch to a link panel (P2P, B2B, or FW) first";
    }

    // ── Ctrl+S Commit Current Row ─────────────────────────────────────

    private void CommitCurrentRow()
    {
        var (_, view) = GetActiveGrid();
        if (view == null) return;
        try
        {
            // Close active editor and commit the row
            view.CommitEditing();
            VM.StatusText = "Row saved";
        }
        catch (Exception ex)
        {
            VM.StatusText = $"Save failed: {ex.Message}";
        }
    }

    // ── Ctrl+G Go To Device/Switch ──────────────────────────────────────

    private void ShowGoToDialog()
    {
        // Ctrl+G opens search bar focused — type name and Enter navigates
        GlobalSearchBar.Visibility = Visibility.Visible;
        GlobalSearchBox.Focus();
        GlobalSearchBox.SelectAll();
        VM.StatusText = "Type device/switch name, press Enter to navigate";
    }

    // ── Ctrl+Tab Panel Cycling ─────────────────────────────────────────

    private void CycleNextPanel()
    {
        var panels = new[] { DevicesPanel, SwitchesPanel, P2PPanel, B2BPanel, FWPanel, VlansPanel, BgpPanel }
            .Where(p => p.Visibility == Visibility.Visible || DockManager.ClosedPanels?.Contains(p) == false)
            .ToList();

        if (panels.Count == 0) return;

        var active = DockManager.ActiveDockItem;
        var idx = panels.IndexOf(active as DevExpress.Xpf.Docking.DocumentPanel);
        var next = panels[(idx + 1) % panels.Count];
        DockManager.Activate(next);
    }

    // ── Global Search (Ctrl+F) ────────────────────────────────────────

    private void ToggleGlobalSearch()
    {
        if (GlobalSearchBar.Visibility == Visibility.Visible)
        {
            GlobalSearchBar.Visibility = Visibility.Collapsed;
            // Clear search
            GlobalSearchBox.Text = "";
            var (grid, _) = GetActiveGrid();
            if (grid != null) grid.FilterString = "";
        }
        else
        {
            GlobalSearchBar.Visibility = Visibility.Visible;
            GlobalSearchBox.Focus();
            GlobalSearchBox.SelectAll();
        }
    }

    private void GlobalSearchBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        var text = GlobalSearchBox.Text?.Trim() ?? "";
        var (grid, _) = GetActiveGrid();
        if (grid == null) return;

        if (string.IsNullOrEmpty(text))
        {
            grid.FilterString = "";
            GlobalSearchCount.Text = "";
            return;
        }

        // Build filter across all visible string columns
        var conditions = new List<string>();
        foreach (var col in grid.Columns)
        {
            if (col.Visible && col.FieldName != null)
                conditions.Add($"Contains([{col.FieldName}], '{text.Replace("'", "''")}')");
        }

        if (conditions.Count > 0)
        {
            grid.FilterString = string.Join(" OR ", conditions);
            GlobalSearchCount.Text = $"{grid.VisibleRowCount} matches";
        }
    }

    private void GlobalSearchBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
            ToggleGlobalSearch();
        else if (e.Key == Key.Enter)
        {
            // Navigate to first match
            var term = GlobalSearchBox.Text?.Trim();
            if (string.IsNullOrEmpty(term)) return;

            var device = VM.Devices.FirstOrDefault(d =>
                d.SwitchName?.Contains(term, StringComparison.OrdinalIgnoreCase) == true);
            if (device != null)
            {
                Central.Core.Shell.PanelMessageBus.Publish(
                    new Central.Core.Shell.NavigateToPanelMessage("devices", device.SwitchName));
                VM.StatusText = $"Navigated to: {device.SwitchName}";
                ToggleGlobalSearch();
                return;
            }

            var sw = VM.Switches.FirstOrDefault(s =>
                s.Hostname?.Contains(term, StringComparison.OrdinalIgnoreCase) == true);
            if (sw != null)
            {
                Central.Core.Shell.PanelMessageBus.Publish(
                    new Central.Core.Shell.NavigateToPanelMessage("switches", sw.Hostname));
                VM.StatusText = $"Navigated to: {sw.Hostname}";
                ToggleGlobalSearch();
                return;
            }

            VM.StatusText = $"No match for '{term}'";
        }
    }

    // ── Keyboard Help (F1) ──────────────────────────────────────────────

    private void ShowKeyboardHelp()
    {
        var help = """
            Keyboard Shortcuts
            ══════════════════
            Ctrl+R / F5     Refresh all data
            Ctrl+N          New item in active grid
            Ctrl+F          Search / filter active grid
            Ctrl+P          Print preview
            Ctrl+E          Export devices to clipboard
            Ctrl+S          Save / commit current row
            Ctrl+D          Toggle details panel
            Ctrl+G          Go to device/switch by name
            Ctrl+Tab        Cycle to next panel
            Delete           Delete selected row
            Escape           Close search / cancel
            F1               This help

            Grid Navigation
            ═══════════════
            Right-click      Context menu (deploy, duplicate, navigate)
            Double-click     Open details panel for selected row
            Ctrl+Click       Multi-select rows for bulk edit
            Shift+Click      Range select rows
            """;

        System.Windows.MessageBox.Show(help, "Central — Keyboard Shortcuts",
            System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
    }

    // ── Deploy Config Preview ──────────────────────────────────────────

    private void PreviewDeployConfig(DevExpress.Xpf.Grid.GridControl grid, string linkType)
    {
        var link = grid.CurrentItem as Central.Core.Models.INetworkLink;
        if (link == null) { VM.StatusText = "Select a link first"; return; }

        // Build config for both sides
        var configA = link.ConfigA;
        var configB = link.ConfigB;

        // Validate before showing preview
        var warnings = link.Validate();
        if (warnings.Count > 0)
        {
            var warningText = string.Join("\n", warnings.Select(w => $"• {w}"));
            if (string.IsNullOrWhiteSpace(configA) && string.IsNullOrWhiteSpace(configB))
            {
                VM.StatusText = $"Cannot deploy — {warnings.Count} issues:\n{warnings[0]}";
                return;
            }
            // Has warnings but can still generate partial config — show warning in deploy panel
            configA = $"# WARNINGS:\n{string.Join("\n", warnings.Select(w => $"# - {w}"))}\n\n{configA}";
            configB = $"# WARNINGS:\n{string.Join("\n", warnings.Select(w => $"# - {w}"))}\n\n{configB}";
        }

        // Open deploy panel with preview
        VM.IsDeployPanelOpen = true;
        DeployGridPanel.HeaderText = $"Deploy {linkType.ToUpper()} Link: {link.DeviceA} ↔ {link.DeviceB}";
        DeployGridPanel.TabAHeader = link.DeviceA ?? "Switch A";
        DeployGridPanel.TabBHeader = link.DeviceB ?? "Switch B";
        DeployGridPanel.ConfigA = configA;
        DeployGridPanel.ConfigB = configB;
        DeployGridPanel.LogText = "";
        DeployGridPanel.StatusText = "Review the config below. Click 'Confirm & Deploy' to send to switches.";
        DeployGridPanel.ConfirmEnabled = true;

        // Wire confirm button
        DeployGridPanel.ConfirmDeployClicked -= OnDeployConfirmed;
        DeployGridPanel.ConfirmDeployClicked += OnDeployConfirmed;
        DeployGridPanel.CancelClicked -= OnDeployCancelled;
        DeployGridPanel.CancelClicked += OnDeployCancelled;

        // Store link reference for deploy
        _pendingDeployLink = link;

        DockManager.Activate(DeployPanel);
    }

    private Central.Core.Models.INetworkLink? _pendingDeployLink;

    private async Task OnDeployConfirmed()
    {
        if (_pendingDeployLink == null) return;
        var link = _pendingDeployLink;

        DeployGridPanel.ConfirmEnabled = false;
        DeployGridPanel.SelectLogTab();
        DeployGridPanel.StatusText = "Deploying...";

        var notif = Central.Core.Services.NotificationService.Instance;

        // Deploy to Switch A
        if (!string.IsNullOrWhiteSpace(DeployGridPanel.ConfigA))
        {
            DeployGridPanel.AppendLog($"[{DateTime.Now:HH:mm:ss}] Connecting to {link.DeviceA}...");
            try
            {
                var sw = VM.Switches.FirstOrDefault(s =>
                    string.Equals(s.Hostname, link.DeviceA, StringComparison.OrdinalIgnoreCase));
                if (sw != null)
                {
                    var result = await DeployConfigToSwitch(sw, DeployGridPanel.ConfigA);
                    DeployGridPanel.AppendLog(result.ok
                        ? $"[{DateTime.Now:HH:mm:ss}] {link.DeviceA}: Config applied successfully"
                        : $"[{DateTime.Now:HH:mm:ss}] {link.DeviceA}: FAILED — {result.error}");
                }
                else
                {
                    DeployGridPanel.AppendLog($"[{DateTime.Now:HH:mm:ss}] {link.DeviceA}: Switch not found in configured switches");
                }
            }
            catch (Exception ex)
            {
                DeployGridPanel.AppendLog($"[{DateTime.Now:HH:mm:ss}] {link.DeviceA}: ERROR — {ex.Message}");
            }
        }

        // Deploy to Switch B
        if (!string.IsNullOrWhiteSpace(DeployGridPanel.ConfigB))
        {
            DeployGridPanel.AppendLog($"[{DateTime.Now:HH:mm:ss}] Connecting to {link.DeviceB}...");
            try
            {
                var sw = VM.Switches.FirstOrDefault(s =>
                    string.Equals(s.Hostname, link.DeviceB, StringComparison.OrdinalIgnoreCase));
                if (sw != null)
                {
                    var result = await DeployConfigToSwitch(sw, DeployGridPanel.ConfigB);
                    DeployGridPanel.AppendLog(result.ok
                        ? $"[{DateTime.Now:HH:mm:ss}] {link.DeviceB}: Config applied successfully"
                        : $"[{DateTime.Now:HH:mm:ss}] {link.DeviceB}: FAILED — {result.error}");
                }
                else
                {
                    DeployGridPanel.AppendLog($"[{DateTime.Now:HH:mm:ss}] {link.DeviceB}: Switch not found in configured switches");
                }
            }
            catch (Exception ex)
            {
                DeployGridPanel.AppendLog($"[{DateTime.Now:HH:mm:ss}] {link.DeviceB}: ERROR — {ex.Message}");
            }
        }

        DeployGridPanel.AppendLog($"[{DateTime.Now:HH:mm:ss}] Deploy complete.");
        DeployGridPanel.StatusText = "Deploy complete — check log for results";
        notif.Success("Deploy Complete", $"{link.DeviceA} ↔ {link.DeviceB}");
        _pendingDeployLink = null;
    }

    private async Task<(bool ok, string error)> DeployConfigToSwitch(SwitchRecord sw, string config)
    {
        var result = await SshProxy.DeployConfigAsync(sw, config);
        return result;
    }

    private void OnDeployCancelled()
    {
        _pendingDeployLink = null;
        DeployGridPanel.StatusText = "Cancelled";
        DeployGridPanel.ConfirmEnabled = false;
    }

    // ── Active Panel Row Count ──────────────────────────────────────────

    private void UpdateActiveRowCount()
    {
        var (grid, _) = GetActiveGrid();
        if (grid != null)
            VM.DeviceCountText = $"{grid.VisibleRowCount} rows · {VM.ActivePanelName}";
    }

    // ── Bulk Edit ────────────────────────────────────────────────────────

    private void BulkEditButton_ItemClick(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)
    {
        var (grid, _) = GetActiveGrid();
        if (grid == null) return;

        var selectedItems = new List<object>();
        foreach (var handle in grid.GetSelectedRowHandles())
        {
            var row = grid.GetRow(handle);
            if (row != null) selectedItems.Add(row);
        }

        if (selectedItems.Count < 2)
        {
            VM.StatusText = "Select 2 or more rows first (Ctrl+Click or Shift+Click)";
            return;
        }

        var dialog = new BulkEditWindow(selectedItems) { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            VM.StatusText = $"Bulk edit: {dialog.ModifiedField} updated on {dialog.ModifiedCount} rows — save each row to persist";
            Central.Core.Services.NotificationService.Instance.Success(
                "Bulk Edit", $"{dialog.ModifiedField} updated on {dialog.ModifiedCount} rows");
        }
    }

    // ── Notification Toasts ────────────────────────────────────────────────

    private System.Windows.Threading.DispatcherTimer? _toastTimer;

    private void WireNotifications()
    {
        var svc = Central.Core.Services.NotificationService.Instance;
        svc.NotificationReceived += notification =>
        {
            Dispatcher.InvokeAsync(() => ShowToast(notification));
        };

        // Wire SignalR DataChanged to show toasts + flash status indicator
        if (App.Connectivity != null)
        {
            App.Connectivity.DataChanged += (table, op, id) =>
            {
                Dispatcher.InvokeAsync(() =>
                {
                    svc.Info($"Data changed: {table}", $"{op} (id: {id?.Substring(0, Math.Min(8, id?.Length ?? 0))}...)", "SignalR");
                    FlashConnectionIndicator();
                });
            };
        }
    }

    private System.Windows.Threading.DispatcherTimer? _flashTimer;

    private void FlashConnectionIndicator()
    {
        // Briefly change DB status to cyan to show real-time activity
        var original = VM.DbStatus;
        VM.DbStatus = "#00BCD4"; // Cyan flash
        _flashTimer?.Stop();
        _flashTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
        _flashTimer.Tick += (_, _) =>
        {
            VM.DbStatus = original;
            _flashTimer.Stop();
        };
        _flashTimer.Start();
    }

    private void ShowToast(Central.Core.Services.Notification n)
    {
        ToastIcon.Text = n.Icon;
        ToastIcon.Foreground = new System.Windows.Media.SolidColorBrush(
            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(n.Color));
        ToastTitle.Text = n.Title;
        ToastMessage.Text = n.Message;
        ToastBorder.Visibility = Visibility.Visible;

        // Auto-hide after 4 seconds
        _toastTimer?.Stop();
        _toastTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
        _toastTimer.Tick += (_, _) =>
        {
            ToastBorder.Visibility = Visibility.Collapsed;
            _toastTimer.Stop();
        };
        _toastTimer.Start();
    }

    // ══════════════════════════════════════════════════════════════════════
    // SERVICE DESK — Unified wiring + data flow
    // All delegates wired ONCE at construction. All loads use SdFilterState.
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>Initialize the enterprise mediator + link engine with pipeline behaviors and default rules.</summary>
    private void InitializeLinkEngine()
    {
        // Add pipeline behaviors for diagnostics
        Central.Core.Shell.Mediator.Instance.AddBehavior(new Central.Core.Shell.MediatorLoggingBehavior());
        Central.Core.Shell.Mediator.Instance.AddBehavior(new Central.Core.Shell.MediatorPerformanceBehavior());

        // Initialize LinkEngine with default cross-panel rules
        // These are the baseline rules — users can add more via "Configure Links..." in the grid right-click menu
        var defaultRules = new List<Central.Core.Models.LinkRule>
        {
            // Service Desk: click technician/requester/group → filter request grid
            new() { SourcePanel = "SdTechnicians", SourceField = "TechnicianName", TargetPanel = "ServiceDesk", TargetField = "TechnicianName", FilterOnSelect = true },
            new() { SourcePanel = "SdRequesters", SourceField = "RequesterName", TargetPanel = "ServiceDesk", TargetField = "RequesterName", FilterOnSelect = true },
            new() { SourcePanel = "SdGroups", SourceField = "GroupName", TargetPanel = "ServiceDesk", TargetField = "GroupName", FilterOnSelect = true },

            // Devices: click device → filter switches by building
            new() { SourcePanel = "Devices", SourceField = "Building", TargetPanel = "Switches", TargetField = "Building", FilterOnSelect = true },

            // Users: click user → filter auth events by username
            new() { SourcePanel = "Users", SourceField = "Username", TargetPanel = "AuthEvents", TargetField = "Username", FilterOnSelect = true },
        };

        Central.Core.Shell.LinkEngine.Instance.Initialize(defaultRules);
    }

    // ── Dashboard ──────────────────────────────────────────────────────────

    private async Task LoadDashboardAsync()
    {
        DashboardGridPanel.LoadDashboardData = async () => await VM.Repo.GetDashboardDataAsync();
        await DashboardGridPanel.LoadAsync();
    }

    private void ImportData_ItemClick(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)
        => ShowImportWizard();

    private void ShowImportWizard()
    {
        var dialog = new ImportWizardDialog(App.Dsn) { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            VM.StatusText = $"Import complete: {dialog.ImportedCount} records imported";
            Central.Core.Services.NotificationService.Instance?.Success(
                $"Import complete: {dialog.ImportedCount} records");
        }
    }

    /// <summary>Attach GridCustomizerDialog to ALL grids globally. Called from MainWindow_Loaded.</summary>
    private void WireGridCustomizers()
    {
        var repo = VM.Repo;
        void Attach(DevExpress.Xpf.Grid.GridControl? grid, DevExpress.Xpf.Grid.TableView? view, string name)
        {
            if (grid != null && view != null)
                try { Services.GridCustomizerHelper.Attach(grid, view, name, repo); } catch { }
        }

        // ── Devices module ──
        Attach(DeviceGridPanel.Grid, DeviceGridPanel.View, "Devices");
        Attach(MasterGridPanel.Grid, MasterGridPanel.View, "Master");
        Attach(AsnGridPanel.Grid, AsnGridPanel.View, "ASN");
        Attach(IpRangesGridPanel.Grid, IpRangesGridPanel.View, "IpRanges");
        Attach(MlagGridPanel.Grid, MlagGridPanel.View, "MLAG");
        Attach(MstpGridPanel.Grid, MstpGridPanel.View, "MSTP");
        Attach(ServerAsGridPanel.Grid, ServerAsGridPanel.View, "ServerAS");

        // ── Switches module ──
        if (SwitchGridPanel.Grid?.View is DevExpress.Xpf.Grid.TableView swView)
            Attach(SwitchGridPanel.Grid, swView, "Switches");

        // ── Links module ──
        Attach(P2PGridPanel.Grid, P2PGridPanel.View, "P2P");
        Attach(B2BGridPanel.Grid, B2BGridPanel.View, "B2B");
        Attach(FWGridPanel.Grid, FWGridPanel.View, "FW");

        // ── VLANs module ──
        Attach(VlanGridPanel.Grid, VlanGridPanel.View, "VLANs");

        // ── Routing module ──
        Attach(BgpGridPanel.Grid, BgpGridPanel.View, "BGP");

        // ── Service Desk module ──
        Attach(RequestGridPanel?.Grid, RequestGridPanel?.View, "ServiceDesk");
        Attach(SdTechGridPanel?.Grid, SdTechGridPanel?.View, "SdTechnicians");
        Attach(SdReqGridPanel?.Grid, SdReqGridPanel?.View, "SdRequesters");
        Attach(SdGroupsGridPanel?.Grid, SdGroupsGridPanel?.View, "SdGroups");

        // ── Admin module ──
        Attach(UsersGridPanel?.Grid, UsersGridPanel?.View, "Users");
        Attach(SchedulerGridPanel?.Grid, SchedulerGridPanel?.View, "Scheduler");
    }

    /// <summary>Wire ALL SD panel delegates once. Called from MainWindow_Loaded.</summary>
    private void WireServiceDeskDelegates()
    {
        // ── SD Settings panel — global filter source ──
        SdSettingsPanel.FiltersChanged += () => Dispatcher.InvokeAsync(RefreshAllSdAsync);
        SdSettingsPanel.GridOptionsChanged += () => Dispatcher.InvokeAsync(ApplyAllSdGridOptions);

        // ── Overview drill-down ──
        OverviewChartPanel.ChartDrillDownRequested += async (seriesName, day) =>
        {
            try
            {
                VM.StatusText = $"Loading {seriesName} — {day:ddd MMM d}...";
                var requests = await VM.Repo.GetSdOverviewDrillDownAsync(seriesName, day);
                Dispatcher.Invoke(() => ShowDrillDown($"{seriesName} — {day:ddd MMM d} ({requests.Count} tickets)", requests));
            }
            catch (Exception ex) { LogDrillDownError(ex); }
        };
        OverviewChartPanel.KpiDrillDownRequested += async kpiName =>
        {
            try
            {
                var f = SdSettingsPanel.GetCurrentFilters();
                VM.StatusText = $"Loading {kpiName}...";
                var requests = await VM.Repo.GetSdKpiDrillDownAsync(kpiName, f.RangeStart, f.RangeEnd);
                Dispatcher.Invoke(() => ShowDrillDown($"{kpiName} — {f.RangeStart:MMM d} to {f.RangeEnd.AddDays(-1):MMM d} ({requests.Count} tickets)", requests));
            }
            catch (Exception ex) { LogDrillDownError(ex); }
        };

        // ── Tech Closures drill-down ──
        TechClosuresPanel.DrillDownRequested += async (techName, day) =>
        {
            try
            {
                VM.StatusText = $"Loading {techName} — {day:ddd MMM d}...";
                var requests = await VM.Repo.GetSdClosureDrillDownAsync(techName, day);
                Dispatcher.Invoke(() => ShowDrillDown($"{techName} — resolved {day:ddd MMM d} ({requests.Count} tickets)", requests));
            }
            catch (Exception ex) { LogDrillDownError(ex); }
        };

        // ── Aging drill-down ──
        AgingChartPanel.DrillDownRequested += async (techName, bucketName, minDays, maxDays) =>
        {
            try
            {
                VM.StatusText = $"Loading {techName} — {bucketName}...";
                var requests = await VM.Repo.GetSdAgingDrillDownAsync(techName, minDays, maxDays);
                Dispatcher.Invoke(() => ShowDrillDown($"{techName} — {bucketName} ({requests.Count} tickets)", requests));
            }
            catch (Exception ex) { LogDrillDownError(ex); }
        };

        // ── CRUD delegates (set once, never re-assigned) ──
        TeamsPanel.SaveTeam = team => VM.Repo.UpsertSdTeamAsync(team);
        TeamsPanel.DeleteTeam = id => VM.Repo.DeleteSdTeamAsync(id);

        SdGroupsGridPanel.SaveGroup = async g => { await VM.Repo.UpsertSdGroupAsync(g); await RefreshSdSettingsAsync(); };

        SdTechGridPanel.SaveActive = async (id, isActive) =>
        {
            await VM.Repo.UpdateSdTechnicianActiveAsync(id, isActive);
            await RefreshSdSettingsAsync();
            await RefreshAllSdAsync();
        };

        GroupCatsPanel.StatusMessage += msg => Dispatcher.Invoke(() => VM.StatusText = msg);
        GroupCatsPanel.SaveCategory = async cat =>
        {
            try
            {
                var id = await VM.Repo.UpsertSdGroupCategoryAsync(cat);
                VM.StatusText = $"Saved category '{cat.Name}' ({cat.Members.Count} members)";
                return id;
            }
            catch (Exception ex) { AppLogger.LogException("ServiceDesk", ex, "SaveGroupCategory"); return cat.Id; }
        };
        GroupCatsPanel.DeleteCategory = async id => await VM.Repo.DeleteSdGroupCategoryAsync(id);
        GroupCatsPanel.OnStructureChanged = async () => { await RefreshSdSettingsAsync(); VM.StatusText = "SD Settings refreshed"; };

        // ── Cross-panel linking: selecting a row in one grid filters the request grid ──
        Central.Core.Shell.PanelMessageBus.Subscribe<Central.Core.Shell.LinkSelectionMessage>(msg =>
        {
            Dispatcher.InvokeAsync(() =>
            {
                // Only react when the request grid is open
                if (!VM.IsServiceDeskPanelOpen) return;

                var field = msg.Field;
                var value = msg.Value?.ToString();
                if (string.IsNullOrEmpty(field) || string.IsNullOrEmpty(value)) return;

                // Build DX filter string for the request grid
                var filterExpr = $"[{field}] = '{value.Replace("'", "''")}'";
                RequestGridPanel.Grid.FilterString = filterExpr;
                VM.StatusText = $"Filtered by {field} = {value}";

                // Activate the request grid so user sees the filtered result
                DockManager.DockController.Restore(ServiceDeskPanel);
                DockManager.Activate(ServiceDeskPanel);
            });
        });

        // Also: selecting a request publishes its tech/group for other panels to highlight
        RequestGridPanel.Grid.CurrentItemChanged += (_, _) =>
        {
            if (RequestGridPanel.Grid.CurrentItem is Central.Core.Models.SdRequest req)
            {
                Central.Core.Shell.PanelMessageBus.Publish(
                    new Central.Core.Shell.SelectionChangedMessage("servicedesk", req));
            }
        };

        // ── Grid layout actions from SD Settings panel ──
        SdSettingsPanel.ColumnChooserRequested += () =>
        {
            var (_, view) = GetActiveGrid();
            if (view != null) { if (view.IsColumnChooserVisible) view.HideColumnChooser(); else view.ShowColumnChooser(); }
        };
        SdSettingsPanel.BestFitRequested += () =>
        {
            var (_, v) = GetActiveGrid();
            v?.BestFitColumns();
        };
        SdSettingsPanel.SaveLayoutRequested += () =>
        {
            if (_layout == null) return;
            foreach (var (grid, key) in GetGridLayoutMap())
                _ = _layout.SaveGridLayoutAsync(grid, key);
            VM.StatusText = "Grid layouts saved";
        };
        SdSettingsPanel.RestoreLayoutRequested += () =>
        {
            var (grid, view) = GetActiveGrid();
            if (view != null)
            {
                grid?.ClearSorting();
                grid?.FilterString = "";
                grid?.ClearGrouping();
                view.BestFitColumns();
            }
            VM.StatusText = "Layout restored to default";
        };
        SdSettingsPanel.ClearSortRequested += () => { var (g, _) = GetActiveGrid(); g?.ClearSorting(); };
        SdSettingsPanel.ClearFilterRequested += () => { var (g, _) = GetActiveGrid(); if (g != null) g.FilterString = ""; };
        SdSettingsPanel.ClearGroupingRequested += () => { var (g, _) = GetActiveGrid(); g?.ClearGrouping(); };

        // ── Request grid write-back ──
        RequestGridPanel.WriteBackDirty = async dirtyRows =>
        {
            var svc = await CreateMeSyncServiceAsync();
            if (svc == null) throw new Exception("ManageEngine not configured");
            foreach (var req in dirtyRows)
            {
                var parts = new List<string>();
                if (req.Status != req.OriginalStatus) parts.Add($"\"status\":{{\"name\":\"{req.Status}\"}}");
                if (req.Priority != req.OriginalPriority) parts.Add($"\"priority\":{{\"name\":\"{req.Priority}\"}}");
                if (req.GroupName != req.OriginalGroupName) parts.Add($"\"group\":{{\"name\":\"{req.GroupName}\"}}");
                if (req.TechnicianName != req.OriginalTechnicianName) parts.Add($"\"technician\":{{\"name\":\"{req.TechnicianName}\"}}");
                if (req.Category != req.OriginalCategory) parts.Add($"\"category\":{{\"name\":\"{req.Category}\"}}");
                if (parts.Count > 0)
                {
                    var (ok, msg) = await svc.UpdateRequestAsync(req.Id, "{" + string.Join(",", parts) + "}");
                    if (!ok) throw new Exception($"{req.DisplayId}: {msg}");
                    await VM.Repo.UpdateSdRequestLocalAsync(req.Id, req.Status, req.Priority, req.GroupName, req.TechnicianName, req.Category);
                }
            }
            NotificationService.Instance.Success($"Wrote {dirtyRows.Count} changes to ManageEngine");
        };
    }

    private void ShowDrillDown(string title, System.Collections.IEnumerable data)
    {
        var win = new Central.Desktop.Services.ChartDrillDownWindow(title, data);
        win.Owner = this;
        win.Show();
    }

    private void LogDrillDownError(Exception ex)
    {
        VM.StatusText = $"Drill-down error: {ex.Message}";
        AppLogger.LogException("ServiceDesk", ex, "DrillDown");
    }

    // ── Unified refresh — reads SdFilterState once, drives everything ──

    private async Task RefreshAllSdAsync()
    {
        try
        {
            var f = SdSettingsPanel.GetCurrentFilters();
            if (VM.IsServiceDeskPanelOpen) await LoadServiceDeskAsync(f);
            if (VM.IsSdOverviewPanelOpen) await LoadSdOverviewAsync(f);
            if (VM.IsSdClosuresPanelOpen) await LoadSdClosuresAsync(f);
            if (VM.IsSdAgingPanelOpen) await LoadSdAgingAsync(f);
        }
        catch (Exception ex) { AppLogger.LogException("ServiceDesk", ex, "RefreshAllSdAsync"); }
    }

    private async Task RefreshSdSettingsAsync()
    {
        try
        {
            var (techs, teams) = await GetSdTechsAndTeamsAsync();
            SdSettingsPanel.LoadTechnicians(techs, teams);

            var groupNames = await VM.Repo.GetSdGroupNamesAsync();
            List<Central.Core.Models.SdGroupCategory> groupCats;
            try { groupCats = await VM.Repo.GetSdGroupCategoriesAsync(); } catch { groupCats = new(); }

            var disabledGroups = new HashSet<string>();
            foreach (var node in GroupCatsPanel.Nodes)
                if (node.NodeType == "Group" && !node.IsActive) disabledGroups.Add(node.DisplayName);
            SdSettingsPanel.DisabledGroups = disabledGroups;
            SdSettingsPanel.LoadGroups(groupNames, groupCats);

            // Also refresh request grid combos
            var activeGroups = (await VM.Repo.GetSdGroupsAsync()).Where(g => g.IsActive).Select(g => g.Name).ToList();
            var activeTechs = (await VM.Repo.GetSdTechniciansAsync()).Where(t => t.IsActive).Select(t => t.Name).OrderBy(n => n).ToList();
            var categories = await VM.Repo.GetSdCategoryNamesAsync();
            RequestGridPanel.BindLookups(activeGroups, activeTechs, categories);
        }
        catch (Exception ex) { AppLogger.LogException("ServiceDesk", ex, "RefreshSdSettingsAsync"); }
    }

    private async Task<(List<string> techs, List<Central.Core.Models.SdTeam> teams)> GetSdTechsAndTeamsAsync()
    {
        var techNames = await VM.Repo.GetSdTechnicianNamesAsync();
        List<Central.Core.Models.SdTeam> teams;
        try { teams = await VM.Repo.GetSdTeamsAsync(); } catch { teams = new(); }
        return (techNames, teams);
    }

    // ── Individual panel loaders (all take SdFilterState) ──

    private async Task LoadServiceDeskAsync() => await LoadServiceDeskAsync(SdSettingsPanel.GetCurrentFilters());

    private async Task LoadServiceDeskAsync(Central.Core.Models.SdFilterState f)
    {
        try
        {
            var requests = await VM.Repo.GetSdRequestsFilteredAsync(f);
            RequestGridPanel.Grid.ItemsSource = requests;
            VM.StatusText = $"Service Desk: {requests.Count} requests";
            if (requests.Count == 0)
                NotificationService.Instance.Info("Service Desk is empty — use Sync to pull from ManageEngine");
        }
        catch (Exception ex) { AppLogger.LogException("ServiceDesk", ex, "LoadServiceDeskAsync"); }
    }

    private async Task LoadSdOverviewAsync() => await LoadSdOverviewAsync(SdSettingsPanel.GetCurrentFilters());

    private async Task LoadSdOverviewAsync(Central.Core.Models.SdFilterState f)
    {
        try
        {
            VM.StatusText = "Loading SD Overview...";
            var kpiTask = VM.Repo.GetSdKpiSummaryAsync(f.RangeStart, f.RangeEnd, f.SelectedGroups);
            var overviewTask = VM.Repo.GetSdOverviewTotalsAsync(f.RangeStart, f.RangeEnd, f.Bucket, f.SelectedGroups);
            await Task.WhenAll(kpiTask, overviewTask);

            foreach (var item in overviewTask.Result) item.Label = f.FormatLabel(item.Day);

            OverviewChartPanel.LoadKpi(kpiTask.Result);
            OverviewChartPanel.LoadOverview(overviewTask.Result);

            // Apply overlay toggles
            if (OverviewChartPanel.FindName("OpenSeries") is DevExpress.Xpf.Charts.LineSeries2D openS) openS.Visible = f.ShowOpenLine;
            if (OverviewChartPanel.FindName("ResolutionSeries") is DevExpress.Xpf.Charts.LineSeries2D resS) resS.Visible = f.ShowResolutionLine;
            if (OverviewChartPanel.FindName("TotalCreatedLine") is DevExpress.Xpf.Charts.LineSeries2D tcLine) tcLine.Visible = f.ShowTotalCreatedLine;
            if (OverviewChartPanel.FindName("TotalClosedLine") is DevExpress.Xpf.Charts.LineSeries2D tlLine) tlLine.Visible = f.ShowTotalClosedLine;
            // KPI cards visibility
            if (OverviewChartPanel.FindName("KpiCards") is System.Windows.Controls.ItemsControl kpiCtl)
                kpiCtl.Visibility = f.ShowKpiCards ? Visibility.Visible : Visibility.Collapsed;
            // Bar labels
            if (OverviewChartPanel.FindName("CreatedSeries") is DevExpress.Xpf.Charts.BarSideBySideSeries2D crBar)
                crBar.LabelsVisibility = f.ShowBarLabels;
            if (OverviewChartPanel.FindName("CompletedSeries") is DevExpress.Xpf.Charts.BarSideBySideSeries2D clBar)
                clBar.LabelsVisibility = f.ShowBarLabels;
            // Apply bar style + chart theme
            ApplyChartStyle(OverviewChartPanel.FindName("OverviewChart") as DevExpress.Xpf.Charts.ChartControl, f);
            VM.StatusText = "SD Overview loaded";
        }
        catch (Exception ex) { AppLogger.LogException("ServiceDesk", ex, "LoadSdOverviewAsync"); }
    }

    private async Task LoadSdClosuresAsync() => await LoadSdClosuresAsync(SdSettingsPanel.GetCurrentFilters());

    private async Task LoadSdClosuresAsync(Central.Core.Models.SdFilterState f)
    {
        try
        {
            VM.StatusText = "Loading SD Tech Closures...";
            var expectedStr = await VM.Repo.GetUserSettingAsync(0, "sd.expected_daily_closures");
            if (int.TryParse(expectedStr, out var expected) && expected > 0)
                TechClosuresPanel.ExpectedDaily = expected;

            var data = await VM.Repo.GetSdTechDailyClosuresAsync(f.RangeStart, f.RangeEnd, f.SelectedTechs);
            TechClosuresPanel.LoadData(data);
            ApplyChartStyle(TechClosuresPanel.FindName("TechChart") as DevExpress.Xpf.Charts.ChartControl, f);
            VM.StatusText = "SD Tech Closures loaded";
        }
        catch (Exception ex) { AppLogger.LogException("ServiceDesk", ex, "LoadSdClosuresAsync"); }
    }

    private async Task LoadSdAgingAsync() => await LoadSdAgingAsync(SdSettingsPanel.GetCurrentFilters());

    private async Task LoadSdAgingAsync(Central.Core.Models.SdFilterState f)
    {
        try
        {
            VM.StatusText = "Loading SD Aging...";
            var data = await VM.Repo.GetSdAgingBucketsAsync(f.SelectedTechs);
            AgingChartPanel.LoadData(data);
            ApplyChartStyle(AgingChartPanel.FindName("AgingChart") as DevExpress.Xpf.Charts.ChartControl, f);
            VM.StatusText = "SD Aging loaded";
        }
        catch (Exception ex) { AppLogger.LogException("ServiceDesk", ex, "LoadSdAgingAsync"); }
    }

    private async Task LoadSdTeamsAsync()
    {
        try
        {
            var techNames = await VM.Repo.GetSdTechnicianNamesAsync();
            TeamsPanel.LoadTechnicians(techNames);
            var teams = await VM.Repo.GetSdTeamsAsync();
            TeamsPanel.LoadTeams(teams);
        }
        catch (Exception ex) { AppLogger.LogException("ServiceDesk", ex, "LoadSdTeamsAsync"); }
    }

    private async Task LoadSdGroupsAsync()
    {
        try
        {
            var groups = await VM.Repo.GetSdGroupsAsync();
            SdGroupsGridPanel.Grid.ItemsSource = groups;
        }
        catch (Exception ex) { AppLogger.LogException("ServiceDesk", ex, "LoadSdGroupsAsync"); }
    }

    private async Task LoadSdGroupCatsAsync()
    {
        try
        {
            var groupNames = await VM.Repo.GetSdGroupNamesAsync();
            var cats = await VM.Repo.GetSdGroupCategoriesAsync();
            var ticketCounts = await VM.Repo.GetSdGroupTicketCountsAsync();
            GroupCatsPanel.LoadData(cats, groupNames, ticketCounts);
        }
        catch (Exception ex) { AppLogger.LogException("ServiceDesk", ex, "LoadSdGroupCatsAsync"); }
    }

    private async Task LoadSdTechniciansAsync()
    {
        try
        {
            var techs = await VM.Repo.GetSdTechniciansAsync();
            SdTechGridPanel.Grid.ItemsSource = techs;
        }
        catch (Exception ex) { AppLogger.LogException("ServiceDesk", ex, "LoadSdTechniciansAsync"); }
    }

    private async Task LoadSdRequestersAsync()
    {
        try
        {
            var reqs = await VM.Repo.GetSdRequestersAsync();
            SdReqGridPanel.Grid.ItemsSource = reqs;
        }
        catch (Exception ex) { AppLogger.LogException("ServiceDesk", ex, "LoadSdRequestersAsync"); }
    }

    private void ApplyAllSdGridOptions()
    {
        var f = SdSettingsPanel.GetCurrentFilters();
        ApplySdGridOptions(RequestGridPanel.Grid, RequestGridPanel.View, f);
        ApplySdGridOptions(SdGroupsGridPanel.Grid, SdGroupsGridPanel.View, f);
        ApplySdGridOptions(SdTechGridPanel.Grid, SdTechGridPanel.View, f);
        ApplySdGridOptions(SdReqGridPanel.Grid, SdReqGridPanel.View, f);
    }

    private static void ApplySdGridOptions(DevExpress.Xpf.Grid.GridControl grid, DevExpress.Xpf.Grid.TableView view, Central.Core.Models.SdFilterState f)
    {
        try
        {
            view.ShowGroupPanel = f.ShowGroupPanel;
            view.ShowAutoFilterRow = f.ShowAutoFilter;
            view.ShowTotalSummary = f.ShowTotalSummary;
            view.UseEvenRowBackground = f.AlternateRows;
            view.ShowSearchPanelMode = f.ShowSearchPanel
                ? DevExpress.Xpf.Grid.ShowSearchPanelMode.Always
                : DevExpress.Xpf.Grid.ShowSearchPanelMode.Never;
            view.ShowFilterPanelMode = f.ShowFilterPanel
                ? DevExpress.Xpf.Grid.ShowFilterPanelMode.ShowAlways
                : DevExpress.Xpf.Grid.ShowFilterPanelMode.Never;
            // Grid row height style
            view.RowMinHeight = f.GridStyle switch { 1 => 20, 2 => 32, _ => 25 };
        }
        catch { }
    }

    /// <summary>Apply chart type, bar style, and palette based on SdFilterState.</summary>
    private static void ApplyChartStyle(DevExpress.Xpf.Charts.ChartControl? chart, Central.Core.Models.SdFilterState f)
    {
        if (chart?.Diagram is not DevExpress.Xpf.Charts.XYDiagram2D diag) return;
        try
        {
            // Collect info from existing bar series to rebuild
            var barInfos = new List<(string DisplayName, string ArgMember, string ValMember,
                object? DataSource, System.Windows.Media.SolidColorBrush? Brush, string? CrosshairPattern, bool LabelsVis)>();

            foreach (var series in diag.Series.ToList())
            {
                if (series is DevExpress.Xpf.Charts.BarSideBySideSeries2D bar)
                {
                    barInfos.Add((bar.DisplayName ?? "", bar.ArgumentDataMember ?? "", bar.ValueDataMember ?? "",
                        bar.DataSource, bar.Brush as System.Windows.Media.SolidColorBrush, bar.CrosshairLabelPattern, f.ShowBarLabels));
                }
            }

            // Only rebuild if we have bar series and chart type changed from default
            if (barInfos.Count > 0)
            {
                // Remove old bar series (keep line series)
                var lineSeries = diag.Series.Where(s => s is not DevExpress.Xpf.Charts.BarSideBySideSeries2D
                    && s is not DevExpress.Xpf.Charts.BarStackedSeries2D
                    && s is not DevExpress.Xpf.Charts.BarFullStackedSeries2D
                    && s is not DevExpress.Xpf.Charts.BarSideBySideStackedSeries2D
                    && s is not DevExpress.Xpf.Charts.LineSeries2D { DisplayName: "Issues created" or "Issues closed" }
                    && s is not DevExpress.Xpf.Charts.AreaSeries2D
                    && s is not DevExpress.Xpf.Charts.SplineSeries2D).ToList();

                // Remove all non-line overlay series
                for (int i = diag.Series.Count - 1; i >= 0; i--)
                {
                    var s = diag.Series[i];
                    if (s is DevExpress.Xpf.Charts.LineSeries2D ls &&
                        ls.DisplayName is "Avg resolution days" or "Open issues" or "Total created" or "Total closed")
                        continue; // keep overlay lines
                    diag.Series.RemoveAt(i);
                }

                // Bar model
                DevExpress.Xpf.Charts.Bar2DModel barModel = f.BarStyle switch
                {
                    1 => new DevExpress.Xpf.Charts.GradientBar2DModel(),
                    2 => new DevExpress.Xpf.Charts.FlatGlassBar2DModel(),
                    3 => new DevExpress.Xpf.Charts.Quasi3DBar2DModel(),
                    _ => new DevExpress.Xpf.Charts.BorderlessSimpleBar2DModel()
                };

                // Rebuild with new chart type
                foreach (var info in barInfos)
                {
                    DevExpress.Xpf.Charts.XYSeries2D newSeries = f.ChartType switch
                    {
                        1 => new DevExpress.Xpf.Charts.BarStackedSeries2D
                        {
                            DisplayName = info.DisplayName, ArgumentDataMember = info.ArgMember,
                            ValueDataMember = info.ValMember, DataSource = info.DataSource,
                            CrosshairLabelPattern = info.CrosshairPattern,
                            Model = barModel, Brush = info.Brush, LabelsVisibility = info.LabelsVis
                        },
                        2 => new DevExpress.Xpf.Charts.BarFullStackedSeries2D
                        {
                            DisplayName = info.DisplayName, ArgumentDataMember = info.ArgMember,
                            ValueDataMember = info.ValMember, DataSource = info.DataSource,
                            CrosshairLabelPattern = info.CrosshairPattern,
                            Model = barModel, Brush = info.Brush, LabelsVisibility = info.LabelsVis
                        },
                        3 => new DevExpress.Xpf.Charts.BarSideBySideStackedSeries2D
                        {
                            DisplayName = info.DisplayName, ArgumentDataMember = info.ArgMember,
                            ValueDataMember = info.ValMember, DataSource = info.DataSource,
                            CrosshairLabelPattern = info.CrosshairPattern,
                            Model = barModel, Brush = info.Brush, LabelsVisibility = info.LabelsVis
                        },
                        4 => new DevExpress.Xpf.Charts.LineSeries2D
                        {
                            DisplayName = info.DisplayName, ArgumentDataMember = info.ArgMember,
                            ValueDataMember = info.ValMember, DataSource = info.DataSource,
                            CrosshairLabelPattern = info.CrosshairPattern,
                            Brush = info.Brush, MarkerVisible = true
                        },
                        5 => new DevExpress.Xpf.Charts.SplineSeries2D
                        {
                            DisplayName = info.DisplayName, ArgumentDataMember = info.ArgMember,
                            ValueDataMember = info.ValMember, DataSource = info.DataSource,
                            CrosshairLabelPattern = info.CrosshairPattern,
                            Brush = info.Brush, MarkerVisible = true
                        },
                        6 => new DevExpress.Xpf.Charts.AreaSeries2D
                        {
                            DisplayName = info.DisplayName, ArgumentDataMember = info.ArgMember,
                            ValueDataMember = info.ValMember, DataSource = info.DataSource,
                            CrosshairLabelPattern = info.CrosshairPattern,
                            Brush = info.Brush
                        },
                        7 => new DevExpress.Xpf.Charts.AreaStackedSeries2D
                        {
                            DisplayName = info.DisplayName, ArgumentDataMember = info.ArgMember,
                            ValueDataMember = info.ValMember, DataSource = info.DataSource,
                            CrosshairLabelPattern = info.CrosshairPattern,
                            Brush = info.Brush
                        },
                        _ => new DevExpress.Xpf.Charts.BarSideBySideSeries2D
                        {
                            DisplayName = info.DisplayName, ArgumentDataMember = info.ArgMember,
                            ValueDataMember = info.ValMember, DataSource = info.DataSource,
                            CrosshairLabelPattern = info.CrosshairPattern,
                            Model = barModel, Brush = info.Brush, LabelsVisibility = info.LabelsVis
                        }
                    };
                    diag.Series.Insert(0, newSeries); // insert before overlay lines
                }

                // Re-add overlay lines that were kept
                // (they're still in the collection since we only removed non-overlays)
            }
            else
            {
                // No bar series to rebuild — just apply bar model to any existing bars
                DevExpress.Xpf.Charts.Bar2DModel model = f.BarStyle switch
                {
                    1 => new DevExpress.Xpf.Charts.GradientBar2DModel(),
                    2 => new DevExpress.Xpf.Charts.FlatGlassBar2DModel(),
                    3 => new DevExpress.Xpf.Charts.Quasi3DBar2DModel(),
                    _ => new DevExpress.Xpf.Charts.BorderlessSimpleBar2DModel()
                };
                foreach (var s in diag.Series)
                    if (s is DevExpress.Xpf.Charts.BarSideBySideSeries2D b) b.Model = model;
            }

            // Chart palette
            chart.Palette = f.ChartTheme switch
            {
                1 => new DevExpress.Xpf.Charts.OfficePalette(),
                2 => new DevExpress.Xpf.Charts.BluePalette(),
                3 => new DevExpress.Xpf.Charts.NatureColorsPalette(),
                _ => null
            };
        }
        catch { }
    }

    private void ToggleSdSettings()
    {
        DockManager.DockController.Restore(SdSettingsLayoutPanel);
        DockManager.DockController.Activate(SdSettingsLayoutPanel);
        _ = RefreshSdSettingsAsync();
    }

    // WireSdRequestLookupsAsync removed — combos bound in RefreshSdSettingsAsync, write-back in WireServiceDeskDelegates

    /// <summary>Build a ManageEngineSyncService with all config loaded from the local DB.</summary>
    private async Task<Central.Module.ServiceDesk.Services.ManageEngineSyncService?> CreateMeSyncServiceAsync()
    {
        var integration = await VM.Repo.GetIntegrationByNameAsync("manageengine");
        if (integration == null)
        {
            NotificationService.Instance.Warning("ManageEngine integration not configured — set up in Admin > Integrations");
            return null;
        }
        if (!integration.IsEnabled)
        {
            NotificationService.Instance.Warning("ManageEngine integration is disabled");
            return null;
        }

        var svc = new Central.Module.ServiceDesk.Services.ManageEngineSyncService(App.Dsn);

        // Load URLs from integration config (base_url column + config_json for oauth/portal)
        svc.BaseUrl = integration.BaseUrl;
        try
        {
            var cfg = System.Text.Json.JsonDocument.Parse(integration.ConfigJson).RootElement;
            if (cfg.TryGetProperty("oauth_url", out var ou)) svc.OAuthUrl = ou.GetString() ?? "";
            if (cfg.TryGetProperty("portal_url", out var pu)) svc.PortalUrl = pu.GetString() ?? "";
        }
        catch { }

        // Load encrypted credentials
        svc.ClientId = await VM.Repo.GetIntegrationCredentialAsync(integration.Id, "client_id");
        svc.ClientSecret = await VM.Repo.GetIntegrationCredentialAsync(integration.Id, "client_secret");
        svc.RefreshToken = await VM.Repo.GetIntegrationCredentialAsync(integration.Id, "refresh_token");

        if (string.IsNullOrEmpty(svc.ClientId) || string.IsNullOrEmpty(svc.RefreshToken))
        {
            NotificationService.Instance.Warning("ManageEngine credentials missing — configure in Admin > Integrations");
            return null;
        }

        return svc;
    }

    /// <summary>Sync requests from ManageEngine into local DB (incremental — only changed records).</summary>
    public async Task SyncManageEngineAsync()
    {
        try
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            VM.StatusText = "Syncing from ManageEngine...";

            var svc = await CreateMeSyncServiceAsync();
            if (svc == null) return;

            svc.StatusChanged += s => Dispatcher.Invoke(() => VM.StatusText = s);

            // Persist rotated refresh token if Zoho sends a new one
            var integrationForToken = await VM.Repo.GetIntegrationByNameAsync("manageengine");
            svc.RefreshTokenRotated += newToken =>
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await VM.Repo.SaveIntegrationCredentialAsync(integrationForToken?.Id ?? 1, "refresh_token", newToken);
                        Dispatcher.Invoke(() => VM.StatusText = "Refresh token rotated and saved");
                    }
                    catch { }
                });
            };

            var count = await svc.SyncRequestsAsync();
            sw.Stop();

            var integration = await VM.Repo.GetIntegrationByNameAsync("manageengine");
            var integrationId = integration?.Id ?? 1;

            if (count < 0)
            {
                // Auth failed
                VM.StatusText = svc.LastError ?? "ManageEngine auth failed";
                NotificationService.Instance.Error(svc.LastError ?? "OAuth token refresh failed — update credentials in Admin > Integrations");
                try { await VM.Repo.LogIntegrationAsync(integrationId, "sync", "auth_failed", svc.LastError); } catch { }
                return;
            }

            var msg = count == 0
                ? "ManageEngine: already up to date"
                : $"ManageEngine sync: {count} requests ({svc.NewCount} new, {svc.UpdatedCount} updated) in {sw.Elapsed.TotalSeconds:F1}s";
            VM.StatusText = msg;

            if (count == 0)
                NotificationService.Instance.Info("Service Desk already up to date — no changes since last sync");
            else
                NotificationService.Instance.Success(msg);

            try
            {
                await VM.Repo.UpdateIntegrationLastSyncAsync(integrationId);
                await VM.Repo.LogIntegrationAsync(integrationId, "sync", "success", msg, (int)sw.ElapsedMilliseconds);
            }
            catch { }

            await LoadServiceDeskAsync();
        }
        catch (Exception ex)
        {
            VM.StatusText = $"Sync error: {ex.Message}";
            AppLogger.LogException("ServiceDesk", ex, "SyncManageEngineAsync");
            try { await VM.Repo.LogIntegrationAsync(1, "sync", "error", ex.Message); } catch { }
        }
    }

    /// <summary>Write an update back to ManageEngine for the selected request.</summary>
    private async Task WriteBackToManageEngineAsync(string action)
    {
        if (RequestGridPanel.Grid.CurrentItem is not Central.Core.Models.SdRequest request)
        {
            NotificationService.Instance.Warning("No request selected");
            return;
        }

        var svc = await CreateMeSyncServiceAsync();
        if (svc == null) return;

        (bool ok, string message) result;

        switch (action)
        {
            case "status":
                var statusOptions = new[] { "Open", "In Progress", "On Hold", "Resolved", "Closed" };
                var newStatus = await ShowPickerAsync("Update Status", "Select new status:", statusOptions, request.Status);
                if (newStatus == null) return;
                VM.StatusText = $"Updating status to '{newStatus}'...";
                result = await svc.UpdateStatusAsync(request.Id, newStatus);
                if (result.ok) request.Status = newStatus;
                break;

            case "priority":
                var priorityOptions = new[] { "Low", "Medium", "Normal", "High", "Urgent" };
                var newPriority = await ShowPickerAsync("Update Priority", "Select new priority:", priorityOptions, request.Priority);
                if (newPriority == null) return;
                VM.StatusText = $"Updating priority to '{newPriority}'...";
                result = await svc.UpdatePriorityAsync(request.Id, newPriority);
                if (result.ok) request.Priority = newPriority;
                break;

            case "technician":
                var techName = InputPrompt.Show("Assign Technician", "Enter technician name:", request.TechnicianName);
                if (techName == null) return;
                VM.StatusText = $"Assigning to '{techName}'...";
                result = await svc.AssignTechnicianAsync(request.Id, techName);
                if (result.ok) request.TechnicianName = techName;
                break;

            case "note":
                var note = InputPrompt.Show("Add Note", "Enter note text:");
                if (string.IsNullOrWhiteSpace(note)) return;
                VM.StatusText = "Adding note...";
                result = await svc.AddNoteAsync(request.Id, note);
                break;

            default:
                return;
        }

        VM.StatusText = result.message;
        if (result.ok)
        {
            NotificationService.Instance.Success($"{request.DisplayId}: {result.message}");
            var integration = await VM.Repo.GetIntegrationByNameAsync("manageengine");
            try { await VM.Repo.LogIntegrationAsync(integration?.Id ?? 1, $"write:{action}", "success", $"{request.DisplayId}: {result.message}"); } catch { }
        }
        else
        {
            NotificationService.Instance.Error(result.message);
            try { await VM.Repo.LogIntegrationAsync(1, $"write:{action}", "error", result.message); } catch { }
        }
    }

    private Task<string?> ShowPickerAsync(string title, string prompt, string[] options, string? current = null)
    {
        return Task.Run(() =>
        {
            string? result = null;
            Dispatcher.Invoke(() =>
            {
                var win = new System.Windows.Window
                {
                    Title = title, Width = 300, Height = 200, WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner,
                    Owner = this, ResizeMode = System.Windows.ResizeMode.NoResize
                };
                var panel = new System.Windows.Controls.StackPanel { Margin = new System.Windows.Thickness(15) };
                panel.Children.Add(new System.Windows.Controls.TextBlock { Text = prompt, Margin = new System.Windows.Thickness(0, 0, 0, 8) });
                var combo = new System.Windows.Controls.ComboBox { Margin = new System.Windows.Thickness(0, 0, 0, 15) };
                foreach (var o in options) combo.Items.Add(o);
                if (current != null) combo.SelectedItem = current;
                panel.Children.Add(combo);
                var btn = new System.Windows.Controls.Button { Content = "OK", Width = 80, HorizontalAlignment = System.Windows.HorizontalAlignment.Right };
                btn.Click += (_, _) => { result = combo.SelectedItem as string; win.DialogResult = true; };
                panel.Children.Add(btn);
                win.Content = panel;
                win.ShowDialog();
            });
            return result;
        });
    }

    private async Task LoadIntegrationsDataAsync()
    {
        try
        {
            var integrations = await VM.Repo.GetIntegrationsAsync();
            IntegrationsGridPanel.Integrations.Clear();
            foreach (var i in integrations) IntegrationsGridPanel.Integrations.Add(i);

            IntegrationsGridPanel.SaveIntegration = async integration =>
            {
                await VM.Repo.UpsertIntegrationAsync(integration);
                NotificationService.Instance.Success($"Integration saved: {integration.DisplayName}");
            };

            IntegrationsGridPanel.SaveCredential = async (integrationId, key, value) =>
            {
                await VM.Repo.SaveIntegrationCredentialAsync(integrationId, key, value);
            };

            IntegrationsGridPanel.GetCredential = async (integrationId, key) =>
            {
                return await VM.Repo.GetIntegrationCredentialAsync(integrationId, key);
            };

            IntegrationsGridPanel.LoadLog = async integrationId =>
            {
                return await VM.Repo.GetIntegrationLogAsync(integrationId);
            };
        }
        catch (Exception ex) { AppLogger.LogException("Integrations", ex, "LoadIntegrationsDataAsync"); }
    }

    // ── Admin panel loading methods extracted to MainWindow.AdminPanels.cs ──

    /// <summary>Reload both ribbon tree panels (user + admin) with fresh DB data.</summary>
    private async Task ReloadRibbonTreesAsync()
    {
        try
        {
            var pages = await VM.Repo.GetRibbonPagesAsync();
            var groups = await VM.Repo.GetRibbonGroupsAsync();
            var items = await VM.Repo.GetRibbonItemsAsync();
            var adminDefaults = await VM.Repo.GetAdminRibbonDefaultsAsync();
            var userId = AuthContext.Instance.CurrentUser?.Id ?? 0;
            var userOverrides = userId > 0 ? await VM.Repo.GetUserRibbonOverridesAsync(userId) : null;

            // Reload user tree — shows admin defaults as baseline, user overrides on top
            RibbonTreeGridPanel.LoadFromRibbon(pages, groups, items, userOverrides);
            // Apply admin defaults to user tree items that have no user override
            foreach (var node in RibbonTreeGridPanel.Items)
            {
                var hasUserOverride = userOverrides?.Any(o => o.ItemKey == node.ItemKey && !string.IsNullOrEmpty(o.CustomIcon)) ?? false;
                if (!hasUserOverride)
                {
                    var adminDef = adminDefaults.FirstOrDefault(d => d.ItemKey == node.ItemKey || d.ItemKey == node.Text);
                    if (adminDef != default && !string.IsNullOrEmpty(adminDef.Icon))
                    {
                        node.IconName = adminDef.Icon;
                    }
                }
            }
            RibbonTreeGridPanel.RefreshIconPreviews();

            // Reload admin tree
            RibbonAdminTreeGridPanel.LoadFromRibbon(pages, groups, items, adminDefaults);
            RibbonAdminTreeGridPanel.RefreshIconPreviews();
        }
        catch (Exception ex) { AppLogger.LogException("Ribbon", ex, "ReloadRibbonTreesAsync"); }
    }

    private async Task<List<Central.Module.Tasks.Views.PortfolioTreeNode>> BuildPortfolioTreeAsync()
    {
        var nodes = new List<Central.Module.Tasks.Views.PortfolioTreeNode>();
        var portfolios = await VM.Repo.GetPortfoliosAsync();
        var programmes = await VM.Repo.GetProgrammesAsync();
        var projects = await VM.Repo.GetTaskProjectsAsync();
        var allTasks = await VM.Repo.GetTasksAsync();
        var sprints = await VM.Repo.GetSprintsAsync();

        foreach (var pf in portfolios)
        {
            var pfTasks = allTasks; // Portfolio spans all
            nodes.Add(new Central.Module.Tasks.Views.PortfolioTreeNode
            {
                TreeId = $"pf_{pf.Id}", Name = pf.Name, Level = "Portfolio",
                TaskCount = pfTasks.Count,
                TotalPoints = pfTasks.Sum(t => t.Points ?? 0),
                CompletedPct = pfTasks.Count > 0 ? $"{pfTasks.Count(t => t.Status == "Done") * 100 / pfTasks.Count}%" : "0%",
                OpenBugs = pfTasks.Count(t => t.TaskType == "Bug" && t.Status != "Done" && t.Status != "Closed"),
            });
        }

        foreach (var pg in programmes)
        {
            nodes.Add(new Central.Module.Tasks.Views.PortfolioTreeNode
            {
                TreeId = $"pg_{pg.Id}", TreeParentId = pg.PortfolioId.HasValue ? $"pf_{pg.PortfolioId}" : null,
                Name = pg.Name, Level = "Programme"
            });
        }

        foreach (var pr in projects)
        {
            var prTasks = allTasks.Where(t => t.ProjectId == pr.Id).ToList();
            var prSprints = sprints.Where(s => s.ProjectId == pr.Id && s.Status == "Active").Count();
            nodes.Add(new Central.Module.Tasks.Views.PortfolioTreeNode
            {
                TreeId = $"pr_{pr.Id}", TreeParentId = pr.ProgrammeId.HasValue ? $"pg_{pr.ProgrammeId}" : null,
                Name = pr.Name, Level = "Project",
                TaskCount = prTasks.Count,
                TotalPoints = prTasks.Sum(t => t.Points ?? 0),
                CompletedPct = prTasks.Count > 0 ? $"{prTasks.Count(t => t.Status == "Done") * 100 / prTasks.Count}%" : "0%",
                OpenBugs = prTasks.Count(t => t.TaskType == "Bug" && t.Status != "Done" && t.Status != "Closed"),
                ActiveSprints = prSprints
            });
        }

        return nodes;
    }

    private async Task LoadRibbonConfigDataAsync()
    {
        try
        {
            // Sync module registrations → DB if items table is empty
            var items = await VM.Repo.GetRibbonItemsAsync();
            if (items.Count == 0)
            {
                await SyncModuleRibbonToDbAsync();
                items = await VM.Repo.GetRibbonItemsAsync();
            }

            var pages = await VM.Repo.GetRibbonPagesAsync();
            var groups = await VM.Repo.GetRibbonGroupsAsync();
            // Wire admin tree panel
            var adminDefaults = await VM.Repo.GetAdminRibbonDefaultsAsync();
            RibbonAdminTreeGridPanel.LoadFromRibbon(pages, groups, items, adminDefaults);

            RibbonAdminTreeGridPanel.OpenIconPicker = () =>
            {
                var picker = new ImagePickerWindow(App.Dsn) { Owner = this };
                return picker.ShowDialog() == true ? picker.SelectedIconName : null;
            };
            RibbonAdminTreeGridPanel.RenderIconPreview = iconName =>
                ResolveGlyphImage(iconName);
            RibbonAdminTreeGridPanel.RefreshIconPreviews();

            RibbonAdminTreeGridPanel.PromptForText = (title, prompt, defaultVal) =>
                Services.InputPrompt.Show(title, prompt, defaultVal, this);

            RibbonAdminTreeGridPanel.GetLinkTargets = () =>
            {
                var targets = new List<string>();
                // Panels
                foreach (var name in new[] { "Devices", "Switches", "P2P", "B2B", "FW", "VLANs", "BGP",
                    "ASN", "Master", "Servers", "MLAG", "MSTP", "ServerAs", "IpRanges",
                    "Roles", "Users", "Lookups", "Settings", "SshLogs", "AppLog", "Jobs",
                    "Tasks", "RibbonConfig", "Builder", "Deploy", "Details", "Compare", "Diagram" })
                    targets.Add($"panel:{name}");
                // Pages
                foreach (var page in DefaultRibbonCategory.Pages.OfType<DevExpress.Xpf.Ribbon.RibbonPage>())
                    if (!string.IsNullOrEmpty(page.Caption?.ToString()))
                        targets.Add($"page:{page.Caption}");
                // Actions
                targets.Add("action:RefreshAll");
                targets.Add("action:PingAll");
                targets.Add("action:SyncAllBgp");
                targets.Add("action:Export");
                targets.Add("action:PrintPreview");
                targets.Add("url:http://127.0.0.1:7472");
                return targets;
            };

            var adminUserId = AuthContext.Instance.CurrentUser?.Id ?? 0;
            RibbonAdminTreeGridPanel.SaveAdminDefault = async node =>
            {
                if (adminUserId == 0 || !AuthContext.Instance.IsAdmin) return;
                await VM.Repo.UpsertAdminRibbonDefaultAsync(
                    node.ItemKey, node.DefaultIcon, node.DefaultLabel, node.IsHidden, adminUserId,
                    node.DisplayStyle, node.LinkTarget);
                NotificationService.Instance.Success($"Default saved: {node.Text}");
                RefreshRibbon();
                // Update user tree to reflect new admin default
                _ = ReloadRibbonTreesAsync();
            };

            RibbonAdminTreeGridPanel.PushAllDefaults = async treeItems =>
            {
                if (adminUserId == 0 || !AuthContext.Instance.IsAdmin) return;
                int count = 0;
                foreach (var node in treeItems.Where(n => !string.IsNullOrEmpty(n.DefaultIcon) || !string.IsNullOrEmpty(n.DefaultLabel) || n.IsHidden || !string.IsNullOrEmpty(n.LinkTarget)))
                {
                    await VM.Repo.UpsertAdminRibbonDefaultAsync(
                        node.ItemKey, node.DefaultIcon, node.DefaultLabel, node.IsHidden, adminUserId,
                        node.DisplayStyle, node.LinkTarget);
                    count++;
                }
                NotificationService.Instance.Success($"Pushed {count} defaults for all users");
                RefreshRibbon();

                // Reload BOTH trees with fresh data
                _ = ReloadRibbonTreesAsync();
            };
            // Wire user tree panel
            var userId = AuthContext.Instance.CurrentUser?.Id ?? 0;
            var userOverrides = userId > 0 ? await VM.Repo.GetUserRibbonOverridesAsync(userId) : null;
            RibbonTreeGridPanel.LoadFromRibbon(pages, groups, items, userOverrides);

            RibbonTreeGridPanel.OpenIconPicker = () =>
            {
                var picker = new ImagePickerWindow(App.Dsn) { Owner = this };
                return picker.ShowDialog() == true ? picker.SelectedIconName : null;
            };
            RibbonTreeGridPanel.PromptForText = (title, prompt, defaultVal) =>
                Services.InputPrompt.Show(title, prompt, defaultVal, this);
            RibbonTreeGridPanel.RenderIconPreview = iconName =>
                ResolveGlyphImage(iconName);
            RibbonTreeGridPanel.RefreshIconPreviews();

            // Auto-save single override on icon pick / hide toggle
            RibbonTreeGridPanel.SaveSingleOverride = async node =>
            {
                if (userId == 0) return;
                if (!string.IsNullOrEmpty(node.CustomText) || !string.IsNullOrEmpty(node.IconName) || node.IsHidden)
                {
                    await VM.Repo.UpsertUserRibbonOverrideAsync(new Central.Core.Models.UserRibbonOverride
                    {
                        UserId = userId, ItemKey = node.ItemKey,
                        CustomIcon = node.IconName, CustomText = node.CustomText,
                        IsHidden = node.IsHidden, SortOrder = node.SortOrder
                    });
                }
                else
                {
                    // No overrides — remove the row
                    await VM.Repo.DeleteUserRibbonOverrideAsync(userId, node.ItemKey);
                }
                // Refresh ribbon immediately
                RefreshRibbon();
            };

            RibbonTreeGridPanel.SaveOverrides = async treeItems =>
            {
                if (userId == 0) return;
                // Save ALL node types (pages, groups, items) — not just items
                foreach (var node in treeItems)
                {
                    if (!string.IsNullOrEmpty(node.CustomText) || !string.IsNullOrEmpty(node.IconName) || node.IsHidden)
                    {
                        await VM.Repo.UpsertUserRibbonOverrideAsync(new Central.Core.Models.UserRibbonOverride
                        {
                            UserId = userId, ItemKey = node.ItemKey,
                            CustomIcon = node.IconName, CustomText = node.CustomText,
                            IsHidden = node.IsHidden, SortOrder = node.SortOrder
                        });
                    }
                }
                RefreshRibbon();
                NotificationService.Instance.Success("Ribbon customizations applied");
            };

            RibbonTreeGridPanel.ResetOverrides = async () =>
            {
                if (userId == 0) return;
                // Delete all user overrides
                await using var conn = new Npgsql.NpgsqlConnection(App.Dsn);
                await conn.OpenAsync();
                await using var cmd = new Npgsql.NpgsqlCommand("DELETE FROM user_ribbon_overrides WHERE user_id=@u", conn);
                cmd.Parameters.AddWithValue("u", userId);
                await cmd.ExecuteNonQueryAsync();

                // Refresh ribbon (will now show admin defaults since user overrides are gone)
                RefreshRibbon();

                // Force full reload of config panel data (re-syncs everything fresh)
                await LoadRibbonConfigDataAsync();
                NotificationService.Instance.Info("Ribbon reset to admin defaults");
            };
        }
        catch (Exception ex) { AppLogger.LogException("Ribbon", ex, "LoadRibbonConfigDataAsync"); }
    }

    private async Task LoadJobsDataAsync()
    {
        try
        {
            var apiUrl = App.Settings?.Get<string>("api.url") ?? "http://192.168.56.203:8000";
            using var client = new Central.Api.Client.CentralApiClient(apiUrl);
            var loginResult = await client.LoginAsync(AuthContext.Instance.CurrentUser?.Username ?? Environment.UserName);
            if (loginResult == null) { VM.StatusText = "Jobs: API login failed"; return; }

            var schedules = await client.GetListAsync<System.Text.Json.JsonElement>("api/jobs");
            var history = await client.GetListAsync<System.Text.Json.JsonElement>("api/jobs/history");

            Dispatcher.Invoke(() =>
            {
                var scheduleData = schedules.Select(s => new
                {
                    Name = s.GetProperty("name").GetString(),
                    JobType = s.GetProperty("job_type").GetString(),
                    IsEnabled = s.GetProperty("is_enabled").GetBoolean(),
                    IntervalMinutes = s.GetProperty("interval_minutes").GetInt32(),
                    LastRunAt = s.TryGetProperty("last_run_at", out var lr) && lr.ValueKind != System.Text.Json.JsonValueKind.Null ? lr.GetString() : "Never",
                    NextRunAt = s.TryGetProperty("next_run_at", out var nr) && nr.ValueKind != System.Text.Json.JsonValueKind.Null ? nr.GetString() : "—"
                }).ToList();
                JobsGridPanel.SchedulesGridControl.ItemsSource = scheduleData;

                var historyData = history.Select(h => new
                {
                    JobType = h.GetProperty("job_type").GetString(),
                    StartedAt = h.GetProperty("started_at").GetString(),
                    Status = h.GetProperty("status").GetString(),
                    Summary = h.TryGetProperty("result_summary", out var rs) ? rs.GetString() : "",
                    Succeeded = h.GetProperty("items_succeeded").GetInt32(),
                    Failed = h.GetProperty("items_failed").GetInt32(),
                    TriggeredBy = h.TryGetProperty("triggered_by", out var tb) ? tb.GetString() : ""
                }).ToList();
                JobsGridPanel.HistoryGridControl.ItemsSource = historyData;

                VM.StatusText = $"Jobs: {scheduleData.Count} schedules, {historyData.Count} history entries";
            });
        }
        catch (Exception ex)
        {
            VM.StatusText = $"Jobs: {ex.Message} (is API server running?)";
        }
    }

    private void WireJobsPanel()
    {
        JobsGridPanel.JobActionRequested += async (action, jobId) =>
        {
            try
            {
                var apiUrl = App.Settings?.Get<string>("api.url") ?? "http://192.168.56.203:8000";
                using var client = new Central.Api.Client.CentralApiClient(apiUrl);
                var login = await client.LoginAsync(AuthContext.Instance.CurrentUser?.Username ?? Environment.UserName);
                if (login == null) { VM.StatusText = "Jobs: API login failed"; return; }

                switch (action)
                {
                    case "enable":
                        await client.PutAsync($"api/jobs/{jobId}/enable", new { });
                        VM.StatusText = $"Job {jobId} enabled";
                        break;
                    case "disable":
                        await client.PutAsync($"api/jobs/{jobId}/disable", new { });
                        VM.StatusText = $"Job {jobId} disabled";
                        break;
                    case "run":
                        VM.StatusText = $"Running job {jobId}...";
                        await client.PostAsync($"api/jobs/{jobId}/run", new { });
                        VM.StatusText = $"Job {jobId} triggered";
                        break;
                }

                // Refresh after action
                await LoadJobsDataAsync();
            }
            catch (Exception ex)
            {
                VM.StatusText = $"Job action failed: {ex.Message}";
            }
        };
    }

    // ── Print Preview + Column Chooser (engine features for active grid) ──

    private void PrintPreview_ItemClick(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)
    {
        var (_, view) = GetActiveGrid();
        view?.ShowPrintPreview(this);
    }

    private void ColumnChooser_ItemClick(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)
    {
        var (_, view) = GetActiveGrid();
        if (view != null)
        {
            if (view.IsColumnChooserVisible)
                view.HideColumnChooser();
            else
                view.ShowColumnChooser();
        }
    }

    // ── Grid context menus (engine-rendered from ContextMenuItem model) ────

    private void WireGridContextMenus()
    {
        var auth = AuthContext.Instance;

        // Devices grid
        GridContextMenuBuilder.AttachSimple(DeviceGridPanel.Grid,
            ("New Device", () => NewButton_ItemClick(this, null!)),
            ("Edit Device", () => EditButton_ItemClick(this, null!)),
            ("Duplicate", () => DuplicateCurrentDevice()),
            ("Delete Device", () => DeleteButton_ItemClick(this, null!)),
            ("-", () => { }),
            ("Bulk Edit Selected", () => BulkEditButton_ItemClick(this, null!)),
            ("Export to Clipboard", () => ExportGridToClipboard(DeviceGridPanel.Grid)),
            ("-", () => { }),
            ("Refresh", () => _ = VM.ReloadDevicesAsync())
        );

        // Switches grid
        GridContextMenuBuilder.AttachSimple(SwitchGridPanel.Grid,
            ("Ping", () => PingSingleSwitch_ContextMenu()),
            ("Download Config", () => SyncConfigButton_Click(this, null!)),
            ("-", () => { }),
            ("View in Detail", () => { VM.IsDetailsPanelOpen = true; }),
            ("Go to Device", () => NavigateToDevice()),
            ("-", () => { }),
            ("Bulk Edit Selected", () => BulkEditButton_ItemClick(this, null!)),
            ("Export to Clipboard", () => ExportGridToClipboard(SwitchGridPanel.Grid)),
            ("-", () => { }),
            ("Refresh", () => _ = VM.ReloadSwitchesAsync())
        );

        // P2P links
        GridContextMenuBuilder.AttachSimple(P2PGridPanel.Grid,
            ("Preview Deploy Config", () => PreviewDeployConfig(P2PGridPanel.Grid, "p2p")),
            ("Duplicate Link", () => DuplicateCurrentRow(P2PGridPanel.Grid, VM.P2PLinks)),
            ("Delete Link", () => DeleteActiveRow()),
            ("-", () => { }),
            ("Go to Switch A", () => GoToSwitch(GetLinkDevice(P2PGridPanel.Grid, "DeviceA"))),
            ("Go to Switch B", () => GoToSwitch(GetLinkDevice(P2PGridPanel.Grid, "DeviceB"))),
            ("-", () => { }),
            ("Bulk Edit Selected", () => BulkEditButton_ItemClick(this, null!)),
            ("Export to Clipboard", () => ExportGridToClipboard(P2PGridPanel.Grid)),
            ("-", () => { }),
            ("Refresh", () => _ = VM.ReloadP2PLinksAsync())
        );

        // B2B links
        GridContextMenuBuilder.AttachSimple(B2BGridPanel.Grid,
            ("Preview Deploy Config", () => PreviewDeployConfig(B2BGridPanel.Grid, "b2b")),
            ("Duplicate Link", () => DuplicateCurrentRow(B2BGridPanel.Grid, VM.B2BLinks)),
            ("Delete Link", () => DeleteActiveRow()),
            ("-", () => { }),
            ("Go to Switch A", () => GoToSwitch(GetLinkDevice(B2BGridPanel.Grid, "DeviceA"))),
            ("Go to Switch B", () => GoToSwitch(GetLinkDevice(B2BGridPanel.Grid, "DeviceB"))),
            ("-", () => { }),
            ("Bulk Edit Selected", () => BulkEditButton_ItemClick(this, null!)),
            ("Export to Clipboard", () => ExportGridToClipboard(B2BGridPanel.Grid)),
            ("-", () => { }),
            ("Refresh", () => _ = VM.ReloadB2BLinksAsync())
        );

        // FW links
        GridContextMenuBuilder.AttachSimple(FWGridPanel.Grid,
            ("Preview Deploy Config", () => PreviewDeployConfig(FWGridPanel.Grid, "fw")),
            ("Duplicate Link", () => DuplicateCurrentRow(FWGridPanel.Grid, VM.FWLinks)),
            ("Delete Link", () => DeleteActiveRow()),
            ("-", () => { }),
            ("Go to Switch", () => GoToSwitch(GetLinkDevice(FWGridPanel.Grid, "DeviceA"))),
            ("Go to Firewall", () => GoToSwitch(GetLinkDevice(FWGridPanel.Grid, "DeviceB"))),
            ("-", () => { }),
            ("Bulk Edit Selected", () => BulkEditButton_ItemClick(this, null!)),
            ("Export to Clipboard", () => ExportGridToClipboard(FWGridPanel.Grid)),
            ("-", () => { }),
            ("Refresh", () => _ = VM.ReloadFWLinksAsync())
        );

        // VLANs
        GridContextMenuBuilder.AttachSimple(VlanGridPanel.Grid,
            ("Duplicate VLAN", () => DuplicateCurrentRow(VlanGridPanel.Grid, VM.VlanEntries)),
            ("Delete VLAN", () => DeleteActiveRow()),
            ("-", () => { }),
            ("Bulk Edit Selected", () => BulkEditButton_ItemClick(this, null!)),
            ("Export to Clipboard", () => ExportGridToClipboard(VlanGridPanel.Grid)),
            ("-", () => { }),
            ("Refresh", () => _ = VM.ReloadVlansAsync())
        );

        // BGP
        GridContextMenuBuilder.AttachSimple(BgpGridPanel.Grid,
            ("Sync from Switch", () => SyncBgpButton_ItemClick(this, null!)),
            ("-", () => { }),
            ("Go to Switch", () => GoToSwitch(GetLinkDevice(BgpGridPanel.Grid, "Hostname"))),
            ("-", () => { }),
            ("Bulk Edit Selected", () => BulkEditButton_ItemClick(this, null!)),
            ("Export to Clipboard", () => ExportGridToClipboard(BgpGridPanel.Grid)),
            ("Refresh", () => _ = VM.ReloadBgpAsync())
        );

        // ASN
        GridContextMenuBuilder.AttachSimple(AsnGridPanel.Grid,
            ("Duplicate ASN", () => DuplicateCurrentRow(AsnGridPanel.Grid, VM.AsnDefinitions)),
            ("Delete ASN", () => DeleteActiveRow()),
            ("-", () => { }),
            ("Bulk Edit Selected", () => BulkEditButton_ItemClick(this, null!)),
            ("Export to Clipboard", () => ExportGridToClipboard(AsnGridPanel.Grid)),
            ("-", () => { }),
            ("Refresh", () => _ = VM.ReloadAsnAsync())
        );

        // Users (admin)
        GridContextMenuBuilder.AttachSimple(UsersGridPanel.Grid,
            ("Set Password", () => SetPasswordButton_ItemClick(this, null!)),
            ("Duplicate User", () => DuplicateCurrentRow(UsersGridPanel.Grid, VM.Users)),
            ("Delete User", () => DeleteActiveRow()),
            ("-", () => { }),
            ("Bulk Edit Selected", () => BulkEditButton_ItemClick(this, null!)),
            ("Export to Clipboard", () => ExportGridToClipboard(UsersGridPanel.Grid)),
            ("-", () => { }),
            ("Refresh", () => _ = VM.ReloadUsersAsync())
        );

        // Roles (admin)
        GridContextMenuBuilder.AttachSimple(RolesGridPanel.Grid,
            ("Duplicate Role", () => DuplicateCurrentRow(RolesGridPanel.Grid, VM.Roles)),
            ("Delete Role", () => DeleteActiveRow()),
            ("-", () => { }),
            ("Export to Clipboard", () => ExportGridToClipboard(RolesGridPanel.Grid)),
            ("Refresh", () => _ = VM.ReloadRolesAsync())
        );

        // Service Desk requests
        GridContextMenuBuilder.AttachSimple(RequestGridPanel.Grid,
            ("Read (Sync from ME)", () => _ = SyncManageEngineAsync()),
            ("-", () => { }),
            ("Update Status", () => _ = WriteBackToManageEngineAsync("status")),
            ("Update Priority", () => _ = WriteBackToManageEngineAsync("priority")),
            ("Assign Technician", () => _ = WriteBackToManageEngineAsync("technician")),
            ("Add Note", () => _ = WriteBackToManageEngineAsync("note")),
            ("-", () => { }),
            ("Open in Browser", () => {
                if (RequestGridPanel.Grid.CurrentItem is Central.Core.Models.SdRequest req && !string.IsNullOrEmpty(req.TicketUrl))
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(req.TicketUrl) { UseShellExecute = true });
            }),
            ("-", () => { }),
            ("Clear Filter", () => { RequestGridPanel.Grid.FilterString = ""; VM.StatusText = "Filter cleared"; }),
            ("Export to Clipboard", () => ExportGridToClipboard(RequestGridPanel.Grid)),
            ("Refresh", () => _ = LoadServiceDeskAsync())
        );

        // ── New admin panels ──

        GridContextMenuBuilder.AttachSimple(SdTechGridPanel.Grid,
            ("Filter Requests by Tech", () => {
                if (SdTechGridPanel.Grid.CurrentItem is Central.Core.Models.SdTechnician t)
                    PanelMessageBus.Publish(new LinkSelectionMessage("SdTechnicians", "TechnicianName", t.Name));
            }),
            ("-", () => { }),
            ("Export to Clipboard", () => ExportGridToClipboard(SdTechGridPanel.Grid)),
            ("Refresh", () => { })
        );

        GridContextMenuBuilder.AttachSimple(SdGroupsGridPanel.Grid,
            ("Filter Requests by Group", () => {
                if (SdGroupsGridPanel.Grid.CurrentItem is Central.Core.Models.SdGroup g)
                    PanelMessageBus.Publish(new LinkSelectionMessage("SdGroups", "GroupName", g.Name));
            }),
            ("-", () => { }),
            ("Export to Clipboard", () => ExportGridToClipboard(SdGroupsGridPanel.Grid)),
            ("Refresh", () => { })
        );

        GridContextMenuBuilder.AttachSimple(SdReqGridPanel.Grid,
            ("Filter Requests by Requester", () => {
                if (SdReqGridPanel.Grid.CurrentItem is Central.Core.Models.SdRequester r)
                    PanelMessageBus.Publish(new LinkSelectionMessage("SdRequesters", "RequesterName", r.Name));
            }),
            ("-", () => { }),
            ("Export to Clipboard", () => ExportGridToClipboard(SdReqGridPanel.Grid)),
            ("Refresh", () => { })
        );

        if (MasterGridPanel?.Grid != null)
            GridContextMenuBuilder.AttachSimple(MasterGridPanel.Grid,
                ("Go to Device", () => NavigateToDevice()),
                ("-", () => { }),
                ("Export to Clipboard", () => ExportGridToClipboard(MasterGridPanel.Grid)),
                ("Refresh", () => _ = VM.LoadPanelDataAsync("master", force: true))
            );

        if (ServerAsGridPanel?.Grid != null)
            GridContextMenuBuilder.AttachSimple(ServerAsGridPanel.Grid,
                ("Export to Clipboard", () => ExportGridToClipboard(ServerAsGridPanel.Grid)),
                ("Refresh", () => _ = VM.LoadPanelDataAsync("serveras", force: true))
            );

        if (IpRangesGridPanel?.Grid != null)
            GridContextMenuBuilder.AttachSimple(IpRangesGridPanel.Grid,
                ("Export to Clipboard", () => ExportGridToClipboard(IpRangesGridPanel.Grid)),
                ("Refresh", () => _ = VM.LoadPanelDataAsync("ipranges", force: true))
            );

        if (MlagGridPanel?.Grid != null)
            GridContextMenuBuilder.AttachSimple(MlagGridPanel.Grid,
                ("Export to Clipboard", () => ExportGridToClipboard(MlagGridPanel.Grid)),
                ("Refresh", () => _ = VM.LoadPanelDataAsync("mlag", force: true))
            );

        if (MstpGridPanel?.Grid != null)
            GridContextMenuBuilder.AttachSimple(MstpGridPanel.Grid,
                ("Export to Clipboard", () => ExportGridToClipboard(MstpGridPanel.Grid)),
                ("Refresh", () => _ = VM.LoadPanelDataAsync("mstp", force: true))
            );

        if (SchedulerGridPanel?.Grid != null)
            GridContextMenuBuilder.AttachSimple(SchedulerGridPanel.Grid,
                ("New Appointment", () => SchedulerGridPanel.GetType().GetMethod("NewAppointment_Click",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    ?.Invoke(SchedulerGridPanel, new object[] { this, new RoutedEventArgs() })),
                ("Delete", () => SchedulerGridPanel.GetType().GetMethod("DeleteAppointment_Click",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    ?.Invoke(SchedulerGridPanel, new object[] { this, new RoutedEventArgs() })),
                ("-", () => { }),
                ("Export to Clipboard", () => ExportGridToClipboard(SchedulerGridPanel.Grid)),
                ("Refresh", () => { if (SchedulerGridPanel.RefreshRequested != null) _ = SchedulerGridPanel.RefreshRequested(); })
            );

        // ── Task Module Context Menus ──
        WireTaskContextMenus();
    }

    private void WireTaskContextMenus()
    {
        void Noop() { }
        void TaskAdd() { _ = Central.Core.Services.CommandGuard.RunAsync("CtxAddTask", async () =>
        {
            var task = new Central.Core.Models.TaskItem { Title = "New Task", Status = "Open", Priority = "Medium", TaskType = "Task", ProjectId = TaskTreeGridPanel.SelectedProjectId, CreatedAt = DateTime.Now, UpdatedAt = DateTime.Now };
            await VM.SaveTaskAsync(task); VM.TaskItems.Insert(0, task);
        }); }
        void TaskAddSub() { _ = Central.Core.Services.CommandGuard.RunAsync("CtxAddSubTask", async () =>
        {
            var parent = TaskTreeGridPanel.Tree.CurrentItem as Central.Core.Models.TaskItem;
            if (parent == null) return;
            var task = new Central.Core.Models.TaskItem { Title = "New Sub-Task", ParentId = parent.Id, Status = "Open", Priority = "Medium", TaskType = "SubTask", ProjectId = parent.ProjectId, CreatedAt = DateTime.Now, UpdatedAt = DateTime.Now };
            await VM.SaveTaskAsync(task); VM.TaskItems.Add(task);
        }); }
        void TaskDelete()
        {
            if (TaskTreeGridPanel.Tree.CurrentItem is Central.Core.Models.TaskItem t)
            {
                var result = DevExpress.Xpf.Core.ThemedMessageBox.Show(
                    $"Delete task \"{t.Title}\"?\n\nThis will also delete all sub-tasks.",
                    "Confirm Delete", System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning);
                if (result != System.Windows.MessageBoxResult.Yes) return;

                var idx = VM.TaskItems.IndexOf(t);
                UndoService.Instance.RecordRemove(VM.TaskItems, t, idx, $"Delete: {t.Title}");
                VM.TaskItems.Remove(t);
                _ = VM.DeleteTaskAsync(t);
            }
        }
        void TaskRefresh() { _ = VM.LoadTasksAsync(TaskTreeGridPanel.SelectedProjectId); }
        void BacklogRefresh() { _ = VM.LoadTasksAsync(BacklogGridPanel.SelectedProjectId); }
        void SprintRefresh() { _ = VM.LoadTasksAsync(); }
        void BugAdd() { _ = Central.Core.Services.CommandGuard.RunAsync("CtxAddBug", async () =>
        {
            var bug = new Central.Core.Models.TaskItem { Title = "New Bug", Status = "New", Priority = "Medium", TaskType = "Bug", Severity = "Major", BugPriority = "Medium", Category = "Bug", ProjectId = QAGridPanel.SelectedProjectId, CreatedAt = DateTime.Now, UpdatedAt = DateTime.Now };
            await VM.SaveTaskAsync(bug);
            var bugs = await VM.Repo.GetBugsAsync(QAGridPanel.SelectedProjectId);
            System.Windows.Application.Current.Dispatcher.Invoke(() => QAGridPanel.Grid.ItemsSource = bugs);
        }); }
        void BugRefresh() { _ = Task.Run(async () => { var bugs = await VM.Repo.GetBugsAsync(QAGridPanel.SelectedProjectId); System.Windows.Application.Current.Dispatcher.Invoke(() => QAGridPanel.Grid.ItemsSource = bugs); }); }
        void LogTime() { }  // handled by toolbar
        void DeleteEntry()
        {
            if (TimesheetViewPanel.Grid.CurrentItem is Central.Core.Models.TimeEntry e && e.Id > 0)
            {
                _ = VM.Repo.DeleteTimeEntryAsync(e.Id);
                Central.Core.Shell.PanelMessageBus.Publish(new Central.Core.Shell.DataModifiedMessage("time_entries", "TimeEntry", "Delete"));
                VM.StatusText = "Time entry deleted";
            }
        }
        void GoToTask() { if (MyTasksViewPanel.Grid.CurrentItem is Central.Core.Models.TaskItem t) { VM.IsTasksPanelOpen = true; try { TaskTreeGridPanel.Tree.CurrentItem = t; } catch { } } }
        void MyTasksRefresh() { VM.IsMyTasksPanelOpen = false; VM.IsMyTasksPanelOpen = true; }
        void PortfolioRefresh() { _ = BuildPortfolioTreeAsync().ContinueWith(t => System.Windows.Application.Current.Dispatcher.Invoke(() => PortfolioViewPanel.LoadData(t.Result))); }
        void ReportExportCsv() { }

        GridContextMenuBuilder.AttachTree(TaskTreeGridPanel.Tree,
            ("New Task", TaskAdd), ("New Sub-Task", TaskAddSub), ("-", Noop),
            ("Delete Task", TaskDelete), ("-", Noop),
            ("Export to Clipboard", () => ExportTreeToClipboard(TaskTreeGridPanel.Tree)), ("-", Noop),
            ("Refresh", TaskRefresh));

        GridContextMenuBuilder.AttachTree(BacklogGridPanel.Tree,
            ("Commit to Sprint", Noop), ("Uncommit", Noop), ("-", Noop),
            ("Export to Clipboard", () => ExportTreeToClipboard(BacklogGridPanel.Tree)), ("-", Noop),
            ("Refresh", BacklogRefresh));

        GridContextMenuBuilder.AttachSimple(SprintPlanGridPanel.Grid,
            ("New Task in Sprint", TaskAdd), ("-", Noop),
            ("Export to Clipboard", () => ExportGridToClipboard(SprintPlanGridPanel.Grid)), ("-", Noop),
            ("Refresh", SprintRefresh));

        GridContextMenuBuilder.AttachSimple(QAGridPanel.Grid,
            ("New Bug", BugAdd), ("Batch Triage", Noop), ("-", Noop),
            ("Export to Clipboard", () => ExportGridToClipboard(QAGridPanel.Grid)), ("-", Noop),
            ("Refresh", BugRefresh));

        GridContextMenuBuilder.AttachSimple(MyTasksViewPanel.Grid,
            ("Go to Task in Tree", GoToTask), ("-", Noop),
            ("Export to Clipboard", () => ExportGridToClipboard(MyTasksViewPanel.Grid)), ("-", Noop),
            ("Refresh", MyTasksRefresh));

        GridContextMenuBuilder.AttachSimple(TimesheetViewPanel.Grid,
            ("Log Time", LogTime), ("Delete Entry", DeleteEntry), ("-", Noop),
            ("Export to Clipboard", () => ExportGridToClipboard(TimesheetViewPanel.Grid)), ("-", Noop),
            ("Refresh", Noop));

        GridContextMenuBuilder.AttachSimple(ReportBuilderViewPanel.Results,
            ("Export to Clipboard", () => ExportGridToClipboard(ReportBuilderViewPanel.Results)),
            ("Export to CSV", ReportExportCsv));

        GridContextMenuBuilder.AttachTree(PortfolioViewPanel.Tree,
            ("Refresh", PortfolioRefresh));
    }

    private void ExportGridToClipboard(DevExpress.Xpf.Grid.GridControl grid)
    {
        try
        {
            if (grid.View is DevExpress.Xpf.Grid.TableView tv)
            {
                grid.SelectAll();
                grid.CopyToClipboard();
                VM.StatusText = "Grid data copied to clipboard";
            }
        }
        catch (Exception ex) { VM.StatusText = $"Export failed: {ex.Message}"; }
    }

    private void ExportTreeToClipboard(DevExpress.Xpf.Grid.TreeListControl tree)
    {
        try
        {
            tree.SelectAll();
            tree.CopyToClipboard();
            VM.StatusText = "Tree data copied to clipboard";
        }
        catch (Exception ex) { VM.StatusText = $"Export failed: {ex.Message}"; }
    }

    private void GoToSwitch(string? hostname)
    {
        if (string.IsNullOrEmpty(hostname)) return;
        Central.Core.Shell.PanelMessageBus.Publish(new Central.Core.Shell.NavigateToPanelMessage("switches", hostname));
    }

    private string? GetLinkDevice(DevExpress.Xpf.Grid.GridControl grid, string fieldName)
    {
        var current = grid.CurrentItem;
        if (current == null) return null;
        var prop = current.GetType().GetProperty(fieldName);
        return prop?.GetValue(current) as string;
    }

    private void NavigateToDevice()
    {
        if (VM.SelectedSwitch?.Hostname is { } hostname)
            Central.Core.Shell.PanelMessageBus.Publish(new Central.Core.Shell.NavigateToPanelMessage("devices", hostname));
    }

    private void PingSingleSwitch_ContextMenu()
    {
        // Reuse existing ping button handler
        PingSelectedButton_ItemClick(this, null!);
    }

    private void DuplicateCurrentDevice()
    {
        if (DeviceGridPanel.Grid.CurrentItem is not DeviceRecord dev) return;
        var clone = new DeviceRecord();
        foreach (var prop in typeof(DeviceRecord).GetProperties())
        {
            if (!prop.CanWrite) continue;
            try { prop.SetValue(clone, prop.GetValue(dev)); } catch { }
        }
        clone.Id = "";
        clone.SwitchName = dev.SwitchName + "-COPY";
        VM.Devices.Insert(0, clone);
        DeviceGridPanel.Grid.CurrentItem = clone;
        VM.StatusText = "Device duplicated — edit and save";
    }

    private void DuplicateCurrentRow<TLink>(DevExpress.Xpf.Grid.GridControl grid, System.Collections.ObjectModel.ObservableCollection<TLink> collection) where TLink : class, new()
    {
        if (grid.CurrentItem is not TLink source) return;
        var clone = new TLink();
        foreach (var prop in typeof(TLink).GetProperties())
        {
            if (!prop.CanWrite) continue;
            try { prop.SetValue(clone, prop.GetValue(source)); } catch { }
        }
        // Reset Id
        var idProp = typeof(TLink).GetProperty("Id");
        if (idProp?.PropertyType == typeof(int)) idProp.SetValue(clone, 0);
        collection.Insert(0, clone);
        grid.CurrentItem = clone;
        VM.StatusText = "Row duplicated — edit and save";
    }

    // ── Cross-panel message wiring ─────────────────────────────────────────

    private void WirePanelMessages()
    {
        // Navigate to a panel and optionally select an item
        Central.Core.Shell.PanelMessageBus.Subscribe<Central.Core.Shell.NavigateToPanelMessage>(msg =>
        {
            Dispatcher.InvokeAsync(() =>
            {
                var panelMap = new Dictionary<string, DevExpress.Xpf.Docking.BaseLayoutItem>(StringComparer.OrdinalIgnoreCase)
                {
                    ["devices"] = DevicesPanel, ["switches"] = SwitchesPanel, ["p2p"] = P2PPanel,
                    ["b2b"] = B2BPanel, ["fw"] = FWPanel, ["vlans"] = VlansPanel,
                    ["bgp"] = BgpPanel, ["roles"] = RolesPanel, ["users"] = UsersPanel,
                    ["lookups"] = LookupsPanel, ["asn"] = AsnPanel, ["master"] = MasterPanel,
                    ["servicedesk"] = ServiceDeskPanel, ["tasks"] = TasksPanel,
                };

                if (panelMap.TryGetValue(msg.TargetPanel, out var panel))
                {
                    DockManager.DockController.Restore(panel);
                    DockManager.Activate(panel);

                    // If an item to select was provided, try to find and select it in the grid
                    if (msg.SelectItem != null)
                    {
                        if (msg.TargetPanel == "switches" && msg.SelectItem is string hostname)
                        {
                            var sw = VM.Switches.FirstOrDefault(s =>
                                string.Equals(s.Hostname, hostname, StringComparison.OrdinalIgnoreCase));
                            if (sw != null) SwitchGridPanel.Grid.CurrentItem = sw;
                        }
                        else if (msg.TargetPanel == "devices" && msg.SelectItem is string switchName)
                        {
                            var dev = VM.Devices.FirstOrDefault(d =>
                                string.Equals(d.SwitchName, switchName, StringComparison.OrdinalIgnoreCase));
                            if (dev != null) DeviceGridPanel.Grid.CurrentItem = dev;
                        }
                        else if (msg.TargetPanel == "servicedesk" && msg.SelectItem is string sdCommand)
                        {
                            // Open panel + load data
                            VM.IsServiceDeskPanelOpen = true;
                            _ = LoadServiceDeskAsync();

                            if (sdCommand == "sync")
                            {
                                _ = SyncManageEngineAsync();
                            }
                            else if (sdCommand.StartsWith("write:"))
                            {
                                var action = sdCommand[6..];
                                _ = WriteBackToManageEngineAsync(action);
                            }
                            else if (sdCommand.StartsWith("panel:"))
                            {
                                switch (sdCommand[6..])
                                {
                                    case "overview": VM.IsSdOverviewPanelOpen = true; break;
                                    case "closures": VM.IsSdClosuresPanelOpen = true; break;
                                    case "aging": VM.IsSdAgingPanelOpen = true; break;
                                    case "groups": VM.IsSdGroupsPanelOpen = true; break;
                                    case "technicians": VM.IsSdTechniciansPanelOpen = true; break;
                                    case "requesters": VM.IsSdRequestersPanelOpen = true; break;
                                    case "teams": VM.IsSdTeamsPanelOpen = true; break;
                                    case "settings": ToggleSdSettings(); break;
                                    case "details": VM.IsDetailsPanelOpen = !VM.IsDetailsPanelOpen; break;
                                    case "groupcats": _ = LoadSdGroupCatsAsync(); DockManager.DockController.Restore(SdGroupCatsPanel); DockManager.Activate(SdGroupCatsPanel); break;
                                }
                            }
                            else if (sdCommand.StartsWith("filter:"))
                            {
                                var filter = sdCommand[7..];
                                if (filter == "Open")
                                    RequestGridPanel.Grid.FilterString = "[Status] = 'Open' Or [Status] = 'In Progress'";
                                else if (filter == "MyTickets")
                                    RequestGridPanel.Grid.FilterString = $"[TechnicianName] = '{AuthContext.Instance.CurrentUser?.DisplayName}'";
                                else if (filter == "All")
                                    RequestGridPanel.Grid.FilterString = "";
                            }
                        }
                    }
                }
            });
        });

        // Refresh a panel's data on demand
        Central.Core.Shell.PanelMessageBus.Subscribe<Central.Core.Shell.RefreshPanelMessage>(msg =>
        {
            Dispatcher.InvokeAsync(async () =>
            {
                switch (msg.TargetPanel)
                {
                    case "devices": await VM.ReloadDevicesAsync(); break;
                    case "switches": await VM.ReloadSwitchesAsync(); break;
                    case "p2p": await VM.ReloadP2PLinksAsync(); break;
                    case "b2b": await VM.ReloadB2BLinksAsync(); break;
                    case "fw": await VM.ReloadFWLinksAsync(); break;
                    case "vlans": await VM.ReloadVlansAsync(); break;
                    case "bgp": await VM.ReloadBgpAsync(); break;
                    case "servicedesk": await LoadServiceDeskAsync(); break;
                }
            });
        });

        // Auto-refresh dependent panels when data is modified
        Central.Core.Shell.PanelMessageBus.Subscribe<Central.Core.Shell.DataModifiedMessage>(msg =>
        {
            Dispatcher.InvokeAsync(async () =>
            {
                // Device changes may affect link descriptions and switch list
                if (msg.SourcePanel == "devices" && msg.Operation != "Delete")
                {
                    // Links reference device names — refresh to pick up name changes
                    await VM.ReloadP2PLinksAsync();
                    await VM.ReloadB2BLinksAsync();
                }

                // Role/permission changes affect UI visibility
                if (msg.SourcePanel == "admin" && msg.EntityType.Contains("Role"))
                    ApplyRibbonPermissions();

                VM.StatusText = $"{msg.EntityType} {msg.Operation.ToLower()}d  ·  {DateTime.Now:HH:mm:ss}";
            });
        });
    }

    // ── Devices grid selection + row change tracking ─────────────────────────

    private async void DevicesGrid_CurrentItemChanged(object sender, CurrentItemChangedEventArgs e)
    {
        VM.SelectedDevice = e.NewItem as DeviceRecord;

        // Auto-resolve linked switch for details/backup/sync
        if (VM.SelectedDevice?.IsLinked == true)
        {
            var sw = await VM.Repo.GetSwitchByHostnameAsync(VM.SelectedDevice.LinkedHostname);
            VM.SelectedSwitch = sw;
        }
        else
        {
            VM.SelectedSwitch = null;
        }

        // Publish selection for cross-panel communication
        Central.Core.Shell.PanelMessageBus.Publish(
            new Central.Core.Shell.SelectionChangedMessage("devices", VM.SelectedDevice));

        // Always refresh detail tabs to match the selected row 1:1
        await LoadRunningConfigForSelectedSwitch();
        UpdateSwitchTabVisibility();
    }

    private async void SwitchGrid_CurrentItemChanged(object sender, CurrentItemChangedEventArgs e)
    {
        VM.SelectedSwitch = e.NewItem as SwitchRecord;

        Central.Core.Shell.PanelMessageBus.Publish(
            new Central.Core.Shell.SelectionChangedMessage("switches", VM.SelectedSwitch));

        await LoadRunningConfigForSelectedSwitch();
        UpdateSwitchTabVisibility();
    }

    private async Task LoadRunningConfigForSelectedSwitch()
    {
        await VM.LoadSwitchDetailAsync();
        SetConfigText(VM.RunningConfigText);
        DetailTabsPanel.SingleConfig.Visibility = Visibility.Visible;
        DetailTabsPanel.ConfigHeader.Text = "";
        UpdateVersionTab(VM.CurrentVersion);
        DetailTabsPanel.AuditLog.ItemsSource = VM.CurrentAuditLog;
        DetailTabsPanel.Backups.ItemsSource = VM.CurrentBackups;
        DetailTabsPanel.VersionsList.ItemsSource = VM.CurrentConfigVersions;
    }

    private void UpdateVersionTab(SwitchVersion? ver)
    {
        DetailTabsPanel.VerModel.Text     = ver?.HardwareModel ?? "—";
        DetailTabsPanel.VerMac.Text       = ver?.MacAddress ?? "—";
        DetailTabsPanel.VerL2L3.Text      = ver?.L2L3Version ?? "—";
        DetailTabsPanel.VerL2L3Date.Text  = ver?.L2L3Date ?? "—";
        DetailTabsPanel.VerLinux.Text     = ver?.LinuxVersion ?? "—";
        DetailTabsPanel.VerOvs.Text       = ver?.OvsVersion ?? "—";
        DetailTabsPanel.VerCapturedAt.Text = ver?.CapturedAt.ToString("yyyy-MM-dd HH:mm:ss") ?? "—";
        DetailTabsPanel.VerRawText.Text   = ver?.RawOutput ?? "";
    }

    // Admin/Lookup/Device handlers → Module UserControls

    // ── Config Sync (SSH download — business logic in MainViewModel) ──

    private async void SyncConfigButton_Click(object sender, RoutedEventArgs e)
    {
        // Determine which switch to sync
        SwitchRecord? sw = null;
        string? mgmtIp = null;
        if (VM.ActivePanel == ActivePanel.Switches && VM.SelectedSwitch != null)
            { sw = VM.SelectedSwitch; mgmtIp = sw.EffectiveSshIp; }
        else if (VM.ActivePanel == ActivePanel.Devices && VM.SelectedDevice != null)
        {
            var dev = VM.SelectedDevice;
            sw = VM.Switches.FirstOrDefault(s => s.Hostname == dev.SwitchName || s.Hostname == dev.LinkedHostname);
            mgmtIp = sw?.EffectiveSshIp ?? dev.ManagementIp;
        }
        if (sw == null) { DetailTabsPanel.SyncStatus.Text = "No switch selected"; return; }

        var candidateIps = VM.GetCandidateIps(sw, mgmtIp);
        if (candidateIps.Count == 0) { DetailTabsPanel.SyncStatus.Text = "No IPs available"; return; }

        var (username, password, port) = VM.ResolveSshCredentials(sw);

        // Prompt for password if none configured
        if (string.IsNullOrWhiteSpace(password))
        {
            var pwBox = new System.Windows.Controls.PasswordBox { Width = 200 };
            var dlg = new Window
            {
                Title = "SSH Password", Width = 300, Height = 140,
                WindowStartupLocation = WindowStartupLocation.CenterOwner, Owner = this,
                ResizeMode = ResizeMode.NoResize,
                Content = new System.Windows.Controls.StackPanel
                {
                    Margin = new Thickness(16),
                    Children =
                    {
                        new System.Windows.Controls.TextBlock { Text = $"Password for {username}@{sw.Hostname}:", Margin = new Thickness(0,0,0,8) },
                        pwBox,
                        new System.Windows.Controls.Button { Content = "Connect", IsDefault = true, Margin = new Thickness(0,8,0,0), HorizontalAlignment = System.Windows.HorizontalAlignment.Right }
                    }
                }
            };
            ((System.Windows.Controls.Button)((System.Windows.Controls.StackPanel)dlg.Content).Children[2]).Click += (_, _) => dlg.DialogResult = true;
            pwBox.Focus();
            if (dlg.ShowDialog() != true) return;
            password = pwBox.Password;
            if (string.IsNullOrWhiteSpace(password)) return;
        }

        DetailTabsPanel.SyncStatus.Text = $"Pinging {candidateIps.Count} candidate IPs...";
        DetailTabsPanel.SyncButton.IsEnabled = false;
        try
        {
            var reachableIp = await VM.FindReachableIpAsync(candidateIps);
            if (reachableIp == null)
            { DetailTabsPanel.SyncStatus.Text = $"All IPs unreachable"; DetailTabsPanel.SyncButton.IsEnabled = true; return; }

            DetailTabsPanel.SyncStatus.Text = $"Connecting to {username}@{reachableIp}:{port}...";
            var sshResult = await Services.SshService.DownloadConfigAsync(
                VM.Repo, sw.Id, sw.Hostname, reachableIp, port, username, password);

            if (!sshResult.Success)
            { DetailTabsPanel.SyncStatus.Text = $"Failed: {sshResult.Error}"; return; }
            if (string.IsNullOrWhiteSpace(sshResult.Config))
            { DetailTabsPanel.SyncStatus.Text = "No config lines returned"; return; }

            await VM.ProcessSyncResultAsync(sw, sshResult, ver => UpdateVersionTab(ver));
            SetConfigText(VM.RunningConfigText);
            DetailTabsPanel.SingleConfig.Visibility = Visibility.Visible;

            DetailTabsPanel.SyncStatus.Text = $"Synced {sshResult.Config.Split('\n').Length} lines  ·  {DateTime.Now:HH:mm:ss}";
            await RefreshAuditAndBackups();
        }
        catch (Exception ex)
        {
            AppLogger.LogException("SyncConfig", ex, "SyncConfigButton_Click");
            DetailTabsPanel.SyncStatus.Text = $"Error: {ex.Message}";
        }
        finally { DetailTabsPanel.SyncButton.IsEnabled = true; }
    }

    // ── SSH Override IP save ─────────────────────────────────────────────

    private async void SshOverrideIp_LostFocus(object sender, RoutedEventArgs e)
    {
        if (VM.SelectedSwitch is { } sw && sw.Id != Guid.Empty)
        {
            await VM.Repo.UpdateSshOverrideIpAsync(sw.Id, sw.SshOverrideIp ?? "");
            var op = AuthContext.Instance.CurrentUser?.DisplayName ?? Environment.UserName;
            await VM.Repo.AddAuditLogAsync(sw.Id, op, "Edit", "ssh_override_ip", null, sw.SshOverrideIp, "");
        }
    }

    // ── Backup & Restore ──────────────────────────────────────────────

    private async void BackupNowButton_Click(object sender, RoutedEventArgs e)
    {
        if (VM.SelectedSwitch is not { } sw || sw.Id == Guid.Empty)
        { DetailTabsPanel.BackupStatus.Text = "No switch selected"; return; }
        if (string.IsNullOrWhiteSpace(VM.RunningConfigText) || VM.RunningConfigText.StartsWith("("))
        { DetailTabsPanel.BackupStatus.Text = "No config to backup"; return; }

        await VM.BackupCurrentConfigAsync();
        DetailTabsPanel.BackupStatus.Text = VM.StatusText;
        DetailTabsPanel.AuditLog.ItemsSource = VM.CurrentAuditLog;
        DetailTabsPanel.Backups.ItemsSource = VM.CurrentBackups;
        DetailTabsPanel.VersionsList.ItemsSource = VM.CurrentConfigVersions;
    }

    private async void RestoreBackupButton_Click(object sender, RoutedEventArgs e)
    {
        if (VM.SelectedSwitch is not { } sw || sw.Id == Guid.Empty) return;
        if (DetailTabsPanel.Backups.SelectedItem is not BackupEntry backup)
        {
            DetailTabsPanel.BackupStatus.Text = "Select a backup to restore";
            return;
        }

        var result = System.Windows.MessageBox.Show(
            $"Restore config from {backup.CreatedAt}?\nThis will set it as the current running config.",
            "Restore Backup", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);
        if (result != MessageBoxResult.Yes) return;

        var op = AuthContext.Instance.CurrentUser?.DisplayName ?? Environment.UserName;
        await VM.Repo.RestoreConfigBackupAsync(sw.Id, backup.Id, op);

        // Reload the config display
        var cfg = await VM.Repo.GetLatestRunningConfigAsync(sw.Id);
        VM.RunningConfigText = cfg ?? "";
        SetConfigText(VM.RunningConfigText);
        DetailTabsPanel.BackupStatus.Text = $"Restored  ·  {DateTime.Now:HH:mm:ss}";
        await RefreshAuditAndBackups();
    }

    private async Task RefreshAuditAndBackups()
    {
        if (VM.SelectedSwitch is not { } sw || sw.Id == Guid.Empty) return;
        await VM.RefreshAuditAndBackupsAsync(sw.Id);
        DetailTabsPanel.AuditLog.ItemsSource = VM.CurrentAuditLog;
        DetailTabsPanel.Backups.ItemsSource = VM.CurrentBackups;
        DetailTabsPanel.VersionsList.ItemsSource = VM.CurrentConfigVersions;
    }

    private async void BackupDescription_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.TextBox tb && tb.DataContext is BackupEntry entry)
        {
            try { await VM.Repo.UpdateConfigBackupDescriptionAsync(entry.Id, entry.Description); }
            catch (Exception ex) { AppLogger.LogException("Backup", ex, "UpdateDescription"); }
        }
    }

    // Simple DTO for backup list binding
    private class BackupEntry
    {
        public int Id { get; set; }
        public string CreatedAt { get; set; } = "";
        public string Operator { get; set; } = "";
        public string Description { get; set; } = "";
        public int LineCount { get; set; }
    }

    // ── Config Version History ─────────────────────────────────────────

    // Config compare UI methods → ConfigCompareHelper.cs

    private async void DeleteConfigVersionButton_Click(object sender, RoutedEventArgs e)
    {
        if (VM.SelectedSwitch is not { } sw) return;
        var selected = DetailTabsPanel.VersionsList.SelectedItems
            .OfType<ConfigVersionEntry>().ToList();
        if (selected.Count == 0) { VM.StatusText = "Select version(s) to delete"; return; }

        var msg = selected.Count == 1
            ? $"Delete config v{selected[0].VersionNum} ({selected[0].DisplayDate})?"
            : $"Delete {selected.Count} config versions?";
        var result = System.Windows.MessageBox.Show(msg, "Delete Config Version",
            MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);
        if (result != MessageBoxResult.Yes) return;

        foreach (var ver in selected)
            await VM.Repo.DeleteConfigVersionAsync(ver.Id);

        var op = AuthContext.Instance.CurrentUser?.DisplayName ?? Environment.UserName;
        await VM.Repo.AddAuditLogAsync(sw.Id, op, "Delete Config", "config_version",
            string.Join(",", selected.Select(v => $"v{v.VersionNum}")), null, "");

        DetailTabsPanel.SyncStatus.Text = $"Deleted {selected.Count} version(s)  ·  {DateTime.Now:HH:mm:ss}";
        await RefreshConfigVersions();
        await RefreshAuditAndBackups();
    }

    private void SetConfigText(string configText)
    {
        var doc = ConfigCompareHelper.BuildNumberedConfigDoc(
            configText.Split('\n').Select(l => l.TrimEnd('\r')).ToArray());
        DetailTabsPanel.ConfigBox.Document = doc;
    }

    private async Task RefreshConfigVersions()
    {
        if (VM.SelectedSwitch == null) return;
        var versions = await VM.Repo.GetConfigVersionsAsync(VM.SelectedSwitch.Id);
        DetailTabsPanel.VersionsList.ItemsSource = versions;
    }

    private async void ConfigVersionsList_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (DetailTabsPanel.VersionsList.SelectedItem is not ConfigVersionEntry ver) return;
        try
        {
            var full = await VM.Repo.GetConfigVersionTextAsync(ver.Id);
            if (string.IsNullOrEmpty(full)) return;
            DetailTabsPanel.ConfigHeader.Text = $"v{ver.VersionNum}  ·  {ver.DisplayDate}  ·  {full.Split('\n').Length} lines";
            SetConfigText(full);
            DetailTabsPanel.SingleConfig.Visibility = Visibility.Visible;
        }
        catch (Exception ex) { AppLogger.LogException("Config", ex, "ConfigVersionDoubleClick"); }
    }

    private async void CompareConfigButton_Click(object sender, RoutedEventArgs e)
    {
        var selected = DetailTabsPanel.VersionsList.SelectedItems
            .OfType<ConfigVersionEntry>().OrderBy(v => v.VersionNum).ToList();
        if (selected.Count != 2) { VM.StatusText = "Select exactly 2 versions to compare"; return; }

        try
        {
            var olderText = await VM.Repo.GetConfigVersionTextAsync(selected[0].Id);
            var newerText = await VM.Repo.GetConfigVersionTextAsync(selected[1].Id);
            if (string.IsNullOrEmpty(olderText) || string.IsNullOrEmpty(newerText)) return;

            // Show in compare panel
            DockManager.DockController.Restore(ComparePanel);
            DockManager.Activate(ComparePanel);
            ConfigCompareHelper.BuildCompareView(CompareContent,
                olderText, newerText,
                selected[0].VersionNum, selected[1].VersionNum,
                selected[0].DisplayDate, selected[1].DisplayDate);
        }
        catch (Exception ex) { AppLogger.LogException("Compare", ex, "CompareConfigButton_Click"); }
    }

    // SSH Logs + App Log handlers → moved to Module.Admin UserControls

    // ── Settings grid (Config Ranges) ───────────────────────────────────

    private async void SettingsView_ValidateRow(object sender, GridRowValidationEventArgs e)
    {
        if (e.Row is not ConfigRange item) return;
        if (string.IsNullOrWhiteSpace(item.Category) || string.IsNullOrWhiteSpace(item.Name))
        {
            e.IsValid      = false;
            e.ErrorContent = "Category and Name are required.";
            return;
        }
        await VM.SaveConfigRangeAsync(item);
    }

    private void SettingsView_InvalidRowException(object sender, InvalidRowExceptionEventArgs e)
    {
        e.ExceptionMode = ExceptionMode.NoAction;
    }

    // ── Servers grid (NIC status edit) ──────────────────────────────────

    private async void ServersView_ValidateRow(object sender, GridRowValidationEventArgs e)
    {
        if (e.Row is not Server srv) return;
        await VM.SaveServerNicStatusAsync(srv);
    }

    private void ServersView_InvalidRowException(object sender, InvalidRowExceptionEventArgs e)
    {
        e.ExceptionMode = ExceptionMode.NoAction;
    }

    // VLANs ValidateRow → moved to Module.VLANs/Views/VlanGridPanel.xaml.cs

    // ── ASN definitions grid ─────────────────────────────────────────────

    private async void AsnView_ValidateRow(object sender, GridRowValidationEventArgs e)
    {
        if (e.Row is not AsnDefinition asn) return;
        await VM.Repo.UpsertAsnDefinitionAsync(asn);
        VM.StatusText = $"ASN saved: {asn.Asn}  ·  {DateTime.Now:HH:mm:ss}";
        Central.Core.Shell.PanelMessageBus.Publish(
            new Central.Core.Shell.DataModifiedMessage("devices", "ASN", "Update"));
    }

    private void AsnView_InvalidRowException(object sender, InvalidRowExceptionEventArgs e)
    {
        e.ExceptionMode = ExceptionMode.NoAction;
    }

    private async Task ToggleLiveDescriptionsAsync()
    {
        try
        {
            if (VM.ShowLiveDescriptions)
            {
                // Load live descriptions from switch_interfaces for each device in P2P links
                var deviceNames = VM.P2PLinks
                    .SelectMany(l => new[] { l.DeviceA, l.DeviceB })
                    .Where(d => !string.IsNullOrEmpty(d))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                // Batch load: device → { port → description } in one query
                var descCache = await VM.Repo.GetInterfaceDescriptionsBatchAsync(deviceNames);

                int mismatches = 0;
                foreach (var link in VM.P2PLinks)
                {
                    // First toggle — populate LiveDesc from switch
                    if (string.IsNullOrEmpty(link.LiveDescA) || string.IsNullOrEmpty(link.LiveDescB))
                    {
                        if (descCache.TryGetValue(link.DeviceA, out var descA) && !string.IsNullOrEmpty(link.PortA) && descA.TryGetValue(link.PortA, out var lda))
                            link.LiveDescA = lda;
                        if (descCache.TryGetValue(link.DeviceB, out var descB) && !string.IsNullOrEmpty(link.PortB) && descB.TryGetValue(link.PortB, out var ldb))
                            link.LiveDescB = ldb;
                    }

                    // Detect mismatches — compare planned vs live (case-insensitive, trim)
                    link.MismatchA = !string.IsNullOrEmpty(link.LiveDescA)
                        && !string.Equals(link.DescA.Trim(), link.LiveDescA.Trim(), StringComparison.OrdinalIgnoreCase);
                    link.MismatchB = !string.IsNullOrEmpty(link.LiveDescB)
                        && !string.Equals(link.DescB.Trim(), link.LiveDescB.Trim(), StringComparison.OrdinalIgnoreCase);
                    if (link.MismatchA) mismatches++;
                    if (link.MismatchB) mismatches++;

                    // Swap: show live in DescA/DescB, stash planned
                    (link.DescA, link.LiveDescA) = (link.LiveDescA, link.DescA);
                    (link.DescB, link.LiveDescB) = (link.LiveDescB, link.DescB);
                }
                VM.StatusText = mismatches > 0
                    ? $"Showing live descriptions — {mismatches} mismatches found  ·  {DateTime.Now:HH:mm:ss}"
                    : $"Showing live descriptions — all match  ·  {DateTime.Now:HH:mm:ss}";
            }
            else
            {
                // Swap back: restore planned descriptions, clear mismatch flags
                foreach (var link in VM.P2PLinks)
                {
                    (link.DescA, link.LiveDescA) = (link.LiveDescA, link.DescA);
                    (link.DescB, link.LiveDescB) = (link.LiveDescB, link.DescB);
                    link.MismatchA = false;
                    link.MismatchB = false;
                }
                VM.StatusText = $"Showing planned descriptions  ·  {DateTime.Now:HH:mm:ss}";
            }
        }
        catch (Exception ex)
        {
            AppLogger.LogException("P2P", ex, "ToggleLiveDescriptions");
        }
    }

    private void P2PView_ShownEditor(object sender, EditorEventArgs e)
    {
        if (_linkEditor == null) return;
        try
        {
            var link = P2PGridPanel.Grid.GetRow(P2PGridPanel.View.FocusedRowHandle) as P2PLink ?? P2PGridPanel.Grid.CurrentItem as P2PLink;
            var field = e.Column.FieldName;
            if (e.Editor is not DevExpress.Xpf.Editors.ComboBoxEdit combo) return;

            if (field == "DeviceA" || field == "DeviceB")
                _linkEditor.WireDeviceDropdown(combo, link?.Building);
            else if (field == "PortA" || field == "PortB")
                _linkEditor.WirePortDropdown(combo, field == "PortA" ? link?.DeviceA : link?.DeviceB);
        }
        catch (Exception ex) { AppLogger.LogException("P2P", ex, "P2PView_ShownEditor"); }
    }

    // Port dropdown, device filtering, natural sort, and item template moved to LinkEditorHelper
    // (see Services/LinkEditorHelper.cs)

    private static void Global_ComboBoxEdit_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != Key.Delete && e.Key != Key.Back) return;
        if (sender is not DevExpress.Xpf.Editors.ComboBoxEdit combo) return;

        var text = combo.Text?.Trim() ?? "";
        if (text.Length <= 1)
        {
            combo.EditValue = null;
            combo.Text = "";
            e.Handled = true;
        }
    }

    // ── B2B Dropdown + Config ───────────────────────────────────────────

    private void B2BView_ShownEditor(object sender, EditorEventArgs e)
    {
        if (_linkEditor == null) return;
        try
        {
            var link = B2BGridPanel.Grid.GetRow(B2BGridPanel.View.FocusedRowHandle) as B2BLink ?? B2BGridPanel.Grid.CurrentItem as B2BLink;
            var field = e.Column?.FieldName ?? "";
            if (e.Editor is not DevExpress.Xpf.Editors.ComboBoxEdit combo) return;

            if (field == "BuildingA" || field == "BuildingB")
                _linkEditor.WireBuildingDropdown(combo);
            else if (field == "DeviceA" || field == "DeviceB")
                _linkEditor.WireDeviceDropdown(combo, field == "DeviceA" ? link?.BuildingA : link?.BuildingB);
            else if (field == "PortA" || field == "PortB")
                _linkEditor.WirePortDropdown(combo, field == "PortA" ? link?.DeviceA : link?.DeviceB);
        }
        catch (Exception ex) { AppLogger.LogException("B2B", ex, "B2BView_ShownEditor"); }
    }

    // ── FW Dropdown + Config ────────────────────────────────────────────

    private void FWView_ShownEditor(object sender, EditorEventArgs e)
    {
        if (_linkEditor == null) return;
        try
        {
            var link = FWGridPanel.Grid.GetRow(FWGridPanel.View.FocusedRowHandle) as FWLink ?? FWGridPanel.Grid.CurrentItem as FWLink;
            var field = e.Column?.FieldName ?? "";
            if (e.Editor is not DevExpress.Xpf.Editors.ComboBoxEdit combo) return;

            if (field == "Building")
                _linkEditor.WireBuildingDropdown(combo);
            else if (field == "Switch" || field == "Firewall")
                _linkEditor.WireDeviceDropdown(combo, link?.Building);
            else if (field == "SwitchPort" || field == "FirewallPort")
                _linkEditor.WirePortDropdown(combo, field == "SwitchPort" ? link?.Switch : link?.Firewall);
        }
        catch (Exception ex) { AppLogger.LogException("FW", ex, "FWView_ShownEditor"); }
    }

    // P2P/B2B/FW ValidateRow → moved to module UserControls with SaveLink delegates

    private void AsnView_ShownEditor(object sender, EditorEventArgs e)
    {
        if (e.Column.FieldName == "BindDevice" && AsnGridPanel.Grid.CurrentItem is AsnDefinition asn)
        {
            // Bound devices first, then all others
            var bound = VM.Devices.Where(d => d.Asn == asn.Asn).ToList();
            var others = VM.Devices.Where(d => d.Asn != asn.Asn).OrderBy(d => d.SwitchName).ToList();
            var combined = new System.Collections.Generic.List<DeviceRecord>();
            combined.AddRange(bound);
            combined.AddRange(others);
            AsnGridPanel.BindComboSources(combined);
        }
    }

    private async void AsnView_CellValueChanged(object sender, CellValueChangedEventArgs e)
    {
        if (e.Column.FieldName == "BindDevice" && e.Value is string deviceName
            && !string.IsNullOrEmpty(deviceName) && AsnGridPanel.Grid.CurrentItem is AsnDefinition asn)
        {
            // Find the device and update its ASN
            var device = VM.Devices.FirstOrDefault(d => d.SwitchName == deviceName);
            if (device != null)
            {
                device.Asn = asn.Asn;
                await VM.SaveDeviceAsync(device);

                // Refresh ASN definitions to update bound device counts
                var defs = await VM.Repo.GetAsnDefinitionsAsync();
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    VM.AsnDefinitions.Clear();
                    foreach (var d in defs) VM.AsnDefinitions.Add(d);
                });
                VM.StatusText = $"Bound {deviceName} to ASN {asn.Asn}  ·  {DateTime.Now:HH:mm:ss}";
            }

            // Clear the unbound column so it's ready for another pick
            AsnGridPanel.Grid.SetCellValue(e.RowHandle, e.Column, null);
        }
    }

    private void AsnGrid_CurrentItemChanged(object sender, CurrentItemChangedEventArgs e)
    {
        VM.SelectedAsn = e.NewItem as AsnDefinition;
        // Populate the bound devices list in the details panel
        DetailTabsPanel.AsnDevices.Items.Clear();
        if (VM.SelectedAsn != null && !string.IsNullOrEmpty(VM.SelectedAsn.Devices))
        {
            foreach (var name in VM.SelectedAsn.Devices.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                DetailTabsPanel.AsnDevices.Items.Add(name);
        }
    }

    private void AsnDevicesList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (DetailTabsPanel.AsnDevices.SelectedItem is string deviceName)
        {
            var device = VM.Devices.FirstOrDefault(d => d.SwitchName == deviceName);
            if (device != null)
            {
                VM.SelectedDevice = device;
                VM.ActivePanel = ActivePanel.Devices;
                DockManager.Activate(DevicesPanel);
            }
            DetailTabsPanel.AsnDevices.SelectedItem = null; // reset so user can click same item again
        }
    }

    // ── Users grid ──────────────────────────────────────────────────────

    private void UsersGrid_CurrentItemChanged(object sender, CurrentItemChangedEventArgs e)
    {
        VM.SelectedUser = e.NewItem as AppUser;
    }

    // Users ValidateRow → Module.Admin/Views/UsersPanel.xaml.cs

    // ── Panel tracking ────────────────────────────────────────────────────

    // ── VLAN site filtering (uses left-hand Sites panel) ───────────────

    /// <summary>Reload VLANs grid based on current site selections + Default toggle.</summary>
    private async Task RefreshVlanSiteDataAsync()
    {
        await VM.LoadPanelDataAsync("vlans", force: true);
        ForceVlanSort();
    }

    // ── Link Config Cog (P2P / B2B / FW) ───────────────────────────────

    private void LinkConfigCog_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button btn) return;
        var side = btn.Tag as string ?? "A";

        // Walk up to find the RowData context
        string configText = "";
        string deviceName = "";
        string linkLabel = "";

        var fe = btn as FrameworkElement;
        while (fe != null)
        {
            if (fe.DataContext is DevExpress.Xpf.Grid.EditGridCellData cellData)
            {
                var row = cellData.RowData?.Row;
                if (row is P2PLink p2p)
                {
                    configText = side == "A" ? p2p.ConfigA : p2p.ConfigB;
                    deviceName = side == "A" ? p2p.DeviceA : p2p.DeviceB;
                    linkLabel = $"P2P VL{p2p.Vlan}: {p2p.DeviceA} \u2194 {p2p.DeviceB}";
                }
                else if (row is B2BLink b2b)
                {
                    // Set PeerAsn for BGP neighbor generation
                    var peerName = side == "A" ? b2b.DeviceB : b2b.DeviceA;
                    var peerDev = VM.Devices.FirstOrDefault(d =>
                        string.Equals(d.SwitchName, peerName, StringComparison.OrdinalIgnoreCase));
                    b2b.PeerAsn = peerDev?.Asn ?? "";

                    configText = side == "A" ? b2b.ConfigA : b2b.ConfigB;
                    deviceName = side == "A" ? b2b.DeviceA : b2b.DeviceB;
                    linkLabel = $"B2B VL{b2b.Vlan}: {b2b.DeviceA} \u2194 {b2b.DeviceB}";
                }
                else if (row is FWLink fw)
                {
                    configText = side == "A" ? fw.ConfigA : fw.ConfigB;
                    deviceName = side == "A" ? fw.Switch : fw.Firewall;
                    linkLabel = $"FW VL{fw.Vlan}: {fw.Switch} \u2194 {fw.Firewall}";
                }
                break;
            }
            fe = System.Windows.Media.VisualTreeHelper.GetParent(fe) as FrameworkElement;
        }

        if (string.IsNullOrEmpty(configText))
        {
            VM.StatusText = "No config to display — check VLAN is set";
            return;
        }

        // Show in deploy panel
        DeployGridPanel.TabAHeader = deviceName;
        DeployGridPanel.TabBHeader = "—";
        DeployGridPanel.ConfigA = configText;
        DeployGridPanel.ConfigB = "";
        DeployGridPanel.LogText = "";
        DeployGridPanel.HeaderText = $"{linkLabel}  —  {deviceName} (Side {side})";
        DeployGridPanel.StatusText = "Review config below — copy or deploy";
        DeployGridPanel.ConfirmEnabled = false; // just viewing, not deploying
        // Select first tab (Switch A)

        DockManager.DockController.Restore(DeployPanel);
        DockManager.Activate(DeployPanel);
    }

    // ── P2P Deploy to Switch ────────────────────────────────────────────

    private P2PLink? _deployLink;
    private List<string> _deployCommandsA = new();
    private List<string> _deployCommandsB = new();

    private static List<string> BuildP2PCommands(P2PLink link, bool sideA) =>
        DeployService.BuildP2PCommands(link, sideA);


    private void SendToSwitchButton_Click(object sender, ItemClickEventArgs e)
    {
        // Only works from P2P panel
        if (P2PGridPanel.Grid.View is not TableView tv) return;
        var link = P2PGridPanel.Grid.GetRow(tv.FocusedRowHandle) as P2PLink;
        if (link == null)
        {
            VM.StatusText = "Select a P2P link row first";
            return;
        }

        if (string.IsNullOrEmpty(link.DeviceA) || string.IsNullOrEmpty(link.DeviceB) || string.IsNullOrEmpty(link.Vlan))
        {
            VM.StatusText = "P2P link must have Device A, Device B, and VLAN set";
            return;
        }

        _deployLink = link;
        _deployCommandsA = BuildP2PCommands(link, sideA: true);
        _deployCommandsB = BuildP2PCommands(link, sideA: false);

        // Populate preview
        DeployGridPanel.TabAHeader = link.DeviceA;
        DeployGridPanel.TabBHeader = link.DeviceB;
        DeployGridPanel.ConfigA = string.Join("\n", _deployCommandsA);
        DeployGridPanel.ConfigB = string.Join("\n", _deployCommandsB);
        DeployGridPanel.LogText = "";
        DeployGridPanel.HeaderText = $"P2P Link: {link.DeviceA} \u2194 {link.DeviceB}  (VLAN {link.Vlan})";
        DeployGridPanel.StatusText = "Review config below, then Confirm & Deploy";
        DeployGridPanel.ConfirmEnabled = true;

        // Show the deploy panel
        DockManager.DockController.Restore(DeployPanel);
        DockManager.Activate(DeployPanel);
    }

    private void DeployCancelButton_Click(object sender, RoutedEventArgs e)
    {
        _deployLink = null;
        DockManager.DockController.Close(DeployPanel);
    }

    private async void DeployConfirmButton_Click(object sender, RoutedEventArgs e)
    {
        if (_deployLink == null) return;
        var link = _deployLink;
        DeployGridPanel.ConfirmEnabled = false;
        DeployGridPanel.StatusText = "Deploying...";
        var logLines = new List<string>();

        void AppendLog(string msg)
        {
            logLines.Add($"[{DateTime.Now:HH:mm:ss}] {msg}");
            DeployGridPanel.LogText = string.Join("\n", logLines);
        }

        // Resolve credentials (same pattern as SyncConfigButton_Click)
        var defaultUser = VM.GetSshDefault("Default SSH Username");
        var defaultPass = VM.GetSshDefault("Default SSH Password");
        var defaultPort = int.TryParse(VM.GetSshDefault("Default SSH Port"), out var dp) ? dp : 22;

        async Task<bool> DeployToDevice(string deviceName, List<string> commands, string label)
        {
            AppendLog($"── {label}: {deviceName} ──");

            // Find switch record
            var sw = VM.Switches.FirstOrDefault(s =>
                string.Equals(s.Hostname, deviceName, StringComparison.OrdinalIgnoreCase));
            string? ip = null;

            if (sw != null)
            {
                ip = sw.EffectiveSshIp?.Split('/')[0];
            }

            // Fallback: look up from devices
            if (string.IsNullOrEmpty(ip))
            {
                var dev = VM.Devices.FirstOrDefault(d =>
                    string.Equals(d.SwitchName, deviceName, StringComparison.OrdinalIgnoreCase));
                ip = dev?.ManagementIp?.Split('/')[0];
                if (string.IsNullOrEmpty(ip))
                    ip = dev?.Ip?.Split('/')[0];
            }

            if (string.IsNullOrEmpty(ip))
            {
                AppendLog($"  ERROR: No IP found for {deviceName}");
                return false;
            }

            var username = (sw != null && !string.IsNullOrWhiteSpace(sw.SshUsername)) ? sw.SshUsername
                         : !string.IsNullOrWhiteSpace(defaultUser) ? defaultUser : "admin";
            var port = (sw != null && sw.SshPort > 0) ? sw.SshPort : defaultPort;
            var password = (sw != null && !string.IsNullOrWhiteSpace(sw.SshPassword)) ? sw.SshPassword : defaultPass;

            if (string.IsNullOrWhiteSpace(password))
            {
                AppendLog($"  ERROR: No SSH password configured for {deviceName}");
                return false;
            }

            AppendLog($"  Connecting to {ip}:{port} as {username}...");
            var result = await SshService.SendCommandsAsync(
                VM.Repo, sw?.Id != Guid.Empty ? sw?.Id : null,
                deviceName, ip, port, username, password, commands);

            if (result.Success)
            {
                AppendLog($"  SUCCESS: {commands.Count} commands sent and committed");
                AppLogger.Audit("Deploy", $"P2P deployed to {deviceName} — VLAN {link.Vlan}, {commands.Count} commands",
                    string.Join("\n", commands), "DeployConfirmButton_Click");
            }
            else
            {
                AppendLog($"  FAILED: {result.Error}");
            }

            if (!string.IsNullOrEmpty(result.LogEntries))
                AppendLog(result.LogEntries);

            return result.Success;
        }

        // Deploy to both switches
        DeployGridPanel.SelectLogTab();
        var okA = await DeployToDevice(link.DeviceA, _deployCommandsA, "Switch A");
        var okB = await DeployToDevice(link.DeviceB, _deployCommandsB, "Switch B");

        DeployGridPanel.StatusText = (okA && okB) ? "Both switches deployed successfully"
                              : (okA || okB) ? "Partial deploy — check log"
                              : "Deploy failed — check log";
        _deployLink = null;
        VM.StatusText = $"P2P deploy: {link.DeviceA} {(okA ? "OK" : "FAIL")} / {link.DeviceB} {(okB ? "OK" : "FAIL")}  ·  {DateTime.Now:HH:mm:ss}";
    }

    // ── Settings Cog (top-right of ribbon) ─────────────────────────────

    private BarSubItem? _cogButton;

    /// <summary>Create a simple colored circle as an ImageSource for the glyph.</summary>
    private static System.Windows.Media.ImageSource CreateCircleGlyph(System.Windows.Media.Color color)
    {
        var visual = new System.Windows.Media.DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawEllipse(new System.Windows.Media.SolidColorBrush(color), null,
                new System.Windows.Point(8, 8), 7, 7);
        }
        var bmp = new System.Windows.Media.Imaging.RenderTargetBitmap(16, 16, 96, 96,
            System.Windows.Media.PixelFormats.Pbgra32);
        bmp.Render(visual);
        bmp.Freeze();
        return bmp;
    }

    private static readonly System.Windows.Media.ImageSource _glyphRed    = CreateCircleGlyph(System.Windows.Media.Colors.Red);
    private static readonly System.Windows.Media.ImageSource _glyphYellow = CreateCircleGlyph(System.Windows.Media.Colors.Orange);
    private static readonly System.Windows.Media.ImageSource _glyphGreen  = CreateCircleGlyph(System.Windows.Media.Colors.LimeGreen);

    private void SetupSettingsCog()
    {
        var displayName = AuthContext.Instance.CurrentUser?.DisplayName ?? Environment.UserName;

        var saveItem = new BarButtonItem { Content = "Save Layout" };
        saveItem.ItemClick += SaveLayoutButton_ItemClick;

        var restoreItem = new BarButtonItem { Content = "Restore Default" };
        restoreItem.ItemClick += RestoreDefaultButton_ItemClick;

        _cogButton = new BarSubItem
        {
            Content = "\u2699",  // ⚙
            Glyph = _glyphRed,
            ToolTip = "Not connected",
            BarItemDisplayMode = BarItemDisplayMode.ContentAndGlyph
        };

        var userHeader = new BarStaticItem
        {
            Content = $"  {displayName}",
            ContentTemplate = CreateLargeTextTemplate(12)
        };
        var tabCloseToggle = new BarCheckItem
        {
            Content = "Shared Close Button",
            IsChecked = false  // default: unchecked = per-tab close buttons
        };
        tabCloseToggle.CheckedChanged += (_, _) =>
        {
            MainDocumentGroup.ClosePageButtonShowMode = tabCloseToggle.IsChecked == true
                ? DevExpress.Xpf.Docking.ClosePageButtonShowMode.InActiveTabPageHeader
                : DevExpress.Xpf.Docking.ClosePageButtonShowMode.InAllTabPageHeaders;
            _ = SaveUserPreferenceAsync("tab_close_shared", tabCloseToggle.IsChecked == true ? "1" : "0");
        };
        // Load saved preference — capture repo on UI thread, then run async
        var repo = VM.Repo;
        var uid = AuthContext.Instance.CurrentUser?.Id ?? 0;
        _ = Task.Run(async () =>
        {
            var val = await repo.GetUserSettingAsync(uid, "tab_close_shared");
            Dispatcher.Invoke(() =>
            {
                if (val == "1")
                {
                    tabCloseToggle.IsChecked = true;
                    MainDocumentGroup.ClosePageButtonShowMode = DevExpress.Xpf.Docking.ClosePageButtonShowMode.InActiveTabPageHeader;
                }
            });
        });

        _cogButton.ItemLinks.Add(userHeader);
        _cogButton.ItemLinks.Add(new BarItemLinkSeparator());
        _cogButton.ItemLinks.Add(saveItem);
        _cogButton.ItemLinks.Add(restoreItem);
        _cogButton.ItemLinks.Add(new BarItemLinkSeparator());
        _cogButton.ItemLinks.Add(tabCloseToggle);

        // User info in ribbon header
        var user = AuthContext.Instance.CurrentUser;
        if (user != null)
        {
            var userItem = new BarStaticItem
            {
                Content = $"{user.DisplayName}  ({user.RoleName})",
                BarItemDisplayMode = BarItemDisplayMode.Content
            };
            Ribbon.PageHeaderItemLinks.Add(userItem);
        }

        Ribbon.PageHeaderItemLinks.Add(_cogButton);

        VM.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.DbStatus))
                UpdateCogGlyph();
            if (e.PropertyName == nameof(MainViewModel.DbStatusTooltip) && _cogButton != null)
                _cogButton.ToolTip = VM.DbStatusTooltip;
        };
    }

    private void UpdateCogGlyph()
    {
        if (_cogButton == null) return;
        _cogButton.Glyph = VM.DbStatus switch
        {
            "Green"  => _glyphGreen,
            "Yellow" => _glyphYellow,
            _        => _glyphRed
        };
        _cogButton.ToolTip = VM.DbStatusTooltip;
    }

    private static System.Windows.DataTemplate CreateLargeTextTemplate(double fontSize)
    {
        var template = new System.Windows.DataTemplate();
        var factory = new System.Windows.FrameworkElementFactory(typeof(System.Windows.Controls.TextBlock));
        factory.SetBinding(System.Windows.Controls.TextBlock.TextProperty,
            new System.Windows.Data.Binding());
        factory.SetValue(System.Windows.Controls.TextBlock.FontSizeProperty, fontSize);
        factory.SetValue(System.Windows.Controls.TextBlock.VerticalAlignmentProperty,
            System.Windows.VerticalAlignment.Center);
        template.VisualTree = factory;
        return template;
    }

    // ── Theme Gallery (disabled — XAML commented out for debugging) ──

    /// <summary>Populate the theme gallery dropdown with all available DevExpress themes, grouped by category.</summary>
    private async Task CheckApiHealthAsync(string apiUrl)
    {
        if (string.IsNullOrEmpty(apiUrl) || apiUrl == "Not configured") return;
        try
        {
            using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(3) };
            var resp = await http.GetAsync($"{apiUrl.TrimEnd('/')}/health");
            BackstageConnStatus.Text = resp.IsSuccessStatusCode
                ? $"DB: {(App.IsDbOnline ? "Connected" : "Offline")} · API: Online"
                : $"DB: {(App.IsDbOnline ? "Connected" : "Offline")} · API: Error ({(int)resp.StatusCode})";
        }
        catch
        {
            BackstageConnStatus.Text = $"DB: {(App.IsDbOnline ? "Connected" : "Offline")} · API: Unreachable";
        }
    }

    // ── Backstage Settings Panel ────────────────────────────────────────

    private void PopulateSettingsPanel()
    {
        if (App.Settings == null) return;
        var panel = BackstageSettingsPanel;

        // Remove dynamic children (keep the header TextBlocks)
        while (panel.Children.Count > 2)
            panel.Children.RemoveAt(panel.Children.Count - 1);

        var defs = App.Settings.GetDefinitions();
        string? currentCategory = null;

        foreach (var def in defs)
        {
            // Category header
            if (def.Category != currentCategory)
            {
                currentCategory = def.Category;
                var header = new System.Windows.Controls.TextBlock
                {
                    Text = currentCategory.ToUpper(),
                    FontWeight = FontWeights.SemiBold,
                    FontSize = 11,
                    Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(156, 163, 175)),
                    Margin = new Thickness(0, 16, 0, 8)
                };
                panel.Children.Add(header);
            }

            // Setting row
            var row = new System.Windows.Controls.StackPanel { Orientation = System.Windows.Controls.Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
            var label = new System.Windows.Controls.TextBlock { Text = def.DisplayName, Width = 200, VerticalAlignment = VerticalAlignment.Center };
            row.Children.Add(label);

            switch (def.Type)
            {
                case Central.Core.Services.SettingType.Boolean:
                    var cb = new System.Windows.Controls.CheckBox { IsChecked = def.CurrentValue is true or "True" or "true" };
                    var defKey1 = def.Key;
                    cb.Checked += async (_, _) => { if (App.Settings != null) await App.Settings.SetAsync(defKey1, true); };
                    cb.Unchecked += async (_, _) => { if (App.Settings != null) await App.Settings.SetAsync(defKey1, false); };
                    row.Children.Add(cb);
                    break;

                case Central.Core.Services.SettingType.Integer:
                    var intBox = new System.Windows.Controls.TextBox
                    {
                        Text = def.CurrentValue?.ToString() ?? "",
                        Width = 100, Height = 24
                    };
                    var defKey2 = def.Key;
                    intBox.LostFocus += async (_, _) =>
                    {
                        if (int.TryParse(intBox.Text, out var val) && App.Settings != null)
                            await App.Settings.SetAsync(defKey2, val);
                    };
                    row.Children.Add(intBox);
                    break;

                default:
                    var textBox = new System.Windows.Controls.TextBox
                    {
                        Text = def.CurrentValue?.ToString() ?? "",
                        Width = 200, Height = 24
                    };
                    var defKey3 = def.Key;
                    textBox.LostFocus += async (_, _) =>
                    {
                        if (App.Settings != null) await App.Settings.SetAsync(defKey3, textBox.Text);
                    };
                    row.Children.Add(textBox);
                    break;
            }

            if (!string.IsNullOrEmpty(def.Description))
            {
                var desc = new System.Windows.Controls.TextBlock
                {
                    Text = def.Description,
                    FontSize = 10,
                    Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(107, 114, 128)),
                    Margin = new Thickness(8, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                row.Children.Add(desc);
            }

            panel.Children.Add(row);
        }
    }

    // ── Role-based ribbon + panel visibility ─────────────────────────────

    /// <summary>Hide ribbon tabs, groups, and buttons based on the current user's permissions.</summary>
    private void ApplyRibbonPermissions()
    {
        var auth = AuthContext.Instance;

        // Static tabs — gated by module permission
        AdminRibbonTab.IsVisible = auth.HasPermission("admin:users") || auth.HasPermission("admin:roles");

        // Map tab names to required permissions
        var tabPermissions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["DevicesRibbonTab"]  = "devices:read",
            ["SwitchesRibbonTab"] = "switches:read",
        };
        foreach (var (tabName, perm) in tabPermissions)
            if (FindName(tabName) is DevExpress.Xpf.Ribbon.RibbonPage tab)
                tab.IsVisible = auth.HasPermission(perm);

        // Dynamic module tabs — visibility driven by page.RequiredPermission
        foreach (var page in App.RibbonBuilder.Pages)
        {
            if (string.IsNullOrEmpty(page.RequiredPermission)) continue;
            var tab = DefaultRibbonCategory.Pages.OfType<DevExpress.Xpf.Ribbon.RibbonPage>()
                .FirstOrDefault(p => string.Equals(p.Caption?.ToString(), page.Header, StringComparison.OrdinalIgnoreCase));
            if (tab != null)
                tab.IsVisible = auth.HasPermission(page.RequiredPermission);
        }

        // Named button permissions
        var buttonPermissions = new Dictionary<string, string>
        {
            ["PingAllButton"]      = "switches:ping",
            ["PingSelectedButton"] = "switches:ping",
            ["SyncBgpButton"]      = "bgp:sync",
            ["SyncAllBgpButton"]   = "bgp:sync",
            ["NewDeviceButton"]    = "devices:write",
            ["DeleteDeviceButton"] = "devices:delete",
            ["ExportButton"]       = "devices:export",
        };
        foreach (var (btnName, perm) in buttonPermissions)
            if (FindName(btnName) is BarItem btn)
                btn.IsVisible = auth.HasPermission(perm);
    }

    /// <summary>Rebuild dynamic ribbon items after permission or role change.</summary>
    private async void RefreshRibbon()
    {
        // Remove all dynamically-added pages (keep static ones)
        var staticPageCaptions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "Home", "Devices", "Switches", "Builder", "Admin" };

        var toRemove = DefaultRibbonCategory.Pages.OfType<DevExpress.Xpf.Ribbon.RibbonPage>()
            .Where(p => !staticPageCaptions.Contains(p.Caption?.ToString() ?? ""))
            .ToList();
        foreach (var page in toRemove)
            DefaultRibbonCategory.Pages.Remove(page);

        // Also remove dynamically-added groups from static pages
        foreach (var staticPage in DefaultRibbonCategory.Pages.OfType<DevExpress.Xpf.Ribbon.RibbonPage>())
        {
            var dynamicGroups = staticPage.Groups.OfType<DevExpress.Xpf.Ribbon.RibbonPageGroup>()
                .Where(g => g.Tag as string == "dynamic")
                .ToList();
            foreach (var g in dynamicGroups)
                staticPage.Groups.Remove(g);
        }

        // Re-read all 3 override layers from DB (admin defaults + items + user overrides)
        await PreloadIconOverridesAsync();

        // Re-wire from module registrations (uses preloaded overrides for correct icons)
        WireModuleRibbon();

        // Clear SVG/PNG cache so changed icons render fresh
        Services.SvgHelper.ClearCache();
        _iconCache.Clear();

        // Apply DB icon overrides to static XAML buttons
        await ApplyDbRibbonOverridesAsync();

        ApplyRibbonPermissions();
    }

    // ── Backstage / Theme / User Profile ────────────────────────────────

    private void PopulateBackstage()
    {
        Services.BackstageHelper.PopulateUserProfile(
            BackstageDisplayName, BackstageUsername, BackstageRole, BackstageEmail,
            BackstageInitials, BackstageLoginType, BackstagePermCount, BackstageSites);

        Services.BackstageHelper.PopulateConnectionInfo(
            BackstageConnMode, BackstageConnDb, BackstageConnStatus, BackstageApiUrl);

        var apiUrl = BackstageApiUrl.Text;

        // Check API health async
        _ = CheckApiHealthAsync(apiUrl);

        // Mode toggle buttons
        BackstageModeStatus.Text = $"Current mode: {App.Connectivity?.Mode}";
        ModeDirectDbButton.Click += (_, _) =>
        {
            if (App.Connectivity != null)
            {
                App.Connectivity.SwitchMode(Central.Core.Data.DataServiceMode.DirectDb);
                BackstageModeStatus.Text = "Switched to Direct DB mode";
                BackstageConnMode.Text = "DirectDb";
            }
        };
        ModeApiButton.Click += async (_, _) =>
        {
            var url = App.Settings?.Get<string>("api.url") ?? "http://192.168.56.203:8000";
            if (App.Connectivity != null)
            {
                App.Connectivity.ApiUrl = url;
                BackstageModeStatus.Text = $"Connecting to API at {url}...";
                BackstageConnMode.Text = "Api";

                try
                {
                    var apiClient = new Central.Api.Client.CentralApiClient(url);
                    var login = await apiClient.LoginAsync(AuthContext.Instance.CurrentUser?.Username ?? Environment.UserName);
                    if (login != null)
                    {
                        // Register API data service and switch mode
                        App.Connectivity.RegisterApi(new Central.Api.Client.ApiDataService(apiClient));
                        App.Connectivity.SwitchMode(Central.Core.Data.DataServiceMode.Api);

                        await App.Connectivity.ConnectSignalRAsync($"{url.TrimEnd('/')}/hubs/notify", login.Token);
                        BackstageModeStatus.Text = $"API mode active + SignalR connected ({login.Role})";
                    }
                }
                catch (Exception ex) { BackstageModeStatus.Text = $"API mode — SignalR failed: {ex.Message}"; }
            }
        };
        ModeTestApiButton.Click += async (_, _) =>
        {
            var url = App.Settings?.Get<string>("api.url") ?? "http://192.168.56.203:8000";
            BackstageModeStatus.Text = "Testing...";
            try
            {
                using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(3) };
                var resp = await http.GetAsync($"{url.TrimEnd('/')}/health");
                if (resp.IsSuccessStatusCode)
                {
                    // Test login
                    var apiClient = new Central.Api.Client.CentralApiClient(url);
                    var login = await apiClient.LoginAsync(AuthContext.Instance.CurrentUser?.Username ?? Environment.UserName);
                    BackstageModeStatus.Text = login != null
                        ? $"API OK — {login.Role} ({login.Permissions?.Length ?? 0} permissions)"
                        : $"API reachable but login failed for {Environment.UserName}";
                }
                else
                    BackstageModeStatus.Text = $"API returned {(int)resp.StatusCode}";
            }
            catch (Exception ex) { BackstageModeStatus.Text = $"Failed: {ex.Message}"; }
        };

        // Bind recent activity to notification service
        var notifSvc = Central.Core.Services.NotificationService.Instance;
        BackstageRecentActivity.ItemsSource = notifSvc.Recent.Select(n => new
        {
            n.Icon,
            Time = n.Timestamp.ToString("HH:mm:ss"),
            n.Title
        }).ToList();
        // Update when new notifications arrive
        notifSvc.NotificationReceived += _ =>
        {
            Dispatcher.InvokeAsync(() =>
            {
                BackstageRecentActivity.ItemsSource = notifSvc.Recent.Take(15).Select(n => new
                {
                    n.Icon,
                    Time = n.Timestamp.ToString("HH:mm:ss"),
                    n.Title
                }).ToList();
            });
        };

        // Wire backstage buttons
        BackstageLogout.Click += BackstageLogout_Click;
        BackstageExit.Click += (object? _, EventArgs _2) => Close();

        // Settings buttons
        BackstageSaveLayout.Click += async (_, _) => { await SaveAllLayoutAsync(); VM.StatusText = "Layout saved"; };
        BackstageResetLayout.Click += async (_, _) =>
        {
            if (_layout != null) { await _layout.ClearAllLayoutsAsync(); VM.StatusText = "Layout reset — restart to apply"; }
        };
        BackstageCloseAllPanels.Click += (_, _) =>
        {
            VM.IsDevicesPanelOpen = false; VM.IsSwitchesPanelOpen = false;
            VM.IsRolesPanelOpen = false; VM.IsUsersPanelOpen = false;
            VM.IsLookupsPanelOpen = false; VM.IsSettingsPanelOpen = false;
            VM.IsMasterPanelOpen = false; VM.IsAsnPanelOpen = false;
            VM.IsP2PPanelOpen = false; VM.IsB2BPanelOpen = false;
            VM.IsFWPanelOpen = false; VM.IsVlansPanelOpen = false;
            VM.IsBgpPanelOpen = false; VM.IsJobsPanelOpen = false; VM.IsDetailsPanelOpen = false;
            VM.IsTasksPanelOpen = false; VM.IsBacklogPanelOpen = false; VM.IsSprintPlanPanelOpen = false;
            VM.IsBurndownPanelOpen = false; VM.IsKanbanPanelOpen = false; VM.IsGanttPanelOpen = false;
            VM.IsQAPanelOpen = false; VM.IsQADashboardPanelOpen = false; VM.IsReportBuilderPanelOpen = false;
            VM.IsTaskDashboardPanelOpen = false; VM.IsTimesheetPanelOpen = false; VM.IsActivityFeedPanelOpen = false;
            VM.IsMyTasksPanelOpen = false; VM.IsPortfolioPanelOpen = false; VM.IsTaskImportPanelOpen = false; VM.IsTaskDetailPanelOpen = false;
            VM.IsRibbonConfigPanelOpen = false; VM.IsIntegrationsPanelOpen = false; VM.IsServiceDeskPanelOpen = false; VM.IsSdOverviewPanelOpen = false; VM.IsSdClosuresPanelOpen = false; VM.IsSdAgingPanelOpen = false; VM.IsSdTeamsPanelOpen = false; VM.IsSdGroupsPanelOpen = false; VM.IsSdTechniciansPanelOpen = false; VM.IsSdRequestersPanelOpen = false;
            VM.StatusText = "All panels closed";
        };
        BackstageOpenDefaultPanels.Click += (_, _) =>
        {
            VM.IsDevicesPanelOpen = true; VM.IsSwitchesPanelOpen = true;
            VM.StatusText = "Default panels opened";
        };
        BackstageRefreshData.Click += async (_, _) =>
        {
            await VM.LoadAllAsync(); BindComboSources(); VM.StatusText = "Data refreshed";
        };
    }

    private void BackstageLogout_Click(object? sender, EventArgs e)
    {
        // Switch user — show login dialog, keep app open if cancelled
        var login = new LoginWindow(App.Dsn);
        var result = login.ShowDialog();
        if (result == true && login.LoginSucceeded)
        {
            // Reload data with new user's permissions
            _ = VM.LoadAllAsync();
            PopulateBackstage();
            ApplyRibbonPermissions();
        }
        // If cancelled, just return to app — don't close
    }

    private void PopulateThemeGallery()
    {
        Services.BackstageHelper.PopulateGalleryControl(ThemeGalleryItem.Gallery ??= new DevExpress.Xpf.Bars.Gallery());
        Services.BackstageHelper.PopulateGalleryControl(BackstageThemeGallery.Gallery);

        ThemeGalleryItem.Gallery.ItemClick -= ThemeGallery_ItemClick;
        ThemeGalleryItem.Gallery.ItemClick += ThemeGallery_ItemClick;
        BackstageThemeGallery.Gallery.ItemClick -= ThemeGallery_ItemClick;
        BackstageThemeGallery.Gallery.ItemClick += ThemeGallery_ItemClick;
    }

    private async void ThemeGallery_ItemClick(object sender, DevExpress.Xpf.Bars.GalleryItemEventArgs e)
    {
        var themeName = e.Item?.Tag as string;
        if (string.IsNullOrEmpty(themeName)) return;
        ApplyTheme(themeName);
    }

    private async void ApplyTheme(string themeName)
    {
        if (string.IsNullOrEmpty(themeName)) return;
        if (themeName == DevExpress.Xpf.Core.ThemeManager.ApplicationThemeName) return;

        try
        {
            DevExpress.Xpf.Core.ThemeManager.ApplicationThemeName = themeName;
        }
        catch (Exception ex)
        {
            VM.StatusText = $"Theme '{themeName}' not available: {ex.InnerException?.Message ?? ex.Message}";
            AppLogger.LogException("Theme", ex, $"ApplyTheme:{themeName}");
            return;
        }

        Services.BackstageHelper.UpdateThemeCheckMarks(
            new[] { ThemeGalleryItem.Gallery, BackstageThemeGallery.Gallery }, themeName);

        // Persist
        if (_layout != null)
            await _layout.SavePreferenceAsync(PreferenceKeys.Theme, themeName);
    }

    // ── Layout Save / Restore ─────────────────────────────────────────

    private async Task SaveUserPreferenceAsync(string key, string value)
    {
        try
        {
            var uid = AuthContext.Instance.CurrentUser?.Id ?? 0;
            if (uid != 0)
                await VM.Repo.SaveUserSettingAsync(uid, key, value);
        }
        catch (Exception ex) { AppLogger.LogException("Settings", ex, "SaveUserPreferenceAsync"); }
    }

    private async void SaveLayoutButton_ItemClick(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)
    {
        await SaveAllLayoutAsync();
        VM.StatusText = $"Layout saved  ·  {DateTime.Now:HH:mm:ss}";
    }

    private async void RestoreDefaultButton_ItemClick(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)
    {
        if (_layout == null) return;
        var result = System.Windows.MessageBox.Show(
            "Reset all grids, panels, filters, and window to defaults?\n\nThe app will restart.",
            "Restore Default", MessageBoxButton.YesNo, MessageBoxImage.Question,
            MessageBoxResult.No);
        if (result != MessageBoxResult.Yes) return;

        await _layout.ClearAllLayoutsAsync();

        // Restart the application — it will load with clean defaults
        var exe = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
        if (exe != null) System.Diagnostics.Process.Start(exe);
        System.Windows.Application.Current.Shutdown();
    }

    private async Task SavePanelStatesAsync()
    {
        if (_layout == null) return;
        var states = new System.Collections.Generic.Dictionary<string, bool>
        {
            ["devices"]     = VM.IsDevicesPanelOpen,
            ["switches"] = VM.IsSwitchesPanelOpen,
            ["roles"]    = VM.IsRolesPanelOpen,
            ["users"]    = VM.IsUsersPanelOpen,
            ["lookups"]  = VM.IsLookupsPanelOpen,
            ["settings"] = VM.IsSettingsPanelOpen,
            ["master"]   = VM.IsMasterPanelOpen,
            ["asn"]      = VM.IsAsnPanelOpen,
            ["p2p"]      = VM.IsP2PPanelOpen,
            ["b2b"]      = VM.IsB2BPanelOpen,
            ["fw"]       = VM.IsFWPanelOpen,
            ["vlans"]    = VM.IsVlansPanelOpen,
            ["mlag"]     = VM.IsMlagPanelOpen,
            ["mstp"]     = VM.IsMstpPanelOpen,
            ["serveras"] = VM.IsServerAsPanelOpen,
            ["ipranges"] = VM.IsIpRangesPanelOpen,
            ["servers"]  = VM.IsServersPanelOpen,
            ["sshlogs"]  = VM.IsSshLogsPanelOpen,
            ["details"]  = VM.IsDetailsPanelOpen,
            ["jobs"]     = VM.IsJobsPanelOpen,
            ["deploy"]   = VM.IsDeployPanelOpen,
            ["tasks"]    = VM.IsTasksPanelOpen,
            ["backlog"]  = VM.IsBacklogPanelOpen,
            ["sprintplan"]= VM.IsSprintPlanPanelOpen,
            ["burndown"] = VM.IsBurndownPanelOpen,
            ["kanban"]   = VM.IsKanbanPanelOpen,
            ["gantt"]    = VM.IsGanttPanelOpen,
            ["qa"]       = VM.IsQAPanelOpen,
            ["qadash"]   = VM.IsQADashboardPanelOpen,
            ["reports"]  = VM.IsReportBuilderPanelOpen,
            ["taskdash"] = VM.IsTaskDashboardPanelOpen,
            ["timesheet"]= VM.IsTimesheetPanelOpen,
            ["actfeed"]  = VM.IsActivityFeedPanelOpen,
            ["mytasks"]  = VM.IsMyTasksPanelOpen,
            ["portfolio"]= VM.IsPortfolioPanelOpen,
            ["taskimport"]= VM.IsTaskImportPanelOpen,
            ["taskdetail"]= VM.IsTaskDetailPanelOpen,
            ["ribboncfg"]= VM.IsRibbonConfigPanelOpen,
            ["servicedesk"]= VM.IsServiceDeskPanelOpen,
            ["sdoverview"]= VM.IsSdOverviewPanelOpen,
            ["sdclosures"]= VM.IsSdClosuresPanelOpen,
            ["sdaging"]= VM.IsSdAgingPanelOpen,
            ["sdteams"]= VM.IsSdTeamsPanelOpen,
            ["sdgroups"]= VM.IsSdGroupsPanelOpen,
            ["sdtechnicians"]= VM.IsSdTechniciansPanelOpen,
            ["sdrequesters"]= VM.IsSdRequestersPanelOpen,
            ["integrations"]= VM.IsIntegrationsPanelOpen,
            ["global_tenants"]= VM.IsGlobalTenantsPanelOpen,
            ["global_users"]= VM.IsGlobalUsersPanelOpen,
            ["global_subscriptions"]= VM.IsGlobalSubscriptionsPanelOpen,
            ["global_licenses"]= VM.IsGlobalLicensesPanelOpen,
            ["platform_dashboard"]= VM.IsPlatformDashboardPanelOpen,
        };
        var json = System.Text.Json.JsonSerializer.Serialize(states);
        await _layout.SavePreferenceAsync(PreferenceKeys.PanelStates, json);
    }

    /// <summary>
    /// Enable floating on every panel in the DockLayoutManager.
    /// Panels can be dragged out to separate windows / second monitors.
    /// Called from MainWindow_Loaded after visual tree is fully built.
    /// </summary>
    private void EnableGlobalFloating()
    {
        // Force Desktop floating mode — panels become independent OS windows
        // that can be dragged to any monitor (not child windows inside the app)
        DockManager.FloatingMode = DevExpress.Xpf.Docking.FloatingMode.Desktop;

        // Walk the full layout tree and enable floating on everything
        SetAllowFloatRecursive(DockManager.LayoutRoot);

        // Also explicitly set on named groups
        MainDocumentGroup.AllowFloat = true;
        MainDocumentGroup.AllowDrag = true;

        // Walk all closed panels too (they're not in the LayoutRoot tree)
        foreach (var item in DockManager.ClosedPanels)
        {
            item.AllowFloat = true;
            item.AllowDrag = true;
        }
    }

    private static void SetAllowFloatRecursive(DevExpress.Xpf.Docking.BaseLayoutItem? item)
    {
        if (item == null) return;
        item.AllowFloat = true;
        item.AllowDrag = true;
        if (item is DevExpress.Xpf.Docking.LayoutGroup group)
            foreach (var child in group.Items)
                SetAllowFloatRecursive(child);
    }

    private void ToggleDockPanel(DevExpress.Xpf.Docking.DocumentPanel panel, bool open)
    {
        try
        {
            if (open)
            {
                DockManager.DockController.Restore(panel);
                DockManager.Activate(panel);
            }
            else
            {
                DockManager.DockController.Close(panel);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ToggleDockPanel error: {ex.Message}");
        }
    }

    /// <summary>
    /// Toggle Details panel visibility.
    /// </summary>
    private void ToggleDetailsPanel(bool open)
    {
        try
        {
            if (open)
            {
                DetailsPanel.Visibility = Visibility.Visible;
                DockManager.DockController.Restore(DetailsPanel);
                DockManager.Activate(DetailsPanel);
            }
            else
            {
                DetailsPanel.Visibility = Visibility.Collapsed;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ToggleDetailsPanel error: {ex.Message}");
        }
    }

    private void OnActivePanelChanged(DevExpress.Xpf.Docking.BaseLayoutItem? item)
    {
        _isRolesActive = (item == RolesPanel);
        _isUsersActive = (item == UsersPanel);
        _isLookupsActive = (item == LookupsPanel);
        _isSettingsActive = (item == SettingsPanel);
        if      (item == DevicesPanel)     VM.ActivePanel = ActivePanel.Devices;
        else if (item == AsnPanel)         VM.ActivePanel = ActivePanel.Asn;
        else if (item == MasterPanel)      VM.ActivePanel = ActivePanel.Master;
        else if (item == SwitchesPanel)    VM.ActivePanel = ActivePanel.Switches;
        else if (item == P2PPanel)         VM.ActivePanel = ActivePanel.P2P;
        else if (item == B2BPanel)         VM.ActivePanel = ActivePanel.B2B;
        else if (item == FWPanel)          VM.ActivePanel = ActivePanel.FW;
        else if (item == VlansPanel)       VM.ActivePanel = ActivePanel.Vlans;
        else if (item == MlagPanel)        VM.ActivePanel = ActivePanel.Mlag;
        else if (item == MstpPanel)        VM.ActivePanel = ActivePanel.Mstp;
        else if (item == ServerAsPanel)    VM.ActivePanel = ActivePanel.ServerAs;
        else if (item == IpRangesPanel)    VM.ActivePanel = ActivePanel.IpRanges;
        else if (item == ServersPanel)     VM.ActivePanel = ActivePanel.Servers;
        else if (item == RolesPanel)       VM.ActivePanel = ActivePanel.Admin;
        else if (item == LookupsPanel)     VM.ActivePanel = ActivePanel.Admin;
        else if (item == UsersPanel)       VM.ActivePanel = ActivePanel.Admin;
        else if (item == SettingsPanel)    VM.ActivePanel = ActivePanel.Admin;
        else if (item == SshLogsPanel)     VM.ActivePanel = ActivePanel.Admin;
        else if (item == AppLogPanel)      VM.ActivePanel = ActivePanel.Admin;
        else if (item == IconDefaultsPanel)  { VM.ActivePanel = ActivePanel.Admin; if (!_iconDefaultsLoaded) _ = LoadIconDefaultsAsync(); }
        else if (item == IconOverridesPanel) { VM.ActivePanel = ActivePanel.Admin; if (!_iconOverridesLoaded) _ = LoadIconOverridesAsync(); }
        else if (item == AdBrowserPanel)     { VM.ActivePanel = ActivePanel.Admin; if (!_adBrowserLoaded) _ = LoadAdBrowserAsync(); }
        else if (item == MigrationsPanel)    { VM.ActivePanel = ActivePanel.Admin; if (!_migrationsLoaded) _ = LoadMigrationsAsync(); }
        else if (item == PurgePanel)         { VM.ActivePanel = ActivePanel.Admin; if (!_purgeLoaded) _ = LoadPurgeAsync(); }
        else if (item == BackupPanel)        { VM.ActivePanel = ActivePanel.Admin; if (!_backupLoaded) _ = LoadBackupAsync(); }
        else if (item == LocationsPanel)     { VM.ActivePanel = ActivePanel.Admin; if (!_locationsLoaded) _ = LoadLocationsAsync(); }
        else if (item == ReferenceConfigPanel) { VM.ActivePanel = ActivePanel.Admin; if (!_referenceConfigLoaded) _ = LoadReferenceConfigAsync(); }
        else if (item == PodmanPanel)        { VM.ActivePanel = ActivePanel.Admin; if (!_podmanLoaded) _ = LoadPodmanAsync(); }
        else if (item == SchedulerPanel)     { VM.ActivePanel = ActivePanel.Admin; if (!_schedulerLoaded) _ = LoadSchedulerAsync(); }
        else if (item == IdentityProvidersPanel) { VM.ActivePanel = ActivePanel.Admin; if (!_idpLoaded) _ = LoadIdentityProvidersAsync(); }
        else if (item == AuthEventsPanel)  { VM.ActivePanel = ActivePanel.Admin; if (!_authEventsLoaded) _ = LoadAuthEventsAsync(); }
        else if (item == SyncConfigPanel)  { VM.ActivePanel = ActivePanel.Admin; if (!_syncConfigLoaded) _ = LoadSyncConfigAsync(); }
        else if (item == ApiKeysPanel)     { VM.ActivePanel = ActivePanel.Admin; if (!_apiKeysLoaded) _ = LoadApiKeysAsync(); }
        else if (item == AuditLogPanel)    { VM.ActivePanel = ActivePanel.Admin; if (!_auditLogLoaded) _ = LoadAuditLogAsync(); }
        else if (item == SessionsPanel)    { VM.ActivePanel = ActivePanel.Admin; if (!_sessionsLoaded) _ = LoadSessionsAsync(); }
        else if (item == NotificationPrefsPanel) { VM.ActivePanel = ActivePanel.Admin; if (!_notifPrefsLoaded) _ = LoadNotificationPrefsAsync(); }
        else if (item == DashboardPanel)   { _ = LoadDashboardAsync(); }
        else if (item == BgpPanel)         VM.ActivePanel = ActivePanel.Bgp;
        else if (item == TasksPanel)       VM.ActivePanel = ActivePanel.Tasks;
        else if (item == SprintPlanningPanel) VM.ActivePanel = ActivePanel.SprintPlan;
        else if (item == QADocPanel)       VM.ActivePanel = ActivePanel.QA;
        else if (item == MyTasksDocPanel)  VM.ActivePanel = ActivePanel.MyTasks;
        else if (item == TimesheetDocPanel) VM.ActivePanel = ActivePanel.Timesheet;
        else if (item == JobsPanel)        VM.ActivePanel = ActivePanel.Jobs;
        else if (item == ServiceDeskPanel) VM.ActivePanel = ActivePanel.ServiceDesk;
        else if (item == SdGroupsPanel)    VM.ActivePanel = ActivePanel.SdGroups;
        else if (item == SdTechniciansPanel) VM.ActivePanel = ActivePanel.SdTechnicians;
        else if (item == SdRequestersPanel) VM.ActivePanel = ActivePanel.SdRequesters;
        else if (item == GlobalTenantsPanel) VM.ActivePanel = ActivePanel.GlobalTenants;
        else if (item == GlobalUsersPanel) VM.ActivePanel = ActivePanel.GlobalUsers;
        else if (item == GlobalSubscriptionsPanel) VM.ActivePanel = ActivePanel.GlobalSubscriptions;
        else if (item == GlobalLicensesPanel) VM.ActivePanel = ActivePanel.GlobalLicenses;

        // Deferred loading — ensure panel data is loaded on first activation
        _ = VM.EnsurePanelLoadedAsync(VM.ActivePanel);

        // Update row count for active grid
        UpdateActiveRowCount();

        // When returning to Devices/Switches panel, re-resolve the selected switch
        if (VM.ActivePanel == ActivePanel.Switches)
        {
            // Re-select current switch row if it exists
            if (VM.SelectedSwitch == null && SwitchGridPanel.Grid.VisibleRowCount > 0)
            {
                var sw = SwitchGridPanel.Grid.GetRow(SwitchGridPanel.Grid.View is DevExpress.Xpf.Grid.TableView tv ? tv.FocusedRowHandle : 0) as SwitchRecord;
                if (sw != null)
                {
                    VM.SelectedSwitch = sw;
                    _ = LoadRunningConfigForSelectedSwitch();
                }
            }
        }
        else if (VM.ActivePanel == ActivePanel.Devices)
        {
            // Re-resolve linked switch from selected device
            if (VM.SelectedDevice?.IsLinked == true && VM.SelectedSwitch == null)
            {
                _ = Task.Run(async () =>
                {
                    var sw = await VM.Repo.GetSwitchByHostnameAsync(VM.SelectedDevice.LinkedHostname);
                    await Dispatcher.InvokeAsync(() =>
                    {
                        VM.SelectedSwitch = sw;
                        UpdateSwitchTabVisibility();
                        if (sw != null) _ = LoadRunningConfigForSelectedSwitch();
                    });
                });
            }
        }

        UpdateSwitchTabVisibility();
    }

    /// <summary>
    /// Show Config/Backups/Version/Interfaces tabs when a switch is selected
    /// (directly from Switches panel, or via linked device from Devices panel).
    /// Also checks device_type for switch-like devices.
    /// </summary>
    private void UpdateSwitchTabVisibility()
    {
        var isSwitchPanel = VM.ActivePanel == ActivePanel.Switches || VM.ActivePanel == ActivePanel.Devices;

        // Determine if current selection is a switch
        var hasSwitch = VM.SelectedSwitch != null;

        // Also check if the selected device has a switch-like device_type
        if (!hasSwitch && VM.SelectedDevice != null && isSwitchPanel)
        {
            var dt = (VM.SelectedDevice.DeviceType ?? "").ToLower();
            hasSwitch = dt.Contains("switch") || dt.Contains("core") || dt.Contains("leaf")
                     || dt.Contains("management") || dt.Contains("storage");
        }

        var show = hasSwitch && isSwitchPanel;
        var vis = show ? Visibility.Visible : Visibility.Collapsed;
        DetailTabsPanel.ConfigTabItem.Visibility     = vis;
        DetailTabsPanel.BackupsTabItem.Visibility    = vis;
        DetailTabsPanel.VersionTabItem.Visibility    = vis;
        DetailTabsPanel.InterfacesTabItem.Visibility = vis;

        // Update interface summary counts
        if (show && VM.SwitchInterfaces.Count > 0)
        {
            var up = VM.SwitchInterfaces.Count(i => i.LinkStatus == "Up");
            var down = VM.SwitchInterfaces.Count(i => i.LinkStatus == "Down");
            var adminDown = VM.SwitchInterfaces.Count(i => i.AdminStatus?.Contains("Down") == true);
            var total = VM.SwitchInterfaces.Count;
            DetailTabsPanel.InterfaceSummary.Text = $"{total} interfaces · {up} up · {down} down · {adminDown} admin-down";
        }
        else
        {
            DetailTabsPanel.InterfaceSummary.Text = "";
        }
    }

    // ── Active grid helper ─────────────────────────────────────────────────

    private (DevExpress.Xpf.Grid.GridControl? Grid, DevExpress.Xpf.Grid.TableView? View) GetActiveGrid()
    {
        return VM.ActivePanel switch
        {
            ActivePanel.Devices   => (DeviceGridPanel.Grid, DeviceGridPanel.View),
            ActivePanel.Switches  => (SwitchGridPanel.Grid, SwitchGridPanel.Grid.View as DevExpress.Xpf.Grid.TableView),
            ActivePanel.Asn       => (AsnGridPanel.Grid, AsnGridPanel.View),
            ActivePanel.P2P       => (P2PGridPanel.Grid, P2PGridPanel.Grid.View as DevExpress.Xpf.Grid.TableView),
            ActivePanel.B2B       => (B2BGridPanel.Grid, B2BGridPanel.Grid.View as DevExpress.Xpf.Grid.TableView),
            ActivePanel.FW        => (FWGridPanel.Grid, FWGridPanel.Grid.View as DevExpress.Xpf.Grid.TableView),
            ActivePanel.Vlans     => (VlanGridPanel.Grid, VlanGridPanel.View),
            ActivePanel.Mlag      => (MlagGridPanel.Grid, MlagGridPanel.Grid.View as DevExpress.Xpf.Grid.TableView),
            ActivePanel.Mstp      => (MstpGridPanel.Grid, MstpGridPanel.Grid.View as DevExpress.Xpf.Grid.TableView),
            ActivePanel.ServerAs  => (ServerAsGridPanel.Grid, ServerAsGridPanel.Grid.View as DevExpress.Xpf.Grid.TableView),
            ActivePanel.IpRanges  => (IpRangesGridPanel.Grid, IpRangesGridPanel.Grid.View as DevExpress.Xpf.Grid.TableView),
            ActivePanel.Servers   => (ServersGrid, ServersGrid.View as DevExpress.Xpf.Grid.TableView),
            ActivePanel.Master    => (MasterGridPanel.Grid, MasterGridPanel.Grid.View as DevExpress.Xpf.Grid.TableView),
            ActivePanel.Bgp       => (BgpGridPanel.Grid, BgpGridPanel.Grid.View as DevExpress.Xpf.Grid.TableView),
            ActivePanel.Jobs      => (JobsGridPanel.SchedulesGridControl, JobsGridPanel.SchedulesGridControl.View as DevExpress.Xpf.Grid.TableView),
            // Tasks tree, Backlog tree, Gantt, Portfolio use TreeListControl — not compatible with GridControl
            ActivePanel.SprintPlan => (SprintPlanGridPanel.Grid, SprintPlanGridPanel.Grid.View as DevExpress.Xpf.Grid.TableView),
            ActivePanel.QA        => (QAGridPanel.Grid, QAGridPanel.View),
            ActivePanel.MyTasks   => (MyTasksViewPanel.Grid, MyTasksViewPanel.View),
            ActivePanel.Timesheet => (TimesheetViewPanel.Grid, TimesheetViewPanel.Grid.View as DevExpress.Xpf.Grid.TableView),
            ActivePanel.ServiceDesk => (RequestGridPanel.Grid, RequestGridPanel.View),
            ActivePanel.SdGroups  => (SdGroupsGridPanel.Grid, SdGroupsGridPanel.View),
            ActivePanel.SdTechnicians => (SdTechGridPanel.Grid, SdTechGridPanel.View),
            ActivePanel.SdRequesters => (SdReqGridPanel.Grid, SdReqGridPanel.View),
            ActivePanel.GlobalTenants => (TenantsGridPanel.Grid, TenantsGridPanel.View),
            ActivePanel.GlobalUsers => (GlobalUsersGridPanel.Grid, GlobalUsersGridPanel.View),
            ActivePanel.GlobalSubscriptions => (SubscriptionsGridPanel.Grid, SubscriptionsGridPanel.View),
            ActivePanel.GlobalLicenses => (LicensesGridPanel.Grid, LicensesGridPanel.View),
            ActivePanel.Admin     => _isUsersActive ? (UsersGridPanel.Grid, UsersGridPanel.View) :
                                     _isSettingsActive ? (SettingsGrid, SettingsGrid.View as DevExpress.Xpf.Grid.TableView) :
                                     ((DevExpress.Xpf.Grid.GridControl?, DevExpress.Xpf.Grid.TableView?))(null, null),
            _ => (null, null)
        };
    }

    // ── Global ribbon action handlers (Home tab) ────────────────────────────

    // ── Undo / Redo ────────────────────────────────────────────────────

    private void WireUndoRedo()
    {
        var undo = UndoService.Instance;
        undo.StateChanged += (_, _) => Dispatcher.InvokeAsync(() =>
        {
            UndoButton.IsEnabled = undo.CanUndo;
            RedoButton.IsEnabled = undo.CanRedo;
            UndoButton.Description = undo.UndoDescription ?? "";
            RedoButton.Description = undo.RedoDescription ?? "";

            // Populate undo history dropdown
            var undoPopup = UndoButton.PopupControl as PopupMenu ?? new PopupMenu();
            undoPopup.Items.Clear();
            foreach (var desc in undo.UndoHistory.Take(10))
            {
                var item = new BarButtonItem { Content = desc };
                item.ItemClick += (_, _) => { try { undo.Undo(); } catch { } };
                undoPopup.Items.Add(item);
            }
            UndoButton.PopupControl = undoPopup;

            var redoPopup = RedoButton.PopupControl as PopupMenu ?? new PopupMenu();
            redoPopup.Items.Clear();
            foreach (var desc in undo.RedoHistory.Take(10))
            {
                var item = new BarButtonItem { Content = desc };
                item.ItemClick += (_, _) => { try { undo.Redo(); } catch { } };
                redoPopup.Items.Add(item);
            }
            RedoButton.PopupControl = redoPopup;
        });
    }

    private void UndoButton_ItemClick(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)
    {
        try { UndoService.Instance.Undo(); }
        catch (Exception ex) { AppLogger.LogException("Undo", ex, "UndoButton"); }
    }

    private void RedoButton_ItemClick(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)
    {
        try { UndoService.Instance.Redo(); }
        catch (Exception ex) { AppLogger.LogException("Redo", ex, "RedoButton"); }
    }

    private void GlobalAdd_ItemClick(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)
    {
        Central.Core.Services.CommandGuard.Run("GlobalAdd", () =>
        {
            var (_, view) = GetActiveGrid();
            if (view != null) view.AddNewRow();
        });
    }

    private async void GlobalDelete_ItemClick(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)
    {
        if (!Central.Core.Services.CommandGuard.TryEnter("GlobalDelete")) return;
        try
        {
        var (grid, view) = GetActiveGrid();
        if (grid == null || view == null) return;

        // Collect selected rows (multi-select support)
        var selectedHandles = view.GetSelectedRowHandles();
        if (selectedHandles.Length == 0)
        {
            // Single row — use focused
            selectedHandles = new[] { view.FocusedRowHandle };
        }

        var rows = selectedHandles
            .Where(h => h >= 0)
            .Select(h => grid.GetRow(h))
            .Where(r => r != null)
            .ToList();

        if (rows.Count == 0) return;

        var msg = rows.Count == 1
            ? "Delete 1 row?"
            : $"Delete {rows.Count} rows?";
        if (System.Windows.MessageBox.Show(msg, "Confirm Delete",
            MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        int deleted = 0;
        var undo = UndoService.Instance;
        undo.BeginBatch($"Delete {rows.Count} rows");
        foreach (var row in rows)
        {
            try
            {
                switch (row)
                {
                    case DeviceRecord d:    await VM.Repo.DeleteDeviceAsync(d.Id); VM.Devices.Remove(d); deleted++; break;
                    case P2PLink p:         await VM.Repo.DeleteP2PLinkAsync(p.Id); VM.P2PLinks.Remove(p); deleted++; break;
                    case B2BLink b:         await VM.Repo.DeleteB2BLinkAsync(b.Id); VM.B2BLinks.Remove(b); deleted++; break;
                    case FWLink f:          await VM.Repo.DeleteFWLinkAsync(f.Id); VM.FWLinks.Remove(f); deleted++; break;
                    case VlanEntry v:       await VM.Repo.DeleteVlanEntryAsync(v.Id); VM.VlanEntries.Remove(v); deleted++; break;
                    case MlagConfig m:      await VM.Repo.DeleteMlagConfigAsync(m.Id); VM.MlagConfigs.Remove(m); deleted++; break;
                    case MstpConfig t:      await VM.Repo.DeleteMstpConfigAsync(t.Id); VM.MstpConfigs.Remove(t); deleted++; break;
                    case ServerAS sa:       await VM.Repo.DeleteServerAsAsync(sa.Id); VM.ServerASList.Remove(sa); deleted++; break;
                    case IpRange ir:        await VM.Repo.DeleteIpRangeAsync(ir.Id); VM.IpRanges.Remove(ir); deleted++; break;
                    case Server sv:         await VM.Repo.DeleteServerAsync(sv.Id); VM.Servers.Remove(sv); deleted++; break;
                    case AsnDefinition a:   await VM.Repo.DeleteAsnDefinitionAsync(a.Id); VM.AsnDefinitions.Remove(a); deleted++; break;
                    case MasterDevice md:   await VM.Repo.DeleteMasterDeviceAsync(md.Id.ToString()); VM.MasterDevices.Remove(md); deleted++; break;
                    case LookupItem li:     await VM.Repo.DeleteLookupAsync(li.Id); VM.LookupItems.Remove(li); deleted++; break;
                    case AppUser u:         if (await VM.Repo.DeleteUserAsync(u.Id)) { VM.Users.Remove(u); deleted++; } break;
                    case AppLogEntry le:    await VM.Repo.DeleteAppLogAsync(le.Id); VM.AppLogs.Remove(le); deleted++; break;
                }
            }
            catch (Exception ex)
            {
                AppLogger.LogException("Delete", ex, $"GlobalDelete row {row.GetType().Name}");
            }
        }
        undo.CommitBatch();
        if (deleted > 0)
            VM.StatusText = $"Deleted {deleted} row(s)  ·  {DateTime.Now:HH:mm:ss}";
        }
        finally { Central.Core.Services.CommandGuard.Exit("GlobalDelete"); }
    }

    private void GlobalEdit_ItemClick(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)
    {
        var (_, view) = GetActiveGrid();
        if (view != null) view.ShowEditor();
    }

    private void GlobalMoveUp_ItemClick(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)
    {
        var (grid, view) = GetActiveGrid();
        if (grid == null || view == null) return;
        var handle = view.FocusedRowHandle;
        if (handle <= 0) return;
        view.FocusedRowHandle = handle - 1;
    }

    private void GlobalMoveDown_ItemClick(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)
    {
        var (grid, view) = GetActiveGrid();
        if (grid == null || view == null) return;
        var handle = view.FocusedRowHandle;
        if (handle >= grid.VisibleRowCount - 1) return;
        view.FocusedRowHandle = handle + 1;
    }

    private void GlobalClearFilter_ItemClick(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)
    {
        var (grid, _) = GetActiveGrid();
        if (grid != null) grid.FilterString = "";
    }

    private void GlobalAutoFilter_CheckedChanged(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)
    {
        var (_, view) = GetActiveGrid();
        if (view != null)
            view.ShowAutoFilterRow = GlobalAutoFilterToggle.IsChecked == true;
    }

    private async void SaveFilter_ItemClick(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)
    {
        var (grid, _) = GetActiveGrid();
        if (grid == null || string.IsNullOrEmpty(grid.FilterString)) { VM.StatusText = "No filter to save"; return; }

        var panelName = GetActivePanelName();
        var name = Services.InputPrompt.Show("Save Filter", "Filter name:", $"Filter {DateTime.Now:HH:mm}", this);
        if (string.IsNullOrWhiteSpace(name)) return;

        var filter = new Central.Core.Models.SavedFilter
        {
            UserId = AuthContext.Instance.CurrentUser?.Id,
            PanelName = panelName,
            FilterName = name,
            FilterExpr = grid.FilterString
        };
        await VM.Repo.UpsertSavedFilterAsync(filter);
        VM.StatusText = $"Filter '{name}' saved";
        NotificationService.Instance.Success($"Filter saved: {name}");
    }

    private async void LoadFilter_ItemClick(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)
    {
        var (grid, _) = GetActiveGrid();
        if (grid == null) return;

        var panelName = GetActivePanelName();
        var filters = await VM.Repo.GetSavedFiltersAsync(panelName, AuthContext.Instance.CurrentUser?.Id);
        if (filters.Count == 0) { VM.StatusText = "No saved filters for this panel"; return; }

        // Populate the split button dropdown
        if (sender is BarSplitButtonItem split)
        {
            var popup = split.PopupControl as PopupMenu ?? new PopupMenu();
            popup.Items.Clear();
            foreach (var f in filters)
            {
                var item = new BarButtonItem { Content = f.FilterName, Tag = f.FilterExpr };
                item.ItemClick += (_, _) =>
                {
                    grid.FilterString = f.FilterExpr;
                    VM.StatusText = $"Filter applied: {f.FilterName}";
                };
                popup.Items.Add(item);
            }
            popup.Items.Add(new BarItemSeparator());
            var clearItem = new BarButtonItem { Content = "Clear Filter" };
            clearItem.ItemClick += (_, _) => { grid.FilterString = ""; VM.StatusText = "Filter cleared"; };
            popup.Items.Add(clearItem);
            split.PopupControl = popup;
        }
        else if (filters.Count > 0)
        {
            // Direct click — apply first/default filter
            var def = filters.FirstOrDefault(f => f.IsDefault) ?? filters[0];
            grid.FilterString = def.FilterExpr;
            VM.StatusText = $"Filter applied: {def.FilterName}";
        }
    }

    private string GetActivePanelName()
    {
        var (grid, _) = GetActiveGrid();
        if (grid == null) return "unknown";
        return grid.Name switch
        {
            _ when grid == DeviceGridPanel.Grid => "devices",
            _ when grid == SwitchGridPanel.Grid => "switches",
            _ when grid == P2PGridPanel.Grid => "p2p",
            _ when grid == B2BGridPanel.Grid => "b2b",
            _ when grid == FWGridPanel.Grid => "fw",
            _ => grid.Name ?? "unknown"
        };
    }

    private void GlobalExpandAll_ItemClick(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)
    {
        var (grid, _) = GetActiveGrid();
        grid?.ExpandAllGroups();
    }

    private void GlobalCollapseAll_ItemClick(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)
    {
        var (grid, _) = GetActiveGrid();
        grid?.CollapseAllGroups();
    }

    private void GlobalClearGrouping_ItemClick(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)
    {
        var (grid, _) = GetActiveGrid();
        if (grid != null) grid.ClearGrouping();
    }

    private string? GetLayoutKeyForActiveGrid()
    {
        return VM.ActivePanel switch
        {
            ActivePanel.Devices   => PreferenceKeys.DevicesGrid,
            ActivePanel.Switches  => PreferenceKeys.SwitchesGrid,
            ActivePanel.Asn       => PreferenceKeys.AsnGrid,
            ActivePanel.P2P       => PreferenceKeys.P2PGrid,
            ActivePanel.B2B       => PreferenceKeys.B2BGrid,
            ActivePanel.FW        => PreferenceKeys.FWGrid,
            ActivePanel.Vlans     => PreferenceKeys.VlansGrid,
            ActivePanel.Mlag      => PreferenceKeys.MlagGrid,
            ActivePanel.Mstp      => PreferenceKeys.MstpGrid,
            ActivePanel.ServerAs  => PreferenceKeys.ServerAsGrid,
            ActivePanel.IpRanges  => PreferenceKeys.IpRangesGrid,
            ActivePanel.Servers   => PreferenceKeys.ServersGrid,
            ActivePanel.Master    => PreferenceKeys.MasterGrid,
            ActivePanel.Admin     => _isUsersActive ? PreferenceKeys.UsersGrid :
                                     _isSettingsActive ? PreferenceKeys.SettingsGrid : null,
            _ => null
        };
    }

    private async void GlobalSaveLayout_ItemClick(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)
    {
        await Central.Core.Services.CommandGuard.RunAsync("SaveLayout", async () =>
        {
            var (grid, _) = GetActiveGrid();
            var key = GetLayoutKeyForActiveGrid();
            if (grid == null || key == null) return;
            await _layout.SaveGridLayoutAsync(grid, key);
            VM.StatusText = $"Layout saved for {VM.ActivePanel}  ·  {DateTime.Now:HH:mm:ss}";
        });
    }

    // ── View Toggles (Widget Customizer) ────────────────────────────────

    private void ToggleSearchPanel_CheckedChanged(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)
    {
        var (_, view) = GetActiveGrid();
        if (view != null)
            view.ShowSearchPanelMode = ToggleSearchPanel.IsChecked == true
                ? DevExpress.Xpf.Grid.ShowSearchPanelMode.Always
                : DevExpress.Xpf.Grid.ShowSearchPanelMode.Never;
    }

    private void ToggleAutoFilter_CheckedChanged(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)
    {
        var (_, view) = GetActiveGrid();
        if (view != null)
            view.ShowAutoFilterRow = ToggleAutoFilter.IsChecked == true;
    }

    private void ToggleGroupPanel_CheckedChanged(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)
    {
        var (_, view) = GetActiveGrid();
        if (view != null)
            view.ShowGroupPanel = ToggleGroupPanel.IsChecked == true;
    }

    private void ToggleGridLines_CheckedChanged(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)
    {
        var (_, view) = GetActiveGrid();
        if (view != null)
        {
            view.ShowVerticalLines = ToggleGridLines.IsChecked == true;
            view.ShowHorizontalLines = ToggleGridLines.IsChecked == true;
        }
    }

    // ── Context Tab Handlers ────────────────────────────────────────────

    private void DeployToSwitchButton_ItemClick(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)
        => DeployLinkButton_ItemClick(sender, e);

    private void CopyConfigA_ItemClick(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)
    {
        var (grid, _) = GetActiveGrid();
        if (grid?.CurrentItem is Central.Core.Models.INetworkLink link)
        {
            System.Windows.Clipboard.SetText(link.ConfigA);
            VM.StatusText = "Config A copied to clipboard";
        }
    }

    private void CopyConfigB_ItemClick(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)
    {
        var (grid, _) = GetActiveGrid();
        if (grid?.CurrentItem is Central.Core.Models.INetworkLink link)
        {
            System.Windows.Clipboard.SetText(link.ConfigB);
            VM.StatusText = "Config B copied to clipboard";
        }
    }

    private void PingSingleSwitch_ItemClick(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)
        => PingSingleSwitch_ContextMenu();

    /// <summary>Safe wrapper for API data service writes. Returns DbResult-compatible tuple.</summary>
    private static async Task<Central.Data.DbResult> SafeApiWrite(Func<Task> action, string context)
    {
        try
        {
            await action();
            return new Central.Data.DbResult { Success = true };
        }
        catch (Exception ex)
        {
            AppLogger.LogException("API", ex, $"SafeApiWrite:{context}");
            return new Central.Data.DbResult { Success = false, Error = ex.Message };
        }
    }

    private void ToggleSummary_CheckedChanged(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)
    {
        var (_, view) = GetActiveGrid();
        if (view != null)
            view.ShowTotalSummary = ToggleSummary.IsChecked == true;
    }

    private void BestFitColumns_ItemClick(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)
    {
        var (_, view) = GetActiveGrid();
        view?.BestFitColumns();
    }

    private async void GlobalResetLayout_ItemClick(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)
    {
        var key = GetLayoutKeyForActiveGrid();
        if (key == null) return;
        // Delete saved layout from DB
        if (AuthContext.Instance.CurrentUser != null)
            await VM.Repo.DeleteUserSettingAsync(AuthContext.Instance.CurrentUser.Id, key);
        VM.StatusText = $"Layout reset for {VM.ActivePanel} — restart to apply  ·  {DateTime.Now:HH:mm:ss}";
    }

    // ── Ribbon ItemClick handlers (DevExpress BarButtonItem) ────────────────

    private void NewButton_ItemClick(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)
    {
        switch (VM.ActivePanel)
        {
            case ActivePanel.Devices:  AddDevice(); break;
            case ActivePanel.Admin:
                if (_isUsersActive) UsersGridPanel.View.AddNewRow();
                else                AddLookup();
                break;
        }
    }

    /// <summary>Keyboard shortcut: Delete key routes to the active panel's delete action.</summary>
    private async Task DeleteActiveRow() => DeleteButton_ItemClick(this, null!);

    private async void DeleteButton_ItemClick(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)
    {
        try
        {
            switch (VM.ActivePanel)
            {
                case ActivePanel.Devices:  await DeleteSelectedDevice(); break;
                case ActivePanel.Admin:
                    if (_isUsersActive) await DeleteSelectedUser();
                    else                await DeleteSelectedLookup();
                    break;
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Delete failed: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void EditButton_ItemClick(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)
    {
        switch (VM.ActivePanel)
        {
            case ActivePanel.Devices:  DeviceGridPanel.View.ShowEditor();  break;
            case ActivePanel.Admin:
                if (_isUsersActive) UsersGridPanel.View.ShowEditor();
                else                LookupsGridPanel.View.ShowEditor();
                break;
        }
    }

    // ── Add / Delete ──────────────────────────────────────────────────────

    private void AddDevice()
    {
        DeviceGridPanel.View.AddNewRow();
    }

    private async Task DeleteSelectedDevice()
    {
        // Capture the target device BEFORE touching the editor — CurrentItem is stable here
        var device = (DeviceGridPanel.Grid.CurrentItem as DeviceRecord)
                  ?? (DeviceGridPanel.Grid.GetFocusedRow() as DeviceRecord)
                  ?? VM.SelectedDevice;

        // Cancel any active row edit without triggering a save
        _deletePending = true;
        try
        {
            try { DeviceGridPanel.View.HideEditor(); } catch { /* no active editor — safe to ignore */ }
            try { DeviceGridPanel.View.CancelRowEdit(); } catch { /* no active row edit — safe to ignore */ }
        }
        finally
        {
            _deletePending = false;
        }

        // Let the UI settle after editor teardown
        await System.Windows.Application.Current.Dispatcher.InvokeAsync(
            () => { }, System.Windows.Threading.DispatcherPriority.Background);

        if (device == null)
        {
            System.Windows.MessageBox.Show("Please select a device first.",
                "No Selection", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var name   = string.IsNullOrWhiteSpace(device.SwitchName) ? "(unnamed)" : device.SwitchName;
        var result = System.Windows.MessageBox.Show(
            $"Delete device '{name}'?\n\nThis cannot be undone.",
            "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning,
            MessageBoxResult.No);

        if (result == MessageBoxResult.Yes)
            await VM.DeleteDeviceAsync(device);
    }

    private void AddLookup()
    {
        LookupsGridPanel.View.AddNewNode();
    }

    private async Task DeleteSelectedLookup()
    {
        var item = (LookupsGridPanel.Grid.CurrentItem as LookupItem)
                ?? VM.SelectedLookup;
        if (item == null)
        {
            System.Windows.MessageBox.Show("Please select a lookup value first.",
                "No Selection", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var result = System.Windows.MessageBox.Show(
            $"Delete lookup '{item.Category} / {item.Value}'?\n\nThis cannot be undone.",
            "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning,
            MessageBoxResult.No);

        if (result == MessageBoxResult.Yes)
            await VM.DeleteLookupAsync(item);
    }

    private async Task DeleteSelectedUser()
    {
        var user = (UsersGridPanel.Grid.CurrentItem as AppUser) ?? VM.SelectedUser;
        if (user == null)
        {
            System.Windows.MessageBox.Show("Please select a user first.",
                "No Selection", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var result = System.Windows.MessageBox.Show(
            $"Delete user '{user.Username}'?\n\nThis cannot be undone.",
            "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning,
            MessageBoxResult.No);

        if (result == MessageBoxResult.Yes)
            await VM.DeleteUserAsync(user);
    }

    // ── Admin Actions (generic New/Edit/Delete routed by active panel) ──

    private void AdminNewButton_ItemClick(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)
    {
        if (_isUsersActive) NewUserButton_ItemClick(sender, e);
        else if (_isSettingsActive) SettingsView.AddNewRow();
        else if (_isLookupsActive) { LookupsGridPanel.View.AddNewNode(); }
        else /* roles is default */ NewRoleButton_ItemClick(sender, e);
    }

    private void AdminEditButton_ItemClick(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)
    {
        if (_isUsersActive) EditUserButton_ItemClick(sender, e);
        else if (_isSettingsActive) SettingsView.ShowEditor();
        else if (_isLookupsActive) LookupsGridPanel.View.ShowEditor();
        else RolesGridPanel.View.ShowEditor();
    }

    private void SetPasswordButton_ItemClick(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)
    {
        if (!_isUsersActive) { VM.StatusText = "Select a user in the Users panel first"; return; }
        var user = UsersGridPanel.Grid.CurrentItem as AppUser;
        if (user == null || user.Id == 0) { VM.StatusText = "Select a user first"; return; }

        var dialog = new SetPasswordWindow(App.Dsn, user) { Owner = this };
        if (dialog.ShowDialog() == true)
            VM.StatusText = $"Password set for {user.DisplayName}";
    }

    private void SetupMfaButton_ItemClick(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)
    {
        var user = AuthContext.Instance.CurrentUser;
        if (user == null) { VM.StatusText = "Not logged in"; return; }

        var dialog = new MfaEnrollmentDialog(user.Username) { Owner = this };
        dialog.OnMfaEnabled = async (secret, recoveryCodes) =>
        {
            // Encrypt secret and save to DB
            var encrypted = Central.Core.Auth.CredentialEncryptor.Encrypt(secret);
            await VM.Repo.EnableMfaAsync(user.Id, encrypted);
            await VM.Repo.SaveRecoveryCodesAsync(user.Id, recoveryCodes);
            VM.StatusText = $"MFA enabled for {user.Username}";
            _ = Central.Core.Services.AuditService.Instance.LogAsync(
                "MfaEnabled", "User", user.Id.ToString(), user.Username);
        };
        dialog.ShowDialog();
    }

    private async void AdminDeleteButton_ItemClick(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)
    {
        try
        {
            if (_isUsersActive) await DeleteSelectedUser();
            else if (_isSettingsActive) await DeleteSelectedRange();
            else if (_isLookupsActive) await DeleteSelectedLookup();
            else DeleteRoleButton_ItemClick(sender, e);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Delete failed: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task DeleteSelectedRange()
    {
        if (VM.SelectedRange == null) return;
        var result = System.Windows.MessageBox.Show(
            $"Delete range '{VM.SelectedRange.Name}'?", "Confirm Delete",
            MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No);
        if (result != MessageBoxResult.Yes) return;
        await VM.DeleteConfigRangeAsync(VM.SelectedRange);
    }

    // ── User CRUD (Admin ribbon) ────────────────────────────────────────

    private void NewUserButton_ItemClick(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)
    {
        // Make sure Users panel is visible
        UsersPanel.Visibility = Visibility.Visible;
        VM.IsUsersPanelOpen = true;
        UsersGridPanel.View.AddNewRow();
    }

    private async void DeleteUserButton_ItemClick(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)
    {
        try { await DeleteSelectedUser(); }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Delete failed: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void EditUserButton_ItemClick(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)
    {
        UsersPanel.Visibility = Visibility.Visible;
        VM.IsUsersPanelOpen = true;
        UsersGridPanel.View.ShowEditor();
    }

    // ── Role CRUD ─────────────────────────────────────────────────────────

    private void NewRoleButton_ItemClick(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)
    {
        RolesGridPanel.View.AddNewRow();
    }

    private async void DeleteRoleButton_ItemClick(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)
    {
        try
        {
            var role = (RolesGridPanel.Grid.CurrentItem as RoleRecord) ?? VM.SelectedRole;
            if (role == null)
            {
                System.Windows.MessageBox.Show("Please select a role first.",
                    "No Selection", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (role.Name == "Admin")
            {
                System.Windows.MessageBox.Show("The Admin role cannot be deleted.",
                    "Protected Role", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var result = System.Windows.MessageBox.Show(
                $"Delete role '{role.Name}'?\n\nUsers with this role will need to be reassigned.",
                "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning,
                MessageBoxResult.No);

            if (result == MessageBoxResult.Yes)
                await VM.DeleteRoleAsync(role);
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Delete failed: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // AdminRefreshButton now uses RefreshCommand binding

    // RolesView ValidateRow → Module.Admin/Views/RolesPanel.xaml.cs

    private void RolesView_InvalidRowException(object sender, InvalidRowExceptionEventArgs e)
    {
        e.ExceptionMode = ExceptionMode.NoAction;
    }

    // ── Roles (logic moved to RolesPanel UserControl) ──

    private async void RolesGrid_CurrentItemChanged(object sender, CurrentItemChangedEventArgs e)
    {
        VM.SelectedRole = e.NewItem as RoleRecord;
        RolesGridPanel.LoadPermissionTreeForRole(VM.SelectedRole);
        if (VM.SelectedRole is { Name.Length: > 0 })
            await VM.LoadRoleSitesAsync(VM.SelectedRole.Name);
    }

    // ── Switch Connectivity (logic in MainViewModel) ──────────────────

    private async void PingAllButton_ItemClick(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)
        => await VM.PingAllAsync();

    private async void PingSelectedButton_ItemClick(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)
        => await VM.PingSelectedAsync((SwitchGridPanel.Grid.CurrentItem as SwitchRecord) ?? VM.SelectedSwitch);

    // ── Auto Scan Timer ────────────────────────────────────────────────

    private void StartScanTimer()
    {
        StopScanTimer();
        _scanTimer = new System.Windows.Threading.DispatcherTimer
            { Interval = TimeSpan.FromMinutes(VM.ScanIntervalMinutes) };
        _scanTimer.Tick += async (_, _) => await VM.RunPingScanAsync();
        _scanTimer.Start();
        VM.StatusText = $"Auto scan enabled — every {VM.ScanIntervalMinutes} min";
    }

    private void StopScanTimer() { _scanTimer?.Stop(); _scanTimer = null; }

    private void ScanToggle_CheckedChanged(object sender, ItemClickEventArgs e)
    {
        if (VM.IsScanEnabled) StartScanTimer();
        else { StopScanTimer(); VM.StatusText = "Auto scan disabled"; }
    }

    private async void RefreshButton_ItemClick(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)
    {
        await Central.Core.Services.CommandGuard.RunAsync("Refresh", async () =>
        {
            VM.StatusText = "Refreshing...";
            await VM.LoadAllAsync();
            BindComboSources();
            _ = VM.RunPingScanAsync();
        });
    }

    // ── Search / Filter / Group ─────────────────────────────────────────

    private void DevicesSearch_EditValueChanged(object sender, DevExpress.Xpf.Editors.EditValueChangedEventArgs e)
        => UpdateDevicesFilter();

    private void SiteToggle_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is SiteSummary site)
        {
            site.IsSelected = !site.IsSelected;
            UpdateDevicesFilter();
            _ = RefreshVlanSiteDataAsync();
        }
    }

    // SiteCheckChanged removed — checkboxes replaced with toggle buttons

    private void UpdateDevicesFilter()
    {
        var parts = new System.Collections.Generic.List<string>();

        var text = DeviceGridPanel.SearchBox.EditValue as string ?? "";
        if (!string.IsNullOrWhiteSpace(text))
            parts.Add($"([SwitchName] Like '%{text}%' Or [DeviceType] Like '%{text}%' Or " +
                      $"[Building] Like '%{text}%' Or [Ip] Like '%{text}%' Or " +
                      $"[ManagementIp] Like '%{text}%' Or [LoopbackIp] Like '%{text}%' Or [Asn] Like '%{text}%')");

        if (VM.HideReserved)
            parts.Add("[Status] <> 'RESERVED'");

        // Site toggle filter — only apply if some sites are deselected
        var enabled  = VM.SiteSummaries.Where(s => s.IsSelected).Select(s => s.Building).ToList();
        var disabled = VM.SiteSummaries.Where(s => !s.IsSelected).ToList();
        string? siteFilter = null;
        string? siteFilterAB = null; // for B2B with BuildingA/BuildingB
        if (disabled.Count > 0 && enabled.Count > 0)
        {
            var inList = string.Join(", ", enabled.Select(b => $"'{b.Replace("'", "''")}'"));
            siteFilter = $"[Building] In ({inList})";
            siteFilterAB = $"([BuildingA] In ({inList}) Or [BuildingB] In ({inList}))";
            parts.Add(siteFilter);
        }
        else if (enabled.Count == 0)
        {
            siteFilter = "1 = 0";
            siteFilterAB = "1 = 0";
            parts.Add("1 = 0");
        }

        DeviceGridPanel.Grid.FilterString  = parts.Count == 0 ? null : string.Join(" And ", parts);
        VM.DeviceCountText     = $"{DeviceGridPanel.Grid.VisibleRowCount} devices";

        // Build reserved filter for all grids
        string? reservedFilter = VM.HideReserved ? "[Status] <> 'RESERVED'" : null;

        // Apply site + reserved filter to all other device grids
        ApplyGridFilter(MasterGridPanel.Grid, siteFilter, reservedFilter);
        ApplyGridFilter(AsnGridPanel.Grid, null, null); // AsnDefinition has no Status/Building columns
        ApplyGridFilter(P2PGridPanel.Grid, siteFilter, reservedFilter);
        ApplyGridFilter(FWGridPanel.Grid, siteFilter, reservedFilter);
        ApplyGridFilter(MlagGridPanel.Grid, siteFilter, reservedFilter);
        ApplyGridFilter(MstpGridPanel.Grid, siteFilter, reservedFilter);
        ApplyGridFilter(ServerAsGridPanel.Grid, siteFilter, reservedFilter);
        ApplyGridFilter(ServersGrid, siteFilter, reservedFilter);
        ApplyGridFilter(IpRangesGridPanel.Grid, null, reservedFilter);
        ApplyGridFilter(VlanGridPanel.Grid, null, reservedFilter);
        // B2B has BuildingA/BuildingB instead of Building
        ApplyGridFilter(B2BGridPanel.Grid, siteFilterAB, reservedFilter);

        // Switches grid uses [Site] not [Building]
        string? switchSiteFilter = null;
        if (disabled.Count > 0 && enabled.Count > 0)
        {
            var inList = string.Join(", ", enabled.Select(b => $"'{b.Replace("'", "''")}'"));
            switchSiteFilter = $"[Site] In ({inList})";
        }
        else if (enabled.Count == 0)
        {
            switchSiteFilter = "1 = 0";
        }
        SwitchGridPanel.Grid.FilterString = switchSiteFilter;

        // Refresh diagram if open
        if (VM.IsDiagramPanelOpen)
            _ = BuildNetworkDiagramAsync();
    }

    private static void ApplyGridFilter(DevExpress.Xpf.Grid.GridControl grid, string? siteFilter, string? reservedFilter)
    {
        try
        {
            var filters = new System.Collections.Generic.List<string>();
            if (!string.IsNullOrEmpty(siteFilter)) filters.Add(siteFilter);
            if (!string.IsNullOrEmpty(reservedFilter)) filters.Add(reservedFilter);
            grid.FilterString = filters.Count == 0 ? null : string.Join(" And ", filters);
        }
        catch (Exception ex)
        {
            AppLogger.LogException("Grid", ex, $"ApplyGridFilter({grid.Name})");
        }
    }

    private void GroupBy(string fieldName)
    {
        DeviceGridPanel.Grid.SortInfo.ClearAndAddRange(new[]
        {
            new GridSortInfo(fieldName, System.ComponentModel.ListSortDirection.Ascending)
        });
        DeviceGridPanel.Grid.GroupCount = 1;
    }

    private void ClearGroups()
    {
        DeviceGridPanel.Grid.GroupCount = 0;
        DeviceGridPanel.Grid.SortInfo.Clear();
    }

    // ── Export ────────────────────────────────────────────────────────────

    private void ExportDevices()
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Filter   = "Excel Workbook|*.xlsx|CSV|*.csv",
            FileName = "Central_Devices"
        };
        if (dlg.ShowDialog() == true)
        {
            if (dlg.FileName.EndsWith(".xlsx", System.StringComparison.OrdinalIgnoreCase))
                DeviceGridPanel.View.ExportToXlsx(dlg.FileName);
            else
                DeviceGridPanel.View.ExportToCsv(dlg.FileName);
        }
    }

    // ── Web App ───────────────────────────────────────────────────────────

    private static void OpenWebApp() =>
        Process.Start(new ProcessStartInfo("http://127.0.0.1:7472") { UseShellExecute = true });

    private async void OpenDevice(DeviceRecord? device)
    {
        if (device == null || !device.IsLinked) return;

        var hostname = device.LinkedHostname;

        // Look up the switch
        var sw = await VM.Repo.GetSwitchByHostnameAsync(hostname);
        if (sw == null)
        {
            System.Windows.MessageBox.Show($"No switch record found for '{hostname}'", "Not Found",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        // Load the latest running config
        var config = await VM.Repo.GetLatestRunningConfigAsync(sw.Id);
        if (string.IsNullOrWhiteSpace(config))
            config = "(no config downloaded yet — use Sync to download)";

        // Create a floating document panel with the config
        var panelName = $"ConfigPanel_{hostname.Replace("-", "_")}";

        // Check if panel already exists and activate it
        // Check if panel already exists by searching float groups
        foreach (var fg in DockManager.FloatGroups)
        {
            foreach (var item in fg.Items)
            {
                if (item is DevExpress.Xpf.Docking.DocumentPanel dp && dp.Name == panelName)
                {
                    DockManager.DockController.Activate(dp);
                    return;
                }
            }
        }

        var textBox = new System.Windows.Controls.TextBox
        {
            Text = config,
            IsReadOnly = true,
            FontFamily = new System.Windows.Media.FontFamily("Cascadia Mono, Consolas, Courier New"),
            FontSize = 12,
            VerticalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = System.Windows.Controls.ScrollBarVisibility.Auto,
            Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 30, 30)),
            Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(220, 220, 220)),
            TextWrapping = TextWrapping.NoWrap,
            AcceptsReturn = true,
        };

        var panel = new DevExpress.Xpf.Docking.DocumentPanel
        {
            Name = panelName,
            Caption = $"Config: {hostname}",
            Content = textBox,
            AllowFloat = true,
            AllowClose = true,
        };

        // Add as a floating document
        DockManager.FloatGroups.Add(new DevExpress.Xpf.Docking.FloatGroup
        {
            FloatSize = new System.Windows.Size(800, 600),
            Items = { panel }
        });
        DockManager.DockController.Activate(panel);
    }

    // ── Network Diagram ─────────────────────────────────────────────────

    // ── BGP Panel ─────────────────────────────────────────────────────────

    /// <summary>Sync BGP from the selected switch.</summary>
    private async void SyncBgpButton_ItemClick(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)
    {
        if (_syncService == null) return;
        var sw = SwitchGridPanel.Grid.SelectedItem as SwitchRecord;
        if (sw == null && BgpGridPanel.Grid.SelectedItem is BgpRecord bgpSel)
            sw = VM.Switches.FirstOrDefault(s => s.Id == bgpSel.SwitchId);
        if (sw == null) { VM.StatusText = "Select a switch to sync BGP from"; return; }

        VM.StatusText = $"Syncing BGP from {sw.Hostname}…";
        var result = await _syncService.SyncBgpAsync(sw);
        VM.StatusText = result.Message;
        if (result.Success) await VM.LoadPanelDataAsync("bgp", force: true);
    }

    /// <summary>Sync BGP from ALL switches that have management IPs.</summary>
    private async void SyncAllBgpButton_ItemClick(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)
    {
        if (_syncService == null) return;
        if (!VM.Switches.Any(s => !string.IsNullOrEmpty(s.ManagementIp)))
        { VM.StatusText = "No switches with management IPs"; return; }

        VM.StatusText = $"Syncing BGP from {VM.Switches.Count} switches…";
        var (success, fail) = await _syncService.SyncAllBgpAsync(VM.Switches, msg => VM.StatusText = msg);
        VM.StatusText = $"BGP sync complete: {success} synced, {fail} failed";
        await VM.LoadPanelDataAsync("bgp", force: true);
    }

    // Diagram button handlers → wired via DiagramGridPanel events

    private List<string> GetSelectedSites()
        => VM.SiteSummaries.Where(s => s.IsSelected).Select(s => s.Building).ToList();

    // Diagram builder logic → Module.Routing/Views/DiagramPanel.xaml.cs
    private async Task BuildNetworkDiagramAsync()
        => await DiagramGridPanel.BuildDiagramAsync(
            VM.Devices, GetSelectedSites(),
            VM.Repo.GetP2PLinksAsync, VM.Repo.GetB2BLinksAsync);

    // ── Detail Tab Order Persistence ────────────────────────────────────

    private async Task SaveDetailTabOrderAsync()
    {
        try
        {
            var order = new List<string>();
            foreach (var item in DetailTabsPanel.Tabs.Items)
            {
                if (item is DevExpress.Xpf.Core.DXTabItem tab && tab.Header is string header)
                    order.Add(header);
            }
            var csv = string.Join(",", order);
            await _layout.SavePreferenceAsync(PreferenceKeys.DetailTabOrder, csv);
        }
        catch (Exception ex)
        {
            AppLogger.LogException("Layout", ex, "SaveDetailTabOrderAsync");
        }
    }

    private async Task RestoreDetailTabOrderAsync()
    {
        try
        {
            var csv = await _layout.GetPreferenceAsync(PreferenceKeys.DetailTabOrder);
            if (string.IsNullOrEmpty(csv)) return;

            var order = csv.Split(',').ToList();
            var tabs = DetailTabsPanel.Tabs.Items.Cast<DevExpress.Xpf.Core.DXTabItem>().ToList();
            var sorted = new List<DevExpress.Xpf.Core.DXTabItem>();

            foreach (var name in order)
            {
                var tab = tabs.FirstOrDefault(t => t.Header is string h && h == name);
                if (tab != null)
                {
                    sorted.Add(tab);
                    tabs.Remove(tab);
                }
            }
            sorted.AddRange(tabs); // append any new tabs not in saved order

            DetailTabsPanel.Tabs.Items.Clear();
            foreach (var tab in sorted) DetailTabsPanel.Tabs.Items.Add(tab);
        }
        catch (Exception ex)
        {
            AppLogger.LogException("Layout", ex, "RestoreDetailTabOrderAsync");
        }
    }

    // ── Config Builder ──────────────────────────────────────────────────

    private readonly ConfigBuilderService _configBuilder = new();
    private bool _builderComboWired;

    private void WireBuilderCombo()
    {
        if (_builderComboWired) return;
        BuilderGridPanel.DeviceCombo.ItemsSource = VM.Devices;
        BuilderGridPanel.DeviceCombo.DisplayMember = "SwitchName";
        BuilderGridPanel.DeviceCombo.ValueMember = "SwitchName";
        _builderComboWired = true;
    }

    private async void BuilderDeviceCombo_EditValueChanged(object sender, DevExpress.Xpf.Editors.EditValueChangedEventArgs e)
    {
        var device = BuilderGridPanel.DeviceCombo.SelectedItem as DeviceRecord;
        VM.BuilderDevice = device;
        if (device == null)
        {
            VM.BuilderSections.Clear();
            BuilderGridPanel.PreviewBox.Document = new System.Windows.Documents.FlowDocument();
            BuilderGridPanel.LineCountText = "";
            BuilderGridPanel.StatusText = "";
            return;
        }

        try
        {
            BuilderGridPanel.StatusText = "Loading selections…";
            var saved = await VM.Repo.GetBuilderSelectionsAsync(device.SwitchName);
            var sections = _configBuilder.BuildSectionsForDevice(device, VM, saved);

            VM.BuilderSections.Clear();
            foreach (var s in sections) VM.BuilderSections.Add(s);

            RegenerateBuilderPreview();
            BuilderGridPanel.StatusText = $"{sections.Count} sections · {device.Building} · ASN {device.Asn}";
        }
        catch (Exception ex)
        {
            AppLogger.LogException("Builder", ex, "BuilderDeviceCombo_EditValueChanged");
            BuilderGridPanel.StatusText = $"Error: {ex.Message}";
        }
    }

    private void BuilderSection_Toggled(object sender, RoutedEventArgs e) => RegenerateBuilderPreview();
    private void BuilderItem_Toggled(object sender, RoutedEventArgs e) => RegenerateBuilderPreview();

    private void BuilderGenerate_ItemClick(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)
        => RegenerateBuilderPreview();

    private void RegenerateBuilderPreview()
    {
        if (VM.BuilderDevice == null) return;
        try
        {
            var lines = _configBuilder.Generate(VM.BuilderDevice, VM.BuilderSections.ToList(), VM);
            BuilderGridPanel.PreviewBox.Document = BuildColorCodedConfigDoc(lines);
            BuilderGridPanel.LineCountText = $"{lines.Count} lines";
        }
        catch (Exception ex)
        {
            AppLogger.LogException("Builder", ex, "RegenerateBuilderPreview");
        }
    }

    private System.Windows.Documents.FlowDocument BuildColorCodedConfigDoc(List<ConfigLine> lines)
    {
        var doc = new System.Windows.Documents.FlowDocument
        {
            FontFamily = new System.Windows.Media.FontFamily("Cascadia Mono, Consolas, Courier New"),
            FontSize = 11,
            PageWidth = 6000
        };
        var gutterWidth = lines.Count.ToString().Length;
        var sectionColors = VM.BuilderSections.ToDictionary(s => s.Key, s => s.ColorHex);

        for (int i = 0; i < lines.Count; i++)
        {
            var lineNum = (i + 1).ToString().PadLeft(gutterWidth);
            var para = new System.Windows.Documents.Paragraph { Margin = new Thickness(0) };

            // Line number gutter
            para.Inlines.Add(new System.Windows.Documents.Run($"{lineNum}  ")
            {
                Foreground = new System.Windows.Media.SolidColorBrush(
                    System.Windows.Media.Color.FromRgb(0x55, 0x55, 0x55))
            });

            // Config text with section color
            var colorHex = sectionColors.GetValueOrDefault(lines[i].SectionKey, "#D4D4D4");
            System.Windows.Media.Brush brush;
            try { brush = new System.Windows.Media.SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(colorHex)); }
            catch { brush = System.Windows.Media.Brushes.LightGray; }

            para.Inlines.Add(new System.Windows.Documents.Run(lines[i].Text) { Foreground = brush });
            doc.Blocks.Add(para);
        }
        return doc;
    }

    private void BuilderCopy_ItemClick(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e) => CopyBuilderConfig();
    private void BuilderCopy_Click(object sender, RoutedEventArgs e) => CopyBuilderConfig();

    private void CopyBuilderConfig()
    {
        if (VM.BuilderDevice == null) return;
        var lines = _configBuilder.Generate(VM.BuilderDevice, VM.BuilderSections.ToList(), VM);
        var text = string.Join("\n", lines.Select(l => l.Text));
        System.Windows.Clipboard.SetText(text);
        VM.StatusText = $"Config copied ({lines.Count} lines)  ·  {DateTime.Now:HH:mm:ss}";
    }

    private void BuilderDownload_ItemClick(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e) => DownloadBuilderConfig();
    private void BuilderDownload_Click(object sender, RoutedEventArgs e) => DownloadBuilderConfig();

    private void DownloadBuilderConfig()
    {
        if (VM.BuilderDevice == null) return;
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            FileName = $"{VM.BuilderDevice.SwitchName}.txt",
            Filter = "Text files|*.txt|All files|*.*"
        };
        if (dlg.ShowDialog() == true)
        {
            var lines = _configBuilder.Generate(VM.BuilderDevice, VM.BuilderSections.ToList(), VM);
            System.IO.File.WriteAllText(dlg.FileName, string.Join("\n", lines.Select(l => l.Text)) + "\n");
            VM.StatusText = $"Config saved to {dlg.FileName}  ·  {DateTime.Now:HH:mm:ss}";
        }
    }

    private void BuilderEnableAll_ItemClick(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)
    {
        foreach (var s in VM.BuilderSections)
        {
            s.IsEnabled = true;
            foreach (var i in s.Items) i.IsEnabled = true;
        }
        RegenerateBuilderPreview();
    }

    private void BuilderDisableAll_ItemClick(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)
    {
        foreach (var s in VM.BuilderSections) s.IsEnabled = false;
        RegenerateBuilderPreview();
    }

    private async void BuilderSaveSelections_ItemClick(object sender, DevExpress.Xpf.Bars.ItemClickEventArgs e)
    {
        if (VM.BuilderDevice == null) return;
        var selections = new List<(string, string, bool)>();
        foreach (var section in VM.BuilderSections)
        {
            selections.Add((section.Key, "", section.IsEnabled));
            foreach (var item in section.Items)
                selections.Add((section.Key, item.Key, item.IsEnabled));
        }
        await VM.Repo.SaveBuilderSelectionsAsync(VM.BuilderDevice.SwitchName, selections);
        VM.StatusText = $"Builder selections saved for {VM.BuilderDevice.SwitchName}  ·  {DateTime.Now:HH:mm:ss}";
        AppLogger.Audit("Builder", $"Saved {selections.Count} selections for {VM.BuilderDevice.SwitchName}");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // GLOBAL ADMIN — ViewModel-driven panels + context tab wiring
    // ═══════════════════════════════════════════════════════════════════════

    private bool _globalAdminBound;
    private static Task Audit(string action, string? entityType = null, string? entityId = null, object? details = null)
        => Central.Module.GlobalAdmin.Services.GlobalAdminAuditService.LogAsync(action, entityType, entityId, details);

    /// <summary>Navigate to a Global Admin panel and select a tenant by slug.</summary>
    private void NavigateToTenant(string slug)
    {
        VM.IsGlobalTenantsPanelOpen = true;
        BindGlobalAdminGrids();
        _tenantsVm?.RefreshCommand.Execute(null);
        // Select after data loads
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, () =>
        {
            var match = _tenantsVm?.Items.FirstOrDefault(t => t.Slug == slug);
            if (match != null) _tenantsVm!.CurrentItem = match;
        });
    }

    /// <summary>Navigate to Global Users panel and select a user by email.</summary>
    private void NavigateToGlobalUser(string email)
    {
        VM.IsGlobalUsersPanelOpen = true;
        BindGlobalAdminGrids();
        _globalUsersVm?.RefreshCommand.Execute(null);
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Background, () =>
        {
            var match = _globalUsersVm?.Items.FirstOrDefault(u => u.Email == email);
            if (match != null) _globalUsersVm!.CurrentItem = match;
        });
    }

    /// <summary>Refresh all Global Admin grids that are currently open.</summary>
    private void CascadeRefreshGlobalAdmin()
    {
        if (VM.IsGlobalTenantsPanelOpen) _tenantsVm?.RefreshCommand.Execute(null);
        if (VM.IsGlobalUsersPanelOpen) _globalUsersVm?.RefreshCommand.Execute(null);
        if (VM.IsGlobalSubscriptionsPanelOpen) _subscriptionsVm?.RefreshCommand.Execute(null);
        if (VM.IsGlobalLicensesPanelOpen) _licensesVm?.RefreshCommand.Execute(null);
    }

    /// <summary>Validate tenant slug format (lowercase alphanumeric + hyphens, 3-50 chars).</summary>
    private static bool ValidateSlug(string slug, out string error)
    {
        error = "";
        if (string.IsNullOrWhiteSpace(slug)) { error = "Slug is required"; return false; }
        if (slug.Length < 3 || slug.Length > 50) { error = "Slug must be 3-50 characters"; return false; }
        if (!System.Text.RegularExpressions.Regex.IsMatch(slug, @"^[a-z0-9][a-z0-9-]*[a-z0-9]$"))
        { error = "Slug must be lowercase alphanumeric with hyphens only"; return false; }
        return true;
    }
    private Central.Module.GlobalAdmin.ViewModels.TenantsListViewModel? _tenantsVm;
    private Central.Module.GlobalAdmin.ViewModels.GlobalUsersListViewModel? _globalUsersVm;
    private Central.Module.GlobalAdmin.ViewModels.SubscriptionsListViewModel? _subscriptionsVm;
    private Central.Module.GlobalAdmin.ViewModels.ModuleLicensesListViewModel? _licensesVm;

    private void BindGlobalAdminGrids()
    {
        if (_globalAdminBound) return;
        Central.Module.GlobalAdmin.Services.GlobalAdminAuditService.Initialize(VM.Repo);

        // ── Tenants ViewModel ──
        _tenantsVm = new Central.Module.GlobalAdmin.ViewModels.TenantsListViewModel
        {
            Loader = () => VM.Repo.GetTenantsTypedAsync(),
            Inserter = async t => { var id = await VM.Repo.CreateTenantTypedAsync(t); t.Id = id; _ = Audit("tenant_created", "tenant", t.Slug); return 1; },
            Updater = async t => { await VM.Repo.UpdateTenantAsync(t); _ = Audit("tenant_updated", "tenant", t.Slug); },
            Deleter = async t => { await VM.Repo.DeleteTenantAsync(t.Id); _ = Audit("tenant_deleted", "tenant", t.Slug); },
            OnSuspend = async t =>
            {
                await VM.Repo.SuspendTenantByIdAsync(t.Id);
                t.IsActive = false;
                _ = Audit("tenant_suspended", "tenant", t.Slug);
                NotificationService.Instance.Success($"Tenant '{t.Slug}' suspended");
            },
            OnActivate = async t =>
            {
                await VM.Repo.ActivateTenantByIdAsync(t.Id);
                t.IsActive = true;
                _ = Audit("tenant_activated", "tenant", t.Slug);
                NotificationService.Instance.Success($"Tenant '{t.Slug}' activated");
            },
            OnProvisionSchema = async t =>
            {
                if (t.Slug == "default") { NotificationService.Instance.Warning("Cannot provision default tenant"); return; }
                var schemaManager = new Central.Tenancy.TenantSchemaManager(App.Dsn);
                var migrationsDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "migrations");
                await schemaManager.ProvisionTenantAsync($"tenant_{t.Slug}", migrationsDir);
                _ = Audit("schema_provisioned", "tenant", t.Slug);
                NotificationService.Instance.Success($"Schema tenant_{t.Slug} provisioned");
            }
        };
        TenantsGridPanel.Grid.ItemsSource = _tenantsVm.Items;

        // ── Global Users ViewModel ──
        _globalUsersVm = new Central.Module.GlobalAdmin.ViewModels.GlobalUsersListViewModel
        {
            Loader = () => VM.Repo.GetGlobalUsersTypedAsync(),
            Deleter = async u => { await VM.Repo.DeleteGlobalUserAsync(u.Id); _ = Audit("user_removed", "user", u.Email); },
            OnToggleAdmin = async u =>
            {
                await VM.Repo.ToggleGlobalAdminByIdAsync(u.Id);
                u.IsGlobalAdmin = !u.IsGlobalAdmin;
                _ = Audit("admin_toggled", "user", u.Email, new { is_admin = u.IsGlobalAdmin });
                NotificationService.Instance.Success($"Admin {(u.IsGlobalAdmin ? "granted" : "revoked")} for {u.Email}");
            },
            OnResetPassword = async u => ShowResetPasswordDialog(u)
        };
        GlobalUsersGridPanel.Grid.ItemsSource = _globalUsersVm.Items;

        // ── Subscriptions ViewModel ──
        _subscriptionsVm = new Central.Module.GlobalAdmin.ViewModels.SubscriptionsListViewModel
        {
            Loader = () => VM.Repo.GetSubscriptionsTypedAsync(),
            OnChangePlan = async sub =>
            {
                ShowAssignPlanDialog(); // opens dialog for the selected tenant
            },
            OnCancel = async sub =>
            {
                if (System.Windows.MessageBox.Show($"Cancel subscription for {sub.TenantSlug}?", "Confirm",
                    MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
                await VM.Repo.CancelSubscriptionAsync(sub.Id);
                sub.Status = "cancelled";
                NotificationService.Instance.Success($"Subscription cancelled for {sub.TenantSlug}");
            }
        };
        SubscriptionsGridPanel.Grid.ItemsSource = _subscriptionsVm.Items;

        // ── Module Licenses ViewModel ──
        _licensesVm = new Central.Module.GlobalAdmin.ViewModels.ModuleLicensesListViewModel
        {
            Loader = () => VM.Repo.GetLicensesTypedAsync(),
            OnGrantModule = () => { ShowGrantModuleDialog(); return Task.CompletedTask; },
            OnRevokeModule = async lic =>
            {
                if (lic.IsBase) { NotificationService.Instance.Warning("Cannot revoke base module"); return; }
                if (System.Windows.MessageBox.Show($"Revoke {lic.ModuleName} from {lic.TenantSlug}?", "Confirm",
                    MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
                await VM.Repo.RevokeModuleLicenseAsync(lic.Id);
                _licensesVm!.Items.Remove(lic);
                NotificationService.Instance.Success($"License revoked: {lic.ModuleName} from {lic.TenantSlug}");
            }
        };
        LicensesGridPanel.Grid.ItemsSource = _licensesVm.Items;

        // ── Context tab button wiring ──
        GATenantNewBtn.ItemClick += (_, _) => _tenantsVm.AddCommand.Execute(null);
        GATenantEditBtn.ItemClick += (_, _) => ShowTenantDetailDialog(isEdit: true);
        GATenantDeleteBtn.ItemClick += (_, _) => _tenantsVm.DeleteCommand.Execute(null);
        GATenantSuspendBtn.ItemClick += (_, _) => _tenantsVm.SuspendCommand.Execute(null);
        GATenantActivateBtn.ItemClick += (_, _) => _tenantsVm.ActivateCommand.Execute(null);
        GATenantProvisionBtn.ItemClick += (_, _) => _tenantsVm.ProvisionSchemaCommand.Execute(null);
        GATenantWizardBtn.ItemClick += (_, _) => ShowTenantSetupWizard();
        GATenantRefreshBtn.ItemClick += (_, _) => _tenantsVm.RefreshCommand.Execute(null);
        GATenantExportBtn.ItemClick += (_, _) => _tenantsVm.ExportCommand.Execute(null);

        GAUserInviteBtn.ItemClick += (_, _) => ShowInviteUserDialog();
        GAUserEditBtn.ItemClick += (_, _) => ShowManageMembershipsDialog();
        GAUserDeleteBtn.ItemClick += (_, _) => _globalUsersVm.DeleteCommand.Execute(null);
        GAUserToggleAdminBtn.ItemClick += (_, _) => _globalUsersVm.ToggleAdminCommand.Execute(null);
        GAUserResetPwdBtn.ItemClick += (_, _) => _globalUsersVm.ResetPasswordCommand.Execute(null);
        GAUserRefreshBtn.ItemClick += (_, _) => _globalUsersVm.RefreshCommand.Execute(null);
        GAUserExportBtn.ItemClick += (_, _) => _globalUsersVm.ExportCommand.Execute(null);

        GASubAssignBtn.ItemClick += (_, _) => ShowAssignPlanDialog();
        GASubChangePlanBtn.ItemClick += (_, _) => _subscriptionsVm.ChangePlanCommand.Execute(null);
        GASubCancelBtn.ItemClick += (_, _) => _subscriptionsVm.CancelSubscriptionCommand.Execute(null);
        GASubRefreshBtn.ItemClick += (_, _) => _subscriptionsVm.RefreshCommand.Execute(null);
        GASubExportBtn.ItemClick += (_, _) => _subscriptionsVm.ExportCommand.Execute(null);

        GALicGrantBtn.ItemClick += (_, _) => _licensesVm.GrantModuleCommand.Execute(null);
        GALicRevokeBtn.ItemClick += (_, _) => _licensesVm.RevokeModuleCommand.Execute(null);
        GALicRefreshBtn.ItemClick += (_, _) => _licensesVm.RefreshCommand.Execute(null);
        GALicExportBtn.ItemClick += (_, _) => _licensesVm.ExportCommand.Execute(null);

        // ── Cross-panel navigation: double-click tenant slug in Subscriptions/Licenses ──
        SubscriptionsGridPanel.Grid.MouseDoubleClick += (_, _) =>
        {
            if (_subscriptionsVm?.CurrentItem?.TenantSlug is string slug && !string.IsNullOrEmpty(slug))
                NavigateToTenant(slug);
        };
        LicensesGridPanel.Grid.MouseDoubleClick += (_, _) =>
        {
            if (_licensesVm?.CurrentItem?.TenantSlug is string slug && !string.IsNullOrEmpty(slug))
                NavigateToTenant(slug);
        };

        _globalAdminBound = true;
    }

    /// <summary>Load data for the active Global Admin ViewModel.</summary>
    private void LoadGlobalAdminPanelAsync(string panelKey)
    {
        BindGlobalAdminGrids();
        switch (panelKey)
        {
            case "global_tenants":
                _tenantsVm!.RefreshCommand.Execute(null);
                break;
            case "global_users":
                _globalUsersVm!.RefreshCommand.Execute(null);
                break;
            case "global_subscriptions":
                _subscriptionsVm!.RefreshCommand.Execute(null);
                break;
            case "global_licenses":
                _licensesVm!.RefreshCommand.Execute(null);
                break;
        }
    }

    // ── Global Admin dialogs ──────────────────────────────────────────

    private void ShowTenantDetailDialog(bool isEdit = false)
    {
        var tenant = isEdit ? _tenantsVm?.CurrentItem : new Central.Core.Models.TenantRecord { Tier = "free" };
        if (tenant == null) return;

        var dlg = new Central.Module.GlobalAdmin.Views.Dialogs.TenantDetailDialog(tenant, isEdit) { Owner = this };
        dlg.LoadTenantDetails = async id =>
        {
            var subs = await VM.Repo.GetTenantSubscriptionsAsync(id);
            var lics = await VM.Repo.GetTenantModulesAsync(id);
            var members = await VM.Repo.GetTenantMembersAsync(id);
            return (subs, lics, members);
        };

        if (dlg.ShowDialogWindow()?.IsDefault == true)
        {
            // Validate slug on new tenants
            if (!isEdit && !ValidateSlug(tenant.Slug, out var slugErr))
            {
                NotificationService.Instance.Warning(slugErr);
                return;
            }
            if (string.IsNullOrWhiteSpace(tenant.DisplayName))
            {
                NotificationService.Instance.Warning("Display name is required");
                return;
            }

            if (isEdit)
            {
                _ = Task.Run(async () =>
                {
                    await VM.Repo.UpdateTenantAsync(tenant);
                    _ = Audit("tenant_updated", "tenant", tenant.Slug);
                    Dispatcher.Invoke(() => { _tenantsVm!.RefreshCommand.Execute(null); CascadeRefreshGlobalAdmin(); });
                });
            }
            else
            {
                _ = Task.Run(async () =>
                {
                    tenant.Id = await VM.Repo.CreateTenantTypedAsync(tenant);
                    _ = Audit("tenant_created", "tenant", tenant.Slug);
                    Dispatcher.Invoke(() =>
                    {
                        _tenantsVm!.Items.Insert(0, tenant);
                        NotificationService.Instance.Success($"Tenant '{tenant.Slug}' created");
                    });
                });
            }
        }
    }

    private async void ShowAssignPlanDialog()
    {
        // Get the tenant from either the Tenants grid or the Subscriptions grid
        var tenantId = _tenantsVm?.CurrentItem?.Id ?? _subscriptionsVm?.CurrentItem?.TenantId ?? Guid.Empty;
        var tenantSlug = _tenantsVm?.CurrentItem?.Slug ?? _subscriptionsVm?.CurrentItem?.TenantSlug ?? "";
        if (tenantId == Guid.Empty) { NotificationService.Instance.Warning("Select a tenant first"); return; }

        var plans = await VM.Repo.GetPlanItemsAsync();
        var dlg = new Central.Module.GlobalAdmin.Views.Dialogs.AssignPlanDialog(tenantSlug, plans) { Owner = this };
        if (dlg.ShowDialogWindow()?.IsDefault == true && dlg.SelectedPlanId > 0)
        {
            await VM.Repo.CreateSubscriptionAsync(tenantId, dlg.SelectedPlanId, dlg.SelectedStatus, dlg.SelectedExpiry);
            _subscriptionsVm!.RefreshCommand.Execute(null);
            _tenantsVm!.RefreshCommand.Execute(null); // plan_name may have changed
            _ = Audit("plan_assigned", "subscription", tenantSlug, new { plan_id = dlg.SelectedPlanId, status = dlg.SelectedStatus });
            CascadeRefreshGlobalAdmin();
            NotificationService.Instance.Success($"Plan assigned to {tenantSlug}");
        }
    }

    private async void ShowGrantModuleDialog()
    {
        var tenantId = _tenantsVm?.CurrentItem?.Id ?? _licensesVm?.CurrentItem?.TenantId ?? Guid.Empty;
        var tenantSlug = _tenantsVm?.CurrentItem?.Slug ?? _licensesVm?.CurrentItem?.TenantSlug ?? "";
        if (tenantId == Guid.Empty) { NotificationService.Instance.Warning("Select a tenant first"); return; }

        var modules = await VM.Repo.GetModuleItemsAsync(tenantId);
        var dlg = new Central.Module.GlobalAdmin.Views.Dialogs.GrantModuleDialog(tenantSlug, modules) { Owner = this };
        if (dlg.ShowDialogWindow()?.IsDefault == true && dlg.SelectedModuleIds.Count > 0)
        {
            await VM.Repo.BulkGrantModulesAsync(tenantId, dlg.SelectedModuleIds, dlg.SelectedExpiry);
            _licensesVm!.RefreshCommand.Execute(null);
            _ = Audit("modules_granted", "license", tenantSlug, new { count = dlg.SelectedModuleIds.Count });
            NotificationService.Instance.Success($"Granted {dlg.SelectedModuleIds.Count} module(s) to {tenantSlug}");
        }
    }

    private async void ShowInviteUserDialog()
    {
        var tenants = await VM.Repo.GetTenantOptionsAsync();
        var dlg = new Central.Module.GlobalAdmin.Views.Dialogs.InviteUserDialog(tenants) { Owner = this };
        if (dlg.ShowDialogWindow()?.IsDefault != true || !dlg.Validate()) return;

        var salt = Central.Core.Auth.PasswordHasher.GenerateSalt();
        var hash = Central.Core.Auth.PasswordHasher.Hash(dlg.Password, salt);
        var userId = await VM.Repo.CreateGlobalUserAsync(dlg.Email, dlg.DisplayName, hash, salt, dlg.IsGlobalAdmin);

        // Add tenant memberships
        foreach (var tid in dlg.SelectedTenantIds)
            await VM.Repo.AddTenantMembershipAsync(userId, tid, "Viewer");

        _globalUsersVm!.RefreshCommand.Execute(null);
        _ = Audit("user_invited", "user", dlg.Email, new { admin = dlg.IsGlobalAdmin, tenants = dlg.SelectedTenantIds.Count });
        NotificationService.Instance.Success($"User {dlg.Email} invited");
    }

    private async void ShowManageMembershipsDialog()
    {
        var user = _globalUsersVm?.CurrentItem;
        if (user == null) return;

        var memberships = await VM.Repo.GetUserMembershipsAsync(user.Id);
        var tenants = await VM.Repo.GetTenantOptionsAsync();
        var dlg = new Central.Module.GlobalAdmin.Views.Dialogs.ManageMembershipsDialog(user.Id, user.Email, memberships, tenants) { Owner = this };
        dlg.OnAddMembership = (uid, tid, role) => VM.Repo.AddTenantMembershipAsync(uid, tid, role);
        dlg.OnRemoveMembership = id => VM.Repo.RemoveTenantMembershipAsync(id);
        dlg.OnChangeRole = (id, role) => VM.Repo.UpdateMembershipRoleAsync(id, role);
        dlg.ShowDialogWindow();

        // Refresh to pick up changed tenant counts
        _globalUsersVm.RefreshCommand.Execute(null);
    }

    private async void ShowResetPasswordDialog(Central.Core.Models.GlobalUserRecord user)
    {
        var dlg = new Central.Module.GlobalAdmin.Views.Dialogs.ResetPasswordDialog(user.Email) { Owner = this };
        if (dlg.ShowDialogWindow()?.IsDefault != true || !dlg.Validate()) return;

        var salt = Central.Core.Auth.PasswordHasher.GenerateSalt();
        var hash = Central.Core.Auth.PasswordHasher.Hash(dlg.NewPassword, salt);
        await VM.Repo.ResetGlobalUserPasswordAsync(user.Id, hash, salt, dlg.ForceEmailVerification);
        if (dlg.ForceEmailVerification) user.EmailVerified = false;
        _ = Audit("password_reset", "user", user.Email);
        NotificationService.Instance.Success($"Password reset for {user.Email}");
    }

    private async void ShowTenantSetupWizard()
    {
        var plans = await VM.Repo.GetPlanItemsAsync();
        var modules = await VM.Repo.GetModuleItemsAsync();
        var wizard = new Central.Module.GlobalAdmin.Views.Dialogs.TenantSetupWizard(plans, modules) { Owner = this };
        wizard.CreateTenant = (slug, name, domain, tier) => VM.Repo.CreateTenantTypedAsync(new Central.Core.Models.TenantRecord { Slug = slug, DisplayName = name, Domain = domain, Tier = tier });
        wizard.CreateSubscription = (tid, pid, status, exp) => VM.Repo.CreateSubscriptionAsync(tid, pid, status, exp);
        wizard.BulkGrantModules = (tid, mids, exp) => VM.Repo.BulkGrantModulesAsync(tid, mids, exp);
        wizard.ProvisionSchema = async slug =>
        {
            var sm = new Central.Tenancy.TenantSchemaManager(App.Dsn);
            var dir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "migrations");
            await sm.ProvisionTenantAsync($"tenant_{slug}", dir);
        };
        wizard.CreateUser = async (email, name, hash, salt, admin) => await VM.Repo.CreateGlobalUserAsync(email, name, hash, salt, admin);
        wizard.AddMembership = (uid, tid, role) => VM.Repo.AddTenantMembershipAsync(uid, tid, role);
        wizard.ShowDialogWindow();

        if (wizard.Provisioned)
        {
            _ = Central.Module.GlobalAdmin.Services.GlobalAdminAuditService.LogAsync("tenant_provisioned", "tenant", "", new { wizard = true });
            _tenantsVm?.RefreshCommand.Execute(null);
            _subscriptionsVm?.RefreshCommand.Execute(null);
            _licensesVm?.RefreshCommand.Execute(null);
            _globalUsersVm?.RefreshCommand.Execute(null);
            NotificationService.Instance.Success("Tenant provisioned via wizard");
        }
    }

    private async Task LoadGlobalAdminAuditAsync()
    {
        try
        {
            var entries = await VM.Repo.GetGlobalAdminAuditAsync();
            AuditGridPanel.LoadData(entries);
        }
        catch (Exception ex) { VM.StatusText = $"Audit log: {ex.Message}"; }
    }

    private async Task LoadPlatformDashboardAsync()
    {
        try
        {
            var (total, active, users, verified, subs) = await VM.Repo.GetPlatformMetricsAsync();
            PlatformDashPanel.UpdateMetrics(total, active, users, verified, subs);

            // Load chart data
            var tierData = await VM.Repo.GetSubscriptionDistributionAsync();
            PlatformDashPanel.UpdateTierChart(tierData);

            var usersData = await VM.Repo.GetTopTenantsByUsersAsync();
            PlatformDashPanel.UpdateUsersByTenantChart(usersData);

            var moduleData = await VM.Repo.GetModuleAdoptionAsync();
            PlatformDashPanel.UpdateModuleAdoptionChart(moduleData);
        }
        catch (Exception ex) { VM.StatusText = $"Dashboard: {ex.Message}"; }
    }
}
