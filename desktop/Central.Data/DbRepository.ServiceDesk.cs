using Npgsql;
using Central.Core.Models;

namespace Central.Data;

public partial class DbRepository
{
    // ── Shared SD request column list + mapper (used by all SD queries) ────

    private const string SdRequestColumns =
        "id, display_id, subject, status, priority, group_name, category, " +
        "technician_id, technician_name, requester_id, requester_name, requester_email, " +
        "site, department, template, is_service_request, created_at, due_by, ticket_url";

    private static SdRequest MapSdRequest(Npgsql.NpgsqlDataReader r, bool acceptChanges = false)
    {
        var req = new SdRequest
        {
            Id = r.GetInt64(0), DisplayId = r.GetString(1), Subject = r.GetString(2),
            Status = r.IsDBNull(3) ? "" : r.GetString(3),
            Priority = r.IsDBNull(4) ? "" : r.GetString(4),
            GroupName = r.IsDBNull(5) ? "" : r.GetString(5),
            Category = r.IsDBNull(6) ? "" : r.GetString(6),
            TechnicianId = r.IsDBNull(7) ? null : r.GetInt64(7),
            TechnicianName = r.IsDBNull(8) ? "" : r.GetString(8),
            RequesterId = r.IsDBNull(9) ? null : r.GetInt64(9),
            RequesterName = r.IsDBNull(10) ? "" : r.GetString(10),
            RequesterEmail = r.IsDBNull(11) ? "" : r.GetString(11),
            Site = r.IsDBNull(12) ? "" : r.GetString(12),
            Department = r.IsDBNull(13) ? "" : r.GetString(13),
            Template = r.IsDBNull(14) ? "" : r.GetString(14),
            IsServiceRequest = r.GetBoolean(15),
            CreatedAt = r.IsDBNull(16) ? null : r.GetDateTime(16),
            DueBy = r.IsDBNull(17) ? null : r.GetDateTime(17),
            TicketUrl = r.IsDBNull(18) ? "" : r.GetString(18)
        };
        if (acceptChanges) req.AcceptChanges();
        return req;
    }

