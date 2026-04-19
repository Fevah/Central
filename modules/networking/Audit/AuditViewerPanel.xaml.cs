using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Central.ApiClient;
using Central.Engine.Shell;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;
using UserControl = System.Windows.Controls.UserControl;

namespace Central.Module.Networking.Audit;

/// <summary>
/// Standalone tenant-wide audit browser. Filter by entity type / action /
/// actor / date range / correlation id, run the query, walk results,
/// verify the chain, export CSV. Wraps every capability of
/// <see cref="NetworkingEngineClient.ListAuditAsync"/> +
/// <see cref="NetworkingEngineClient.VerifyAuditChainAsync"/> +
/// <see cref="NetworkingEngineClient.ExportAuditAsync"/> in one panel.
///
/// <para>Distinct from the audit tab inside the Change Set detail dialog,
/// which is correlation-scoped to one Set. This panel is the tenant-wide
/// forensics entry point — search for any mutation regardless of which
/// Change Set (if any) triggered it.</para>
/// </summary>
public partial class AuditViewerPanel : UserControl
{
    private string? _baseUrl;
    private Guid _tenantId;
    private int? _actorUserId;
    private CancellationTokenSource? _cts;
    private IReadOnlyList<AuditRowDto> _lastResult = Array.Empty<AuditRowDto>();

    // Entity-id filter now lives in the visible EntityIdBox in the
    // filter bar. The private _pendingEntityId of earlier revisions
    // was invisible to operators — drilled-in state was easy to
    // lose. The box is the source of truth; cross-panel drill-down
    // writes into it via ApplyEntityDrillDown.

    // Static list of entity types seen in the wild — drives the combo's
    // autocomplete. Keeps in step with the engine's audit stamps
    // (Device / Link / Server / Vlan / AsnAllocation / MlagDomain /
    // Subnet / IpAddress / ChangeSet / ChangeSetItem / NamingOverride /
    // ReservationShelf). A typo still works — the combo is editable so
    // admins can filter on entity types we haven't yet listed here.
    private static readonly string[] KnownEntityTypes = new[]
    {
        "", "Device", "Link", "Server", "Vlan", "AsnAllocation",
        "MlagDomain", "Subnet", "IpAddress", "ChangeSet", "ChangeSetItem",
        "NamingOverride", "ReservationShelf", "AsnBlock", "VlanBlock",
        "IpPool", "Building", "Site", "Region", "Rack",
    };

