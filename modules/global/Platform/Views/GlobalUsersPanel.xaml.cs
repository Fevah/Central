using DevExpress.Xpf.Grid;
using System.Collections.Generic;

namespace Central.Module.Global.Platform;

public partial class GlobalUsersPanel : System.Windows.Controls.UserControl
{
    public GlobalUsersPanel()
    {
        InitializeComponent();
    }

    public GridControl Grid => UsersGrid;
    public TableView View => UsersView;

    public void LoadData(List<Dictionary<string, object?>> users) => UsersGrid.ItemsSource = users;
    public Dictionary<string, object?>? SelectedUser => UsersGrid.SelectedItem as Dictionary<string, object?>;
}
