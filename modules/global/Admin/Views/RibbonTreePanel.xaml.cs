using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using DevExpress.Xpf.Grid;
using DevExpress.Xpf.Grid.TreeList;
using Central.Engine.Models;

namespace Central.Module.Global.Admin;

public partial class RibbonTreePanel : System.Windows.Controls.UserControl
{
    public ObservableCollection<RibbonTreeItem> Items { get; } = new();
    private int _nextId = 1000;

    /// <summary>Delegate to open icon picker. Returns icon name or null.</summary>
    public Func<string?>? OpenIconPicker { get; set; }

    /// <summary>Delegate to prompt for text input. Args: title, prompt, defaultValue. Returns text or null.</summary>
    public Func<string, string, string, string?>? PromptForText { get; set; }

    /// <summary>Delegate to render an icon name to an ImageSource preview. Set by shell.</summary>
    public Func<string, object?>? RenderIconPreview { get; set; }

    /// <summary>Delegate to save user overrides. Called when Apply is clicked.</summary>
    public Func<ObservableCollection<RibbonTreeItem>, Task>? SaveOverrides { get; set; }

    /// <summary>Delegate to save a SINGLE override immediately (auto-save on icon pick/hide/rename).</summary>
    public Func<RibbonTreeItem, Task>? SaveSingleOverride { get; set; }

    /// <summary>Delegate to reset all user overrides.</summary>
    public Func<Task>? ResetOverrides { get; set; }

    public RibbonTreePanel()
    {
        InitializeComponent();
        RibbonTree.ItemsSource = Items;
    }

    public TreeListControl Tree => RibbonTree;
    public TreeListView View => RibbonTreeView;

    /// <summary>Load the tree from ribbon pages/groups/items.</summary>
    public void LoadFromRibbon(
        System.Collections.Generic.List<RibbonPageConfig> pages,
        System.Collections.Generic.List<RibbonGroupConfig> groups,
        System.Collections.Generic.List<RibbonItemConfig> items,
        System.Collections.Generic.List<UserRibbonOverride>? userOverrides = null)
    {
        Items.Clear();
        _nextId = 1;

        var overrideMap = userOverrides?.ToDictionary(o => o.ItemKey, o => o) ?? new();

        foreach (var page in pages.OrderBy(p => p.SortOrder))
        {
            var pageId = _nextId++;
            Items.Add(new RibbonTreeItem
            {
                Id = pageId, ParentId = 0, NodeType = "page",
                Text = page.Header, SortOrder = page.SortOrder,
                Permission = page.RequiredPermission, ItemKey = page.Header,
                IconName = page.IconName
            });

            foreach (var group in groups.Where(g => g.PageId == page.Id).OrderBy(g => g.SortOrder))
            {
                var groupId = _nextId++;
                Items.Add(new RibbonTreeItem
                {
                    Id = groupId, ParentId = pageId, NodeType = "group",
                    Text = group.Header, SortOrder = group.SortOrder,
                    ItemKey = $"{page.Header}/{group.Header}"
                });

                foreach (var item in items.Where(i => i.GroupId == group.Id).OrderBy(i => i.SortOrder))
                {
                    var itemId = _nextId++;
                    var key = $"{page.Header}/{group.Header}/{item.Content}";
                    var ov = overrideMap.GetValueOrDefault(key);

                    Items.Add(new RibbonTreeItem
                    {
                        Id = itemId, ParentId = groupId,
                        NodeType = item.ItemType == "separator" ? "separator" : "item",
                        Text = item.Content, SortOrder = item.SortOrder,
                        Permission = item.Permission, ItemKey = key,
                        IconName = ov?.CustomIcon ?? item.Glyph,
                        CustomText = ov?.CustomText,
                        IsHidden = ov?.IsHidden ?? !item.IsVisible
                    });
                }
            }
        }
    }

    /// <summary>Refresh all icon previews. Call after setting RenderIconPreview delegate.</summary>
    public void RefreshIconPreviews()
    {
        if (RenderIconPreview == null) return;
        foreach (var item in Items)
        {
            if (!string.IsNullOrEmpty(item.IconName))
                item.IconPreview = RenderIconPreview(item.IconName);
        }
    }

    private void AddPage_Click(object sender, RoutedEventArgs e)
    {
        var name = PromptForText?.Invoke("New Ribbon Page", "Page name:", "New Page");
        if (string.IsNullOrWhiteSpace(name)) return;

        Items.Add(new RibbonTreeItem
        {
            Id = _nextId++, ParentId = 0, NodeType = "page",
            Text = name, SortOrder = (Items.Where(i => i.ParentId == 0).Max(i => (int?)i.SortOrder) ?? 0) + 10,
            ItemKey = name
        });
    }

