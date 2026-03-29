using DevExpress.Xpf.Grid;
using Central.Core.Models;

namespace Central.Module.Admin.Views;

/// <summary>
/// App Log grid panel — extracted from MainWindow.
/// Grid with detail pane, toolbar for refresh/delete/clear.
/// </summary>
public partial class AppLogPanel : System.Windows.Controls.UserControl
{
    public AppLogPanel()
    {
        InitializeComponent();
    }

    /// <summary>Expose grid for layout save/restore and external access.</summary>
    public GridControl Grid => AppLogGrid;
    public TableView View => AppLogView;

    /// <summary>Expose count text for host to update after refresh.</summary>
    public System.Windows.Controls.TextBlock CountText => AppLogCountText;

    // ── Events delegated to host ──

    /// <summary>Raised when the Refresh button is clicked.</summary>
    public event Func<Task>? RefreshRequested;

    /// <summary>Raised when the Delete Selected button is clicked.</summary>
    public event Func<Task>? DeleteRequested;

    /// <summary>Raised when the Clear All button is clicked.</summary>
    public event Func<Task>? ClearRequested;

    private async void RefreshAppLogButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (RefreshRequested != null)
            await RefreshRequested.Invoke();
    }

    private async void DeleteAppLogButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DeleteRequested != null)
            await DeleteRequested.Invoke();
    }

    private async void ClearAppLogButton_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (ClearRequested != null)
            await ClearRequested.Invoke();
    }

    private void AppLogGrid_CurrentItemChanged(object sender, CurrentItemChangedEventArgs e)
    {
        if (AppLogGrid.CurrentItem is AppLogEntry entry)
            AppLogDetailText.Text = $"[{entry.Level}] {entry.Tag} — {entry.Source}\n\n{entry.Message}\n\n{entry.Detail}";
        else
            AppLogDetailText.Text = "";
    }
}