    public AuditViewerPanel()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        PanelMessageBus.Subscribe<NavigateToPanelMessage>(OnNavigate);
        PanelMessageBus.Subscribe<RefreshPanelMessage>(OnRefresh);
        EntityTypeCombo.ItemsSource = KnownEntityTypes;
    }

    public void SetContext(string baseUrl, Guid tenantId, int? actorUserId = null)
    {
        _baseUrl = baseUrl;
        _tenantId = tenantId;
        _actorUserId = actorUserId;
        if (IsLoaded) _ = ReloadAsync();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_baseUrl) && _tenantId != Guid.Empty)
            _ = ReloadAsync();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
        _cts = null;
    }

    // ─── Message routing ─────────────────────────────────────────────────

    private void OnNavigate(NavigateToPanelMessage msg)
    {
        if (msg.TargetPanel != "audit") return;
        switch (msg.SelectItem)
        {
            case "action:runQuery":    _ = RunQueryAsync(); break;
            case "action:verifyChain": _ = VerifyChainAsync(); break;
            case "action:exportCsv":   _ = ExportCsvAsync(); break;
            // Cross-panel drill-down payload: "selectEntity:{type}:{guid}"
            // — e.g. from the SearchPanel context menu. Set the filter
            // fields + pending id, then auto-run. Operator can widen
            // the search back out by editing the filter bar; the first
            // such edit clears _pendingEntityId.
            case string s when s.StartsWith("selectEntity:", StringComparison.Ordinal):
                ApplyEntityDrillDown(s);
                break;
            default:                   _ = ReloadAsync(); break;
        }
    }

    /// <summary>Parse "selectEntity:Device:{guid}" — set the filter
    /// bar (entity type + entity id), run the query. Silently no-ops
    /// on a malformed payload rather than popping a dialog; the
    /// worst outcome is the existing filter state stays + nothing
    /// auto-runs.</summary>
    internal void ApplyEntityDrillDown(string payload)
    {
        // payload shape: "selectEntity:{type}:{guid}"
        var parts = payload.Split(':', 3);
        if (parts.Length != 3) return;
        var entityType = parts[1];
        if (!Guid.TryParse(parts[2], out var entityId)) return;

        EntityTypeCombo.EditValue = entityType;
        EntityIdBox.Text = entityId.ToString();
        // Clear the other filter inputs so the drill-down is what
        // the operator sees — not last session's stale action box.
        ActionBox.Text = "";
        ActorBox.Text = "";
        CorrelationBox.Text = "";
        _ = RunQueryAsync();
    }

    private void OnRefresh(RefreshPanelMessage msg)
    {
        if (msg.TargetPanel != "audit") return;
        _ = ReloadAsync();
    }

    private void OnRun(object sender, RoutedEventArgs e) => _ = RunQueryAsync();

    public Task ReloadAsync() => RunQueryAsync();

    /// <summary>Double-click an audit row to drill into the Change Set
    /// that produced it, if any. Rows without a correlation_id (or where
    /// the correlation doesn't match a Set — ad-hoc entity audits from
    /// e.g. allocation retires) get a polite info prompt instead of a
    /// silent no-op.</summary>
    private async void OnAuditRowDoubleClick(object sender,
        DevExpress.Xpf.Grid.RowDoubleClickEventArgs e)
    {
        // Map back to the underlying DTO via the grid's current index —
        // we built the ItemsSource from _lastResult so indexes line up.
        var disp = AuditGrid.CurrentItem as AuditDisplayRow;
        if (disp is null) return;
        var source = _lastResult.FirstOrDefault(r => r.SequenceId == disp.SequenceId);
        if (source is null) return;

        if (source.CorrelationId is not Guid cid)
        {
            MessageBox.Show(
                "This audit row has no correlation id — it wasn't produced by a Change Set.",
                "No Change Set to drill into",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        await DrillIntoSetByCorrelationAsync(cid);
    }

    private async Task DrillIntoSetByCorrelationAsync(Guid correlationId)
    {
        if (string.IsNullOrEmpty(_baseUrl) || _tenantId == Guid.Empty) return;
        try
        {
            using var client = new NetworkingEngineClient(_baseUrl);
            if (_actorUserId is int uid) client.SetActorUserId(uid);

            var detail = await client.GetChangeSetByCorrelationAsync(correlationId, _tenantId);
            if (detail is null)
            {
                MessageBox.Show(
                    $"No Change Set found for correlation id {correlationId}. The row likely came from an entity-level audit outside any Set (e.g. a direct allocation retire).",
                    "Not linked to a Change Set",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Project the ChangeSetDetailDto into the ChangeSetRow shape
            // the Governance detail dialog expects. The dialog owns its
            // own reload from the engine, so we only need enough here
            // to drive the header + the initial fetch.
            var row = new Central.Module.Networking.Governance.ChangeSetRow
            {
                Id = detail.Id,
                Title = detail.Title,
                Status = detail.Status,
                ItemCount = detail.ItemCount,
                RequestedByDisplay = detail.RequestedByDisplay,
                RequiredApprovals = detail.RequiredApprovals,
                CreatedAt = detail.CreatedAt,
                SubmittedAt = detail.SubmittedAt,
                ApprovedAt = detail.ApprovedAt,
                AppliedAt = detail.AppliedAt,
                RolledBackAt = detail.RolledBackAt,
                CancelledAt = detail.CancelledAt,
                Version = detail.Version,
            };
            var dialog = new Central.Module.Networking.Governance.ChangeSetDetailDialog(
                _baseUrl!, _tenantId, _actorUserId, row)
            {
                Owner = Window.GetWindow(this),
            };
            dialog.ShowDialog();
        }
        catch (NetworkingEngineException ex)
        {
            MessageBox.Show($"Engine error ({ex.StatusCode}): {ex.Message}",
                "Drill-down failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch (HttpRequestException ex)
        {
            MessageBox.Show($"Network error: {ex.Message}",
                "Drill-down failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed: {ex.Message}",
                "Drill-down failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ─── Run query ──────────────────────────────────────────────────────

    private async Task RunQueryAsync()
    {
        if (string.IsNullOrEmpty(_baseUrl) || _tenantId == Guid.Empty)
        {
            StatusLabel.Text = "No tenant context";
            return;
        }

        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        RunButton.IsEnabled = false;
        StatusLabel.Text = "Loading…";
        try
        {
            var req = BuildRequest();
            using var client = new NetworkingEngineClient(_baseUrl);
            if (_actorUserId is int uid) client.SetActorUserId(uid);

            var rows = await client.ListAuditAsync(req, ct);
            _lastResult = rows;
            AuditGrid.ItemsSource = rows.Select(ToDisplay).ToList();
            StatusLabel.Text = $"{rows.Count} row{(rows.Count == 1 ? "" : "s")} · " +
                               $"loaded {DateTime.Now:HH:mm:ss}";
        }
        catch (OperationCanceledException) { /* ignore */ }
        catch (NetworkingEngineException ex) { StatusLabel.Text = $"Engine error ({ex.StatusCode}): {ex.Message}"; }
        catch (HttpRequestException ex)     { StatusLabel.Text = $"Network error: {ex.Message}"; }
        catch (Exception ex)                { StatusLabel.Text = $"Query failed: {ex.Message}"; }
        finally { RunButton.IsEnabled = true; }
    }

    /// <summary>Project the filter bar into a ListAuditRequest. Empty
    /// strings → null filters so the engine doesn't see
    /// <c>entity_type = ""</c> (which would match nothing).</summary>
    private ListAuditRequest BuildRequest()
    {
        int? actorUserId = null;
        if (int.TryParse(ActorBox.Text?.Trim(), out var a)) actorUserId = a;

        Guid? correlationId = null;
        if (Guid.TryParse(CorrelationBox.Text?.Trim(), out var c)) correlationId = c;

        // Entity id from the visible box — operators can see, edit,
        // clear it directly. Invalid UUIDs are treated as no-filter
        // rather than rejected so the run button works even when
        // they're mid-typing.
        Guid? entityId = null;
        if (Guid.TryParse(EntityIdBox.Text?.Trim(), out var eid)) entityId = eid;

        return new ListAuditRequest
        {
            OrganizationId = _tenantId,
            EntityType = Empty2Null(EntityTypeCombo.Text),
            EntityId = entityId,
            Action = Empty2Null(ActionBox.Text),
            ActorUserId = actorUserId,
            CorrelationId = correlationId,
            FromAt = FromDate.DateTime == DateTime.MinValue ? null : FromDate.DateTime.ToUniversalTime(),
            ToAt = ToDate.DateTime == DateTime.MinValue ? null : ToDate.DateTime.ToUniversalTime(),
            Limit = 500,
        };
    }

    /// <summary>Clear every filter input back to blank so the next
    /// Run Query returns the full tenant-scope set. Explicit button
    /// because drill-down state isn't always obvious — an operator
    /// who drilled in from a search two screens ago shouldn't have
    /// to hunt for the entity id box to widen their view.</summary>
    private void OnClearFilters(object sender, RoutedEventArgs e)
    {
        EntityTypeCombo.EditValue = null;
        EntityIdBox.Text = "";
        ActionBox.Text = "";
        ActorBox.Text = "";
        FromDate.DateTime = DateTime.MinValue;
        ToDate.DateTime = DateTime.MinValue;
        CorrelationBox.Text = "";
        StatusLabel.Text = "Filters cleared — hit Run Query to refresh.";
    }

    private static string? Empty2Null(string? s)
        => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    // ─── Verify chain ───────────────────────────────────────────────────

    private async Task VerifyChainAsync()
    {
        if (string.IsNullOrEmpty(_baseUrl) || _tenantId == Guid.Empty) return;

        StatusLabel.Text = "Verifying chain…";
        try
        {
            using var client = new NetworkingEngineClient(_baseUrl);
            if (_actorUserId is int uid) client.SetActorUserId(uid);

            var result = await client.VerifyAuditChainAsync(_tenantId);
            var dialog = new ChainVerifyResultDialog(result)
            {
                Owner = Window.GetWindow(this),
            };
            dialog.ShowDialog();

            StatusLabel.Text = result.Ok
                ? $"Chain verified: {result.RowsChecked} rows, no mismatches · checked {DateTime.Now:HH:mm:ss}"
                : $"Chain TAMPERED: {result.Mismatches.Count} mismatch(es) in {result.RowsChecked} rows · checked {DateTime.Now:HH:mm:ss}";
        }
        catch (NetworkingEngineException ex) { StatusLabel.Text = $"Engine error ({ex.StatusCode}): {ex.Message}"; }
        catch (HttpRequestException ex)     { StatusLabel.Text = $"Network error: {ex.Message}"; }
        catch (Exception ex)                { StatusLabel.Text = $"Verify failed: {ex.Message}"; }
    }

    // ─── Export CSV ─────────────────────────────────────────────────────

    /// <summary>Server-side CSV export using the current filter set —
    /// the engine emits the CSV directly so large exports stream without
    /// local buffering. Filename defaults to audit-YYYYMMDD-HHmmss.csv.</summary>
    private async Task ExportCsvAsync()
    {
        if (string.IsNullOrEmpty(_baseUrl) || _tenantId == Guid.Empty) return;

        var save = new Microsoft.Win32.SaveFileDialog
        {
            FileName = $"audit-{DateTime.Now:yyyyMMdd-HHmmss}.csv",
            Filter = "CSV file (*.csv)|*.csv|All files (*.*)|*.*",
            DefaultExt = ".csv",
        };
        if (save.ShowDialog() != true) return;

        StatusLabel.Text = "Exporting CSV…";
        try
        {
            var req = BuildRequest();
            using var client = new NetworkingEngineClient(_baseUrl);
            if (_actorUserId is int uid) client.SetActorUserId(uid);
            var csv = await client.ExportAuditAsync(_tenantId, "csv",
                entityType: req.EntityType,
                entityId: req.EntityId,
                fromAt: req.FromAt, toAt: req.ToAt,
                limit: req.Limit,
                correlationId: req.CorrelationId);
            File.WriteAllText(save.FileName, csv);
            StatusLabel.Text = $"Exported to {save.FileName}";
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"Export failed: {ex.Message}";
        }
    }

    // ─── Display projection ─────────────────────────────────────────────

    /// <summary>Same shape as the Change Set detail dialog's audit tab.
    /// Keeps the two views visually interchangeable for admins used to
    /// one or the other.</summary>
    private static AuditDisplayRow ToDisplay(AuditRowDto r) => new()
    {
        SequenceId = r.SequenceId,
        CreatedAt = r.CreatedAt,
        EntityType = r.EntityType,
        EntityId = r.EntityId?.ToString() ?? "",
        Action = r.Action,
        ActorDisplay = r.ActorDisplay ?? (r.ActorUserId?.ToString() ?? ""),
        CorrelationId = r.CorrelationId?.ToString() ?? "",
        DetailsText = SummariseDetails(r.Details),
    };

    private static string SummariseDetails(object? details)
    {
        if (details is null) return "";
        try
        {
            string raw = details is JsonElement je ? je.GetRawText() : JsonSerializer.Serialize(details);
            if (string.IsNullOrWhiteSpace(raw) || raw == "{}") return "";

            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return raw;

            var parts = new List<string>();
            foreach (var prop in root.EnumerateObject())
            {
                if (prop.Name is "change_set_item_id") continue;
                var v = prop.Value.ValueKind switch
                {
                    JsonValueKind.String => prop.Value.GetString() ?? "",
                    JsonValueKind.Null   => "null",
                    _                    => prop.Value.GetRawText(),
                };
                parts.Add($"{prop.Name}={v}");
                if (parts.Count >= 6) break;
            }
            return string.Join(" · ", parts);
        }
        catch { return details.ToString() ?? ""; }
    }

    private sealed class AuditDisplayRow
    {
        public long SequenceId { get; set; }
        public DateTime CreatedAt { get; set; }
        public string EntityType { get; set; } = "";
        public string EntityId { get; set; } = "";
        public string Action { get; set; } = "";
        public string ActorDisplay { get; set; } = "";
        public string CorrelationId { get; set; } = "";
        public string DetailsText { get; set; } = "";
    }
}
