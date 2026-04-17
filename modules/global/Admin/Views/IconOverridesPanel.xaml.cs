using System.Collections.ObjectModel;
using System.Windows;
using DevExpress.Xpf.Grid;
using Central.Engine.Models;

namespace Central.Module.Global.Admin;

public partial class IconOverridesPanel : System.Windows.Controls.UserControl
{
    public IconOverridesPanel()
    {
        InitializeComponent();
    }

    public GridControl Grid => OverridesGrid;
    public TableView View => OverridesView;

    public ObservableCollection<IconOverride> Items { get; } = new();

    // ── Delegates wired by shell ──
    public Func<IconOverride, Task>? SaveOverride { get; set; }
    public Func<IconOverride, Task>? DeleteOverride { get; set; }
    public Func<Task>? ResetAllRequested { get; set; }
    public Func<Task>? RefreshRequested { get; set; }

    public void Load(IEnumerable<IconOverride> items)
    {
        Items.Clear();
        foreach (var item in items)
            Items.Add(item);
        OverridesGrid.ItemsSource = Items;
        StatusLabel.Text = $"{Items.Count} overrides";
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        var newItem = new IconOverride { Context = "status.device", ElementKey = "New" };
        Items.Add(newItem);
        OverridesGrid.SelectedItem = newItem;
        OverridesView.FocusedRowHandle = OverridesGrid.GetRowHandleByListIndex(Items.Count - 1);
    }

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (OverridesGrid.SelectedItem is not IconOverride selected) return;
        if (System.Windows.MessageBox.Show($"Remove your override for '{selected.Context}/{selected.ElementKey}'?\nThe admin default will be used instead.",
            "Confirm", System.Windows.MessageBoxButton.YesNo) != System.Windows.MessageBoxResult.Yes) return;

        try
        {
            if (DeleteOverride != null) await DeleteOverride(selected);
            Items.Remove(selected);
            StatusLabel.Text = $"Removed. {Items.Count} overrides";
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"Error: {ex.Message}";
        }
    }

    private async void ResetAll_Click(object sender, RoutedEventArgs e)
    {
        if (Items.Count == 0) return;
        if (System.Windows.MessageBox.Show("Remove all your icon overrides? Admin defaults will be used.",
            "Reset All", System.Windows.MessageBoxButton.YesNo) != System.Windows.MessageBoxResult.Yes) return;

        try
        {
            if (ResetAllRequested != null) await ResetAllRequested();
            Items.Clear();
            OverridesGrid.ItemsSource = Items;
            StatusLabel.Text = "All overrides removed";
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

    private void OverridesView_CellValueChanged(object sender, CellValueChangedEventArgs e)
    {
        if (e.Row is IconOverride item && SaveOverride != null
            && !string.IsNullOrWhiteSpace(item.Context) && !string.IsNullOrWhiteSpace(item.ElementKey))
        {
            _ = SaveOverride(item);
            StatusLabel.Text = $"Saved: {item.Context}/{item.ElementKey}";
        }
    }
}
