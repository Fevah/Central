using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace Central.ApiClient;

/// <summary>
/// Thin typed wrapper over the <c>/api/modules/catalog</c> endpoints
/// (Phase 1 of the module-update system —
/// <c>docs/MODULE_UPDATE_SYSTEM.md</c>). The desktop shell + the web
/// admin page call this on startup / periodically to compare loaded
/// module versions to the server's current_version and surface a
/// "updates available" banner.
///
/// Phase 1 is read-only. Phase 2 adds <see cref="PublishAsync"/> +
/// <see cref="DownloadDllAsync"/> for CI upload + client download.
/// </summary>
public class ModuleCatalogClient
{
    private readonly HttpClient _http;

    // ── Optional in-memory catalog cache ─────────────────────────────────
    // The desktop calls ListCatalogAsync on startup + whenever a reload
    // banner resolves. Hitting the API every time is wasteful; the
    // catalog barely changes. <see cref="CatalogCacheTtl"/> defaults to
    // 30 seconds — short enough that newly-published versions surface
    // quickly, long enough that burst queries don't fan out. SignalR's
    // ModuleUpdated event invalidates the cache on publish so the
    // polling path stays as a fallback for disconnected clients.
    private readonly object _cacheLock = new();
    private IReadOnlyList<ModuleCatalogEntryDto>? _cached;
    private DateTimeOffset _cachedAt;

    /// <summary>How long <see cref="ListCatalogAsync"/> may return cached data.</summary>
    public TimeSpan CatalogCacheTtl { get; set; } = TimeSpan.FromSeconds(30);

    public ModuleCatalogClient(HttpClient http) => _http = http;

    /// <summary>
    /// Return the current module catalog — one entry per module the
    /// platform publishes, with the latest version's change_kind +
    /// min_engine_contract. Empty list is possible on a fresh DB
    /// before migration 109 runs; callers should treat null/empty as
    /// "no catalog data yet, skip update check."
    ///
    /// <para>Results are cached in-memory for
    /// <see cref="CatalogCacheTtl"/>. Pass
    /// <paramref name="bypassCache"/>=true to force a refetch (or
    /// call <see cref="InvalidateCatalogCache"/> ahead of time).</para>
    /// </summary>
    public async Task<IReadOnlyList<ModuleCatalogEntryDto>> ListCatalogAsync(CancellationToken ct = default, bool bypassCache = false)
    {
        if (!bypassCache)
        {
            lock (_cacheLock)
            {
                if (_cached is not null && DateTimeOffset.UtcNow - _cachedAt < CatalogCacheTtl)
                    return _cached;
            }
        }

        var entries = await _http.GetFromJsonAsync<List<ModuleCatalogEntryDto>>(
            "api/modules/catalog", ct);
        var result = (IReadOnlyList<ModuleCatalogEntryDto>)(entries ?? new List<ModuleCatalogEntryDto>());

        lock (_cacheLock)
        {
            _cached = result;
            _cachedAt = DateTimeOffset.UtcNow;
        }
        return result;
    }

    /// <summary>
    /// Drop the in-memory catalog cache. SignalR's <c>ModuleUpdated</c>
    /// handler should call this on every event so the next
    /// <see cref="ListCatalogAsync"/> refetches — otherwise the
    /// banner could show stale data for up to
    /// <see cref="CatalogCacheTtl"/> after a publish.
    /// </summary>
    public void InvalidateCatalogCache()
    {
        lock (_cacheLock)
        {
            _cached = null;
            _cachedAt = default;
        }
    }

    /// <summary>
    /// Published history for a single module. Newest first. Yanked rows
    /// included with <see cref="ModuleVersionDto.IsYanked"/>=true; the
    /// desktop ignores those for update-check purposes but an admin
    /// UI surfaces them greyed out.
    /// </summary>
    public async Task<IReadOnlyList<ModuleVersionDto>> ListVersionsAsync(string moduleCode, CancellationToken ct = default)
    {
        var versions = await _http.GetFromJsonAsync<List<ModuleVersionDto>>(
            $"api/modules/{Uri.EscapeDataString(moduleCode)}/versions", ct);
        return versions ?? new List<ModuleVersionDto>();
    }

