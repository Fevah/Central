using DevExpress.Xpf.Grid;

namespace Central.Module.Networking.Devices;

/// <summary>
/// IP Ranges grid panel — extracted from MainWindow.
/// Read-only grid with region grouping and status combo.
/// </summary>
public partial class IpRangesGridPanel : System.Windows.Controls.UserControl
{
    public IpRangesGridPanel()
    {
        InitializeComponent();
    }

    /// <summary>Expose grid for layout save/restore and external access.</summary>
    public GridControl Grid => IpRangesGrid;
    public TableView View => IpRangesView;

    // ── Combo sources — set by host after construction ──

    public void BindComboSources(object statuses)
    {
        IpRangesStatusCombo.ItemsSource = statuses;
    }
}
