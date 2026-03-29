using Central.Collaboration;
using Central.Tenancy;

namespace Central.Api.Endpoints;

/// <summary>
/// Read-only presence queries — who is editing what.
/// Join/leave operations go through SignalR (NotificationHub), not REST.
/// </summary>
public static class PresenceEndpoints
{
    public static RouteGroupBuilder MapPresenceEndpoints(this RouteGroupBuilder group)
    {
        // GET /editors/{entityType}/{entityId} — who's editing a specific entity
        group.MapGet("/editors/{entityType}/{entityId}", (string entityType, string entityId,
            PresenceService presence, TenantContext tenant) =>
        {
            var editors = presence.GetEditors(tenant.TenantSlug, entityType, entityId);
            return Results.Ok(editors.Select(e => new
            {
                e.Username,
                e.EntityType,
                e.EntityId,
                e.JoinedAt
            }));
        });

        // GET /tenant — all active editing sessions for the current tenant
        group.MapGet("/tenant", (PresenceService presence, TenantContext tenant) =>
        {
            var sessions = presence.GetTenantPresence(tenant.TenantSlug);
            return Results.Ok(new
            {
                active_count = sessions.Count,
                sessions = sessions.Select(e => new
                {
                    e.Username,
                    e.EntityType,
                    e.EntityId,
                    e.JoinedAt
                })
            });
        });

        return group;
    }
}
