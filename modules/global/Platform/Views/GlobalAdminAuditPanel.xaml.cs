using System.Collections.Generic;
using Central.Engine.Models;
using DevExpress.Xpf.Grid;

namespace Central.Module.Global.Platform;

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
