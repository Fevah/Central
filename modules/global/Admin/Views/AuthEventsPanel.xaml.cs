using System.Collections.ObjectModel;
using DevExpress.Xpf.Grid;
using Central.Engine.Auth;

namespace Central.Module.Global.Admin;

public partial class AuthEventsPanel : System.Windows.Controls.UserControl
{
    public AuthEventsPanel() => InitializeComponent();

    public GridControl Grid => EventsGrid;
    public System.Windows.Controls.TextBlock Status => StatusLabel;

    public ObservableCollection<AuthEvent> Events { get; } = new();

    public Func<Task>? RefreshRequested { get; set; }

    public void Load(IEnumerable<AuthEvent> events)
    {
        Events.Clear();
        foreach (var e in events) Events.Add(e);
        EventsGrid.ItemsSource = Events;
        StatusLabel.Text = $"{Events.Count} events";
    }

    private async void Refresh_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (RefreshRequested != null) await RefreshRequested();
    }
}
