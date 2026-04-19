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
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;
using MessageBoxResult = System.Windows.MessageBoxResult;
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
        // Saved views are per-user; refresh the sidebar when the
        // actor context lands so operators see their own views on
        // first open without needing to trigger it manually.
        if (IsLoaded) _ = ReloadSavedViewsAsync();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_baseUrl) && _tenantId != Guid.Empty)
            _ = ReloadSavedViewsAsync();
    }

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
        DevExpress.Xpf.Grid.RowDoubleClickEventArgs e) => OpenEntityPanelForCurrentRow();

    private void OnContextOpenEntity(object sender, RoutedEventArgs e)
        => OpenEntityPanelForCurrentRow();

    /// <summary>Shared by double-click + "Open in entity panel"
    /// context menu. Publishes two messages:
    ///   1. OpenPanelMessage so MainWindow restores the target dock
    ///      panel (matching the audit-drill pattern)
    ///   2. NavigateToPanelMessage with a `selectId:{guid}:{label}`
    ///      payload — subscribing grids parse and use whichever
    ///      identifier they can resolve. The three-segment payload
    ///      covers the mismatch between net.* uuid-keyed entities
    ///      and the legacy switch_guide-backed WPF grids (which
    ///      still carry numeric ids); both sides keep hostname/
    ///      display-name as the disambiguating label.</summary>
    private void OpenEntityPanelForCurrentRow()
    {
        if (ResultsGrid.CurrentItem is not SearchResultRow row) return;

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
        PanelMessageBus.Publish(new OpenPanelMessage(target));
        PanelMessageBus.Publish(new NavigateToPanelMessage(
            target, $"selectId:{row.Id}:{row.Label}"));
        StatusLabel.Text = $"Navigated to {target} with selectId:{row.Id}";
    }

    /// <summary>Cross-panel drill from search result → audit history.
    /// Publishes OpenPanelMessage first so MainWindow flips the
    /// IsAuditPanelOpen VM flag (which restores the Audit dock panel
    /// + auto-reloads), then publishes the selectEntity payload the
    /// Audit panel's OnNavigate consumes to set its entity-id filter.
    /// Two-message pattern because one message can't both open a
    /// panel and drive state inside it — the open happens in
    /// MainWindow, the state-set happens inside the panel's own
    /// handler.</summary>
    private void OnContextShowAudit(object sender, RoutedEventArgs e)
    {
        if (ResultsGrid.CurrentItem is not SearchResultRow row) return;

        PanelMessageBus.Publish(new OpenPanelMessage("audit"));
        PanelMessageBus.Publish(new NavigateToPanelMessage(
            "audit", $"selectEntity:{row.EntityType}:{row.Id}"));
        StatusLabel.Text = $"Drilled into audit for {row.EntityType} {row.Id}";
    }

    private void OnContextCopyId(object sender, RoutedEventArgs e)
    {
        if (ResultsGrid.CurrentItem is not SearchResultRow row) return;
        try
        {
            System.Windows.Clipboard.SetText(row.Id.ToString());
            StatusLabel.Text = $"Copied id {row.Id} to clipboard";
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"Copy failed: {ex.Message}";
        }
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

    // ─── Saved views sidebar ────────────────────────────────────────────

    /// <summary>Reload the caller's saved-view list from the engine.
    /// Silently skips when context isn't set — the panel is happy to
    /// open before SetContext runs. Errors land in the status bar
    /// rather than throwing because the sidebar is a nice-to-have
    /// and shouldn't block search itself.</summary>
    private async Task ReloadSavedViewsAsync()
    {
        if (string.IsNullOrEmpty(_baseUrl) || _tenantId == Guid.Empty) return;
        try
        {
            using var client = new NetworkingEngineClient(_baseUrl);
            if (_actorUserId is int uid) client.SetActorUserId(uid);
            var views = await client.ListSavedViewsAsync(_tenantId);
            SavedViewsList.ItemsSource = views
                .Select(v => new SavedViewRow(v))
                .ToList();
        }
        catch (Exception ex)
        {
            // Non-fatal — sidebar stays empty, search still works.
            StatusLabel.Text = $"Saved views unavailable: {ex.Message}";
        }
    }

    private void OnSavedViewSelected(object sender,
        System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (SavedViewsList.SelectedItem is not SavedViewRow row) return;
        // Populate the query box + entity-types from the view. Filters
        // jsonb isn't rendered yet — that's a richer facet UI that
        // lands when facet state needs more than the two fields here.
        QueryBox.Text = row.Source.Q ?? "";
        EntityTypesCombo.EditValue = row.Source.EntityTypes ?? "";
        // Auto-run so clicking a saved view is one click, not two.
        _ = RunSearchAsync();
    }

    private async void OnSaveView(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_baseUrl) || _tenantId == Guid.Empty) return;

        // Prompt for a name. Prefer the first 40 chars of the current
        // query as the default — matches how operators reach for names
        // ("mep core 02" becomes the view "mep core 02").
        var q = (QueryBox.Text ?? "").Trim();
        var defaultName = q.Length == 0
            ? $"View {DateTime.Now:HH:mm:ss}"
            : (q.Length <= 40 ? q : q.Substring(0, 40));
        var name = TextInputPrompt.Show("Save view", "Name for this view:",
                                        defaultName, Window.GetWindow(this));
        if (string.IsNullOrWhiteSpace(name)) return;

        try
        {
            using var client = new NetworkingEngineClient(_baseUrl);
            if (_actorUserId is int uid) client.SetActorUserId(uid);

            var entityTypesCsv = (EntityTypesCombo.EditValue as string)?.Trim();
            var req = new CreateSavedViewRequest(
                OrganizationId: _tenantId,
                Name: name.Trim(),
                Q: q,
                EntityTypes: string.IsNullOrWhiteSpace(entityTypesCsv) ? null : entityTypesCsv,
                Filters: null,
                Notes: null);
            await client.CreateSavedViewAsync(req);
            await ReloadSavedViewsAsync();
            StatusLabel.Text = $"Saved view '{name}' · {DateTime.Now:HH:mm:ss}";
        }
        catch (NetworkingEngineException ex) { StatusLabel.Text = $"Save failed ({ex.StatusCode}): {ex.Message}"; }
        catch (Exception ex)                 { StatusLabel.Text = $"Save failed: {ex.Message}"; }
    }

    private async void OnDeleteView(object sender, RoutedEventArgs e)
    {
        if (SavedViewsList.SelectedItem is not SavedViewRow row)
        {
            StatusLabel.Text = "Pick a saved view from the sidebar first.";
            return;
        }
        if (string.IsNullOrEmpty(_baseUrl) || _tenantId == Guid.Empty) return;

        var confirm = MessageBox.Show(
            $"Delete saved view '{row.Name}'?",
            "Delete saved view",
            MessageBoxButton.OKCancel, MessageBoxImage.Warning,
            MessageBoxResult.Cancel);
        if (confirm != MessageBoxResult.OK) return;

        try
        {
            using var client = new NetworkingEngineClient(_baseUrl);
            if (_actorUserId is int uid) client.SetActorUserId(uid);
            await client.DeleteSavedViewAsync(row.Source.Id, _tenantId);
            await ReloadSavedViewsAsync();
            StatusLabel.Text = $"Deleted '{row.Name}' · {DateTime.Now:HH:mm:ss}";
        }
        catch (NetworkingEngineException ex) { StatusLabel.Text = $"Delete failed ({ex.StatusCode}): {ex.Message}"; }
        catch (Exception ex)                 { StatusLabel.Text = $"Delete failed: {ex.Message}"; }
    }
}

