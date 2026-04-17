using Npgsql;
using Central.Persistence;

namespace Central.Api.Endpoints;

public static class ProjectEndpoints
{
    public static RouteGroupBuilder MapProjectEndpoints(this RouteGroupBuilder group)
    {
        // ── Portfolios ──

        group.MapGet("/portfolios", async (DbConnectionFactory db) =>
        {
            await using var conn = new NpgsqlConnection(db.ConnectionString);
            await conn.OpenAsync();
            var list = new List<object>();
            await using var cmd = new NpgsqlCommand(@"
                SELECT p.id, p.name, p.description, p.owner_id, COALESCE(u.display_name,''), p.archived
                FROM portfolios p LEFT JOIN app_users u ON u.id = p.owner_id ORDER BY p.name", conn);
            await using var rdr = await cmd.ExecuteReaderAsync();
            while (await rdr.ReadAsync())
                list.Add(new { id = rdr.GetInt32(0), name = rdr.GetString(1), description = rdr.IsDBNull(2) ? "" : rdr.GetString(2),
                    owner_id = rdr.IsDBNull(3) ? (int?)null : rdr.GetInt32(3), owner_name = rdr.GetString(4), archived = rdr.GetBoolean(5) });
            return Results.Ok(list);
        });

        group.MapPost("/portfolios", async (PortfolioReq req, DbConnectionFactory db) =>
        {
            await using var conn = new NpgsqlConnection(db.ConnectionString);
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand(
                "INSERT INTO portfolios (name, description, owner_id) VALUES (@n, @d, @o) RETURNING id", conn);
            cmd.Parameters.AddWithValue("n", req.Name ?? "");
            cmd.Parameters.AddWithValue("d", (object?)req.Description ?? DBNull.Value);
            cmd.Parameters.AddWithValue("o", (object?)req.OwnerId ?? DBNull.Value);
            var id = (int)(await cmd.ExecuteScalarAsync())!;
            return Results.Created($"/api/projects/portfolios/{id}", new { id });
        });

        group.MapPut("/portfolios/{id}", async (int id, PortfolioReq req, DbConnectionFactory db) =>
        {
            await using var conn = new NpgsqlConnection(db.ConnectionString);
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand(
                "UPDATE portfolios SET name=@n, description=@d, owner_id=@o, archived=@a, updated_at=now() WHERE id=@id", conn);
            cmd.Parameters.AddWithValue("id", id);
            cmd.Parameters.AddWithValue("n", req.Name ?? "");
            cmd.Parameters.AddWithValue("d", (object?)req.Description ?? DBNull.Value);
            cmd.Parameters.AddWithValue("o", (object?)req.OwnerId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("a", req.Archived ?? false);
            await cmd.ExecuteNonQueryAsync();
            return Results.Ok();
        });

        group.MapDelete("/portfolios/{id}", async (int id, DbConnectionFactory db) =>
        {
            await using var conn = new NpgsqlConnection(db.ConnectionString);
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand("DELETE FROM portfolios WHERE id=@id", conn);
            cmd.Parameters.AddWithValue("id", id);
            await cmd.ExecuteNonQueryAsync();
            return Results.Ok();
        });

        // ── Programmes ──

        group.MapGet("/programmes", async (DbConnectionFactory db) =>
        {
            await using var conn = new NpgsqlConnection(db.ConnectionString);
            await conn.OpenAsync();
            var list = new List<object>();
            await using var cmd = new NpgsqlCommand(@"
                SELECT p.id, p.portfolio_id, p.name, p.description, p.owner_id, COALESCE(u.display_name,'')
                FROM programmes p LEFT JOIN app_users u ON u.id = p.owner_id ORDER BY p.name", conn);
            await using var rdr = await cmd.ExecuteReaderAsync();
            while (await rdr.ReadAsync())
                list.Add(new { id = rdr.GetInt32(0), portfolio_id = rdr.IsDBNull(1) ? (int?)null : rdr.GetInt32(1),
                    name = rdr.GetString(2), description = rdr.IsDBNull(3) ? "" : rdr.GetString(3),
                    owner_id = rdr.IsDBNull(4) ? (int?)null : rdr.GetInt32(4), owner_name = rdr.GetString(5) });
            return Results.Ok(list);
        });

        group.MapPost("/programmes", async (ProgrammeReq req, DbConnectionFactory db) =>
        {
            await using var conn = new NpgsqlConnection(db.ConnectionString);
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand(
                "INSERT INTO programmes (portfolio_id, name, description, owner_id) VALUES (@p, @n, @d, @o) RETURNING id", conn);
            cmd.Parameters.AddWithValue("p", (object?)req.PortfolioId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("n", req.Name ?? "");
            cmd.Parameters.AddWithValue("d", (object?)req.Description ?? DBNull.Value);
            cmd.Parameters.AddWithValue("o", (object?)req.OwnerId ?? DBNull.Value);
            var id = (int)(await cmd.ExecuteScalarAsync())!;
            return Results.Created($"/api/projects/programmes/{id}", new { id });
        });

        group.MapDelete("/programmes/{id}", async (int id, DbConnectionFactory db) =>
        {
            await using var conn = new NpgsqlConnection(db.ConnectionString);
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand("DELETE FROM programmes WHERE id=@id", conn);
            cmd.Parameters.AddWithValue("id", id);
            await cmd.ExecuteNonQueryAsync();
            return Results.Ok();
        });

        // ── Projects ──

        group.MapGet("/", async (DbConnectionFactory db) =>
        {
            await using var conn = new NpgsqlConnection(db.ConnectionString);
            await conn.OpenAsync();
            var list = new List<object>();
            await using var cmd = new NpgsqlCommand(@"
                SELECT id, programme_id, name, description, scheduling_method, default_mode,
                       method_template, calendar, archived
                FROM task_projects ORDER BY name", conn);
            await using var rdr = await cmd.ExecuteReaderAsync();
            while (await rdr.ReadAsync())
                list.Add(new { id = rdr.GetInt32(0), programme_id = rdr.IsDBNull(1) ? (int?)null : rdr.GetInt32(1),
                    name = rdr.GetString(2), description = rdr.IsDBNull(3) ? "" : rdr.GetString(3),
                    scheduling_method = rdr.GetString(4), default_mode = rdr.GetString(5),
                    method_template = rdr.GetString(6), calendar = rdr.IsDBNull(7) ? "" : rdr.GetString(7),
                    archived = rdr.GetBoolean(8) });
            return Results.Ok(list);
        });

        group.MapPost("/", async (ProjectReq req, DbConnectionFactory db) =>
        {
            await using var conn = new NpgsqlConnection(db.ConnectionString);
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand(@"
                INSERT INTO task_projects (programme_id, name, description, scheduling_method, default_mode, method_template, calendar)
                VALUES (@prog, @n, @d, @sm, @dm, @mt, @cal) RETURNING id", conn);
            cmd.Parameters.AddWithValue("prog", (object?)req.ProgrammeId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("n", req.Name ?? "");
            cmd.Parameters.AddWithValue("d", (object?)req.Description ?? DBNull.Value);
            cmd.Parameters.AddWithValue("sm", req.SchedulingMethod ?? "FixedDuration");
            cmd.Parameters.AddWithValue("dm", req.DefaultMode ?? "Agile");
            cmd.Parameters.AddWithValue("mt", req.MethodTemplate ?? "Scrum");
            cmd.Parameters.AddWithValue("cal", (object?)req.Calendar ?? DBNull.Value);
            var id = (int)(await cmd.ExecuteScalarAsync())!;
            return Results.Created($"/api/projects/{id}", new { id });
        });

        group.MapDelete("/{id}", async (int id, DbConnectionFactory db) =>
        {
            await using var conn = new NpgsqlConnection(db.ConnectionString);
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand("DELETE FROM task_projects WHERE id=@id", conn);
            cmd.Parameters.AddWithValue("id", id);
            await cmd.ExecuteNonQueryAsync();
            return Results.Ok();
        });

        // ── Sprints ──

        group.MapGet("/{projectId}/sprints", async (int projectId, DbConnectionFactory db) =>
        {
            await using var conn = new NpgsqlConnection(db.ConnectionString);
            await conn.OpenAsync();
            var list = new List<object>();
            await using var cmd = new NpgsqlCommand(@"
                SELECT id, name, start_date, end_date, goal, status, velocity_points, velocity_hours
                FROM sprints WHERE project_id=@pid ORDER BY start_date DESC, name", conn);
            cmd.Parameters.AddWithValue("pid", projectId);
            await using var rdr = await cmd.ExecuteReaderAsync();
            while (await rdr.ReadAsync())
                list.Add(new { id = rdr.GetInt32(0), name = rdr.GetString(1),
                    start_date = rdr.IsDBNull(2) ? null : rdr.GetDateTime(2).ToString("yyyy-MM-dd"),
                    end_date = rdr.IsDBNull(3) ? null : rdr.GetDateTime(3).ToString("yyyy-MM-dd"),
                    goal = rdr.IsDBNull(4) ? "" : rdr.GetString(4), status = rdr.GetString(5),
                    velocity_points = rdr.IsDBNull(6) ? (decimal?)null : rdr.GetDecimal(6),
                    velocity_hours = rdr.IsDBNull(7) ? (decimal?)null : rdr.GetDecimal(7) });
            return Results.Ok(list);
        });

        group.MapPost("/{projectId}/sprints", async (int projectId, SprintReq req, DbConnectionFactory db) =>
        {
            await using var conn = new NpgsqlConnection(db.ConnectionString);
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand(@"
                INSERT INTO sprints (project_id, name, start_date, end_date, goal, status)
                VALUES (@pid, @n, @s, @e, @g, @st) RETURNING id", conn);
            cmd.Parameters.AddWithValue("pid", projectId);
            cmd.Parameters.AddWithValue("n", req.Name ?? "");
            cmd.Parameters.AddWithValue("s", (object?)req.StartDate ?? DBNull.Value);
            cmd.Parameters.AddWithValue("e", (object?)req.EndDate ?? DBNull.Value);
            cmd.Parameters.AddWithValue("g", (object?)req.Goal ?? DBNull.Value);
            cmd.Parameters.AddWithValue("st", req.Status ?? "Planning");
            var id = (int)(await cmd.ExecuteScalarAsync())!;
            return Results.Created($"/api/projects/{projectId}/sprints/{id}", new { id });
        });

        group.MapDelete("/{projectId}/sprints/{id}", async (int projectId, int id, DbConnectionFactory db) =>
        {
            await using var conn = new NpgsqlConnection(db.ConnectionString);
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand("DELETE FROM sprints WHERE id=@id AND project_id=@pid", conn);
            cmd.Parameters.AddWithValue("id", id);
            cmd.Parameters.AddWithValue("pid", projectId);
            await cmd.ExecuteNonQueryAsync();
            return Results.Ok();
        });

        // Update sprint dates / goal / status. Velocity is recomputed when
        // tasks are committed/completed; not editable directly here.
        group.MapPut("/{projectId}/sprints/{id}", async (int projectId, int id, SprintReq req, DbConnectionFactory db) =>
        {
            await using var conn = new NpgsqlConnection(db.ConnectionString);
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand(@"
                UPDATE sprints
                SET name=@n, start_date=@s, end_date=@e, goal=@g, status=@st
                WHERE id=@id AND project_id=@pid RETURNING id", conn);
            cmd.Parameters.AddWithValue("id", id);
            cmd.Parameters.AddWithValue("pid", projectId);
            cmd.Parameters.AddWithValue("n", req.Name ?? "");
            cmd.Parameters.AddWithValue("s", (object?)req.StartDate ?? DBNull.Value);
            cmd.Parameters.AddWithValue("e", (object?)req.EndDate ?? DBNull.Value);
            cmd.Parameters.AddWithValue("g", (object?)req.Goal ?? DBNull.Value);
            cmd.Parameters.AddWithValue("st", req.Status ?? "Planning");
            var result = await cmd.ExecuteScalarAsync();
            return result is null ? Results.NotFound() : Results.Ok(new { id });
        });

        // Burndown snapshot history. Returns one row per day the snapshot
        // function ran. The Tasks module's WPF panel renders this as a
        // DxChart with "ideal" vs "actual" lines; the web client mirrors that.
        group.MapGet("/{projectId}/sprints/{id}/burndown", async (int projectId, int id, DbConnectionFactory db) =>
        {
            await using var conn = new NpgsqlConnection(db.ConnectionString);
            await conn.OpenAsync();
            var list = new List<object>();
            await using var cmd = new NpgsqlCommand(@"
                SELECT snapshot_date, points_remaining, hours_remaining,
                       points_completed, hours_completed
                FROM sprint_burndown
                WHERE sprint_id=@sid
                ORDER BY snapshot_date", conn);
            cmd.Parameters.AddWithValue("sid", id);
            await using var rdr = await cmd.ExecuteReaderAsync();
            while (await rdr.ReadAsync())
                list.Add(new {
                    snapshot_date    = rdr.GetDateTime(0).ToString("yyyy-MM-dd"),
                    points_remaining = rdr.GetDecimal(1),
                    hours_remaining  = rdr.GetDecimal(2),
                    points_completed = rdr.GetDecimal(3),
                    hours_completed  = rdr.GetDecimal(4),
                });
            return Results.Ok(list);
        });

        // Force a fresh burndown snapshot for today (idempotent — uses the
        // SQL function's ON CONFLICT to overwrite today's row).
        group.MapPost("/{projectId}/sprints/{id}/burndown/snapshot", async (int projectId, int id, DbConnectionFactory db) =>
        {
            await using var conn = new NpgsqlConnection(db.ConnectionString);
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand("SELECT snapshot_sprint_burndown(@sid)", conn);
            cmd.Parameters.AddWithValue("sid", id);
            await cmd.ExecuteNonQueryAsync();
            return Results.Ok(new { snapshotted = true });
        });

        // ── Releases ──

        group.MapGet("/{projectId}/releases", async (int projectId, DbConnectionFactory db) =>
        {
            await using var conn = new NpgsqlConnection(db.ConnectionString);
            await conn.OpenAsync();
            var list = new List<object>();
            await using var cmd = new NpgsqlCommand(@"
                SELECT id, name, target_date, description, status
                FROM releases WHERE project_id=@pid ORDER BY target_date, name", conn);
            cmd.Parameters.AddWithValue("pid", projectId);
            await using var rdr = await cmd.ExecuteReaderAsync();
            while (await rdr.ReadAsync())
                list.Add(new { id = rdr.GetInt32(0), name = rdr.GetString(1),
                    target_date = rdr.IsDBNull(2) ? null : rdr.GetDateTime(2).ToString("yyyy-MM-dd"),
                    description = rdr.IsDBNull(3) ? "" : rdr.GetString(3), status = rdr.GetString(4) });
            return Results.Ok(list);
        });

        group.MapPost("/{projectId}/releases", async (int projectId, ReleaseReq req, DbConnectionFactory db) =>
        {
            await using var conn = new NpgsqlConnection(db.ConnectionString);
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand(@"
                INSERT INTO releases (project_id, name, target_date, description, status)
                VALUES (@pid, @n, @d, @desc, @st) RETURNING id", conn);
            cmd.Parameters.AddWithValue("pid", projectId);
            cmd.Parameters.AddWithValue("n", req.Name ?? "");
            cmd.Parameters.AddWithValue("d", (object?)req.TargetDate ?? DBNull.Value);
            cmd.Parameters.AddWithValue("desc", (object?)req.Description ?? DBNull.Value);
            cmd.Parameters.AddWithValue("st", req.Status ?? "Planned");
            var id = (int)(await cmd.ExecuteScalarAsync())!;
            return Results.Created($"/api/projects/{projectId}/releases/{id}", new { id });
        });

        return group;
    }

    private record PortfolioReq(string? Name, string? Description, int? OwnerId, bool? Archived);
    private record ProgrammeReq(int? PortfolioId, string? Name, string? Description, int? OwnerId);
    private record ProjectReq(int? ProgrammeId, string? Name, string? Description, string? SchedulingMethod,
        string? DefaultMode, string? MethodTemplate, string? Calendar);
    private record SprintReq(string? Name, DateTime? StartDate, DateTime? EndDate, string? Goal, string? Status);
    private record ReleaseReq(string? Name, DateTime? TargetDate, string? Description, string? Status);
}
