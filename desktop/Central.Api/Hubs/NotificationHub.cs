using Central.Collaboration;
using Microsoft.AspNetCore.SignalR;

namespace Central.Api.Hubs;

public class NotificationHub : Hub
{
    /// <summary>Join the tenant's SignalR group on connection.</summary>
    public override async Task OnConnectedAsync()
    {
        var tenantSlug = GetTenantSlug();
        if (!string.IsNullOrEmpty(tenantSlug))
            await Groups.AddToGroupAsync(Context.ConnectionId, tenantSlug);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var tenantSlug = GetTenantSlug();
        if (!string.IsNullOrEmpty(tenantSlug))
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, tenantSlug);

        // Clean up presence entries for this connection
        var username = Context.User?.Identity?.Name ?? "unknown";
        var leftEntities = PresenceService.Instance.GetTenantPresence(tenantSlug)
            .Where(e => e.ConnectionId == Context.ConnectionId).ToList();
        PresenceService.Instance.DisconnectAll(Context.ConnectionId);
        foreach (var entry in leftEntities)
            await Clients.Group(tenantSlug).SendAsync("EditorLeft", entry.EntityType, entry.EntityId, username);

        await base.OnDisconnectedAsync(exception);
    }

    // ── Presence ────────────────────────────────────────────────────────

    /// <summary>Mark the caller as editing an entity. Broadcasts EditorJoined to tenant group.</summary>
    public async Task JoinEditing(string entityType, string entityId)
    {
        var tenantSlug = GetTenantSlug();
        var username = Context.User?.Identity?.Name ?? "unknown";
        PresenceService.Instance.JoinEditing(tenantSlug, entityType, entityId, username, Context.ConnectionId);
        await Clients.Group(tenantSlug).SendAsync("EditorJoined", entityType, entityId, username);
    }

    /// <summary>Remove the caller from editing. Broadcasts EditorLeft to tenant group.</summary>
    public async Task LeaveEditing(string entityType, string entityId)
    {
        var tenantSlug = GetTenantSlug();
        var username = Context.User?.Identity?.Name ?? "unknown";
        PresenceService.Instance.LeaveEditing(tenantSlug, entityType, entityId, Context.ConnectionId);
        await Clients.Group(tenantSlug).SendAsync("EditorLeft", entityType, entityId, username);
    }

    /// <summary>Get all editors for a specific entity. Returns EditorsResult to caller only.</summary>
    public async Task GetEditors(string entityType, string entityId)
    {
        var tenantSlug = GetTenantSlug();
        var editors = PresenceService.Instance.GetEditors(tenantSlug, entityType, entityId)
            .Select(e => new { e.Username, e.JoinedAt }).ToList();
        await Clients.Caller.SendAsync("EditorsResult", entityType, entityId, editors);
    }

    /// <summary>Broadcasts a data-changed event to the tenant's group.</summary>
    public async Task SendDataChanged(string entity, int id, string action)
    {
        var group = GetTenantGroup();
        await group.SendAsync("DataChanged", entity, id, action);
    }

    /// <summary>Broadcasts a ping result to the tenant's group.</summary>
    public async Task SendPingResult(string hostname, bool success, double? latencyMs)
    {
        var group = GetTenantGroup();
        await group.SendAsync("PingResult", hostname, success, latencyMs);
    }

    /// <summary>Broadcasts sync progress to the tenant's group.</summary>
    public async Task SendSyncProgress(string hostname, string status, int progressPct)
    {
        var group = GetTenantGroup();
        await group.SendAsync("SyncProgress", hostname, status, progressPct);
    }

    /// <summary>Broadcasts a notification event to the tenant's group.</summary>
    public async Task SendNotification(string eventType, string title, string message, string severity)
    {
        var group = GetTenantGroup();
        await group.SendAsync("NotificationEvent", eventType, title, message, severity);
    }

    /// <summary>Broadcasts a webhook received event to the tenant's group.</summary>
    public async Task SendWebhookReceived(string source, long webhookId)
    {
        var group = GetTenantGroup();
        await group.SendAsync("WebhookReceived", source, webhookId);
    }

    /// <summary>Broadcasts an audit event to the tenant's group.</summary>
    public async Task SendAuditEvent(string action, string entityType, string entityName, string username)
    {
        var group = GetTenantGroup();
        await group.SendAsync("AuditEvent", action, entityType, entityName, username);
    }

    /// <summary>Broadcasts a sync completion to the tenant's group.</summary>
    public async Task SendSyncComplete(string configName, string status, int recordsRead, int recordsFailed)
    {
        var group = GetTenantGroup();
        await group.SendAsync("SyncComplete", configName, status, recordsRead, recordsFailed);
    }

    /// <summary>Broadcasts a session event to the tenant's group.</summary>
    public async Task SendSessionEvent(string eventType, string username, string authMethod)
    {
        var group = GetTenantGroup();
        await group.SendAsync("SessionEvent", eventType, username, authMethod);
    }

    // ── Helpers ──

    private string GetTenantSlug()
    {
        return Context.User?.FindFirst("tenant_slug")?.Value ?? "default";
    }

    private IClientProxy GetTenantGroup()
    {
        var slug = GetTenantSlug();
        return string.IsNullOrEmpty(slug) ? Clients.All : Clients.Group(slug);
    }
}
