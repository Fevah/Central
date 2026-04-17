using System;
using System.Threading.Tasks;
using DevExpress.Xpf.Grid;
using Central.Engine.Models;

namespace Central.Module.Routing.Views;

public partial class BgpGridPanel : System.Windows.Controls.UserControl
{
    public BgpGridPanel()
    {
        InitializeComponent();
        BgpGrid.MasterRowExpanded += BgpGrid_MasterRowExpanded;
    }

    public GridControl Grid => BgpGrid;
    public TableView View => BgpView;

    // ── Events delegated to host ──
    public event Action<object?>? CurrentItemChanged;
    /// <summary>Fired when a master row is expanded — host loads neighbors + networks.</summary>
    public event Func<BgpRecord, Task>? LoadDetailData;

    private void BgpGrid_CurrentItemChanged(object sender, CurrentItemChangedEventArgs e)
        => CurrentItemChanged?.Invoke(e.NewItem);

    private async void BgpGrid_MasterRowExpanded(object sender, RowEventArgs e)
    {
        if (BgpGrid.GetRow(e.RowHandle) is BgpRecord bgp && bgp.DetailNeighbors.Count == 0)
        {
            if (LoadDetailData != null)
                await LoadDetailData(bgp);
        }
    }
}
