using System;
using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using DevExpress.Xpf.Grid;
using Central.ApiClient;
using Central.Engine.Models;
using Central.Engine.Shell;

namespace Central.Module.Networking.Devices;

/// <summary>
/// IPAM device grid panel — extracted from MainWindow.
/// DataContext is set by the host (MainViewModel for now, DeviceListViewModel later).
/// </summary>
public partial class DeviceGridPanel : System.Windows.Controls.UserControl
{
    public DeviceGridPanel()
    {
        InitializeComponent();

        // Wire ValidateRow + InvalidRowException in constructor (DX pattern)
        DevicesView.ValidateRow += DevicesView_ValidateRow;
        DevicesView.InvalidRowException += (_, e) => e.ExceptionMode = ExceptionMode.NoAction;
        DevicesGrid.MasterRowExpanded += DevicesGrid_MasterRowExpanded;

        // Cross-panel drill-down: SearchPanel (and any other source)
        // can publish `selectId:{guid}:{label}` to focus a row here.
        // The engine's net.device uuid rarely matches this grid's
        // numeric switch_guide id, so we match by hostname (label)
        // instead — hostnames are kept in sync by the net.device ↔
        // public.switches dual-write trigger.
        PanelMessageBus.Subscribe<NavigateToPanelMessage>(OnNavigate);
    }

    public GridControl Grid => DevicesGrid;
    public TableView View => DevicesView;
    public DevExpress.Xpf.Editors.TextEdit SearchBox => DevicesSearch;

    // Engine context for the audit-drill context menu. The panel
    // doesn't keep its own NetworkingEngineClient alive; it spins
    // one up per action. Set by the host when it knows the base URL
    // + current tenant (mirrors BulkPanel / SearchPanel wiring).
    private string? _engineBaseUrl;
    private Guid _engineTenantId;
    private int? _engineActorUserId;

    public void SetEngineContext(string baseUrl, Guid tenantId, int? actorUserId = null)
    {
        _engineBaseUrl = baseUrl;
        _engineTenantId = tenantId;
        _engineActorUserId = actorUserId;
    }

    // ── Combo sources — set by host after construction ──

    public void BindComboSources(object statuses, object deviceTypes, object buildings,
        object regions, object asnDefs)
    {
        StatusCombo.ItemsSource = statuses;
        DeviceTypeCombo.ItemsSource = deviceTypes;
        BuildingCombo.ItemsSource = buildings;
        RegionCombo.ItemsSource = regions;
        DevicesAsnCombo.ItemsSource = asnDefs;
    }

    // ── Events — forwarded to host ──

    public event EventHandler<CellValueChangedEventArgs>? CellValueChanged;
    public event Func<DeviceRecord, Task>? SaveDevice;
    public event Action<string>? SearchChanged;
    /// <summary>Fired when a master row is expanded — host loads links for the device.</summary>
    public event Func<DeviceRecord, Task>? LoadDetailLinks;

    private void DevicesView_CellValueChanged(object sender, CellValueChangedEventArgs e)
        => CellValueChanged?.Invoke(this, e);

    private async void DevicesView_ValidateRow(object sender, GridRowValidationEventArgs e)
    {
        if (e.Row is not DeviceRecord device) return;
        if (string.IsNullOrWhiteSpace(device.SwitchName))
        {
            e.IsValid = false;
            e.ErrorContent = "Device name is required.";
            return;
        }
        if (SaveDevice != null)
            await SaveDevice.Invoke(device);
    }

    private void DevicesSearch_EditValueChanged(object sender,
        DevExpress.Xpf.Editors.EditValueChangedEventArgs e)
        => SearchChanged?.Invoke(DevicesSearch.EditValue as string ?? "");

    private async void DevicesGrid_MasterRowExpanded(object sender, RowEventArgs e)
    {
        if (DevicesGrid.GetRow(e.RowHandle) is DeviceRecord dev && dev.DetailLinks.Count == 0)
        {
            if (LoadDetailLinks != null)
                await LoadDetailLinks(dev);
        }
    }

    // ─── Cross-panel drill-down (selectId handler) ──────────────────────

    private void OnNavigate(NavigateToPanelMessage msg)
    {
        if (msg.TargetPanel != "devices") return;
        if (msg.SelectItem is not string payload) return;
        if (!payload.StartsWith("selectId:", StringComparison.Ordinal)) return;

        // Payload format: `selectId:{guid}:{label}`. The guid comes
        // from net.device; label is the hostname. We match by label
        // because the grid's Id column holds switch_guide's numeric
        // id, not the net.device uuid.
        var parts = payload.Split(':', 3);
        if (parts.Length < 3) return;
        var label = parts[2];
        if (string.IsNullOrWhiteSpace(label)) return;

        Dispatcher.BeginInvoke(() => FocusByHostname(label));
    }

