using DevExpress.Xpf.Bars;
using DevExpress.Xpf.Grid;
using Central.Engine.Widgets;

namespace Central.Desktop.Services;

/// <summary>
/// Engine service that builds DX context menus for grids and tree lists.
/// Uses ShowGridMenu event which works in DX 25.2.
/// </summary>
public static class GridContextMenuBuilder
{
    /// <summary>
    /// Attach a context menu with named actions to a GridControl (TableView).
    /// </summary>
    public static void AttachSimple(GridControl grid, params (string Text, Action? Action)[] items)
    {
        var tableView = grid.View as TableView;
        if (tableView == null) return;

        tableView.ShowGridMenu += (sender, e) =>
        {
            if (e.MenuType != GridMenuType.RowCell) return;
            AddMenuItems(e, items);
        };
    }

    /// <summary>
    /// Attach a context menu with named actions to a TreeListControl.
    /// </summary>
    public static void AttachTree(TreeListControl tree, params (string Text, Action? Action)[] items)
    {
        var treeView = tree.View as DevExpress.Xpf.Grid.TreeListView;
        if (treeView == null) return;

        treeView.ShowGridMenu += (sender, e) =>
        {
            if (e.MenuType != GridMenuType.RowCell) return;
            AddMenuItems(e, items);
        };
    }

    private static void AddMenuItems(GridMenuEventArgs e, (string Text, Action? Action)[] items)
    {
        foreach (var (text, action) in items)
        {
            if (text == "-")
            {
                e.Customizations.Add(new BarItemLinkSeparator());
                continue;
            }

            var barItem = new BarButtonItem { Content = text };
            barItem.ItemClick += (_, _) => action?.Invoke();
            e.Customizations.Add(barItem);
        }
    }
}
