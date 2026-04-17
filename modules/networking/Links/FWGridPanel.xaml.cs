using DevExpress.Xpf.Grid;
using Central.Engine.Models;

namespace Central.Module.Networking.Links;

public partial class FWGridPanel : System.Windows.Controls.UserControl
{
    public FWGridPanel()
    {
        InitializeComponent();
        FWGrid.MasterRowExpanded += (_, e) =>
        {
            if (FWGrid.GetRow(e.RowHandle) is FWLink link && link.DetailConfigLines.Count == 0)
                link.GenerateDetailConfig();
        };
    }

    public GridControl Grid => FWGrid;
    public TableView View => FWView;

    public void BindComboSources(object buildings, object statuses)
    {
        FWBuildingCombo.ItemsSource = buildings;
        FWStatusCombo.ItemsSource = statuses;
    }

    // ── Events delegated to host ──
    public event Func<FWLink, Task>? SaveLink;
    public event Action<object, CellValueChangedEventArgs>? CellChanged;
    public event Action<string, object>? ConfigCogClicked;  // side ("A"/"B"), row

    private async void FWView_ValidateRow(object sender, GridRowValidationEventArgs e)
    {
        if (e.Row is not FWLink link) return;
        if (SaveLink != null)
            await SaveLink.Invoke(link);
    }

    private void FWView_CellValueChanged(object sender, CellValueChangedEventArgs e)
        => CellChanged?.Invoke(sender, e);

    private void ConfigCog_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button btn && btn.Tag is string side)
        {
            var row = (btn.DataContext as DevExpress.Xpf.Grid.EditGridCellData)?.RowData?.Row;
            ConfigCogClicked?.Invoke(side, row!);
        }
    }
}
