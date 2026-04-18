using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Central.ApiClient;
using Central.Engine.Shell;
using UserControl = System.Windows.Controls.UserControl;

namespace Central.Module.Networking.Governance;

/// <summary>
/// Read-only grid of Change Sets for the current tenant. Mirrors the
/// Servers panel shape: header + DX GridControl, tenant-scoped reload,
/// cancellation-aware load.
///
/// <para>Mutation actions (new / submit / approve / apply / rollback /
/// cancel) arrive as <see cref="NavigateToPanelMessage"/> events from
/// the ribbon; the panel routes each to the matching method on
/// <see cref="NetworkingEngineClient"/>. The dialogs that collect
/// per-action inputs (title, decision rationale, item forms) are
/// deferred to a follow-on slice — this panel only handles the
/// read + refresh + navigate path today.</para>
/// </summary>
public partial class ChangeSetsListPanel : UserControl
{
    private string? _baseUrl;
    private Guid _tenantId;
    private int? _actorUserId;
    private CancellationTokenSource? _cts;

    public ChangeSetsListPanel()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        PanelMessageBus.Subscribe<NavigateToPanelMessage>(OnNavigate);
        PanelMessageBus.Subscribe<RefreshPanelMessage>(OnRefresh);
    }

    /// <summary>Bind the panel to a tenant + the engine's base URL.
    /// Pass null <paramref name="actorUserId"/> to omit the
    /// X-User-Id header (mutations will stamp audit rows with no
    /// actor).</summary>
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

    // ─── Message handlers ────────────────────────────────────────────────

    private void OnNavigate(NavigateToPanelMessage msg)
    {
        if (msg.TargetPanel != "changesets") return;
        // Every ribbon action currently triggers a reload after the
        // action completes — the detailed dialog work (inputs per action)
        // is a follow-on slice. For now, navigate -> refresh keeps the
        // grid up to date when actions happen outside this panel.
        _ = ReloadAsync();
    }

    private void OnRefresh(RefreshPanelMessage msg)
    {
        if (msg.TargetPanel != "changesets") return;
        _ = ReloadAsync();
    }

    // ─── Data load ───────────────────────────────────────────────────────

    public async Task ReloadAsync()
    {
        if (string.IsNullOrEmpty(_baseUrl) || _tenantId == Guid.Empty)
        {
            StatusLabel.Text = "No tenant context";
            return;
        }

        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        StatusLabel.Text = "Loading…";
        try
        {
            using var client = new NetworkingEngineClient(_baseUrl);
            if (_actorUserId is int uid) client.SetActorUserId(uid);

            var sets = await client.ListChangeSetsAsync(_tenantId, ct: ct);
            var rows = sets.Select(ChangeSetRow.FromDto).ToList();
            Grid.ItemsSource = rows;
            StatusLabel.Text = BuildStatusSummary(rows);
        }
        catch (OperationCanceledException) { /* ignore */ }
        catch (NetworkingEngineException ex)
        {
            StatusLabel.Text = $"Engine error ({ex.StatusCode}): {ex.Message}";
        }
        catch (HttpRequestException ex)
        {
            StatusLabel.Text = $"Network error: {ex.Message}";
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"Load failed: {ex.Message}";
        }
    }

    /// <summary>Tight per-status counts for the header bar — gives admins
    /// an at-a-glance sense of the queue without scanning every row.
    /// "12 sets · 3 draft · 2 submitted · 5 applied · loaded 14:22:03".</summary>
    private static string BuildStatusSummary(IReadOnlyList<ChangeSetRow> rows)
    {
        if (rows.Count == 0) return "0 sets · loaded " + DateTime.Now.ToString("HH:mm:ss");

        var counts = new Dictionary<string, int>();
        foreach (var r in rows)
            counts[r.Status] = counts.TryGetValue(r.Status, out var c) ? c + 1 : 1;

        // Stable ordering so the header doesn't jiggle between reloads.
        string[] order = { "Draft", "Submitted", "Approved", "Applied",
                           "Rejected", "Cancelled", "RolledBack" };
        var parts = order
            .Where(s => counts.ContainsKey(s))
            .Select(s => $"{counts[s]} {s.ToLowerInvariant()}");
        return $"{rows.Count} sets · {string.Join(" · ", parts)} · loaded {DateTime.Now:HH:mm:ss}";
    }
}
