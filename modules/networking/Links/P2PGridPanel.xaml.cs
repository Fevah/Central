using System;
using System.Threading.Tasks;
using DevExpress.Xpf.Grid;
using Central.Engine.Models;

namespace Central.Module.Networking.Links;

public partial class P2PGridPanel : System.Windows.Controls.UserControl
{
    public P2PGridPanel()
    {
        InitializeComponent();
        P2PGrid.MasterRowExpanded += P2PGrid_MasterRowExpanded;
    }

    public GridControl Grid => P2PGrid;
    public TableView View => P2PView;

    public void BindStatusCombo(object statuses) => P2PStatusCombo.ItemsSource = statuses;

    // ── Events delegated to host ──
    public event Func<P2PLink, Task>? SaveLink;
    public event Action<object, CellValueChangedEventArgs>? CellChanged;
    public event Action<string, object>? ConfigCogClicked;

    private async void P2PView_ValidateRow(object sender, GridRowValidationEventArgs e)
    {
        if (e.Row is not P2PLink link) return;
        if (SaveLink != null)
            await SaveLink.Invoke(link);
    }

    private void P2PView_CellValueChanged(object sender, CellValueChangedEventArgs e)
        => CellChanged?.Invoke(sender, e);

    private void ConfigCog_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn && btn.Tag is string side)
        {
            var row = (btn.DataContext as DevExpress.Xpf.Grid.EditGridCellData)?.RowData?.Row;
            ConfigCogClicked?.Invoke(side, row!);
        }
    }

    private void P2PGrid_MasterRowExpanded(object sender, RowEventArgs e)
    {
        if (P2PGrid.GetRow(e.RowHandle) is P2PLink link && link.DetailConfigLines.Count == 0)
            link.GenerateDetailConfig();
    }
}
