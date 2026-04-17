using System.Collections.ObjectModel;
using DevExpress.Xpf.Grid;
using DevExpress.Xpf.Grid.TreeList;
using Central.Engine.Models;

namespace Central.Module.Global.Admin;

public partial class RolesPanel : System.Windows.Controls.UserControl
{
    private readonly ObservableCollection<PermissionNode> _permissionNodes = new();

    public RolesPanel()
    {
        InitializeComponent();
        RolesGrid.MasterRowExpanded += RolesGrid_MasterRowExpanded;
    }

    /// <summary>Fired when role row expanded — host loads users with this role.</summary>
    public event System.Func<RoleRecord, System.Threading.Tasks.Task>? LoadDetailUsers;

    private async void RolesGrid_MasterRowExpanded(object sender, RowEventArgs e)
    {
        if (RolesGrid.GetRow(e.RowHandle) is RoleRecord role && role.DetailUsers.Count == 0)
        {
            if (LoadDetailUsers != null)
                await LoadDetailUsers(role);
        }
    }

    public GridControl Grid => RolesGrid;
    public TableView View => RolesView;
    public TreeListControl PermTree => PermissionsTree;
    public TreeListView PermView => PermissionsTreeView;

    // ── Events delegated to host ──
    public event Func<RoleRecord, Task>? SaveRole;
    public event Action<RoleSiteAccess>? SiteAccessToggled;
    public event Action<RoleRecord>? RoleSelectionChanged;

    // ── Permission tree logic (moved from MainWindow.xaml.cs) ──

    public void LoadPermissionTreeForRole(RoleRecord? role)
    {
        _permissionNodes.Clear();
        if (role == null) { PermissionsTree.ItemsSource = _permissionNodes; return; }

        _permissionNodes.Add(new PermissionNode { Key = "devices",  ParentKey = "", DisplayName = "Devices",  Module = "devices" });
        _permissionNodes.Add(new PermissionNode { Key = "switches", ParentKey = "", DisplayName = "Switches", Module = "switches" });
        _permissionNodes.Add(new PermissionNode { Key = "admin",    ParentKey = "", DisplayName = "Admin",    Module = "admin" });

        _permissionNodes.Add(new PermissionNode { Key = "devices.view",     ParentKey = "devices",  DisplayName = "View",          Module = "devices",  Permission = "View",         IsEnabled = role.DevicesView });
        _permissionNodes.Add(new PermissionNode { Key = "devices.edit",     ParentKey = "devices",  DisplayName = "Edit",          Module = "devices",  Permission = "Edit",         IsEnabled = role.DevicesEdit });
        _permissionNodes.Add(new PermissionNode { Key = "devices.delete",   ParentKey = "devices",  DisplayName = "Delete",        Module = "devices",  Permission = "Delete",       IsEnabled = role.DevicesDelete });
        _permissionNodes.Add(new PermissionNode { Key = "devices.reserved", ParentKey = "devices",  DisplayName = "View Reserved", Module = "devices",  Permission = "ViewReserved", IsEnabled = role.DevicesViewReserved });

        _permissionNodes.Add(new PermissionNode { Key = "switches.view",   ParentKey = "switches", DisplayName = "View",   Module = "switches", Permission = "View",   IsEnabled = role.SwitchesView });
        _permissionNodes.Add(new PermissionNode { Key = "switches.edit",   ParentKey = "switches", DisplayName = "Edit",   Module = "switches", Permission = "Edit",   IsEnabled = role.SwitchesEdit });
        _permissionNodes.Add(new PermissionNode { Key = "switches.delete", ParentKey = "switches", DisplayName = "Delete", Module = "switches", Permission = "Delete", IsEnabled = role.SwitchesDelete });

        _permissionNodes.Add(new PermissionNode { Key = "admin.view",   ParentKey = "admin", DisplayName = "View",   Module = "admin", Permission = "View",   IsEnabled = role.AdminView });
        _permissionNodes.Add(new PermissionNode { Key = "admin.edit",   ParentKey = "admin", DisplayName = "Edit",   Module = "admin", Permission = "Edit",   IsEnabled = role.AdminEdit });
        _permissionNodes.Add(new PermissionNode { Key = "admin.delete", ParentKey = "admin", DisplayName = "Delete", Module = "admin", Permission = "Delete", IsEnabled = role.AdminDelete });

        PermissionsTree.ItemsSource = _permissionNodes;
    }

    /// <summary>Sync tree checkboxes back to RoleRecord and auto-save.</summary>
    private async void PermissionsTree_CellValueChanged(object sender, TreeListCellValueChangedEventArgs e)
    {
        if (e.Column.FieldName != "IsEnabled") return;
        if (e.Row is not PermissionNode node) return;

        // Parent toggle → set all children
        if (string.IsNullOrEmpty(node.Permission))
        {
            foreach (var child in _permissionNodes.Where(n => n.ParentKey == node.Key))
                child.IsEnabled = node.IsEnabled;
        }

        // Sync back to bound RoleRecord
        var role = Grid.CurrentItem as RoleRecord;
        if (role == null) return;

        foreach (var n in _permissionNodes.Where(n => !string.IsNullOrEmpty(n.Permission)))
        {
            switch (n.Module + "." + n.Permission)
            {
                case "devices.View": role.DevicesView = n.IsEnabled; break;
                case "devices.Edit": role.DevicesEdit = n.IsEnabled; break;
                case "devices.Delete": role.DevicesDelete = n.IsEnabled; break;
                case "devices.ViewReserved": role.DevicesViewReserved = n.IsEnabled; break;
                case "switches.View": role.SwitchesView = n.IsEnabled; break;
                case "switches.Edit": role.SwitchesEdit = n.IsEnabled; break;
                case "switches.Delete": role.SwitchesDelete = n.IsEnabled; break;
                case "admin.View": role.AdminView = n.IsEnabled; break;
                case "admin.Edit": role.AdminEdit = n.IsEnabled; break;
                case "admin.Delete": role.AdminDelete = n.IsEnabled; break;
            }
        }

        if (role.Id > 0 && SaveRole != null)
            await SaveRole.Invoke(role);
    }

    private void SiteAccessChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.CheckBox cb && cb.DataContext is RoleSiteAccess site)
            SiteAccessToggled?.Invoke(site);
    }
}
