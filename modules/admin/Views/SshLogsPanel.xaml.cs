using DevExpress.Xpf.Grid;
using Central.Engine.Models;

namespace Central.Module.Admin.Views;

/// <summary>
/// SSH Logs grid panel — extracted from MainWindow.
/// Grid with success/failure row colouring and detail pane showing log entries + raw output.
/// </summary>
public partial class SshLogsPanel : System.Windows.Controls.UserControl
{
    public SshLogsPanel()
    {
        InitializeComponent();
    }

    /// <summary>Expose grid for layout save/restore and external access.</summary>
    public GridControl Grid => SshLogsGrid;
    public TableView View => SshLogsView;

    /// <summary>Expose count text for host to update after refresh.</summary>
    public System.Windows.Controls.TextBlock CountText => SshLogCountText;

    // ── Events delegated to host ──

    /// <summary>Raised when the Refresh button is clicked.</summary>
    public event Func<Task>? RefreshRequested;

    /// <summary>Raised when the Purge button is clicked.</summary>
    public event Func<Task>? PurgeRequested;

    private async void RefreshSshLogsButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (RefreshRequested != null)
            await RefreshRequested.Invoke();
    }

    private async void PurgeSshLogsButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (PurgeRequested != null)
            await PurgeRequested.Invoke();
    }

    private void SshLogsGrid_CurrentItemChanged(object sender, CurrentItemChangedEventArgs e)
    {
        if (SshLogsGrid.CurrentItem is SshLogEntry entry)
        {
            SshLogEntriesText.Text = entry.LogEntries;
            SshLogRawText.Text = entry.RawOutput;
        }
        else
        {
            SshLogEntriesText.Text = "";
            SshLogRawText.Text = "";
        }
    }
}
