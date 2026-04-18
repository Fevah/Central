using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using Central.ApiClient;

namespace Central.Module.Networking.Governance;

/// <summary>
/// Subnet carve convenience form. IP-pool picker + prefix length +
/// subnet code + display name + scope. Unlike VLAN/ASN/MLAG, the
/// picker doesn't show a "free count" because availability depends
/// on the prefix length the admin's carving — computing that
/// per-prefix-per-pool on list would be expensive. Engine carves
/// at apply time and surfaces PoolExhausted cleanly if it can't.
///
/// <para>Prefix sanity clamp: 0..32 for v4 pools, 0..128 for v6.
/// Must also be ≥ pool's own prefix (can't carve a /24 out of a /28).
/// Engine's AllocationRangeException surfaces as a clean 422 if the
/// client's guards miss something.</para>
/// </summary>
public partial class CreateSubnetItemDialog : DevExpress.Xpf.Core.DXWindow
{
    private readonly string _baseUrl;
    private readonly Guid _tenantId;
    private readonly int? _actorUserId;
    private readonly Guid _setId;

    public ChangeSetItemDto? CreatedItem { get; private set; }

    public CreateSubnetItemDialog(string baseUrl, Guid tenantId, int? actorUserId, ChangeSetRow row)
    {
        InitializeComponent();
        _baseUrl = baseUrl;
        _tenantId = tenantId;
        _actorUserId = actorUserId;
        _setId = row.Id;
        HeaderLabel.Text = $"Subnet carve for \u201C{row.Title}\u201D " +
                           $"(currently {row.ItemCount} item{(row.ItemCount == 1 ? "" : "s")})";
        Loaded += async (_, _) => await LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            using var client = new NetworkingEngineClient(_baseUrl);
            if (_actorUserId is int uid) client.SetActorUserId(uid);
            PoolsGrid.ItemsSource = await client.ListIpPoolsAsync(_tenantId);
        }
        catch (Exception ex) { ShowError($"Failed to load IP pools: {ex.Message}"); }
    }

    private async void OnOk(object sender, RoutedEventArgs e)
    {
        var picked = PoolsGrid.CurrentItem as IpPoolDto;
        if (picked is null) { ShowError("Pick a pool."); return; }

        var prefix = (int)(decimal)PrefixEdit.Value;
        // Client-side sanity: prefix ≤ 32 for v4, ≤ 128 for v6. The engine
        // will also enforce "prefix ≥ pool prefix" — no need to replicate.
        var maxPrefix = picked.Family == 4 ? 32 : 128;
        if (prefix < 0 || prefix > maxPrefix)
        { ShowError($"Prefix /{prefix} is outside v{picked.Family} valid range 0..{maxPrefix}."); return; }

        var code = SubnetCodeBox.Text?.Trim() ?? "";
        if (code.Length == 0) { ShowError("Subnet code is required."); return; }
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

        OkButton.IsEnabled = false;
        ClearError();
        try
        {
            using var client = new NetworkingEngineClient(_baseUrl);
            if (_actorUserId is int uid) client.SetActorUserId(uid);

            var after = new Dictionary<string, object?>
            {
                ["poolId"]       = picked.Id,
                ["prefixLength"] = prefix,
                ["subnetCode"]   = code,
                ["displayName"]  = displayName,
                ["scopeLevel"]   = scopeLevel,
            };
            if (scopeEntityId is Guid sid) after["scopeEntityId"] = sid;

            var req = new AddChangeSetItemRequest(
                EntityType: "Subnet", EntityId: null, Action: "Create",
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
