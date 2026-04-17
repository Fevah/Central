using Npgsql;
using Central.Core.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Central.Data;

public partial class DbRepository
{
    private readonly string _connectionString;

    /// <summary>
    /// Tenant ID for RLS context. Default = single-tenant mode.
    /// Set via SetTenantId() after auth establishes the session.
    /// </summary>
    public static string TenantId { get; private set; } = "00000000-0000-0000-0000-000000000001";

    public static void SetTenantId(string tenantId) => TenantId = tenantId;

    public DbRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    /// <summary>
    /// Opens a connection and sets the RLS tenant context.
    /// Use this instead of raw new NpgsqlConnection + OpenAsync.
    /// </summary>
    protected async Task<NpgsqlConnection> OpenConnectionAsync()
    {
        var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand("SELECT set_config('app.tenant_id', @tid, false)", conn);
        cmd.Parameters.AddWithValue("tid", TenantId);
        await cmd.ExecuteNonQueryAsync();
        return conn;
    }

    // ── Devices / Device Inventory ────────────────────────────────────────────

    public async Task<List<DeviceRecord>> GetDevicesAsync(List<string>? allowedSites = null, bool excludeReserved = false)
    {
        var list = new List<DeviceRecord>();
        await using var conn = await OpenConnectionAsync();

        var conditions = new System.Collections.Generic.List<string>();
        if (allowedSites != null && allowedSites.Count > 0)
            conditions.Add("sg.building = ANY(@sites)");
        else if (allowedSites != null && allowedSites.Count == 0)
            conditions.Add("FALSE");
        if (excludeReserved)
            conditions.Add("sg.status <> 'RESERVED'");
        var where = conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : "";

        var sql = $"""
            SELECT
                sg.id::text,
                sg.switch_name,
                COALESCE(sg.site, ''),
                sg.device_type,
                sg.building,
                sg.region,
                sg.status,
                COALESCE(NULLIF(sg.primary_ip,''), cast(sg.ip as text), ''),
                cast(sg.management_ip as text),
                cast(sg.mgmt_l3_ip   as text),
                cast(sg.loopback_ip  as text),
                sg.loopback_subnet::text,
                sg.asn,
                sg.mlag_domain,
                sg.ae_range,
                COALESCE(sg.floor,         ''),
                COALESCE(sg.rack,          ''),
                COALESCE(sg.model,         ''),
                COALESCE(sg.serial_number, ''),
                COALESCE(sg.uplink_switch, ''),
                COALESCE(sg.uplink_port,   ''),
                sg.notes,
                sw.hostname AS linked_hostname
            FROM switch_guide sg
            LEFT JOIN switches sw ON UPPER(sw.hostname) = UPPER(sg.switch_name)
            {where}
            ORDER BY sg.building, sg.device_type, sg.switch_name
            """;

        await using var cmd = new NpgsqlCommand(sql, conn);
        if (allowedSites != null && allowedSites.Count > 0)
            cmd.Parameters.AddWithValue("@sites", allowedSites.ToArray());
        await using var rdr = await cmd.ExecuteReaderAsync();
        while (await rdr.ReadAsync())
        {
            list.Add(new DeviceRecord
            {
                Id             = rdr.GetString(0),
                SwitchName     = rdr.IsDBNull(1)  ? "" : rdr.GetString(1),
                Site           = rdr.IsDBNull(2)  ? "" : rdr.GetString(2),
                DeviceType     = rdr.IsDBNull(3)  ? "" : rdr.GetString(3),
                Building       = rdr.IsDBNull(4)  ? "" : rdr.GetString(4),
                Region         = rdr.IsDBNull(5)  ? "" : rdr.GetString(5),
                Status         = rdr.IsDBNull(6)  ? "" : rdr.GetString(6),
                Ip             = rdr.IsDBNull(7)  ? "" : rdr.GetString(7),
                ManagementIp   = rdr.IsDBNull(8)  ? "" : rdr.GetString(8),
                MgmtL3Ip       = rdr.IsDBNull(9)  ? "" : rdr.GetString(9),
                LoopbackIp     = rdr.IsDBNull(10) ? "" : rdr.GetString(10),
                LoopbackSubnet = rdr.IsDBNull(11) ? "" : rdr.GetString(11),
                Asn            = rdr.IsDBNull(12) ? "" : rdr.GetString(12),
                MlagDomain     = rdr.IsDBNull(13) ? "" : rdr.GetString(13),
                AeRange        = rdr.IsDBNull(14) ? "" : rdr.GetString(14),
                Floor          = rdr.IsDBNull(15) ? "" : rdr.GetString(15),
                Rack           = rdr.IsDBNull(16) ? "" : rdr.GetString(16),
                Model          = rdr.IsDBNull(17) ? "" : rdr.GetString(17),
                SerialNumber   = rdr.IsDBNull(18) ? "" : rdr.GetString(18),
                UplinkSwitch   = rdr.IsDBNull(19) ? "" : rdr.GetString(19),
                UplinkPort     = rdr.IsDBNull(20) ? "" : rdr.GetString(20),
                Notes          = rdr.IsDBNull(21) ? "" : rdr.GetString(21),
                LinkedHostname = rdr.IsDBNull(22) ? "" : rdr.GetString(22),
            });
        }
        return list;
    }

