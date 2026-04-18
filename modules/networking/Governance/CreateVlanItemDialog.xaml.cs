using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using Central.ApiClient;

namespace Central.Module.Networking.Governance;

/// <summary>
/// VLAN create convenience form. Picks a block from a grid (showing
/// range + available count) and collects display name + optional
/// description + optional scope. Posts as a ChangeSetItem with
/// Action=Create, EntityType=Vlan.
///
/// <para>The allocated VLAN number is picked by the engine's
/// AllocationService at apply time — lowest free value in the block
/// that isn't on the reservation shelf. Admins see the block range
/// here, not a specific id.</para>
/// </summary>
public partial class CreateVlanItemDialog : DevExpress.Xpf.Core.DXWindow
{
    private readonly string _baseUrl;
    private readonly Guid _tenantId;
    private readonly int? _actorUserId;
    private readonly Guid _setId;

    public ChangeSetItemDto? CreatedItem { get; private set; }

    public CreateVlanItemDialog(string baseUrl, Guid tenantId, int? actorUserId,
        ChangeSetRow row)
    {
        InitializeComponent();
        _baseUrl = baseUrl;
        _tenantId = tenantId;
        _actorUserId = actorUserId;
        _setId = row.Id;
        HeaderLabel.Text = $"VLAN create for \u201C{row.Title}\u201D " +
                           $"(currently {row.ItemCount} item{(row.ItemCount == 1 ? "" : "s")})";
        Loaded += async (_, _) => await LoadBlocksAsync();
    }

    private async Task LoadBlocksAsync()
    {
        try
        {
            using var client = new NetworkingEngineClient(_baseUrl);
            if (_actorUserId is int uid) client.SetActorUserId(uid);
            var blocks = await client.ListVlanBlocksAsync(_tenantId);
            BlocksGrid.ItemsSource = blocks;
        }
        catch (Exception ex) { ShowError($"Failed to load VLAN blocks: {ex.Message}"); }
    }

    private async void OnOk(object sender, RoutedEventArgs e)
    {
        var picked = BlocksGrid.CurrentItem as VlanBlockDto;
        if (picked is null) { ShowError("Pick a block."); return; }

        var displayName = DisplayNameBox.Text?.Trim() ?? "";
        if (displayName.Length == 0) { ShowError("Display name is required."); return; }

        var scopeLevel = (ScopeCombo.EditValue as string) ?? "Free";

        // Scope entity id: required when scope level isn't Free. Parse
        // as UUID up-front so a mangled string surfaces here rather than
        // as a 400 from the engine.
        Guid? scopeEntityId = null;
        var sidText = ScopeEntityIdBox.Text?.Trim() ?? "";
        if (scopeLevel == "Free")
        {
            if (sidText.Length > 0)
            {
                ShowError("Scope entity id must be blank when scope is 'Free'.");
                return;
            }
        }
        else
        {
            if (sidText.Length == 0)
            {
                ShowError($"Scope level '{scopeLevel}' requires a scope entity id (UUID).");
                return;
            }
            if (!Guid.TryParse(sidText, out var sid))
            {
                ShowError($"'{sidText}' isn't a valid UUID.");
                return;
            }
            scopeEntityId = sid;
        }

        if (picked.Available <= 0)
        {
            ShowError($"Block '{picked.BlockCode}' has no free VLAN numbers " +
                      $"(range {picked.VlanFirst}-{picked.VlanLast}).");
            return;
        }

        OkButton.IsEnabled = false;
        ClearError();
        try
        {
            using var client = new NetworkingEngineClient(_baseUrl);
            if (_actorUserId is int uid) client.SetActorUserId(uid);

            var after = new Dictionary<string, object?>
            {
                ["blockId"]     = picked.Id,
                ["displayName"] = displayName,
                ["scopeLevel"]  = scopeLevel,
            };
            if (DescriptionBox.Text?.Trim() is { Length: > 0 } desc)
                after["description"] = desc;
            if (scopeEntityId is Guid sid2)
                after["scopeEntityId"] = sid2;

            var req = new AddChangeSetItemRequest(
                EntityType: "Vlan",
                EntityId: null,  // engine assigns at apply
                Action: "Create",
                BeforeJson: null,
                AfterJson: after,
                ExpectedVersion: null,
                Notes: null);

            CreatedItem = await client.AddChangeSetItemAsync(_setId, _tenantId, req);
            DialogResult = true;
            Close();
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
