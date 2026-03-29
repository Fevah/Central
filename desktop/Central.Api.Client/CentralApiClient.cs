using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Central.Api.Client;

/// <summary>
/// Typed HTTP client for the Central REST API.
/// Handles JWT auth, token refresh, and all CRUD operations.
/// </summary>
public class CentralApiClient : IDisposable
{
    private readonly HttpClient _http;
    private string? _token;

    public CentralApiClient(string baseUrl) : this(baseUrl, null) { }

    public CentralApiClient(string baseUrl, HttpMessageHandler? handler)
    {
        _http = handler != null ? new HttpClient(handler) : new HttpClient();
        _http.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
        _http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    /// <summary>True if we have a valid JWT token.</summary>
    public bool IsAuthenticated => !string.IsNullOrEmpty(_token);

    /// <summary>The current JWT token.</summary>
    public string? Token => _token;

    // ── Auth ─────────────────────────────────────────────────────────────

    /// <summary>Login with Windows username, returns true if successful.</summary>
    public async Task<LoginResult?> LoginAsync(string username)
    {
        _username = username;
        var resp = await _http.PostAsJsonAsync("api/auth/login", new { username });
        if (!resp.IsSuccessStatusCode) return null;

        var result = await resp.Content.ReadFromJsonAsync<LoginResult>();
        if (result != null)
        {
            _token = result.Token;
            _tokenExpiry = DateTime.UtcNow.AddHours(24); // Default JWT expiry
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token);
        }
        return result;
    }

    // ── Generic CRUD ─────────────────────────────────────────────────────

    private string? _username;
    private DateTime _tokenExpiry = DateTime.MinValue;

    /// <summary>Ensure the token is still valid. Re-login if expired or about to expire.</summary>
    private async Task EnsureAuthenticatedAsync()
    {
        if (!string.IsNullOrEmpty(_token) && DateTime.UtcNow < _tokenExpiry.AddMinutes(-2))
            return; // Token still valid with 2-min buffer

        if (!string.IsNullOrEmpty(_username))
            await LoginAsync(_username);
    }

