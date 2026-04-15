using Npgsql;
using Central.Data;

namespace Central.Api.Endpoints;

public static class TaskEndpoints
{
    public static RouteGroupBuilder MapTaskEndpoints(this RouteGroupBuilder group)
    {
        // GET /api/tasks — all tasks with full Phase 1 fields
        group.MapGet("/", async (int? project_id, DbConnectionFactory db) =>
        {
            await using var conn = new NpgsqlConnection(db.ConnectionString);
            await conn.OpenAsync();
            var where = project_id.HasValue ? " WHERE t.project_id = @pid" : "";
            await using var cmd = new NpgsqlCommand($@"
                SELECT t.id, t.parent_id, t.title, t.description, t.status, t.priority, t.task_type,
                       t.assigned_to, COALESCE(a.display_name,'') as assigned_name,
                       t.created_by, COALESCE(c.display_name,'') as created_name,
                       t.building, t.due_date, t.estimated_hours, t.actual_hours, t.tags,
                       t.sort_order, t.created_at, t.updated_at, t.completed_at,
                       t.project_id, COALESCE(pr.name,'') as project_name,
                       t.sprint_id, COALESCE(sp.name,'') as sprint_name,
                       t.wbs, t.is_epic, t.is_user_story, t.points, t.work_remaining,
                       t.start_date, t.finish_date, t.is_milestone, t.risk, t.confidence,
                       t.severity, t.bug_priority, t.backlog_priority, t.sprint_priority,
                       t.committed_to, COALESCE(cs.name,'') as committed_sprint,
                       t.category, t.time_spent, t.color, t.board_column, t.board_lane
                FROM tasks t
                LEFT JOIN app_users a ON a.id = t.assigned_to
                LEFT JOIN app_users c ON c.id = t.created_by
                LEFT JOIN task_projects pr ON pr.id = t.project_id
                LEFT JOIN sprints sp ON sp.id = t.sprint_id
                LEFT JOIN sprints cs ON cs.id = t.committed_to
                {where} ORDER BY t.sort_order, t.created_at", conn);
            if (project_id.HasValue) cmd.Parameters.AddWithValue("pid", project_id.Value);
            var list = new List<object>();
            await using var rdr = await cmd.ExecuteReaderAsync();
            while (await rdr.ReadAsync())
                list.Add(new
                {
                    id = rdr.GetInt32(0), parent_id = rdr.IsDBNull(1) ? (int?)null : rdr.GetInt32(1),
                    title = rdr.GetString(2), description = rdr.IsDBNull(3) ? "" : rdr.GetString(3),
                    status = rdr.GetString(4), priority = rdr.GetString(5), task_type = rdr.GetString(6),
                    assigned_to = rdr.IsDBNull(7) ? (int?)null : rdr.GetInt32(7), assigned_name = rdr.GetString(8),
                    created_by = rdr.IsDBNull(9) ? (int?)null : rdr.GetInt32(9), created_name = rdr.GetString(10),
                    building = rdr.IsDBNull(11) ? "" : rdr.GetString(11),
                    due_date = rdr.IsDBNull(12) ? null : rdr.GetDateTime(12).ToString("yyyy-MM-dd"),
                    estimated_hours = rdr.IsDBNull(13) ? (decimal?)null : rdr.GetDecimal(13),
                    actual_hours = rdr.IsDBNull(14) ? (decimal?)null : rdr.GetDecimal(14),
                    tags = rdr.IsDBNull(15) ? "" : rdr.GetString(15),
                    sort_order = rdr.GetInt32(16),
                    created_at = rdr.GetDateTime(17).ToString("o"), updated_at = rdr.GetDateTime(18).ToString("o"),
                    completed_at = rdr.IsDBNull(19) ? null : rdr.GetDateTime(19).ToString("o"),
                    project_id = rdr.IsDBNull(20) ? (int?)null : rdr.GetInt32(20), project_name = rdr.GetString(21),
                    sprint_id = rdr.IsDBNull(22) ? (int?)null : rdr.GetInt32(22), sprint_name = rdr.GetString(23),
                    wbs = rdr.IsDBNull(24) ? "" : rdr.GetString(24),
                    is_epic = !rdr.IsDBNull(25) && rdr.GetBoolean(25),
                    is_user_story = !rdr.IsDBNull(26) && rdr.GetBoolean(26),
                    points = rdr.IsDBNull(27) ? (decimal?)null : rdr.GetDecimal(27),
                    work_remaining = rdr.IsDBNull(28) ? (decimal?)null : rdr.GetDecimal(28),
                    start_date = rdr.IsDBNull(29) ? null : rdr.GetDateTime(29).ToString("yyyy-MM-dd"),
                    finish_date = rdr.IsDBNull(30) ? null : rdr.GetDateTime(30).ToString("yyyy-MM-dd"),
                    is_milestone = !rdr.IsDBNull(31) && rdr.GetBoolean(31),
                    risk = rdr.IsDBNull(32) ? "" : rdr.GetString(32),
                    confidence = rdr.IsDBNull(33) ? "" : rdr.GetString(33),
                    severity = rdr.IsDBNull(34) ? "" : rdr.GetString(34),
                    bug_priority = rdr.IsDBNull(35) ? "" : rdr.GetString(35),
                    backlog_priority = rdr.IsDBNull(36) ? 0 : rdr.GetInt32(36),
                    sprint_priority = rdr.IsDBNull(37) ? 0 : rdr.GetInt32(37),
                    committed_to = rdr.IsDBNull(38) ? (int?)null : rdr.GetInt32(38),
                    committed_sprint = rdr.GetString(39),
                    category = rdr.IsDBNull(40) ? "" : rdr.GetString(40),
                    time_spent = rdr.IsDBNull(41) ? 0m : rdr.GetDecimal(41),
                    color = rdr.IsDBNull(42) ? "" : rdr.GetString(42),
                    board_column = rdr.IsDBNull(43) ? "" : rdr.GetString(43),
                    board_lane = rdr.IsDBNull(44) ? "" : rdr.GetString(44),
                });
            return Results.Ok(list);
        });

        // POST /api/tasks — create task with all fields
        group.MapPost("/", async (TaskRequest req, DbConnectionFactory db) =>
        {
            await using var conn = new NpgsqlConnection(db.ConnectionString);
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand(@"
                INSERT INTO tasks (parent_id, title, description, status, priority, task_type,
                    assigned_to, created_by, building, due_date, tags, project_id, sprint_id,
                    is_epic, is_user_story, points, work_remaining, start_date, finish_date,
                    is_milestone, risk, severity, bug_priority, category, color, committed_to)
                VALUES (@parent, @title, @desc, @status, @pri, @type, @assigned, @created, @bldg,
                    @due, @tags, @projId, @sprintId, @isEpic, @isStory, @points, @workRem,
                    @startDt, @finishDt, @isMile, @risk, @sev, @bugPri, @cat, @color, @committed)
                RETURNING id", conn);
            AddParams(cmd, req);
            var id = (int)(await cmd.ExecuteScalarAsync())!;
            return Results.Created($"/api/tasks/{id}", new { id });
        });

        // PUT /api/tasks/{id} — update task with all fields
        group.MapPut("/{id}", async (int id, TaskRequest req, DbConnectionFactory db) =>
        {
            await using var conn = new NpgsqlConnection(db.ConnectionString);
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand(@"
                UPDATE tasks SET title=@title, description=@desc, status=@status, priority=@pri,
                    task_type=@type, assigned_to=@assigned, building=@bldg, due_date=@due, tags=@tags,
                    project_id=@projId, sprint_id=@sprintId, is_epic=@isEpic, is_user_story=@isStory,
                    points=@points, work_remaining=@workRem, start_date=@startDt, finish_date=@finishDt,
                    is_milestone=@isMile, risk=@risk, severity=@sev, bug_priority=@bugPri,
                    category=@cat, color=@color, committed_to=@committed,
                    updated_at=now(), completed_at = CASE WHEN @status='Done' THEN COALESCE(completed_at,now()) ELSE NULL END
                WHERE id=@id", conn);
            cmd.Parameters.AddWithValue("id", id);
            AddParams(cmd, req);
            await cmd.ExecuteNonQueryAsync();
            return Results.Ok();
        });

        // DELETE /api/tasks/{id}
        group.MapDelete("/{id}", async (int id, DbConnectionFactory db) =>
        {
            await using var conn = new NpgsqlConnection(db.ConnectionString);
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand("DELETE FROM tasks WHERE id = @id", conn);
            cmd.Parameters.AddWithValue("id", id);
            await cmd.ExecuteNonQueryAsync();
            return Results.Ok();
        });

        // POST /api/tasks/{id}/commit — commit to sprint
        group.MapPost("/{id}/commit", async (int id, CommitRequest req, DbConnectionFactory db) =>
        {
            await using var conn = new NpgsqlConnection(db.ConnectionString);
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand("UPDATE tasks SET committed_to=@sid, updated_at=now() WHERE id=@id", conn);
            cmd.Parameters.AddWithValue("id", id);
            cmd.Parameters.AddWithValue("sid", req.SprintId);
            await cmd.ExecuteNonQueryAsync();
            return Results.Ok();
        });

        // DELETE /api/tasks/{id}/commit — uncommit from sprint
        group.MapDelete("/{id}/commit", async (int id, DbConnectionFactory db) =>
        {
            await using var conn = new NpgsqlConnection(db.ConnectionString);
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand("UPDATE tasks SET committed_to=NULL, updated_at=now() WHERE id=@id", conn);
            cmd.Parameters.AddWithValue("id", id);
            await cmd.ExecuteNonQueryAsync();
            return Results.Ok();
        });

        // GET /api/tasks/{id}/links — get task links
        group.MapGet("/{id}/links", async (int id, DbConnectionFactory db) =>
        {
            await using var conn = new NpgsqlConnection(db.ConnectionString);
            await conn.OpenAsync();
            var list = new List<object>();
            await using var cmd = new NpgsqlCommand(@"
                SELECT tl.id, tl.source_id, tl.target_id, tl.link_type, tl.lag_days, COALESCE(t.title,'')
                FROM task_links tl LEFT JOIN tasks t ON t.id = tl.target_id
                WHERE tl.source_id=@id OR tl.target_id=@id ORDER BY tl.id", conn);
            cmd.Parameters.AddWithValue("id", id);
            await using var rdr = await cmd.ExecuteReaderAsync();
            while (await rdr.ReadAsync())
                list.Add(new { id = rdr.GetInt32(0), source_id = rdr.GetInt32(1), target_id = rdr.GetInt32(2),
                    link_type = rdr.GetString(3), lag_days = rdr.GetInt32(4), target_title = rdr.GetString(5) });
            return Results.Ok(list);
        });

        // POST /api/tasks/{id}/links — create task link
        group.MapPost("/{id}/links", async (int id, LinkRequest req, DbConnectionFactory db) =>
        {
            await using var conn = new NpgsqlConnection(db.ConnectionString);
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand(@"
                INSERT INTO task_links (source_id, target_id, link_type, lag_days)
                VALUES (@src, @tgt, @type, @lag)
                ON CONFLICT (source_id, target_id, link_type) DO UPDATE SET lag_days=@lag", conn);
            cmd.Parameters.AddWithValue("src", id);
            cmd.Parameters.AddWithValue("tgt", req.TargetId);
            cmd.Parameters.AddWithValue("type", req.LinkType ?? "relates_to");
            cmd.Parameters.AddWithValue("lag", req.LagDays ?? 0);
            await cmd.ExecuteNonQueryAsync();
            return Results.Ok();
        });

        // GET /api/tasks/{id}/dependencies — get Gantt dependencies
        group.MapGet("/{id}/dependencies", async (int id, DbConnectionFactory db) =>
        {
            await using var conn = new NpgsqlConnection(db.ConnectionString);
            await conn.OpenAsync();
            var list = new List<object>();
            await using var cmd = new NpgsqlCommand(@"
                SELECT td.id, td.predecessor_id, td.successor_id, td.dep_type, td.lag_days,
                       COALESCE(p.title,''), COALESCE(s.title,'')
                FROM task_dependencies td
                LEFT JOIN tasks p ON p.id=td.predecessor_id LEFT JOIN tasks s ON s.id=td.successor_id
                WHERE td.predecessor_id=@id OR td.successor_id=@id ORDER BY td.id", conn);
            cmd.Parameters.AddWithValue("id", id);
            await using var rdr = await cmd.ExecuteReaderAsync();
            while (await rdr.ReadAsync())
                list.Add(new { id = rdr.GetInt32(0), predecessor_id = rdr.GetInt32(1), successor_id = rdr.GetInt32(2),
                    dep_type = rdr.GetString(3), lag_days = rdr.GetInt32(4),
                    predecessor_title = rdr.GetString(5), successor_title = rdr.GetString(6) });
            return Results.Ok(list);
        });

        // GET /api/tasks/{id}/comments
        group.MapGet("/{id}/comments", async (int id, DbConnectionFactory db) =>
        {
            await using var conn = new NpgsqlConnection(db.ConnectionString);
            await conn.OpenAsync();
            var list = new List<object>();
            await using var cmd = new NpgsqlCommand(@"
                SELECT c.id, c.comment_text, c.created_at, COALESCE(u.display_name,'Unknown')
                FROM task_comments c LEFT JOIN app_users u ON u.id=c.user_id
                WHERE c.task_id=@tid ORDER BY c.created_at", conn);
            cmd.Parameters.AddWithValue("tid", id);
            await using var rdr = await cmd.ExecuteReaderAsync();
            while (await rdr.ReadAsync())
                list.Add(new { id = rdr.GetInt32(0), text = rdr.GetString(1), created_at = rdr.GetDateTime(2).ToString("o"), user = rdr.GetString(3) });
            return Results.Ok(list);
        });

        // POST /api/tasks/{id}/comments
        group.MapPost("/{id}/comments", async (int id, CommentRequest req, DbConnectionFactory db) =>
        {
            await using var conn = new NpgsqlConnection(db.ConnectionString);
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand(
                "INSERT INTO task_comments (task_id, user_id, comment_text) VALUES (@tid, @uid, @text) RETURNING id", conn);
            cmd.Parameters.AddWithValue("tid", id);
            cmd.Parameters.AddWithValue("uid", (object?)req.UserId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("text", req.Text ?? "");
            var commentId = (int)(await cmd.ExecuteScalarAsync())!;
            return Results.Created($"/api/tasks/{id}/comments/{commentId}", new { id = commentId });
        });

        // GET /api/tasks/{id}/time — time entries for a task
        group.MapGet("/{id}/time", async (int id, DbConnectionFactory db) =>
        {
            await using var conn = new NpgsqlConnection(db.ConnectionString);
            await conn.OpenAsync();
            var list = new List<object>();
            await using var cmd = new NpgsqlCommand(@"
                SELECT te.id, te.entry_date, te.hours, te.activity_type, te.notes, COALESCE(u.display_name,'')
                FROM time_entries te LEFT JOIN app_users u ON u.id=te.user_id
                WHERE te.task_id=@tid ORDER BY te.entry_date DESC", conn);
            cmd.Parameters.AddWithValue("tid", id);
            await using var rdr = await cmd.ExecuteReaderAsync();
            while (await rdr.ReadAsync())
                list.Add(new { id = rdr.GetInt32(0), entry_date = rdr.GetDateTime(1).ToString("yyyy-MM-dd"),
                    hours = rdr.GetDecimal(2), activity_type = rdr.IsDBNull(3) ? "" : rdr.GetString(3),
                    notes = rdr.IsDBNull(4) ? "" : rdr.GetString(4), user = rdr.GetString(5) });
            return Results.Ok(list);
        });

        // POST /api/tasks/{id}/time — log time against a task. UserId is
        // resolved from the JWT subject claim if not supplied (so users
        // can only log against themselves through normal UI flows).
        group.MapPost("/{id}/time", async (int id, TimeEntryRequest req, DbConnectionFactory db, HttpContext ctx) =>
        {
            if (req.Hours <= 0)
                return Results.BadRequest(new { error = "hours must be > 0" });

            // Try to derive user_id from JWT if not supplied. Falls back to
            // null which the DB allows for service-account usage.
            int? userId = req.UserId;
            if (userId is null)
            {
                var sub = ctx.User?.FindFirst("user_id")?.Value
                       ?? ctx.User?.FindFirst("sub")?.Value;
                if (int.TryParse(sub, out var parsed)) userId = parsed;
            }

            await using var conn = new NpgsqlConnection(db.ConnectionString);
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand(@"
                INSERT INTO time_entries (task_id, user_id, entry_date, hours, activity_type, notes)
                VALUES (@tid, @uid, @ed, @h, @at, @n) RETURNING id", conn);
            cmd.Parameters.AddWithValue("tid", id);
            cmd.Parameters.AddWithValue("uid", (object?)userId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("ed",  req.EntryDate ?? DateTime.Today);
            cmd.Parameters.AddWithValue("h",   req.Hours);
            cmd.Parameters.AddWithValue("at",  (object?)req.ActivityType ?? DBNull.Value);
            cmd.Parameters.AddWithValue("n",   (object?)req.Notes ?? DBNull.Value);
            var newId = (int)(await cmd.ExecuteScalarAsync())!;
            return Results.Created($"/api/tasks/{id}/time/{newId}", new { id = newId });
        });

        // DELETE /api/tasks/{id}/time/{entryId} — remove a time entry. The
        // server doesn't enforce ownership at this level — that's a job for
        // the future tighter authz layer.
        group.MapDelete("/{id}/time/{entryId:int}", async (int id, int entryId, DbConnectionFactory db) =>
        {
            await using var conn = new NpgsqlConnection(db.ConnectionString);
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand(
                "DELETE FROM time_entries WHERE id=@eid AND task_id=@tid RETURNING id", conn);
            cmd.Parameters.AddWithValue("eid", entryId);
            cmd.Parameters.AddWithValue("tid", id);
            var result = await cmd.ExecuteScalarAsync();
            return result is null ? Results.NotFound() : Results.NoContent();
        });

        // GET /api/tasks/{id}/activity — recent activity for a single task.
        // Sourced from activity_feed (migration 067) which is populated by
        // the log_task_activity() trigger.
        group.MapGet("/{id}/activity", async (int id, int? limit, DbConnectionFactory db) =>
        {
            await using var conn = new NpgsqlConnection(db.ConnectionString);
            await conn.OpenAsync();
            var list = new List<object>();
            await using var cmd = new NpgsqlCommand(@"
                SELECT a.id, a.created_at, a.action, a.summary,
                       COALESCE(u.display_name, a.username, '') AS who
                FROM activity_feed a
                LEFT JOIN app_users u ON u.id = a.user_id
                WHERE a.task_id = @tid
                ORDER BY a.created_at DESC
                LIMIT @lim", conn);
            cmd.Parameters.AddWithValue("tid", id);
            cmd.Parameters.AddWithValue("lim", limit ?? 50);
            await using var rdr = await cmd.ExecuteReaderAsync();
            while (await rdr.ReadAsync())
                list.Add(new {
                    id        = rdr.GetInt32(0),
                    timestamp = rdr.GetDateTime(1),
                    action    = rdr.IsDBNull(2) ? "" : rdr.GetString(2),
                    summary   = rdr.IsDBNull(3) ? "" : rdr.GetString(3),
                    user      = rdr.GetString(4),
                });
            return Results.Ok(list);
        });

        return group;
    }

    private static void AddParams(NpgsqlCommand cmd, TaskRequest req)
    {
        cmd.Parameters.AddWithValue("parent", (object?)req.ParentId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("title", req.Title ?? "");
        cmd.Parameters.AddWithValue("desc", (object?)req.Description ?? DBNull.Value);
        cmd.Parameters.AddWithValue("status", req.Status ?? "Open");
        cmd.Parameters.AddWithValue("pri", req.Priority ?? "Medium");
        cmd.Parameters.AddWithValue("type", req.TaskType ?? "Task");
        cmd.Parameters.AddWithValue("assigned", (object?)req.AssignedTo ?? DBNull.Value);
        cmd.Parameters.AddWithValue("created", (object?)req.CreatedBy ?? DBNull.Value);
        cmd.Parameters.AddWithValue("bldg", (object?)req.Building ?? DBNull.Value);
        cmd.Parameters.AddWithValue("due", (object?)req.DueDate ?? DBNull.Value);
        cmd.Parameters.AddWithValue("tags", (object?)req.Tags ?? DBNull.Value);
        cmd.Parameters.AddWithValue("projId", (object?)req.ProjectId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("sprintId", (object?)req.SprintId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("isEpic", req.IsEpic ?? false);
        cmd.Parameters.AddWithValue("isStory", req.IsUserStory ?? false);
        cmd.Parameters.AddWithValue("points", (object?)req.Points ?? DBNull.Value);
        cmd.Parameters.AddWithValue("workRem", (object?)req.WorkRemaining ?? DBNull.Value);
        cmd.Parameters.AddWithValue("startDt", (object?)req.StartDate ?? DBNull.Value);
        cmd.Parameters.AddWithValue("finishDt", (object?)req.FinishDate ?? DBNull.Value);
        cmd.Parameters.AddWithValue("isMile", req.IsMilestone ?? false);
        cmd.Parameters.AddWithValue("risk", (object?)req.Risk ?? DBNull.Value);
        cmd.Parameters.AddWithValue("sev", (object?)req.Severity ?? DBNull.Value);
        cmd.Parameters.AddWithValue("bugPri", (object?)req.BugPriority ?? DBNull.Value);
        cmd.Parameters.AddWithValue("cat", (object?)req.Category ?? DBNull.Value);
        cmd.Parameters.AddWithValue("color", (object?)req.Color ?? DBNull.Value);
        cmd.Parameters.AddWithValue("committed", (object?)req.CommittedTo ?? DBNull.Value);
    }

    private record TaskRequest(int? ParentId, string? Title, string? Description, string? Status,
        string? Priority, string? TaskType, int? AssignedTo, int? CreatedBy, string? Building,
        DateTime? DueDate, string? Tags, int? ProjectId, int? SprintId, bool? IsEpic, bool? IsUserStory,
        decimal? Points, decimal? WorkRemaining, DateTime? StartDate, DateTime? FinishDate,
        bool? IsMilestone, string? Risk, string? Severity, string? BugPriority, string? Category,
        string? Color, int? CommittedTo);
    private record CommentRequest(int? UserId, string? Text);
    private record CommitRequest(int SprintId);
    private record LinkRequest(int TargetId, string? LinkType, int? LagDays);
    private record TimeEntryRequest(decimal Hours, string? ActivityType, string? Notes,
                                    DateTime? EntryDate, int? UserId);
}
