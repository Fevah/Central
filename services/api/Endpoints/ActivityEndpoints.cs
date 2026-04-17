using System.Security.Claims;
using Npgsql;
using Central.Persistence;

namespace Central.Api.Endpoints;

/// <summary>
/// Activity timeline endpoints — per-user and global activity feeds.
/// Combines: audit log, auth events, sync log, webhook log into a unified timeline.
/// </summary>
public static class ActivityEndpoints
{
    public static RouteGroupBuilder MapActivityEndpoints(this RouteGroupBuilder group)
    {
        // Global activity feed (admin)
        group.MapGet("/global", async (DbConnectionFactory db, int? limit) =>
        {
            var max = Math.Min(limit ?? 50, 200);
            await using var conn = new NpgsqlConnection(db.ConnectionString);
            await conn.OpenAsync();

            var items = new List<object>();

            // Audit log
            try
            {
                await using var cmd = new NpgsqlCommand(
                    $"SELECT created_at, action, entity_type, entity_name, username FROM audit_log ORDER BY created_at DESC LIMIT {max}", conn);
                await using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                    items.Add(new
                    {
                        time = r.GetDateTime(0),
                        source = "audit",
                        action = r.GetString(1),
                        entity = r.IsDBNull(2) ? "" : r.GetString(2),
                        name = r.IsDBNull(3) ? "" : r.GetString(3),
                        user = r.IsDBNull(4) ? "" : r.GetString(4)
                    });
            }
            catch { }

            // Auth events
            try
            {
                await using var cmd = new NpgsqlCommand(
                    $"SELECT timestamp, event_type, provider_type, username, success FROM auth_events ORDER BY timestamp DESC LIMIT {max}", conn);
                await using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                    items.Add(new
                    {
                        time = r.GetDateTime(0),
                        source = "auth",
                        action = r.GetString(1),
                        entity = r.IsDBNull(2) ? "" : r.GetString(2),
                        name = r.IsDBNull(3) ? "" : r.GetString(3),
                        user = r.IsDBNull(3) ? "" : r.GetString(3)
                    });
            }
            catch { }

            // Sort combined and take limit
            var sorted = items
                .OrderByDescending(i => ((dynamic)i).time)
                .Take(max);

            return Results.Ok(sorted);
        });

        // My activity feed (current user)
        group.MapGet("/me", async (HttpContext ctx, DbConnectionFactory db, int? limit) =>
        {
            var username = ctx.User.Identity?.Name ?? "";
            var max = Math.Min(limit ?? 30, 100);

            await using var conn = new NpgsqlConnection(db.ConnectionString);
            await conn.OpenAsync();

            var items = new List<object>();
            try
            {
                await using var cmd = new NpgsqlCommand(
                    $"SELECT created_at, action, entity_type, entity_name FROM audit_log WHERE username = @u ORDER BY created_at DESC LIMIT {max}", conn);
                cmd.Parameters.AddWithValue("u", username);
                await using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                    items.Add(new
                    {
                        time = r.GetDateTime(0),
                        action = r.GetString(1),
                        entity = r.IsDBNull(2) ? "" : r.GetString(2),
                        name = r.IsDBNull(3) ? "" : r.GetString(3)
                    });
            }
            catch { }

            return Results.Ok(items);
        });

        return group;
    }
}
