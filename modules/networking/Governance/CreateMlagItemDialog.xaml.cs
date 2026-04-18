using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using Central.ApiClient;

namespace Central.Module.Networking.Governance;

/// <summary>
/// MLAG domain create convenience form. Pool picker + display name +
/// scope. Same draft-vs-apply semantics as VLAN — engine picks the
/// domain id at apply from tenant-wide free space, not per-pool.
/// </summary>
public partial class CreateMlagItemDialog : DevExpress.Xpf.Core.DXWindow
{
    private readonly string _baseUrl;
    private readonly Guid _tenantId;
    private readonly int? _actorUserId;
    private readonly Guid _setId;

    public ChangeSetItemDto? CreatedItem { get; private set; }

    public CreateMlagItemDialog(string baseUrl, Guid tenantId, int? actorUserId, ChangeSetRow row)
    {
        InitializeComponent();
        _baseUrl = baseUrl;
        _tenantId = tenantId;
        _actorUserId = actorUserId;
        _setId = row.Id;
        HeaderLabel.Text = $"MLAG create for \u201C{row.Title}\u201D " +
                           $"(currently {row.ItemCount} item{(row.ItemCount == 1 ? "" : "s")})";
        Loaded += async (_, _) => await LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            using var client = new NetworkingEngineClient(_baseUrl);
            if (_actorUserId is int uid) client.SetActorUserId(uid);
            PoolsGrid.ItemsSource = await client.ListMlagPoolsAsync(_tenantId);
        }
        catch (Exception ex) { ShowError($"Failed to load MLAG pools: {ex.Message}"); }
    }

    private async void OnOk(object sender, RoutedEventArgs e)
    {
        var picked = PoolsGrid.CurrentItem as MlagPoolDto;
        if (picked is null) { ShowError("Pick a pool."); return; }

        var displayName = DisplayNameBox.Text?.Trim() ?? "";
        if (displayName.Length == 0) { ShowError("Display name is required."); return; }

        var scopeLevel = (ScopeCombo.EditValue as string) ?? "Free";
        Guid? scopeEntityId = null;
        var sidText = ScopeEntityIdBox.Text?.Trim() ?? "";
        if (scopeLevel == "Free")
        {
            if (sidText.Length > 0)
            { ShowError("Scope entity id must be blank when scope is 'Free'."); return; }
        }
        else
        {
            if (sidText.Length == 0)
            { ShowError($"Scope level '{scopeLevel}' requires a scope entity id (UUID)."); return; }
            if (!Guid.TryParse(sidText, out var sid))
            { ShowError($"'{sidText}' isn't a valid UUID."); return; }
            scopeEntityId = sid;
        }

        if (picked.Available <= 0)
        { ShowError($"Pool '{picked.PoolCode}' is exhausted."); return; }

        OkButton.IsEnabled = false;
        ClearError();
        try
        {
            using var client = new NetworkingEngineClient(_baseUrl);
            if (_actorUserId is int uid) client.SetActorUserId(uid);

            var after = new Dictionary<string, object?>
            {
                ["poolId"]      = picked.Id,
                ["displayName"] = displayName,
                ["scopeLevel"]  = scopeLevel,
            };
            if (scopeEntityId is Guid sid) after["scopeEntityId"] = sid;

            var req = new AddChangeSetItemRequest(
                EntityType: "MlagDomain", EntityId: null, Action: "Create",
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
