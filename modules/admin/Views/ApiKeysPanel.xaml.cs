using System.Collections.ObjectModel;
using System.Windows;
using DevExpress.Xpf.Grid;
using Central.Engine.Models;

namespace Central.Module.Admin.Views;

public partial class ApiKeysPanel : System.Windows.Controls.UserControl
{
    public ApiKeysPanel() => InitializeComponent();

    public GridControl Grid => KeysGrid;
    public TableView View => KeysView;
    public System.Windows.Controls.TextBlock Status => StatusLabel;

    public ObservableCollection<ApiKeyRecord> Keys { get; } = new();

    public Func<string, string, Task<string>>? GenerateKey { get; set; }
    public Func<int, Task>? RevokeKey { get; set; }
    public Func<int, Task>? DeleteKey { get; set; }
    public Func<Task>? RefreshRequested { get; set; }

    public void Load(IEnumerable<ApiKeyRecord> keys)
    {
        Keys.Clear();
        foreach (var k in keys) Keys.Add(k);
        KeysGrid.ItemsSource = Keys;
        StatusLabel.Text = $"{Keys.Count} API keys";
    }

    private async void Generate_Click(object sender, RoutedEventArgs e)
    {
        if (GenerateKey == null) return;

        var name = Microsoft.VisualBasic.Interaction.InputBox("API Key Name:", "Generate API Key", "Service Account");
        if (string.IsNullOrWhiteSpace(name)) return;

        var rawKey = await GenerateKey(name, "Viewer");

        System.Windows.MessageBox.Show(
            $"API Key generated. Copy it now — it won't be shown again:\n\n{rawKey}",
            "API Key Created", System.Windows.MessageBoxButton.OK);

        System.Windows.Clipboard.SetText(rawKey);
        StatusLabel.Text = $"Key '{name}' created and copied to clipboard";

        if (RefreshRequested != null) await RefreshRequested();
    }

    private async void Revoke_Click(object sender, RoutedEventArgs e)
    {
        if (KeysGrid.SelectedItem is not ApiKeyRecord key) return;
        if (RevokeKey != null) await RevokeKey(key.Id);
        key.IsActive = false;
        StatusLabel.Text = $"Revoked: {key.Name}";
    }

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (KeysGrid.SelectedItem is not ApiKeyRecord key) return;
        if (System.Windows.MessageBox.Show($"Delete API key '{key.Name}'?", "Confirm",
            System.Windows.MessageBoxButton.YesNo) != System.Windows.MessageBoxResult.Yes) return;
        if (DeleteKey != null) await DeleteKey(key.Id);
        Keys.Remove(key);
        StatusLabel.Text = $"Deleted: {key.Name}";
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        if (RefreshRequested != null) await RefreshRequested();
    }
}
