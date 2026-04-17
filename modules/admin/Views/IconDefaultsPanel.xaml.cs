using System.Collections.ObjectModel;
using System.Windows;
using DevExpress.Xpf.Grid;
using Central.Core.Models;

namespace Central.Module.Admin.Views;

public partial class IconDefaultsPanel : System.Windows.Controls.UserControl
{
    public IconDefaultsPanel()
    {
        InitializeComponent();
    }

    public GridControl Grid => DefaultsGrid;
    public TableView View => DefaultsView;

    public ObservableCollection<IconOverride> Items { get; } = new();

    // ── Delegates wired by shell ──
    public Func<IconOverride, Task>? SaveDefault { get; set; }
    public Func<IconOverride, Task>? DeleteDefault { get; set; }
    public Func<Task>? RefreshRequested { get; set; }

    public void Load(IEnumerable<IconOverride> items)
    {
        Items.Clear();
        foreach (var item in items)
            Items.Add(item);
        DefaultsGrid.ItemsSource = Items;
        StatusLabel.Text = $"{Items.Count} defaults";
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        var newItem = new IconOverride { Context = "status.device", ElementKey = "New" };
        Items.Add(newItem);
        DefaultsGrid.SelectedItem = newItem;
        DefaultsView.FocusedRowHandle = DefaultsGrid.GetRowHandleByListIndex(Items.Count - 1);
    }

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (DefaultsGrid.SelectedItem is not IconOverride selected) return;
        if (System.Windows.MessageBox.Show($"Delete default '{selected.Context}/{selected.ElementKey}'?",
            "Confirm", System.Windows.MessageBoxButton.YesNo) != System.Windows.MessageBoxResult.Yes) return;

        try
        {
            if (DeleteDefault != null) await DeleteDefault(selected);
            Items.Remove(selected);
            StatusLabel.Text = $"Deleted. {Items.Count} defaults";
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"Error: {ex.Message}";
        }
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        if (DefaultsGrid.SelectedItem is not IconOverride selected) return;
        if (string.IsNullOrWhiteSpace(selected.Context) || string.IsNullOrWhiteSpace(selected.ElementKey))
        {
            StatusLabel.Text = "Context and Element Key are required";
            return;
        }

        try
        {
            if (SaveDefault != null) await SaveDefault(selected);
            StatusLabel.Text = $"Saved: {selected.Context}/{selected.ElementKey}";
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"Error: {ex.Message}";
        }
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        if (RefreshRequested != null) await RefreshRequested();
    }

    private void DefaultsView_CellValueChanged(object sender, CellValueChangedEventArgs e)
    {
        if (e.Row is IconOverride item && SaveDefault != null
            && !string.IsNullOrWhiteSpace(item.Context) && !string.IsNullOrWhiteSpace(item.ElementKey))
        {
            _ = SaveDefault(item);
            StatusLabel.Text = $"Saved: {item.Context}/{item.ElementKey}";
        }
    }
}
