using System;
using System.Net.Http;
using System.Windows;
using Central.ApiClient;

namespace Central.Module.Networking.Governance;

/// <summary>
/// Submit dialog. Collects the <c>required_approvals</c> threshold and
/// calls <see cref="NetworkingEngineClient.SubmitChangeSetAsync"/>. Most
/// tenants land on "1 approval" so that's the default; the spinner
/// covers the realistic 1..20 range.
/// </summary>
public partial class SubmitChangeSetDialog : DevExpress.Xpf.Core.DXWindow
{
    private readonly string _baseUrl;
    private readonly Guid _tenantId;
    private readonly int? _actorUserId;
    private readonly Guid _setId;

    public ChangeSetDto? UpdatedSet { get; private set; }

    public SubmitChangeSetDialog(string baseUrl, Guid tenantId, int? actorUserId,
        ChangeSetRow row)
    {
        InitializeComponent();
        _baseUrl = baseUrl;
        _tenantId = tenantId;
        _actorUserId = actorUserId;
        _setId = row.Id;

        HeaderLabel.Text = $"Submit \u201C{row.Title}\u201D ({row.ItemCount} item{(row.ItemCount == 1 ? "" : "s")})";
    }

    private async void OnOk(object sender, RoutedEventArgs e)
    {
        var required = (int)(decimal)ApprovalsEdit.Value;
        if (required < 1)
        {
            ShowError("Required approvals must be >= 1.");
            return;
        }

        OkButton.IsEnabled = false;
        ClearError();
        try
        {
            using var client = new NetworkingEngineClient(_baseUrl);
            if (_actorUserId is int uid) client.SetActorUserId(uid);
            UpdatedSet = await client.SubmitChangeSetAsync(_setId, _tenantId, required);
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
