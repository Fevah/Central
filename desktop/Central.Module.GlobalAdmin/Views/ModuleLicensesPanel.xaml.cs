using DevExpress.Xpf.Grid;
using System.Collections.Generic;

namespace Central.Module.GlobalAdmin.Views;

public partial class ModuleLicensesPanel : System.Windows.Controls.UserControl
{
    public ModuleLicensesPanel()
    {
        InitializeComponent();
    }

    public GridControl Grid => LicensesGrid;
    public TableView View => LicensesView;

    public void LoadData(List<Dictionary<string, object?>> licenses) => LicensesGrid.ItemsSource = licenses;
    public Dictionary<string, object?>? SelectedLicense => LicensesGrid.SelectedItem as Dictionary<string, object?>;
}
