using Central.Core.Commands;
using Central.Core.Widgets;
using Central.Core.Models;

namespace Central.Module.GlobalAdmin.ViewModels;

public class GlobalUsersListViewModel : ListViewModelBase<GlobalUserRecord>
{
    protected override string Category => "global_admin";
    protected override string TypeName => "User";
    protected override string TypeNamePlural => "Users";

    public Func<Task<List<GlobalUserRecord>>>? Loader { get; set; }
    public Func<GlobalUserRecord, Task<int>>? Inserter { get; set; }
    public Func<GlobalUserRecord, Task>? Updater { get; set; }
    public Func<GlobalUserRecord, Task>? Deleter { get; set; }

    // Custom commands
    public AsyncRelayCommand ToggleAdminCommand { get; }
    public AsyncRelayCommand ResetPasswordCommand { get; }

    public Func<GlobalUserRecord, Task>? OnToggleAdmin { get; set; }
    public Func<GlobalUserRecord, Task>? OnResetPassword { get; set; }

    public GlobalUsersListViewModel()
    {
        ToggleAdminCommand = new AsyncRelayCommand(
            async () => { if (CurrentItem != null && OnToggleAdmin != null) await OnToggleAdmin(CurrentItem); },
            () => CurrentItem != null);

        ResetPasswordCommand = new AsyncRelayCommand(
            async () => { if (CurrentItem != null && OnResetPassword != null) await OnResetPassword(CurrentItem); },
            () => CurrentItem != null);
    }

    protected override async Task<List<GlobalUserRecord>> LoadItemsAsync()
        => Loader != null ? await Loader() : new();

    protected override async Task<int> InsertItemAsync(GlobalUserRecord item)
        => Inserter != null ? await Inserter(item) : 0;

    protected override async Task UpdateItemAsync(GlobalUserRecord item)
    {
        if (Updater != null) await Updater(item);
    }

    protected override async Task DeleteItemAsync(GlobalUserRecord item)
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
        items.Insert(sepIdx + 1, new ContextMenuItem("Toggle Global Admin", ToggleAdminCommand));
        items.Insert(sepIdx + 2, new ContextMenuItem("Reset Password", ResetPasswordCommand));
        return items;
    }
}
