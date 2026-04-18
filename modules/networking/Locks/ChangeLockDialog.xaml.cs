using System;
using System.Net.Http;
using System.Windows;
using Central.ApiClient;

namespace Central.Module.Networking.Locks;

/// <summary>
/// Change-lock-state dialog. Populates with the current state +
/// reason, lets the admin pick a new state from the four enum
/// values + provide a reason. Engine-side validator rejects
/// Immutable → anything-else transitions cleanly as a 400 — no
/// client-side pre-check needed beyond "the admin has to pick
/// something different".
/// </summary>
public partial class ChangeLockDialog : DevExpress.Xpf.Core.DXWindow
{
    private readonly string _baseUrl;
    private readonly Guid _tenantId;
    private readonly int? _actorUserId;
    private readonly LockedRowDto _row;

    public ChangeLockDialog(string baseUrl, Guid tenantId, int? actorUserId,
        LockedRowDto row)
    {
        InitializeComponent();
        _baseUrl = baseUrl;
        _tenantId = tenantId;
        _actorUserId = actorUserId;
        _row = row;

        HeaderLabel.Text = $"Change lock on {row.TableName} \u201C{row.DisplayLabel}\u201D";
        CurrentLabel.Text = $"Current state: {row.LockState}  ·  reason: {row.LockReason ?? "(none)"}  ·  v{row.Version}";
        StateCombo.EditValue = row.LockState;
        ReasonBox.Text = row.LockReason ?? "";
    }

    private async void OnOk(object sender, RoutedEventArgs e)
    {
        var next = (StateCombo.EditValue as string) ?? "";
        if (string.IsNullOrWhiteSpace(next))
        {
            ShowError("Pick a state.");
            return;
        }

        OkButton.IsEnabled = false;
        ClearError();
        try
        {
            using var client = new NetworkingEngineClient(_baseUrl);
            if (_actorUserId is int uid) client.SetActorUserId(uid);

            var reason = ReasonBox.Text?.Trim() is { Length: > 0 } r ? r : null;
            await client.SetEntityLockAsync(_row.TableName, _row.Id, _tenantId,
                lockState: next, lockReason: reason);
            DialogResult = true;
            Close();
        }
        catch (NetworkingEngineException ex) { ShowError($"Engine error ({ex.StatusCode}): {ex.Message}"); }
        catch (HttpRequestException ex)     { ShowError($"Network error: {ex.Message}"); }
        catch (Exception ex)                { ShowError($"Failed: {ex.Message}"); }
        finally { OkButton.IsEnabled = true; }
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void ShowError(string msg) { ErrorLabel.Text = msg; ErrorLabel.Visibility = Visibility.Visible; }
    private void ClearError()          { ErrorLabel.Text = "";  ErrorLabel.Visibility = Visibility.Collapsed; }
}
