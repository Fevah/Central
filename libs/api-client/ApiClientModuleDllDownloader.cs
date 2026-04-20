using Central.Engine.Modules;

namespace Central.ApiClient;

/// <summary>
/// <see cref="IModuleDllDownloader"/> adapter that delegates to
/// <see cref="ModuleCatalogClient.DownloadDllAsync"/>. Phase 5 of the
/// module-update system (see <c>docs/MODULE_UPDATE_SYSTEM.md</c>) —
/// wires the Phase 3 kernel's download seam to the Phase 2 HTTP
/// endpoint.
///
/// <para>Lives in <c>libs/api-client</c> because it's a thin HTTP
/// wrapper + api-client already references
/// <c>Central.Engine</c> (which is where <see cref="IModuleDllDownloader"/>
/// lives). Desktop wiring just constructs one of these pointed at
/// the shell's shared <see cref="ModuleCatalogClient"/> + registers
/// it with the <see cref="ModuleHostManager"/>.</para>
///
/// <para>Returns null on 404 / 410 (yanked) to match
/// <see cref="IModuleDllDownloader.DownloadAsync"/>'s contract.
/// Other failures propagate as exceptions.</para>
/// </summary>
public sealed class ApiClientModuleDllDownloader : IModuleDllDownloader
{
    private readonly ModuleCatalogClient _catalog;

    public ApiClientModuleDllDownloader(ModuleCatalogClient catalog)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
    }

    public Task<byte[]?> DownloadAsync(string moduleCode, string version, CancellationToken ct = default)
        => _catalog.DownloadDllAsync(moduleCode, version, ct);
}
