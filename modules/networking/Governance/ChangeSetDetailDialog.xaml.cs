using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using Central.ApiClient;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;

namespace Central.Module.Networking.Governance;

/// <summary>
/// Read-only detail inspector for a single Change Set. Loads both
/// tabs in parallel — items from <see cref="NetworkingEngineClient.GetChangeSetAsync"/>
/// and the full audit stream filtered by the Set's
/// <c>correlation_id</c> via <see cref="NetworkingEngineClient.ListAuditAsync"/>.
///
/// <para>The audit tab is the interesting one: every lifecycle event
/// (Drafted / ItemAdded / Submitted / ApprovalRecorded / Applied /
/// RolledBack / Renamed / Created / ...) threaded by one correlation id
/// lives here. Admin can scroll through the complete forensic history
/// of the Set — including the entity-level mutations landed during
/// apply — without bouncing to a separate audit panel.</para>
/// </summary>
public partial class ChangeSetDetailDialog : DevExpress.Xpf.Core.DXWindow
{
    private readonly string _baseUrl;
    private readonly Guid _tenantId;
    private readonly int? _actorUserId;
    private readonly Guid _setId;
    private Guid? _correlationId;

    public ChangeSetDetailDialog(string baseUrl, Guid tenantId, int? actorUserId,
        ChangeSetRow row)
    {
        InitializeComponent();
        _baseUrl = baseUrl;
        _tenantId = tenantId;
        _actorUserId = actorUserId;
        _setId = row.Id;

        TitleLabel.Text = row.Title;
        SubtitleLabel.Text = $"{row.Status} · {row.ItemCount} item{(row.ItemCount == 1 ? "" : "s")} · id {row.Id}";
        Loaded += async (_, _) => await LoadAsync();
    }

    private async void OnRefresh(object sender, RoutedEventArgs e) => await LoadAsync();

