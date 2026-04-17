using DevExpress.Xpf.Grid;

namespace Central.Module.Networking.Devices;

/// <summary>
/// Server AS grid panel — extracted from MainWindow.
/// Read-only grid showing server autonomous systems per building.
/// </summary>
public partial class ServerAsGridPanel : System.Windows.Controls.UserControl
{
    public ServerAsGridPanel()
    {
        InitializeComponent();
    }

    /// <summary>Expose grid for layout save/restore and external access.</summary>
    public GridControl Grid => ServerAsGrid;
    public TableView View => ServerAsView;

    // ── Combo sources — set by host after construction ──

    public void BindComboSources(object statuses)
    {
        ServerAsStatusCombo.ItemsSource = statuses;
    }
}
