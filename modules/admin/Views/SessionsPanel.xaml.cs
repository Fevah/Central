using System.Collections.ObjectModel;
using System.Windows;
using DevExpress.Xpf.Grid;
using Central.Core.Models;

namespace Central.Module.Admin.Views;

public partial class SessionsPanel : System.Windows.Controls.UserControl
{
    public SessionsPanel() => InitializeComponent();

    public GridControl Grid => SessionsGrid;
    public System.Windows.Controls.TextBlock Status => StatusLabel;

    public ObservableCollection<ActiveSession> Sessions { get; } = new();

    public Func<int, Task>? ForceLogout { get; set; }
    public Func<int, Task>? ForceLogoutAll { get; set; }
    public Func<Task>? RefreshRequested { get; set; }

    public void Load(IEnumerable<ActiveSession> sessions)
    {
        Sessions.Clear();
        foreach (var s in sessions) Sessions.Add(s);
        SessionsGrid.ItemsSource = Sessions;
        StatusLabel.Text = $"{Sessions.Count} active sessions";
    }

    private async void ForceLogout_Click(object sender, RoutedEventArgs e)
    {
        if (SessionsGrid.SelectedItem is not ActiveSession session) return;
        if (System.Windows.MessageBox.Show($"Force logout {session.Username} from {session.MachineName}?",
            "Confirm", System.Windows.MessageBoxButton.YesNo) != System.Windows.MessageBoxResult.Yes) return;
        if (ForceLogout != null) await ForceLogout(session.Id);
        Sessions.Remove(session);
        StatusLabel.Text = $"Session terminated: {session.Username}";
    }

    private async void ForceLogoutAll_Click(object sender, RoutedEventArgs e)
    {
        if (SessionsGrid.SelectedItem is not ActiveSession session) return;
        if (System.Windows.MessageBox.Show($"Force logout ALL sessions for {session.Username}?",
            "Confirm", System.Windows.MessageBoxButton.YesNo) != System.Windows.MessageBoxResult.Yes) return;
        if (ForceLogoutAll != null) await ForceLogoutAll(session.UserId);
        var toRemove = Sessions.Where(s => s.UserId == session.UserId).ToList();
        foreach (var s in toRemove) Sessions.Remove(s);
        StatusLabel.Text = $"All sessions terminated for {session.Username}";
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        if (RefreshRequested != null) await RefreshRequested();
    }
}
