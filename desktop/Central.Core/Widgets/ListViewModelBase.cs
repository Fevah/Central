using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Input;
using Central.Core.Auth;
using Central.Core.Commands;
using Central.Core.Models;
using Central.Core.Services;
using Central.Core.Shell;

namespace Central.Core.Widgets;

/// <summary>
/// Base for all grid/list panel ViewModels. Provides:
/// - ObservableCollection items + selected items
/// - [WidgetCommand] decorated Add/Delete/Refresh/Duplicate/Export commands
/// - Permission-gated CanAdd/CanEdit/CanDelete
/// - Row validation auto-save pattern
/// - Text replacements ({Type}, {TypePlural})
/// - Context menu model (engine renders via DX PopupMenu)
/// - Selection change messaging via PanelMessageBus
///
/// Based on TotalLink's ListViewModelBase (815 lines).
/// Adapted for Npgsql (no XPO, no facades). Engine-first: every grid gets
/// enterprise features for free.
/// </summary>
public abstract class ListViewModelBase<T> : WidgetViewModelBase, IActionTarget where T : class, new()
{
    // ── Collections ──

    public ObservableCollection<T> Items { get; } = new();
    public ObservableCollection<T> SelectedItems { get; } = new();

    private T? _currentItem;
    public T? CurrentItem
    {
        get => _currentItem;
        set
        {
            _currentItem = value;
            OnPropertyChanged();
            OnCurrentItemChanged();
            // Broadcast selection to other panels
            PanelMessageBus.Publish(new SelectionChangedMessage(Category, value));
        }
    }

    private int _itemCount;
    public int ItemCount { get => _itemCount; private set { _itemCount = value; OnPropertyChanged(); } }

    private string _statusText = "";
    public string StatusText { get => _statusText; set { _statusText = value; OnPropertyChanged(); } }

    // ── Permission properties ──

    protected abstract string Category { get; }  // "devices", "links", "bgp"

    public bool CanAdd    => AuthContext.Instance.HasPermission($"{Category}:write");
    public bool CanEdit   => AuthContext.Instance.HasPermission($"{Category}:write");
    public bool CanDelete => AuthContext.Instance.HasPermission($"{Category}:delete");

    // ── Text replacements ──

    protected virtual string TypeName => typeof(T).Name;
    protected virtual string TypeNamePlural => TypeName + "s";

    public override WidgetCommandData GetWidgetCommandData()
    {
        var data = base.GetWidgetCommandData();
        data.TextReplacements["Type"] = TypeName;
        data.TextReplacements["TypePlural"] = TypeNamePlural;
        return data;
    }

    // ── Commands (decorated for auto-ribbon) ──

    [WidgetCommand("Add {Type}", "Edit", "Add a new {Type}")]
    public AsyncRelayCommand AddCommand { get; }

    [WidgetCommand("Delete {TypePlural}", "Edit", "Delete selected {TypePlural}")]
    public AsyncRelayCommand DeleteCommand { get; }

    [WidgetCommand("Duplicate {Type}", "Edit", "Duplicate the selected {Type}")]
    public AsyncRelayCommand DuplicateCommand { get; }

    [WidgetCommand("Refresh {TypePlural}", "Data", "Refresh the {Type} list")]
    public AsyncRelayCommand RefreshCommand { get; }

    [WidgetCommand("Export {TypePlural}", "Data", "Export to clipboard/CSV")]
    public AsyncRelayCommand ExportCommand { get; }

    // ── IActionTarget ──

    ICommand? IActionTarget.GetActionCommand(string actionKey) => actionKey switch
    {
        GlobalActionService.ActionAdd       => AddCommand,
        GlobalActionService.ActionDelete    => DeleteCommand,
        GlobalActionService.ActionDuplicate => DuplicateCommand,
        GlobalActionService.ActionRefresh   => RefreshCommand,
        GlobalActionService.ActionExport    => ExportCommand,
        _ => null
    };

    // ── Constructor ──

    protected ListViewModelBase()
    {
        AddCommand = new AsyncRelayCommand(OnAddExecuteAsync, () => CanAdd);
        DeleteCommand = new AsyncRelayCommand(OnDeleteExecuteAsync, () => CanDelete && SelectedItems.Count > 0);
        DuplicateCommand = new AsyncRelayCommand(OnDuplicateExecuteAsync, () => CanAdd && CurrentItem != null);
        RefreshCommand = new AsyncRelayCommand(OnRefreshExecuteAsync);
        ExportCommand = new AsyncRelayCommand(OnExportExecuteAsync);

        InitializeWidgetCommands();
    }

    // ── CRUD — override in concrete ViewModels ──

    protected abstract Task<List<T>> LoadItemsAsync();
    protected abstract Task<int> InsertItemAsync(T item);
    protected abstract Task UpdateItemAsync(T item);
    protected abstract Task DeleteItemAsync(T item);

    // ── Command handlers ──

    protected virtual async Task OnAddExecuteAsync()
    {
        var item = new T();
        Items.Insert(0, item);
        CurrentItem = item;
    }

