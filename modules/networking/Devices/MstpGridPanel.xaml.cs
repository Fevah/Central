using DevExpress.Xpf.Grid;

namespace Central.Module.Networking.Devices;

/// <summary>
/// MSTP configuration grid panel — extracted from MainWindow.
/// Read-only grid showing MSTP bridge priorities per device.
/// </summary>
public partial class MstpGridPanel : System.Windows.Controls.UserControl
{
    public MstpGridPanel()
    {
        InitializeComponent();
    }

    /// <summary>Expose grid for layout save/restore and external access.</summary>
    public GridControl Grid => MstpGrid;
    public TableView View => MstpView;

    // ── Combo sources — set by host after construction ──

    public void BindComboSources(object statuses)
    {
        MstpStatusCombo.ItemsSource = statuses;
    }
}
