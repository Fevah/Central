namespace Central.Engine.Data;

/// <summary>
/// Abstraction over data access — implemented by DirectDbDataService (Npgsql)
/// and ApiDataService (HTTP client). Allows WPF to work in either mode.
///
/// Phase 4.8: Define the interface.
/// Phase 4.9: Implement both services.
/// Phase 4.10: ConnectivityManager tri-mode (Api → DirectDb → Offline).
/// </summary>
public interface IDataService
{
    // ── Devices ──
    Task<List<T>> GetDevicesAsync<T>(string[]? allowedSites = null) where T : class, new();
    Task UpsertDeviceAsync(object device);
    Task SoftDeleteDeviceAsync(int id);

    // ── Switches ──
    Task<List<T>> GetSwitchesAsync<T>() where T : class, new();
    Task<T?> GetSwitchByHostnameAsync<T>(string hostname) where T : class, new();

    // ── Links ──
    Task<List<T>> GetP2PLinksAsync<T>() where T : class, new();
    Task<List<T>> GetB2BLinksAsync<T>() where T : class, new();
    Task<List<T>> GetFWLinksAsync<T>() where T : class, new();
    Task UpsertP2PLinkAsync(object link);
    Task UpsertB2BLinkAsync(object link);
    Task UpsertFWLinkAsync(object link);
    Task DeleteLinkAsync(string linkType, int id);

    // ── VLANs ──
    Task<List<T>> GetVlansAsync<T>(string[]? sites = null) where T : class, new();
    Task UpsertVlanAsync(object vlan);

    // ── BGP ──
    Task<List<T>> GetBgpConfigsAsync<T>() where T : class, new();
    Task<List<T>> GetBgpNeighborsAsync<T>(int bgpId) where T : class, new();
    Task<List<T>> GetBgpNetworksAsync<T>(int bgpId) where T : class, new();

    // ── Admin ──
    Task<List<T>> GetUsersAsync<T>() where T : class, new();
    Task<List<T>> GetRolesAsync<T>() where T : class, new();
    Task<List<T>> GetLookupsAsync<T>() where T : class, new();

    // ── Settings ──
    Task<string?> GetUserSettingAsync(int userId, string key);
    Task SaveUserSettingAsync(int userId, string key, string value);
}

/// <summary>Data service connection mode.</summary>
public enum DataServiceMode
{
    /// <summary>Direct PostgreSQL via Npgsql.</summary>
    DirectDb,
    /// <summary>Via REST API server.</summary>
    Api,
    /// <summary>No connection available.</summary>
    Offline
}
