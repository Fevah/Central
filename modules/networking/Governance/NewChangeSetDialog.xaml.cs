using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using Central.ApiClient;

namespace Central.Module.Networking.Governance;

/// <summary>
/// Simple modal for the "New Change Set" ribbon action. Collects title +
/// optional description, calls <see cref="NetworkingEngineClient.CreateChangeSetAsync"/>,
/// returns the new <see cref="ChangeSetDto"/> so the caller can refresh
/// the grid and (optionally) open a detail view.
///
/// <para>Item drafting (the actual mutations inside the Set) happens after
/// the draft exists — there's no point collecting items before we know the
/// Set was created cleanly. Item-add flows live in a follow-on dialog.</para>
/// </summary>
public partial class NewChangeSetDialog : DevExpress.Xpf.Core.DXWindow
{
    private readonly string _baseUrl;
    private readonly Guid _tenantId;
    private readonly int? _actorUserId;
    private readonly string? _actorDisplay;

    public ChangeSetDto? CreatedSet { get; private set; }

    public NewChangeSetDialog(string baseUrl, Guid tenantId,
        int? actorUserId = null, string? actorDisplay = null)
    {
        InitializeComponent();
        _baseUrl = baseUrl;
        _tenantId = tenantId;
        _actorUserId = actorUserId;
        _actorDisplay = actorDisplay;

        // Focus the title box on open so admins can just start typing.
        Loaded += (_, _) => TitleBox.Focus();
    }

    private async void OnOk(object sender, RoutedEventArgs e)
    {
        var title = TitleBox.Text?.Trim() ?? "";
        if (title.Length == 0)
        {
            ShowError("Title is required.");
            return;
        }

        OkButton.IsEnabled = false;
        CancelButton.IsEnabled = false;
        ClearError();

        try
        {
            using var client = new NetworkingEngineClient(_baseUrl);
            if (_actorUserId is int uid) client.SetActorUserId(uid);

            var req = new CreateChangeSetRequest(
                _tenantId, title,
                DescriptionBox.Text?.Trim() is { Length: > 0 } desc ? desc : null,
                _actorDisplay);

            CreatedSet = await client.CreateChangeSetAsync(req);
            DialogResult = true;
            Close();
        }
        catch (NetworkingEngineException ex)
        {
            ShowError($"Engine error ({ex.StatusCode}): {ex.Message}");
        }
        catch (HttpRequestException ex)
        {
            ShowError($"Network error: {ex.Message}");
        }
        catch (Exception ex)
        {
            ShowError($"Failed: {ex.Message}");
        }
        finally
        {
            OkButton.IsEnabled = true;
            CancelButton.IsEnabled = true;
        }
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void ShowError(string msg)
    {
        ErrorLabel.Text = msg;
        ErrorLabel.Visibility = Visibility.Visible;
    }

    private void ClearError()
    {
        ErrorLabel.Text = "";
        ErrorLabel.Visibility = Visibility.Collapsed;
    }
}
