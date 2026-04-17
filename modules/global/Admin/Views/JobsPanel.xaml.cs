using System;
using System.Threading.Tasks;

namespace Central.Module.Global.Admin;

public partial class JobsPanel : System.Windows.Controls.UserControl
{
    public JobsPanel() => InitializeComponent();

    public DevExpress.Xpf.Grid.GridControl SchedulesGridControl => SchedulesGrid;
    public DevExpress.Xpf.Grid.GridControl HistoryGridControl => HistoryGrid;

    /// <summary>Fires when Enable/Disable/Run/Refresh is clicked. Args: action ("enable"/"disable"/"run"/"refresh"), jobId.</summary>
    public event Func<string, int, Task>? JobActionRequested;

    private int GetSelectedJobId()
    {
        var row = SchedulesGrid.CurrentItem;
        if (row == null) return 0;
        var idProp = row.GetType().GetProperty("Id");
        if (idProp != null) return (int)(idProp.GetValue(row) ?? 0);
        return 0;
    }

    private async void EnableJobButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        var id = GetSelectedJobId();
        if (id > 0 && JobActionRequested != null) await JobActionRequested("enable", id);
    }

    private async void DisableJobButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        var id = GetSelectedJobId();
        if (id > 0 && JobActionRequested != null) await JobActionRequested("disable", id);
    }

    private async void RunJobButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        var id = GetSelectedJobId();
        if (id > 0 && JobActionRequested != null) await JobActionRequested("run", id);
    }

    private async void RefreshJobsButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (JobActionRequested != null) await JobActionRequested("refresh", 0);
    }
}
