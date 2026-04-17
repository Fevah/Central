using Central.Engine.Data;
using Central.Engine.Models;
using Central.Persistence;

namespace Central.Desktop.Services;

/// <summary>
/// IDataService implementation that delegates to DbRepository for direct PostgreSQL access.
/// The app currently uses DbRepository directly (VM.Repo). This service exists as the bridge
/// for Phase 7 when we switch between DirectDb and Api modes.
/// </summary>
public class DirectDbDataService : IDataService
{
    public DbRepository Repo { get; }
    public DirectDbDataService(DbRepository repo) => Repo = repo;

    public async Task<List<T>> GetDevicesAsync<T>(string[]? s = null) where T : class, new()
        => (await Repo.GetDevicesAsync(s?.ToList())).Cast<T>().ToList();
    public Task UpsertDeviceAsync(object d) => d is DeviceRecord r ? Repo.UpdateDeviceAsync(r) : Task.CompletedTask;
    public Task SoftDeleteDeviceAsync(int id) => Task.CompletedTask;

    public async Task<List<T>> GetSwitchesAsync<T>() where T : class, new()
        => (await Repo.GetSwitchesAsync()).Cast<T>().ToList();
    public Task<T?> GetSwitchByHostnameAsync<T>(string h) where T : class, new() => Task.FromResult(default(T));

    public async Task<List<T>> GetP2PLinksAsync<T>() where T : class, new()
        => (await Repo.GetP2PLinksAsync()).Cast<T>().ToList();
    public async Task<List<T>> GetB2BLinksAsync<T>() where T : class, new()
        => (await Repo.GetB2BLinksAsync()).Cast<T>().ToList();
    public async Task<List<T>> GetFWLinksAsync<T>() where T : class, new()
        => (await Repo.GetFWLinksAsync()).Cast<T>().ToList();
    public Task UpsertP2PLinkAsync(object l) => l is P2PLink p ? Repo.UpsertP2PLinkAsync(p) : Task.CompletedTask;
    public Task UpsertB2BLinkAsync(object l) => l is B2BLink b ? Repo.UpsertB2BLinkAsync(b) : Task.CompletedTask;
    public Task UpsertFWLinkAsync(object l) => l is FWLink f ? Repo.UpsertFWLinkAsync(f) : Task.CompletedTask;
    public Task DeleteLinkAsync(string t, int id) => t switch
    {
        "p2p" => Repo.DeleteP2PLinkAsync(id),
        "b2b" => Repo.DeleteB2BLinkAsync(id),
        "fw" => Repo.DeleteFWLinkAsync(id),
        _ => Task.CompletedTask
    };

    public Task<List<T>> GetVlansAsync<T>(string[]? s = null) where T : class, new() => Task.FromResult(new List<T>());
    public Task UpsertVlanAsync(object v) => Task.CompletedTask;

    public Task<List<T>> GetBgpConfigsAsync<T>() where T : class, new() => Task.FromResult(new List<T>());
    public Task<List<T>> GetBgpNeighborsAsync<T>(int id) where T : class, new() => Task.FromResult(new List<T>());
    public Task<List<T>> GetBgpNetworksAsync<T>(int id) where T : class, new() => Task.FromResult(new List<T>());

    public Task<List<T>> GetUsersAsync<T>() where T : class, new() => Task.FromResult(new List<T>());
    public Task<List<T>> GetRolesAsync<T>() where T : class, new() => Task.FromResult(new List<T>());
    public async Task<List<T>> GetLookupsAsync<T>() where T : class, new()
        => (await Repo.GetLookupItemsAsync()).Cast<T>().ToList();

    public Task<string?> GetUserSettingAsync(int u, string k) => Repo.GetUserSettingAsync(u, k);
    public Task SaveUserSettingAsync(int u, string k, string v) => Repo.SaveUserSettingAsync(u, k, v);
}
