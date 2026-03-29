using System.Collections.ObjectModel;
using System.Windows;
using DevExpress.Xpf.Grid;
using Central.Core.Auth;

namespace Central.Module.Admin.Views;

public partial class IdentityProvidersPanel : System.Windows.Controls.UserControl
{
    public IdentityProvidersPanel() => InitializeComponent();

    public GridControl Grid => ProvidersGrid;
    public TableView View => ProvidersView;
    public System.Windows.Controls.TextBlock Status => StatusLabel;

    public ObservableCollection<IdentityProviderConfig> Providers { get; } = new();
    public ObservableCollection<IdpDomainMapping> DomainMappings { get; } = new();

    // Delegates
    public Func<IdentityProviderConfig, Task>? SaveProvider { get; set; }
    public Func<int, Task>? DeleteProvider { get; set; }
    public Func<string, int, Task>? SaveDomainMapping { get; set; }
    public Func<Task>? RefreshRequested { get; set; }

    public void Load(IEnumerable<IdentityProviderConfig> providers, IEnumerable<IdpDomainMapping> domains)
    {
        Providers.Clear();
        foreach (var p in providers) Providers.Add(p);
        ProvidersGrid.ItemsSource = Providers;

        DomainMappings.Clear();
        foreach (var d in domains) DomainMappings.Add(d);
        DomainGrid.ItemsSource = DomainMappings;

        StatusLabel.Text = $"{Providers.Count} providers, {DomainMappings.Count} domain mappings";
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        var p = new IdentityProviderConfig { ProviderType = "entra_id", Name = "New Provider" };
        Providers.Add(p);
        ProvidersGrid.SelectedItem = p;
    }

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (ProvidersGrid.SelectedItem is not IdentityProviderConfig p) return;
        if (System.Windows.MessageBox.Show($"Delete provider '{p.Name}'?", "Confirm",
            System.Windows.MessageBoxButton.YesNo) != System.Windows.MessageBoxResult.Yes) return;
        if (p.Id > 0 && DeleteProvider != null) await DeleteProvider(p.Id);
        Providers.Remove(p);
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        if (ProvidersGrid.SelectedItem is not IdentityProviderConfig p) return;
        if (SaveProvider != null) await SaveProvider(p);
        StatusLabel.Text = $"Saved: {p.Name}";
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        if (RefreshRequested != null) await RefreshRequested();
    }

    private void ProvidersGrid_CurrentItemChanged(object sender, CurrentItemChangedEventArgs e) { }

    private void ProvidersView_CellValueChanged(object sender, CellValueChangedEventArgs e)
    {
        if (e.Row is IdentityProviderConfig p && SaveProvider != null && !string.IsNullOrWhiteSpace(p.Name))
        {
            _ = SaveProvider(p);
            StatusLabel.Text = $"Auto-saved: {p.Name}";
        }
    }
}
