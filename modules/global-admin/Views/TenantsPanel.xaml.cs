using DevExpress.Xpf.Grid;
using System.Collections.Generic;

namespace Central.Module.GlobalAdmin.Views;

public partial class TenantsPanel : System.Windows.Controls.UserControl
{
    public TenantsPanel()
    {
        InitializeComponent();
    }

    public GridControl Grid => TenantsGrid;
    public TableView View => TenantsView;

    public void LoadData(List<Dictionary<string, object?>> tenants)
    {
        TenantsGrid.ItemsSource = tenants;
    }

    public Dictionary<string, object?>? SelectedTenant =>
        TenantsGrid.SelectedItem as Dictionary<string, object?>;
}
