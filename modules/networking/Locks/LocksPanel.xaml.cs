using System;
using System.Collections.Generic;
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

namespace Central.Module.Networking.Locks;

/// <summary>
/// Lists every row in a non-Open lock state across the five numbering
/// tables (asn_allocation / vlan / mlag_domain / subnet / ip_address).
/// Right-click / double-click to change state; "Clear Lock" drops a
/// row back to Open (subject to the engine's transition rules —
/// Immutable stays terminal).
/// </summary>
public partial class LocksPanel : UserControl
{
    private string? _baseUrl;
    private Guid _tenantId;
    private int? _actorUserId;
    private CancellationTokenSource? _cts;

    public LocksPanel()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        PanelMessageBus.Subscribe<NavigateToPanelMessage>(OnNavigate);
        PanelMessageBus.Subscribe<RefreshPanelMessage>(OnRefresh);
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
        if (!string.IsNullOrEmpty(_baseUrl) && _tenantId != Guid.Empty) _ = ReloadAsync();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
        _cts = null;
    }

    // ─── Routing ────────────────────────────────────────────────────────

    private void OnNavigate(NavigateToPanelMessage msg)
    {
        if (msg.TargetPanel != "locks") return;
        switch (msg.SelectItem as string)
        {
            case "action:changeState": RunWithSelection("change state on", OpenChangeLockDialog); break;
            case "action:clearLock":   RunWithSelection("clear the lock on", ConfirmAndClear); break;
            default:                   _ = ReloadAsync(); break;
        }
    }

    private void OnRefresh(RefreshPanelMessage msg)
    {
        if (msg.TargetPanel != "locks") return;
        _ = ReloadAsync();
    }

    private void OnRowDoubleClick(object sender, DevExpress.Xpf.Grid.RowDoubleClickEventArgs e)
    {
        var row = Current();
        if (row is not null) OpenChangeLockDialog(row);
    }

    private void CtxChangeState(object sender, RoutedEventArgs e)
        => RunWithSelection("change state on", OpenChangeLockDialog);
    private void CtxClearLock(object sender, RoutedEventArgs e)
        => RunWithSelection("clear the lock on", ConfirmAndClear);
    private void CtxRefresh(object sender, RoutedEventArgs e) => _ = ReloadAsync();

    private LockedRowDto? Current() => Grid.CurrentItem as LockedRowDto;

    private void RunWithSelection(string verb, Action<LockedRowDto> body)
    {
        var row = Current();
        if (row is null)
        {
            MessageBox.Show($"Select a locked row to {verb}.",
                "No selection", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        body(row);
    }

    // ─── Reload ─────────────────────────────────────────────────────────

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
            var rows = await client.ListLockedAsync(_tenantId, ct: ct);
            Grid.ItemsSource = rows;

            var hard = 0; var imm = 0; var soft = 0;
            foreach (var r in rows)
            {
                if (r.LockState == "HardLock") hard++;
                else if (r.LockState == "Immutable") imm++;
                else if (r.LockState == "SoftLock") soft++;
            }
            StatusLabel.Text = $"{rows.Count} locked · " +
                               $"{hard} HardLock · {imm} Immutable · {soft} SoftLock · " +
                               $"loaded {DateTime.Now:HH:mm:ss}";
        }
        catch (OperationCanceledException) { /* ignore */ }
        catch (NetworkingEngineException ex) { StatusLabel.Text = $"Engine error ({ex.StatusCode}): {ex.Message}"; }
        catch (HttpRequestException ex)     { StatusLabel.Text = $"Network error: {ex.Message}"; }
        catch (Exception ex)                { StatusLabel.Text = $"Load failed: {ex.Message}"; }
    }

    // ─── Actions ────────────────────────────────────────────────────────

    private void OpenChangeLockDialog(LockedRowDto row)
    {
        if (string.IsNullOrEmpty(_baseUrl) || _tenantId == Guid.Empty) return;
        var dialog = new ChangeLockDialog(_baseUrl!, _tenantId, _actorUserId, row)
        {
            Owner = Window.GetWindow(this),
        };
        if (dialog.ShowDialog() == true) _ = ReloadAsync();
    }

    /// <summary>Shortcut for "set back to Open". Standard confirm prompt
    /// then one PATCH. Immutable rows are handled by the engine's
    /// validator — surfaces as a clean 400 without hitting the DB
    /// trigger.</summary>
    private async void ConfirmAndClear(LockedRowDto row)
    {
        var confirm = MessageBox.Show(
            $"Clear lock on {row.TableName} '{row.DisplayLabel}'?\n\n" +
            $"Sets lock_state back to Open. The row becomes mutable again.",
            "Clear lock", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes) return;

        try
        {
            using var client = new NetworkingEngineClient(_baseUrl!);
            if (_actorUserId is int uid) client.SetActorUserId(uid);
            await client.SetEntityLockAsync(row.TableName, row.Id, _tenantId,
                lockState: "Open", lockReason: null);
            await ReloadAsync();
        }
        catch (NetworkingEngineException ex)
        {
            MessageBox.Show($"Engine error ({ex.StatusCode}): {ex.Message}",
                "Clear lock failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch (HttpRequestException ex)
        {
            MessageBox.Show($"Network error: {ex.Message}",
                "Clear lock failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed: {ex.Message}",
                "Clear lock failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