    private void AddGroup_Click(object sender, RoutedEventArgs e)
    {
        var row = RibbonTree.GetRow(RibbonTreeView.FocusedRowHandle) as RibbonTreeItem;
        // Find parent page
        var pageItem = row?.NodeType == "page" ? row
            : row?.NodeType == "group" ? Items.FirstOrDefault(i => i.Id == row.ParentId)
            : row != null ? Items.FirstOrDefault(i => i.Id == Items.FirstOrDefault(g => g.Id == row.ParentId)?.ParentId)
            : null;
        if (pageItem == null) { return; }

        var name = PromptForText?.Invoke("New Ribbon Group", "Group name:", "New Group");
        if (string.IsNullOrWhiteSpace(name)) return;

        Items.Add(new RibbonTreeItem
        {
            Id = _nextId++, ParentId = pageItem.Id, NodeType = "group",
            Text = name, SortOrder = (Items.Where(i => i.ParentId == pageItem.Id).Max(i => (int?)i.SortOrder) ?? 0) + 10,
            ItemKey = $"{pageItem.Text}/{name}"
        });
    }

    private void AddSeparator_Click(object sender, RoutedEventArgs e)
    {
        var current = RibbonTree.View is TreeListView tv ? tv.FocusedNode : null;
        if (current == null) return;
        var row = RibbonTree.GetRow(current.RowHandle) as RibbonTreeItem;
        if (row == null) return;

        // Add separator as sibling
        var sep = new RibbonTreeItem
        {
            Id = _nextId++, ParentId = row.ParentId, NodeType = "separator",
            Text = "───", SortOrder = row.SortOrder + 1, ItemKey = $"sep_{_nextId}"
        };
        Items.Add(sep);
    }

    private void MoveUp_Click(object sender, RoutedEventArgs e) => MoveSelected(-1);
    private void MoveDown_Click(object sender, RoutedEventArgs e) => MoveSelected(1);

    private void MoveSelected(int direction)
    {
        var row = RibbonTree.GetRow(RibbonTreeView.FocusedRowHandle) as RibbonTreeItem;
        if (row == null) return;

        var siblings = Items.Where(i => i.ParentId == row.ParentId).OrderBy(i => i.SortOrder).ToList();
        var idx = siblings.IndexOf(row);
        var newIdx = idx + direction;
        if (newIdx < 0 || newIdx >= siblings.Count) return;

        // Swap sort orders
        var other = siblings[newIdx];
        (row.SortOrder, other.SortOrder) = (other.SortOrder, row.SortOrder);

        // Refresh tree
        var items = Items.ToList();
        Items.Clear();
        foreach (var item in items.OrderBy(i => i.SortOrder)) Items.Add(item);
    }

    private void ToggleHidden_Click(object sender, RoutedEventArgs e)
    {
        var row = RibbonTree.GetRow(RibbonTreeView.FocusedRowHandle) as RibbonTreeItem;
        if (row != null)
        {
            row.IsHidden = !row.IsHidden;
            if (SaveSingleOverride != null) _ = SaveSingleOverride(row);
        }
    }

    private void SetIcon_Click(object sender, RoutedEventArgs e)
    {
        if (OpenIconPicker == null) return;
        // Capture row BEFORE dialog opens (focus shifts to dialog)
        var row = RibbonTree.GetRow(RibbonTreeView.FocusedRowHandle) as RibbonTreeItem;
        if (row == null) return;
        var icon = OpenIconPicker();
        if (!string.IsNullOrEmpty(icon))
        {
            row.IconName = icon;
            row.IconPreview = RenderIconPreview?.Invoke(icon);
            // Auto-save immediately to DB
            if (SaveSingleOverride != null) _ = SaveSingleOverride(row);
        }
    }

    private void TreeIconPicker_Click(object sender, RoutedEventArgs e)
    {
        SetIcon_Click(sender, e);
    }

    private void RibbonTreeView_RowDoubleClick(object sender, DevExpress.Xpf.Grid.RowDoubleClickEventArgs e)
    {
        // Only open picker when double-clicking the IconName or NodeIcon column
        var col = RibbonTreeView.FocusedColumn?.FieldName;
        if (col == "IconName" || col == "NodeIcon")
        {
            e.Handled = true;
            SetIcon_Click(sender, e);
        }
    }

    private async void Apply_Click(object sender, RoutedEventArgs e)
    {
        if (SaveOverrides != null)
            await SaveOverrides(Items);
    }

    private async void ResetAll_Click(object sender, RoutedEventArgs e)
    {
        if (ResetOverrides != null)
        {
            await ResetOverrides();
            // Clear all custom overrides from tree
            foreach (var item in Items)
            {
                item.CustomText = null;
                item.IsHidden = false;
                item.IconName = null;
            }
        }
    }

    // Drag-drop reordering handled by DX TreeListView.AllowDragDrop + Move Up/Down buttons
}
