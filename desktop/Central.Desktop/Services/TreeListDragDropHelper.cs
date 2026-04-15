using System.Collections;
using DevExpress.Xpf.Grid;

namespace Central.Desktop.Services;

/// <summary>
/// Reusable drag-drop wiring for any <see cref="TreeListView"/>. Gives every
/// tree-based panel in the app the same behaviour — drop an item, all sibling
/// SortOrder values are recomputed, and a per-item save callback persists the
/// new layout. Designed to be a one-line attach so panels don't reinvent.
///
/// DevExpress notes:
///   * <c>CompleteRecordDragDrop</c> fires <i>before</i> the <c>ItemsSource</c>
///     reorders, so we wrap the recompute in <c>Dispatcher.BeginInvoke</c>.
///   * With <c>KeyFieldName="Id"</c> + <c>ParentFieldName="ParentId"</c>, DX
///     auto-updates the parent column when an item is dropped into a different
///     parent — we only have to fix up <c>SortOrder</c> ourselves.
///
/// Usage:
/// <code>
///   TreeListDragDropHelper.EnableReorder(
///       view: AdminTreeView,
///       items: Items,
///       parentIdSelector: r => r.ParentId,
///       sortOrderSetter:  (r, order) => r.SortOrder = order,
///       saveCallback:     async r => await SaveAdminDefault(r));
/// </code>
/// </summary>
public static class TreeListDragDropHelper
{
    /// <summary>
    /// Enables drag-drop reorder on <paramref name="view"/>. After every drop,
    /// each parent group's children are renumbered with sort orders 0, 10, 20…
    /// and (if supplied) <paramref name="saveCallback"/> runs for every item
    /// whose order changed.
    /// </summary>
    public static void EnableReorder<T>(
        TreeListView view,
        IList<T> items,
        Func<T, int> parentIdSelector,
        Action<T, int> sortOrderSetter,
        Func<T, Task>? saveCallback = null,
        Func<T, int>? sortOrderGetter = null,
        int sortStep = 10,
        Func<Task>? afterBatchSaved = null)
        where T : class
    {
        if (view == null) throw new ArgumentNullException(nameof(view));
        if (items == null) throw new ArgumentNullException(nameof(items));
        if (parentIdSelector == null) throw new ArgumentNullException(nameof(parentIdSelector));
        if (sortOrderSetter == null) throw new ArgumentNullException(nameof(sortOrderSetter));

        view.AllowDragDrop = true;

        view.CompleteRecordDragDrop += (s, e) =>
        {
            if (e.Canceled) return;

            // ItemsSource hasn't been mutated yet — defer to next dispatcher tick
            view.Dispatcher.BeginInvoke(new Action(async () =>
            {
                try
                {
                    // Read the *visible row order* from the TreeListView — that
                    // reflects the post-drop state, even when DX leaves the
                    // underlying ObservableCollection order untouched (which it
                    // does for sibling reorders within the same parent).
                    var changed = RecomputeSortOrdersFromView(view, items,
                        parentIdSelector, sortOrderSetter, sortOrderGetter, sortStep);

                    if (saveCallback != null)
                        foreach (var item in changed)
                            await saveCallback(item);

                    // Single post-batch hook so consumers can refresh the live UI
                    // exactly once per drag operation rather than once per row.
                    if (afterBatchSaved != null && changed.Count > 0)
                        await afterBatchSaved();
                }
                catch (Exception ex)
                {
                    // Log but don't surface — bad layout shouldn't crash the panel
                    System.Diagnostics.Debug.WriteLine($"TreeListDragDrop save failed: {ex.Message}");
                }
            }));
        };
    }

    /// <summary>
    /// Walk visible rows in the order DX TreeListView is currently displaying
    /// them — this reflects what the user just dragged into place. Group by
    /// parent and renumber. Falls back to <see cref="RecomputeSortOrders"/> if
    /// the view's grid isn't a <see cref="DevExpress.Xpf.Grid.TreeListControl"/>
    /// (shouldn't happen in practice).
    /// </summary>
    private static List<T> RecomputeSortOrdersFromView<T>(
        TreeListView view,
        IList<T> items,
        Func<T, int> parentIdSelector,
        Action<T, int> sortOrderSetter,
        Func<T, int>? sortOrderGetter,
        int sortStep) where T : class
    {
        var grid = view.DataControl as DevExpress.Xpf.Grid.TreeListControl;
        if (grid == null)
            return RecomputeSortOrders(items, parentIdSelector, sortOrderSetter, sortOrderGetter, sortStep);

        var byParent = new Dictionary<int, List<T>>();
        var rowCount = grid.VisibleRowCount;
        for (int handle = 0; handle < rowCount; handle++)
        {
            var row = grid.GetRow(handle) as T;
            if (row == null) continue;
            var pid = parentIdSelector(row);
            if (!byParent.TryGetValue(pid, out var list))
                byParent[pid] = list = new List<T>();
            list.Add(row);
        }

        var changed = new List<T>();
        foreach (var siblings in byParent.Values)
        {
            int order = 0;
            foreach (var item in siblings)
            {
                var newOrder = order;
                var current  = sortOrderGetter?.Invoke(item) ?? int.MinValue;
                if (current != newOrder)
                {
                    sortOrderSetter(item, newOrder);
                    changed.Add(item);
                }
                order += sortStep;
            }
        }
        return changed;
    }

    /// <summary>
    /// Group items by parent, sort by current ItemsSource order (which DX has
    /// just rearranged for the dropped record), and re-issue sort values
    /// 0, sortStep, 2*sortStep…  Returns only the items whose value actually
    /// changed so the save callback doesn't churn unmodified rows.
    /// </summary>
    private static List<T> RecomputeSortOrders<T>(
        IList<T> items,
        Func<T, int> parentIdSelector,
        Action<T, int> sortOrderSetter,
        Func<T, int>? sortOrderGetter,
        int sortStep) where T : class
    {
        var changed = new List<T>();
        var byParent = new Dictionary<int, List<T>>();

        // Preserve the visual order DX has already applied to the ObservableCollection
        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            var pid = parentIdSelector(item);
            if (!byParent.TryGetValue(pid, out var list))
                byParent[pid] = list = new List<T>();
            list.Add(item);
        }

        foreach (var siblings in byParent.Values)
        {
            int order = 0;
            foreach (var item in siblings)
            {
                var newOrder = order;
                var current  = sortOrderGetter?.Invoke(item) ?? int.MinValue;
                if (current != newOrder)
                {
                    sortOrderSetter(item, newOrder);
                    changed.Add(item);
                }
                order += sortStep;
            }
        }

        return changed;
    }
}
