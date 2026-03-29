using System.IO;
using System.Security.Cryptography;

namespace Central.Core.Services;

/// <summary>
/// File management service — versioned file storage with entity attachment.
/// Ported from TotalLink's Repository module.
/// Supports: upload with MD5 integrity, versioning, entity attachment, metadata.
/// Storage: small files in DB (bytea), large files on filesystem.
/// </summary>
public class FileManagementService
{
    private static FileManagementService? _instance;
    public static FileManagementService Instance => _instance ??= new();

    private const long MaxInlineSize = 10 * 1024 * 1024; // 10MB — above this, use filesystem
    private string _storagePath = "";

    /// <summary>Configure filesystem storage path for large files.</summary>
    public void Configure(string storagePath)
    {
        _storagePath = storagePath;
        if (!string.IsNullOrEmpty(storagePath))
            Directory.CreateDirectory(storagePath);
    }

    /// <summary>Compute MD5 hash of file content.</summary>
    public static string ComputeMd5(byte[] data)
    {
        var hash = MD5.HashData(data);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>Compute MD5 hash of a stream.</summary>
    public static string ComputeMd5(Stream stream)
    {
        var hash = MD5.HashData(stream);
        stream.Position = 0;
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>Determine if a file should be stored inline (DB) or on filesystem.</summary>
    public bool ShouldStoreInline(long fileSize) => fileSize <= MaxInlineSize;

    /// <summary>Get the filesystem path for a file version.</summary>
    public string GetStoragePath(Guid fileId, int versionNumber)
    {
        var dir = Path.Combine(_storagePath, fileId.ToString("N")[..2], fileId.ToString("N"));
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, $"v{versionNumber}");
    }

    /// <summary>Save file data to filesystem (for large files).</summary>
    public async Task SaveToFilesystemAsync(string path, byte[] data)
    {
        await File.WriteAllBytesAsync(path, data);
    }

    /// <summary>Read file data from filesystem.</summary>
    public async Task<byte[]> ReadFromFilesystemAsync(string path)
    {
        return await File.ReadAllBytesAsync(path);
    }

    /// <summary>Delete a file from filesystem.</summary>
    public void DeleteFromFilesystem(string path)
    {
        if (File.Exists(path)) File.Delete(path);
    }

    // ── Storage Service Integration (CAS dedup backend) ──

    private string? _storageServiceUrl;

    /// <summary>Enable storage-service backend for file uploads (CAS deduplication).</summary>
    public void ConfigureStorageService(string storageServiceUrl)
    {
        _storageServiceUrl = storageServiceUrl;
    }

    /// <summary>True if storage-service is configured.</summary>
    public bool UseStorageService => !string.IsNullOrEmpty(_storageServiceUrl);

    /// <summary>Get the storage-service URL (for creating StorageServiceClient instances).</summary>
    public string? StorageServiceUrl => _storageServiceUrl;
}

/// <summary>File metadata record.</summary>
public class FileRecord
{
    public Guid Id { get; set; }
    public string Filename { get; set; } = "";
    public string Description { get; set; } = "";
    public string MimeType { get; set; } = "application/octet-stream";
    public long? FileSize { get; set; }
    public string? EntityType { get; set; }
    public string? EntityId { get; set; }
    public int? UploadedBy { get; set; }
    public string? Md5Hash { get; set; }
    public string Tags { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public int VersionCount { get; set; }

    public string FileSizeDisplay => FileSize switch
    {
        null => "",
        < 1024 => $"{FileSize} B",
        < 1024 * 1024 => $"{FileSize / 1024.0:F1} KB",
        < 1024 * 1024 * 1024 => $"{FileSize / (1024.0 * 1024):F1} MB",
        _ => $"{FileSize / (1024.0 * 1024 * 1024):F2} GB"
    };
}

/// <summary>File version record.</summary>
public class FileVersionRecord
{
    public Guid Id { get; set; }
    public Guid FileId { get; set; }
    public int VersionNumber { get; set; }
    public long? FileSize { get; set; }
    public string? Md5Hash { get; set; }
    public string? StoragePath { get; set; }
    public int? UploadedBy { get; set; }
    public DateTime CreatedAt { get; set; }
}
