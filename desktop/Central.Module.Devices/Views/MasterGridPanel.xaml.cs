using DevExpress.Xpf.Grid;

namespace Central.Module.Devices.Views;

/// <summary>
/// Master device list grid panel — extracted from MainWindow.
/// Read-only grid with grouping, filtering, and ASN combo.
/// </summary>
public partial class MasterGridPanel : System.Windows.Controls.UserControl
{
    public MasterGridPanel()
    {
        InitializeComponent();
    }

    /// <summary>Expose grid for layout save/restore and external access.</summary>
    public GridControl Grid => MasterGrid;
    public TableView View => MasterView;

    // ── Combo sources — set by host after construction ──

    public void BindComboSources(object statuses, object asnDefs)
    {
        MasterStatusCombo.ItemsSource = statuses;
        MasterAsnCombo.ItemsSource = asnDefs;
    }
}
