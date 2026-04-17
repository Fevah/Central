namespace Central.Core.Services;

/// <summary>
/// Append-only audit logging service.
/// All entity changes are logged with before/after state.
/// </summary>
public interface IAuditService
{
    Task LogAsync(string category, int entityId, string action,
        Dictionary<string, object?>? oldValue = null,
        Dictionary<string, object?>? newValue = null,
        string? summary = null);

    Task LogAsync(string category, string entityId, string action,
        string? summary = null);
}
