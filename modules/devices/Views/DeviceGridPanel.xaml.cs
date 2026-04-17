using System;
using System.Threading.Tasks;
using DevExpress.Xpf.Grid;
using Central.Core.Models;

namespace Central.Module.Devices.Views;

/// <summary>
/// IPAM device grid panel — extracted from MainWindow.
/// DataContext is set by the host (MainViewModel for now, DeviceListViewModel later).
/// </summary>
public partial class DeviceGridPanel : System.Windows.Controls.UserControl
{
    public DeviceGridPanel()
    {
        InitializeComponent();

        // Wire ValidateRow + InvalidRowException in constructor (DX pattern)
        DevicesView.ValidateRow += DevicesView_ValidateRow;
        DevicesView.InvalidRowException += (_, e) => e.ExceptionMode = ExceptionMode.NoAction;
        DevicesGrid.MasterRowExpanded += DevicesGrid_MasterRowExpanded;
    }

    public GridControl Grid => DevicesGrid;
    public TableView View => DevicesView;
    public DevExpress.Xpf.Editors.TextEdit SearchBox => DevicesSearch;

    // ── Combo sources — set by host after construction ──

    public void BindComboSources(object statuses, object deviceTypes, object buildings,
        object regions, object asnDefs)
    {
        StatusCombo.ItemsSource = statuses;
        DeviceTypeCombo.ItemsSource = deviceTypes;
        BuildingCombo.ItemsSource = buildings;
        RegionCombo.ItemsSource = regions;
        DevicesAsnCombo.ItemsSource = asnDefs;
    }

    // ── Events — forwarded to host ──

    public event EventHandler<CellValueChangedEventArgs>? CellValueChanged;
    public event Func<DeviceRecord, Task>? SaveDevice;
    public event Action<string>? SearchChanged;
    /// <summary>Fired when a master row is expanded — host loads links for the device.</summary>
    public event Func<DeviceRecord, Task>? LoadDetailLinks;

    private void DevicesView_CellValueChanged(object sender, CellValueChangedEventArgs e)
        => CellValueChanged?.Invoke(this, e);

    private async void DevicesView_ValidateRow(object sender, GridRowValidationEventArgs e)
    {
        if (e.Row is not DeviceRecord device) return;
        if (string.IsNullOrWhiteSpace(device.SwitchName))
        {
            e.IsValid = false;
            e.ErrorContent = "Device name is required.";
            return;
        }
        if (SaveDevice != null)
            await SaveDevice.Invoke(device);
    }

    private void DevicesSearch_EditValueChanged(object sender,
        DevExpress.Xpf.Editors.EditValueChangedEventArgs e)
        => SearchChanged?.Invoke(DevicesSearch.EditValue as string ?? "");

    private async void DevicesGrid_MasterRowExpanded(object sender, RowEventArgs e)
    {
        if (DevicesGrid.GetRow(e.RowHandle) is DeviceRecord dev && dev.DetailLinks.Count == 0)
        {
            if (LoadDetailLinks != null)
                await LoadDetailLinks(dev);
        }
    }
}
