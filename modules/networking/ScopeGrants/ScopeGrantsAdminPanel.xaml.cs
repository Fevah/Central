using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Central.ApiClient;
using Central.Engine.Shell;
using MessageBox = System.Windows.MessageBox;
using MessageBoxButton = System.Windows.MessageBoxButton;
using MessageBoxImage = System.Windows.MessageBoxImage;
using MessageBoxResult = System.Windows.MessageBoxResult;
using UserControl = System.Windows.Controls.UserControl;

namespace Central.Module.Networking.ScopeGrants;

/// <summary>
/// Admin workspace for the Phase-10 RBAC foundation. Every scope-gated
/// engine endpoint (bulk import / bulk edit / config-gen / DHCP relay
/// CRUD / saved-view writes, etc.) consults <c>net.scope_grant</c> via
/// <c>scope_grants::require_permission</c>. Without a UI, granting
/// someone the right to bulk-import requires raw SQL — this panel
/// exposes the CRUD surface + the dry-run permission resolver.
///
/// Grants are tuples (user_id, action, entity_type, scope_type,
/// scope_entity_id). Scope resolution walks hierarchy (Region → Site
/// → Building for Device / Server / Building / Site entity types);
/// the admin picks scope_type + scope_entity_id here, the engine
/// handles the walk at request time.
/// </summary>
public partial class ScopeGrantsAdminPanel : UserControl
{
    private string? _baseUrl;
    private Guid _tenantId;
    private int? _actorUserId;
    private CancellationTokenSource? _cts;
    private IReadOnlyList<ScopeGrantDto> _lastResult = Array.Empty<ScopeGrantDto>();

    /// <summary>Action codes the engine recognises. Editable combo so
    /// new actions added on the Rust side show up without a round-trip
    /// through the UI repo first.</summary>
    internal static readonly string[] KnownActions = new[]
    {
        "", "read", "write", "delete", "apply", "render",
    };

    /// <summary>Entity types the scope resolver knows about. Order
    /// matches the hierarchy-walk direction (biggest → smallest) so
    /// admins scan the list the same way they reason about scopes.</summary>
    internal static readonly string[] KnownEntityTypes = new[]
    {
        "", "Region", "Site", "Building", "Device", "Server", "Link",
        "Vlan", "Subnet", "DhcpRelayTarget",
    };

    /// <summary>Scope types. "Global" grants on every row of the
    /// entity type; the hierarchy types narrow to a region / site /
    /// building; "EntityId" is the narrowest — a single row.</summary>
    internal static readonly string[] KnownScopeTypes = new[]
    {
        "Global", "Region", "Site", "Building", "EntityId",
    };

