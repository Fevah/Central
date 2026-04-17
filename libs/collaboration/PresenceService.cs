using System.Collections.Concurrent;

namespace Central.Collaboration;

/// <summary>
/// Tracks who is currently editing which records.
/// In-memory for single-node; extensible to Redis for multi-node.
/// Used by SignalR to broadcast presence to tenant group.
/// </summary>
public class PresenceService
{
    private static PresenceService? _instance;
    public static PresenceService Instance => _instance ??= new();

    private readonly ConcurrentDictionary<string, PresenceEntry> _entries = new();

    /// <summary>Mark a user as editing an entity.</summary>
    public void JoinEditing(string tenantSlug, string entityType, string entityId, string username, string connectionId)
    {
        var key = $"{tenantSlug}:{entityType}:{entityId}:{connectionId}";
        _entries[key] = new PresenceEntry
        {
            TenantSlug = tenantSlug,
            EntityType = entityType,
            EntityId = entityId,
            Username = username,
            ConnectionId = connectionId,
            JoinedAt = DateTime.UtcNow
        };
    }

    /// <summary>Remove a user from editing.</summary>
    public void LeaveEditing(string tenantSlug, string entityType, string entityId, string connectionId)
    {
        var key = $"{tenantSlug}:{entityType}:{entityId}:{connectionId}";
        _entries.TryRemove(key, out _);
    }

    /// <summary>Remove all entries for a disconnected connection.</summary>
    public void DisconnectAll(string connectionId)
    {
        var keysToRemove = _entries.Keys.Where(k => k.EndsWith($":{connectionId}")).ToList();
        foreach (var key in keysToRemove)
            _entries.TryRemove(key, out _);
    }

    /// <summary>Get all users editing a specific entity.</summary>
    public List<PresenceEntry> GetEditors(string tenantSlug, string entityType, string entityId)
    {
        var prefix = $"{tenantSlug}:{entityType}:{entityId}:";
        return _entries.Values
            .Where(e => e.TenantSlug == tenantSlug && e.EntityType == entityType && e.EntityId == entityId)
            .ToList();
    }

    /// <summary>Get all active editing sessions for a tenant.</summary>
    public List<PresenceEntry> GetTenantPresence(string tenantSlug)
    {
        return _entries.Values.Where(e => e.TenantSlug == tenantSlug).ToList();
    }

    /// <summary>Get the total number of active editing sessions.</summary>
    public int ActiveCount => _entries.Count;
}

public class PresenceEntry
{
    public string TenantSlug { get; set; } = "";
    public string EntityType { get; set; } = "";
    public string EntityId { get; set; } = "";
    public string Username { get; set; } = "";
    public string ConnectionId { get; set; } = "";
    public DateTime JoinedAt { get; set; }
}
