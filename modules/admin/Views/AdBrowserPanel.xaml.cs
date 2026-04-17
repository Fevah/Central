using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using DevExpress.Xpf.Grid;
using Central.Engine.Models;

namespace Central.Module.Admin.Views;

/// <summary>
/// AD Browser panel — read-only grid of Active Directory users with import/sync actions.
/// </summary>
public partial class AdBrowserPanel : System.Windows.Controls.UserControl
{
    public ObservableCollection<AdUser> Items { get; } = new();

    /// <summary>Delegate to browse AD and populate the grid.</summary>
    public Func<Task>? BrowseAd { get; set; }

    /// <summary>Delegate to import the selected AD users into Central.</summary>
    public Func<Task>? ImportSelected { get; set; }

    /// <summary>Delegate to sync all AD users.</summary>
    public Func<Task>? SyncAll { get; set; }

    public AdBrowserPanel()
    {
        InitializeComponent();
        AdGrid.ItemsSource = Items;
    }

    /// <summary>Expose grid for layout save/restore and external access.</summary>
    public GridControl Grid => AdGrid;
    public TableView View => AdView;

    /// <summary>Expose status label for host to update.</summary>
    public System.Windows.Controls.TextBlock Status => StatusLabel;

    /// <summary>Load AD users into the grid.</summary>
    public void Load(IEnumerable<AdUser> items)
    {
        Items.Clear();
        foreach (var item in items)
            Items.Add(item);
        StatusLabel.Text = $"{Items.Count} AD user(s)";
    }

    private async void BrowseAd_Click(object sender, RoutedEventArgs e)
    {
        if (BrowseAd != null)
            await BrowseAd.Invoke();
    }

    private async void ImportSelected_Click(object sender, RoutedEventArgs e)
    {
        if (ImportSelected != null)
            await ImportSelected.Invoke();
    }

    private async void SyncAll_Click(object sender, RoutedEventArgs e)
    {
        if (SyncAll != null)
            await SyncAll.Invoke();
    }
}
