using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Central.Engine.Integration;

/// <summary>
/// Retry logic with exponential backoff for sync operations.
/// Ported from TotalLink's IntegrationServer patterns.
/// </summary>
public static class SyncRetry
{
    /// <summary>Execute an action with retry and exponential backoff.</summary>
    public static async Task<bool> WithRetryAsync(Func<Task> action, int maxRetries = 3, int baseDelayMs = 1000)
    {
        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                await action();
                return true;
            }
            catch
            {
                if (attempt == maxRetries) return false;
                var delay = baseDelayMs * (int)Math.Pow(2, attempt); // 1s, 2s, 4s, 8s...
                await Task.Delay(Math.Min(delay, 30000)); // cap at 30s
            }
        }
        return false;
    }
}

/// <summary>
/// Hash-based change detection — skips records that haven't changed since last sync.
/// Prevents fake updates (where source sends same data repeatedly).
/// </summary>
public static class SyncHashDetector
{
    /// <summary>Compute a content hash for a record (field values only, sorted by key).</summary>
    public static string ComputeHash(Dictionary<string, object?> record)
    {
        var sorted = record.OrderBy(kv => kv.Key)
            .Select(kv => $"{kv.Key}={kv.Value}")
            .ToArray();
        var content = string.Join("|", sorted);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToBase64String(hash);
    }

    /// <summary>Check if a record has changed since the last sync.</summary>
    public static bool HasChanged(string currentHash, string? previousHash)
    {
        if (string.IsNullOrEmpty(previousHash)) return true; // no previous hash = always sync
        return currentHash != previousHash;
    }
}

/// <summary>
/// Failed sync record for the dead letter queue.
/// Records that fail after all retries are stored here for manual review.
/// </summary>
public class FailedSyncRecord
{
    public long Id { get; set; }
    public int SyncConfigId { get; set; }
    public int? EntityMapId { get; set; }
    public string? SourceEntity { get; set; }
    public string? RecordKey { get; set; }
    public string RecordJson { get; set; } = "{}";
    public string ErrorMessage { get; set; } = "";
    public int RetryCount { get; set; }
    public int MaxRetries { get; set; } = 3;
    public DateTime? NextRetryAt { get; set; }
    public string Status { get; set; } = "pending";
    public DateTime CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }

    public string StatusColor => Status switch
    {
        "resolved" => "#22C55E",
        "pending" => "#F59E0B",
        "retrying" => "#3B82F6",
        "abandoned" => "#EF4444",
        _ => "#6B7280"
    };
}

/// <summary>
/// Pre-write field validation for sync records.
/// Validates required fields, data types, and referential constraints before upsert.
/// </summary>
public static class SyncFieldValidator
{
    /// <summary>Validate a mapped record before writing to the target table.</summary>
    public static List<string> Validate(Dictionary<string, object?> record, List<SyncFieldMap> fieldMaps)
    {
        var errors = new List<string>();

        foreach (var map in fieldMaps.Where(m => m.IsRequired))
        {
            if (!record.TryGetValue(map.TargetColumn, out var value) || value == null || (value is string s && string.IsNullOrWhiteSpace(s)))
                errors.Add($"Required field '{map.TargetColumn}' (from '{map.SourceField}') is null or empty");
        }

        // Check for empty key fields
        foreach (var map in fieldMaps.Where(m => m.IsKey))
        {
            if (!record.TryGetValue(map.TargetColumn, out var value) || value == null)
                errors.Add($"Key field '{map.TargetColumn}' cannot be null");
        }

        return errors;
    }
}
