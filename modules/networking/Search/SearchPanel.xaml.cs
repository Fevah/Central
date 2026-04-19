using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using Central.ApiClient;
using Central.Engine.Shell;
using UserControl = System.Windows.Controls.UserControl;

namespace Central.Module.Networking.Search;

/// <summary>
/// Global-search workspace. Typed-in free text hits
/// <c>/api/net/search</c> which runs a tsvector UNION across the six
/// tenant-owned entities (migration 107 added partial GIN indexes per
/// entity so this stays cheap as tenant size grows).
///
/// Results render in a flat grid grouped by EntityType by default.
/// Double-clicking a row publishes a <see cref="NavigateToPanelMessage"/>
/// targeting the matching entity panel with a
/// <c>selectId:{guid}</c> payload so the owning panel can focus the
/// row. Panels that don't handle the message (yet) will simply ignore
/// the double-click — the message is a hint, not a contract.
/// </summary>
public partial class SearchPanel : UserControl
{
    private string? _baseUrl;
    private Guid _tenantId;
    private int? _actorUserId;
    private CancellationTokenSource? _cts;

    /// <summary>Entity-type combo suggestions. Operators can pick one
    /// or comma-separate several — the engine accepts any subset of
    /// <c>Device,Vlan,Subnet,Server,Link,DhcpRelayTarget</c>.</summary>
    private static readonly string[] EntityTypeSuggestions = new[]
    {
        "", "Device", "Vlan", "Subnet", "Server", "Link", "DhcpRelayTarget",
        // Common two-type combos operators reach for when narrowing.
        "Device,Vlan", "Vlan,Subnet", "Device,Server",
    };

    public SearchPanel()
    {
        InitializeComponent();
        EntityTypesCombo.ItemsSource = EntityTypeSuggestions;
        StatusLabel.Text = "Type a query and press Enter (or click Search) — empty returns nothing.";
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        PanelMessageBus.Subscribe<NavigateToPanelMessage>(OnNavigate);
    }

    public void SetContext(string baseUrl, Guid tenantId, int? actorUserId = null)
    {
        _baseUrl = baseUrl;
        _tenantId = tenantId;
        _actorUserId = actorUserId;
    }

    private void OnLoaded(object sender, RoutedEventArgs e) { /* no auto-fetch */ }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
        _cts = null;
    }

    private void OnNavigate(NavigateToPanelMessage msg)
    {
        if (msg.TargetPanel != "search") return;
        switch (msg.SelectItem)
        {
            case "action:run":
                _ = RunSearchAsync();
                break;
            case "action:clear":
                QueryBox.Clear();
                EntityTypesCombo.EditValue = "";
                ResultsGrid.ItemsSource = null;
                StatusLabel.Text = "Cleared.";
                break;
            // "q:foo" payload — pre-fill the query box + run. Useful
            // for future "search from here" context-menu items.
            case string payload when payload.StartsWith("q:", StringComparison.Ordinal):
                QueryBox.Text = payload.Substring(2);
                _ = RunSearchAsync();
                break;
        }
    }

    // ─── Toolbar handlers ───────────────────────────────────────────────

    private void OnSearch(object sender, RoutedEventArgs e) => _ = RunSearchAsync();

    private void OnQueryKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            _ = RunSearchAsync();
        }
    }

    private void OnClear(object sender, RoutedEventArgs e)
    {
        QueryBox.Clear();
        EntityTypesCombo.EditValue = "";
        ResultsGrid.ItemsSource = null;
        StatusLabel.Text = "Cleared.";
    }

    // ─── Row handling ───────────────────────────────────────────────────

    private void OnResultDoubleClick(object sender,
        DevExpress.Xpf.Grid.RowDoubleClickEventArgs e)
    {
        if (ResultsGrid.CurrentItem is not SearchResultRow row) return;

        // Route to the entity-specific panel by mapping the engine's
        // entity_type to a NetworkingRibbonRegistrar.Panel* constant.
        // Panels that haven't implemented selectId:{guid} yet simply
        // bring themselves to focus without selecting the row; when
        // they gain the handler, double-click gets richer for free.
        var target = row.EntityType switch
        {
            "Device"          => "devices",
            "Vlan"            => "vlans",
            "Subnet"          => "devices",      // subnets live in IPAM grid
            "Server"          => "servers",
            "Link"            => "links",
            "DhcpRelayTarget" => "devices",      // no dedicated panel yet
            _ => null,
        };
        if (target is null)
        {
            StatusLabel.Text = $"Unknown entity type '{row.EntityType}' — no drill target.";
            return;
        }
        PanelMessageBus.Publish(new NavigateToPanelMessage(target, $"selectId:{row.Id}"));
        StatusLabel.Text = $"Navigated to {target} with selectId:{row.Id}";
    }

    // ─── Run search ─────────────────────────────────────────────────────

    private async Task RunSearchAsync()
    {
        if (string.IsNullOrEmpty(_baseUrl) || _tenantId == Guid.Empty)
        {
            StatusLabel.Text = "No tenant context — set base URL + tenant first.";
            return;
        }

        var q = (QueryBox.Text ?? "").Trim();
        if (q.Length == 0)
        {
            StatusLabel.Text = "Empty query — type something and hit Search.";
            ResultsGrid.ItemsSource = null;
            return;
        }

        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        SearchButton.IsEnabled = false;
        StatusLabel.Text = "Searching…";
        try
        {
            using var client = new NetworkingEngineClient(_baseUrl!);
            if (_actorUserId is int uid) client.SetActorUserId(uid);

            var entityTypes = ParseEntityTypes(EntityTypesCombo.EditValue as string);
            var limit = (int?)LimitSpin.Value;
            var results = await client.GlobalSearchAsync(
                _tenantId, q, entityTypes, limit, ct);

            ResultsGrid.ItemsSource = results
                .Select(r => new SearchResultRow(r.EntityType, r.Id, r.Label,
                                                 r.Snippet, r.Rank))
                .ToList();
            StatusLabel.Text = results.Count == 0
                ? $"No matches for \"{q}\" · {DateTime.Now:HH:mm:ss}"
                : $"{results.Count} match{(results.Count == 1 ? "" : "es")} · " +
                  $"loaded {DateTime.Now:HH:mm:ss}";
        }
        catch (OperationCanceledException) { /* ignore */ }
        catch (NetworkingEngineException ex) { StatusLabel.Text = $"Engine error ({ex.StatusCode}): {ex.Message}"; }
        catch (HttpRequestException ex)     { StatusLabel.Text = $"Network error: {ex.Message}"; }
        catch (Exception ex)                { StatusLabel.Text = $"Search failed: {ex.Message}"; }
        finally { SearchButton.IsEnabled = true; }
    }

    /// <summary>Parse the free-text entity-types value into a list.
    /// Empty / whitespace → null so the client omits the query param
    /// and the engine searches every entity type.</summary>
    internal static IReadOnlyList<string>? ParseEntityTypes(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var list = raw.Split(',')
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .ToList();
        return list.Count == 0 ? null : list;
    }
}

/// <summary>Flat shape for the results grid. Rank is formatted as a
/// float column by default — the grid's own column formatting handles
/// display (the DTO ships ts_rank verbatim).</summary>
internal sealed record SearchResultRow(string EntityType, Guid Id,
    string Label, string Snippet, float Rank);
