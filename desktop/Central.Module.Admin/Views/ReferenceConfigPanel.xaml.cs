using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using DevExpress.Xpf.Grid;
using Central.Core.Models;

namespace Central.Module.Admin.Views;

/// <summary>
/// Reference Config panel — editable grid for reference number configurations with auto-save on cell change.
/// </summary>
public partial class ReferenceConfigPanel : System.Windows.Controls.UserControl
{
    public ObservableCollection<ReferenceConfig> Items { get; } = new();

    /// <summary>Delegate to save a reference config record (auto-save on cell change).</summary>
    public Func<ReferenceConfig, Task>? SaveConfig { get; set; }

    /// <summary>Delegate to refresh data from the database.</summary>
    public Func<Task>? RefreshRequested { get; set; }

    public ReferenceConfigPanel()
    {
        InitializeComponent();
        RefConfigGrid.ItemsSource = Items;
    }

    /// <summary>Expose grid for layout save/restore and external access.</summary>
    public GridControl Grid => RefConfigGrid;
    public TableView View => RefConfigView;

    /// <summary>Expose status label for host to update.</summary>
    public System.Windows.Controls.TextBlock Status => StatusLabel;

    /// <summary>Load reference config records into the grid.</summary>
    public void Load(IEnumerable<ReferenceConfig> items)
    {
        Items.Clear();
        foreach (var item in items)
            Items.Add(item);
        StatusLabel.Text = $"{Items.Count} config(s)";
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        Items.Add(new ReferenceConfig
        {
            EntityType = "New",
            Prefix = "",
            Suffix = "",
            PadLength = 6,
            NextValue = 1,
            Description = ""
        });
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        if (SaveConfig != null)
        {
            foreach (var item in Items)
                await SaveConfig.Invoke(item);
            StatusLabel.Text = "Saved.";
        }
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        if (RefreshRequested != null)
            await RefreshRequested.Invoke();
    }

    private async void RefConfigView_CellValueChanged(object sender, CellValueChangedEventArgs e)
    {
        if (RefConfigGrid.CurrentItem is ReferenceConfig config && SaveConfig != null)
            await SaveConfig.Invoke(config);
    }

    private async void RefConfigView_ValidateRow(object sender, GridRowValidationEventArgs e)
    {
        if (e.Row is ReferenceConfig config && SaveConfig != null)
            await SaveConfig.Invoke(config);
    }

    private void View_InvalidRowException(object sender, InvalidRowExceptionEventArgs e)
    {
        e.ExceptionMode = ExceptionMode.NoAction;
    }
}
