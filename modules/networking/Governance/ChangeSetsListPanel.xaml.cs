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

        // Action routing. SelectItem carries the sub-action tag
        // (from the registrar: "action:new" / "action:submit" / ...).
        // Actions that operate on a single Set read the grid's current
        // row; missing selection falls through to a MessageBox prompt.
        var action = msg.SelectItem as string;
        switch (action)
        {
            case "action:new":
                OpenNewChangeSetDialog();
                break;
            case "action:addItem":
                RunWithSelection("add an item to", OpenAddItemDialog);
                break;
            case "action:renameDevice":
                RunWithSelection("add a device rename to", OpenRenameDeviceDialog);
                break;
            case "action:submit":
                RunWithSelection("Submit", OpenSubmitDialog);
                break;
            case "action:decide":
                RunWithSelection("record a decision on", OpenDecideDialog);
                break;
            case "action:apply":
                RunWithSelection("Apply", ConfirmAndApply);
                break;
            case "action:rollback":
                RunWithSelection("Rollback", ConfirmAndRollback);
                break;
            case "action:cancel":
                RunWithSelection("Cancel", ConfirmAndCancel);
                break;
            case "action:details":
                RunWithSelection("inspect", OpenDetailDialog);
                break;
            default:
                _ = ReloadAsync();
                break;
        }
    }

    // ─── Selection helpers ──────────────────────────────────────────────

    /// <summary>Current selected row from the grid, or null. Actions
    /// that need a target Set go through this.</summary>
    private ChangeSetRow? CurrentRow() => Grid.CurrentItem as ChangeSetRow;

    /// <summary>Wraps the "did the admin actually select something?"
    /// check that every non-new action shares. <paramref name="verb"/>
    /// is the human-readable phrase that shows in the prompt ("Apply",
    /// "Rollback", etc.).</summary>
    private void RunWithSelection(string verb, Action<ChangeSetRow> body)
    {
        var row = CurrentRow();
        if (row is null)
        {
            MessageBox.Show(
                $"Select a Change Set to {verb}.",
                "No selection", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        body(row);
    }

    // ─── New ────────────────────────────────────────────────────────────

    private void OpenNewChangeSetDialog()
    {
        if (string.IsNullOrEmpty(_baseUrl) || _tenantId == Guid.Empty) return;

        var dialog = new NewChangeSetDialog(_baseUrl!, _tenantId, _actorUserId)
        {
            Owner = Window.GetWindow(this),
        };
        if (dialog.ShowDialog() == true && dialog.CreatedSet is not null)
        {
            _ = ReloadAsync();
        }
    }

    // ─── Detail ─────────────────────────────────────────────────────────

    /// <summary>Row-double-click shortcut to the detail view. Bound
    /// from XAML via TableView.RowDoubleClick.</summary>
    private void OnRowDoubleClick(object sender, DevExpress.Xpf.Grid.RowDoubleClickEventArgs e)
    {
        var row = CurrentRow();
        if (row is not null) OpenDetailDialog(row);
    }

    // ─── Context menu (right-click on row) ──────────────────────────────
    //
    // Items are gated on the row's Status: e.g. Apply is only sensible
    // when the Set is Approved; Cancel only works from non-terminal
    // states. Disabling rather than hiding keeps the menu's shape
    // consistent so admins can learn it by muscle memory.

    private void OnContextMenuOpened(object sender, RoutedEventArgs e)
    {
        var row = CurrentRow();
        var status = row?.Status ?? "";
        bool draft      = status == "Draft";
        bool submitted  = status == "Submitted";
        bool approved   = status == "Approved";
        bool applied    = status == "Applied";
        bool terminal   = status is "Rejected" or "Cancelled" or "RolledBack";

        CtxAddItemMenu.IsEnabled  = draft;
        CtxSubmitMenu.IsEnabled   = draft;
        CtxDecideMenu.IsEnabled   = submitted;
        CtxApplyMenu.IsEnabled    = approved;
        CtxRollbackMenu.IsEnabled = applied;
        CtxCancelMenu.IsEnabled   = !terminal && !applied; // cancel is pre-apply
    }

    private void CtxDetails(object sender, RoutedEventArgs e) => RunWithSelection("inspect", OpenDetailDialog);
    private void CtxAddItem(object sender, RoutedEventArgs e) => RunWithSelection("add an item to", OpenAddItemDialog);
    private void CtxSubmit(object sender, RoutedEventArgs e)  => RunWithSelection("Submit", OpenSubmitDialog);
    private void CtxDecide(object sender, RoutedEventArgs e)  => RunWithSelection("record a decision on", OpenDecideDialog);
    private void CtxApply(object sender, RoutedEventArgs e)   => RunWithSelection("Apply", ConfirmAndApply);
    private void CtxRollback(object sender, RoutedEventArgs e) => RunWithSelection("Rollback", ConfirmAndRollback);
    private void CtxCancel(object sender, RoutedEventArgs e)  => RunWithSelection("Cancel", ConfirmAndCancel);
    private void CtxRefresh(object sender, RoutedEventArgs e) => _ = ReloadAsync();

    private void OpenDetailDialog(ChangeSetRow row)
    {
        var dialog = new ChangeSetDetailDialog(_baseUrl!, _tenantId, _actorUserId, row)
        {
            Owner = Window.GetWindow(this),
        };
        dialog.ShowDialog();
    }

    // ─── Add Item ───────────────────────────────────────────────────────

    private void OpenAddItemDialog(ChangeSetRow row)
    {
        if (!RequireDraft(row)) return;
        var dialog = new AddChangeSetItemDialog(_baseUrl!, _tenantId, _actorUserId, row)
        {
            Owner = Window.GetWindow(this),
        };
        if (dialog.ShowDialog() == true) _ = ReloadAsync();
    }

    /// <summary>Convenience variant of AddItem for the common Device/Rename
    /// case. Drops the JSON textareas in favour of a device picker + a
    /// new-hostname textbox. Same underlying AddChangeSetItemAsync call.</summary>
    private void OpenRenameDeviceDialog(ChangeSetRow row)
    {
        if (!RequireDraft(row)) return;
        var dialog = new RenameDeviceItemDialog(_baseUrl!, _tenantId, _actorUserId, row)
        {
            Owner = Window.GetWindow(this),
        };
        if (dialog.ShowDialog() == true) _ = ReloadAsync();
    }

    private bool RequireDraft(ChangeSetRow row)
    {
        if (row.Status == "Draft") return true;
        MessageBox.Show(
            $"Items can only be added to a Change Set in Draft. " +
            $"This set is {row.Status}.\n\nCancel + re-draft, or submit as-is.",
            "Set not in Draft", MessageBoxButton.OK, MessageBoxImage.Information);
        return false;
    }

    // ─── Submit ─────────────────────────────────────────────────────────

    private void OpenSubmitDialog(ChangeSetRow row)
    {
        var dialog = new SubmitChangeSetDialog(_baseUrl!, _tenantId, _actorUserId, row)
        {
            Owner = Window.GetWindow(this),
        };
        if (dialog.ShowDialog() == true) _ = ReloadAsync();
    }

    // ─── Decide ─────────────────────────────────────────────────────────

    private void OpenDecideDialog(ChangeSetRow row)
    {
        var dialog = new DecideChangeSetDialog(
            _baseUrl!, _tenantId, _actorUserId, actorDisplay: null, row)
        {
            Owner = Window.GetWindow(this),
        };
        if (dialog.ShowDialog() == true) _ = ReloadAsync();
    }

    // ─── Apply ──────────────────────────────────────────────────────────

    private async void ConfirmAndApply(ChangeSetRow row)
    {
        var confirm = MessageBox.Show(
            $"Apply \u201C{row.Title}\u201D?\n\n" +
            $"{row.ItemCount} item{(row.ItemCount == 1 ? "" : "s")} will execute. " +
            "Items that succeed stamp audit entries; partial failures leave the \n" +
            "Set at Approved for retry.",
            "Apply Change Set", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes) return;

        await RunRemote(
            client => client.ApplyChangeSetAsync(row.Id, _tenantId),
            onSuccess: result =>
            {
                _ = ReloadAsync();
                if (result.FailedCount > 0)
                {
                    MessageBox.Show(
                        $"Apply finished with {result.AppliedCount} succeeded, " +
                        $"{result.FailedCount} failed. See the Set detail for per-item errors.",
                        "Partial apply", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            });
    }

    // ─── Rollback ───────────────────────────────────────────────────────

    private async void ConfirmAndRollback(ChangeSetRow row)
    {
        var confirm = MessageBox.Show(
            $"Rollback \u201C{row.Title}\u201D?\n\n" +
            "Every applied item is reversed. The audit log keeps both the \n" +
            "original apply entries and fresh RolledBack entries — the \n" +
            "history is never lost.",
            "Rollback Change Set", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) return;

        await RunRemote(
            client => client.RollbackChangeSetAsync(row.Id, _tenantId),
            onSuccess: result =>
            {
                _ = ReloadAsync();
                if (result.FailedCount > 0)
                {
                    MessageBox.Show(
                        $"Rollback finished with {result.RevertedCount} reverted, " +
                        $"{result.FailedCount} failed. Items that couldn't be reversed \n" +
                        "stay as their applied value; see per-item errors in the Set detail.",
                        "Partial rollback", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            });
    }

    // ─── Cancel ─────────────────────────────────────────────────────────

    private async void ConfirmAndCancel(ChangeSetRow row)
    {
        var confirm = MessageBox.Show(
            $"Cancel \u201C{row.Title}\u201D?\n\nThe Set moves to Cancelled (terminal).",
            "Cancel Change Set", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes) return;

        await RunRemote(
            client => client.CancelChangeSetAsync(row.Id, _tenantId),
            onSuccess: result => { _ = ReloadAsync(); });
    }

    // ─── Remote-call wrapper ────────────────────────────────────────────

    /// <summary>Handles the typed-exception -> MessageBox mapping shared
    /// by every ribbon action that calls the Rust engine. Takes the
    /// client delegate inline so each caller supplies its own method
    /// pointer + result-type handler.</summary>
    private async Task RunRemote<T>(
        Func<NetworkingEngineClient, Task<T>> call,
        Action<T> onSuccess)
    {
        try
        {
            using var client = new NetworkingEngineClient(_baseUrl!);
            if (_actorUserId is int uid) client.SetActorUserId(uid);
            var result = await call(client);
            onSuccess(result);
        }
        catch (NetworkingEngineException ex)
        {
            MessageBox.Show($"Engine error ({ex.StatusCode}): {ex.Message}",
                "Operation failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch (HttpRequestException ex)
        {
            MessageBox.Show($"Network error: {ex.Message}",
                "Operation failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed: {ex.Message}",
                "Operation failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
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
