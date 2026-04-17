using System.Text.Json;
using Npgsql;
using Central.Persistence;

namespace Central.Api.Endpoints;

/// <summary>Groups — dynamic, rule-based permission buckets (independent of teams).</summary>
public static class GroupEndpoints
{
    public static RouteGroupBuilder MapGroupEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", async (DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                @"SELECT g.id, g.name, g.description, g.group_type, g.rule_expression, g.is_active,
                         (SELECT COUNT(*) FROM group_members m WHERE m.group_id = g.id) as member_count
                  FROM user_groups g WHERE g.is_active = true ORDER BY g.name", conn);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        group.MapPost("/", async (JsonElement body, DbConnectionFactory db) =>
        {
            var name = body.GetProperty("name").GetString() ?? "";
            if (string.IsNullOrWhiteSpace(name)) return ApiProblem.ValidationError("Group name is required.");
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                @"INSERT INTO user_groups (name, description, group_type, rule_expression)
                  VALUES (@n, @d, @t, @r) RETURNING id", conn);
            cmd.Parameters.AddWithValue("n", name);
            cmd.Parameters.AddWithValue("d", body.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "");
            cmd.Parameters.AddWithValue("t", body.TryGetProperty("group_type", out var t) ? t.GetString() ?? "static" : "static");
            cmd.Parameters.AddWithValue("r", body.TryGetProperty("rule_expression", out var re) ? (object)(re.GetString() ?? "") : DBNull.Value);
            var id = (int)(await cmd.ExecuteScalarAsync())!;
            return Results.Created($"/api/groups/{id}", new { id });
        });

        group.MapPut("/{id:int}", async (int id, JsonElement body, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                @"UPDATE user_groups SET name = @n, description = @d, group_type = @t,
                  rule_expression = @r, updated_at = NOW() WHERE id = @id RETURNING id", conn);
            cmd.Parameters.AddWithValue("id", id);
            cmd.Parameters.AddWithValue("n", body.GetProperty("name").GetString() ?? "");
            cmd.Parameters.AddWithValue("d", body.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "");
            cmd.Parameters.AddWithValue("t", body.TryGetProperty("group_type", out var t) ? t.GetString() ?? "static" : "static");
            cmd.Parameters.AddWithValue("r", body.TryGetProperty("rule_expression", out var re) ? (object)(re.GetString() ?? "") : DBNull.Value);
            var result = await cmd.ExecuteScalarAsync();
            return result is null ? ApiProblem.NotFound($"Group {id} not found") : Results.Ok(new { id });
        });

        group.MapDelete("/{id:int}", async (int id, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand("UPDATE user_groups SET is_active = false WHERE id = @id RETURNING id", conn);
            cmd.Parameters.AddWithValue("id", id);
            var result = await cmd.ExecuteScalarAsync();
            return result is null ? ApiProblem.NotFound($"Group {id} not found") : Results.NoContent();
        });

        // Members
        group.MapGet("/{id:int}/members", async (int id, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                @"SELECT gm.id, gm.user_id, u.username, u.display_name, gm.auto_assigned, gm.added_at
                  FROM group_members gm JOIN app_users u ON u.id = gm.user_id
                  WHERE gm.group_id = @id ORDER BY u.display_name", conn);
            cmd.Parameters.AddWithValue("id", id);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        group.MapPost("/{id:int}/members", async (int id, JsonElement body, DbConnectionFactory db) =>
        {
            var userId = body.GetProperty("user_id").GetInt32();
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                @"INSERT INTO group_members (group_id, user_id) VALUES (@g, @u)
                  ON CONFLICT (group_id, user_id) DO NOTHING RETURNING id", conn);
            cmd.Parameters.AddWithValue("g", id);
            cmd.Parameters.AddWithValue("u", userId);
            var mid = await cmd.ExecuteScalarAsync();
            return Results.Ok(new { added = mid is not null });
        });

        group.MapDelete("/{gid:int}/members/{uid:int}", async (int gid, int uid, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand("DELETE FROM group_members WHERE group_id = @g AND user_id = @u RETURNING id", conn);
            cmd.Parameters.AddWithValue("g", gid);
            cmd.Parameters.AddWithValue("u", uid);
            var r = await cmd.ExecuteScalarAsync();
            return r is null ? ApiProblem.NotFound("Member not in group") : Results.NoContent();
        });

        // Permissions granted to group
        group.MapGet("/{id:int}/permissions", async (int id, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                "SELECT * FROM group_permissions WHERE group_id = @id ORDER BY permission_code", conn);
            cmd.Parameters.AddWithValue("id", id);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        group.MapPost("/{id:int}/permissions", async (int id, JsonElement body, DbConnectionFactory db) =>
        {
            var code = body.GetProperty("permission_code").GetString() ?? "";
            var granted = !body.TryGetProperty("is_granted", out var g) || g.GetBoolean();
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                @"INSERT INTO group_permissions (group_id, permission_code, is_granted) VALUES (@g, @c, @gr)
                  ON CONFLICT (group_id, permission_code) DO UPDATE SET is_granted = @gr RETURNING id", conn);
            cmd.Parameters.AddWithValue("g", id);
            cmd.Parameters.AddWithValue("c", code);
            cmd.Parameters.AddWithValue("gr", granted);
            var pid = (int)(await cmd.ExecuteScalarAsync())!;
            return Results.Ok(new { id = pid });
        });

        // Assignment rules
        group.MapGet("/{id:int}/rules", async (int id, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                "SELECT * FROM group_assignment_rules WHERE group_id = @id ORDER BY priority, rule_name", conn);
            cmd.Parameters.AddWithValue("id", id);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        group.MapPost("/{id:int}/rules", async (int id, JsonElement body, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                @"INSERT INTO group_assignment_rules (group_id, rule_name, rule_type, rule_value, priority)
                  VALUES (@g, @n, @t, @v, @p) RETURNING id", conn);
            cmd.Parameters.AddWithValue("g", id);
            cmd.Parameters.AddWithValue("n", body.GetProperty("rule_name").GetString() ?? "");
            cmd.Parameters.AddWithValue("t", body.GetProperty("rule_type").GetString() ?? "");
            cmd.Parameters.AddWithValue("v", body.GetProperty("rule_value").GetString() ?? "");
            cmd.Parameters.AddWithValue("p", body.TryGetProperty("priority", out var pr) ? pr.GetInt32() : 100);
            var rid = (int)(await cmd.ExecuteScalarAsync())!;
            return Results.Created($"/api/groups/{id}/rules/{rid}", new { id = rid });
        });

        // Resource access
        group.MapGet("/{id:int}/resources", async (int id, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                "SELECT * FROM group_resource_access WHERE group_id = @id ORDER BY resource_type, resource_id", conn);
            cmd.Parameters.AddWithValue("id", id);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        return group;
    }
}
