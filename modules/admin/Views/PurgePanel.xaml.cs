using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using DevExpress.Xpf.Grid;

namespace Central.Module.Admin.Views;

/// <summary>Simple inline model for purge table display.</summary>
public class PurgeItem : INotifyPropertyChanged
{
    private string _tableName = "";
    private int _deletedCount;

    public string TableName { get => _tableName; set { _tableName = value; OnPropertyChanged(); } }
    public int DeletedCount { get => _deletedCount; set { _deletedCount = value; OnPropertyChanged(); } }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>
/// Purge panel — grid showing soft-deleted record counts per table with purge actions.
/// </summary>
public partial class PurgePanel : System.Windows.Controls.UserControl
{
    public ObservableCollection<PurgeItem> Items { get; } = new();

    /// <summary>Delegate to purge a specific table. Receives table name.</summary>
    public Func<string, Task>? PurgeTable { get; set; }

    /// <summary>Delegate to purge all soft-deleted records across all tables.</summary>
    public Func<Task>? PurgeAll { get; set; }

    /// <summary>Delegate to refresh the deleted counts.</summary>
    public Func<Task>? RefreshRequested { get; set; }

    public PurgePanel()
    {
        InitializeComponent();
        PurgeGrid.ItemsSource = Items;
    }

    /// <summary>Expose grid for layout save/restore and external access.</summary>
    public GridControl Grid => PurgeGrid;
    public TableView View => PurgeView;

    /// <summary>Expose status label for host to update.</summary>
    public System.Windows.Controls.TextBlock Status => StatusLabel;

    /// <summary>Load purge counts into the grid from a dictionary of table name to deleted count.</summary>
    public void Load(Dictionary<string, int> counts)
    {
        Items.Clear();
        foreach (var kvp in counts)
            Items.Add(new PurgeItem { TableName = kvp.Key, DeletedCount = kvp.Value });

        var total = 0;
        foreach (var kvp in counts)
            total += kvp.Value;
        StatusLabel.Text = $"{counts.Count} table(s), {total} deleted record(s)";
    }

    private async void PurgeSelected_Click(object sender, RoutedEventArgs e)
    {
        if (PurgeGrid.CurrentItem is PurgeItem item && PurgeTable != null)
        {
            var result = System.Windows.MessageBox.Show(
                $"Permanently purge {item.DeletedCount} deleted record(s) from '{item.TableName}'?",
                "Confirm Purge", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
                await PurgeTable.Invoke(item.TableName);
        }
    }

    private async void PurgeAll_Click(object sender, RoutedEventArgs e)
    {
        if (PurgeAll != null)
        {
            var result = System.Windows.MessageBox.Show(
                "Permanently purge ALL soft-deleted records from all tables?",
                "Confirm Purge All", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
                await PurgeAll.Invoke();
        }
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        if (RefreshRequested != null)
            await RefreshRequested.Invoke();
    }
}