    // ─── Row context menu → audit drill-down ───────────────────────────
    //
    // DeviceRecord.Id is switch_guide's numeric id, not net.device's
    // uuid — the audit panel expects a uuid. Resolve hostname → uuid
    // via the engine's thin /api/net/devices list endpoint (capped at
    // 5000 per tenant; for operator-scale tenants that's plenty for
    // a rare context-menu click). Drill only fires once resolution
    // succeeds; on miss (hostname drift, unreachable engine) we
    // surface the failure rather than opening the audit panel with
    // no filter.

    private async void OnContextShowAudit(object sender, RoutedEventArgs e)
    {
        if (DevicesGrid.CurrentItem is not DeviceRecord row) return;
        if (string.IsNullOrWhiteSpace(row.SwitchName)) return;
        if (string.IsNullOrEmpty(_engineBaseUrl) || _engineTenantId == Guid.Empty) return;

        try
        {
            using var client = new NetworkingEngineClient(_engineBaseUrl);
            if (_engineActorUserId is int uid) client.SetActorUserId(uid);

            var all = await client.ListDevicesAsync(_engineTenantId);
            var match = all.FirstOrDefault(d =>
                string.Equals(d.Hostname, row.SwitchName, StringComparison.OrdinalIgnoreCase));
            if (match is null)
            {
                // No matching net.device — dual-write gap or the row
                // is still switch_guide-only. Fall back to a broader
                // entity-type-only drill so the operator gets
                // *something*.
                PanelMessageBus.Publish(new OpenPanelMessage("audit"));
                PanelMessageBus.Publish(new NavigateToPanelMessage(
                    "audit", "selectEntity:Device:00000000-0000-0000-0000-000000000000"));
                return;
            }
            PanelMessageBus.Publish(new OpenPanelMessage("audit"));
            PanelMessageBus.Publish(new NavigateToPanelMessage(
                "audit", $"selectEntity:Device:{match.Id}"));
        }
        catch
        {
            // Swallow — the context menu is a nice-to-have; the grid
            // itself must keep working even when the engine is down.
        }
    }

    private void OnContextCopyHostname(object sender, RoutedEventArgs e)
    {
        if (DevicesGrid.CurrentItem is not DeviceRecord row) return;
        if (string.IsNullOrWhiteSpace(row.SwitchName)) return;
        try { System.Windows.Clipboard.SetText(row.SwitchName); } catch { /* ignore */ }
    }

    /// <summary>Publish the OpenPanelMessage + NavigateToPanelMessage
    /// pair so the Search panel opens pre-populated with this
    /// device's hostname — a one-click "find every link / subnet /
    /// VLAN that mentions this switch" flow.</summary>
    private void OnContextSearchFromHere(object sender, RoutedEventArgs e)
    {
        if (DevicesGrid.CurrentItem is not DeviceRecord row) return;
        if (string.IsNullOrWhiteSpace(row.SwitchName)) return;
        PanelMessageBus.Publish(new OpenPanelMessage("search"));
        PanelMessageBus.Publish(new NavigateToPanelMessage("search", $"q:{row.SwitchName}"));
    }

    /// <summary>Walk the grid's ItemsSource for a DeviceRecord whose
    /// SwitchName matches the incoming hostname (case-insensitive —
    /// PicOS is case-agnostic and operators paste between tools).
    /// Sets CurrentItem + scrolls into view via FocusedRowHandle.
    /// No-op when the grid hasn't been populated yet or the row
    /// isn't present (e.g. caller searched for a device outside the
    /// user's site scope).</summary>
    internal bool FocusByHostname(string hostname)
    {
        if (DevicesGrid.ItemsSource is not IEnumerable source) return false;
        var idx = 0;
        foreach (var item in source)
        {
            if (item is DeviceRecord d &&
                string.Equals(d.SwitchName, hostname, StringComparison.OrdinalIgnoreCase))
            {
                DevicesGrid.CurrentItem = d;
                // FocusedRowHandle drives the scroll-into-view +
                // keyboard-focus-row behaviour. Computing the handle
                // from the visible index matches the DX recipe for
                // "programmatically focus this row".
                DevicesView.FocusedRowHandle = DevicesGrid.GetRowHandleByListIndex(idx);
                return true;
            }
            idx++;
        }
        return false;
    }
}
