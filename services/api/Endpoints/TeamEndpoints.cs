using System.Text.Json;
using Npgsql;
using Central.Data;

namespace Central.Api.Endpoints;

/// <summary>Phases 3, 12: Departments, teams, and team membership CRUD.</summary>
public static class TeamEndpoints
{
    public static RouteGroupBuilder MapTeamEndpoints(this RouteGroupBuilder group)
    {
        // ── Departments ──

        group.MapGet("/departments", async (DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(@"
                SELECT d.id, d.name, d.parent_id, p.name as parent_name,
                       d.head_user_id, COALESCE(u.display_name, '') as head_user_name,
                       d.cost_center, d.description, d.is_active,
                       (SELECT COUNT(*) FROM app_users au WHERE au.department_id = d.id) as member_count
                FROM departments d
                LEFT JOIN departments p ON p.id = d.parent_id
                LEFT JOIN app_users u ON u.id = d.head_user_id
                WHERE d.is_active = true
                ORDER BY d.name", conn);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        group.MapPost("/departments", async (JsonElement body, DbConnectionFactory db) =>
        {
            var name = body.GetProperty("name").GetString() ?? "";
            if (string.IsNullOrWhiteSpace(name)) return ApiProblem.ValidationError("Department name is required.");

            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                @"INSERT INTO departments (name, parent_id, head_user_id, cost_center, description)
                  VALUES (@n, @pid, @hid, @cc, @desc) RETURNING id", conn);
            cmd.Parameters.AddWithValue("n", name);
            cmd.Parameters.AddWithValue("pid", body.TryGetProperty("parent_id", out var pid) && pid.ValueKind != JsonValueKind.Null ? pid.GetInt32() : DBNull.Value);
            cmd.Parameters.AddWithValue("hid", body.TryGetProperty("head_user_id", out var hid) && hid.ValueKind != JsonValueKind.Null ? hid.GetInt32() : DBNull.Value);
            cmd.Parameters.AddWithValue("cc", body.TryGetProperty("cost_center", out var cc) ? cc.GetString() ?? "" : "");
            cmd.Parameters.AddWithValue("desc", body.TryGetProperty("description", out var desc) ? desc.GetString() ?? "" : "");
            var id = (int)(await cmd.ExecuteScalarAsync())!;
            return Results.Created($"/api/teams/departments/{id}", new { id });
        });

        group.MapPut("/departments/{id:int}", async (int id, JsonElement body, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                @"UPDATE departments SET name = @n, parent_id = @pid, head_user_id = @hid,
                  cost_center = @cc, description = @desc, updated_at = NOW()
                  WHERE id = @id RETURNING id", conn);
            cmd.Parameters.AddWithValue("id", id);
            cmd.Parameters.AddWithValue("n", body.GetProperty("name").GetString() ?? "");
            cmd.Parameters.AddWithValue("pid", body.TryGetProperty("parent_id", out var pid) && pid.ValueKind != JsonValueKind.Null ? pid.GetInt32() : DBNull.Value);
            cmd.Parameters.AddWithValue("hid", body.TryGetProperty("head_user_id", out var hid) && hid.ValueKind != JsonValueKind.Null ? hid.GetInt32() : DBNull.Value);
            cmd.Parameters.AddWithValue("cc", body.TryGetProperty("cost_center", out var cc) ? cc.GetString() ?? "" : "");
            cmd.Parameters.AddWithValue("desc", body.TryGetProperty("description", out var desc) ? desc.GetString() ?? "" : "");
            var result = await cmd.ExecuteScalarAsync();
            return result is null ? ApiProblem.NotFound($"Department {id} not found") : Results.Ok(new { id });
        });

        group.MapDelete("/departments/{id:int}", async (int id, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand("UPDATE departments SET is_active = false WHERE id = @id RETURNING id", conn);
            cmd.Parameters.AddWithValue("id", id);
            var result = await cmd.ExecuteScalarAsync();
            return result is null ? ApiProblem.NotFound($"Department {id} not found") : Results.NoContent();
        });

        // ── Teams ──

        group.MapGet("/", async (int? department_id, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            var where = department_id.HasValue ? "AND t.department_id = @did" : "";
            await using var cmd = new NpgsqlCommand($@"
                SELECT t.id, t.name, t.description, t.department_id,
                       COALESCE(d.name, '') as department_name,
                       t.team_lead_id, COALESCE(u.display_name, '') as team_lead_name,
                       t.is_active,
                       (SELECT COUNT(*) FROM team_members tm WHERE tm.team_id = t.id) as member_count
                FROM teams t
                LEFT JOIN departments d ON d.id = t.department_id
                LEFT JOIN app_users u ON u.id = t.team_lead_id
                WHERE t.is_active = true {where}
                ORDER BY t.name", conn);
            if (department_id.HasValue) cmd.Parameters.AddWithValue("did", department_id.Value);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        group.MapPost("/", async (JsonElement body, DbConnectionFactory db) =>
        {
            var name = body.GetProperty("name").GetString() ?? "";
            if (string.IsNullOrWhiteSpace(name)) return ApiProblem.ValidationError("Team name is required.");

            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                @"INSERT INTO teams (name, description, department_id, team_lead_id)
                  VALUES (@n, @desc, @did, @lid) RETURNING id", conn);
            cmd.Parameters.AddWithValue("n", name);
            cmd.Parameters.AddWithValue("desc", body.TryGetProperty("description", out var desc) ? desc.GetString() ?? "" : "");
            cmd.Parameters.AddWithValue("did", body.TryGetProperty("department_id", out var did) && did.ValueKind != JsonValueKind.Null ? did.GetInt32() : DBNull.Value);
            cmd.Parameters.AddWithValue("lid", body.TryGetProperty("team_lead_id", out var lid) && lid.ValueKind != JsonValueKind.Null ? lid.GetInt32() : DBNull.Value);
            var id = (int)(await cmd.ExecuteScalarAsync())!;
            return Results.Created($"/api/teams/{id}", new { id });
        });

        group.MapPut("/{id:int}", async (int id, JsonElement body, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                @"UPDATE teams SET name = @n, description = @desc, department_id = @did,
                  team_lead_id = @lid, updated_at = NOW() WHERE id = @id RETURNING id", conn);
            cmd.Parameters.AddWithValue("id", id);
            cmd.Parameters.AddWithValue("n", body.GetProperty("name").GetString() ?? "");
            cmd.Parameters.AddWithValue("desc", body.TryGetProperty("description", out var desc) ? desc.GetString() ?? "" : "");
            cmd.Parameters.AddWithValue("did", body.TryGetProperty("department_id", out var did) && did.ValueKind != JsonValueKind.Null ? did.GetInt32() : DBNull.Value);
            cmd.Parameters.AddWithValue("lid", body.TryGetProperty("team_lead_id", out var lid) && lid.ValueKind != JsonValueKind.Null ? lid.GetInt32() : DBNull.Value);
            var result = await cmd.ExecuteScalarAsync();
            return result is null ? ApiProblem.NotFound($"Team {id} not found") : Results.Ok(new { id });
        });

        group.MapDelete("/{id:int}", async (int id, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand("UPDATE teams SET is_active = false WHERE id = @id RETURNING id", conn);
            cmd.Parameters.AddWithValue("id", id);
            var result = await cmd.ExecuteScalarAsync();
            return result is null ? ApiProblem.NotFound($"Team {id} not found") : Results.NoContent();
        });

        // ── Team Members ──

        group.MapGet("/{id:int}/members", async (int id, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                @"SELECT tm.id, tm.team_id, tm.user_id, tm.role_in_team, tm.joined_at,
                         u.username, u.display_name
                  FROM team_members tm JOIN app_users u ON u.id = tm.user_id
                  WHERE tm.team_id = @id ORDER BY u.display_name", conn);
            cmd.Parameters.AddWithValue("id", id);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        group.MapPost("/{id:int}/members", async (int id, JsonElement body, DbConnectionFactory db) =>
        {
            var userId = body.GetProperty("user_id").GetInt32();
            var role = body.TryGetProperty("role_in_team", out var r) ? r.GetString() ?? "member" : "member";

            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                @"INSERT INTO team_members (team_id, user_id, role_in_team)
                  VALUES (@tid, @uid, @role)
                  ON CONFLICT (team_id, user_id) DO UPDATE SET role_in_team = @role
                  RETURNING id", conn);
            cmd.Parameters.AddWithValue("tid", id);
            cmd.Parameters.AddWithValue("uid", userId);
            cmd.Parameters.AddWithValue("role", role);
            var memberId = (int)(await cmd.ExecuteScalarAsync())!;
            return Results.Ok(new { id = memberId });
        });

        group.MapDelete("/{teamId:int}/members/{userId:int}", async (int teamId, int userId, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                "DELETE FROM team_members WHERE team_id = @tid AND user_id = @uid RETURNING id", conn);
            cmd.Parameters.AddWithValue("tid", teamId);
            cmd.Parameters.AddWithValue("uid", userId);
            var result = await cmd.ExecuteScalarAsync();
            return result is null ? ApiProblem.NotFound("Member not found") : Results.NoContent();
        });

        return group;
    }
}
