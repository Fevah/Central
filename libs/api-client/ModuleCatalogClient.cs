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

    public ModuleCatalogClient(HttpClient http) => _http = http;

    /// <summary>
    /// Return the current module catalog — one entry per module the
    /// platform publishes, with the latest version's change_kind +
    /// min_engine_contract. Empty list is possible on a fresh DB
    /// before migration 109 runs; callers should treat null/empty as
    /// "no catalog data yet, skip update check."
    /// </summary>
    public async Task<IReadOnlyList<ModuleCatalogEntryDto>> ListCatalogAsync(CancellationToken ct = default)
    {
        var entries = await _http.GetFromJsonAsync<List<ModuleCatalogEntryDto>>(
            "api/modules/catalog", ct);
        return entries ?? new List<ModuleCatalogEntryDto>();
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