    private async Task<List<SdRequest>> QuerySdRequestsAsync(string whereClause, Action<NpgsqlCommand>? addParams = null, bool acceptChanges = false)
    {
        var list = new List<SdRequest>();
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand($"SELECT {SdRequestColumns} FROM sd_requests {whereClause}", conn);
        addParams?.Invoke(cmd);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) list.Add(MapSdRequest(r, acceptChanges));
        return list;
    }

    // ── SD Request Queries ────────────────────────────────────────────────

    public Task<List<SdRequest>> GetSdRequestsAsync(int limit = 5000) =>
        QuerySdRequestsAsync($"ORDER BY created_at DESC LIMIT {limit}", acceptChanges: true);

    /// <summary>Get requests filtered by SdFilterState (for SD Settings panel driven grid).</summary>
    public Task<List<SdRequest>> GetSdRequestsFilteredAsync(SdFilterState f)
    {
        var conds = new List<string> { "created_at >= @s", "created_at < @e" };
        if (f.SelectedGroups is { Count: > 0 }) conds.Add("group_name = ANY(@groups)");
        if (f.SelectedTechs is { Count: > 0 }) conds.Add("technician_name = ANY(@techs)");
        var where = "WHERE " + string.Join(" AND ", conds) + " ORDER BY created_at DESC LIMIT 5000";
        return QuerySdRequestsAsync(where, cmd =>
        {
            cmd.Parameters.AddWithValue("s", f.RangeStart);
            cmd.Parameters.AddWithValue("e", f.RangeEnd);
            if (f.SelectedGroups is { Count: > 0 }) cmd.Parameters.AddWithValue("groups", f.SelectedGroups.ToArray());
            if (f.SelectedTechs is { Count: > 0 }) cmd.Parameters.AddWithValue("techs", f.SelectedTechs.ToArray());
        }, acceptChanges: true);
    }

    // Old filtered overload removed — use GetSdRequestsFilteredAsync(SdFilterState) above

    public async Task<int> GetSdRequestCountAsync()
    {
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand("SELECT count(*) FROM sd_requests", conn);
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    public async Task<long?> GetLastMeSyncTimeAsync()
    {
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand("SELECT MAX(me_updated_time) FROM sd_requests", conn);
        var result = await cmd.ExecuteScalarAsync();
        return result is long val ? val : null;
    }

    public async Task UpdateIntegrationLastSyncAsync(int integrationId)
    {
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            "UPDATE integrations SET last_sync_at = NOW() WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("id", integrationId);
        await cmd.ExecuteNonQueryAsync();
    }

    // ── SD Lookup CRUD (Groups, Technicians, Requesters) ────────────────

    public async Task<List<SdGroup>> GetSdGroupsAsync()
    {
        var list = new List<SdGroup>();
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand("SELECT id, name, is_active, sort_order FROM sd_groups ORDER BY sort_order, name", conn);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new SdGroup { Id = r.GetInt32(0), Name = r.GetString(1), IsActive = r.GetBoolean(2), SortOrder = r.GetInt32(3) });
        return list;
    }

    public async Task UpsertSdGroupAsync(SdGroup g)
    {
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(g.Id > 0
            ? "UPDATE sd_groups SET name=@n, is_active=@a, sort_order=@s WHERE id=@id"
            : "INSERT INTO sd_groups (name, is_active, sort_order) VALUES (@n, @a, @s) RETURNING id", conn);
        if (g.Id > 0) cmd.Parameters.AddWithValue("id", g.Id);
        cmd.Parameters.AddWithValue("n", g.Name);
        cmd.Parameters.AddWithValue("a", g.IsActive);
        cmd.Parameters.AddWithValue("s", g.SortOrder);
        if (g.Id == 0) g.Id = (int)(await cmd.ExecuteScalarAsync())!;
        else await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteSdGroupAsync(int id)
    {
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand("DELETE FROM sd_groups WHERE id=@id", conn);
        cmd.Parameters.AddWithValue("id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<SdTechnician>> GetSdTechniciansAsync()
    {
        var list = new List<SdTechnician>();
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT id, name, COALESCE(email,''), COALESCE(department,''), is_active FROM sd_technicians ORDER BY name", conn);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new SdTechnician { Id = r.GetInt64(0), Name = r.GetString(1), Email = r.GetString(2), Department = r.GetString(3), IsActive = r.GetBoolean(4) });
        return list;
    }

    public async Task UpdateSdTechnicianActiveAsync(long id, bool isActive)
    {
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand("UPDATE sd_technicians SET is_active=@a WHERE id=@id", conn);
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("a", isActive);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<SdRequester>> GetSdRequestersAsync()
    {
        var list = new List<SdRequester>();
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT id, name, COALESCE(email,''), COALESCE(phone,''), COALESCE(department,''), COALESCE(site,''), COALESCE(job_title,''), is_vip FROM sd_requesters ORDER BY name", conn);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new SdRequester { Id = r.GetInt64(0), Name = r.GetString(1), Email = r.GetString(2), Phone = r.GetString(3),
                Department = r.GetString(4), Site = r.GetString(5), JobTitle = r.GetString(6), IsVip = r.GetBoolean(7) });
        return list;
    }

    /// <summary>Update local DB fields for a request (after ME write-back succeeds).</summary>
    public async Task UpdateSdRequestLocalAsync(long id, string status, string priority, string groupName, string technicianName, string category)
    {
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            @"UPDATE sd_requests SET status=@st, priority=@pri, group_name=@grp, technician_name=@tn, category=@cat
              WHERE id=@id", conn);
        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("st", status);
        cmd.Parameters.AddWithValue("pri", priority);
        cmd.Parameters.AddWithValue("grp", groupName);
        cmd.Parameters.AddWithValue("tn", technicianName);
        cmd.Parameters.AddWithValue("cat", category);
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>Get open requests for a technician within an age range (days).</summary>
    public Task<List<SdRequest>> GetSdAgingDrillDownAsync(string technicianName, double minDays, double maxDays) =>
        QuerySdRequestsAsync(
            @"WHERE status NOT IN ('Resolved','Closed','Canceled','Cancelled')
              AND technician_name = @tech
              AND EXTRACT(EPOCH FROM (NOW() - created_at)) / 86400.0 >= @minD
              AND EXTRACT(EPOCH FROM (NOW() - created_at)) / 86400.0 < @maxD
              ORDER BY created_at",
            cmd => { cmd.Parameters.AddWithValue("tech", technicianName); cmd.Parameters.AddWithValue("minD", minDays); cmd.Parameters.AddWithValue("maxD", maxDays); });

    public Task<List<SdRequest>> GetSdClosureDrillDownAsync(string technicianName, DateTime day) =>
        QuerySdRequestsAsync(
            @"WHERE status IN ('Resolved','Closed') AND technician_name = @tech
              AND resolved_at IS NOT NULL AND resolved_at::date = @day ORDER BY resolved_at",
            cmd => { cmd.Parameters.AddWithValue("tech", technicianName); cmd.Parameters.AddWithValue("day", day.Date); });

    public Task<List<SdRequest>> GetSdOverviewDrillDownAsync(string type, DateTime day)
    {
        var filter = type switch
        {
            "Issues created" => "created_at::date = @day",
            "Issues closed" or "Issues resolved" => "status IN ('Resolved','Closed') AND resolved_at IS NOT NULL AND resolved_at::date = @day",
            _ => "created_at::date = @day"
        };
        return QuerySdRequestsAsync($"WHERE {filter} ORDER BY created_at", cmd => cmd.Parameters.AddWithValue("day", day.Date));
    }

    public Task<List<SdRequest>> GetSdKpiDrillDownAsync(string kpiName, DateTime rangeStart, DateTime rangeEnd)
    {
        var filter = kpiName switch
        {
            "Incoming" => "created_at >= @s AND created_at < @e",
            "Closed" or "Resolutions" => "status IN ('Resolved','Closed') AND resolved_at >= @s AND resolved_at < @e",
            "Escalations" => "priority IN ('High','Urgent') AND created_at >= @s AND created_at < @e",
            "SLA Compliant" => "status IN ('Resolved','Closed') AND resolved_at >= @s AND resolved_at < @e AND (due_by IS NULL OR resolved_at <= due_by)",
            "Open" => "status NOT IN ('Resolved','Closed','Canceled','Cancelled')",
            _ => "created_at >= @s AND created_at < @e"
        };
        return QuerySdRequestsAsync($"WHERE {filter} ORDER BY created_at DESC LIMIT 5000",
            cmd => { cmd.Parameters.AddWithValue("s", rangeStart); cmd.Parameters.AddWithValue("e", rangeEnd); });
    }

    /// <summary>Get distinct technician names that have tickets.</summary>
    public async Task<List<string>> GetSdTechnicianNamesAsync()
    {
        var list = new List<string>();
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            @"SELECT DISTINCT technician_name FROM sd_requests
              WHERE technician_name <> ''
                AND technician_name IN (SELECT name FROM sd_technicians WHERE is_active = true)
              ORDER BY technician_name", conn);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) list.Add(r.GetString(0));
        return list;
    }

    // ── Teams ─────────────────────────────────────────────────────────────

    public async Task<List<SdTeam>> GetSdTeamsAsync()
    {
        var teams = new List<SdTeam>();
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            @"SELECT t.id, t.name, t.sort_order, COALESCE(array_agg(m.technician_name ORDER BY m.technician_name) FILTER (WHERE m.technician_name IS NOT NULL), '{}')
              FROM sd_teams t LEFT JOIN sd_team_members m ON m.team_id = t.id
              GROUP BY t.id ORDER BY t.sort_order, t.name", conn);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            teams.Add(new SdTeam
            {
                Id = r.GetInt32(0), Name = r.GetString(1), SortOrder = r.GetInt32(2),
                Members = ((string[])r.GetValue(3)).ToList()
            });
        }
        return teams;
    }

    public async Task<int> UpsertSdTeamAsync(SdTeam team)
    {
        await using var conn = await OpenConnectionAsync();
        await using var tx = await conn.BeginTransactionAsync();

        // Upsert team
        await using var cmd = new NpgsqlCommand(team.Id > 0
            ? "UPDATE sd_teams SET name=@n, sort_order=@s WHERE id=@id RETURNING id"
            : "INSERT INTO sd_teams (name, sort_order) VALUES (@n, @s) RETURNING id", conn, tx);
        if (team.Id > 0) cmd.Parameters.AddWithValue("id", team.Id);
        cmd.Parameters.AddWithValue("n", team.Name);
        cmd.Parameters.AddWithValue("s", team.SortOrder);
        team.Id = (int)(await cmd.ExecuteScalarAsync())!;

        // Replace members
        await using var del = new NpgsqlCommand("DELETE FROM sd_team_members WHERE team_id=@id", conn, tx);
        del.Parameters.AddWithValue("id", team.Id);
        await del.ExecuteNonQueryAsync();

        foreach (var m in team.Members)
        {
            await using var ins = new NpgsqlCommand(
                "INSERT INTO sd_team_members (team_id, technician_name) VALUES (@tid, @n) ON CONFLICT DO NOTHING", conn, tx);
            ins.Parameters.AddWithValue("tid", team.Id);
            ins.Parameters.AddWithValue("n", m);
            await ins.ExecuteNonQueryAsync();
        }

        await tx.CommitAsync();
        return team.Id;
    }

    public async Task DeleteSdTeamAsync(int teamId)
    {
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand("DELETE FROM sd_teams WHERE id=@id", conn);
        cmd.Parameters.AddWithValue("id", teamId);
        await cmd.ExecuteNonQueryAsync();
    }

    // ── Group Categories ──────────────────────────────────────────────────

    public async Task<List<SdGroupCategory>> GetSdGroupCategoriesAsync()
    {
        var list = new List<SdGroupCategory>();
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            @"SELECT c.id, c.name, c.sort_order, c.is_active,
                     COALESCE(array_agg(m.group_name ORDER BY m.group_name) FILTER (WHERE m.group_name IS NOT NULL), '{}')
              FROM sd_group_categories c
              LEFT JOIN sd_group_category_members m ON m.category_id = c.id
              GROUP BY c.id ORDER BY c.sort_order, c.name", conn);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new SdGroupCategory
            {
                Id = r.GetInt32(0), Name = r.GetString(1), SortOrder = r.GetInt32(2),
                IsActive = r.GetBoolean(3), Members = ((string[])r.GetValue(4)).ToList()
            });
        return list;
    }

    public async Task<int> UpsertSdGroupCategoryAsync(SdGroupCategory cat)
    {
        await using var conn = await OpenConnectionAsync();
        await using var tx = await conn.BeginTransactionAsync();

        await using var cmd = new NpgsqlCommand(cat.Id > 0
            ? "UPDATE sd_group_categories SET name=@n, sort_order=@s, is_active=@a WHERE id=@id RETURNING id"
            : "INSERT INTO sd_group_categories (name, sort_order, is_active) VALUES (@n, @s, @a) RETURNING id", conn, tx);
        if (cat.Id > 0) cmd.Parameters.AddWithValue("id", cat.Id);
        cmd.Parameters.AddWithValue("n", cat.Name);
        cmd.Parameters.AddWithValue("s", cat.SortOrder);
        cmd.Parameters.AddWithValue("a", cat.IsActive);
        cat.Id = (int)(await cmd.ExecuteScalarAsync())!;

        await using var del = new NpgsqlCommand("DELETE FROM sd_group_category_members WHERE category_id=@id", conn, tx);
        del.Parameters.AddWithValue("id", cat.Id);
        await del.ExecuteNonQueryAsync();

        foreach (var m in cat.Members)
        {
            await using var ins = new NpgsqlCommand(
                "INSERT INTO sd_group_category_members (category_id, group_name) VALUES (@cid, @n) ON CONFLICT DO NOTHING", conn, tx);
            ins.Parameters.AddWithValue("cid", cat.Id);
            ins.Parameters.AddWithValue("n", m);
            await ins.ExecuteNonQueryAsync();
        }

        await tx.CommitAsync();
        return cat.Id;
    }

    public async Task DeleteSdGroupCategoryAsync(int id)
    {
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand("DELETE FROM sd_group_categories WHERE id=@id", conn);
        cmd.Parameters.AddWithValue("id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>Get all ME group names (distinct from sd_requests).</summary>
    public async Task<List<string>> GetSdGroupNamesAsync()
    {
        var list = new List<string>();
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT DISTINCT group_name FROM sd_requests WHERE group_name <> '' ORDER BY group_name", conn);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) list.Add(r.GetString(0));
        return list;
    }

    /// <summary>Get distinct category names from sd_requests.</summary>
    public async Task<List<string>> GetSdCategoryNamesAsync()
    {
        var list = new List<string>();
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT DISTINCT category FROM sd_requests WHERE category <> '' ORDER BY category", conn);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) list.Add(r.GetString(0));
        return list;
    }

    /// <summary>Get ticket count per group name.</summary>
    public async Task<Dictionary<string, int>> GetSdGroupTicketCountsAsync()
    {
        var dict = new Dictionary<string, int>();
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT group_name, count(*)::int FROM sd_requests WHERE group_name <> '' GROUP BY group_name", conn);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) dict[r.GetString(0)] = r.GetInt32(1);
        return dict;
    }

    // ── KPI Summary ────────────────────────────────────────────────────────

    /// <summary>KPI stats for a period, plus the previous period of the same length for trend.</summary>
    public async Task<SdKpiSummary> GetSdKpiSummaryAsync(DateTime rangeStart, DateTime rangeEnd, List<string>? groupFilter = null)
    {
        var kpi = new SdKpiSummary();
        var periodLen = rangeEnd - rangeStart;
        var prevStart = rangeStart - periodLen;
        var prevEnd = rangeStart;
        var gf = groupFilter is { Count: > 0 } ? " AND group_name = ANY(@grps)" : "";

        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand($@"
            SELECT
                -- Current period
                (SELECT count(*) FROM sd_requests WHERE created_at >= @s AND created_at < @e {gf})::int AS incoming,
                (SELECT count(*) FROM sd_requests WHERE status IN ('Resolved','Closed') AND resolved_at >= @s AND resolved_at < @e {gf})::int AS resolutions,
                (SELECT count(*) FROM sd_requests WHERE priority IN ('High','Urgent') AND created_at >= @s AND created_at < @e {gf})::int AS escalations,
                (SELECT count(*) FROM sd_requests WHERE status IN ('Resolved','Closed') AND resolved_at >= @s AND resolved_at < @e
                    AND (due_by IS NULL OR resolved_at <= due_by) {gf})::int AS sla_ok,
                COALESCE((SELECT AVG(EXTRACT(EPOCH FROM (resolved_at - created_at))/3600.0) FROM sd_requests
                    WHERE status IN ('Resolved','Closed') AND resolved_at >= @s AND resolved_at < @e AND resolved_at IS NOT NULL {gf}), 0)::float8 AS avg_hrs,
                (SELECT count(*) FROM sd_requests WHERE status NOT IN ('Resolved','Closed','Canceled','Cancelled') {gf})::int AS open_now,
                (SELECT count(*) FROM sd_technicians WHERE is_active = true)::int AS active_techs,
                -- Previous period
                (SELECT count(*) FROM sd_requests WHERE created_at >= @ps AND created_at < @pe {gf})::int,
                (SELECT count(*) FROM sd_requests WHERE status IN ('Resolved','Closed') AND resolved_at >= @ps AND resolved_at < @pe {gf})::int,
                (SELECT count(*) FROM sd_requests WHERE priority IN ('High','Urgent') AND created_at >= @ps AND created_at < @pe {gf})::int,
                (SELECT count(*) FROM sd_requests WHERE status IN ('Resolved','Closed') AND resolved_at >= @ps AND resolved_at < @pe
                    AND (due_by IS NULL OR resolved_at <= due_by) {gf})::int,
                COALESCE((SELECT AVG(EXTRACT(EPOCH FROM (resolved_at - created_at))/3600.0) FROM sd_requests
                    WHERE status IN ('Resolved','Closed') AND resolved_at >= @ps AND resolved_at < @pe AND resolved_at IS NOT NULL {gf}), 0)::float8,
                (SELECT count(*) FROM sd_requests WHERE status NOT IN ('Resolved','Closed','Canceled','Cancelled')
                    AND created_at < @pe {gf})::int", conn);
        cmd.Parameters.AddWithValue("s", rangeStart);
        cmd.Parameters.AddWithValue("e", rangeEnd);
        cmd.Parameters.AddWithValue("ps", prevStart);
        cmd.Parameters.AddWithValue("pe", prevEnd);
        if (groupFilter is { Count: > 0 })
            cmd.Parameters.AddWithValue("grps", groupFilter.ToArray());
        await using var r = await cmd.ExecuteReaderAsync();
        if (await r.ReadAsync())
        {
            kpi.Incoming = r.GetInt32(0);
            kpi.Resolutions = r.GetInt32(1);
            kpi.Escalations = r.GetInt32(2);
            kpi.SlaCompliant = r.GetInt32(3);
            kpi.AvgResolutionHours = r.GetDouble(4);
            kpi.OpenCount = r.GetInt32(5);
            kpi.ActiveTechCount = r.GetInt32(6);
            kpi.PrevIncoming = r.GetInt32(7);
            kpi.PrevResolutions = r.GetInt32(8);
            kpi.PrevEscalations = r.GetInt32(9);
            kpi.PrevSlaCompliant = r.GetInt32(10);
            kpi.PrevAvgResolutionHours = r.GetDouble(11);
            kpi.PrevOpenCount = r.GetInt32(12);
        }
        return kpi;
    }

    // ── Service Desk Dashboard Queries (parameterized) ────────────────────

    /// <summary>Overview totals: created vs completed, avg resolution days, open count — bucketed by day/week/month.</summary>
    public async Task<List<SdWeeklyTotal>> GetSdOverviewTotalsAsync(DateTime rangeStart, DateTime rangeEnd, string bucket = "day", List<string>? groupFilter = null)
    {
        var list = new List<SdWeeklyTotal>();
        await using var conn = await OpenConnectionAsync();

        var interval = bucket switch { "month" => "interval '1 month'", "week" => "interval '1 week'", _ => "interval '1 day'" };
        var seriesExpr = $"generate_series(@start::date, (@end::date - interval '1 day')::date, {interval})";

        // bucketOf: maps a timestamp to its bucket start date
        var bucketOf = bucket switch { "month" => "date_trunc('month', x)::date", "week" => "date_trunc('week', x)::date", _ => "x::date" };

        var grpFilter = groupFilter is { Count: > 0 } ? " AND group_name = ANY(@grps)" : "";

        // open_baseline: open tickets at the start of the range (created before start, not yet resolved)
        // Then use running sum of (created - closed) per bucket to get open count at each point
        var sql = $@"
            WITH buckets AS (SELECT {seriesExpr} AS d),
            baseline AS (
                SELECT count(*)::int AS open_before
                FROM sd_requests
                WHERE created_at < @start
                  AND (resolved_at IS NULL OR resolved_at >= @start)
                  AND status NOT IN ('Canceled','Cancelled','Archive')
                  {grpFilter}
            ),
            created AS (
                SELECT ({bucketOf.Replace("x", "created_at")}) AS bucket, count(*) AS cnt
                FROM sd_requests
                WHERE created_at >= @start AND created_at < @end {grpFilter}
                GROUP BY 1
            ),
            closed AS (
                SELECT ({bucketOf.Replace("x", "resolved_at")}) AS bucket, count(*) AS cnt
                FROM sd_requests
                WHERE status IN ('Resolved','Closed')
                  AND resolved_at IS NOT NULL
                  AND resolved_at >= @start AND resolved_at < @end {grpFilter}
                GROUP BY 1
            ),
            period_mean AS (
                SELECT AVG(EXTRACT(EPOCH FROM (resolved_at - created_at)) / 86400.0) AS avg_days
                FROM sd_requests
                WHERE status IN ('Resolved','Closed')
                  AND resolved_at IS NOT NULL
                  AND resolved_at >= @start AND resolved_at < @end {grpFilter}
            )
            SELECT b.d::date,
                   COALESCE(cr.cnt, 0)::int,
                   COALESCE(cl.cnt, 0)::int,
                   COALESCE(pm.avg_days, 0)::float8,
                   (bl.open_before + SUM(COALESCE(cr.cnt,0) - COALESCE(cl.cnt,0)) OVER (ORDER BY b.d))::int AS open_cnt
            FROM buckets b
            CROSS JOIN period_mean pm
            CROSS JOIN baseline bl
            LEFT JOIN created cr ON cr.bucket = b.d::date
            LEFT JOIN closed  cl ON cl.bucket = b.d::date
            ORDER BY b.d";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("start", rangeStart);
        cmd.Parameters.AddWithValue("end", rangeEnd);
        if (groupFilter is { Count: > 0 })
            cmd.Parameters.AddWithValue("grps", groupFilter.ToArray());
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            list.Add(new SdWeeklyTotal
            {
                Day = r.GetDateTime(0),
                Created = r.GetInt32(1),
                Completed = r.GetInt32(2),
                AvgResolutionDays = r.GetDouble(3),
                OpenCount = r.GetInt32(4)
            });
        }
        return list;
    }

    /// <summary>Daily closures per technician within a date range, optionally filtered.</summary>
    public async Task<List<SdTechDaily>> GetSdTechDailyClosuresAsync(DateTime rangeStart, DateTime rangeEnd, List<string>? techFilter = null)
    {
        var list = new List<SdTechDaily>();
        await using var conn = await OpenConnectionAsync();

        var filterClause = techFilter is { Count: > 0 }
            ? " AND technician_name = ANY(@techs)" : "";

        var sql = $@"
            SELECT technician_name, resolved_at::date AS day, count(*) AS cnt
            FROM sd_requests
            WHERE status IN ('Resolved','Closed')
              AND technician_name <> ''
              AND technician_name IN (SELECT name FROM sd_technicians WHERE is_active = true)
              AND resolved_at IS NOT NULL
              AND resolved_at >= @start AND resolved_at < @end
              {filterClause}
            GROUP BY technician_name, resolved_at::date
            ORDER BY technician_name, day";

        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("start", rangeStart);
        cmd.Parameters.AddWithValue("end", rangeEnd);
        if (techFilter is { Count: > 0 })
            cmd.Parameters.AddWithValue("techs", techFilter.ToArray());
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            list.Add(new SdTechDaily
            {
                TechnicianName = r.GetString(0),
                Day = r.GetDateTime(1),
                Closed = r.GetInt32(2)
            });
        }
        return list;
    }

    /// <summary>Ticket aging buckets, optionally filtered by technician.</summary>
    public async Task<List<SdAgingBucket>> GetSdAgingBucketsAsync(List<string>? techFilter = null)
    {
        var list = new List<SdAgingBucket>();
        await using var conn = await OpenConnectionAsync();

        var filterClause = techFilter is { Count: > 0 }
            ? " AND technician_name = ANY(@techs)" : "";

        var sql = $@"
            SELECT technician_name,
                   SUM(CASE WHEN age_days < 1  THEN 1 ELSE 0 END)::int AS d0_1,
                   SUM(CASE WHEN age_days >= 1  AND age_days < 2  THEN 1 ELSE 0 END)::int AS d1_2,
                   SUM(CASE WHEN age_days >= 2  AND age_days < 4  THEN 1 ELSE 0 END)::int AS d2_4,
                   SUM(CASE WHEN age_days >= 4  AND age_days < 7  THEN 1 ELSE 0 END)::int AS d4_7,
                   SUM(CASE WHEN age_days >= 7  THEN 1 ELSE 0 END)::int AS d7plus
            FROM (
                SELECT technician_name,
                       EXTRACT(EPOCH FROM (NOW() - created_at)) / 86400.0 AS age_days
                FROM sd_requests
                WHERE status NOT IN ('Resolved','Closed','Canceled','Cancelled')
                  AND technician_name <> ''
                  AND technician_name IN (SELECT name FROM sd_technicians WHERE is_active = true)
                  {filterClause}
            ) sub
            GROUP BY technician_name
            ORDER BY technician_name";

        await using var cmd = new NpgsqlCommand(sql, conn);
        if (techFilter is { Count: > 0 })
            cmd.Parameters.AddWithValue("techs", techFilter.ToArray());
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            list.Add(new SdAgingBucket
            {
                TechnicianName = r.GetString(0),
                Days0to1 = r.GetInt32(1),
                Days1to2 = r.GetInt32(2),
                Days2to4 = r.GetInt32(3),
                Days4to7 = r.GetInt32(4),
                Days7Plus = r.GetInt32(5)
            });
        }
        return list;
    }
}
