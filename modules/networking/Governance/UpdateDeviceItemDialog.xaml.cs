using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using Central.ApiClient;

namespace Central.Module.Networking.Governance;

/// <summary>
/// Update convenience form — picker + three most-edited fields
/// (displayName / managementIp / notes). Anything else goes through
/// the generic AddChangeSetItemDialog. Only fields the admin actually
/// types a value into are sent in after_json; engine-side COALESCE
/// keeps absent fields at their current value.
///
/// <para>Setting a field to empty here keeps the current value
/// (absent from after_json). To actively clear a field, admins
/// use the generic dialog with an explicit JSON <c>null</c>.</para>
/// </summary>
public partial class UpdateDeviceItemDialog : DevExpress.Xpf.Core.DXWindow
{
    private readonly string _baseUrl;
    private readonly Guid _tenantId;
    private readonly int? _actorUserId;
    private readonly Guid _setId;
    private List<DeviceListRowDto> _all = new();

    public ChangeSetItemDto? CreatedItem { get; private set; }

    public UpdateDeviceItemDialog(string baseUrl, Guid tenantId, int? actorUserId,
        ChangeSetRow row)
    {
        InitializeComponent();
        _baseUrl = baseUrl;
        _tenantId = tenantId;
        _actorUserId = actorUserId;
        _setId = row.Id;
        HeaderLabel.Text = $"Update item for \u201C{row.Title}\u201D " +
                           $"(currently {row.ItemCount} item{(row.ItemCount == 1 ? "" : "s")})";
        Loaded += async (_, _) => await LoadDevicesAsync();
    }

    private async Task LoadDevicesAsync()
    {
        try
        {
            using var client = new NetworkingEngineClient(_baseUrl);
            if (_actorUserId is int uid) client.SetActorUserId(uid);
            _all = await client.ListDevicesAsync(_tenantId);
            ApplyFilter();
        }
        catch (Exception ex) { ShowError($"Failed to load devices: {ex.Message}"); }
    }

    private void OnFilterChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        => ApplyFilter();

    private void ApplyFilter()
    {
        var q = FilterBox.Text?.Trim() ?? "";
        IEnumerable<DeviceListRowDto> rows = _all;
        if (q.Length > 0)
        {
            rows = _all.Where(d =>
                d.Hostname.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                (d.RoleCode     ?? "").Contains(q, StringComparison.OrdinalIgnoreCase) ||
                (d.BuildingCode ?? "").Contains(q, StringComparison.OrdinalIgnoreCase));
        }
        DevicesGrid.ItemsSource = rows.ToList();
    }

    /// <summary>Prepopulate editable fields when the admin picks a
    /// device so they can see current values and edit just what they
    /// need. Tricky point: this dialog only has the public list DTO,
    /// which doesn't carry displayName / managementIp — it only has
    /// hostname + role + building + version. So we pre-fill only
    /// what we have.</summary>
    private void OnFocusedRowChanged(object sender,
        DevExpress.Xpf.Grid.FocusedRowChangedEventArgs e)
    {
        ClearError();
    }

    private async void OnOk(object sender, RoutedEventArgs e)
    {
        var picked = DevicesGrid.CurrentItem as DeviceListRowDto;
        if (picked is null) { ShowError("Pick a device from the grid first."); return; }

        var displayName = DisplayNameBox.Text?.Trim();
        var managementIp = ManagementIpBox.Text?.Trim();
        var notes = NotesBox.Text?.Trim();

        if (string.IsNullOrEmpty(displayName) &&
            string.IsNullOrEmpty(managementIp) &&
            string.IsNullOrEmpty(notes))
        {
            ShowError("Fill in at least one field to update.");
            return;
        }

        // Validate management_ip format before posting — the engine's
        // inet cast would surface a 500-ish error otherwise. Accept
        // IPv4 and IPv6 via IPAddress.TryParse.
        if (!string.IsNullOrEmpty(managementIp) && !IPAddress.TryParse(managementIp, out _))
        {
            ShowError($"Management IP '{managementIp}' isn't a valid IPv4/IPv6 address.");
            return;
        }

        // Build after_json with only the fields the admin touched.
        // Omitted keys keep current value (engine COALESCE). This
        // mirrors the generic dialog's partial-update behaviour.
        var after = new Dictionary<string, object?>();
        if (!string.IsNullOrEmpty(displayName)) after["displayName"] = displayName;
        if (!string.IsNullOrEmpty(managementIp)) after["managementIp"] = managementIp;
        if (!string.IsNullOrEmpty(notes)) after["notes"] = notes;

        OkButton.IsEnabled = false;
        ClearError();
        try
        {
            using var client = new NetworkingEngineClient(_baseUrl);
            if (_actorUserId is int uid) client.SetActorUserId(uid);

            var req = new AddChangeSetItemRequest(
                EntityType: "Device",
                EntityId: picked.Id,
                Action: "Update",
                BeforeJson: new { hostname = picked.Hostname },
                AfterJson: after,
                ExpectedVersion: picked.Version,
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
