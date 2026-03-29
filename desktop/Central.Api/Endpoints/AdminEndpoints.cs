using System.Text.Json;
using Npgsql;
using Central.Data;

namespace Central.Api.Endpoints;

public static class AdminEndpoints
{
    public static RouteGroupBuilder MapAdminEndpoints(this RouteGroupBuilder group)
    {
        // ── Users ──────────────────────────────────────────────────────

        group.MapGet("/users", async (DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM app_users ORDER BY username";
            await using var reader = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(reader));
        });

        group.MapPut("/users", async (DbConnectionFactory db, JsonElement body) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            var id = body.TryGetProperty("id", out var idProp) ? idProp.GetInt32() : 0;
            var username = body.GetProperty("username").GetString() ?? "";
            var displayName = body.TryGetProperty("display_name", out var dn) ? dn.GetString() ?? "" : "";
            var role = body.TryGetProperty("role", out var r) ? r.GetString() ?? "Viewer" : "Viewer";
            var email = body.TryGetProperty("email", out var em) ? em.GetString() ?? "" : "";
            var isActive = !body.TryGetProperty("is_active", out var ia) || ia.GetBoolean();

            await using var cmd = new NpgsqlCommand(id > 0
                ? "UPDATE app_users SET username=@u, display_name=@dn, role=@r, email=@em, is_active=@ia, updated_at=NOW() WHERE id=@id"
                : "INSERT INTO app_users (username, display_name, role, email, is_active) VALUES (@u, @dn, @r, @em, @ia) RETURNING id", conn);
            if (id > 0) cmd.Parameters.AddWithValue("id", id);
            cmd.Parameters.AddWithValue("u", username);
            cmd.Parameters.AddWithValue("dn", displayName);
            cmd.Parameters.AddWithValue("r", role);
            cmd.Parameters.AddWithValue("em", email);
            cmd.Parameters.AddWithValue("ia", isActive);
            if (id == 0) id = (int)(await cmd.ExecuteScalarAsync())!;
            else await cmd.ExecuteNonQueryAsync();
            return Results.Ok(new { id });
        });

        group.MapDelete("/users/{id:int}", async (int id, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand("DELETE FROM app_users WHERE id=@id", conn);
            cmd.Parameters.AddWithValue("id", id);
            await cmd.ExecuteNonQueryAsync();
            return Results.Ok();
        });

        // ── Roles ──────────────────────────────────────────────────────

        group.MapGet("/roles", async (DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM roles ORDER BY name";
            await using var reader = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(reader));
        });

        group.MapPut("/roles", async (DbConnectionFactory db, JsonElement body) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            var id = body.TryGetProperty("id", out var idProp) ? idProp.GetInt32() : 0;
            var name = body.GetProperty("name").GetString() ?? "";
            var desc = body.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "";
            var priority = body.TryGetProperty("priority", out var p) ? p.GetInt32() : 10;

            await using var cmd = new NpgsqlCommand(id > 0
                ? "UPDATE roles SET name=@n, description=@d, priority=@p, updated_at=NOW() WHERE id=@id"
                : "INSERT INTO roles (name, description, priority) VALUES (@n, @d, @p) RETURNING id", conn);
            if (id > 0) cmd.Parameters.AddWithValue("id", id);
            cmd.Parameters.AddWithValue("n", name);
            cmd.Parameters.AddWithValue("d", desc);
            cmd.Parameters.AddWithValue("p", priority);
            if (id == 0) id = (int)(await cmd.ExecuteScalarAsync())!;
            else await cmd.ExecuteNonQueryAsync();
            return Results.Ok(new { id });
        });

        group.MapDelete("/roles/{id:int}", async (int id, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand("DELETE FROM roles WHERE id=@id AND name NOT IN ('Admin')", conn);
            cmd.Parameters.AddWithValue("id", id);
            await cmd.ExecuteNonQueryAsync();
            return Results.Ok();
        });

        // ── Lookups ────────────────────────────────────────────────────

        group.MapGet("/lookups", async (DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM lookup_values ORDER BY category, sort_order";
            await using var reader = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(reader));
        });

        group.MapPut("/lookups", async (DbConnectionFactory db, JsonElement body) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            var id = body.TryGetProperty("id", out var idProp) ? idProp.GetInt32() : 0;
            var category = body.GetProperty("category").GetString() ?? "";
            var value = body.GetProperty("value").GetString() ?? "";
            var sortOrder = body.TryGetProperty("sort_order", out var so) ? so.GetInt32() : 0;

            await using var cmd = new NpgsqlCommand(id > 0
                ? "UPDATE lookup_values SET category=@c, value=@v, sort_order=@s WHERE id=@id"
                : "INSERT INTO lookup_values (category, value, sort_order) VALUES (@c, @v, @s) RETURNING id", conn);
            if (id > 0) cmd.Parameters.AddWithValue("id", id);
            cmd.Parameters.AddWithValue("c", category);
            cmd.Parameters.AddWithValue("v", value);
            cmd.Parameters.AddWithValue("s", sortOrder);
            if (id == 0) id = (int)(await cmd.ExecuteScalarAsync())!;
            else await cmd.ExecuteNonQueryAsync();
            return Results.Ok(new { id });
        });

        group.MapDelete("/lookups/{id:int}", async (int id, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand("DELETE FROM lookup_values WHERE id=@id", conn);
            cmd.Parameters.AddWithValue("id", id);
            await cmd.ExecuteNonQueryAsync();
            return Results.Ok();
        });

        // ── Settings ───────────────────────────────────────────────────

        group.MapGet("/settings/{userId:int}/{key}", async (int userId, string key, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand("SELECT setting_value FROM user_settings WHERE user_id=@u AND setting_key=@k", conn);
            cmd.Parameters.AddWithValue("u", userId);
            cmd.Parameters.AddWithValue("k", key);
            var result = await cmd.ExecuteScalarAsync();
            return result != null ? Results.Ok(new { value = result.ToString() }) : Results.NotFound();
        });

        group.MapPut("/settings/{userId:int}/{key}", async (int userId, string key, DbConnectionFactory db, JsonElement body) =>
        {
            var value = body.GetProperty("value").GetString() ?? "";
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                @"INSERT INTO user_settings (user_id, setting_key, setting_value) VALUES (@u, @k, @v)
                  ON CONFLICT (user_id, setting_key) DO UPDATE SET setting_value=@v, updated_at=NOW()", conn);
            cmd.Parameters.AddWithValue("u", userId);
            cmd.Parameters.AddWithValue("k", key);
            cmd.Parameters.AddWithValue("v", value);
            await cmd.ExecuteNonQueryAsync();
            return Results.Ok();
        });

        // ── Audit ──────────────────────────────────────────────────────

        group.MapGet("/audit", async (DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM switch_audit_log ORDER BY created_at DESC LIMIT 100";
            await using var reader = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(reader));
        });

        return group;
    }
}
