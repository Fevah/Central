using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Central.ApiClient;

/// <summary>
/// Client for the Rust sync-service. Handles offline-first sync with Merkle tree diff.
/// Push local changes, pull remote changes, SSE event stream.
/// </summary>
public class SyncServiceClient
{
    private readonly HttpClient _http;

    public SyncServiceClient(string baseUrl = "http://localhost:8083")
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

    /// <summary>Push a document to the sync service.</summary>
    public async Task<SyncPushResult?> PushAsync(string collection, string key, object data)
    {
        var response = await _http.PostAsJsonAsync("/api/v1/sync/push", new
        {
            collection,
            key,
            data
        });
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<SyncPushResult>(JsonOpts);
    }

    /// <summary>Pull changes since a cursor.</summary>
    public async Task<SyncPullResult?> PullAsync(string collection, string? cursor = null)
    {
        var response = await _http.PostAsJsonAsync("/api/v1/sync/pull", new
        {
            collection,
            cursor
        });
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<SyncPullResult>(JsonOpts);
    }

    /// <summary>List documents in a collection.</summary>
    public async Task<List<SyncDocument>> ListAsync(string collection)
    {
        var response = await _http.GetAsync($"/api/v1/sync/{collection}");
        if (!response.IsSuccessStatusCode) return [];
        return await response.Content.ReadFromJsonAsync<List<SyncDocument>>(JsonOpts) ?? [];
    }

    /// <summary>Get a single document.</summary>
    public async Task<SyncDocument?> GetAsync(string collection, string key)
    {
        var response = await _http.GetAsync($"/api/v1/sync/{collection}/{key}");
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<SyncDocument>(JsonOpts);
    }

    /// <summary>Delete a document (soft delete).</summary>
    public async Task<bool> DeleteAsync(string collection, string key)
    {
        var response = await _http.DeleteAsync($"/api/v1/sync/{collection}/{key}");
        return response.IsSuccessStatusCode;
    }

    /// <summary>Get sync status.</summary>
    public async Task<SyncStatus?> GetStatusAsync()
    {
        var response = await _http.GetAsync("/api/v1/sync/status");
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<SyncStatus>(JsonOpts);
    }

    /// <summary>Open SSE stream for real-time sync events.</summary>
    public async Task<Stream?> OpenStreamAsync()
    {
        try
        {
            var response = await _http.GetAsync("/api/v1/sync/stream", HttpCompletionOption.ResponseHeadersRead);
            if (!response.IsSuccessStatusCode) return null;
            return await response.Content.ReadAsStreamAsync();
        }
        catch { return null; }
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

public class SyncPushResult
{
    [JsonPropertyName("key")] public string Key { get; set; } = "";
    [JsonPropertyName("version")] public long Version { get; set; }
    [JsonPropertyName("conflict")] public bool Conflict { get; set; }
}

public class SyncPullResult
{
    [JsonPropertyName("changes")] public List<SyncChange> Changes { get; set; } = [];
    [JsonPropertyName("cursor")] public string Cursor { get; set; } = "";
    [JsonPropertyName("has_more")] public bool HasMore { get; set; }
}

public class SyncChange
{
    [JsonPropertyName("collection")] public string Collection { get; set; } = "";
    [JsonPropertyName("key")] public string Key { get; set; } = "";
    [JsonPropertyName("operation")] public string Operation { get; set; } = ""; // insert, update, delete
    [JsonPropertyName("data")] public JsonElement? Data { get; set; }
    [JsonPropertyName("version")] public long Version { get; set; }
    [JsonPropertyName("timestamp")] public string Timestamp { get; set; } = "";
}

public class SyncDocument
{
    [JsonPropertyName("collection")] public string Collection { get; set; } = "";
    [JsonPropertyName("key")] public string Key { get; set; } = "";
    [JsonPropertyName("data")] public JsonElement? Data { get; set; }
    [JsonPropertyName("version")] public long Version { get; set; }
    [JsonPropertyName("content_hash")] public string ContentHash { get; set; } = "";
}

public class SyncStatus
{
    [JsonPropertyName("documents")] public long Documents { get; set; }
    [JsonPropertyName("pending_changes")] public long PendingChanges { get; set; }
    [JsonPropertyName("last_sync")] public string? LastSync { get; set; }
}
