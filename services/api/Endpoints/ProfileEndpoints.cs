using System.Security.Claims;
using System.Text.Json;
using Npgsql;
using Central.Persistence;

namespace Central.Api.Endpoints;

/// <summary>Phase 5: User profile CRUD + Phase 11: Invitations + Phase 13: Role templates.</summary>
public static class ProfileEndpoints
{
    public static RouteGroupBuilder MapProfileEndpoints(this RouteGroupBuilder group)
    {
        // GET /api/profile — current user's profile
        group.MapGet("/", async (ClaimsPrincipal principal, DbConnectionFactory db) =>
        {
            var username = principal.Identity?.Name;
            if (string.IsNullOrEmpty(username)) return Results.Unauthorized();

            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                @"SELECT p.*, u.username, u.display_name, u.email, u.role,
                         COALESCE(m.display_name, '') as manager_name
                  FROM user_profiles p
                  JOIN app_users u ON u.id = p.user_id
                  LEFT JOIN app_users m ON m.id = p.manager_id
                  WHERE u.username = @u", conn);
            cmd.Parameters.AddWithValue("u", username);
            await using var r = await cmd.ExecuteReaderAsync();
            if (!await r.ReadAsync())
            {
                // Auto-create profile if missing
                await r.CloseAsync();
                await using var idCmd = new NpgsqlCommand("SELECT id FROM app_users WHERE username = @u", conn);
                idCmd.Parameters.AddWithValue("u", username);
                var userId = await idCmd.ExecuteScalarAsync();
                if (userId is int uid)
                {
                    await using var ins = new NpgsqlCommand("INSERT INTO user_profiles (user_id) VALUES (@uid) ON CONFLICT DO NOTHING", conn);
                    ins.Parameters.AddWithValue("uid", uid);
                    await ins.ExecuteNonQueryAsync();
                }
                return Results.Ok(new { username, message = "Profile created. Refresh to load." });
            }
            var row = new Dictionary<string, object?>();
            for (int i = 0; i < r.FieldCount; i++) row[r.GetName(i)] = r.IsDBNull(i) ? null : r.GetValue(i);
            return Results.Ok(row);
        });

        // PUT /api/profile — update current user's profile
        group.MapPut("/", async (ClaimsPrincipal principal, JsonElement body, DbConnectionFactory db) =>
        {
            var username = principal.Identity?.Name;
            if (string.IsNullOrEmpty(username)) return Results.Unauthorized();

            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                @"UPDATE user_profiles SET
                    bio = @bio, timezone = @tz, locale = @loc, date_format = @df, time_format = @tf,
                    linkedin_url = @li, github_url = @gh, phone_ext = @pe, office_location = @ol,
                    updated_at = NOW()
                  FROM app_users u WHERE u.id = user_profiles.user_id AND u.username = @u
                  RETURNING user_profiles.id", conn);
            cmd.Parameters.AddWithValue("u", username);
            cmd.Parameters.AddWithValue("bio", body.TryGetProperty("bio", out var bio) ? bio.GetString() ?? "" : "");
            cmd.Parameters.AddWithValue("tz", body.TryGetProperty("timezone", out var tz) ? tz.GetString() ?? "UTC" : "UTC");
            cmd.Parameters.AddWithValue("loc", body.TryGetProperty("locale", out var loc) ? loc.GetString() ?? "en-GB" : "en-GB");
            cmd.Parameters.AddWithValue("df", body.TryGetProperty("date_format", out var df) ? df.GetString() ?? "dd/MM/yyyy" : "dd/MM/yyyy");
            cmd.Parameters.AddWithValue("tf", body.TryGetProperty("time_format", out var tf) ? tf.GetString() ?? "HH:mm" : "HH:mm");
            cmd.Parameters.AddWithValue("li", body.TryGetProperty("linkedin_url", out var li) ? (object?)(li.GetString()) ?? DBNull.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("gh", body.TryGetProperty("github_url", out var gh) ? (object?)(gh.GetString()) ?? DBNull.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("pe", body.TryGetProperty("phone_ext", out var pe) ? (object?)(pe.GetString()) ?? DBNull.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("ol", body.TryGetProperty("office_location", out var ol) ? (object?)(ol.GetString()) ?? DBNull.Value : DBNull.Value);
            var result = await cmd.ExecuteScalarAsync();
            return result is null ? ApiProblem.NotFound("Profile not found") : Results.Ok(new { updated = true });
        });

        return group;
    }
}

/// <summary>Phase 11: User invitation endpoints.</summary>
public static class InvitationEndpoints
{
    public static RouteGroupBuilder MapInvitationEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", async (DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                @"SELECT i.*, COALESCE(u.display_name, '') as invited_by_name
                  FROM user_invitations i
                  LEFT JOIN app_users u ON u.id = i.invited_by
                  ORDER BY i.created_at DESC LIMIT 200", conn);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        group.MapPost("/", async (ClaimsPrincipal principal, JsonElement body, DbConnectionFactory db) =>
        {
            var email = body.GetProperty("email").GetString() ?? "";
            if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
                return ApiProblem.ValidationError("Valid email address is required.");

            var role = body.TryGetProperty("role", out var r) ? r.GetString() ?? "Viewer" : "Viewer";
            var message = body.TryGetProperty("message", out var m) ? m.GetString() ?? "" : "";
            var token = Guid.NewGuid().ToString("N");

            // Resolve inviter
            int? invitedBy = null;
            var username = principal.Identity?.Name;
            if (!string.IsNullOrEmpty(username))
            {
                await using var conn2 = await db.OpenConnectionAsync();
                await using var uc = new NpgsqlCommand("SELECT id FROM app_users WHERE username = @u", conn2);
                uc.Parameters.AddWithValue("u", username);
                var uid = await uc.ExecuteScalarAsync();
                if (uid is int id) invitedBy = id;
            }

            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                @"INSERT INTO user_invitations (email, role, invited_by, token, message)
                  VALUES (@e, @r, @ib, @t, @m) RETURNING id", conn);
            cmd.Parameters.AddWithValue("e", email);
            cmd.Parameters.AddWithValue("r", role);
            cmd.Parameters.AddWithValue("ib", invitedBy.HasValue ? invitedBy.Value : DBNull.Value);
            cmd.Parameters.AddWithValue("t", token);
            cmd.Parameters.AddWithValue("m", message);
            var newId = (int)(await cmd.ExecuteScalarAsync())!;

            return Results.Created($"/api/invitations/{newId}", new { id = newId, token, email, role });
        });

        group.MapDelete("/{id:int}", async (int id, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand("DELETE FROM user_invitations WHERE id = @id AND accepted_at IS NULL RETURNING id", conn);
            cmd.Parameters.AddWithValue("id", id);
            var result = await cmd.ExecuteScalarAsync();
            return result is null ? ApiProblem.NotFound("Invitation not found or already accepted") : Results.NoContent();
        });

        // POST /api/invitations/accept — accept an invitation (anonymous)
        group.MapPost("/accept", async (JsonElement body, DbConnectionFactory db) =>
        {
            var token = body.GetProperty("token").GetString() ?? "";
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                @"UPDATE user_invitations SET accepted_at = NOW()
                  WHERE token = @t AND accepted_at IS NULL AND expires_at > NOW()
                  RETURNING id, email, role", conn);
            cmd.Parameters.AddWithValue("t", token);
            await using var r = await cmd.ExecuteReaderAsync();
            if (!await r.ReadAsync()) return ApiProblem.NotFound("Invitation expired or already accepted.");
            return Results.Ok(new { id = r.GetInt32(0), email = r.GetString(1), role = r.GetString(2), accepted = true });
        }).AllowAnonymous();

        return group;
    }
}

/// <summary>Phase 13: Role template endpoints.</summary>
public static class RoleTemplateEndpoints
{
    public static RouteGroupBuilder MapRoleTemplateEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", async (DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                "SELECT * FROM role_templates ORDER BY is_system DESC, name", conn);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        group.MapPost("/", async (JsonElement body, DbConnectionFactory db) =>
        {
            var name = body.GetProperty("name").GetString() ?? "";
            if (string.IsNullOrWhiteSpace(name)) return ApiProblem.ValidationError("Template name is required.");

            var codes = new List<string>();
            if (body.TryGetProperty("permission_codes", out var arr) && arr.ValueKind == JsonValueKind.Array)
                foreach (var item in arr.EnumerateArray())
                    if (item.GetString() is string s) codes.Add(s);

            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                @"INSERT INTO role_templates (name, description, permission_codes)
                  VALUES (@n, @d, @codes) RETURNING id", conn);
            cmd.Parameters.AddWithValue("n", name);
            cmd.Parameters.AddWithValue("d", body.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "");
            cmd.Parameters.AddWithValue("codes", codes.ToArray());
            var id = (int)(await cmd.ExecuteScalarAsync())!;
            return Results.Created($"/api/role-templates/{id}", new { id });
        });

        group.MapDelete("/{id:int}", async (int id, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand("DELETE FROM role_templates WHERE id = @id AND is_system = false RETURNING id", conn);
            cmd.Parameters.AddWithValue("id", id);
            var result = await cmd.ExecuteScalarAsync();
            return result is null ? ApiProblem.NotFound("Template not found or is a system template") : Results.NoContent();
        });

        return group;
    }
}
