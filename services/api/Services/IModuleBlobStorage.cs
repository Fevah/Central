namespace Central.Api.Services;

/// <summary>
/// Storage abstraction for module DLL blobs. Phase 2 of the
/// module-update system (see <c>docs/MODULE_UPDATE_SYSTEM.md</c>).
///
/// Current implementation: <see cref="FilesystemModuleBlobStorage"/>,
/// which writes under a configurable root directory. Works for
/// single-replica dev + on-prem. Multi-replica / K8s prod will
/// eventually need a MinIO-backed adapter implementing the same
/// interface; the storage root gives the adapter a clean seam.
/// Don't couple any endpoint or repository directly to the filesystem
/// adapter — always go through this interface.
/// </summary>
public interface IModuleBlobStorage
{
    /// <summary>
    /// Write a DLL blob for <paramref name="moduleCode"/> at
    /// <paramref name="version"/>. Overwrites any existing blob at
    /// the same coordinate — the publish endpoint is responsible for
    /// refusing re-publishes via the DB's UNIQUE (module_code, version)
    /// constraint, so by the time this is called the version is new
    /// OR a deliberate force-push (yank + republish) has happened.
    /// </summary>
    Task WriteAsync(string moduleCode, string version, Stream content, CancellationToken ct = default);

    /// <summary>
    /// Open a read-only stream for the DLL blob. Returns null when
    /// the blob doesn't exist (common for rows created before
    /// Phase 2 shipped — they have metadata but no bytes).
    /// Caller owns the stream + must dispose.
    /// </summary>
    Task<Stream?> OpenReadAsync(string moduleCode, string version, CancellationToken ct = default);

    /// <summary>Check whether a blob exists without opening it. Fast path.</summary>
    Task<bool> ExistsAsync(string moduleCode, string version, CancellationToken ct = default);

    /// <summary>
    /// Delete a blob. Called by the yank flow when a version is
    /// pulled for cause (crashing, wrong DLL, security). The DB row
    /// stays around with is_yanked=true so the audit trail is
    /// preserved; only the bytes go away.
    /// </summary>
    Task<bool> DeleteAsync(string moduleCode, string version, CancellationToken ct = default);
}

/// <summary>
/// Filesystem-backed <see cref="IModuleBlobStorage"/> — one file per
/// (moduleCode, version) under <see cref="Root"/>. Path shape is
/// <c>{Root}/{moduleCode}/{version}/module.dll</c>. Directories are
/// created on demand; safe against path-traversal because
/// moduleCode + version are validated elsewhere (DB CHECK + endpoint
/// parameter validation) and we strip anything that looks like a path
/// separator before use as a final defence.
/// </summary>
public sealed class FilesystemModuleBlobStorage : IModuleBlobStorage
{
    /// <summary>Root directory under which blobs live.</summary>
    public string Root { get; }

    public FilesystemModuleBlobStorage(string root)
    {
        if (string.IsNullOrWhiteSpace(root))
            throw new ArgumentException("Storage root must be set (CENTRAL_MODULE_STORAGE_ROOT env var or IOptions config).");
        Root = Path.GetFullPath(root);
        Directory.CreateDirectory(Root);
    }

    public async Task WriteAsync(string moduleCode, string version, Stream content, CancellationToken ct = default)
    {
        var path = Resolve(moduleCode, version);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var fs = File.Create(path);
        await content.CopyToAsync(fs, ct);
    }

    public Task<Stream?> OpenReadAsync(string moduleCode, string version, CancellationToken ct = default)
    {
        var path = Resolve(moduleCode, version);
        return Task.FromResult<Stream?>(File.Exists(path)
            ? File.OpenRead(path)
            : null);
    }

    public Task<bool> ExistsAsync(string moduleCode, string version, CancellationToken ct = default)
        => Task.FromResult(File.Exists(Resolve(moduleCode, version)));

    public Task<bool> DeleteAsync(string moduleCode, string version, CancellationToken ct = default)
    {
        var path = Resolve(moduleCode, version);
        if (!File.Exists(path)) return Task.FromResult(false);
        File.Delete(path);
        // Best-effort tidy: remove the version dir if empty, then the
        // module dir. Leaves Root in place. Failure to clean up is
        // non-fatal — the blob is the source of truth.
        TryRemoveEmptyDir(Path.GetDirectoryName(path)!);
        TryRemoveEmptyDir(Path.GetDirectoryName(Path.GetDirectoryName(path))!);
        return Task.FromResult(true);
    }

    private string Resolve(string moduleCode, string version)
    {
        var safeCode    = Sanitise(moduleCode);
        var safeVersion = Sanitise(version);
        // Path.Combine's final segment is fixed so malicious code/version
        // values that bypass Sanitise still can't escape Root via ../
        // (Path.GetFullPath would also collapse any residual traversal).
        var combined = Path.GetFullPath(Path.Combine(Root, safeCode, safeVersion, "module.dll"));
        if (!combined.StartsWith(Root, StringComparison.Ordinal))
            throw new InvalidOperationException($"Resolved blob path '{combined}' escaped the storage root '{Root}'.");
        return combined;
    }

    private static string Sanitise(string segment)
    {
        // Reject anything that could traverse. Real validation lives at
        // the endpoint boundary; this is the last-line defence.
        if (string.IsNullOrWhiteSpace(segment))
            throw new ArgumentException("Module code + version must be non-empty.", nameof(segment));
        foreach (var invalid in Path.GetInvalidFileNameChars())
            if (segment.Contains(invalid))
                throw new ArgumentException($"Illegal character '{invalid}' in segment '{segment}'.");
        if (segment.Contains("..") || segment.Contains('/') || segment.Contains('\\'))
            throw new ArgumentException($"Path traversal segment rejected: '{segment}'.");
        return segment;
    }

    private static void TryRemoveEmptyDir(string dir)
    {
        try
        {
            if (Directory.Exists(dir) && !Directory.EnumerateFileSystemEntries(dir).Any())
                Directory.Delete(dir);
        }
        catch { /* best effort */ }
    }
}
