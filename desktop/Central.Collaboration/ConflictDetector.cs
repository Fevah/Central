namespace Central.Collaboration;

/// <summary>
/// Detects and resolves optimistic concurrency conflicts.
/// Compares row_version on save — if mismatch, identifies conflicting fields.
/// </summary>
public static class ConflictDetector
{
    /// <summary>
    /// Detect if there's a conflict between what the client sent and what's in the DB.
    /// Returns null if no conflict (versions match).
    /// </summary>
    public static ConflictInfo? Detect(long expectedVersion, long actualVersion,
        Dictionary<string, object?> clientValues, Dictionary<string, object?> serverValues)
    {
        if (expectedVersion == actualVersion) return null;

        var conflicts = new List<FieldConflict>();
        foreach (var (field, clientValue) in clientValues)
        {
            if (!serverValues.TryGetValue(field, out var serverValue)) continue;
            if (!ValuesEqual(clientValue, serverValue))
            {
                conflicts.Add(new FieldConflict
                {
                    FieldName = field,
                    ClientValue = clientValue,
                    ServerValue = serverValue
                });
            }
        }

        return new ConflictInfo
        {
            ExpectedVersion = expectedVersion,
            ActualVersion = actualVersion,
            Conflicts = conflicts,
            HasFieldConflicts = conflicts.Count > 0
        };
    }

    /// <summary>
    /// Three-way merge: base → client changes + server changes.
    /// Non-overlapping changes can be auto-merged.
    /// Overlapping changes require user resolution.
    /// </summary>
    public static MergeResult Merge(
        Dictionary<string, object?> baseValues,
        Dictionary<string, object?> clientValues,
        Dictionary<string, object?> serverValues)
    {
        var merged = new Dictionary<string, object?>(serverValues);
        var autoMerged = new List<string>();
        var needsResolution = new List<FieldConflict>();

        foreach (var (field, clientValue) in clientValues)
        {
            var baseValue = baseValues.GetValueOrDefault(field);
            var serverValue = serverValues.GetValueOrDefault(field);

            var clientChanged = !ValuesEqual(clientValue, baseValue);
            var serverChanged = !ValuesEqual(serverValue, baseValue);

            if (clientChanged && !serverChanged)
            {
                // Only client changed — take client value
                merged[field] = clientValue;
                autoMerged.Add(field);
            }
            else if (clientChanged && serverChanged)
            {
                // Both changed — conflict
                if (ValuesEqual(clientValue, serverValue))
                {
                    // Both changed to same value — no conflict
                    merged[field] = clientValue;
                }
                else
                {
                    needsResolution.Add(new FieldConflict
                    {
                        FieldName = field,
                        ClientValue = clientValue,
                        ServerValue = serverValue,
                        BaseValue = baseValue
                    });
                }
            }
            // If only server changed, keep server value (already in merged)
        }

        return new MergeResult
        {
            MergedValues = merged,
            AutoMergedFields = autoMerged,
            ConflictingFields = needsResolution,
            CanAutoMerge = needsResolution.Count == 0
        };
    }

    private static bool ValuesEqual(object? a, object? b)
    {
        if (a == null && b == null) return true;
        if (a == null || b == null) return false;
        return a.ToString() == b.ToString();
    }
}

public class ConflictInfo
{
    public long ExpectedVersion { get; set; }
    public long ActualVersion { get; set; }
    public List<FieldConflict> Conflicts { get; set; } = new();
    public bool HasFieldConflicts { get; set; }
}

public class FieldConflict
{
    public string FieldName { get; set; } = "";
    public object? ClientValue { get; set; }
    public object? ServerValue { get; set; }
    public object? BaseValue { get; set; }
}

public class MergeResult
{
    public Dictionary<string, object?> MergedValues { get; set; } = new();
    public List<string> AutoMergedFields { get; set; } = new();
    public List<FieldConflict> ConflictingFields { get; set; } = new();
    public bool CanAutoMerge { get; set; }
}
