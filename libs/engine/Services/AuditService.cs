namespace Central.Engine.Services;

/// <summary>
/// Structured audit trail for all CRUD operations across the platform.
/// Records: who did what, when, on which entity, with before/after snapshots.
/// Stored in the audit_log table (migration 025).
/// </summary>
public class AuditService
{
    private static AuditService? _instance;
    public static AuditService Instance => _instance ??= new();

    private Func<AuditEntry, Task>? _persistFunc;
    private Action<string, string, string?, string?>? _broadcastFunc;

    /// <summary>Set the persistence callback (wired to DbRepository at startup).</summary>
    public void SetPersistFunc(Func<AuditEntry, Task> func) => _persistFunc = func;

    /// <summary>Set the SignalR broadcast callback (wired in App startup when API connected).</summary>
    public void SetBroadcastFunc(Action<string, string, string?, string?> func) => _broadcastFunc = func;

    /// <summary>Record an audit event.</summary>
    public async Task LogAsync(string action, string entityType, string? entityId = null,
        string? entityName = null, string? details = null,
        Dictionary<string, object?>? before = null, Dictionary<string, object?>? after = null)
    {
        var entry = new AuditEntry
        {
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            EntityName = entityName,
            Username = Auth.AuthContext.Instance.CurrentUser?.Username ?? "system",
            UserId = Auth.AuthContext.Instance.CurrentUser?.Id,
            Details = details,
            BeforeJson = before != null ? System.Text.Json.JsonSerializer.Serialize(before) : null,
            AfterJson = after != null ? System.Text.Json.JsonSerializer.Serialize(after) : null,
            Timestamp = DateTime.UtcNow
        };

        if (_persistFunc != null)
        {
            try { await _persistFunc(entry); }
            catch { /* audit logging must never block the operation */ }
        }

        // Broadcast via SignalR for real-time multi-user awareness
        if (_broadcastFunc != null)
        {
            try { _broadcastFunc(action, entityType, entityName, entry.Username); }
            catch { }
        }
    }

    // ── Convenience methods ──

    public Task LogCreateAsync(string entityType, string entityId, string? entityName = null, Dictionary<string, object?>? fields = null)
        => LogAsync("Create", entityType, entityId, entityName, after: fields);

    public Task LogUpdateAsync(string entityType, string entityId, string? entityName = null,
        Dictionary<string, object?>? before = null, Dictionary<string, object?>? after = null)
        => LogAsync("Update", entityType, entityId, entityName, before: before, after: after);

    public Task LogDeleteAsync(string entityType, string entityId, string? entityName = null)
        => LogAsync("Delete", entityType, entityId, entityName);

    public Task LogViewAsync(string entityType, string? entityId = null, string? details = null)
        => LogAsync("View", entityType, entityId, details: details);

    public Task LogExportAsync(string entityType, string? details = null)
        => LogAsync("Export", entityType, details: details);

    public Task LogLoginAsync(string username, bool success, string? provider = null)
        => LogAsync(success ? "Login" : "LoginFailed", "User", entityName: username, details: provider);

    public Task LogSettingChangeAsync(string settingKey, string? oldValue, string? newValue)
        => LogAsync("SettingChange", "Setting", entityId: settingKey,
            before: new() { ["value"] = oldValue },
            after: new() { ["value"] = newValue });
}

/// <summary>Single audit log entry.</summary>
public class AuditEntry
{
    public string Action { get; set; } = "";
    public string EntityType { get; set; } = "";
    public string? EntityId { get; set; }
    public string? EntityName { get; set; }
    public string? Username { get; set; }
    public int? UserId { get; set; }
    public string? Details { get; set; }
    public string? BeforeJson { get; set; }
    public string? AfterJson { get; set; }
    public DateTime Timestamp { get; set; }
}
