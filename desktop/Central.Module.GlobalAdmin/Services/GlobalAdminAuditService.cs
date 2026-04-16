using Central.Core.Auth;
using Central.Data;

namespace Central.Module.GlobalAdmin.Services;

/// <summary>
/// Logs all Global Admin operations to central_platform.global_admin_audit_log.
/// Auto-captures the current user from AuthContext.
/// </summary>
public static class GlobalAdminAuditService
{
    private static DbRepository? _repo;

    public static void Initialize(DbRepository repo) => _repo = repo;

    public static async Task LogAsync(string action, string? entityType = null, string? entityId = null, object? details = null)
    {
        if (_repo == null) return;
        try
        {
            var user = AuthContext.Instance.CurrentUser;
            await _repo.InsertGlobalAdminAuditAsync(
                actorEmail: user?.Username ?? "system",
                action: action,
                entityType: entityType,
                entityId: entityId,
                details: details != null ? System.Text.Json.JsonSerializer.Serialize(details) : null);
        }
        catch { /* audit logging should never break the operation */ }
    }
}
