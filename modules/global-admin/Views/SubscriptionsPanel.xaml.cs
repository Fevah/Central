using DevExpress.Xpf.Grid;
using System.Collections.Generic;

namespace Central.Module.GlobalAdmin.Views;

public partial class SubscriptionsPanel : System.Windows.Controls.UserControl
{
    public SubscriptionsPanel()
    {
        InitializeComponent();
    }

    public GridControl Grid => SubscriptionsGrid;
    public TableView View => SubscriptionsView;

    public void LoadData(List<Dictionary<string, object?>> subs) => SubscriptionsGrid.ItemsSource = subs;
}
