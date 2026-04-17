using Central.Engine.Models;
using Central.Engine.Widgets;

namespace Central.Module.VLANs;

/// <summary>
/// Pilot VM that wires VLAN CRUD through <see cref="ListViewModelBase{T}"/>.
/// Keeps the existing MainViewModel-owned <c>VlanEntries</c> collection as the
/// grid's ItemsSource (no data-flow change), and just exposes the uniform
/// Add / Edit / Delete / Refresh / Duplicate / Export / Undo / Redo commands that
/// <see cref="Central.Engine.Services.GlobalActionService"/> dispatches to.
///
/// Proves out per-panel undo: pushing an Add here recorded on
/// <see cref="ListViewModelBase.Undo"/> is independent of the other panels'
/// undo stacks, so Ctrl+Z on the VLANs tab rolls back only VLAN edits.
///
/// The shell supplies repo adapters so this VM has no direct DbRepository dep
/// and can move into Central.Engine once other modules adopt the pattern.
/// </summary>
public sealed class VlansListViewModel : ListViewModelBase<VlanEntry>
{
    private readonly Func<Task<List<VlanEntry>>> _loader;
    private readonly Func<VlanEntry, Task> _saver;
    private readonly Func<VlanEntry, Task> _deleter;

    public VlansListViewModel(
        Func<Task<List<VlanEntry>>> loader,
        Func<VlanEntry, Task> saver,
        Func<VlanEntry, Task> deleter)
    {
        _loader  = loader;
        _saver   = saver;
        _deleter = deleter;
    }

    protected override string Category => "vlans";
    protected override string TypeName => "VLAN";
    protected override string TypeNamePlural => "VLANs";

    protected override Task<List<VlanEntry>> LoadItemsAsync() => _loader();

    protected override async Task<int> InsertItemAsync(VlanEntry item)
    {
        await _saver(item);
        return item.Id;
    }

    protected override Task UpdateItemAsync(VlanEntry item) => _saver(item);
    protected override Task DeleteItemAsync(VlanEntry item) => _deleter(item);
}
