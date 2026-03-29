using DevExpress.Xpf.Grid;

namespace Central.Module.GlobalAdmin.Views;

public partial class TenantsPanel : System.Windows.Controls.UserControl
{
    public TenantsPanel()
    {
        InitializeComponent();
    }

    public GridControl Grid => TenantsGrid;
    public TableView View => TenantsView;
}
