using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using DevExpress.Xpf.Grid;
using DevExpress.Xpf.Grid.TreeList;
using Central.Core.Models;

namespace Central.Module.Admin.Views;

public partial class RibbonAdminTreePanel : System.Windows.Controls.UserControl
{
    public static List<string> DisplayStyles { get; } = new() { "large", "small", "smallNoText" };
    public static List<string> ItemTypes { get; } = new() { "button", "split", "check", "toggle", "separator" };

    public ObservableCollection<RibbonTreeItem> Items { get; } = new();
    private int _nextId = 1000;

    public Func<string?>? OpenIconPicker { get; set; }
    public Func<string, string, string, string?>? PromptForText { get; set; }
    /// <summary>Get available link targets for the link picker. Returns list of "type:target" strings.</summary>
    public Func<List<string>>? GetLinkTargets { get; set; }
    /// <summary>Render icon name to ImageSource preview.</summary>
    public Func<string, object?>? RenderIconPreview { get; set; }
    /// <summary>Save a single admin default immediately.</summary>
    public Func<RibbonTreeItem, Task>? SaveAdminDefault { get; set; }
    /// <summary>Push ALL current items as admin defaults.</summary>
    public Func<ObservableCollection<RibbonTreeItem>, Task>? PushAllDefaults { get; set; }

    public RibbonAdminTreePanel()
    {
        InitializeComponent();
        AdminTree.ItemsSource = Items;
    }

    public TreeListControl Tree => AdminTree;
    public TreeListView View => AdminTreeView;

    public void LoadFromRibbon(
        List<RibbonPageConfig> pages, List<RibbonGroupConfig> groups,
        List<RibbonItemConfig> items,
        List<(string ItemKey, string? Icon, string? Text, bool IsHidden)>? adminDefaults = null)
    {
        Items.Clear();
        _nextId = 1;
        var defaultMap = new Dictionary<string, (string ItemKey, string? Icon, string? Text, bool IsHidden)>(StringComparer.OrdinalIgnoreCase);
        if (adminDefaults != null)
            foreach (var d in adminDefaults)
                defaultMap[d.ItemKey] = d; // last-write-wins for case-insensitive dupes

        foreach (var page in pages.OrderBy(p => p.SortOrder))
        {
            var pageId = _nextId++;
            var pageDef = defaultMap.GetValueOrDefault(page.Header);
            Items.Add(new RibbonTreeItem
            {
                Id = pageId, ParentId = 0, NodeType = "page",
                Text = page.Header, SortOrder = page.SortOrder,
                Permission = page.RequiredPermission, ItemKey = page.Header,
                DefaultIcon = pageDef.Icon ?? page.IconName,
                DefaultLabel = pageDef.Text,
                IsHidden = pageDef.IsHidden
            });

            foreach (var group in groups.Where(g => g.PageId == page.Id).OrderBy(g => g.SortOrder))
            {
                var groupId = _nextId++;
                var groupKey = $"{page.Header}/{group.Header}";
                var groupDef = defaultMap.GetValueOrDefault(groupKey);
                Items.Add(new RibbonTreeItem
                {
                    Id = groupId, ParentId = pageId, NodeType = "group",
                    Text = group.Header, SortOrder = group.SortOrder,
                    ItemKey = groupKey,
                    DefaultLabel = groupDef.Text
                });

                foreach (var item in items.Where(i => i.GroupId == group.Id).OrderBy(i => i.SortOrder))
                {
                    var itemId = _nextId++;
                    var key = $"{page.Header}/{group.Header}/{item.Content}";
                    var itemDef = defaultMap.GetValueOrDefault(key) != default
                        ? defaultMap.GetValueOrDefault(key)
                        : defaultMap.GetValueOrDefault(item.Content);

                    Items.Add(new RibbonTreeItem
                    {
                        Id = itemId, ParentId = groupId,
                        NodeType = item.ItemType == "separator" ? "separator" : "item",
                        Text = item.Content, SortOrder = item.SortOrder,
                        Permission = item.Permission, ItemKey = key,
                        ItemType = item.ItemType,
                        DefaultIcon = itemDef.Icon ?? item.Glyph,
                        DefaultLabel = itemDef.Text,
                        IsHidden = itemDef.IsHidden || !item.IsVisible
                    });
                }
            }
        }
    }

    private void AddPage_Click(object sender, RoutedEventArgs e)
    {
        var name = PromptForText?.Invoke("New Page", "Page name:", "New Page");
        if (string.IsNullOrWhiteSpace(name)) return;
        Items.Add(new RibbonTreeItem
        {
            Id = _nextId++, ParentId = 0, NodeType = "page", Text = name,
            SortOrder = (Items.Where(i => i.ParentId == 0).Max(i => (int?)i.SortOrder) ?? 0) + 10,
            ItemKey = name
        });
    }

    private void AddGroup_Click(object sender, RoutedEventArgs e)
    {
        var row = AdminTree.GetRow(AdminTreeView.FocusedRowHandle) as RibbonTreeItem;
        var pageItem = row?.NodeType == "page" ? row
            : row?.NodeType == "group" ? Items.FirstOrDefault(i => i.Id == row.ParentId)
            : row != null ? Items.FirstOrDefault(i => i.Id == Items.FirstOrDefault(g => g.Id == row.ParentId)?.ParentId)
            : null;
        if (pageItem == null) return;
        var name = PromptForText?.Invoke("New Group", "Group name:", "New Group");
        if (string.IsNullOrWhiteSpace(name)) return;
        Items.Add(new RibbonTreeItem
        {
            Id = _nextId++, ParentId = pageItem.Id, NodeType = "group", Text = name,
            SortOrder = (Items.Where(i => i.ParentId == pageItem.Id).Max(i => (int?)i.SortOrder) ?? 0) + 10,
            ItemKey = $"{pageItem.Text}/{name}"
        });
    }

