using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using Central.ApiClient;

namespace Central.Module.Networking.Governance;

/// <summary>
/// Convenience form: pick a device from a filterable grid, type the
/// new hostname, click OK. Produces the same AddChangeSetItemRequest
/// as the generic dialog but without admins having to hand-type JSON.
///
/// <para>For every other (entity_type, action) pair the generic
/// <see cref="AddChangeSetItemDialog"/> remains the catch-all. This
/// dialog handles just the common Device/Rename case.</para>
/// </summary>
public partial class RenameDeviceItemDialog : DevExpress.Xpf.Core.DXWindow
{
    private readonly string _baseUrl;
    private readonly Guid _tenantId;
    private readonly int? _actorUserId;
    private readonly Guid _setId;
    private List<DeviceListRowDto> _all = new();

    public ChangeSetItemDto? CreatedItem { get; private set; }

    public RenameDeviceItemDialog(string baseUrl, Guid tenantId, int? actorUserId,
        ChangeSetRow row)
    {
        InitializeComponent();
        _baseUrl = baseUrl;
        _tenantId = tenantId;
        _actorUserId = actorUserId;
        _setId = row.Id;
        HeaderLabel.Text = $"Rename item for \u201C{row.Title}\u201D " +
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
        catch (Exception ex)
        {
            ShowError($"Failed to load devices: {ex.Message}");
        }
    }

    private void OnFilterChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        => ApplyFilter();

    /// <summary>Case-insensitive substring filter across hostname, role,
    /// building — covers the three fields an admin would search on.</summary>
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

    private void OnRowDoubleClick(object sender, DevExpress.Xpf.Grid.RowDoubleClickEventArgs e)
        => OnOk(sender, new RoutedEventArgs());

    private async void OnOk(object sender, RoutedEventArgs e)
    {
        var picked = DevicesGrid.CurrentItem as DeviceListRowDto;
        if (picked is null)
        {
            ShowError("Pick a device from the grid first.");
            return;
        }
        var newHost = NewHostnameBox.Text?.Trim() ?? "";
        if (newHost.Length == 0)
        {
            ShowError("New hostname is required.");
            return;
        }
        if (string.Equals(newHost, picked.Hostname, StringComparison.Ordinal))
        {
            ShowError("New hostname is the same as the current hostname — nothing to do.");
            return;
        }

        OkButton.IsEnabled = false;
        ClearError();
        try
        {
            using var client = new NetworkingEngineClient(_baseUrl);
            if (_actorUserId is int uid) client.SetActorUserId(uid);

            var req = new AddChangeSetItemRequest(
                EntityType: "Device",
                EntityId: picked.Id,
                Action: "Rename",
                BeforeJson: new { hostname = picked.Hostname },
                AfterJson:  new { hostname = newHost },
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

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void ShowError(string msg) { ErrorLabel.Text = msg; ErrorLabel.Visibility = Visibility.Visible; }
    private void ClearError()          { ErrorLabel.Text = "";  ErrorLabel.Visibility = Visibility.Collapsed; }
}
