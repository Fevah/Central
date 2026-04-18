using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using Central.ApiClient;

namespace Central.Module.Networking.Governance;

/// <summary>
/// ASN allocation create convenience form. Picks a block + target
/// type/id; engine's AllocationService picks the number at apply.
/// </summary>
public partial class CreateAsnItemDialog : DevExpress.Xpf.Core.DXWindow
{
    private readonly string _baseUrl;
    private readonly Guid _tenantId;
    private readonly int? _actorUserId;
    private readonly Guid _setId;

    public ChangeSetItemDto? CreatedItem { get; private set; }

    public CreateAsnItemDialog(string baseUrl, Guid tenantId, int? actorUserId, ChangeSetRow row)
    {
        InitializeComponent();
        _baseUrl = baseUrl;
        _tenantId = tenantId;
        _actorUserId = actorUserId;
        _setId = row.Id;
        HeaderLabel.Text = $"ASN create for \u201C{row.Title}\u201D " +
                           $"(currently {row.ItemCount} item{(row.ItemCount == 1 ? "" : "s")})";
        Loaded += async (_, _) => await LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            using var client = new NetworkingEngineClient(_baseUrl);
            if (_actorUserId is int uid) client.SetActorUserId(uid);
            BlocksGrid.ItemsSource = await client.ListAsnBlocksAsync(_tenantId);
        }
        catch (Exception ex) { ShowError($"Failed to load ASN blocks: {ex.Message}"); }
    }

    private async void OnOk(object sender, RoutedEventArgs e)
    {
        var picked = BlocksGrid.CurrentItem as AsnBlockDto;
        if (picked is null) { ShowError("Pick a block."); return; }

        var targetType = (TargetTypeCombo.EditValue as string) ?? "Device";
        var idText = TargetIdBox.Text?.Trim() ?? "";
        if (idText.Length == 0) { ShowError("Target UUID is required."); return; }
        if (!Guid.TryParse(idText, out var targetId))
        { ShowError($"'{idText}' isn't a valid UUID."); return; }

        if (picked.Available <= 0)
        { ShowError($"Block '{picked.BlockCode}' has no free ASNs."); return; }

        OkButton.IsEnabled = false;
        ClearError();
        try
        {
            using var client = new NetworkingEngineClient(_baseUrl);
            if (_actorUserId is int uid) client.SetActorUserId(uid);

            var after = new Dictionary<string, object?>
            {
                ["blockId"]          = picked.Id,
                ["allocatedToType"]  = targetType,
                ["allocatedToId"]    = targetId,
            };
            var req = new AddChangeSetItemRequest(
                EntityType: "AsnAllocation", EntityId: null, Action: "Create",
                BeforeJson: null, AfterJson: after,
                ExpectedVersion: null, Notes: null);

            CreatedItem = await client.AddChangeSetItemAsync(_setId, _tenantId, req);
            DialogResult = true; Close();
        }
        catch (NetworkingEngineException ex) { ShowError($"Engine error ({ex.StatusCode}): {ex.Message}"); }
        catch (HttpRequestException ex)     { ShowError($"Network error: {ex.Message}"); }
        catch (Exception ex)                { ShowError($"Failed: {ex.Message}"); }
        finally { OkButton.IsEnabled = true; }
    }

    private void OnCancel(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }
    private void ShowError(string msg) { ErrorLabel.Text = msg; ErrorLabel.Visibility = Visibility.Visible; }
    private void ClearError()          { ErrorLabel.Text = "";  ErrorLabel.Visibility = Visibility.Collapsed; }
}
