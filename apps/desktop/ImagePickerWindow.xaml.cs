using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media.Imaging;
using Npgsql;
using Central.Persistence;

namespace Central.Desktop;

public partial class ImagePickerWindow : DevExpress.Xpf.Core.DXDialogWindow
{
    private readonly string _dsn;
    private List<IconDisplayItem> _allItems = new();

    /// <summary>Selected icon ID. 0 = no selection, -1 = cleared.</summary>
    public int SelectedIconId { get; private set; }

    /// <summary>Selected icon name.</summary>
    public string SelectedIconName { get; private set; } = "";

    public ImagePickerWindow(string dsn, string? preselectedSize = null)
    {
        _dsn = dsn;
        InitializeComponent();

        // Populate pack checkboxes + category checkboxes — all selected by default
        var svc = IconService.Instance;
        var packs = svc.GetIconSets();
        PackList.ItemsSource = packs;
        PackList.EditValue = packs; // all packs selected

        RefreshCategories();
        // Don't load icons on open — user selects categories first
    }

    private int _loadGeneration;
    private bool _suppressReload;

    private void LoadIcons()
    {
        if (_suppressReload) return;
        _loadGeneration++;
        var gen = _loadGeneration;

        var search = SearchBox.Text?.Trim() ?? "";
        var selectedPacks = GetSelectedPacks();
        var selectedCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (CategoryList.EditValue is System.Collections.IList editList)
            foreach (var item in editList)
                if (item is string s) selectedCategories.Add(s);
        if (selectedCategories.Count == 0 && CategoryList.SelectedItems?.Count > 0)
            foreach (var item in CategoryList.SelectedItems)
                if (item is string s) selectedCategories.Add(s);

        // Show loading indicator
        IconList.ItemsSource = null;
        CountLabel.Text = "Loading...";

        // Run filtering + DB load + rendering on background thread
        _ = Task.Run(() =>
        {
            var svc = IconService.Instance;
            var allIcons = svc.AllIcons.AsEnumerable();

            // Filter by selected packs
            if (selectedPacks.Count > 0)
                allIcons = allIcons.Where(i => selectedPacks.Contains(i.IconSet));
            // Filter by selected categories — no categories = no icons
            if (selectedCategories.Count == 0)
            {
                Dispatcher.Invoke(() =>
                {
                    _allItems = new List<IconDisplayItem>();
                    IconList.ItemsSource = _allItems;
                    CountLabel.Text = "Select categories to browse icons";
                });
                return;
            }
            allIcons = allIcons.Where(i => selectedCategories.Contains(i.Category));
            // Filter by search
            if (!string.IsNullOrEmpty(search))
                allIcons = allIcons.Where(i => i.Name.Contains(search, StringComparison.OrdinalIgnoreCase));

            // Sort by name then pack so same icons from different packs appear side by side
            var filtered = allIcons.OrderBy(i => i.Name).ThenBy(i => i.IconSet).ToList();
            // Limit display — use search/category to narrow down for large sets
            var maxDisplay = string.IsNullOrEmpty(search) && selectedCategories.Count > 5 ? 2000 : 5000;
            var display = filtered.Take(maxDisplay).ToList();
            var ids = display.Select(i => i.Id).ToList();

            // Batch load PNG 32px (pre-rendered — instant, no SVG rendering)
            var pngMap = new Dictionary<int, byte[]>();
            if (ids.Count > 0)
            {
                try
                {
                    using var conn = new NpgsqlConnection(_dsn);
                    conn.Open();
                    for (int b = 0; b < ids.Count; b += 1000)
                    {
                        if (gen != _loadGeneration) return;
                        var batchIds = ids.Skip(b).Take(1000).ToArray();
                        using var cmd = new NpgsqlCommand(
                            "SELECT id, COALESCE(png_32, icon_data) FROM icon_library WHERE id = ANY(@ids) AND (png_32 IS NOT NULL OR icon_data IS NOT NULL)", conn);
                        cmd.Parameters.AddWithValue("ids", batchIds);
                        using var rdr = cmd.ExecuteReader();
                        while (rdr.Read())
                            if (!rdr.IsDBNull(1)) pngMap[rdr.GetInt32(0)] = (byte[])rdr[1];
                    }
                }
                catch { }
            }

            if (gen != _loadGeneration) return;

            // Create items with pre-rendered PNG — instant BitmapImage
            var items = new List<IconDisplayItem>(display.Count);
            foreach (var info in display)
            {
                if (gen != _loadGeneration) return;
                var item = new IconDisplayItem { Id = info.Id, Name = info.Name, Category = info.Category, Size = info.Size, IconSet = info.IconSet };
                if (pngMap.TryGetValue(info.Id, out var png))
                {
                    try
                    {
                        var bmp = new System.Windows.Media.Imaging.BitmapImage();
                        bmp.BeginInit();
                        bmp.StreamSource = new MemoryStream(png);
                        bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                        bmp.DecodePixelWidth = 32;
                        bmp.EndInit();
                        bmp.Freeze();
                        item.ImageSource = bmp;
                    }
                    catch { }
                }
                items.Add(item);
            }

            if (gen != _loadGeneration) return;

            Dispatcher.Invoke(() =>
            {
                if (gen != _loadGeneration) return;
                _allItems = items;
                IconList.ItemsSource = _allItems;
                CountLabel.Text = filtered.Count > display.Count
                    ? $"{display.Count} of {filtered.Count} icons (narrow with search)"
                    : $"{display.Count} icons";
            });
        });
    }

