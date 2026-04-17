using System.Collections.ObjectModel;
using Central.Engine.Models;

namespace Central.Module.Admin.Views;

/// <summary>
/// Dialog for managing saved filter presets per panel.
/// Users can save the current grid filter, load saved filters, set a default.
/// </summary>
public partial class SavedFilterDialog : DevExpress.Xpf.Core.DXWindow
{
    public SavedFilterDialog() => InitializeComponent();

    public ObservableCollection<SavedFilter> Filters { get; } = new();

    /// <summary>The filter string to apply when user clicks Apply.</summary>
    public string? SelectedFilterString { get; private set; }

    /// <summary>The current grid filter string to save.</summary>
    public string CurrentFilterString { get; set; } = "";

    public string PanelName { get; set; } = "";

    // Delegates
    public Func<string, string, Task>? SaveFilter { get; set; }
    public Func<int, Task>? DeleteFilter { get; set; }
    public Func<int, Task>? SetDefaultFilter { get; set; }

    public void Load(IEnumerable<SavedFilter> filters)
    {
        Filters.Clear();
        foreach (var f in filters) Filters.Add(f);
        FiltersGrid.ItemsSource = Filters;
    }

    private async void SaveCurrent_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        var name = FilterNameEdit.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(name))
        {
            StatusLabel.Text = "Enter a filter name";
            return;
        }
        if (string.IsNullOrEmpty(CurrentFilterString))
        {
            StatusLabel.Text = "No active filter to save";
            return;
        }

        if (SaveFilter != null) await SaveFilter(name, CurrentFilterString);
        StatusLabel.Text = $"Saved: {name}";
    }

    private void Apply_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (FiltersGrid.SelectedItem is SavedFilter f)
        {
            SelectedFilterString = f.FilterExpr;
            DialogResult = true;
        }
    }

    private async void SetDefault_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (FiltersGrid.SelectedItem is not SavedFilter f) return;
        foreach (var filter in Filters) filter.IsDefault = false;
        f.IsDefault = true;
        if (SetDefaultFilter != null) await SetDefaultFilter(f.Id);
        StatusLabel.Text = $"Default set: {f.FilterName}";
    }

    private async void Delete_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (FiltersGrid.SelectedItem is not SavedFilter f) return;
        if (f.Id > 0 && DeleteFilter != null) await DeleteFilter(f.Id);
        Filters.Remove(f);
        StatusLabel.Text = "Filter deleted";
    }

    private void Close_Click(object sender, System.Windows.RoutedEventArgs e) => Close();
}
