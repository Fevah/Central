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
        switch (msg.SelectItem as string)
        {
            case "action:runQuery":    _ = RunQueryAsync(); break;
            case "action:verifyChain": _ = VerifyChainAsync(); break;
            case "action:exportCsv":   _ = ExportCsvAsync(); break;
            default:                   _ = ReloadAsync(); break;
        }
    }

    private void OnRefresh(RefreshPanelMessage msg)
    {
        if (msg.TargetPanel != "audit") return;
        _ = ReloadAsync();
    }

    private void OnRun(object sender, RoutedEventArgs e) => _ = RunQueryAsync();

    public Task ReloadAsync() => RunQueryAsync();

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

        return new ListAuditRequest
        {
            OrganizationId = _tenantId,
            EntityType = Empty2Null(EntityTypeCombo.Text),
            Action = Empty2Null(ActionBox.Text),
            ActorUserId = actorUserId,
            CorrelationId = correlationId,
            FromAt = FromDate.DateTime == DateTime.MinValue ? null : FromDate.DateTime.ToUniversalTime(),
            ToAt = ToDate.DateTime == DateTime.MinValue ? null : ToDate.DateTime.ToUniversalTime(),
            Limit = 500,
        };
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
