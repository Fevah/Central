using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Controls;
using DevExpress.Xpf.Grid;
using Central.Engine.Models;

namespace Central.Module.Admin.Views;

public partial class RibbonConfigPanel : System.Windows.Controls.UserControl
{
    public static List<string> ItemTypes { get; } = new() { "button", "split", "check", "toggle", "separator" };
    public static List<string> CommandTypes { get; } = new() { "navigate_panel", "open_url", "execute_action" };

    public ObservableCollection<RibbonPageConfig> Pages { get; } = new();
    public ObservableCollection<RibbonGroupConfig> Groups { get; } = new();
    public ObservableCollection<RibbonItemConfig> Items { get; } = new();

    /// <summary>Delegate wired by shell to save a page.</summary>
    public Func<RibbonPageConfig, System.Threading.Tasks.Task>? SavePage { get; set; }
    /// <summary>Delegate wired by shell to save a group.</summary>
    public Func<RibbonGroupConfig, System.Threading.Tasks.Task>? SaveGroup { get; set; }
    /// <summary>Delegate wired by shell to save an item.</summary>
    public Func<RibbonItemConfig, System.Threading.Tasks.Task>? SaveItem { get; set; }
    /// <summary>Delegate wired by shell to open the icon picker. Returns selected icon name or null.</summary>
    public Func<string?>? OpenIconPicker { get; set; }

    public RibbonConfigPanel()
    {
        InitializeComponent();
        PagesGrid.ItemsSource = Pages;
        GroupsGrid.ItemsSource = Groups;
        ItemsGrid.ItemsSource = Items;
    }

    public GridControl Grid => PagesGrid;
    public TableView View => PagesView;

    private int _selectedPageId;
    private int _selectedGroupId;

    private void PagesGrid_FocusedRowChanged(object sender, FocusedRowChangedEventArgs e)
    {
        if (PagesGrid.CurrentItem is RibbonPageConfig page)
        {
            _selectedPageId = page.Id;
            GroupsLabel.Text = $"Groups — {page.Header}";
            // Filter groups to selected page
            var filtered = new ObservableCollection<RibbonGroupConfig>();
            foreach (var g in _allGroups)
                if (g.PageId == page.Id) filtered.Add(g);
            Groups.Clear();
            foreach (var g in filtered) Groups.Add(g);
            Items.Clear();
            ItemsLabel.Text = "Items";
        }
    }

    private void GroupsGrid_FocusedRowChanged(object sender, FocusedRowChangedEventArgs e)
    {
        if (GroupsGrid.CurrentItem is RibbonGroupConfig group)
        {
            _selectedGroupId = group.Id;
            ItemsLabel.Text = $"Items — {group.Header}";
            var filtered = new ObservableCollection<RibbonItemConfig>();
            foreach (var i in _allItems)
                if (i.GroupId == group.Id) filtered.Add(i);
            Items.Clear();
            foreach (var i in filtered) Items.Add(i);
        }
    }

    private List<RibbonGroupConfig> _allGroups = new();
    private List<RibbonItemConfig> _allItems = new();

    /// <summary>Load all data. Called by shell after panel opens.</summary>
    public void LoadData(List<RibbonPageConfig> pages, List<RibbonGroupConfig> groups, List<RibbonItemConfig> items)
    {
        Pages.Clear();
        foreach (var p in pages) Pages.Add(p);

        _allGroups = groups;
        _allItems = items;

        Groups.Clear();
        Items.Clear();

        // Auto-select first page
        if (Pages.Count > 0)
            PagesGrid.CurrentItem = Pages[0];
    }

    private async void PagesView_ValidateRow(object sender, GridRowValidationEventArgs e)
    {
        if (e.Row is RibbonPageConfig page && SavePage != null)
        {
            page.PageId_ForNewGroups(page.Id);
            await SavePage(page);
        }
    }

    private async void GroupsView_ValidateRow(object sender, GridRowValidationEventArgs e)
    {
        if (e.Row is RibbonGroupConfig group && SaveGroup != null)
        {
            if (group.PageId == 0) group.PageId = _selectedPageId;
            await SaveGroup(group);
            if (!_allGroups.Contains(group)) _allGroups.Add(group);
        }
    }

    private async void ItemsView_ValidateRow(object sender, GridRowValidationEventArgs e)
    {
        if (e.Row is RibbonItemConfig item && SaveItem != null)
        {
            if (item.GroupId == 0) item.GroupId = _selectedGroupId;
            await SaveItem(item);
            if (!_allItems.Contains(item)) _allItems.Add(item);
        }
    }

    private void IconPicker_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (OpenIconPicker == null) return;

        // Capture which grid/row BEFORE opening the picker dialog (focus will shift)
        var targetItem = ItemsGrid.CurrentItem as Central.Engine.Models.RibbonItemConfig;
        var targetPage = PagesGrid.CurrentItem as Central.Engine.Models.RibbonPageConfig;
        var targetColumn = ItemsGrid.CurrentColumn?.FieldName;

        var selected = OpenIconPicker();
        if (string.IsNullOrEmpty(selected)) return;

        // Set directly on model + auto-save
        if (targetItem != null)
        {
            if (targetColumn == "LargeGlyph")
                targetItem.LargeGlyph = selected;
            else
                targetItem.Glyph = selected;
            try { ItemsGrid.RefreshRow(ItemsView.FocusedRowHandle); } catch { }
            if (SaveItem != null) _ = SaveItem(targetItem);
        }
        else if (targetPage != null)
        {
            targetPage.IconName = selected;
            try { PagesGrid.RefreshRow(PagesView.FocusedRowHandle); } catch { }
            if (SavePage != null) _ = SavePage(targetPage);
        }
    }

    private void View_InvalidRowException(object sender, InvalidRowExceptionEventArgs e)
    {
        e.ExceptionMode = DevExpress.Xpf.Grid.ExceptionMode.NoAction;
    }
}

// Extension to avoid ambiguity
static file class RibbonPageConfigExtensions
{
    public static void PageId_ForNewGroups(this RibbonPageConfig _, int __) { }
}
