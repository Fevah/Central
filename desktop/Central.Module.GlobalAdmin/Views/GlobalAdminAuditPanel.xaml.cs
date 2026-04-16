using System.Collections.Generic;
using Central.Core.Models;
using DevExpress.Xpf.Grid;

namespace Central.Module.GlobalAdmin.Views;

public partial class GlobalAdminAuditPanel : System.Windows.Controls.UserControl
{
    public GlobalAdminAuditPanel()
    {
        InitializeComponent();
    }

    public GridControl Grid => AuditGrid;
    public TableView View => AuditView;

    public void LoadData(List<AuditLogEntry> entries)
    {
        AuditGrid.ItemsSource = entries;
    }
}