    private HashSet<string> GetSelectedPacks()
    {
        var packs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (PackList.EditValue is System.Collections.IList list)
            foreach (var item in list)
                if (item is string s) packs.Add(s);
        return packs;
    }

    private void RefreshCategories()
    {
        _suppressReload = true;
        try
        {
            var selectedPacks = GetSelectedPacks();
            var svc = IconService.Instance;
            // Show categories from selected packs only
            var categories = svc.AllIcons
                .Where(i => selectedPacks.Count == 0 || selectedPacks.Contains(i.IconSet))
                .Select(i => i.Category).Distinct().OrderBy(c => c).ToList();

            // Preserve current category selections where possible
            var currentCats = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (CategoryList.EditValue is System.Collections.IList editList)
                foreach (var item in editList)
                    if (item is string s) currentCats.Add(s);

            CategoryList.ItemsSource = categories;

            // Keep previously selected categories that still exist
            var toSelect = currentCats.Count > 0
                ? categories.Where(c => currentCats.Contains(c)).ToList()
                : new List<string>(); // start with none selected
            CategoryList.EditValue = toSelect.Count > 0 ? toSelect : null;
        }
        finally { _suppressReload = false; }
    }

    private void PackList_Changed(object sender, DevExpress.Xpf.Editors.EditValueChangedEventArgs e)
    {
        RefreshCategories();
        LoadIcons();
    }

    private void Filter_Changed(object sender, DevExpress.Xpf.Editors.EditValueChangedEventArgs e) => LoadIcons();
    private void CategoryList_EditValueChanged(object sender, DevExpress.Xpf.Editors.EditValueChangedEventArgs e) => LoadIcons();

    private void SelectAllCategories_Click(object sender, RoutedEventArgs e)
    {
        var cats = IconService.Instance.GetCategories();
        CategoryList.EditValue = cats;
        LoadIcons();
    }

    private void ClearAllCategories_Click(object sender, RoutedEventArgs e)
    {
        CategoryList.EditValue = null;
        LoadIcons();
    }

    private void SelectIcon_Click(object sender, RoutedEventArgs e)
    {
        if (IconList.SelectedItem is IconDisplayItem item)
        {
            SelectedIconId = item.Id;
            SelectedIconName = item.Name;
            DialogResult = true;
        }
    }

    private void ClearIcon_Click(object sender, RoutedEventArgs e)
    {
        SelectedIconId = -1; // -1 = explicitly cleared
        SelectedIconName = "";
        DialogResult = true;
    }

    private async void DeleteIcon_Click(object sender, RoutedEventArgs e)
    {
        if (IconList.SelectedItem is not IconDisplayItem item) return;
        var result = System.Windows.MessageBox.Show($"Delete icon '{item.Name}'?", "Confirm Delete",
            System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question);
        if (result != System.Windows.MessageBoxResult.Yes) return;

        await IconService.Instance.DeleteIconAsync(_dsn, item.Id);
        LoadIcons(); // refresh list
    }

    // Background rendering handles progressive icon display — no scroll handler needed

    private void IconList_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (IconList.SelectedItem is IconDisplayItem item)
        {
            SelectedIconId = item.Id;
            SelectedIconName = item.Name;
            DialogResult = true;
        }
    }
}

public class IconDisplayItem : System.ComponentModel.INotifyPropertyChanged
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public string Size { get; set; } = "";
    public string IconSet { get; set; } = "";
    public string TooltipText => $"{Name}\n{Category} ({IconSet})";
    public string? SvgText { get; set; }

    private System.Windows.Media.ImageSource? _imageSource;
    public System.Windows.Media.ImageSource? ImageSource
    {
        get => _imageSource;
        set { _imageSource = value; PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(ImageSource))); }
    }

    public string ShortName => Name.Length > 12 ? Name[..12] + "…" : Name;
    public string PackLabel => string.IsNullOrEmpty(IconSet) ? "" : IconSet[..1]; // "O" or "U"

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
}