    private void AddItem_Click(object sender, RoutedEventArgs e)
    {
        var row = AdminTree.GetRow(AdminTreeView.FocusedRowHandle) as RibbonTreeItem;
        var groupItem = row?.NodeType == "group" ? row
            : row?.NodeType == "item" ? Items.FirstOrDefault(i => i.Id == row.ParentId)
            : null;
        if (groupItem == null) return;
        var name = PromptForText?.Invoke("New Item", "Button label:", "New Button");
        if (string.IsNullOrWhiteSpace(name)) return;
        var pageItem = Items.FirstOrDefault(i => i.Id == groupItem.ParentId);
        Items.Add(new RibbonTreeItem
        {
            Id = _nextId++, ParentId = groupItem.Id, NodeType = "item", Text = name,
            ItemType = "button",
            SortOrder = (Items.Where(i => i.ParentId == groupItem.Id).Max(i => (int?)i.SortOrder) ?? 0) + 10,
            ItemKey = $"{pageItem?.Text}/{groupItem.Text}/{name}"
        });
    }

    private void AddSeparator_Click(object sender, RoutedEventArgs e)
    {
        var row = AdminTree.GetRow(AdminTreeView.FocusedRowHandle) as RibbonTreeItem;
        if (row == null) return;
        Items.Add(new RibbonTreeItem
        {
            Id = _nextId++, ParentId = row.ParentId, NodeType = "separator",
            Text = "───", SortOrder = row.SortOrder + 1, ItemKey = $"sep_{_nextId}"
        });
    }

    private void MoveUp_Click(object sender, RoutedEventArgs e) => MoveSelected(-1);
    private void MoveDown_Click(object sender, RoutedEventArgs e) => MoveSelected(1);

    private void MoveSelected(int direction)
    {
        var row = AdminTree.GetRow(AdminTreeView.FocusedRowHandle) as RibbonTreeItem;
        if (row == null) return;
        var siblings = Items.Where(i => i.ParentId == row.ParentId).OrderBy(i => i.SortOrder).ToList();
        var idx = siblings.IndexOf(row);
        var newIdx = idx + direction;
        if (newIdx < 0 || newIdx >= siblings.Count) return;
        var other = siblings[newIdx];
        (row.SortOrder, other.SortOrder) = (other.SortOrder, row.SortOrder);
    }

    /// <summary>Refresh all icon previews from DefaultIcon. Call after setting RenderIconPreview.</summary>
    public void RefreshIconPreviews()
    {
        if (RenderIconPreview == null) return;
        foreach (var item in Items)
            if (!string.IsNullOrEmpty(item.DefaultIcon))
                item.IconPreview = RenderIconPreview(item.DefaultIcon);
    }

    private void SetDefaultIcon_Click(object sender, RoutedEventArgs e)
    {
        if (OpenIconPicker == null) return;
        var row = AdminTree.GetRow(AdminTreeView.FocusedRowHandle) as RibbonTreeItem;
        if (row == null) return;
        var icon = OpenIconPicker();
        if (!string.IsNullOrEmpty(icon))
        {
            row.DefaultIcon = icon;
            row.IconPreview = RenderIconPreview?.Invoke(icon);
            // Auto-save immediately
            if (SaveAdminDefault != null) _ = SaveAdminDefault(row);
        }
    }

    private void AdminIconPicker_Click(object sender, RoutedEventArgs e)
        => SetDefaultIcon_Click(sender, e);

    private void AdminTreeView_RowDoubleClick(object sender, DevExpress.Xpf.Grid.RowDoubleClickEventArgs e)
    {
        var col = AdminTreeView.FocusedColumn?.FieldName;
        if (col == "DefaultIcon" || col == "IconName" || col == "NodeIcon")
        {
            e.Handled = true;
            SetDefaultIcon_Click(sender, e);
        }
    }

    private void LinkPicker_Click(object sender, RoutedEventArgs e)
    {
        var row = AdminTree.GetRow(AdminTreeView.FocusedRowHandle) as RibbonTreeItem;
        if (row == null) return;

        var targets = GetLinkTargets?.Invoke() ?? new List<string>();
        if (targets.Count == 0) return;

        // Show link target selection
        var selected = PromptForText?.Invoke("Link Target",
            "Enter link target:\n\npanel:PanelName  — open/toggle a panel\nurl:https://...  — open URL\naction:ActionKey  — run an action\npage:PageName  — switch to ribbon page\n\nAvailable panels: " +
            string.Join(", ", targets.Where(t => t.StartsWith("panel:")).Select(t => t[6..])),
            row.LinkTarget ?? "panel:");
        if (!string.IsNullOrEmpty(selected))
        {
            row.LinkTarget = selected;
            if (SaveAdminDefault != null) _ = SaveAdminDefault(row);
        }
    }

    private async void PushAllDefaults_Click(object sender, RoutedEventArgs e)
    {
        if (PushAllDefaults != null)
            await PushAllDefaults(Items);
    }
}
