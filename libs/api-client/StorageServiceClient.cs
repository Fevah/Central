using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Central.ApiClient;

/// <summary>
/// Client for the Rust storage-service. Handles file upload, download, presigned URLs.
/// Content-addressed storage with BLAKE3 deduplication.
/// </summary>
public class StorageServiceClient
{
    private readonly HttpClient _http;

    public StorageServiceClient(string baseUrl = "http://localhost:8084")
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

    /// <summary>Upload a file. Returns the object key (content-addressed hash).</summary>
    public async Task<StorageUploadResult?> UploadAsync(string bucket, string key, Stream content, string contentType, string? fileName = null)
    {
        using var multipart = new MultipartFormDataContent();
        var streamContent = new StreamContent(content);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        multipart.Add(streamContent, "file", fileName ?? key);

        var response = await _http.PostAsync($"/api/v1/storage/objects/{bucket}/{key}", multipart);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<StorageUploadResult>(JsonOpts);
    }

    /// <summary>Download a file by bucket + key.</summary>
    public async Task<Stream?> DownloadAsync(string bucket, string key)
    {
        var response = await _http.GetAsync($"/api/v1/storage/objects/{bucket}/{key}");
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadAsStreamAsync();
    }

    /// <summary>Get a pre-signed download URL (valid for 15 minutes by default).</summary>
    public async Task<string?> GetPresignedUrlAsync(string bucket, string key)
    {
        var response = await _http.GetAsync($"/api/v1/storage/objects/{bucket}/{key}/presign");
        if (!response.IsSuccessStatusCode) return null;
        var result = await response.Content.ReadFromJsonAsync<PresignedUrlResult>(JsonOpts);
        return result?.Url;
    }

    /// <summary>Get object metadata.</summary>
    public async Task<StorageObjectMeta?> GetMetadataAsync(string bucket, string key)
    {
        var response = await _http.GetAsync($"/api/v1/storage/objects/{bucket}/{key}/meta");
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<StorageObjectMeta>(JsonOpts);
    }

    /// <summary>Delete an object (soft delete).</summary>
    public async Task<bool> DeleteAsync(string bucket, string key)
    {
        var response = await _http.DeleteAsync($"/api/v1/storage/objects/{bucket}/{key}");
        return response.IsSuccessStatusCode;
    }

    /// <summary>List objects in a bucket.</summary>
    public async Task<List<StorageObjectMeta>> ListAsync(string? bucket = null, int limit = 50, int offset = 0)
    {
        var url = $"/api/v1/storage/objects?limit={limit}&offset={offset}";
        if (!string.IsNullOrEmpty(bucket)) url += $"&bucket={bucket}";
        var response = await _http.GetAsync(url);
        if (!response.IsSuccessStatusCode) return [];
        return await response.Content.ReadFromJsonAsync<List<StorageObjectMeta>>(JsonOpts) ?? [];
    }

    /// <summary>Get storage stats.</summary>
    public async Task<StorageStats?> GetStatsAsync()
    {
        var response = await _http.GetAsync("/api/v1/storage/stats");
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<StorageStats>(JsonOpts);
    }

    /// <summary>Check health.</summary>
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

public class StorageUploadResult
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("key")] public string Key { get; set; } = "";
    [JsonPropertyName("content_hash")] public string ContentHash { get; set; } = "";
    [JsonPropertyName("size")] public long Size { get; set; }
    [JsonPropertyName("deduplicated")] public bool Deduplicated { get; set; }
}

public class StorageObjectMeta
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("bucket")] public string Bucket { get; set; } = "";
    [JsonPropertyName("key")] public string Key { get; set; } = "";
    [JsonPropertyName("content_type")] public string ContentType { get; set; } = "";
    [JsonPropertyName("size")] public long Size { get; set; }
    [JsonPropertyName("content_hash")] public string ContentHash { get; set; } = "";
    [JsonPropertyName("created_at")] public string CreatedAt { get; set; } = "";
}

public class PresignedUrlResult
{
    [JsonPropertyName("url")] public string Url { get; set; } = "";
    [JsonPropertyName("expires_in")] public int ExpiresIn { get; set; }
}

public class StorageStats
{
    [JsonPropertyName("total_objects")] public long TotalObjects { get; set; }
    [JsonPropertyName("total_size")] public long TotalSize { get; set; }
    [JsonPropertyName("dedup_savings")] public long DedupSavings { get; set; }
}
