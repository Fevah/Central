using System.Net.Http.Json;
using System.Text.Json;
using Central.Core.Data;

namespace Central.Api.Client;

/// <summary>
/// IDataService implementation that uses the Central REST API.
/// Deserializes JSON responses into typed model objects.
/// </summary>
public class ApiDataService : IDataService
{
    private readonly CentralApiClient _client;
    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public ApiDataService(CentralApiClient client) => _client = client;

    // ── Devices ──
    public async Task<List<T>> GetDevicesAsync<T>(string[]? allowedSites = null) where T : class, new()
    {
        var path = allowedSites?.Length > 0
            ? $"api/devices?sites={string.Join(",", allowedSites)}"
            : "api/devices";
        return await GetTypedListAsync<T>(path);
    }

    public async Task UpsertDeviceAsync(object device)
        => await _client.PutAsync("api/devices", device);

    public async Task SoftDeleteDeviceAsync(int id)
        => await _client.DeleteAsync($"api/devices/{id}");

    // ── Switches ──
    public Task<List<T>> GetSwitchesAsync<T>() where T : class, new()
        => GetTypedListAsync<T>("api/switches");

    public async Task<T?> GetSwitchByHostnameAsync<T>(string hostname) where T : class, new()
    {
        var list = await GetTypedListAsync<T>($"api/switches?hostname={hostname}");
        return list.FirstOrDefault();
    }

    // ── Links ──
    public Task<List<T>> GetP2PLinksAsync<T>() where T : class, new()
        => GetTypedListAsync<T>("api/links/p2p");

    public Task<List<T>> GetB2BLinksAsync<T>() where T : class, new()
        => GetTypedListAsync<T>("api/links/b2b");

    public Task<List<T>> GetFWLinksAsync<T>() where T : class, new()
        => GetTypedListAsync<T>("api/links/fw");

    public async Task UpsertP2PLinkAsync(object link)
        => await _client.PutAsync("api/links/p2p", link);

    public async Task UpsertB2BLinkAsync(object link)
        => await _client.PutAsync("api/links/b2b", link);

    public async Task UpsertFWLinkAsync(object link)
        => await _client.PutAsync("api/links/fw", link);

    public async Task DeleteLinkAsync(string linkType, int id)
        => await _client.DeleteAsync($"api/links/{linkType}/{id}");

    // ── VLANs ──
    public async Task<List<T>> GetVlansAsync<T>(string[]? sites = null) where T : class, new()
    {
        var path = sites?.Length > 0
            ? $"api/vlans?sites={string.Join(",", sites)}"
            : "api/vlans";
        return await GetTypedListAsync<T>(path);
    }

    public async Task UpsertVlanAsync(object vlan)
        => await _client.PutAsync("api/vlans", vlan);

    // ── BGP ──
    public Task<List<T>> GetBgpConfigsAsync<T>() where T : class, new()
        => GetTypedListAsync<T>("api/bgp");

    public Task<List<T>> GetBgpNeighborsAsync<T>(int bgpId) where T : class, new()
        => GetTypedListAsync<T>($"api/bgp/{bgpId}/neighbors");

    public Task<List<T>> GetBgpNetworksAsync<T>(int bgpId) where T : class, new()
        => GetTypedListAsync<T>($"api/bgp/{bgpId}/networks");

    // ── Admin ──
    public Task<List<T>> GetUsersAsync<T>() where T : class, new()
        => GetTypedListAsync<T>("api/admin/users");

    public Task<List<T>> GetRolesAsync<T>() where T : class, new()
        => GetTypedListAsync<T>("api/admin/roles");

    public Task<List<T>> GetLookupsAsync<T>() where T : class, new()
        => GetTypedListAsync<T>("api/admin/lookups");

    // ── Settings ──
    public async Task<string?> GetUserSettingAsync(int userId, string key)
    {
        var result = await _client.GetAsync<JsonElement>($"api/admin/settings/{userId}/{key}");
        return result.ValueKind == JsonValueKind.Undefined ? null : result.GetProperty("value").GetString();
    }

    public async Task SaveUserSettingAsync(int userId, string key, string value)
        => await _client.PutAsync($"api/admin/settings/{userId}/{key}", new { value });

    // ── Helper ──
    private async Task<List<T>> GetTypedListAsync<T>(string path) where T : class, new()
    {
        var elements = await _client.GetListAsync<JsonElement>(path);
        var result = new List<T>(elements.Count);
        foreach (var el in elements)
        {
            var item = el.Deserialize<T>(_jsonOpts);
            if (item != null) result.Add(item);
        }
        return result;
    }
}
