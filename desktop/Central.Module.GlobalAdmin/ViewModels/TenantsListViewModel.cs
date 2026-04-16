using Central.Core.Commands;
using Central.Core.Widgets;
using Central.Core.Models;

namespace Central.Module.GlobalAdmin.ViewModels;

public class TenantsListViewModel : ListViewModelBase<TenantRecord>
{
    protected override string Category => "global_admin";
    protected override string TypeName => "Tenant";
    protected override string TypeNamePlural => "Tenants";

    // Delegate-based CRUD — wired by MainWindow after construction
    public Func<Task<List<TenantRecord>>>? Loader { get; set; }
    public Func<TenantRecord, Task<int>>? Inserter { get; set; }
    public Func<TenantRecord, Task>? Updater { get; set; }
    public Func<TenantRecord, Task>? Deleter { get; set; }

    // Custom commands
    public AsyncRelayCommand SuspendCommand { get; }
    public AsyncRelayCommand ActivateCommand { get; }
    public AsyncRelayCommand ProvisionSchemaCommand { get; }

    // Delegates for custom operations — wired by MainWindow
    public Func<TenantRecord, Task>? OnSuspend { get; set; }
    public Func<TenantRecord, Task>? OnActivate { get; set; }
    public Func<TenantRecord, Task>? OnProvisionSchema { get; set; }

    public TenantsListViewModel()
    {
        SuspendCommand = new AsyncRelayCommand(
            async () => { if (CurrentItem != null && OnSuspend != null) await OnSuspend(CurrentItem); },
            () => CurrentItem != null && CurrentItem.IsActive);

        ActivateCommand = new AsyncRelayCommand(
            async () => { if (CurrentItem != null && OnActivate != null) await OnActivate(CurrentItem); },
            () => CurrentItem != null && !CurrentItem.IsActive);

        ProvisionSchemaCommand = new AsyncRelayCommand(
            async () => { if (CurrentItem != null && OnProvisionSchema != null) await OnProvisionSchema(CurrentItem); },
            () => CurrentItem != null);
    }

    protected override async Task<List<TenantRecord>> LoadItemsAsync()
        => Loader != null ? await Loader() : new();

    protected override async Task<int> InsertItemAsync(TenantRecord item)
        => Inserter != null ? await Inserter(item) : 0;

    protected override async Task UpdateItemAsync(TenantRecord item)
    {
        if (Updater != null) await Updater(item);
    }

    protected override async Task DeleteItemAsync(TenantRecord item)
    {
        if (Deleter != null) await Deleter(item);
    }

    protected override void OnCurrentItemChanged()
    {
    }

    public override List<ContextMenuItem> GetContextMenuItems()
    {
        var items = base.GetContextMenuItems();
        // Insert tenant-specific actions before the separator
        var sepIdx = items.FindIndex(i => i.IsSeparator);
        if (sepIdx < 0) sepIdx = items.Count;
        items.Insert(sepIdx, ContextMenuItem.Separator);
        items.Insert(sepIdx + 1, new ContextMenuItem("Suspend", SuspendCommand));
        items.Insert(sepIdx + 2, new ContextMenuItem("Activate", ActivateCommand));
        items.Insert(sepIdx + 3, new ContextMenuItem("Provision Schema", ProvisionSchemaCommand));
        return items;
    }
}
