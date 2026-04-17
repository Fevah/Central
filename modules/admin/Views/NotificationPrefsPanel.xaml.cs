using System.Collections.ObjectModel;
using DevExpress.Xpf.Grid;
using Central.Engine.Models;

namespace Central.Module.Admin.Views;

public partial class NotificationPrefsPanel : System.Windows.Controls.UserControl
{
    public NotificationPrefsPanel() => InitializeComponent();

    public GridControl Grid => PrefsGrid;
    public System.Windows.Controls.TextBlock Status => StatusLabel;

    public ObservableCollection<NotificationPreference> Prefs { get; } = new();

    public Func<NotificationPreference, Task>? SavePref { get; set; }
    public Func<Task>? RefreshRequested { get; set; }

    public void Load(IEnumerable<NotificationPreference> prefs)
    {
        Prefs.Clear();

        // Ensure all event types are represented
        var existing = prefs.ToDictionary(p => p.EventType, p => p);
        foreach (var eventType in NotificationEventTypes.All)
        {
            if (existing.TryGetValue(eventType, out var pref))
                Prefs.Add(pref);
            else
                Prefs.Add(new NotificationPreference { EventType = eventType, Channel = "toast", IsEnabled = true });
        }

        PrefsGrid.ItemsSource = Prefs;
        StatusLabel.Text = $"{Prefs.Count} notification types";
    }

    private async void SaveAll_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (SavePref == null) return;
        int saved = 0;
        foreach (var pref in Prefs)
        {
            await SavePref(pref);
            saved++;
        }
        StatusLabel.Text = $"Saved {saved} preferences";
    }

    private async void Refresh_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (RefreshRequested != null) await RefreshRequested();
    }

    private void PrefsView_CellValueChanged(object sender, CellValueChangedEventArgs e)
    {
        if (e.Row is NotificationPreference pref && SavePref != null)
        {
            _ = SavePref(pref);
            StatusLabel.Text = $"Auto-saved: {pref.EventType}";
        }
    }
}
