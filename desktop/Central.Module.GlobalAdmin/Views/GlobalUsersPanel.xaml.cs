using DevExpress.Xpf.Grid;

namespace Central.Module.GlobalAdmin.Views;

public partial class GlobalUsersPanel : System.Windows.Controls.UserControl
{
    public GlobalUsersPanel()
    {
        InitializeComponent();
    }

    public GridControl Grid => UsersGrid;
    public TableView View => UsersView;
}