    /// <summary>Server-side CSV export for the audit slice threaded by
    /// this Set's correlation_id. Engine does the formatting — we just
    /// stream the body to a file the admin picks. Useful for handing a
    /// change-set history to auditors without pasting screenshots.</summary>
    private async void OnExportAudit(object sender, RoutedEventArgs e)
    {
        if (_correlationId is not Guid cid)
        {
            MessageBox.Show("No data loaded yet.",
                "Wait", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var save = new Microsoft.Win32.SaveFileDialog
        {
            FileName = $"changeset-audit-{_setId}-{DateTime.Now:yyyyMMdd-HHmmss}.csv",
            Filter = "CSV file (*.csv)|*.csv|All files (*.*)|*.*",
            DefaultExt = ".csv",
        };
        if (save.ShowDialog() != true) return;

        ExportAuditButton.IsEnabled = false;
        StatusLabel.Text = "Exporting audit CSV…";
        try
        {
            using var client = new NetworkingEngineClient(_baseUrl);
            if (_actorUserId is int uid) client.SetActorUserId(uid);

            // Server-side CSV: audit.rs::render_csv. The ExportQuery's
            // correlationId filter narrows to just this Set's history.
            var csv = await client.ExportAuditAsync(_tenantId, "csv",
                entityType: null, entityId: null,
                fromAt: null, toAt: null, limit: null,
                correlationId: cid);
            File.WriteAllText(save.FileName, csv);
            StatusLabel.Text = $"Exported to {save.FileName}";
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"Export failed: {ex.Message}";
        }
        finally { ExportAuditButton.IsEnabled = true; }
    }

    private void OnClose(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private async Task LoadAsync()
    {
        StatusLabel.Text = "Loading…";
        RefreshButton.IsEnabled = false;
        try
        {
            using var client = new NetworkingEngineClient(_baseUrl);
            if (_actorUserId is int uid) client.SetActorUserId(uid);

            // Detail first — we need correlation_id to fetch audit.
            var detail = await client.GetChangeSetAsync(_setId, _tenantId);
            _correlationId = detail.CorrelationId;

            RenderLifecycle(detail);
            RenderItems(detail.Items);

            // Approvals + audit load concurrently — they're independent
            // and both short, so parallelism halves the wall-clock wait.
            var approvalsTask = client.ListApprovalsAsync(_setId, _tenantId);
            var auditTask = client.ListAuditAsync(new ListAuditRequest
            {
                OrganizationId = _tenantId,
                CorrelationId = _correlationId,
                Limit = 500,
            });

            var approvals = await approvalsTask;
            RenderApprovals(approvals);

            var audit = await auditTask;
            // The list endpoint returns DESC; flip to ASC for reading order.
            RenderAudit(audit.OrderBy(a => a.SequenceId).ToList());

            StatusLabel.Text = $"{detail.Items.Count} item{(detail.Items.Count == 1 ? "" : "s")} · " +
                                $"{approvals.Count} approval{(approvals.Count == 1 ? "" : "s")} · " +
                                $"{audit.Count} audit row{(audit.Count == 1 ? "" : "s")} · " +
                                $"loaded {DateTime.Now:HH:mm:ss}";
        }
        catch (NetworkingEngineException ex) { StatusLabel.Text = $"Engine error ({ex.StatusCode}): {ex.Message}"; }
        catch (HttpRequestException ex)     { StatusLabel.Text = $"Network error: {ex.Message}"; }
        catch (Exception ex)                { StatusLabel.Text = $"Load failed: {ex.Message}"; }
        finally { RefreshButton.IsEnabled = true; }
    }

    /// <summary>Flatten the lifecycle stamps into one readable line so
    /// the admin sees the whole story without squinting at timestamp
    /// columns.</summary>
    private void RenderLifecycle(ChangeSetDetailDto d)
    {
        var bits = new List<string>();
        bits.Add($"Created {d.CreatedAt:yyyy-MM-dd HH:mm} by {d.RequestedByDisplay ?? "—"}");
        if (d.SubmittedAt is { } s)    bits.Add($"submitted {s:HH:mm}");
        if (d.ApprovedAt is { } a)     bits.Add($"approved {a:HH:mm}");
        if (d.AppliedAt is { } ap)     bits.Add($"applied {ap:HH:mm}");
        if (d.RolledBackAt is { } rb)  bits.Add($"rolled back {rb:HH:mm}");
        if (d.CancelledAt is { } c)    bits.Add($"cancelled {c:HH:mm}");
        if (d.RequiredApprovals is int req)
            bits.Add($"required approvals: {req}");
        LifecycleLabel.Text = string.Join("  ·  ", bits);
    }

    private void RenderItems(IReadOnlyList<ChangeSetItemDto> items)
    {
        ItemsGrid.ItemsSource = items.Select(i => new ItemRow
        {
            ItemOrder = i.ItemOrder,
            EntityType = i.EntityType,
            EntityId = i.EntityId?.ToString() ?? "",
            Action = i.Action,
            ExpectedVersion = i.ExpectedVersion,
            AppliedAt = i.AppliedAt,
            ApplyError = i.ApplyError ?? "",
            Notes = i.Notes ?? "",
        }).ToList();
    }

    private void RenderApprovals(IReadOnlyList<ApprovalDto> approvals)
    {
        ApprovalsGrid.ItemsSource = approvals.Select(a => new ApprovalDisplayRow
        {
            DecidedAt = a.DecidedAt,
            ApproverDisplay = a.ApproverDisplay ?? "",
            ApproverUserId = a.ApproverUserId,
            Decision = a.Decision,
            Notes = a.Notes ?? "",
        }).ToList();
    }

    private void RenderAudit(IReadOnlyList<AuditRowDto> audit)
    {
        AuditGrid.ItemsSource = audit.Select(a => new AuditDisplayRow
        {
            SequenceId = a.SequenceId,
            CreatedAt = a.CreatedAt,
            EntityType = a.EntityType,
            EntityId = a.EntityId?.ToString() ?? "",
            Action = a.Action,
            ActorDisplay = a.ActorDisplay ?? (a.ActorUserId?.ToString() ?? ""),
            DetailsText = SummariseDetails(a.Details),
        }).ToList();
    }

    /// <summary>Flatten the details JSON into a single readable line.
    /// The engine emits a handful of structured keys per action — pick
    /// the ones admins most want to see (from / to / asn / vlan_id /
    /// approve_count / items_applied / ...). Anything else dumps as
    /// compact JSON.</summary>
    private static string SummariseDetails(object? details)
    {
        if (details is null) return "";
        try
        {
            string raw;
            if (details is JsonElement je) raw = je.GetRawText();
            else raw = JsonSerializer.Serialize(details);
            if (string.IsNullOrWhiteSpace(raw) || raw == "{}") return "";

            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return raw;

            var parts = new List<string>();
            foreach (var prop in root.EnumerateObject())
            {
                // Skip the plumbing keys — admins don't care that the
                // item id was threaded through.
                if (prop.Name is "change_set_item_id") continue;
                var v = prop.Value.ValueKind switch
                {
                    JsonValueKind.String => prop.Value.GetString() ?? "",
                    JsonValueKind.Null   => "null",
                    _                    => prop.Value.GetRawText(),
                };
                parts.Add($"{prop.Name}={v}");
                if (parts.Count >= 6) break; // cap so wide rows stay scannable
            }
            return string.Join(" · ", parts);
        }
        catch
        {
            return details.ToString() ?? "";
        }
    }

    // Row view models — kept internal to the dialog. DevExpress grid
    // binds by field name, so these are just DTO shapes with
    // display-friendly column names.
    private sealed class ItemRow
    {
        public int ItemOrder { get; set; }
        public string EntityType { get; set; } = "";
        public string EntityId { get; set; } = "";
        public string Action { get; set; } = "";
        public int? ExpectedVersion { get; set; }
        public DateTime? AppliedAt { get; set; }
        public string ApplyError { get; set; } = "";
        public string Notes { get; set; } = "";
    }

    private sealed class ApprovalDisplayRow
    {
        public DateTime DecidedAt { get; set; }
        public string ApproverDisplay { get; set; } = "";
        public int ApproverUserId { get; set; }
        public string Decision { get; set; } = "";
        public string Notes { get; set; } = "";
    }

    private sealed class AuditDisplayRow
    {
        public long SequenceId { get; set; }
        public DateTime CreatedAt { get; set; }
        public string EntityType { get; set; } = "";
        public string EntityId { get; set; } = "";
        public string Action { get; set; } = "";
        public string ActorDisplay { get; set; } = "";
        public string DetailsText { get; set; } = "";
    }
}
