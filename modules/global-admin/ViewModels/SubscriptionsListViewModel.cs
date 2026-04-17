using Central.Engine.Commands;
using Central.Engine.Widgets;
using Central.Engine.Models;

namespace Central.Module.GlobalAdmin.ViewModels;

public class SubscriptionsListViewModel : ListViewModelBase<SubscriptionRecord>
{
    protected override string Category => "global_admin";
    protected override string TypeName => "Subscription";
    protected override string TypeNamePlural => "Subscriptions";

    public Func<Task<List<SubscriptionRecord>>>? Loader { get; set; }
    public Func<SubscriptionRecord, Task<int>>? Inserter { get; set; }
    public Func<SubscriptionRecord, Task>? Updater { get; set; }
    public Func<SubscriptionRecord, Task>? Deleter { get; set; }

    // Custom commands
    public AsyncRelayCommand ChangePlanCommand { get; }
    public AsyncRelayCommand CancelSubscriptionCommand { get; }

    public Func<SubscriptionRecord, Task>? OnChangePlan { get; set; }
    public Func<SubscriptionRecord, Task>? OnCancel { get; set; }

    public SubscriptionsListViewModel()
    {
        ChangePlanCommand = new AsyncRelayCommand(
            async () => { if (CurrentItem != null && OnChangePlan != null) await OnChangePlan(CurrentItem); },
            () => CurrentItem != null && CurrentItem.Status != "cancelled");

        CancelSubscriptionCommand = new AsyncRelayCommand(
            async () => { if (CurrentItem != null && OnCancel != null) await OnCancel(CurrentItem); },
            () => CurrentItem != null && CurrentItem.Status != "cancelled");
    }

    protected override async Task<List<SubscriptionRecord>> LoadItemsAsync()
        => Loader != null ? await Loader() : new();

    protected override async Task<int> InsertItemAsync(SubscriptionRecord item)
        => Inserter != null ? await Inserter(item) : 0;

    protected override async Task UpdateItemAsync(SubscriptionRecord item)
    {
        if (Updater != null) await Updater(item);
    }

    protected override async Task DeleteItemAsync(SubscriptionRecord item)
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
        items.Insert(sepIdx + 1, new ContextMenuItem("Change Plan", ChangePlanCommand));
        items.Insert(sepIdx + 2, new ContextMenuItem("Cancel Subscription", CancelSubscriptionCommand));
        return items;
    }
}
