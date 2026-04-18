using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using Central.ApiClient;

namespace Central.Module.Networking.Governance;

/// <summary>
/// Delete convenience form — picker + confirmation. Produces an
/// AddChangeSetItemRequest with EntityType=Device, Action=Delete,
/// expectedVersion set from the picked row (so OCC catches any
/// concurrent edit before apply).
///
/// <para>No afterJson — Delete items must not carry one per the
/// engine's validator.</para>
/// </summary>
public partial class DeleteDeviceItemDialog : DevExpress.Xpf.Core.DXWindow
{
    private readonly string _baseUrl;
    private readonly Guid _tenantId;
    private readonly int? _actorUserId;
    private readonly Guid _setId;
    private List<DeviceListRowDto> _all = new();

    public ChangeSetItemDto? CreatedItem { get; private set; }

    public DeleteDeviceItemDialog(string baseUrl, Guid tenantId, int? actorUserId,
        ChangeSetRow row)
    {
        InitializeComponent();
        _baseUrl = baseUrl;
        _tenantId = tenantId;
        _actorUserId = actorUserId;
        _setId = row.Id;
        HeaderLabel.Text = $"Delete item for \u201C{row.Title}\u201D " +
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

    private void OnRowDoubleClick(object sender, DevExpress.Xpf.Grid.RowDoubleClickEventArgs e)
        => OnOk(sender, new RoutedEventArgs());

    private async void OnOk(object sender, RoutedEventArgs e)
    {
        var picked = DevicesGrid.CurrentItem as DeviceListRowDto;
        if (picked is null) { ShowError("Pick a device from the grid first."); return; }

        OkButton.IsEnabled = false;
        ClearError();
        try
        {
            using var client = new NetworkingEngineClient(_baseUrl);
            if (_actorUserId is int uid) client.SetActorUserId(uid);

            var req = new AddChangeSetItemRequest(
                EntityType: "Device",
                EntityId: picked.Id,
                Action: "Delete",
                BeforeJson: new { hostname = picked.Hostname },
                AfterJson: null, // Delete items must not carry afterJson
                ExpectedVersion: picked.Version,
                Notes: NotesBox.Text?.Trim() is { Length: > 0 } n ? n : null);

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