    public async Task<List<T>> GetListAsync<T>(string path)
    {
        await EnsureAuthenticatedAsync();
        var resp = await _http.GetAsync(path);
        if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized && !string.IsNullOrEmpty(_username))
        {
            await LoginAsync(_username);
            resp = await _http.GetAsync(path);
        }
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<List<T>>() ?? new();
    }

    public async Task<T?> GetAsync<T>(string path)
    {
        await EnsureAuthenticatedAsync();
        var resp = await _http.GetAsync(path);
        if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized && !string.IsNullOrEmpty(_username))
        {
            await LoginAsync(_username);
            resp = await _http.GetAsync(path);
        }
        if (!resp.IsSuccessStatusCode) return default;
        return await resp.Content.ReadFromJsonAsync<T>();
    }

    public async Task<bool> PostAsync<T>(string path, T data)
    {
        var resp = await _http.PostAsJsonAsync(path, data);
        return resp.IsSuccessStatusCode;
    }

    public async Task<bool> PutAsync<T>(string path, T data)
    {
        var resp = await _http.PutAsJsonAsync(path, data);
        return resp.IsSuccessStatusCode;
    }

    public async Task<bool> DeleteAsync(string path)
    {
        var resp = await _http.DeleteAsync(path);
        return resp.IsSuccessStatusCode;
    }

    // ── Typed Endpoints ──────────────────────────────────────────────────

    public Task<List<JsonElement>> GetDevicesAsync() => GetListAsync<JsonElement>("api/devices");
    public Task<List<JsonElement>> GetSwitchesAsync() => GetListAsync<JsonElement>("api/switches");
    public Task<List<JsonElement>> GetP2PLinksAsync() => GetListAsync<JsonElement>("api/links/p2p");
    public Task<List<JsonElement>> GetB2BLinksAsync() => GetListAsync<JsonElement>("api/links/b2b");
    public Task<List<JsonElement>> GetFWLinksAsync() => GetListAsync<JsonElement>("api/links/fw");
    public Task<List<JsonElement>> GetVlansAsync() => GetListAsync<JsonElement>("api/vlans");
    public Task<List<JsonElement>> GetBgpConfigsAsync() => GetListAsync<JsonElement>("api/bgp");

    // ── SSH Operations (server-side) ─────────────────────────────────────

    /// <summary>Ping a switch via the API server.</summary>
    public async Task<JsonElement?> PingSwitchAsync(Guid switchId)
        => await PostAndGetAsync($"api/ssh/{switchId}/ping");

    /// <summary>Download running config from a switch via the API server.</summary>
    public async Task<JsonElement?> DownloadConfigAsync(Guid switchId)
        => await PostAndGetAsync($"api/ssh/{switchId}/download-config");

    /// <summary>Sync BGP config from a switch via the API server.</summary>
    public async Task<JsonElement?> SyncBgpAsync(Guid switchId)
        => await PostAndGetAsync($"api/ssh/{switchId}/sync-bgp");

    /// <summary>Batch ping all switches via the API server.</summary>
    public async Task<JsonElement?> PingAllSwitchesAsync()
        => await PostAndGetAsync("api/ssh/ping-all");

    private async Task<JsonElement?> PostAndGetAsync(string path)
    {
        var resp = await _http.PostAsync(path, null);
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadFromJsonAsync<JsonElement>();
    }

    // ── Search ─────────────────────────────────────────────────────────

    /// <summary>Global search across all entities.</summary>
    public async Task<JsonElement?> SearchAsync(string query, int limit = 50)
        => await GetAsync<JsonElement>($"api/search?q={Uri.EscapeDataString(query)}&limit={limit}");

    // ── Dashboard ─────────────────────────────────────────────────────

    /// <summary>Get platform dashboard KPIs.</summary>
    public async Task<JsonElement?> GetDashboardAsync()
        => await GetAsync<JsonElement>("api/dashboard");

    // ── Status ────────────────────────────────────────────────────────

    /// <summary>Get complete platform status.</summary>
    public async Task<JsonElement?> GetStatusAsync()
        => await GetAsync<JsonElement>("api/status");

    // ── Activity ──────────────────────────────────────────────────────

    /// <summary>Get global activity feed.</summary>
    public async Task<JsonElement?> GetActivityAsync(int limit = 50)
        => await GetAsync<JsonElement>($"api/activity/global?limit={limit}");

    /// <summary>Get current user's activity.</summary>
    public async Task<JsonElement?> GetMyActivityAsync(int limit = 30)
        => await GetAsync<JsonElement>($"api/activity/me?limit={limit}");

    // ── Sync ──────────────────────────────────────────────────────────

    /// <summary>Get sync configurations.</summary>
    public async Task<JsonElement?> GetSyncConfigsAsync()
        => await GetAsync<JsonElement>("api/sync/configs");

    /// <summary>Trigger a sync run.</summary>
    public async Task<JsonElement?> RunSyncAsync(int configId)
        => await PostAndGetAsync($"api/sync/configs/{configId}/run");

    // ── Audit ─────────────────────────────────────────────────────────

    /// <summary>Get audit log entries.</summary>
    public async Task<JsonElement?> GetAuditLogAsync(int limit = 200, string? entityType = null)
    {
        var url = $"api/audit?limit={limit}";
        if (!string.IsNullOrEmpty(entityType)) url += $"&entityType={Uri.EscapeDataString(entityType)}";
        return await GetAsync<JsonElement>(url);
    }

    // ── Identity ──────────────────────────────────────────────────────

    /// <summary>Get identity providers.</summary>
    public async Task<JsonElement?> GetIdentityProvidersAsync()
        => await GetAsync<JsonElement>("api/identity/providers");

    // ── Import ────────────────────────────────────────────────────────

    /// <summary>Import records into a target table.</summary>
    public async Task<JsonElement?> ImportAsync(string targetTable, string upsertKey, JsonElement records)
    {
        var payload = JsonSerializer.Serialize(new { target_table = targetTable, upsert_key = upsertKey, records });
        var content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");
        var resp = await _http.PostAsync("api/import", content);
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadFromJsonAsync<JsonElement>();
    }

    // ── Health ─────────────────────────────────────────────────────────

    /// <summary>Simple health check (no auth required).</summary>
    public async Task<JsonElement?> HealthCheckAsync()
    {
        try
        {
            var resp = await _http.GetAsync("api/health");
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadFromJsonAsync<JsonElement>();
        }
        catch { return null; }
    }

    // ── Global Admin ────────────────────────────────────────────────────

    public Task<List<JsonElement>> GetGlobalTenantsAsync() => GetListAsync<JsonElement>("api/global-admin/tenants");
    public Task<List<JsonElement>> GetGlobalUsersAsync() => GetListAsync<JsonElement>("api/global-admin/users");
    public Task<List<JsonElement>> GetGlobalSubscriptionsAsync() => GetListAsync<JsonElement>("api/global-admin/subscriptions");
    public Task<List<JsonElement>> GetGlobalLicensesAsync() => GetListAsync<JsonElement>("api/global-admin/licenses");
    public async Task<JsonElement?> GetPlatformDashboardAsync() => await GetAsync<JsonElement>("api/global-admin/dashboard");

    public async Task<bool> SuspendTenantAsync(Guid tenantId)
        => (await _http.PostAsync($"api/global-admin/tenants/{tenantId}/suspend", null)).IsSuccessStatusCode;

    public async Task<bool> ActivateTenantAsync(Guid tenantId)
        => (await _http.PostAsync($"api/global-admin/tenants/{tenantId}/activate", null)).IsSuccessStatusCode;

    public async Task<bool> ProvisionTenantAsync(Guid tenantId)
        => (await _http.PostAsync($"api/global-admin/tenants/{tenantId}/provision", null)).IsSuccessStatusCode;

    public Task<bool> GrantModuleLicenseAsync(Guid tenantId, string moduleCode)
        => PostAsync("api/global-admin/licenses", new { tenant_id = tenantId.ToString(), module_code = moduleCode });

    public Task<bool> RevokeModuleLicenseAsync(int licenseId)
        => DeleteAsync($"api/global-admin/licenses/{licenseId}");

    public void Dispose() => _http.Dispose();
}

public record LoginResult(string Token, string Username, string Role, string[] Permissions);