    protected virtual async Task OnDeleteExecuteAsync()
    {
        if (SelectedItems.Count == 0) return;

        var count = SelectedItems.Count;
        var msg = count == 1
            ? $"Delete this {TypeName}?"
            : $"Delete {count} {TypeNamePlural}?";

        if (MessageBox.Show(msg, "Confirm Delete",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        var toDelete = SelectedItems.ToList();
        foreach (var item in toDelete)
        {
            try
            {
                await DeleteItemAsync(item);
                Items.Remove(item);
            }
            catch (Exception ex)
            {
                StatusText = $"Delete failed: {ex.Message}";
            }
        }
        ItemCount = Items.Count;
        PanelMessageBus.Publish(new DataModifiedMessage(Category, TypeName, "Delete"));
    }

    protected virtual async Task OnDuplicateExecuteAsync()
    {
        if (CurrentItem == null) return;
        var clone = CloneItem(CurrentItem);
        if (clone == null) return;

        // Reset Id to 0 so it gets inserted as new
        var idProp = clone.GetType().GetProperty("Id");
        if (idProp != null && idProp.PropertyType == typeof(int))
            idProp.SetValue(clone, 0);
        else if (idProp != null && idProp.PropertyType == typeof(Guid))
            idProp.SetValue(clone, Guid.Empty);

        Items.Insert(0, clone);
        CurrentItem = clone;
        StatusText = $"{TypeName} duplicated — edit and save";
    }

    protected virtual async Task OnRefreshExecuteAsync()
    {
        IsLoading = true;
        try
        {
            var data = await LoadItemsAsync();
            Items.Clear();
            foreach (var item in data) Items.Add(item);
            ItemCount = Items.Count;
            StatusText = $"{Items.Count} {TypeNamePlural} loaded";
        }
        finally
        {
            IsLoading = false;
        }
    }

    protected virtual async Task OnExportExecuteAsync()
    {
        // Export visible items to clipboard as tab-separated
        var props = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanRead && p.PropertyType.Namespace == "System")
            .ToArray();

        var sb = new StringBuilder();
        // Header
        sb.AppendLine(string.Join("\t", props.Select(p => p.Name)));
        // Rows
        foreach (var item in Items)
            sb.AppendLine(string.Join("\t", props.Select(p => p.GetValue(item)?.ToString() ?? "")));

        Clipboard.SetText(sb.ToString());
        StatusText = $"{Items.Count} {TypeNamePlural} copied to clipboard";
    }

    /// <summary>
    /// Called by grid ValidateRow — auto-save on row commit.
    /// Determines insert vs update by checking the Id property.
    /// </summary>
    public virtual async Task OnRowValidatedAsync(T item)
    {
        try
        {
            var idProp = item.GetType().GetProperty("Id");
            var isNew = false;
            if (idProp?.PropertyType == typeof(int))
                isNew = (int)(idProp.GetValue(item) ?? 0) == 0;
            else if (idProp?.PropertyType == typeof(Guid))
                isNew = (Guid)(idProp.GetValue(item) ?? Guid.Empty) == Guid.Empty;

            if (isNew)
                await InsertItemAsync(item);
            else
                await UpdateItemAsync(item);

            ItemCount = Items.Count;
            PanelMessageBus.Publish(new DataModifiedMessage(Category, TypeName, isNew ? "Insert" : "Update"));
        }
        catch (Exception ex)
        {
            StatusText = $"Save failed: {ex.Message}";
        }
    }

    /// <summary>Override to react to current item changes (e.g. load detail panel).</summary>
    protected virtual void OnCurrentItemChanged() { }

    // ── Helpers ──

    /// <summary>Shallow-clone an item by copying all public writable properties.</summary>
    protected virtual T? CloneItem(T source)
    {
        var clone = new T();
        foreach (var prop in typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!prop.CanWrite || !prop.CanRead) continue;
            try { prop.SetValue(clone, prop.GetValue(source)); }
            catch { }
        }
        return clone;
    }

    // ── Context menu model (engine renders via DX PopupMenu) ──

    /// <summary>
    /// Returns the standard context menu items for this grid.
    /// The engine/shell reads this to build a DX PopupMenu.
    /// Modules can override to add custom items.
    /// </summary>
    public virtual List<ContextMenuItem> GetContextMenuItems()
    {
        var items = new List<ContextMenuItem>();
        if (CanAdd) items.Add(new ContextMenuItem("Add", AddCommand));
        if (CanEdit) items.Add(new ContextMenuItem("Edit", null)); // wired by shell
        if (CanAdd) items.Add(new ContextMenuItem("Duplicate", DuplicateCommand));
        if (CanDelete) items.Add(new ContextMenuItem("Delete", DeleteCommand));
        items.Add(ContextMenuItem.Separator);
        items.Add(new ContextMenuItem("Refresh", RefreshCommand));
        items.Add(new ContextMenuItem("Export to Clipboard", ExportCommand));
        return items;
    }
}

/// <summary>Context menu item model for the engine to render.</summary>
public class ContextMenuItem
{
    public string Text { get; }
    public AsyncRelayCommand? Command { get; }
    public bool IsSeparator { get; }

    public ContextMenuItem(string text, AsyncRelayCommand? command)
    {
        Text = text;
        Command = command;
    }

    private ContextMenuItem() { IsSeparator = true; }
    public static readonly ContextMenuItem Separator = new();
}
