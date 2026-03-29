using System.Security.Claims;
using System.Text.Json;
using Npgsql;
using Central.Data;

namespace Central.Api.Endpoints;

public static class NotificationEndpoints
{
    public static RouteGroupBuilder MapNotificationEndpoints(this RouteGroupBuilder group)
    {
        // Get current user's notification preferences
        group.MapGet("/preferences", async (HttpContext ctx, DbConnectionFactory db) =>
        {
            var userId = GetUserId(ctx);
            if (userId == 0) return Results.Unauthorized();
            var repo = new DbRepository(db.ConnectionString);
            return Results.Ok(await repo.GetNotificationPreferencesAsync(userId));
        });

        // Update a notification preference
        group.MapPut("/preferences", async (HttpContext ctx, DbConnectionFactory db, JsonElement body) =>
        {
            var userId = GetUserId(ctx);
            if (userId == 0) return Results.Unauthorized();
            var eventType = body.GetProperty("event_type").GetString() ?? "";
            var channel = body.TryGetProperty("channel", out var ch) ? ch.GetString() ?? "toast" : "toast";
            var isEnabled = !body.TryGetProperty("is_enabled", out var en) || en.GetBoolean();
            var repo = new DbRepository(db.ConnectionString);
            await repo.UpsertNotificationPreferenceAsync(userId, eventType, channel, isEnabled);
            return Results.Ok();
        });

        // Get active sessions (admin only)
        group.MapGet("/sessions", async (DbConnectionFactory db) =>
        {
            var repo = new DbRepository(db.ConnectionString);
            return Results.Ok(await repo.GetActiveSessionsAsync());
        });

        // Force end a session (admin only)
        group.MapDelete("/sessions/{id:int}", async (int id, DbConnectionFactory db) =>
        {
            var repo = new DbRepository(db.ConnectionString);
            await repo.ForceEndSessionAsync(id);
            return Results.Ok();
        });

        return group;
    }

    private static int GetUserId(HttpContext ctx)
    {
        var claim = ctx.User.FindFirst(ClaimTypes.NameIdentifier) ?? ctx.User.FindFirst("sub");
        return claim != null && int.TryParse(claim.Value, out var id) ? id : 0;
    }
}
