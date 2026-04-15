using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Central.Api.Client;

/// <summary>
/// Client for the Rust admin-service. Tenant CRUD, global user management,
/// licensing, platform health. Routes via gateway /api/v1/admin/*.
/// </summary>
public class AdminServiceClient
{
    private readonly HttpClient _http;

    public AdminServiceClient(string baseUrl = "http://localhost:8000")
    {
        _http = new HttpClient { BaseAddress = new Uri(baseUrl.TrimEnd('/')) };
    }

    public void SetAuthToken(string token)
        => _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    public void SetTenant(string tenantId)
    {
        _http.DefaultRequestHeaders.Remove("X-Tenant-ID");
        _http.DefaultRequestHeaders.Add("X-Tenant-ID", tenantId);
    }

    // ── Tenants ──

    public async Task<List<TenantInfo>> GetTenantsAsync()
    {
        var resp = await _http.GetAsync("/api/v1/admin/tenants");
        if (!resp.IsSuccessStatusCode) return [];
        return await resp.Content.ReadFromJsonAsync<List<TenantInfo>>(JsonOpts) ?? [];
    }

    public async Task<TenantInfo?> CreateTenantAsync(string name, string? slug = null, string plan = "free", string? adminEmail = null)
    {
        var resp = await _http.PostAsJsonAsync("/api/v1/admin/tenants",
            new { name, slug, plan, admin_email = adminEmail }, JsonOpts);
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadFromJsonAsync<TenantInfo>(JsonOpts);
    }

    public async Task<bool> UpdateTenantStatusAsync(string tenantId, string status)
    {
        var resp = await _http.PutAsJsonAsync($"/api/v1/admin/tenants/{tenantId}/status",
            new { status }, JsonOpts);
        return resp.IsSuccessStatusCode;
    }

    public async Task<bool> ProvisionSchemaAsync(string tenantId)
    {
        var resp = await _http.PostAsync($"/api/v1/admin/tenants/{tenantId}/provision", null);
        return resp.IsSuccessStatusCode;
    }

    // ── Global Users ──

    public async Task<List<GlobalUserInfo>> GetGlobalUsersAsync()
    {
        var resp = await _http.GetAsync("/api/v1/admin/users");
        if (!resp.IsSuccessStatusCode) return [];
        return await resp.Content.ReadFromJsonAsync<List<GlobalUserInfo>>(JsonOpts) ?? [];
    }

    public async Task<bool> ResetPasswordAsync(string userId)
    {
        var resp = await _http.PostAsync($"/api/v1/admin/users/{userId}/reset-password", null);
        return resp.IsSuccessStatusCode;
    }

    public async Task<bool> ToggleAdminAsync(string userId)
    {
        var resp = await _http.PostAsync($"/api/v1/admin/users/{userId}/toggle-admin", null);
        return resp.IsSuccessStatusCode;
    }

    // ── Licensing ──

    public async Task<List<ModuleLicenseInfo>> GetLicensesAsync()
    {
        var resp = await _http.GetAsync("/api/v1/admin/licenses");
        if (!resp.IsSuccessStatusCode) return [];
        return await resp.Content.ReadFromJsonAsync<List<ModuleLicenseInfo>>(JsonOpts) ?? [];
    }

    public async Task<bool> GrantModuleAsync(string tenantId, string moduleId)
    {
        var resp = await _http.PostAsJsonAsync("/api/v1/admin/licenses/grant",
            new { tenant_id = tenantId, module_id = moduleId }, JsonOpts);
        return resp.IsSuccessStatusCode;
    }

    public async Task<bool> RevokeModuleAsync(string tenantId, string moduleId)
    {
        var resp = await _http.PostAsJsonAsync("/api/v1/admin/licenses/revoke",
            new { tenant_id = tenantId, module_id = moduleId }, JsonOpts);
        return resp.IsSuccessStatusCode;
    }

    public async Task<bool> ChangePlanAsync(string tenantId, string plan)
    {
        var resp = await _http.PutAsJsonAsync($"/api/v1/admin/tenants/{tenantId}/plan",
            new { plan }, JsonOpts);
        return resp.IsSuccessStatusCode;
    }

    // ── Subscriptions ──

    public async Task<List<SubscriptionInfo>> GetSubscriptionsAsync()
    {
        var resp = await _http.GetAsync("/api/v1/admin/subscriptions");
        if (!resp.IsSuccessStatusCode) return [];
        return await resp.Content.ReadFromJsonAsync<List<SubscriptionInfo>>(JsonOpts) ?? [];
    }

    // ── Platform Health ──

    public async Task<PlatformHealthResult?> GetPlatformHealthAsync()
    {
        var resp = await _http.GetAsync("/health");
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadFromJsonAsync<PlatformHealthResult>(JsonOpts);
    }

    public async Task<bool> IsHealthyAsync()
    {
        try { return (await _http.GetAsync("/health")).IsSuccessStatusCode; }
        catch { return false; }
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}

// ── DTOs ──

public class TenantInfo
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("slug")] public string Slug { get; set; } = "";
    [JsonPropertyName("status")] public string Status { get; set; } = "";
    [JsonPropertyName("plan")] public string Plan { get; set; } = "";
    [JsonPropertyName("user_count")] public int UserCount { get; set; }
    [JsonPropertyName("created_at")] public string CreatedAt { get; set; } = "";
}

public class GlobalUserInfo
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("email")] public string Email { get; set; } = "";
    [JsonPropertyName("display_name")] public string DisplayName { get; set; } = "";
    [JsonPropertyName("tenant_name")] public string TenantName { get; set; } = "";
    [JsonPropertyName("roles")] public string Roles { get; set; } = "";
    [JsonPropertyName("status")] public string Status { get; set; } = "";
    [JsonPropertyName("mfa_enabled")] public bool MfaEnabled { get; set; }
    [JsonPropertyName("last_login")] public string? LastLogin { get; set; }
}

public class ModuleLicenseInfo
{
    [JsonPropertyName("id")] public int Id { get; set; }
    [JsonPropertyName("tenant_id")] public string TenantId { get; set; } = "";
    [JsonPropertyName("tenant_name")] public string TenantName { get; set; } = "";
    [JsonPropertyName("module_name")] public string ModuleName { get; set; } = "";
    [JsonPropertyName("enabled")] public bool Enabled { get; set; }
    [JsonPropertyName("max_users")] public int MaxUsers { get; set; }
    [JsonPropertyName("expires_at")] public string? ExpiresAt { get; set; }
}

public class SubscriptionInfo
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("tenant_name")] public string TenantName { get; set; } = "";
    [JsonPropertyName("plan")] public string Plan { get; set; } = "";
    [JsonPropertyName("status")] public string Status { get; set; } = "";
    [JsonPropertyName("started_at")] public string StartedAt { get; set; } = "";
    [JsonPropertyName("expires_at")] public string? ExpiresAt { get; set; }
}

public class PlatformHealthResult
{
    [JsonPropertyName("status")] public string Status { get; set; } = "";
    [JsonPropertyName("service")] public string Service { get; set; } = "";
    [JsonPropertyName("backends")] public List<BackendHealthInfo> Backends { get; set; } = [];
}

public class BackendHealthInfo
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("url")] public string Url { get; set; } = "";
    [JsonPropertyName("healthy")] public bool Healthy { get; set; }
}
