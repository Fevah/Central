using DevExpress.Xpf.Grid;

namespace Central.Module.GlobalAdmin.Views;

public partial class SubscriptionsPanel : System.Windows.Controls.UserControl
{
    public SubscriptionsPanel()
    {
        InitializeComponent();
    }

    public GridControl Grid => SubscriptionsGrid;
    public TableView View => SubscriptionsView;
}