    public async Task InsertDeviceAsync(DeviceRecord d)
    {
        await using var conn = await OpenConnectionAsync();
        const string sql = """
            INSERT INTO switch_guide
                (switch_name, site, device_type, building, region, status,
                 ip, management_ip, mgmt_l3_ip, loopback_ip, loopback_subnet,
                 asn, mlag_domain, ae_range, floor, rack, model, serial_number,
                 uplink_switch, uplink_port, notes)
            VALUES
                (@name, @site, @dtype, @bldg, @region, @status,
                 @ip::inet, @mgmt::inet, @ml3::inet, @lb::inet, @lbs::cidr,
                 @asn, @mlag, @ae, @floor, @rack, @model, @sn,
                 @us, @up, @notes)
            RETURNING id::text
            """;
        await using var cmd = new NpgsqlCommand(sql, conn);
        AddDeviceParams(cmd, d);
        d.Id = (string)(await cmd.ExecuteScalarAsync())!;
    }

    public async Task UpdateDeviceAsync(DeviceRecord d)
    {
        await using var conn = await OpenConnectionAsync();
        const string sql = """
            UPDATE switch_guide SET
                switch_name    = @name,
                site           = @site,
                device_type    = @dtype,
                building       = @bldg,
                region         = @region,
                status         = @status,
                ip             = @ip::inet,
                management_ip  = @mgmt::inet,
                mgmt_l3_ip     = @ml3::inet,
                loopback_ip    = @lb::inet,
                loopback_subnet= @lbs::cidr,
                asn            = @asn,
                mlag_domain    = @mlag,
                ae_range       = @ae,
                floor          = @floor,
                rack           = @rack,
                model          = @model,
                serial_number  = @sn,
                uplink_switch  = @us,
                uplink_port    = @up,
                notes          = @notes
            WHERE id = @id::uuid
            """;
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("@id", d.Id);
        AddDeviceParams(cmd, d);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteDeviceAsync(string id)
    {
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            "DELETE FROM switch_guide WHERE id = @id::uuid", conn);
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    private static void AddDeviceParams(NpgsqlCommand cmd, DeviceRecord d)
    {
        static object Nul(string s) => string.IsNullOrWhiteSpace(s) ? DBNull.Value : s;

        cmd.Parameters.AddWithValue("@name",   Nul(d.SwitchName));
        cmd.Parameters.AddWithValue("@site",   Nul(d.Site));
        cmd.Parameters.AddWithValue("@dtype",  Nul(d.DeviceType));
        cmd.Parameters.AddWithValue("@bldg",   Nul(d.Building));
        cmd.Parameters.AddWithValue("@region", Nul(d.Region));
        cmd.Parameters.AddWithValue("@status", Nul(d.Status));
        cmd.Parameters.AddWithValue("@ip",     Nul(d.Ip));
        cmd.Parameters.AddWithValue("@mgmt",   Nul(d.ManagementIp));
        cmd.Parameters.AddWithValue("@ml3",    Nul(d.MgmtL3Ip));
        cmd.Parameters.AddWithValue("@lb",     Nul(d.LoopbackIp));
        cmd.Parameters.AddWithValue("@lbs",    Nul(d.LoopbackSubnet));
        cmd.Parameters.AddWithValue("@asn",    Nul(d.Asn));
        cmd.Parameters.AddWithValue("@mlag",   Nul(d.MlagDomain));
        cmd.Parameters.AddWithValue("@ae",     Nul(d.AeRange));
        cmd.Parameters.AddWithValue("@floor",  Nul(d.Floor));
        cmd.Parameters.AddWithValue("@rack",   Nul(d.Rack));
        cmd.Parameters.AddWithValue("@model",  Nul(d.Model));
        cmd.Parameters.AddWithValue("@sn",     Nul(d.SerialNumber));
        cmd.Parameters.AddWithValue("@us",     Nul(d.UplinkSwitch));
        cmd.Parameters.AddWithValue("@up",     Nul(d.UplinkPort));
        cmd.Parameters.AddWithValue("@notes",  Nul(d.Notes));
    }

    // ── Lookup Values (Admin) ──────────────────────────────────────────────

    public async Task<Dictionary<string, List<string>>> GetLookupsAsync()
    {
        var result = new Dictionary<string, List<string>>();
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT category, value FROM lookup_values ORDER BY category, sort_order, value", conn);
        await using var rdr = await cmd.ExecuteReaderAsync();
        while (await rdr.ReadAsync())
        {
            var cat = rdr.GetString(0);
            var val = rdr.GetString(1);
            if (!result.ContainsKey(cat)) result[cat] = new List<string>();
            result[cat].Add(val);
        }
        return result;
    }

    public async Task<List<LookupItem>> GetLookupItemsAsync()
    {
        var list = new List<LookupItem>();
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT id, category, value, sort_order, COALESCE(grid_name,''), COALESCE(module,'') FROM lookup_values ORDER BY category, sort_order, value", conn);
        await using var rdr = await cmd.ExecuteReaderAsync();
        while (await rdr.ReadAsync())
            list.Add(new LookupItem
            {
                Id        = rdr.GetInt32(0),
                Category  = rdr.GetString(1),
                Value     = rdr.GetString(2),
                SortOrder = rdr.GetInt32(3),
                GridName  = rdr.GetString(4),
                Module    = rdr.GetString(5),
            });
        return list;
    }

    public async Task UpsertLookupAsync(LookupItem item)
    {
        await using var conn = await OpenConnectionAsync();
        if (item.Id == 0)
        {
            const string sql = """
                INSERT INTO lookup_values (category, value, sort_order, grid_name, module)
                VALUES (@cat, @val, @ord, @grid, @mod)
                ON CONFLICT (category, value) DO UPDATE SET sort_order = EXCLUDED.sort_order, grid_name = EXCLUDED.grid_name, module = EXCLUDED.module
                RETURNING id
                """;
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@cat",  item.Category);
            cmd.Parameters.AddWithValue("@val",  item.Value);
            cmd.Parameters.AddWithValue("@ord",  item.SortOrder);
            cmd.Parameters.AddWithValue("@grid", item.GridName);
            cmd.Parameters.AddWithValue("@mod",  item.Module);
            item.Id = (int)(await cmd.ExecuteScalarAsync())!;
        }
        else
        {
            const string sql = "UPDATE lookup_values SET category=@cat, value=@val, sort_order=@ord, grid_name=@grid, module=@mod WHERE id=@id";
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@cat",  item.Category);
            cmd.Parameters.AddWithValue("@val",  item.Value);
            cmd.Parameters.AddWithValue("@ord",  item.SortOrder);
            cmd.Parameters.AddWithValue("@grid", item.GridName);
            cmd.Parameters.AddWithValue("@mod",  item.Module);
            cmd.Parameters.AddWithValue("@id",   item.Id);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    public async Task DeleteLookupAsync(int id)
    {
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand("DELETE FROM lookup_values WHERE id=@id", conn);
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    // ── Config Ranges (Settings) ────────────────────────────────────────────

    public async Task<List<ConfigRange>> GetConfigRangesAsync()
    {
        var list = new List<ConfigRange>();
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT id, category, name, range_start, range_end, description, sort_order FROM config_ranges ORDER BY category, sort_order", conn);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            list.Add(new ConfigRange
            {
                Id          = r.GetInt32(0),
                Category    = r.GetString(1),
                Name        = r.GetString(2),
                RangeStart  = r.GetString(3),
                RangeEnd    = r.GetString(4),
                Description = r.IsDBNull(5) ? "" : r.GetString(5),
                SortOrder   = r.GetInt32(6)
            });
        }
        return list;
    }

    public async Task UpsertConfigRangeAsync(ConfigRange item)
    {
        await using var conn = await OpenConnectionAsync();
        if (item.Id == 0)
        {
            const string sql = """
                INSERT INTO config_ranges (category, name, range_start, range_end, description, sort_order)
                VALUES (@cat, @name, @start, @end, @desc, @ord)
                RETURNING id
                """;
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@cat",   item.Category);
            cmd.Parameters.AddWithValue("@name",  item.Name);
            cmd.Parameters.AddWithValue("@start", item.RangeStart);
            cmd.Parameters.AddWithValue("@end",   item.RangeEnd);
            cmd.Parameters.AddWithValue("@desc",  item.Description);
            cmd.Parameters.AddWithValue("@ord",   item.SortOrder);
            item.Id = (int)(await cmd.ExecuteScalarAsync())!;
        }
        else
        {
            const string sql = "UPDATE config_ranges SET category=@cat, name=@name, range_start=@start, range_end=@end, description=@desc, sort_order=@ord WHERE id=@id";
            await using var cmd = new NpgsqlCommand(sql, conn);
            cmd.Parameters.AddWithValue("@cat",   item.Category);
            cmd.Parameters.AddWithValue("@name",  item.Name);
            cmd.Parameters.AddWithValue("@start", item.RangeStart);
            cmd.Parameters.AddWithValue("@end",   item.RangeEnd);
            cmd.Parameters.AddWithValue("@desc",  item.Description);
            cmd.Parameters.AddWithValue("@ord",   item.SortOrder);
            cmd.Parameters.AddWithValue("@id",    item.Id);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    public async Task DeleteConfigRangeAsync(int id)
    {
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand("DELETE FROM config_ranges WHERE id=@id", conn);
        cmd.Parameters.AddWithValue("@id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    // ── Shared Helpers ──────────────────────────────────────────────────

    /// <summary>Execute a write operation with error handling. Returns DbResult.</summary>
    public async Task<DbResult> SafeWriteAsync(Func<Task> operation, string context)
    {
        try
        {
            await operation();
            return DbResult.Ok();
        }
        catch (Npgsql.PostgresException ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DB] {context}: {ex.MessageText}");
            return DbResult.Fail($"Database error: {ex.MessageText}");
        }
        catch (NpgsqlException ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DB] {context}: {ex.Message}");
            return DbResult.Fail($"Connection error: {ex.Message}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[DB] {context}: {ex.Message}");
            return DbResult.Fail(ex);
        }
    }

    // ── Tasks ─────────────────────────────────────────────────────────────

    public async Task<List<Central.Core.Models.TaskItem>> GetTasksAsync(int? projectId = null)
    {
        var list = new List<Central.Core.Models.TaskItem>();
        await using var conn = await OpenConnectionAsync();
        var where = projectId.HasValue ? " WHERE t.project_id = @pid" : "";
        await using var cmd = new NpgsqlCommand($@"
            SELECT t.id, t.parent_id, t.title, t.description, t.status, t.priority, t.task_type,
                   t.assigned_to, COALESCE(a.display_name, ''), t.created_by, COALESCE(c.display_name, ''),
                   t.building, t.due_date, t.estimated_hours, t.actual_hours, t.tags,
                   t.sort_order, t.created_at, t.updated_at, t.completed_at,
                   t.project_id, COALESCE(pr.name,''), t.sprint_id, COALESCE(sp.name,''),
                   t.wbs, t.is_epic, t.is_user_story, t.points, t.work_remaining,
                   t.start_date, t.finish_date, t.is_milestone, t.risk, t.confidence,
                   t.severity, t.bug_priority, t.backlog_priority, t.sprint_priority,
                   t.committed_to, COALESCE(cs.name,''), t.category, t.time_spent, t.color
            FROM tasks t
            LEFT JOIN app_users a ON a.id = t.assigned_to
            LEFT JOIN app_users c ON c.id = t.created_by
            LEFT JOIN task_projects pr ON pr.id = t.project_id
            LEFT JOIN sprints sp ON sp.id = t.sprint_id
            LEFT JOIN sprints cs ON cs.id = t.committed_to
            {where}
            ORDER BY t.sort_order, t.created_at", conn);
        if (projectId.HasValue) cmd.Parameters.AddWithValue("pid", projectId.Value);
        await using var rdr = await cmd.ExecuteReaderAsync();
        while (await rdr.ReadAsync())
            list.Add(new Central.Core.Models.TaskItem
            {
                Id = rdr.GetInt32(0),
                ParentId = rdr.IsDBNull(1) ? null : rdr.GetInt32(1),
                Title = rdr.GetString(2),
                Description = rdr.IsDBNull(3) ? "" : rdr.GetString(3),
                Status = rdr.GetString(4),
                Priority = rdr.GetString(5),
                TaskType = rdr.GetString(6),
                AssignedTo = rdr.IsDBNull(7) ? null : rdr.GetInt32(7),
                AssignedToName = rdr.GetString(8),
                CreatedBy = rdr.IsDBNull(9) ? null : rdr.GetInt32(9),
                CreatedByName = rdr.GetString(10),
                Building = rdr.IsDBNull(11) ? "" : rdr.GetString(11),
                DueDate = rdr.IsDBNull(12) ? null : rdr.GetDateTime(12),
                EstimatedHours = rdr.IsDBNull(13) ? null : rdr.GetDecimal(13),
                ActualHours = rdr.IsDBNull(14) ? null : rdr.GetDecimal(14),
                Tags = rdr.IsDBNull(15) ? "" : rdr.GetString(15),
                SortOrder = rdr.GetInt32(16),
                CreatedAt = rdr.GetDateTime(17),
                UpdatedAt = rdr.GetDateTime(18),
                CompletedAt = rdr.IsDBNull(19) ? null : rdr.GetDateTime(19),
                ProjectId = rdr.IsDBNull(20) ? null : rdr.GetInt32(20),
                ProjectName = rdr.GetString(21),
                SprintId = rdr.IsDBNull(22) ? null : rdr.GetInt32(22),
                SprintName = rdr.GetString(23),
                Wbs = S(rdr, 24),
                IsEpic = !rdr.IsDBNull(25) && rdr.GetBoolean(25),
                IsUserStory = !rdr.IsDBNull(26) && rdr.GetBoolean(26),
                Points = rdr.IsDBNull(27) ? null : rdr.GetDecimal(27),
                WorkRemaining = rdr.IsDBNull(28) ? null : rdr.GetDecimal(28),
                StartDate = rdr.IsDBNull(29) ? null : rdr.GetDateTime(29),
                FinishDate = rdr.IsDBNull(30) ? null : rdr.GetDateTime(30),
                IsMilestone = !rdr.IsDBNull(31) && rdr.GetBoolean(31),
                Risk = S(rdr, 32),
                Confidence = S(rdr, 33),
                Severity = S(rdr, 34),
                BugPriority = S(rdr, 35),
                BacklogPriority = rdr.IsDBNull(36) ? 0 : rdr.GetInt32(36),
                SprintPriority = rdr.IsDBNull(37) ? 0 : rdr.GetInt32(37),
                CommittedTo = rdr.IsDBNull(38) ? null : rdr.GetInt32(38),
                CommittedToName = rdr.GetString(39),
                Category = S(rdr, 40),
                TimeSpent = rdr.IsDBNull(41) ? 0 : rdr.GetDecimal(41),
                Color = S(rdr, 42),
            });
        return list;
    }

    public async Task UpsertTaskAsync(Central.Core.Models.TaskItem t)
    {
        await using var conn = await OpenConnectionAsync();
        if (t.Id == 0)
        {
            await using var cmd = new NpgsqlCommand(@"
                INSERT INTO tasks (parent_id, title, description, status, priority, task_type,
                    assigned_to, building, due_date, tags, project_id, sprint_id, is_epic,
                    is_user_story, points, work_remaining, start_date, finish_date, is_milestone,
                    risk, confidence, severity, bug_priority, backlog_priority, sprint_priority,
                    committed_to, category, color)
                VALUES (@parent, @title, @desc, @status, @pri, @type, @assigned, @bldg, @due, @tags,
                    @projId, @sprintId, @isEpic, @isStory, @points, @workRem, @startDt, @finishDt,
                    @isMile, @risk, @conf, @sev, @bugPri, @blPri, @spPri, @committed, @cat, @color)
                RETURNING id", conn);
            AddTaskParams(cmd, t);
            t.Id = (int)(await cmd.ExecuteScalarAsync())!;
        }
        else
        {
            await using var cmd = new NpgsqlCommand(@"
                UPDATE tasks SET title=@title, description=@desc, status=@status, priority=@pri,
                    task_type=@type, assigned_to=@assigned, building=@bldg, due_date=@due, tags=@tags,
                    project_id=@projId, sprint_id=@sprintId, is_epic=@isEpic, is_user_story=@isStory,
                    points=@points, work_remaining=@workRem, start_date=@startDt, finish_date=@finishDt,
                    is_milestone=@isMile, risk=@risk, confidence=@conf, severity=@sev, bug_priority=@bugPri,
                    backlog_priority=@blPri, sprint_priority=@spPri, committed_to=@committed,
                    category=@cat, color=@color,
                    updated_at=now(), completed_at = CASE WHEN @status = 'Done' THEN COALESCE(completed_at, now()) ELSE NULL END
                WHERE id=@id", conn);
            cmd.Parameters.AddWithValue("id", t.Id);
            AddTaskParams(cmd, t);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    private static void AddTaskParams(NpgsqlCommand cmd, Central.Core.Models.TaskItem t)
    {
        cmd.Parameters.AddWithValue("parent", (object?)t.ParentId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("title", t.Title);
        cmd.Parameters.AddWithValue("desc", (object?)t.Description ?? DBNull.Value);
        cmd.Parameters.AddWithValue("status", t.Status);
        cmd.Parameters.AddWithValue("pri", t.Priority);
        cmd.Parameters.AddWithValue("type", t.TaskType);
        cmd.Parameters.AddWithValue("assigned", (object?)t.AssignedTo ?? DBNull.Value);
        cmd.Parameters.AddWithValue("bldg", (object?)t.Building ?? DBNull.Value);
        cmd.Parameters.AddWithValue("due", (object?)t.DueDate ?? DBNull.Value);
        cmd.Parameters.AddWithValue("tags", (object?)t.Tags ?? DBNull.Value);
        cmd.Parameters.AddWithValue("projId", (object?)t.ProjectId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("sprintId", (object?)t.SprintId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("isEpic", t.IsEpic);
        cmd.Parameters.AddWithValue("isStory", t.IsUserStory);
        cmd.Parameters.AddWithValue("points", (object?)t.Points ?? DBNull.Value);
        cmd.Parameters.AddWithValue("workRem", (object?)t.WorkRemaining ?? DBNull.Value);
        cmd.Parameters.AddWithValue("startDt", (object?)t.StartDate ?? DBNull.Value);
        cmd.Parameters.AddWithValue("finishDt", (object?)t.FinishDate ?? DBNull.Value);
        cmd.Parameters.AddWithValue("isMile", t.IsMilestone);
        cmd.Parameters.AddWithValue("risk", (object?)t.Risk ?? DBNull.Value);
        cmd.Parameters.AddWithValue("conf", (object?)t.Confidence ?? DBNull.Value);
        cmd.Parameters.AddWithValue("sev", (object?)t.Severity ?? DBNull.Value);
        cmd.Parameters.AddWithValue("bugPri", (object?)t.BugPriority ?? DBNull.Value);
        cmd.Parameters.AddWithValue("blPri", t.BacklogPriority);
        cmd.Parameters.AddWithValue("spPri", t.SprintPriority);
        cmd.Parameters.AddWithValue("committed", (object?)t.CommittedTo ?? DBNull.Value);
        cmd.Parameters.AddWithValue("cat", (object?)t.Category ?? DBNull.Value);
        cmd.Parameters.AddWithValue("color", (object?)t.Color ?? DBNull.Value);
    }

    public async Task DeleteTaskAsync(int id)
    {
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand("DELETE FROM tasks WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>Helper to read a nullable string from a data reader.</summary>
    internal static string S(System.Data.Common.DbDataReader r, int i)
        => r.IsDBNull(i) ? "" : r.GetString(i);

    /// <summary>Create a typed JSONB parameter (handles null/empty → DBNull).</summary>
    internal static NpgsqlParameter JsonbParam(string name, string? value)
        => new(name, NpgsqlTypes.NpgsqlDbType.Jsonb) { Value = string.IsNullOrEmpty(value) ? DBNull.Value : value };
}
