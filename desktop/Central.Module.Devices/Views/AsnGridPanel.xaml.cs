using DevExpress.Xpf.Grid;
using Central.Core.Models;

namespace Central.Module.Devices.Views;

/// <summary>
/// ASN definitions grid panel — extracted from MainWindow.
/// Editable grid with ASN type combo and device-bind combo.
/// </summary>
public partial class AsnGridPanel : System.Windows.Controls.UserControl
{
    public AsnGridPanel()
    {
        InitializeComponent();
        AsnGrid.MasterRowExpanded += AsnGrid_MasterRowExpanded;
    }

    public GridControl Grid => AsnGrid;
    public TableView View => AsnView;

    /// <summary>Fired when a master row is expanded — host populates bound devices.</summary>
    public event System.Func<AsnDefinition, System.Threading.Tasks.Task>? LoadDetailDevices;

    public void BindComboSources(object boundDevices)
    {
        AsnBoundDevicesCombo.ItemsSource = boundDevices;
    }

    private async void AsnGrid_MasterRowExpanded(object sender, RowEventArgs e)
    {
        if (AsnGrid.GetRow(e.RowHandle) is AsnDefinition asn && asn.DetailDevices.Count == 0)
        {
            if (LoadDetailDevices != null)
                await LoadDetailDevices(asn);
        }
    }
}
