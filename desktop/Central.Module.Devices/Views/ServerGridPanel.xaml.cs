using DevExpress.Xpf.Grid;
using Central.Core.Models;

namespace Central.Module.Devices.Views;

public partial class ServerGridPanel : System.Windows.Controls.UserControl
{
    public ServerGridPanel()
    {
        InitializeComponent();
        ServersGrid.MasterRowExpanded += (_, e) =>
        {
            if (ServersGrid.GetRow(e.RowHandle) is Server srv && srv.DetailNics.Count == 0)
                srv.PopulateNicDetails();
        };
    }

    public GridControl Grid => ServersGrid;
    public TableView View => ServersView;
}
