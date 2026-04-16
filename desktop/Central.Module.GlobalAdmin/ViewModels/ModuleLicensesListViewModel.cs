using Central.Core.Commands;
using Central.Core.Widgets;
using Central.Core.Models;

namespace Central.Module.GlobalAdmin.ViewModels;

public class ModuleLicensesListViewModel : ListViewModelBase<ModuleLicenseRecord>
{
    protected override string Category => "global_admin";
    protected override string TypeName => "License";
    protected override string TypeNamePlural => "Licenses";

    public Func<Task<List<ModuleLicenseRecord>>>? Loader { get; set; }
    public Func<ModuleLicenseRecord, Task<int>>? Inserter { get; set; }
    public Func<ModuleLicenseRecord, Task>? Updater { get; set; }
    public Func<ModuleLicenseRecord, Task>? Deleter { get; set; }

    // Custom commands
    public AsyncRelayCommand GrantModuleCommand { get; }
    public AsyncRelayCommand RevokeModuleCommand { get; }

    public Func<Task>? OnGrantModule { get; set; }
    public Func<ModuleLicenseRecord, Task>? OnRevokeModule { get; set; }

    public ModuleLicensesListViewModel()
    {
        GrantModuleCommand = new AsyncRelayCommand(
            async () => { if (OnGrantModule != null) await OnGrantModule(); },
            () => true);

        RevokeModuleCommand = new AsyncRelayCommand(
            async () => { if (CurrentItem != null && OnRevokeModule != null) await OnRevokeModule(CurrentItem); },
            () => CurrentItem != null && !CurrentItem.IsBase);
    }

    protected override async Task<List<ModuleLicenseRecord>> LoadItemsAsync()
        => Loader != null ? await Loader() : new();

    protected override async Task<int> InsertItemAsync(ModuleLicenseRecord item)
        => Inserter != null ? await Inserter(item) : 0;

    protected override async Task UpdateItemAsync(ModuleLicenseRecord item)
    {
        if (Updater != null) await Updater(item);
    }

    protected override async Task DeleteItemAsync(ModuleLicenseRecord item)
    {
        if (Deleter != null) await Deleter(item);
    }

    protected override void OnCurrentItemChanged()
    {
    }

    public override List<ContextMenuItem> GetContextMenuItems()
    {
        var items = base.GetContextMenuItems();
        var sepIdx = items.FindIndex(i => i.IsSeparator);
        if (sepIdx < 0) sepIdx = items.Count;
        items.Insert(sepIdx, ContextMenuItem.Separator);
        items.Insert(sepIdx + 1, new ContextMenuItem("Grant Module", GrantModuleCommand));
        items.Insert(sepIdx + 2, new ContextMenuItem("Revoke Module", RevokeModuleCommand));
        return items;
    }
}
