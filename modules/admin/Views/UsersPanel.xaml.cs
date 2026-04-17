using System;
using System.Threading.Tasks;
using DevExpress.Xpf.Grid;
using Central.Core.Models;

namespace Central.Module.Admin.Views;

public partial class UsersPanel : System.Windows.Controls.UserControl
{
    public UsersPanel()
    {
        InitializeComponent();
        UsersGrid.MasterRowExpanded += UsersGrid_MasterRowExpanded;
    }

    public GridControl Grid => UsersGrid;
    public TableView View => UsersView;

    public void BindComboSources(object? roles)
    {
        if (roles != null) UserRoleCombo.ItemsSource = roles;
    }

    // ── Events delegated to host ──
    public event Func<AppUser, Task>? SaveUser;
    /// <summary>Fired when user row expanded — host loads permissions for the user's role.</summary>
    public event Func<AppUser, Task>? LoadDetailPermissions;

    private async void UsersGrid_MasterRowExpanded(object sender, RowEventArgs e)
    {
        if (UsersGrid.GetRow(e.RowHandle) is AppUser user && user.DetailPermissions.Count == 0)
        {
            if (LoadDetailPermissions != null)
                await LoadDetailPermissions(user);
        }
    }
}