    public ScopeGrantsAdminPanel()
    {
        InitializeComponent();
        ActionFilterCombo.ItemsSource = KnownActions;
        EntityTypeFilterCombo.ItemsSource = KnownEntityTypes;
        StatusLabel.Text = "Filter + reload to inspect the grant table. 'New grant' creates a tuple.";
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        PanelMessageBus.Subscribe<NavigateToPanelMessage>(OnNavigate);
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

    private void OnNavigate(NavigateToPanelMessage msg)
    {
        if (msg.TargetPanel != "scopeGrants") return;
        switch (msg.SelectItem)
        {
            case "action:reload":   _ = ReloadAsync(); break;
            case "action:newGrant": _ = NewGrantAsync(); break;
            case "action:check":    _ = CheckPermissionAsync(); break;
        }
    }

    // ─── Toolbar handlers ──────────────────────────────────────────────

    private void OnRunFilter(object sender, RoutedEventArgs e) => _ = ReloadAsync();
    private void OnNewGrant(object sender, RoutedEventArgs e)  => _ = NewGrantAsync();
    private void OnDeleteGrant(object sender, RoutedEventArgs e) => _ = DeleteSelectedAsync();
    private void OnCheckPermission(object sender, RoutedEventArgs e) => _ = CheckPermissionAsync();

    public Task ReloadAsync() => RunListQueryAsync();

    // ─── List query ────────────────────────────────────────────────────

    private async Task RunListQueryAsync()
    {
        if (!RequireContext()) return;
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        RunFilterButton.IsEnabled = false;
        StatusLabel.Text = "Loading…";
        try
        {
            using var client = new NetworkingEngineClient(_baseUrl!);
            if (_actorUserId is int uid) client.SetActorUserId(uid);

            int? userId = null;
            if (int.TryParse(UserFilterBox.Text?.Trim(), out var u)) userId = u;
            var action = Empty2Null(ActionFilterCombo.EditValue as string);
            var entityType = Empty2Null(EntityTypeFilterCombo.EditValue as string);

            var rows = await client.ListScopeGrantsAsync(_tenantId, userId, action, entityType, ct);
            _lastResult = rows;
            GrantsGrid.ItemsSource = rows;
            StatusLabel.Text = $"{rows.Count} grant{(rows.Count == 1 ? "" : "s")} · " +
                               $"loaded {DateTime.Now:HH:mm:ss}";
            SummaryLabel.Text = BuildSummary(rows);
        }
        catch (OperationCanceledException) { /* ignore */ }
        catch (NetworkingEngineException ex) { StatusLabel.Text = $"Engine error ({ex.StatusCode}): {ex.Message}"; }
        catch (HttpRequestException ex)     { StatusLabel.Text = $"Network error: {ex.Message}"; }
        catch (Exception ex)                { StatusLabel.Text = $"Query failed: {ex.Message}"; }
        finally { RunFilterButton.IsEnabled = true; }
    }

    /// <summary>One-line cross-section summary under the grid so
    /// admins can spot "this user has wildcard Global grants"
    /// at a glance.</summary>
    internal static string BuildSummary(IReadOnlyList<ScopeGrantDto> rows)
    {
        if (rows.Count == 0) return "No grants match the current filter.";
        var globalCount = rows.Count(r => r.ScopeType == "Global");
        var distinctUsers = rows.Select(r => r.UserId).Distinct().Count();
        var distinctActions = rows.Select(r => r.Action).Distinct().Count();
        return $"{rows.Count} total · {distinctUsers} distinct user(s) · " +
               $"{distinctActions} distinct action(s) · {globalCount} Global-scope grant(s)";
    }

    // ─── New grant ─────────────────────────────────────────────────────

    private async Task NewGrantAsync()
    {
        if (!RequireContext()) return;

        var dlg = new NewScopeGrantDialog
        {
            Owner = Window.GetWindow(this),
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            using var client = new NetworkingEngineClient(_baseUrl!);
            if (_actorUserId is int uid) client.SetActorUserId(uid);

            var req = new CreateScopeGrantRequest(
                OrganizationId: _tenantId,
                UserId: dlg.GrantUserId,
                Action: dlg.GrantAction,
                EntityType: dlg.GrantEntityType,
                ScopeType: dlg.GrantScopeType,
                ScopeEntityId: dlg.GrantScopeEntityId,
                Notes: string.IsNullOrWhiteSpace(dlg.GrantNotes) ? null : dlg.GrantNotes);
            var created = await client.CreateScopeGrantAsync(req);
            await ReloadAsync();
            StatusLabel.Text = $"Created grant {created.Id} for user {created.UserId}: " +
                               $"{created.Action}:{created.EntityType} @ {created.ScopeType}";
        }
        catch (NetworkingEngineException ex) { StatusLabel.Text = $"Create failed ({ex.StatusCode}): {ex.Message}"; }
        catch (Exception ex)                 { StatusLabel.Text = $"Create failed: {ex.Message}"; }
    }

    // ─── Delete ────────────────────────────────────────────────────────

    private async Task DeleteSelectedAsync()
    {
        if (!RequireContext()) return;
        if (GrantsGrid.CurrentItem is not ScopeGrantDto row)
        {
            StatusLabel.Text = "Select a grant row first.";
            return;
        }

        var confirm = MessageBox.Show(
            $"Delete grant for user {row.UserId} — {row.Action}:{row.EntityType} @ {row.ScopeType}?\n\n" +
            "This is immediate; the grant is soft-deleted but enforcement stops within the RBAC cache window.",
            "Delete scope grant",
            MessageBoxButton.OKCancel, MessageBoxImage.Warning,
            MessageBoxResult.Cancel);
        if (confirm != MessageBoxResult.OK) return;

        try
        {
            using var client = new NetworkingEngineClient(_baseUrl!);
            if (_actorUserId is int uid) client.SetActorUserId(uid);
            await client.DeleteScopeGrantAsync(row.Id, _tenantId);
            await ReloadAsync();
            StatusLabel.Text = $"Deleted grant {row.Id} · {DateTime.Now:HH:mm:ss}";
        }
        catch (NetworkingEngineException ex) { StatusLabel.Text = $"Delete failed ({ex.StatusCode}): {ex.Message}"; }
        catch (Exception ex)                 { StatusLabel.Text = $"Delete failed: {ex.Message}"; }
    }

    // ─── Check permission (dry-run resolver) ───────────────────────────

    private async Task CheckPermissionAsync()
    {
        if (!RequireContext()) return;

        var dlg = new CheckPermissionDialog
        {
            Owner = Window.GetWindow(this),
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            using var client = new NetworkingEngineClient(_baseUrl!);
            if (_actorUserId is int uid) client.SetActorUserId(uid);

            var decision = await client.CheckPermissionAsync(
                _tenantId,
                dlg.CheckUserId,
                dlg.CheckAction,
                dlg.CheckEntityType,
                dlg.CheckEntityId);

            var verdict = decision.Allowed ? "ALLOWED" : "DENIED";
            var via = decision.MatchedGrantId is Guid g ? $" via grant {g}" : "";
            var msg = $"Resolver verdict: {verdict}{via}\n\n" +
                      $"Inputs:\n" +
                      $"  user_id      = {dlg.CheckUserId}\n" +
                      $"  action       = {dlg.CheckAction}\n" +
                      $"  entity_type  = {dlg.CheckEntityType}\n" +
                      $"  entity_id    = {(dlg.CheckEntityId?.ToString() ?? "(none)")}";
            MessageBox.Show(msg, "Permission check",
                MessageBoxButton.OK,
                decision.Allowed ? MessageBoxImage.Information : MessageBoxImage.Warning);

            StatusLabel.Text = $"Checked {dlg.CheckAction}:{dlg.CheckEntityType} for user " +
                               $"{dlg.CheckUserId} → {verdict}";
        }
        catch (NetworkingEngineException ex) { StatusLabel.Text = $"Check failed ({ex.StatusCode}): {ex.Message}"; }
        catch (Exception ex)                 { StatusLabel.Text = $"Check failed: {ex.Message}"; }
    }

    // ─── Row context menu → audit drill-down ───────────────────────────
    //
    // Grants are audited on create + delete (scope_grants.rs emits
    // AuditEvent with entity_type="ScopeGrant"). Right-click a row
    // to jump to its audit trail — symmetric with the SearchPanel
    // context menu and the ServerGridPanel row menu.

    private void OnContextShowAudit(object sender, RoutedEventArgs e)
    {
        if (GrantsGrid.CurrentItem is not ScopeGrantDto row) return;
        PanelMessageBus.Publish(new OpenPanelMessage("audit"));
        PanelMessageBus.Publish(new NavigateToPanelMessage(
            "audit", $"selectEntity:ScopeGrant:{row.Id}"));
        StatusLabel.Text = $"Drilled into audit for grant {row.Id}";
    }

    private void OnContextCopyId(object sender, RoutedEventArgs e)
    {
        if (GrantsGrid.CurrentItem is not ScopeGrantDto row) return;
        try
        {
            System.Windows.Clipboard.SetText(row.Id.ToString());
            StatusLabel.Text = $"Copied grant id {row.Id}";
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"Copy failed: {ex.Message}";
        }
    }

    // ─── Helpers ───────────────────────────────────────────────────────

    private bool RequireContext()
    {
        if (string.IsNullOrEmpty(_baseUrl) || _tenantId == Guid.Empty)
        {
            StatusLabel.Text = "No tenant context — set base URL + tenant first.";
            return false;
        }
        return true;
    }

    internal static string? Empty2Null(string? s) =>
        string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