/// <summary>Sidebar row binding. Keeps the full DTO accessible
/// (.Source) so selection can populate the full set of fields
/// without a second engine round-trip.</summary>
internal sealed class SavedViewRow
{
    public SavedViewRow(SavedViewDto source)
    {
        Source = source;
    }
    public SavedViewDto Source { get; }
    public string Name => Source.Name;
    /// <summary>Second line under the name — query + entity-type
    /// summary so operators can pick the right view without clicking
    /// each one.</summary>
    public string SubtitleText
    {
        get
        {
            var q = string.IsNullOrWhiteSpace(Source.Q) ? "(empty)" : Source.Q;
            if (q.Length > 40) q = q.Substring(0, 40) + "…";
            var types = string.IsNullOrWhiteSpace(Source.EntityTypes)
                ? "all types" : Source.EntityTypes;
            return $"{q} · {types}";
        }
    }
}

/// <summary>Flat shape for the results grid. Rank is formatted as a
/// float column by default — the grid's own column formatting handles
/// display (the DTO ships ts_rank verbatim).</summary>
internal sealed record SearchResultRow(string EntityType, Guid Id,
    string Label, string Snippet, float Rank);

/// <summary>Minimal text-input dialog — DX-free, WPF-only. A shared
/// helper in apps/desktop exists but the networking module doesn't
/// reference desktop; duplicating this ~30-line helper beats a new
/// project reference for one call site.</summary>
internal static class TextInputPrompt
{
    public static string? Show(string title, string prompt, string defaultValue,
        Window? owner)
    {
        var input = new System.Windows.Controls.TextBox
        {
            Text = defaultValue,
            Margin = new Thickness(10, 4, 10, 10),
            MinWidth = 320,
        };
        var ok = new System.Windows.Controls.Button
        {
            Content = "OK", Width = 80, Margin = new Thickness(4),
            IsDefault = true,
        };
        var cancel = new System.Windows.Controls.Button
        {
            Content = "Cancel", Width = 80, Margin = new Thickness(4),
            IsCancel = true,
        };
        var buttons = new System.Windows.Controls.StackPanel
        {
            Orientation = System.Windows.Controls.Orientation.Horizontal,
            HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
            Margin = new Thickness(0, 0, 4, 4),
        };
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);

        var stack = new System.Windows.Controls.StackPanel();
        stack.Children.Add(new System.Windows.Controls.TextBlock
        {
            Text = prompt, Margin = new Thickness(10, 10, 10, 0),
        });
        stack.Children.Add(input);
        stack.Children.Add(buttons);

        var dlg = new Window
        {
            Title = title, Content = stack,
            SizeToContent = SizeToContent.WidthAndHeight,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = owner,
        };
        ok.Click += (_, _) => { dlg.DialogResult = true; dlg.Close(); };

        input.SelectAll();
        input.Focus();
        return dlg.ShowDialog() == true ? input.Text : null;
    }
}
