using System;
using System.Net.Http;
using System.Windows;
using Central.ApiClient;

namespace Central.Module.Networking.Governance;

/// <summary>
/// Approve / Reject dialog. Records a decision for the selected
/// Submitted Change Set. The Rust side enforces self-approval
/// prevention + the per-set UNIQUE (change_set_id, approver_user_id),
/// so we surface those errors directly in the dialog's error label
/// without pre-validating client-side.
///
/// The header shows "n of m approvals" drawn from the current row's
/// state so the approver sees whether their decision will cross the
/// threshold.
/// </summary>
public partial class DecideChangeSetDialog : DevExpress.Xpf.Core.DXWindow
{
    private readonly string _baseUrl;
    private readonly Guid _tenantId;
    private readonly int? _actorUserId;
    private readonly string? _actorDisplay;
    private readonly Guid _setId;

    public DecisionResultDto? Result { get; private set; }

    public DecideChangeSetDialog(string baseUrl, Guid tenantId, int? actorUserId,
        string? actorDisplay, ChangeSetRow row)
    {
        InitializeComponent();
        _baseUrl = baseUrl;
        _tenantId = tenantId;
        _actorUserId = actorUserId;
        _actorDisplay = actorDisplay;
        _setId = row.Id;

        HeaderLabel.Text = $"Decide on \u201C{row.Title}\u201D";
        ProgressLabel.Text = $"Status: {row.Status} · {row.ItemCount} item{(row.ItemCount == 1 ? "" : "s")} · " +
                             $"Threshold: {row.RequiredApprovals?.ToString() ?? "—"}";
    }

    private async void OnOk(object sender, RoutedEventArgs e)
    {
        if (_actorUserId is not int)
        {
            ShowError("No actor user id — cannot record a decision anonymously.");
            return;
        }

        var decision = ApproveRadio.IsChecked == true ? "Approve" : "Reject";
        var notes = NotesBox.Text?.Trim() is { Length: > 0 } n ? n : null;

        OkButton.IsEnabled = false;
        ClearError();
        try
        {
            using var client = new NetworkingEngineClient(_baseUrl);
            client.SetActorUserId(_actorUserId!.Value);
            Result = await client.RecordDecisionAsync(_setId, _tenantId,
                decision, _actorDisplay, notes);
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
