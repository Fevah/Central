using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using DevExpress.Xpf.Grid;
using Central.Engine.Models;

using Country = Central.Engine.Models.Country;
using Region = Central.Engine.Models.Region;

namespace Central.Module.Admin.Views;

/// <summary>
/// Locations panel — master-detail: Countries grid on top, Regions grid on bottom filtered by selected country.
/// </summary>
public partial class LocationsPanel : System.Windows.Controls.UserControl
{
    public ObservableCollection<Country> Countries { get; } = new();
    public ObservableCollection<Region> Regions { get; } = new();

    private readonly ObservableCollection<Region> _filteredRegions = new();
    private Country? _selectedCountry;

    /// <summary>Delegate to save a country record.</summary>
    public Func<Country, Task>? SaveCountry { get; set; }

    /// <summary>Delegate to save a region record.</summary>
    public Func<Region, Task>? SaveRegion { get; set; }

    /// <summary>Delegate to delete a country record.</summary>
    public Func<Country, Task>? DeleteCountry { get; set; }

    /// <summary>Delegate to delete a region record.</summary>
    public Func<Region, Task>? DeleteRegion { get; set; }

    /// <summary>Delegate to refresh data from the database.</summary>
    public Func<Task>? RefreshRequested { get; set; }

    public LocationsPanel()
    {
        InitializeComponent();
        CountriesGrid.ItemsSource = Countries;
        RegionsGrid.ItemsSource = _filteredRegions;
    }

    /// <summary>Expose grids for layout save/restore and external access.</summary>
    public GridControl CountriesGridControl => CountriesGrid;
    public TableView CountriesTableView => CountriesView;
    public GridControl RegionsGridControl => RegionsGrid;
    public TableView RegionsTableView => RegionsView;

    /// <summary>Expose status label for host to update.</summary>
    public System.Windows.Controls.TextBlock Status => StatusLabel;

    /// <summary>The currently selected country.</summary>
    public Country? SelectedCountry => _selectedCountry;

    /// <summary>Load countries and regions from data.</summary>
    public void Load(IEnumerable<Country> countries, IEnumerable<Region> regions)
    {
        Countries.Clear();
        Regions.Clear();
        foreach (var c in countries) Countries.Add(c);
        foreach (var r in regions) Regions.Add(r);
        StatusLabel.Text = $"{Countries.Count} countries, {Regions.Count} regions";
        FilterRegions();
    }

    private void FilterRegions()
    {
        _filteredRegions.Clear();
        if (_selectedCountry != null)
        {
            foreach (var r in Regions.Where(r => r.CountryId == _selectedCountry.Id))
                _filteredRegions.Add(r);
            RegionsLabel.Text = $"Regions — {_selectedCountry.Name}";
        }
        else
        {
            foreach (var r in Regions)
                _filteredRegions.Add(r);
            RegionsLabel.Text = "Regions";
        }
    }

    private void CountriesGrid_CurrentItemChanged(object sender, CurrentItemChangedEventArgs e)
    {
        _selectedCountry = CountriesGrid.CurrentItem as Country;
        FilterRegions();
    }

    private void AddCountry_Click(object sender, RoutedEventArgs e)
    {
        var next = Countries.Count > 0 ? Countries.Max(c => c.SortOrder) + 1 : 1;
        Countries.Add(new Country { Code = "XX", Name = "New Country", SortOrder = next });
    }

    private void AddRegion_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedCountry == null)
        {
            System.Windows.MessageBox.Show(
                "Select a country first.", "Add Region",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        var next = _filteredRegions.Count > 0 ? _filteredRegions.Max(r => r.SortOrder) + 1 : 1;
        var region = new Region
        {
            CountryId = _selectedCountry.Id,
            CountryName = _selectedCountry.Name,
            Code = "XX",
            Name = "New Region",
            SortOrder = next
        };
        Regions.Add(region);
        _filteredRegions.Add(region);
    }

    private async void Delete_Click(object sender, RoutedEventArgs e)
    {
        // Try deleting from whichever grid is focused
        if (RegionsGrid.IsKeyboardFocusWithin && RegionsGrid.CurrentItem is Region region && DeleteRegion != null)
        {
            var result = System.Windows.MessageBox.Show(
                $"Delete region '{region.Name}'?", "Confirm Delete",
                MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
            {
                await DeleteRegion.Invoke(region);
                Regions.Remove(region);
                _filteredRegions.Remove(region);
            }
        }
        else if (CountriesGrid.CurrentItem is Country country && DeleteCountry != null)
        {
            var result = System.Windows.MessageBox.Show(
                $"Delete country '{country.Name}' and all its regions?", "Confirm Delete",
                MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
            {
                await DeleteCountry.Invoke(country);
                Countries.Remove(country);
            }
        }
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        // Save all modified countries and regions
        if (SaveCountry != null)
            foreach (var c in Countries)
                await SaveCountry.Invoke(c);

        if (SaveRegion != null)
            foreach (var r in Regions)
                await SaveRegion.Invoke(r);

        StatusLabel.Text = "Saved.";
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        if (RefreshRequested != null)
            await RefreshRequested.Invoke();
    }

    private async void CountriesView_ValidateRow(object sender, GridRowValidationEventArgs e)
    {
        if (e.Row is Country country && SaveCountry != null)
            await SaveCountry.Invoke(country);
    }

    private async void RegionsView_ValidateRow(object sender, GridRowValidationEventArgs e)
    {
        if (e.Row is Region region && SaveRegion != null)
            await SaveRegion.Invoke(region);
    }

    private void View_InvalidRowException(object sender, InvalidRowExceptionEventArgs e)
    {
        e.ExceptionMode = ExceptionMode.NoAction;
    }
}
