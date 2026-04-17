using System.Collections.ObjectModel;
using System.Windows;
using DevExpress.Xpf.Grid;
using Central.Engine.Integration;

namespace Central.Module.Admin.Views;

public partial class SyncConfigPanel : System.Windows.Controls.UserControl
{
    public SyncConfigPanel() => InitializeComponent();

    public GridControl Grid => ConfigsGrid;
    public TableView View => ConfigsView;
    public System.Windows.Controls.TextBlock Status => StatusLabel;

    public ObservableCollection<SyncConfig> Configs { get; } = new();
    public ObservableCollection<SyncLogEntry> LogEntries { get; } = new();

    // Delegates
    public Func<SyncConfig, Task>? SaveConfig { get; set; }
    public Func<SyncConfig, Task>? RunSync { get; set; }
    public Func<SyncConfig, Task<string>>? TestConnection { get; set; }
    public Func<int, Task<List<SyncLogEntry>>>? LoadLog { get; set; }
    public Func<Task>? RefreshRequested { get; set; }

    public void Load(IEnumerable<SyncConfig> configs)
    {
        Configs.Clear();
        foreach (var c in configs) Configs.Add(c);
        ConfigsGrid.ItemsSource = Configs;
        StatusLabel.Text = $"{Configs.Count} sync configurations";
    }

    private void AddConfig_Click(object sender, RoutedEventArgs e)
    {
        var config = new SyncConfig { Name = "New Integration", AgentType = "rest_api" };
        Configs.Add(config);
        ConfigsGrid.SelectedItem = config;
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        if (ConfigsGrid.SelectedItem is not SyncConfig config) return;
        if (SaveConfig != null) await SaveConfig(config);
        StatusLabel.Text = $"Saved: {config.Name}";
    }

    private async void RunSync_Click(object sender, RoutedEventArgs e)
    {
        if (ConfigsGrid.SelectedItem is not SyncConfig config) return;
        if (!config.IsEnabled) { StatusLabel.Text = "Enable the config first"; return; }
        StatusLabel.Text = $"Running sync: {config.Name}...";
        if (RunSync != null) await RunSync(config);
    }

    private async void TestConnection_Click(object sender, RoutedEventArgs e)
    {
        if (ConfigsGrid.SelectedItem is not SyncConfig config) return;
        StatusLabel.Text = "Testing connection...";
        if (TestConnection != null)
        {
            var result = await TestConnection(config);
            StatusLabel.Text = result;
        }
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        if (RefreshRequested != null) await RefreshRequested();
    }

    private async void ConfigsGrid_CurrentItemChanged(object sender, CurrentItemChangedEventArgs e)
    {
        if (e.NewItem is SyncConfig config && config.Id > 0 && LoadLog != null)
        {
            var log = await LoadLog(config.Id);
            LogEntries.Clear();
            foreach (var entry in log) LogEntries.Add(entry);
            LogGrid.ItemsSource = LogEntries;
        }
    }

    private void ConfigsView_CellValueChanged(object sender, CellValueChangedEventArgs e)
    {
        if (e.Row is SyncConfig config && SaveConfig != null && !string.IsNullOrWhiteSpace(config.Name))
        {
            _ = SaveConfig(config);
            StatusLabel.Text = $"Auto-saved: {config.Name}";
        }
    }
}