    /// <summary>
    /// Phase 2 — cheap metadata lookup before downloading. Clients
    /// call this to read the SHA-256 + size + change_kind without
    /// pulling the DLL bytes. Returns null when the version is
    /// unknown.
    /// </summary>
    public async Task<ModuleManifestDto?> GetManifestAsync(string moduleCode, string version, CancellationToken ct = default)
    {
        var resp = await _http.GetAsync(
            $"api/modules/{Uri.EscapeDataString(moduleCode)}/{Uri.EscapeDataString(version)}/manifest", ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<ModuleManifestDto>(cancellationToken: ct);
    }

    /// <summary>
    /// Phase 2 — download the DLL bytes for <paramref name="moduleCode"/>
    /// at <paramref name="version"/>. Returns null when the version is
    /// unknown or yanked (HTTP 410 Gone). Caller verifies SHA-256
    /// against <see cref="ModuleManifestDto.Sha256"/> before trusting
    /// the bytes.
    /// </summary>
    public async Task<byte[]?> DownloadDllAsync(string moduleCode, string version, CancellationToken ct = default)
    {
        var resp = await _http.GetAsync(
            $"api/modules/{Uri.EscapeDataString(moduleCode)}/{Uri.EscapeDataString(version)}/dll", ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        if (resp.StatusCode == System.Net.HttpStatusCode.Gone)     return null;
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadAsByteArrayAsync(ct);
    }

    /// <summary>
    /// Phase 2 — CI publish. Uploads a DLL + metadata as
    /// multipart/form-data. Returns the server-computed row (ID,
    /// SHA-256, sizeBytes) for telemetry / the CI log.
    /// </summary>
    public async Task<ModulePublishResultDto?> PublishAsync(
        string moduleCode,
        string version,
        string changeKind,
        int minEngineContract,
        byte[] dllBytes,
        string? releaseNotes = null,
        bool setAsCurrent = true,
        CancellationToken ct = default)
    {
        using var form = new MultipartFormDataContent
        {
            { new StringContent(moduleCode),                       "moduleCode" },
            { new StringContent(version),                          "version" },
            { new StringContent(changeKind),                       "changeKind" },
            { new StringContent(minEngineContract.ToString()),     "minEngineContract" },
            { new StringContent(setAsCurrent ? "true" : "false"),  "setAsCurrent" },
        };
        if (!string.IsNullOrEmpty(releaseNotes))
            form.Add(new StringContent(releaseNotes), "releaseNotes");

        var fileContent = new ByteArrayContent(dllBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        form.Add(fileContent, "dll", $"{moduleCode}-{version}.dll");

        var resp = await _http.PostAsync("api/modules/publish", form, ct);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<ModulePublishResultDto>(cancellationToken: ct);
    }
}

/// <summary>One row from /api/modules/catalog.</summary>
public record ModuleCatalogEntryDto(
    string Code,
    string DisplayName,
    string? Description,
    bool IsBase,
    string? CurrentVersion,
    DateTime? CurrentVersionUpdatedAt,
    string? ChangeKind,
    int? MinEngineContract,
    DateTime? PublishedAt,
    string? ReleaseNotes
);

/// <summary>
/// One published version of a module. <see cref="ChangeKind"/> drives
/// the desktop's reaction when this version rolls out live
/// (HotSwap / SoftReload / FullRestart — see
/// <c>docs/MODULE_UPDATE_SYSTEM.md</c>).
/// </summary>
public record ModuleVersionDto(
    string Version,
    string ChangeKind,
    int MinEngineContract,
    string? Sha256,
    long? SizeBytes,
    string? ReleaseNotes,
    DateTime PublishedAt,
    Guid? PublishedBy,
    bool IsYanked,
    DateTime? YankedAt,
    string? YankedReason
);

/// <summary>Response from /manifest — lightweight metadata for a single published version.</summary>
public record ModuleManifestDto(
    string ModuleCode,
    string Version,
    string ChangeKind,
    int MinEngineContract,
    string? Sha256,
    long? SizeBytes,
    string? ReleaseNotes,
    DateTime PublishedAt,
    bool IsYanked,
    string? YankedReason
);

/// <summary>Response from /publish — server-computed identity + integrity.</summary>
public record ModulePublishResultDto(
    long Id,
    string ModuleCode,
    string Version,
    string ChangeKind,
    int MinEngineContract,
    string Sha256,
    long SizeBytes,
    DateTime PublishedAt,
    bool SetAsCurrent
);
