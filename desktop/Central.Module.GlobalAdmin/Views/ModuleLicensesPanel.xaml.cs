using DevExpress.Xpf.Grid;

namespace Central.Module.GlobalAdmin.Views;

public partial class ModuleLicensesPanel : System.Windows.Controls.UserControl
{
    public ModuleLicensesPanel()
    {
        InitializeComponent();
    }

    public GridControl Grid => LicensesGrid;
    public TableView View => LicensesView;
}
