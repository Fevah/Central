using DevExpress.Xpf.Grid;

namespace Central.Module.Networking.Devices;

/// <summary>
/// MLAG configuration grid panel — extracted from MainWindow.
/// Read-only grid showing MLAG domains, peer links, and node IPs.
/// </summary>
public partial class MlagGridPanel : System.Windows.Controls.UserControl
{
    public MlagGridPanel()
    {
        InitializeComponent();
    }

    /// <summary>Expose grid for layout save/restore and external access.</summary>
    public GridControl Grid => MlagGrid;
    public TableView View => MlagView;

    // ── Combo sources — set by host after construction ──

    public void BindComboSources(object statuses)
    {
        MlagStatusCombo.ItemsSource = statuses;
    }
}
