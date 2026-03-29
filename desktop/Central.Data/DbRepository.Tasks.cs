using Npgsql;
using Central.Core.Models;

namespace Central.Data;

public partial class DbRepository
{
    // ════════════════════════════════════════════
    // Portfolios
    // ════════════════════════════════════════════

    public async Task<List<Portfolio>> GetPortfoliosAsync()
    {
        var list = new List<Portfolio>();
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(@"
            SELECT p.id, p.name, p.description, p.owner_id, COALESCE(u.display_name,''),
                   p.archived, p.created_at, p.updated_at
            FROM portfolios p LEFT JOIN app_users u ON u.id = p.owner_id
            ORDER BY p.name", conn);
        await using var rdr = await cmd.ExecuteReaderAsync();
        while (await rdr.ReadAsync())
            list.Add(new Portfolio
            {
                Id = rdr.GetInt32(0), Name = rdr.GetString(1),
                Description = S(rdr, 2), OwnerId = rdr.IsDBNull(3) ? null : rdr.GetInt32(3),
                OwnerName = rdr.GetString(4), Archived = rdr.GetBoolean(5),
                CreatedAt = rdr.GetDateTime(6), UpdatedAt = rdr.GetDateTime(7)
            });
        return list;
    }

    public async Task UpsertPortfolioAsync(Portfolio p)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        if (p.Id == 0)
        {
            await using var cmd = new NpgsqlCommand(@"
                INSERT INTO portfolios (name, description, owner_id, archived)
                VALUES (@name, @desc, @owner, @archived) RETURNING id", conn);
            cmd.Parameters.AddWithValue("name", p.Name);
            cmd.Parameters.AddWithValue("desc", (object?)p.Description ?? DBNull.Value);
            cmd.Parameters.AddWithValue("owner", (object?)p.OwnerId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("archived", p.Archived);
            p.Id = (int)(await cmd.ExecuteScalarAsync())!;
        }
        else
        {
            await using var cmd = new NpgsqlCommand(@"
                UPDATE portfolios SET name=@name, description=@desc, owner_id=@owner,
                    archived=@archived, updated_at=now() WHERE id=@id", conn);
            cmd.Parameters.AddWithValue("id", p.Id);
            cmd.Parameters.AddWithValue("name", p.Name);
            cmd.Parameters.AddWithValue("desc", (object?)p.Description ?? DBNull.Value);
            cmd.Parameters.AddWithValue("owner", (object?)p.OwnerId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("archived", p.Archived);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    public async Task DeletePortfolioAsync(int id)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand("DELETE FROM portfolios WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    // ════════════════════════════════════════════
    // Programmes
    // ════════════════════════════════════════════

    public async Task<List<Programme>> GetProgrammesAsync()
    {
        var list = new List<Programme>();
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(@"
            SELECT p.id, p.portfolio_id, p.name, p.description, p.owner_id,
                   COALESCE(u.display_name,''), p.created_at, p.updated_at
            FROM programmes p LEFT JOIN app_users u ON u.id = p.owner_id
            ORDER BY p.name", conn);
        await using var rdr = await cmd.ExecuteReaderAsync();
        while (await rdr.ReadAsync())
            list.Add(new Programme
            {
                Id = rdr.GetInt32(0), PortfolioId = rdr.IsDBNull(1) ? null : rdr.GetInt32(1),
                Name = rdr.GetString(2), Description = S(rdr, 3),
                OwnerId = rdr.IsDBNull(4) ? null : rdr.GetInt32(4), OwnerName = rdr.GetString(5),
                CreatedAt = rdr.GetDateTime(6), UpdatedAt = rdr.GetDateTime(7)
            });
        return list;
    }

    public async Task UpsertProgrammeAsync(Programme p)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        if (p.Id == 0)
        {
            await using var cmd = new NpgsqlCommand(@"
                INSERT INTO programmes (portfolio_id, name, description, owner_id)
                VALUES (@port, @name, @desc, @owner) RETURNING id", conn);
            cmd.Parameters.AddWithValue("port", (object?)p.PortfolioId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("name", p.Name);
            cmd.Parameters.AddWithValue("desc", (object?)p.Description ?? DBNull.Value);
            cmd.Parameters.AddWithValue("owner", (object?)p.OwnerId ?? DBNull.Value);
            p.Id = (int)(await cmd.ExecuteScalarAsync())!;
        }
        else
        {
            await using var cmd = new NpgsqlCommand(@"
                UPDATE programmes SET portfolio_id=@port, name=@name, description=@desc,
                    owner_id=@owner, updated_at=now() WHERE id=@id", conn);
            cmd.Parameters.AddWithValue("id", p.Id);
            cmd.Parameters.AddWithValue("port", (object?)p.PortfolioId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("name", p.Name);
            cmd.Parameters.AddWithValue("desc", (object?)p.Description ?? DBNull.Value);
            cmd.Parameters.AddWithValue("owner", (object?)p.OwnerId ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    public async Task DeleteProgrammeAsync(int id)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand("DELETE FROM programmes WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    // ════════════════════════════════════════════
    // Task Projects
    // ════════════════════════════════════════════

    public async Task<List<TaskProject>> GetTaskProjectsAsync()
    {
        var list = new List<TaskProject>();
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(@"
            SELECT id, programme_id, name, description, scheduling_method, default_mode,
                   method_template, calendar, archived, created_at, updated_at
            FROM task_projects ORDER BY name", conn);
        await using var rdr = await cmd.ExecuteReaderAsync();
        while (await rdr.ReadAsync())
            list.Add(new TaskProject
            {
                Id = rdr.GetInt32(0), ProgrammeId = rdr.IsDBNull(1) ? null : rdr.GetInt32(1),
                Name = rdr.GetString(2), Description = S(rdr, 3),
                SchedulingMethod = rdr.GetString(4), DefaultMode = rdr.GetString(5),
                MethodTemplate = rdr.GetString(6), Calendar = S(rdr, 7),
                Archived = rdr.GetBoolean(8), CreatedAt = rdr.GetDateTime(9), UpdatedAt = rdr.GetDateTime(10)
            });
        return list;
    }

    public async Task UpsertTaskProjectAsync(TaskProject p)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        if (p.Id == 0)
        {
            await using var cmd = new NpgsqlCommand(@"
                INSERT INTO task_projects (programme_id, name, description, scheduling_method, default_mode, method_template, calendar, archived)
                VALUES (@prog, @name, @desc, @sched, @mode, @tmpl, @cal, @arch) RETURNING id", conn);
            cmd.Parameters.AddWithValue("prog", (object?)p.ProgrammeId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("name", p.Name);
            cmd.Parameters.AddWithValue("desc", (object?)p.Description ?? DBNull.Value);
            cmd.Parameters.AddWithValue("sched", p.SchedulingMethod);
            cmd.Parameters.AddWithValue("mode", p.DefaultMode);
            cmd.Parameters.AddWithValue("tmpl", p.MethodTemplate);
            cmd.Parameters.AddWithValue("cal", (object?)p.Calendar ?? DBNull.Value);
            cmd.Parameters.AddWithValue("arch", p.Archived);
            p.Id = (int)(await cmd.ExecuteScalarAsync())!;
        }
        else
        {
            await using var cmd = new NpgsqlCommand(@"
                UPDATE task_projects SET programme_id=@prog, name=@name, description=@desc,
                    scheduling_method=@sched, default_mode=@mode, method_template=@tmpl,
                    calendar=@cal, archived=@arch, updated_at=now() WHERE id=@id", conn);
            cmd.Parameters.AddWithValue("id", p.Id);
            cmd.Parameters.AddWithValue("prog", (object?)p.ProgrammeId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("name", p.Name);
            cmd.Parameters.AddWithValue("desc", (object?)p.Description ?? DBNull.Value);
            cmd.Parameters.AddWithValue("sched", p.SchedulingMethod);
            cmd.Parameters.AddWithValue("mode", p.DefaultMode);
            cmd.Parameters.AddWithValue("tmpl", p.MethodTemplate);
            cmd.Parameters.AddWithValue("cal", (object?)p.Calendar ?? DBNull.Value);
            cmd.Parameters.AddWithValue("arch", p.Archived);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    public async Task DeleteTaskProjectAsync(int id)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand("DELETE FROM task_projects WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    // ════════════════════════════════════════════
    // Project Members
    // ════════════════════════════════════════════

    public async Task<List<ProjectMember>> GetProjectMembersAsync(int projectId)
    {
        var list = new List<ProjectMember>();
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(@"
            SELECT pm.id, pm.project_id, pm.user_id, COALESCE(u.display_name,''), pm.role
            FROM project_members pm LEFT JOIN app_users u ON u.id = pm.user_id
            WHERE pm.project_id = @pid ORDER BY u.display_name", conn);
        cmd.Parameters.AddWithValue("pid", projectId);
        await using var rdr = await cmd.ExecuteReaderAsync();
        while (await rdr.ReadAsync())
            list.Add(new ProjectMember
            {
                Id = rdr.GetInt32(0), ProjectId = rdr.GetInt32(1),
                UserId = rdr.GetInt32(2), UserName = rdr.GetString(3), Role = rdr.GetString(4)
            });
        return list;
    }

    public async Task UpsertProjectMemberAsync(ProjectMember m)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(@"
            INSERT INTO project_members (project_id, user_id, role)
            VALUES (@pid, @uid, @role)
            ON CONFLICT (project_id, user_id) DO UPDATE SET role = @role", conn);
        cmd.Parameters.AddWithValue("pid", m.ProjectId);
        cmd.Parameters.AddWithValue("uid", m.UserId);
        cmd.Parameters.AddWithValue("role", m.Role);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task RemoveProjectMemberAsync(int projectId, int userId)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "DELETE FROM project_members WHERE project_id = @pid AND user_id = @uid", conn);
        cmd.Parameters.AddWithValue("pid", projectId);
        cmd.Parameters.AddWithValue("uid", userId);
        await cmd.ExecuteNonQueryAsync();
    }

    // ════════════════════════════════════════════
    // Sprints
    // ════════════════════════════════════════════

    public async Task<List<Sprint>> GetSprintsAsync(int? projectId = null)
    {
        var list = new List<Sprint>();
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        var sql = @"SELECT id, project_id, name, start_date, end_date, goal, status,
                           velocity_points, velocity_hours, created_at
                    FROM sprints" + (projectId.HasValue ? " WHERE project_id = @pid" : "") + " ORDER BY start_date DESC, name";
        await using var cmd = new NpgsqlCommand(sql, conn);
        if (projectId.HasValue) cmd.Parameters.AddWithValue("pid", projectId.Value);
        await using var rdr = await cmd.ExecuteReaderAsync();
        while (await rdr.ReadAsync())
            list.Add(new Sprint
            {
                Id = rdr.GetInt32(0), ProjectId = rdr.GetInt32(1), Name = rdr.GetString(2),
                StartDate = rdr.IsDBNull(3) ? null : rdr.GetDateTime(3),
                EndDate = rdr.IsDBNull(4) ? null : rdr.GetDateTime(4),
                Goal = S(rdr, 5), Status = rdr.GetString(6),
                VelocityPoints = rdr.IsDBNull(7) ? null : rdr.GetDecimal(7),
                VelocityHours = rdr.IsDBNull(8) ? null : rdr.GetDecimal(8),
                CreatedAt = rdr.GetDateTime(9)
            });
        return list;
    }

    public async Task UpsertSprintAsync(Sprint s)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        if (s.Id == 0)
        {
            await using var cmd = new NpgsqlCommand(@"
                INSERT INTO sprints (project_id, name, start_date, end_date, goal, status)
                VALUES (@pid, @name, @start, @end, @goal, @status) RETURNING id", conn);
            cmd.Parameters.AddWithValue("pid", s.ProjectId);
            cmd.Parameters.AddWithValue("name", s.Name);
            cmd.Parameters.AddWithValue("start", (object?)s.StartDate ?? DBNull.Value);
            cmd.Parameters.AddWithValue("end", (object?)s.EndDate ?? DBNull.Value);
            cmd.Parameters.AddWithValue("goal", (object?)s.Goal ?? DBNull.Value);
            cmd.Parameters.AddWithValue("status", s.Status);
            s.Id = (int)(await cmd.ExecuteScalarAsync())!;
        }
        else
        {
            await using var cmd = new NpgsqlCommand(@"
                UPDATE sprints SET name=@name, start_date=@start, end_date=@end, goal=@goal,
                    status=@status, velocity_points=@vp, velocity_hours=@vh WHERE id=@id", conn);
            cmd.Parameters.AddWithValue("id", s.Id);
            cmd.Parameters.AddWithValue("name", s.Name);
            cmd.Parameters.AddWithValue("start", (object?)s.StartDate ?? DBNull.Value);
            cmd.Parameters.AddWithValue("end", (object?)s.EndDate ?? DBNull.Value);
            cmd.Parameters.AddWithValue("goal", (object?)s.Goal ?? DBNull.Value);
            cmd.Parameters.AddWithValue("status", s.Status);
            cmd.Parameters.AddWithValue("vp", (object?)s.VelocityPoints ?? DBNull.Value);
            cmd.Parameters.AddWithValue("vh", (object?)s.VelocityHours ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    public async Task DeleteSprintAsync(int id)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand("DELETE FROM sprints WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    // ════════════════════════════════════════════
    // Releases
    // ════════════════════════════════════════════

    public async Task<List<Release>> GetReleasesAsync(int? projectId = null)
    {
        var list = new List<Release>();
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        var sql = @"SELECT id, project_id, name, target_date, description, status, created_at
                    FROM releases" + (projectId.HasValue ? " WHERE project_id = @pid" : "") + " ORDER BY target_date, name";
        await using var cmd = new NpgsqlCommand(sql, conn);
        if (projectId.HasValue) cmd.Parameters.AddWithValue("pid", projectId.Value);
        await using var rdr = await cmd.ExecuteReaderAsync();
        while (await rdr.ReadAsync())
            list.Add(new Release
            {
                Id = rdr.GetInt32(0), ProjectId = rdr.GetInt32(1), Name = rdr.GetString(2),
                TargetDate = rdr.IsDBNull(3) ? null : rdr.GetDateTime(3),
                Description = S(rdr, 4), Status = rdr.GetString(5), CreatedAt = rdr.GetDateTime(6)
            });
        return list;
    }

    public async Task UpsertReleaseAsync(Release r)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        if (r.Id == 0)
        {
            await using var cmd = new NpgsqlCommand(@"
                INSERT INTO releases (project_id, name, target_date, description, status)
                VALUES (@pid, @name, @date, @desc, @status) RETURNING id", conn);
            cmd.Parameters.AddWithValue("pid", r.ProjectId);
            cmd.Parameters.AddWithValue("name", r.Name);
            cmd.Parameters.AddWithValue("date", (object?)r.TargetDate ?? DBNull.Value);
            cmd.Parameters.AddWithValue("desc", (object?)r.Description ?? DBNull.Value);
            cmd.Parameters.AddWithValue("status", r.Status);
            r.Id = (int)(await cmd.ExecuteScalarAsync())!;
        }
        else
        {
            await using var cmd = new NpgsqlCommand(@"
                UPDATE releases SET name=@name, target_date=@date, description=@desc, status=@status WHERE id=@id", conn);
            cmd.Parameters.AddWithValue("id", r.Id);
            cmd.Parameters.AddWithValue("name", r.Name);
            cmd.Parameters.AddWithValue("date", (object?)r.TargetDate ?? DBNull.Value);
            cmd.Parameters.AddWithValue("desc", (object?)r.Description ?? DBNull.Value);
            cmd.Parameters.AddWithValue("status", r.Status);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    public async Task DeleteReleaseAsync(int id)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand("DELETE FROM releases WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    // ════════════════════════════════════════════
    // Task Links
    // ════════════════════════════════════════════

    public async Task<List<TaskLink>> GetTaskLinksAsync(int taskId)
    {
        var list = new List<TaskLink>();
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(@"
            SELECT tl.id, tl.source_id, tl.target_id, tl.link_type, tl.lag_days,
                   COALESCE(t.title,''), tl.created_at
            FROM task_links tl LEFT JOIN tasks t ON t.id = tl.target_id
            WHERE tl.source_id = @tid
            UNION ALL
            SELECT tl.id, tl.source_id, tl.target_id, tl.link_type, tl.lag_days,
                   COALESCE(t.title,''), tl.created_at
            FROM task_links tl LEFT JOIN tasks t ON t.id = tl.source_id
            WHERE tl.target_id = @tid
            ORDER BY 7", conn);
        cmd.Parameters.AddWithValue("tid", taskId);
        await using var rdr = await cmd.ExecuteReaderAsync();
        while (await rdr.ReadAsync())
            list.Add(new TaskLink
            {
                Id = rdr.GetInt32(0), SourceId = rdr.GetInt32(1), TargetId = rdr.GetInt32(2),
                LinkType = rdr.GetString(3), LagDays = rdr.GetInt32(4),
                TargetTitle = rdr.GetString(5), CreatedAt = rdr.GetDateTime(6)
            });
        return list;
    }

    public async Task UpsertTaskLinkAsync(TaskLink link)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(@"
            INSERT INTO task_links (source_id, target_id, link_type, lag_days)
            VALUES (@src, @tgt, @type, @lag)
            ON CONFLICT (source_id, target_id, link_type) DO UPDATE SET lag_days = @lag", conn);
        cmd.Parameters.AddWithValue("src", link.SourceId);
        cmd.Parameters.AddWithValue("tgt", link.TargetId);
        cmd.Parameters.AddWithValue("type", link.LinkType);
        cmd.Parameters.AddWithValue("lag", link.LagDays);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteTaskLinkAsync(int id)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand("DELETE FROM task_links WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    // ════════════════════════════════════════════
    // Task Dependencies (Gantt)
    // ════════════════════════════════════════════

    public async Task<List<TaskDependency>> GetTaskDependenciesAsync(int? taskId = null)
    {
        var list = new List<TaskDependency>();
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        var where = taskId.HasValue ? " WHERE td.predecessor_id = @tid OR td.successor_id = @tid" : "";
        await using var cmd = new NpgsqlCommand($@"
            SELECT td.id, td.predecessor_id, td.successor_id, td.dep_type, td.lag_days,
                   COALESCE(p.title,''), COALESCE(s.title,'')
            FROM task_dependencies td
            LEFT JOIN tasks p ON p.id = td.predecessor_id
            LEFT JOIN tasks s ON s.id = td.successor_id
            {where} ORDER BY td.id", conn);
        if (taskId.HasValue) cmd.Parameters.AddWithValue("tid", taskId.Value);
        await using var rdr = await cmd.ExecuteReaderAsync();
        while (await rdr.ReadAsync())
            list.Add(new TaskDependency
            {
                Id = rdr.GetInt32(0), PredecessorId = rdr.GetInt32(1), SuccessorId = rdr.GetInt32(2),
                DepType = rdr.GetString(3), LagDays = rdr.GetInt32(4),
                PredecessorTitle = rdr.GetString(5), SuccessorTitle = rdr.GetString(6)
            });
        return list;
    }

    public async Task UpsertTaskDependencyAsync(TaskDependency dep)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(@"
            INSERT INTO task_dependencies (predecessor_id, successor_id, dep_type, lag_days)
            VALUES (@pred, @succ, @type, @lag)
            ON CONFLICT (predecessor_id, successor_id) DO UPDATE SET dep_type = @type, lag_days = @lag", conn);
        cmd.Parameters.AddWithValue("pred", dep.PredecessorId);
        cmd.Parameters.AddWithValue("succ", dep.SuccessorId);
        cmd.Parameters.AddWithValue("type", dep.DepType);
        cmd.Parameters.AddWithValue("lag", dep.LagDays);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteTaskDependencyAsync(int id)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand("DELETE FROM task_dependencies WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    // ════════════════════════════════════════════
    // Sprint Allocations
    // ════════════════════════════════════════════

    public async Task<List<SprintAllocation>> GetSprintAllocationsAsync(int sprintId)
    {
        var list = new List<SprintAllocation>();
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(@"
            SELECT sa.id, sa.sprint_id, sa.user_id, COALESCE(u.display_name,''),
                   sa.capacity_hours, sa.capacity_points
            FROM sprint_allocations sa LEFT JOIN app_users u ON u.id = sa.user_id
            WHERE sa.sprint_id = @sid ORDER BY u.display_name", conn);
        cmd.Parameters.AddWithValue("sid", sprintId);
        await using var rdr = await cmd.ExecuteReaderAsync();
        while (await rdr.ReadAsync())
            list.Add(new SprintAllocation
            {
                Id = rdr.GetInt32(0), SprintId = rdr.GetInt32(1), UserId = rdr.GetInt32(2),
                UserName = rdr.GetString(3),
                CapacityHours = rdr.IsDBNull(4) ? null : rdr.GetDecimal(4),
                CapacityPoints = rdr.IsDBNull(5) ? null : rdr.GetDecimal(5)
            });
        return list;
    }

    public async Task UpsertSprintAllocationAsync(SprintAllocation a)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(@"
            INSERT INTO sprint_allocations (sprint_id, user_id, capacity_hours, capacity_points)
            VALUES (@sid, @uid, @hrs, @pts)
            ON CONFLICT (sprint_id, user_id) DO UPDATE SET capacity_hours=@hrs, capacity_points=@pts", conn);
        cmd.Parameters.AddWithValue("sid", a.SprintId);
        cmd.Parameters.AddWithValue("uid", a.UserId);
        cmd.Parameters.AddWithValue("hrs", (object?)a.CapacityHours ?? DBNull.Value);
        cmd.Parameters.AddWithValue("pts", (object?)a.CapacityPoints ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync();
    }

    // ════════════════════════════════════════════
    // Sprint Burndown
    // ════════════════════════════════════════════

    public async Task<List<SprintBurndownPoint>> GetSprintBurndownAsync(int sprintId)
    {
        var list = new List<SprintBurndownPoint>();
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(@"
            SELECT id, sprint_id, snapshot_date, points_remaining, hours_remaining,
                   points_completed, hours_completed
            FROM sprint_burndown WHERE sprint_id = @sid
            ORDER BY snapshot_date", conn);
        cmd.Parameters.AddWithValue("sid", sprintId);
        await using var rdr = await cmd.ExecuteReaderAsync();
        while (await rdr.ReadAsync())
            list.Add(new SprintBurndownPoint
            {
                Id = rdr.GetInt32(0), SprintId = rdr.GetInt32(1),
                SnapshotDate = rdr.GetDateTime(2),
                PointsRemaining = rdr.GetDecimal(3), HoursRemaining = rdr.GetDecimal(4),
                PointsCompleted = rdr.GetDecimal(5), HoursCompleted = rdr.GetDecimal(6)
            });
        return list;
    }

    public async Task SnapshotSprintBurndownAsync(int sprintId)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand("SELECT snapshot_sprint_burndown(@sid)", conn);
        cmd.Parameters.AddWithValue("sid", sprintId);
        await cmd.ExecuteNonQueryAsync();
    }

    // ════════════════════════════════════════════
    // Backlog Operations
    // ════════════════════════════════════════════

    /// <summary>Commit a backlog item to a sprint (sets committed_to, doesn't copy).</summary>
    public async Task CommitToSprintAsync(int taskId, int sprintId)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "UPDATE tasks SET committed_to = @sid, updated_at = now() WHERE id = @tid", conn);
        cmd.Parameters.AddWithValue("sid", sprintId);
        cmd.Parameters.AddWithValue("tid", taskId);
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>Remove a task from a sprint (uncommit).</summary>
    public async Task UncommitFromSprintAsync(int taskId)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "UPDATE tasks SET committed_to = NULL, updated_at = now() WHERE id = @tid", conn);
        cmd.Parameters.AddWithValue("tid", taskId);
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>Update backlog priority ordering for a task.</summary>
    public async Task UpdateBacklogPriorityAsync(int taskId, int priority)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "UPDATE tasks SET backlog_priority = @pri, updated_at = now() WHERE id = @tid", conn);
        cmd.Parameters.AddWithValue("pri", priority);
        cmd.Parameters.AddWithValue("tid", taskId);
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>Update sprint priority ordering for a task.</summary>
    public async Task UpdateSprintPriorityAsync(int taskId, int priority)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "UPDATE tasks SET sprint_priority = @pri, updated_at = now() WHERE id = @tid", conn);
        cmd.Parameters.AddWithValue("pri", priority);
        cmd.Parameters.AddWithValue("tid", taskId);
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>Get sprint summary stats (total points, hours, completed counts).</summary>
    public async Task<(decimal totalPoints, decimal totalHours, decimal donePoints, decimal doneHours, int itemCount)> GetSprintStatsAsync(int sprintId)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(@"
            SELECT COALESCE(SUM(points), 0), COALESCE(SUM(COALESCE(estimated_hours,0)), 0),
                   COALESCE(SUM(CASE WHEN status='Done' THEN points ELSE 0 END), 0),
                   COALESCE(SUM(CASE WHEN status='Done' THEN COALESCE(estimated_hours,0) ELSE 0 END), 0),
                   COUNT(*)
            FROM tasks WHERE committed_to = @sid OR sprint_id = @sid", conn);
        cmd.Parameters.AddWithValue("sid", sprintId);
        await using var rdr = await cmd.ExecuteReaderAsync();
        if (await rdr.ReadAsync())
            return (rdr.GetDecimal(0), rdr.GetDecimal(1), rdr.GetDecimal(2), rdr.GetDecimal(3), rdr.GetInt32(4));
        return (0, 0, 0, 0, 0);
    }

    /// <summary>Close a sprint: record velocity, snapshot burndown, optionally carry forward incomplete items.</summary>
    public async Task CloseSprintAsync(int sprintId, int? carryForwardToSprintId = null)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();

        // Snapshot final burndown
        await using (var snap = new NpgsqlCommand("SELECT snapshot_sprint_burndown(@sid)", conn))
        {
            snap.Parameters.AddWithValue("sid", sprintId);
            await snap.ExecuteNonQueryAsync();
        }

        // Calculate and store velocity
        await using (var vel = new NpgsqlCommand(@"
            UPDATE sprints SET status = 'Closed',
                velocity_points = (SELECT COALESCE(SUM(points),0) FROM tasks WHERE (committed_to=@sid OR sprint_id=@sid) AND status='Done'),
                velocity_hours = (SELECT COALESCE(SUM(COALESCE(estimated_hours,0)),0) FROM tasks WHERE (committed_to=@sid OR sprint_id=@sid) AND status='Done')
            WHERE id = @sid", conn))
        {
            vel.Parameters.AddWithValue("sid", sprintId);
            await vel.ExecuteNonQueryAsync();
        }

        // Carry forward incomplete items
        if (carryForwardToSprintId.HasValue)
        {
            await using var carry = new NpgsqlCommand(@"
                UPDATE tasks SET committed_to = @next, updated_at = now()
                WHERE (committed_to = @sid OR sprint_id = @sid) AND status != 'Done'", conn);
            carry.Parameters.AddWithValue("sid", sprintId);
            carry.Parameters.AddWithValue("next", carryForwardToSprintId.Value);
            await carry.ExecuteNonQueryAsync();
        }
    }

    // ════════════════════════════════════════════
    // Board Columns
    // ════════════════════════════════════════════

    public async Task<List<BoardColumn>> GetBoardColumnsAsync(int projectId, string boardName = "Default")
    {
        var list = new List<BoardColumn>();
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(@"
            SELECT bc.id, bc.project_id, bc.board_name, bc.column_name, bc.status_mapping,
                   bc.sort_order, bc.wip_limit, bc.color,
                   (SELECT COUNT(*) FROM tasks t WHERE t.project_id = bc.project_id
                    AND (t.board_column = bc.column_name OR (t.board_column IS NULL AND t.status = bc.status_mapping)))
            FROM board_columns bc
            WHERE bc.project_id = @pid AND bc.board_name = @bn
            ORDER BY bc.sort_order", conn);
        cmd.Parameters.AddWithValue("pid", projectId);
        cmd.Parameters.AddWithValue("bn", boardName);
        await using var rdr = await cmd.ExecuteReaderAsync();
        while (await rdr.ReadAsync())
            list.Add(new BoardColumn
            {
                Id = rdr.GetInt32(0), ProjectId = rdr.GetInt32(1), BoardName = rdr.GetString(2),
                ColumnName = rdr.GetString(3), StatusMapping = S(rdr, 4),
                SortOrder = rdr.GetInt32(5), WipLimit = rdr.IsDBNull(6) ? null : rdr.GetInt32(6),
                Color = S(rdr, 7), CurrentCount = rdr.GetInt32(8)
            });
        return list;
    }

    public async Task UpsertBoardColumnAsync(BoardColumn col)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        if (col.Id == 0)
        {
            await using var cmd = new NpgsqlCommand(@"
                INSERT INTO board_columns (project_id, board_name, column_name, status_mapping, sort_order, wip_limit, color)
                VALUES (@pid, @bn, @cn, @sm, @so, @wip, @clr) RETURNING id", conn);
            cmd.Parameters.AddWithValue("pid", col.ProjectId);
            cmd.Parameters.AddWithValue("bn", col.BoardName);
            cmd.Parameters.AddWithValue("cn", col.ColumnName);
            cmd.Parameters.AddWithValue("sm", (object?)col.StatusMapping ?? DBNull.Value);
            cmd.Parameters.AddWithValue("so", col.SortOrder);
            cmd.Parameters.AddWithValue("wip", (object?)col.WipLimit ?? DBNull.Value);
            cmd.Parameters.AddWithValue("clr", (object?)col.Color ?? DBNull.Value);
            col.Id = (int)(await cmd.ExecuteScalarAsync())!;
        }
        else
        {
            await using var cmd = new NpgsqlCommand(@"
                UPDATE board_columns SET column_name=@cn, status_mapping=@sm, sort_order=@so,
                    wip_limit=@wip, color=@clr WHERE id=@id", conn);
            cmd.Parameters.AddWithValue("id", col.Id);
            cmd.Parameters.AddWithValue("cn", col.ColumnName);
            cmd.Parameters.AddWithValue("sm", (object?)col.StatusMapping ?? DBNull.Value);
            cmd.Parameters.AddWithValue("so", col.SortOrder);
            cmd.Parameters.AddWithValue("wip", (object?)col.WipLimit ?? DBNull.Value);
            cmd.Parameters.AddWithValue("clr", (object?)col.Color ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    public async Task DeleteBoardColumnAsync(int id)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand("DELETE FROM board_columns WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>Move a task to a board column (updates board_column + status).</summary>
    public async Task MoveTaskToColumnAsync(int taskId, string columnName, string? statusMapping)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        var sql = statusMapping != null
            ? "UPDATE tasks SET board_column=@col, status=@st, updated_at=now(), completed_at = CASE WHEN @st='Done' THEN COALESCE(completed_at,now()) ELSE completed_at END WHERE id=@tid"
            : "UPDATE tasks SET board_column=@col, updated_at=now() WHERE id=@tid";
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("col", columnName);
        cmd.Parameters.AddWithValue("tid", taskId);
        if (statusMapping != null) cmd.Parameters.AddWithValue("st", statusMapping);
        await cmd.ExecuteNonQueryAsync();
    }

    // ════════════════════════════════════════════
    // Board Lanes
    // ════════════════════════════════════════════

    public async Task<List<BoardLane>> GetBoardLanesAsync(int projectId, string boardName = "Default")
    {
        var list = new List<BoardLane>();
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(@"
            SELECT id, project_id, board_name, lane_name, lane_field, sort_order
            FROM board_lanes WHERE project_id = @pid AND board_name = @bn
            ORDER BY sort_order", conn);
        cmd.Parameters.AddWithValue("pid", projectId);
        cmd.Parameters.AddWithValue("bn", boardName);
        await using var rdr = await cmd.ExecuteReaderAsync();
        while (await rdr.ReadAsync())
            list.Add(new BoardLane
            {
                Id = rdr.GetInt32(0), ProjectId = rdr.GetInt32(1), BoardName = rdr.GetString(2),
                LaneName = rdr.GetString(3), LaneField = S(rdr, 4), SortOrder = rdr.GetInt32(5)
            });
        return list;
    }

    // ════════════════════════════════════════════
    // QA / Bug Queries
    // ════════════════════════════════════════════

    // ════════════════════════════════════════════
    // Baselines
    // ════════════════════════════════════════════

    public async Task<List<TaskBaseline>> GetBaselinesAsync(int taskId)
    {
        var list = new List<TaskBaseline>();
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT id, task_id, baseline_name, start_date, finish_date, points, hours, saved_at FROM task_baselines WHERE task_id=@tid ORDER BY saved_at", conn);
        cmd.Parameters.AddWithValue("tid", taskId);
        await using var rdr = await cmd.ExecuteReaderAsync();
        while (await rdr.ReadAsync())
            list.Add(new TaskBaseline
            {
                Id = rdr.GetInt32(0), TaskId = rdr.GetInt32(1), BaselineName = rdr.GetString(2),
                StartDate = rdr.IsDBNull(3) ? null : rdr.GetDateTime(3),
                FinishDate = rdr.IsDBNull(4) ? null : rdr.GetDateTime(4),
                Points = rdr.IsDBNull(5) ? null : rdr.GetDecimal(5),
                Hours = rdr.IsDBNull(6) ? null : rdr.GetDecimal(6),
                SavedAt = rdr.GetDateTime(7)
            });
        return list;
    }

    public async Task<int> SaveProjectBaselineAsync(int projectId, string baselineName)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand("SELECT save_project_baseline(@pid, @bn)", conn);
        cmd.Parameters.AddWithValue("pid", projectId);
        cmd.Parameters.AddWithValue("bn", baselineName);
        var result = await cmd.ExecuteScalarAsync();
        return result is int cnt ? cnt : 0;
    }

    /// <summary>Get all dependencies as GanttPredecessorLink for DX GanttControl binding.</summary>
    public async Task<List<Central.Core.Models.GanttPredecessorLink>> GetGanttLinksAsync(int? projectId = null)
    {
        var list = new List<Central.Core.Models.GanttPredecessorLink>();
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        var where = projectId.HasValue
            ? " WHERE td.predecessor_id IN (SELECT id FROM tasks WHERE project_id=@pid) OR td.successor_id IN (SELECT id FROM tasks WHERE project_id=@pid)"
            : "";
        await using var cmd = new NpgsqlCommand($@"
            SELECT td.predecessor_id, td.successor_id, td.dep_type, td.lag_days
            FROM task_dependencies td {where}", conn);
        if (projectId.HasValue) cmd.Parameters.AddWithValue("pid", projectId.Value);
        await using var rdr = await cmd.ExecuteReaderAsync();
        while (await rdr.ReadAsync())
        {
            var depType = rdr.GetString(2) switch
            {
                "FS" => 0, "FF" => 1, "SS" => 2, "SF" => 3, _ => 0
            };
            list.Add(new Central.Core.Models.GanttPredecessorLink
            {
                PredecessorTaskId = rdr.GetInt32(0), SuccessorTaskId = rdr.GetInt32(1),
                LinkType = depType, Lag = rdr.GetInt32(3)
            });
        }
        return list;
    }

    /// <summary>Get all bugs (task_type='Bug'), optionally filtered by project.</summary>
    public async Task<List<Central.Core.Models.TaskItem>> GetBugsAsync(int? projectId = null)
    {
        // Reuse GetTasksAsync with post-filter — bugs use the same schema
        var all = await GetTasksAsync(projectId);
        return all.Where(t => t.TaskType == "Bug").ToList();
    }

    /// <summary>Batch triage: set severity + bug_priority + status='Triaged' on multiple bugs.</summary>
    public async Task BatchTriageBugsAsync(List<int> taskIds, string severity, string bugPriority)
    {
        if (taskIds.Count == 0) return;
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        var idList = string.Join(",", taskIds);
        await using var cmd = new NpgsqlCommand($@"
            UPDATE tasks SET severity=@sev, bug_priority=@bp,
                status = CASE WHEN status = 'Open' OR status = 'New' THEN 'Triaged' ELSE status END,
                updated_at = now()
            WHERE id = ANY(@ids) AND task_type = 'Bug'", conn);
        cmd.Parameters.AddWithValue("sev", severity);
        cmd.Parameters.AddWithValue("bp", bugPriority);
        cmd.Parameters.AddWithValue("ids", taskIds.ToArray());
        await cmd.ExecuteNonQueryAsync();
    }

    // ════════════════════════════════════════════
    // Saved Reports
    // ════════════════════════════════════════════

    public async Task<List<SavedReport>> GetSavedReportsAsync(int? projectId = null)
    {
        var list = new List<SavedReport>();
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        var where = projectId.HasValue ? " WHERE r.project_id = @pid OR r.project_id IS NULL" : "";
        await using var cmd = new NpgsqlCommand($@"
            SELECT r.id, r.project_id, r.name, r.folder, r.query_json::text,
                   r.created_by, COALESCE(u.display_name,''), r.shared_with::text,
                   r.created_at, r.updated_at
            FROM saved_reports r LEFT JOIN app_users u ON u.id = r.created_by
            {where} ORDER BY r.folder, r.name", conn);
        if (projectId.HasValue) cmd.Parameters.AddWithValue("pid", projectId.Value);
        await using var rdr = await cmd.ExecuteReaderAsync();
        while (await rdr.ReadAsync())
            list.Add(new SavedReport
            {
                Id = rdr.GetInt32(0), ProjectId = rdr.IsDBNull(1) ? null : rdr.GetInt32(1),
                Name = rdr.GetString(2), Folder = S(rdr, 3), QueryJson = rdr.GetString(4),
                CreatedBy = rdr.IsDBNull(5) ? null : rdr.GetInt32(5), CreatedByName = rdr.GetString(6),
                SharedWith = S(rdr, 7), CreatedAt = rdr.GetDateTime(8), UpdatedAt = rdr.GetDateTime(9)
            });
        return list;
    }

    public async Task UpsertSavedReportAsync(SavedReport r)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        if (r.Id == 0)
        {
            await using var cmd = new NpgsqlCommand(@"
                INSERT INTO saved_reports (project_id, name, folder, query_json, created_by, shared_with)
                VALUES (@pid, @name, @folder, @qj, @cb, @sw) RETURNING id", conn);
            cmd.Parameters.AddWithValue("pid", (object?)r.ProjectId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("name", r.Name);
            cmd.Parameters.AddWithValue("folder", (object?)r.Folder ?? DBNull.Value);
            cmd.Parameters.Add(JsonbParam("qj", r.QueryJson));
            cmd.Parameters.AddWithValue("cb", (object?)r.CreatedBy ?? DBNull.Value);
            cmd.Parameters.Add(JsonbParam("sw", r.SharedWith));
            r.Id = (int)(await cmd.ExecuteScalarAsync())!;
        }
        else
        {
            await using var cmd = new NpgsqlCommand(@"
                UPDATE saved_reports SET name=@name, folder=@folder, query_json=@qj,
                    shared_with=@sw, updated_at=now() WHERE id=@id", conn);
            cmd.Parameters.AddWithValue("id", r.Id);
            cmd.Parameters.AddWithValue("name", r.Name);
            cmd.Parameters.AddWithValue("folder", (object?)r.Folder ?? DBNull.Value);
            cmd.Parameters.Add(JsonbParam("qj", r.QueryJson));
            cmd.Parameters.Add(JsonbParam("sw", r.SharedWith));
            await cmd.ExecuteNonQueryAsync();
        }
    }

    public async Task DeleteSavedReportAsync(int id)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand("DELETE FROM saved_reports WHERE id=@id", conn);
        cmd.Parameters.AddWithValue("id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    // ════════════════════════════════════════════
    // Dashboards
    // ════════════════════════════════════════════

    public async Task<List<Central.Core.Models.Dashboard>> GetDashboardsAsync()
    {
        var list = new List<Central.Core.Models.Dashboard>();
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(@"
            SELECT d.id, d.name, d.layout_json::text, d.template,
                   d.created_by, COALESCE(u.display_name,''), d.shared_with::text,
                   d.created_at, d.updated_at
            FROM dashboards d LEFT JOIN app_users u ON u.id = d.created_by
            ORDER BY d.name", conn);
        await using var rdr = await cmd.ExecuteReaderAsync();
        while (await rdr.ReadAsync())
            list.Add(new Central.Core.Models.Dashboard
            {
                Id = rdr.GetInt32(0), Name = rdr.GetString(1), LayoutJson = rdr.GetString(2),
                Template = S(rdr, 3), CreatedBy = rdr.IsDBNull(4) ? null : rdr.GetInt32(4),
                CreatedByName = rdr.GetString(5), SharedWith = S(rdr, 6),
                CreatedAt = rdr.GetDateTime(7), UpdatedAt = rdr.GetDateTime(8)
            });
        return list;
    }

    public async Task UpsertDashboardAsync(Central.Core.Models.Dashboard d)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        if (d.Id == 0)
        {
            await using var cmd = new NpgsqlCommand(@"
                INSERT INTO dashboards (name, layout_json, template, created_by, shared_with)
                VALUES (@name, @lj, @tmpl, @cb, @sw) RETURNING id", conn);
            cmd.Parameters.AddWithValue("name", d.Name);
            cmd.Parameters.Add(JsonbParam("lj", d.LayoutJson));
            cmd.Parameters.AddWithValue("tmpl", (object?)d.Template ?? DBNull.Value);
            cmd.Parameters.AddWithValue("cb", (object?)d.CreatedBy ?? DBNull.Value);
            cmd.Parameters.Add(JsonbParam("sw", d.SharedWith));
            d.Id = (int)(await cmd.ExecuteScalarAsync())!;
        }
        else
        {
            await using var cmd = new NpgsqlCommand(@"
                UPDATE dashboards SET name=@name, layout_json=@lj, template=@tmpl,
                    shared_with=@sw, updated_at=now() WHERE id=@id", conn);
            cmd.Parameters.AddWithValue("id", d.Id);
            cmd.Parameters.AddWithValue("name", d.Name);
            cmd.Parameters.Add(JsonbParam("lj", d.LayoutJson));
            cmd.Parameters.AddWithValue("tmpl", (object?)d.Template ?? DBNull.Value);
            cmd.Parameters.Add(JsonbParam("sw", d.SharedWith));
            await cmd.ExecuteNonQueryAsync();
        }
    }

    public async Task DeleteDashboardAsync(int id)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand("DELETE FROM dashboards WHERE id=@id", conn);
        cmd.Parameters.AddWithValue("id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    // ════════════════════════════════════════════
    // Custom Columns
    // ════════════════════════════════════════════

    public async Task<List<CustomColumn>> GetCustomColumnsAsync(int projectId)
    {
        var list = new List<CustomColumn>();
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT id, project_id, name, column_type, config, sort_order, default_value, is_required FROM custom_columns WHERE project_id=@pid ORDER BY sort_order", conn);
        cmd.Parameters.AddWithValue("pid", projectId);
        await using var rdr = await cmd.ExecuteReaderAsync();
        while (await rdr.ReadAsync())
            list.Add(new CustomColumn
            {
                Id = rdr.GetInt32(0), ProjectId = rdr.GetInt32(1), Name = rdr.GetString(2),
                ColumnType = rdr.GetString(3), Config = S(rdr, 4),
                SortOrder = rdr.GetInt32(5), DefaultValue = S(rdr, 6),
                IsRequired = rdr.GetBoolean(7)
            });
        return list;
    }

    public async Task UpsertCustomColumnAsync(CustomColumn col)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        if (col.Id == 0)
        {
            await using var cmd = new NpgsqlCommand(@"
                INSERT INTO custom_columns (project_id, name, column_type, config, sort_order, default_value, is_required)
                VALUES (@pid, @name, @type, @cfg, @so, @def, @req) RETURNING id", conn);
            cmd.Parameters.AddWithValue("pid", col.ProjectId);
            cmd.Parameters.AddWithValue("name", col.Name);
            cmd.Parameters.AddWithValue("type", col.ColumnType);
            cmd.Parameters.Add(JsonbParam("cfg", col.Config));
            cmd.Parameters.AddWithValue("so", col.SortOrder);
            cmd.Parameters.AddWithValue("def", (object?)col.DefaultValue ?? DBNull.Value);
            cmd.Parameters.AddWithValue("req", col.IsRequired);
            col.Id = (int)(await cmd.ExecuteScalarAsync())!;
        }
        else
        {
            await using var cmd = new NpgsqlCommand(@"
                UPDATE custom_columns SET name=@name, column_type=@type, config=@cfg,
                    sort_order=@so, default_value=@def, is_required=@req WHERE id=@id", conn);
            cmd.Parameters.AddWithValue("id", col.Id);
            cmd.Parameters.AddWithValue("name", col.Name);
            cmd.Parameters.AddWithValue("type", col.ColumnType);
            cmd.Parameters.Add(JsonbParam("cfg", col.Config));
            cmd.Parameters.AddWithValue("so", col.SortOrder);
            cmd.Parameters.AddWithValue("def", (object?)col.DefaultValue ?? DBNull.Value);
            cmd.Parameters.AddWithValue("req", col.IsRequired);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    public async Task DeleteCustomColumnAsync(int id)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand("DELETE FROM custom_columns WHERE id=@id", conn);
        cmd.Parameters.AddWithValue("id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    // ── Custom Column Values ──

    public async Task<List<TaskCustomValue>> GetCustomValuesAsync(int taskId)
    {
        var list = new List<TaskCustomValue>();
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(@"
            SELECT cv.task_id, cv.column_id, cc.name, cc.column_type,
                   cv.value_text, cv.value_number, cv.value_date, cv.value_json::text
            FROM task_custom_values cv
            JOIN custom_columns cc ON cc.id = cv.column_id
            WHERE cv.task_id = @tid ORDER BY cc.sort_order", conn);
        cmd.Parameters.AddWithValue("tid", taskId);
        await using var rdr = await cmd.ExecuteReaderAsync();
        while (await rdr.ReadAsync())
            list.Add(new TaskCustomValue
            {
                TaskId = rdr.GetInt32(0), ColumnId = rdr.GetInt32(1),
                ColumnName = rdr.GetString(2), ColumnType = rdr.GetString(3),
                ValueText = S(rdr, 4),
                ValueNumber = rdr.IsDBNull(5) ? null : rdr.GetDecimal(5),
                ValueDate = rdr.IsDBNull(6) ? null : rdr.GetDateTime(6),
                ValueJson = S(rdr, 7)
            });
        return list;
    }

    /// <summary>Get all custom values for all tasks in a project (for bulk grid loading).</summary>
    public async Task<Dictionary<int, Dictionary<string, string>>> GetAllCustomValuesAsync(int projectId)
    {
        var result = new Dictionary<int, Dictionary<string, string>>();
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(@"
            SELECT cv.task_id, cc.name, cc.column_type,
                   cv.value_text, cv.value_number, cv.value_date
            FROM task_custom_values cv
            JOIN custom_columns cc ON cc.id = cv.column_id
            WHERE cc.project_id = @pid", conn);
        cmd.Parameters.AddWithValue("pid", projectId);
        await using var rdr = await cmd.ExecuteReaderAsync();
        while (await rdr.ReadAsync())
        {
            var taskId = rdr.GetInt32(0);
            var colName = rdr.GetString(1);
            var colType = rdr.GetString(2);
            var display = colType switch
            {
                "Number" or "Hours" => rdr.IsDBNull(4) ? "" : rdr.GetDecimal(4).ToString("N2"),
                "Date" => rdr.IsDBNull(5) ? "" : rdr.GetDateTime(5).ToString("yyyy-MM-dd"),
                "DateTime" => rdr.IsDBNull(5) ? "" : rdr.GetDateTime(5).ToString("yyyy-MM-dd HH:mm"),
                _ => S(rdr, 3)
            };
            if (!result.ContainsKey(taskId)) result[taskId] = new Dictionary<string, string>();
            result[taskId][colName] = display;
        }
        return result;
    }

    public async Task UpsertCustomValueAsync(int taskId, int columnId, string? text, decimal? number, DateTime? date, string? json)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(@"
            INSERT INTO task_custom_values (task_id, column_id, value_text, value_number, value_date, value_json)
            VALUES (@tid, @cid, @txt, @num, @dt, @js)
            ON CONFLICT (task_id, column_id) DO UPDATE SET
                value_text=@txt, value_number=@num, value_date=@dt, value_json=@js", conn);
        cmd.Parameters.AddWithValue("tid", taskId);
        cmd.Parameters.AddWithValue("cid", columnId);
        cmd.Parameters.AddWithValue("txt", (object?)text ?? DBNull.Value);
        cmd.Parameters.AddWithValue("num", (object?)number ?? DBNull.Value);
        cmd.Parameters.AddWithValue("dt", (object?)date ?? DBNull.Value);
        cmd.Parameters.Add(JsonbParam("js", json));
        await cmd.ExecuteNonQueryAsync();
    }

    // ── Custom Column Permissions ──

    public async Task<List<CustomColumnPermission>> GetCustomColumnPermissionsAsync(int columnId)
    {
        var list = new List<CustomColumnPermission>();
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT id, column_id, user_id, group_name, can_view, can_edit FROM custom_column_permissions WHERE column_id=@cid", conn);
        cmd.Parameters.AddWithValue("cid", columnId);
        await using var rdr = await cmd.ExecuteReaderAsync();
        while (await rdr.ReadAsync())
            list.Add(new CustomColumnPermission
            {
                Id = rdr.GetInt32(0), ColumnId = rdr.GetInt32(1),
                UserId = rdr.IsDBNull(2) ? null : rdr.GetInt32(2),
                GroupName = S(rdr, 3), CanView = rdr.GetBoolean(4), CanEdit = rdr.GetBoolean(5)
            });
        return list;
    }

    public async Task UpsertCustomColumnPermissionAsync(CustomColumnPermission p)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        if (p.Id == 0)
        {
            await using var cmd = new NpgsqlCommand(@"
                INSERT INTO custom_column_permissions (column_id, user_id, group_name, can_view, can_edit)
                VALUES (@cid, @uid, @gn, @cv, @ce) RETURNING id", conn);
            cmd.Parameters.AddWithValue("cid", p.ColumnId);
            cmd.Parameters.AddWithValue("uid", (object?)p.UserId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("gn", (object?)p.GroupName ?? DBNull.Value);
            cmd.Parameters.AddWithValue("cv", p.CanView);
            cmd.Parameters.AddWithValue("ce", p.CanEdit);
            p.Id = (int)(await cmd.ExecuteScalarAsync())!;
        }
        else
        {
            await using var cmd = new NpgsqlCommand(
                "UPDATE custom_column_permissions SET can_view=@cv, can_edit=@ce WHERE id=@id", conn);
            cmd.Parameters.AddWithValue("id", p.Id);
            cmd.Parameters.AddWithValue("cv", p.CanView);
            cmd.Parameters.AddWithValue("ce", p.CanEdit);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    // ════════════════════════════════════════════
    // Time Entries
    // ════════════════════════════════════════════

    public async Task<List<TimeEntry>> GetTimeEntriesAsync(int? userId = null, DateTime? from = null, DateTime? to = null)
    {
        var list = new List<TimeEntry>();
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        var conditions = new List<string>();
        if (userId.HasValue) conditions.Add("te.user_id = @uid");
        if (from.HasValue) conditions.Add("te.entry_date >= @from");
        if (to.HasValue) conditions.Add("te.entry_date <= @to");
        var where = conditions.Count > 0 ? " WHERE " + string.Join(" AND ", conditions) : "";
        await using var cmd = new NpgsqlCommand($@"
            SELECT te.id, te.task_id, COALESCE(t.title,''), te.user_id, COALESCE(u.display_name,''),
                   te.entry_date, te.hours, te.activity_type, te.notes, te.created_at
            FROM time_entries te
            LEFT JOIN tasks t ON t.id = te.task_id
            LEFT JOIN app_users u ON u.id = te.user_id
            {where} ORDER BY te.entry_date DESC, te.created_at DESC", conn);
        if (userId.HasValue) cmd.Parameters.AddWithValue("uid", userId.Value);
        if (from.HasValue) cmd.Parameters.AddWithValue("from", from.Value);
        if (to.HasValue) cmd.Parameters.AddWithValue("to", to.Value);
        await using var rdr = await cmd.ExecuteReaderAsync();
        while (await rdr.ReadAsync())
            list.Add(new TimeEntry
            {
                Id = rdr.GetInt32(0), TaskId = rdr.GetInt32(1), TaskTitle = rdr.GetString(2),
                UserId = rdr.GetInt32(3), UserName = rdr.GetString(4),
                EntryDate = rdr.GetDateTime(5), Hours = rdr.GetDecimal(6),
                ActivityType = S(rdr, 7), Notes = S(rdr, 8), CreatedAt = rdr.GetDateTime(9)
            });
        return list;
    }

    public async Task UpsertTimeEntryAsync(TimeEntry e)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        if (e.Id == 0)
        {
            await using var cmd = new NpgsqlCommand(@"
                INSERT INTO time_entries (task_id, user_id, entry_date, hours, activity_type, notes)
                VALUES (@tid, @uid, @dt, @hrs, @at, @notes) RETURNING id", conn);
            cmd.Parameters.AddWithValue("tid", e.TaskId);
            cmd.Parameters.AddWithValue("uid", e.UserId);
            cmd.Parameters.AddWithValue("dt", e.EntryDate);
            cmd.Parameters.AddWithValue("hrs", e.Hours);
            cmd.Parameters.AddWithValue("at", (object?)e.ActivityType ?? DBNull.Value);
            cmd.Parameters.AddWithValue("notes", (object?)e.Notes ?? DBNull.Value);
            e.Id = (int)(await cmd.ExecuteScalarAsync())!;
        }
        else
        {
            await using var cmd = new NpgsqlCommand(@"
                UPDATE time_entries SET hours=@hrs, activity_type=@at, notes=@notes WHERE id=@id", conn);
            cmd.Parameters.AddWithValue("id", e.Id);
            cmd.Parameters.AddWithValue("hrs", e.Hours);
            cmd.Parameters.AddWithValue("at", (object?)e.ActivityType ?? DBNull.Value);
            cmd.Parameters.AddWithValue("notes", (object?)e.Notes ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    public async Task DeleteTimeEntryAsync(int id)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand("DELETE FROM time_entries WHERE id=@id", conn);
        cmd.Parameters.AddWithValue("id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    // ════════════════════════════════════════════
    // Activity Feed
    // ════════════════════════════════════════════

    public async Task<List<ActivityFeedItem>> GetActivityFeedAsync(int? projectId = null, int limit = 100)
    {
        var list = new List<ActivityFeedItem>();
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        var where = projectId.HasValue ? " WHERE af.project_id = @pid" : "";
        await using var cmd = new NpgsqlCommand($@"
            SELECT af.id, af.project_id, af.task_id, af.user_id,
                   COALESCE(af.user_name, COALESCE(u.display_name,'')),
                   af.action, af.summary, COALESCE(af.details::text,''), af.created_at
            FROM activity_feed af LEFT JOIN app_users u ON u.id = af.user_id
            {where} ORDER BY af.created_at DESC LIMIT @lim", conn);
        if (projectId.HasValue) cmd.Parameters.AddWithValue("pid", projectId.Value);
        cmd.Parameters.AddWithValue("lim", limit);
        await using var rdr = await cmd.ExecuteReaderAsync();
        while (await rdr.ReadAsync())
            list.Add(new ActivityFeedItem
            {
                Id = rdr.GetInt32(0), ProjectId = rdr.IsDBNull(1) ? null : rdr.GetInt32(1),
                TaskId = rdr.IsDBNull(2) ? null : rdr.GetInt32(2),
                UserId = rdr.IsDBNull(3) ? null : rdr.GetInt32(3),
                UserName = rdr.GetString(4), Action = rdr.GetString(5),
                Summary = S(rdr, 6), Details = rdr.GetString(7), CreatedAt = rdr.GetDateTime(8)
            });
        return list;
    }

    // ════════════════════════════════════════════
    // Saved Views
    // ════════════════════════════════════════════

    public async Task<List<TaskViewConfig>> GetTaskViewsAsync(int? projectId = null)
    {
        var list = new List<TaskViewConfig>();
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        var where = projectId.HasValue ? " WHERE project_id = @pid" : "";
        await using var cmd = new NpgsqlCommand($"SELECT id, project_id, name, view_type, config_json::text, created_by, is_default, shared_with::text, created_at FROM task_views {where} ORDER BY name", conn);
        if (projectId.HasValue) cmd.Parameters.AddWithValue("pid", projectId.Value);
        await using var rdr = await cmd.ExecuteReaderAsync();
        while (await rdr.ReadAsync())
            list.Add(new TaskViewConfig
            {
                Id = rdr.GetInt32(0), ProjectId = rdr.IsDBNull(1) ? null : rdr.GetInt32(1),
                Name = rdr.GetString(2), ViewType = rdr.GetString(3), ConfigJson = rdr.GetString(4),
                CreatedBy = rdr.IsDBNull(5) ? null : rdr.GetInt32(5),
                IsDefault = rdr.GetBoolean(6), SharedWith = S(rdr, 7), CreatedAt = rdr.GetDateTime(8)
            });
        return list;
    }

    public async Task UpsertTaskViewAsync(TaskViewConfig v)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        if (v.Id == 0)
        {
            await using var cmd = new NpgsqlCommand(@"
                INSERT INTO task_views (project_id, name, view_type, config_json, created_by, is_default, shared_with)
                VALUES (@pid, @name, @vt, @cj, @cb, @def, @sw) RETURNING id", conn);
            cmd.Parameters.AddWithValue("pid", (object?)v.ProjectId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("name", v.Name);
            cmd.Parameters.AddWithValue("vt", v.ViewType);
            cmd.Parameters.Add(JsonbParam("cj", v.ConfigJson));
            cmd.Parameters.AddWithValue("cb", (object?)v.CreatedBy ?? DBNull.Value);
            cmd.Parameters.AddWithValue("def", v.IsDefault);
            cmd.Parameters.Add(JsonbParam("sw", v.SharedWith));
            v.Id = (int)(await cmd.ExecuteScalarAsync())!;
        }
        else
        {
            await using var cmd = new NpgsqlCommand(@"
                UPDATE task_views SET name=@name, config_json=@cj, is_default=@def, shared_with=@sw WHERE id=@id", conn);
            cmd.Parameters.AddWithValue("id", v.Id);
            cmd.Parameters.AddWithValue("name", v.Name);
            cmd.Parameters.Add(JsonbParam("cj", v.ConfigJson));
            cmd.Parameters.AddWithValue("def", v.IsDefault);
            cmd.Parameters.Add(JsonbParam("sw", v.SharedWith));
            await cmd.ExecuteNonQueryAsync();
        }
    }
}
