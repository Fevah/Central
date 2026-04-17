using System.Security.Claims;
using System.Text.Json;
using Npgsql;
using Central.Data;

namespace Central.Api.Endpoints;

// ══════════════════════════════════════════════════════════════════════
// Stage 1: Marketing Automation
// ══════════════════════════════════════════════════════════════════════

public static class CampaignEndpoints
{
    private static readonly HashSet<string> WritableColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        "name", "description", "campaign_type", "status", "owner_id", "parent_id",
        "start_date", "end_date", "budget", "expected_revenue", "expected_responses",
        "is_active", "source_code", "tags", "metadata"
    };
    private static readonly HashSet<string> FilterableColumns = new(StringComparer.OrdinalIgnoreCase)
    { "campaign_type", "status", "owner_id", "is_active", "source_code" };
    private static readonly string[] SearchableColumns = ["name", "description", "source_code"];

    public static RouteGroupBuilder MapCampaignEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", async (int? offset, int? limit, string? sort, string? order, string? search, string? filter, DbConnectionFactory db) =>
        {
            var q = new PaginatedQuery(offset, limit, sort, order, search, filter);
            await using var conn = await db.OpenConnectionAsync();
            var conditions = new List<string> { "c.is_deleted IS NOT TRUE" };
            var allParams = new List<(string, object)>();
            var (fw, fp) = PaginationHelpers.BuildFilterClause(filter, FilterableColumns);
            if (!string.IsNullOrEmpty(fw)) { conditions.Add(fw); allParams.AddRange(fp); }
            var (sw, sp) = PaginationHelpers.BuildSearchClause(search, SearchableColumns);
            if (!string.IsNullOrEmpty(sw)) { conditions.Add(sw); allParams.AddRange(sp); }
            var where = string.Join(" AND ", conditions);
            var sortClause = PaginationHelpers.BuildSortClause(q, "c.start_date DESC", FilterableColumns);

            await using var countCmd = conn.CreateCommand();
            countCmd.CommandText = $"SELECT COUNT(*) FROM crm_campaigns c WHERE {where}";
            foreach (var (n, v) in allParams) countCmd.Parameters.AddWithValue(n, v);
            var total = Convert.ToInt32(await countCmd.ExecuteScalarAsync());

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $@"SELECT c.id, c.name, c.campaign_type, c.status, c.start_date, c.end_date,
                c.budget, c.actual_cost, c.expected_revenue, c.owner_id, COALESCE(u.display_name,'') AS owner_name
                FROM crm_campaigns c LEFT JOIN app_users u ON u.id = c.owner_id
                WHERE {where} ORDER BY {sortClause} LIMIT @limit OFFSET @offset";
            foreach (var (n, v) in allParams) cmd.Parameters.AddWithValue(n, v);
            cmd.Parameters.AddWithValue("limit", q.EffectiveLimit);
            cmd.Parameters.AddWithValue("offset", q.EffectiveOffset);
            await using var reader = await cmd.ExecuteReaderAsync();
            return Results.Ok(await PaginationHelpers.ExecutePaginatedAsync(reader, total, q));
        });

        group.MapPost("/", async (Dictionary<string, object?> body, DbConnectionFactory db) =>
        {
            var inv = EndpointHelpers.ValidateColumns(body.Keys, WritableColumns);
            if (inv is not null) return inv;
            if (!body.ContainsKey("name")) return ApiProblem.ValidationError("Campaign name is required.");
            return await CrmCrudHelpers.InsertAsync(db, "crm_campaigns", body);
        });

        group.MapPut("/{id:int}", async (int id, Dictionary<string, object?> body, DbConnectionFactory db) =>
        {
            var inv = EndpointHelpers.ValidateColumns(body.Keys, WritableColumns);
            if (inv is not null) return inv;
            return await CrmCrudHelpers.UpdateAsync(db, "crm_campaigns", id, body);
        });

        group.MapDelete("/{id:int}", async (int id, DbConnectionFactory db)
            => await CrmCrudHelpers.SoftDeleteAsync(db, "crm_campaigns", id));

        // Members
        group.MapGet("/{id:int}/members", async (int id, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                "SELECT * FROM crm_campaign_members WHERE campaign_id = @id ORDER BY added_at DESC", conn);
            cmd.Parameters.AddWithValue("id", id);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        group.MapPost("/{id:int}/members", async (int id, JsonElement body, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                @"INSERT INTO crm_campaign_members (campaign_id, member_type, member_id, status)
                  VALUES (@cid, @mt, @mid, @s) ON CONFLICT DO NOTHING RETURNING id", conn);
            cmd.Parameters.AddWithValue("cid", id);
            cmd.Parameters.AddWithValue("mt", body.GetProperty("member_type").GetString() ?? "");
            cmd.Parameters.AddWithValue("mid", body.GetProperty("member_id").GetInt32());
            cmd.Parameters.AddWithValue("s", body.TryGetProperty("status", out var s) ? s.GetString() ?? "sent" : "sent");
            var newId = await cmd.ExecuteScalarAsync();
            return Results.Ok(new { id = newId });
        });

        // Costs
        group.MapPost("/{id:int}/costs", async (int id, JsonElement body, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                @"INSERT INTO crm_campaign_costs (campaign_id, cost_category, amount, currency, description)
                  VALUES (@c, @cat, @a, @cur, @d) RETURNING id", conn);
            cmd.Parameters.AddWithValue("c", id);
            cmd.Parameters.AddWithValue("cat", body.GetProperty("cost_category").GetString() ?? "other");
            cmd.Parameters.AddWithValue("a", body.GetProperty("amount").GetDecimal());
            cmd.Parameters.AddWithValue("cur", body.TryGetProperty("currency", out var cur) ? cur.GetString() ?? "GBP" : "GBP");
            cmd.Parameters.AddWithValue("d", body.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "");
            var cid = (int)(await cmd.ExecuteScalarAsync())!;
            return Results.Ok(new { id = cid });
        });

        // Influence (materialized view)
        group.MapGet("/influence", async (DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                "SELECT * FROM crm_campaign_influence ORDER BY revenue_linear DESC", conn);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        group.MapPost("/refresh-influence", async (DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand("SELECT refresh_crm_attribution()", conn);
            await cmd.ExecuteNonQueryAsync();
            return Results.Ok(new { refreshed = true });
        });

        return group;
    }
}

public static class SegmentSequenceEndpoints
{
    public static RouteGroupBuilder MapSegmentSequenceEndpoints(this RouteGroupBuilder group)
    {
        // Segments
        group.MapGet("/segments", async (DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                "SELECT * FROM crm_segments WHERE is_active = true ORDER BY name", conn);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        group.MapPost("/segments", async (JsonElement body, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                @"INSERT INTO crm_segments (name, description, segment_type, member_type, rule_expression)
                  VALUES (@n, @d, @st, @mt, @r::jsonb) RETURNING id", conn);
            cmd.Parameters.AddWithValue("n", body.GetProperty("name").GetString() ?? "");
            cmd.Parameters.AddWithValue("d", body.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "");
            cmd.Parameters.AddWithValue("st", body.TryGetProperty("segment_type", out var st) ? st.GetString() ?? "static" : "static");
            cmd.Parameters.AddWithValue("mt", body.TryGetProperty("member_type", out var mt) ? mt.GetString() ?? "contact" : "contact");
            cmd.Parameters.AddWithValue("r", body.TryGetProperty("rule_expression", out var re) ? re.GetRawText() : "{}");
            var id = (int)(await cmd.ExecuteScalarAsync())!;
            return Results.Created($"/api/crm/marketing/segments/{id}", new { id });
        });

        // Sequences
        group.MapGet("/sequences", async (DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                "SELECT * FROM crm_email_sequences ORDER BY name", conn);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        group.MapPost("/sequences", async (JsonElement body, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                @"INSERT INTO crm_email_sequences (name, description, trigger_event, trigger_config, stop_on_reply, stop_on_unsubscribe, stop_on_meeting)
                  VALUES (@n, @d, @te, @tc::jsonb, @sr, @su, @sm) RETURNING id", conn);
            cmd.Parameters.AddWithValue("n", body.GetProperty("name").GetString() ?? "");
            cmd.Parameters.AddWithValue("d", body.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "");
            cmd.Parameters.AddWithValue("te", body.TryGetProperty("trigger_event", out var te) ? te.GetString() ?? "manual" : "manual");
            cmd.Parameters.AddWithValue("tc", body.TryGetProperty("trigger_config", out var tc) ? tc.GetRawText() : "{}");
            cmd.Parameters.AddWithValue("sr", !body.TryGetProperty("stop_on_reply", out var sr) || sr.GetBoolean());
            cmd.Parameters.AddWithValue("su", !body.TryGetProperty("stop_on_unsubscribe", out var su) || su.GetBoolean());
            cmd.Parameters.AddWithValue("sm", !body.TryGetProperty("stop_on_meeting", out var sm) || sm.GetBoolean());
            var id = (int)(await cmd.ExecuteScalarAsync())!;
            return Results.Created($"/api/crm/marketing/sequences/{id}", new { id });
        });

        group.MapPost("/sequences/{id:int}/enroll", async (int id, JsonElement body, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                @"INSERT INTO crm_sequence_enrollments (sequence_id, member_type, member_id, next_action_at)
                  VALUES (@sid, @mt, @mid, NOW())
                  ON CONFLICT (sequence_id, member_type, member_id) DO UPDATE SET status = 'active'
                  RETURNING id", conn);
            cmd.Parameters.AddWithValue("sid", id);
            cmd.Parameters.AddWithValue("mt", body.GetProperty("member_type").GetString() ?? "contact");
            cmd.Parameters.AddWithValue("mid", body.GetProperty("member_id").GetInt32());
            var eid = (int)(await cmd.ExecuteScalarAsync())!;
            return Results.Ok(new { enrollment_id = eid });
        });

        // Landing pages (public GET)
        group.MapGet("/landing-pages/{slug}", async (string slug, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                "SELECT id, title, content_html, campaign_id FROM crm_landing_pages WHERE slug = @s AND is_published = true", conn);
            cmd.Parameters.AddWithValue("s", slug);
            await using var r = await cmd.ExecuteReaderAsync();
            if (!await r.ReadAsync()) return Results.NotFound();
            var result = new {
                id = r.GetInt32(0), title = r.GetString(1), content_html = r.GetString(2),
                campaign_id = r.IsDBNull(3) ? (int?)null : r.GetInt32(3)
            };
            await r.CloseAsync();
            await using var inc = new NpgsqlCommand("UPDATE crm_landing_pages SET view_count = view_count + 1 WHERE slug = @s", conn);
            inc.Parameters.AddWithValue("s", slug);
            await inc.ExecuteNonQueryAsync();
            return Results.Ok(result);
        }).AllowAnonymous();

        // Public form submission
        group.MapPost("/forms/{slug}/submit", async (string slug, JsonElement body, HttpContext ctx, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var lookup = new NpgsqlCommand(
                "SELECT id, on_submit_action, on_submit_config, campaign_id FROM crm_forms WHERE slug = @s AND is_active = true", conn);
            lookup.Parameters.AddWithValue("s", slug);
            await using var fr = await lookup.ExecuteReaderAsync();
            if (!await fr.ReadAsync()) return Results.NotFound();
            var formId = fr.GetInt32(0);
            var action = fr.GetString(1);
            var campaignId = fr.IsDBNull(3) ? (int?)null : fr.GetInt32(3);
            await fr.CloseAsync();

            // Insert submission
            await using var sub = new NpgsqlCommand(
                @"INSERT INTO crm_form_submissions (form_id, payload, ip_address, user_agent, referrer,
                    utm_source, utm_medium, utm_campaign, utm_term, utm_content)
                  VALUES (@f, @p::jsonb, @ip::inet, @ua, @ref, @src, @med, @camp, @trm, @cont) RETURNING id", conn);
            sub.Parameters.AddWithValue("f", formId);
            sub.Parameters.AddWithValue("p", body.GetRawText());
            sub.Parameters.AddWithValue("ip", (object)(ctx.Connection.RemoteIpAddress?.ToString() ?? "0.0.0.0"));
            sub.Parameters.AddWithValue("ua", (object)(ctx.Request.Headers.UserAgent.ToString() ?? ""));
            sub.Parameters.AddWithValue("ref", (object?)(ctx.Request.Headers.Referer.ToString()) ?? DBNull.Value);
            sub.Parameters.AddWithValue("src", GetUtm(body, "utm_source"));
            sub.Parameters.AddWithValue("med", GetUtm(body, "utm_medium"));
            sub.Parameters.AddWithValue("camp", GetUtm(body, "utm_campaign"));
            sub.Parameters.AddWithValue("trm", GetUtm(body, "utm_term"));
            sub.Parameters.AddWithValue("cont", GetUtm(body, "utm_content"));
            var subId = (long)(await sub.ExecuteScalarAsync())!;

            // Auto-action: create lead
            if (action == "create_lead" && body.TryGetProperty("email", out var em) && !string.IsNullOrEmpty(em.GetString()))
            {
                await using var lead = new NpgsqlCommand(
                    @"INSERT INTO crm_leads (first_name, last_name, email, phone, company_name, title, source, campaign_id)
                      VALUES (@fn, @ln, @em, @ph, @co, @t, 'web_form', @camp) RETURNING id", conn);
                lead.Parameters.AddWithValue("fn", GetStr(body, "first_name"));
                lead.Parameters.AddWithValue("ln", GetStr(body, "last_name"));
                lead.Parameters.AddWithValue("em", em.GetString() ?? "");
                lead.Parameters.AddWithValue("ph", GetStr(body, "phone"));
                lead.Parameters.AddWithValue("co", GetStr(body, "company"));
                lead.Parameters.AddWithValue("t", GetStr(body, "title"));
                lead.Parameters.AddWithValue("camp", campaignId.HasValue ? campaignId.Value : DBNull.Value);
                var leadId = (int)(await lead.ExecuteScalarAsync())!;
                await using var link = new NpgsqlCommand(
                    "UPDATE crm_form_submissions SET created_lead_id = @l WHERE id = @s", conn);
                link.Parameters.AddWithValue("l", leadId);
                link.Parameters.AddWithValue("s", subId);
                await link.ExecuteNonQueryAsync();
            }
            return Results.Ok(new { submission_id = subId });
        }).AllowAnonymous();

        return group;
    }

    private static string GetStr(JsonElement el, string key)
        => el.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
    private static object GetUtm(JsonElement el, string key)
    {
        var v = GetStr(el, key);
        return string.IsNullOrEmpty(v) ? (object)DBNull.Value : v;
    }
}

// ══════════════════════════════════════════════════════════════════════
// Stage 2: Sales Operations
// ══════════════════════════════════════════════════════════════════════

public static class SalesOpsEndpoints
{
    public static RouteGroupBuilder MapSalesOpsEndpoints(this RouteGroupBuilder group)
    {
        // ── Territories ──
        group.MapGet("/territories", async (DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(@"
                SELECT t.*, (SELECT COUNT(*) FROM crm_territory_members WHERE territory_id = t.id) AS member_count,
                       (SELECT COUNT(*) FROM crm_accounts WHERE territory_id = t.id AND is_deleted IS NOT TRUE) AS account_count
                FROM crm_territories t WHERE t.is_active = true ORDER BY t.name", conn);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        group.MapPost("/territories", async (JsonElement body, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                @"INSERT INTO crm_territories (name, parent_id, territory_type, description)
                  VALUES (@n, @p, @t, @d) RETURNING id", conn);
            cmd.Parameters.AddWithValue("n", body.GetProperty("name").GetString() ?? "");
            cmd.Parameters.AddWithValue("p", body.TryGetProperty("parent_id", out var p) && p.ValueKind == JsonValueKind.Number ? p.GetInt32() : DBNull.Value);
            cmd.Parameters.AddWithValue("t", body.TryGetProperty("territory_type", out var t) ? t.GetString() ?? "geographic" : "geographic");
            cmd.Parameters.AddWithValue("d", body.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "");
            var id = (int)(await cmd.ExecuteScalarAsync())!;
            return Results.Created($"/api/crm/salesops/territories/{id}", new { id });
        });

        // ── Quotas ──
        group.MapGet("/quotas", async (DbConnectionFactory db, int? user_id) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            var where = user_id.HasValue ? "WHERE q.user_id = @uid" : "";
            await using var cmd = new NpgsqlCommand($@"
                SELECT q.*, COALESCE(u.display_name,'') AS user_name,
                    COALESCE((SELECT SUM(d.value) FROM crm_deals d
                              WHERE d.owner_id = q.user_id AND d.stage='Closed Won'
                                AND d.actual_close BETWEEN q.period_start AND q.period_end), 0) AS achieved_amount
                FROM crm_quotas q
                JOIN app_users u ON u.id = q.user_id
                {where}
                ORDER BY q.period_start DESC", conn);
            if (user_id.HasValue) cmd.Parameters.AddWithValue("uid", user_id.Value);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        group.MapPost("/quotas", async (JsonElement body, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                @"INSERT INTO crm_quotas (user_id, territory_id, product_category, period_type, period_start, period_end, target_amount, currency, ramp_pct)
                  VALUES (@u, @t, @pc, @pt, @ps, @pe, @a, @c, @rp)
                  ON CONFLICT (user_id, period_start, period_end, product_category) DO UPDATE
                  SET target_amount = @a, ramp_pct = @rp RETURNING id", conn);
            cmd.Parameters.AddWithValue("u", body.GetProperty("user_id").GetInt32());
            cmd.Parameters.AddWithValue("t", body.TryGetProperty("territory_id", out var t) && t.ValueKind == JsonValueKind.Number ? t.GetInt32() : DBNull.Value);
            cmd.Parameters.AddWithValue("pc", body.TryGetProperty("product_category", out var pc) ? pc.GetString() ?? "" : "");
            cmd.Parameters.AddWithValue("pt", body.TryGetProperty("period_type", out var pt) ? pt.GetString() ?? "quarterly" : "quarterly");
            cmd.Parameters.AddWithValue("ps", DateTime.Parse(body.GetProperty("period_start").GetString() ?? "").Date);
            cmd.Parameters.AddWithValue("pe", DateTime.Parse(body.GetProperty("period_end").GetString() ?? "").Date);
            cmd.Parameters.AddWithValue("a", body.GetProperty("target_amount").GetDecimal());
            cmd.Parameters.AddWithValue("c", body.TryGetProperty("currency", out var cu) ? cu.GetString() ?? "GBP" : "GBP");
            cmd.Parameters.AddWithValue("rp", body.TryGetProperty("ramp_pct", out var rp) && rp.ValueKind == JsonValueKind.Number ? rp.GetDecimal() : 100m);
            var id = (int)(await cmd.ExecuteScalarAsync())!;
            return Results.Created($"/api/crm/salesops/quotas/{id}", new { id });
        });

        // ── Commission plans ──
        group.MapGet("/commission-plans", async (DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                "SELECT * FROM crm_commission_plans WHERE is_active = true ORDER BY name", conn);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        group.MapGet("/commission-payouts", async (DbConnectionFactory db, int? user_id) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            var where = user_id.HasValue ? "WHERE user_id = @uid" : "";
            await using var cmd = new NpgsqlCommand(
                $"SELECT p.*, u.display_name AS user_name FROM crm_commission_payouts p JOIN app_users u ON u.id = p.user_id {where} ORDER BY period_end DESC", conn);
            if (user_id.HasValue) cmd.Parameters.AddWithValue("uid", user_id.Value);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        // ── Opportunity splits ──
        group.MapGet("/deals/{id:int}/splits", async (int id, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                @"SELECT s.*, u.display_name AS user_name FROM crm_opportunity_splits s
                  JOIN app_users u ON u.id = s.user_id WHERE deal_id = @id ORDER BY split_type, credit_pct DESC", conn);
            cmd.Parameters.AddWithValue("id", id);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        group.MapPost("/deals/{id:int}/splits", async (int id, JsonElement body, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                @"INSERT INTO crm_opportunity_splits (deal_id, user_id, split_type, credit_pct, role)
                  VALUES (@d, @u, @t, @p, @r)
                  ON CONFLICT (deal_id, user_id, split_type) DO UPDATE SET credit_pct = @p, role = @r RETURNING id", conn);
            cmd.Parameters.AddWithValue("d", id);
            cmd.Parameters.AddWithValue("u", body.GetProperty("user_id").GetInt32());
            cmd.Parameters.AddWithValue("t", body.TryGetProperty("split_type", out var t) ? t.GetString() ?? "revenue" : "revenue");
            cmd.Parameters.AddWithValue("p", body.GetProperty("credit_pct").GetDecimal());
            cmd.Parameters.AddWithValue("r", body.TryGetProperty("role", out var r) ? r.GetString() ?? "" : "");
            var sid = (int)(await cmd.ExecuteScalarAsync())!;
            return Results.Ok(new { id = sid });
        });

        // ── Account teams ──
        group.MapGet("/accounts/{id:int}/team", async (int id, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                @"SELECT t.*, u.display_name AS user_name FROM crm_account_teams t
                  JOIN app_users u ON u.id = t.user_id WHERE t.account_id = @id ORDER BY u.display_name", conn);
            cmd.Parameters.AddWithValue("id", id);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        group.MapPost("/accounts/{id:int}/team", async (int id, JsonElement body, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                @"INSERT INTO crm_account_teams (account_id, user_id, team_role, access_level)
                  VALUES (@a, @u, @r, @al)
                  ON CONFLICT (account_id, user_id, team_role) DO UPDATE SET access_level = @al RETURNING id", conn);
            cmd.Parameters.AddWithValue("a", id);
            cmd.Parameters.AddWithValue("u", body.GetProperty("user_id").GetInt32());
            cmd.Parameters.AddWithValue("r", body.GetProperty("team_role").GetString() ?? "member");
            cmd.Parameters.AddWithValue("al", body.TryGetProperty("access_level", out var al) ? al.GetString() ?? "read" : "read");
            var tid = (int)(await cmd.ExecuteScalarAsync())!;
            return Results.Ok(new { id = tid });
        });

        // ── Account plans ──
        group.MapGet("/accounts/{id:int}/plan", async (int id, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                "SELECT * FROM crm_account_plans WHERE account_id = @id", conn);
            cmd.Parameters.AddWithValue("id", id);
            await using var r = await cmd.ExecuteReaderAsync();
            if (!await r.ReadAsync()) return Results.Ok(new { });
            var row = new Dictionary<string, object?>();
            for (int i = 0; i < r.FieldCount; i++) row[r.GetName(i)] = r.IsDBNull(i) ? null : r.GetValue(i);
            return Results.Ok(row);
        });

        group.MapPut("/accounts/{id:int}/plan", async (int id, JsonElement body, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                @"INSERT INTO crm_account_plans
                    (account_id, fiscal_year, annual_target, strategic_goals, known_initiatives, known_budget, owner_id)
                  VALUES (@a, @fy, @at, @sg, @ki, @kb, @o)
                  ON CONFLICT (account_id) DO UPDATE SET
                    fiscal_year = @fy, annual_target = @at, strategic_goals = @sg,
                    known_initiatives = @ki, known_budget = @kb, owner_id = @o, updated_at = NOW()
                  RETURNING id", conn);
            cmd.Parameters.AddWithValue("a", id);
            cmd.Parameters.AddWithValue("fy", body.TryGetProperty("fiscal_year", out var fy) && fy.ValueKind == JsonValueKind.Number ? fy.GetInt32() : DBNull.Value);
            cmd.Parameters.AddWithValue("at", body.TryGetProperty("annual_target", out var at) && at.ValueKind == JsonValueKind.Number ? at.GetDecimal() : DBNull.Value);
            cmd.Parameters.AddWithValue("sg", body.TryGetProperty("strategic_goals", out var sg) ? sg.GetString() ?? "" : "");
            cmd.Parameters.AddWithValue("ki", body.TryGetProperty("known_initiatives", out var ki) ? ki.GetString() ?? "" : "");
            cmd.Parameters.AddWithValue("kb", body.TryGetProperty("known_budget", out var kb) && kb.ValueKind == JsonValueKind.Number ? kb.GetDecimal() : DBNull.Value);
            cmd.Parameters.AddWithValue("o", body.TryGetProperty("owner_id", out var o) && o.ValueKind == JsonValueKind.Number ? o.GetInt32() : DBNull.Value);
            var pid = (int)(await cmd.ExecuteScalarAsync())!;
            return Results.Ok(new { id = pid });
        });

        // ── Pipeline health ──
        group.MapGet("/pipeline-health", async (DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                "SELECT * FROM crm_pipeline_health ORDER BY weighted_pipeline DESC", conn);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        // ── Deal insights ──
        group.MapGet("/deal-insights", async (DbConnectionFactory db, int? deal_id, bool? resolved) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            var where = new List<string>();
            var parms = new List<(string, object)>();
            if (deal_id.HasValue) { where.Add("deal_id = @d"); parms.Add(("d", deal_id.Value)); }
            if (resolved.HasValue) { where.Add("is_resolved = @r"); parms.Add(("r", resolved.Value)); }
            var whereSql = where.Count > 0 ? "WHERE " + string.Join(" AND ", where) : "";
            await using var cmd = new NpgsqlCommand(
                $"SELECT * FROM crm_deal_insights {whereSql} ORDER BY detected_at DESC LIMIT 500", conn);
            foreach (var (n, v) in parms) cmd.Parameters.AddWithValue(n, v);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        group.MapPost("/deal-insights/generate", async (DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand("SELECT generate_deal_insights()", conn);
            var count = (int)(await cmd.ExecuteScalarAsync())!;
            return Results.Ok(new { generated = count });
        });

        return group;
    }
}

// ══════════════════════════════════════════════════════════════════════
// Stage 3: CPQ + Contracts + Revenue
// ══════════════════════════════════════════════════════════════════════

public static class CpqEndpoints
{
    public static RouteGroupBuilder MapCpqEndpoints(this RouteGroupBuilder group)
    {
        // Product bundles
        group.MapGet("/bundles", async (DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(@"
                SELECT b.*, p.name AS parent_product_name
                FROM crm_product_bundles b JOIN crm_products p ON p.id = b.parent_product_id
                WHERE b.is_active = true ORDER BY b.name", conn);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        group.MapPost("/bundles", async (JsonElement body, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                @"INSERT INTO crm_product_bundles (parent_product_id, name, description)
                  VALUES (@p, @n, @d) RETURNING id", conn);
            cmd.Parameters.AddWithValue("p", body.GetProperty("parent_product_id").GetInt32());
            cmd.Parameters.AddWithValue("n", body.GetProperty("name").GetString() ?? "");
            cmd.Parameters.AddWithValue("d", body.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "");
            var id = (int)(await cmd.ExecuteScalarAsync())!;
            return Results.Created($"/api/crm/cpq/bundles/{id}", new { id });
        });

        group.MapPost("/bundles/{id:int}/components", async (int id, JsonElement body, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                @"INSERT INTO crm_bundle_components (bundle_id, component_product_id, quantity, is_optional, override_price, sort_order)
                  VALUES (@b, @p, @q, @o, @op, @s) RETURNING id", conn);
            cmd.Parameters.AddWithValue("b", id);
            cmd.Parameters.AddWithValue("p", body.GetProperty("component_product_id").GetInt32());
            cmd.Parameters.AddWithValue("q", body.TryGetProperty("quantity", out var q) && q.ValueKind == JsonValueKind.Number ? q.GetDecimal() : 1m);
            cmd.Parameters.AddWithValue("o", body.TryGetProperty("is_optional", out var o) && o.GetBoolean());
            cmd.Parameters.AddWithValue("op", body.TryGetProperty("override_price", out var op) && op.ValueKind == JsonValueKind.Number ? op.GetDecimal() : DBNull.Value);
            cmd.Parameters.AddWithValue("s", body.TryGetProperty("sort_order", out var so) && so.ValueKind == JsonValueKind.Number ? so.GetInt32() : 0);
            var cid = (int)(await cmd.ExecuteScalarAsync())!;
            return Results.Ok(new { id = cid });
        });

        // Pricing rules
        group.MapGet("/pricing-rules", async (DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                "SELECT * FROM crm_pricing_rules WHERE is_active = true ORDER BY priority, name", conn);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        group.MapPost("/pricing-rules", async (JsonElement body, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                @"INSERT INTO crm_pricing_rules (name, rule_type, product_id, bundle_id, min_quantity, max_quantity,
                    account_id, promo_code, valid_from, valid_to, max_uses, discount_pct, discount_amount, fixed_price, priority)
                  VALUES (@n, @t, @p, @b, @minq, @maxq, @a, @pc, @vf, @vt, @mu, @dp, @da, @fp, @pr) RETURNING id", conn);
            cmd.Parameters.AddWithValue("n", body.GetProperty("name").GetString() ?? "");
            cmd.Parameters.AddWithValue("t", body.GetProperty("rule_type").GetString() ?? "volume");
            cmd.Parameters.AddWithValue("p", body.TryGetProperty("product_id", out var p) && p.ValueKind == JsonValueKind.Number ? p.GetInt32() : DBNull.Value);
            cmd.Parameters.AddWithValue("b", body.TryGetProperty("bundle_id", out var b) && b.ValueKind == JsonValueKind.Number ? b.GetInt32() : DBNull.Value);
            cmd.Parameters.AddWithValue("minq", body.TryGetProperty("min_quantity", out var minq) && minq.ValueKind == JsonValueKind.Number ? minq.GetDecimal() : DBNull.Value);
            cmd.Parameters.AddWithValue("maxq", body.TryGetProperty("max_quantity", out var maxq) && maxq.ValueKind == JsonValueKind.Number ? maxq.GetDecimal() : DBNull.Value);
            cmd.Parameters.AddWithValue("a", body.TryGetProperty("account_id", out var a) && a.ValueKind == JsonValueKind.Number ? a.GetInt32() : DBNull.Value);
            cmd.Parameters.AddWithValue("pc", body.TryGetProperty("promo_code", out var pc) ? pc.GetString() ?? "" : "");
            cmd.Parameters.AddWithValue("vf", body.TryGetProperty("valid_from", out var vf) && vf.ValueKind == JsonValueKind.String ? DateTime.Parse(vf.GetString()!).Date : DBNull.Value);
            cmd.Parameters.AddWithValue("vt", body.TryGetProperty("valid_to", out var vt) && vt.ValueKind == JsonValueKind.String ? DateTime.Parse(vt.GetString()!).Date : DBNull.Value);
            cmd.Parameters.AddWithValue("mu", body.TryGetProperty("max_uses", out var mu) && mu.ValueKind == JsonValueKind.Number ? mu.GetInt32() : DBNull.Value);
            cmd.Parameters.AddWithValue("dp", body.TryGetProperty("discount_pct", out var dp) && dp.ValueKind == JsonValueKind.Number ? dp.GetDecimal() : DBNull.Value);
            cmd.Parameters.AddWithValue("da", body.TryGetProperty("discount_amount", out var da) && da.ValueKind == JsonValueKind.Number ? da.GetDecimal() : DBNull.Value);
            cmd.Parameters.AddWithValue("fp", body.TryGetProperty("fixed_price", out var fp) && fp.ValueKind == JsonValueKind.Number ? fp.GetDecimal() : DBNull.Value);
            cmd.Parameters.AddWithValue("pr", body.TryGetProperty("priority", out var pr) && pr.ValueKind == JsonValueKind.Number ? pr.GetInt32() : 100);
            var id = (int)(await cmd.ExecuteScalarAsync())!;
            return Results.Created($"/api/crm/cpq/pricing-rules/{id}", new { id });
        });

        // Discount approval
        group.MapGet("/discount-matrix", async (DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                "SELECT * FROM crm_discount_approval_matrix WHERE is_active = true ORDER BY priority, min_discount_pct", conn);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        return group;
    }
}

public static class ApprovalEndpoints
{
    public static RouteGroupBuilder MapApprovalEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", async (DbConnectionFactory db, string? status) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            var where = string.IsNullOrEmpty(status) ? "" : "WHERE r.status = @st";
            await using var cmd = new NpgsqlCommand(
                $@"SELECT r.*, u.display_name AS requested_by_name
                   FROM approval_requests r LEFT JOIN app_users u ON u.id = r.requested_by
                   {where} ORDER BY r.requested_at DESC LIMIT 200", conn);
            if (!string.IsNullOrEmpty(status)) cmd.Parameters.AddWithValue("st", status);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        group.MapPost("/", async (ClaimsPrincipal principal, JsonElement body, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            var username = principal.Identity?.Name ?? "";

            await using var reqCmd = new NpgsqlCommand(
                @"INSERT INTO approval_requests (entity_type, entity_id, requested_by, approval_type, priority, context, reason, expires_at)
                  SELECT @et, @eid, u.id, @at, @p, @c::jsonb, @r, @exp FROM app_users u WHERE u.username = @u RETURNING id", conn);
            reqCmd.Parameters.AddWithValue("u", username);
            reqCmd.Parameters.AddWithValue("et", body.GetProperty("entity_type").GetString() ?? "");
            reqCmd.Parameters.AddWithValue("eid", body.GetProperty("entity_id").GetInt32());
            reqCmd.Parameters.AddWithValue("at", body.GetProperty("approval_type").GetString() ?? "");
            reqCmd.Parameters.AddWithValue("p", body.TryGetProperty("priority", out var pr) ? pr.GetString() ?? "normal" : "normal");
            reqCmd.Parameters.AddWithValue("c", body.TryGetProperty("context", out var cx) ? cx.GetRawText() : "{}");
            reqCmd.Parameters.AddWithValue("r", body.TryGetProperty("reason", out var rs) ? rs.GetString() ?? "" : "");
            reqCmd.Parameters.AddWithValue("exp", body.TryGetProperty("expires_at", out var exp) && exp.ValueKind == JsonValueKind.String ? DateTime.Parse(exp.GetString()!) : DBNull.Value);
            var id = (int)(await reqCmd.ExecuteScalarAsync())!;

            // Add steps
            if (body.TryGetProperty("steps", out var steps) && steps.ValueKind == JsonValueKind.Array)
            {
                int order = 1;
                foreach (var step in steps.EnumerateArray())
                {
                    await using var sc = new NpgsqlCommand(
                        @"INSERT INTO approval_steps (request_id, step_order, approver_user_id, approver_role, is_parallel, status)
                          VALUES (@r, @o, @a, @ar, @p, CASE WHEN @o = 1 THEN 'pending' ELSE 'waiting' END)", conn);
                    sc.Parameters.AddWithValue("r", id);
                    sc.Parameters.AddWithValue("o", step.TryGetProperty("step_order", out var so) && so.ValueKind == JsonValueKind.Number ? so.GetInt32() : order);
                    sc.Parameters.AddWithValue("a", step.TryGetProperty("approver_user_id", out var au) && au.ValueKind == JsonValueKind.Number ? au.GetInt32() : DBNull.Value);
                    sc.Parameters.AddWithValue("ar", step.TryGetProperty("approver_role", out var ar) ? ar.GetString() ?? "" : "");
                    sc.Parameters.AddWithValue("p", step.TryGetProperty("is_parallel", out var ip) && ip.GetBoolean());
                    await sc.ExecuteNonQueryAsync();
                    order++;
                }
            }
            return Results.Created($"/api/approvals/{id}", new { id });
        });

        group.MapPost("/{id:int}/act", async (int id, ClaimsPrincipal principal, JsonElement body, DbConnectionFactory db) =>
        {
            var action = body.GetProperty("action").GetString() ?? "";
            if (action != "approve" && action != "reject")
                return ApiProblem.ValidationError("action must be 'approve' or 'reject'");

            var username = principal.Identity?.Name ?? "";
            await using var conn = await db.OpenConnectionAsync();

            // Find actor's pending step
            await using var step = new NpgsqlCommand(
                @"UPDATE approval_steps SET status = @ns, comment = @c, acted_at = NOW()
                  FROM app_users u WHERE u.username = @u AND approval_steps.request_id = @id
                    AND approval_steps.status = 'pending'
                    AND (approval_steps.approver_user_id = u.id OR approval_steps.approver_role = u.role)
                  RETURNING approval_steps.id, approval_steps.step_order", conn);
            step.Parameters.AddWithValue("u", username);
            step.Parameters.AddWithValue("id", id);
            step.Parameters.AddWithValue("ns", action == "approve" ? "approved" : "rejected");
            step.Parameters.AddWithValue("c", body.TryGetProperty("comment", out var c) ? c.GetString() ?? "" : "");

            await using var rr = await step.ExecuteReaderAsync();
            if (!await rr.ReadAsync()) return ApiProblem.NotFound("No pending step for this approver");
            var stepId = rr.GetInt32(0);
            var stepOrder = rr.GetInt32(1);
            await rr.CloseAsync();

            // Log action
            await using var log = new NpgsqlCommand(
                @"INSERT INTO approval_actions (request_id, step_id, action, actor_id, comment)
                  SELECT @r, @s, @a, u.id, @c FROM app_users u WHERE u.username = @u", conn);
            log.Parameters.AddWithValue("u", username);
            log.Parameters.AddWithValue("r", id);
            log.Parameters.AddWithValue("s", stepId);
            log.Parameters.AddWithValue("a", action + "d");
            log.Parameters.AddWithValue("c", body.TryGetProperty("comment", out var cm) ? cm.GetString() ?? "" : "");
            await log.ExecuteNonQueryAsync();

            // If approved and not rejected, activate next step
            if (action == "approve")
            {
                await using var next = new NpgsqlCommand(
                    "UPDATE approval_steps SET status = 'pending' WHERE request_id = @id AND step_order = @o + 1 AND status = 'waiting'", conn);
                next.Parameters.AddWithValue("id", id);
                next.Parameters.AddWithValue("o", stepOrder);
                await next.ExecuteNonQueryAsync();
            }

            return Results.Ok(new { action });
        });

        return group;
    }
}

public static class ContractEndpoints
{
    public static RouteGroupBuilder MapContractEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", async (DbConnectionFactory db, string? status, int? account_id) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            var where = new List<string> { "c.is_deleted IS NOT TRUE" };
            var parms = new List<(string, object)>();
            if (!string.IsNullOrEmpty(status))    { where.Add("c.status = @s");      parms.Add(("s", status)); }
            if (account_id.HasValue)               { where.Add("c.account_id = @a"); parms.Add(("a", account_id.Value)); }
            var whereSql = string.Join(" AND ", where);
            await using var cmd = new NpgsqlCommand(
                $@"SELECT c.*, a.name AS account_name, u.display_name AS owner_name
                   FROM crm_contracts c
                   LEFT JOIN crm_accounts a ON a.id = c.account_id
                   LEFT JOIN app_users u ON u.id = c.owner_id
                   WHERE {whereSql} ORDER BY c.end_date NULLS LAST LIMIT 500", conn);
            foreach (var (n, v) in parms) cmd.Parameters.AddWithValue(n, v);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        group.MapGet("/renewals", async (DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                "SELECT * FROM crm_contract_renewals ORDER BY end_date", conn);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        group.MapPost("/", async (JsonElement body, DbConnectionFactory db) =>
        {
            var num = body.TryGetProperty("contract_number", out var cn) ? cn.GetString() ?? "" : $"CON-{DateTime.UtcNow:yyyyMM}-{Guid.NewGuid().ToString()[..6].ToUpper()}";
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                @"INSERT INTO crm_contracts (contract_number, title, contract_type, account_id, deal_id, quote_id,
                    contract_value, currency, start_date, end_date, auto_renew, renewal_term_months, renewal_notice_days, owner_id, counter_party)
                  VALUES (@n, @t, @ct, @a, @d, @q, @v, @c, @s, @e, @ar, @rt, @rnd, @o, @cp) RETURNING id", conn);
            cmd.Parameters.AddWithValue("n", num);
            cmd.Parameters.AddWithValue("t", body.GetProperty("title").GetString() ?? "");
            cmd.Parameters.AddWithValue("ct", body.GetProperty("contract_type").GetString() ?? "msa");
            cmd.Parameters.AddWithValue("a", body.TryGetProperty("account_id", out var a) && a.ValueKind == JsonValueKind.Number ? a.GetInt32() : DBNull.Value);
            cmd.Parameters.AddWithValue("d", body.TryGetProperty("deal_id", out var d) && d.ValueKind == JsonValueKind.Number ? d.GetInt32() : DBNull.Value);
            cmd.Parameters.AddWithValue("q", body.TryGetProperty("quote_id", out var q) && q.ValueKind == JsonValueKind.Number ? q.GetInt32() : DBNull.Value);
            cmd.Parameters.AddWithValue("v", body.TryGetProperty("contract_value", out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDecimal() : DBNull.Value);
            cmd.Parameters.AddWithValue("c", body.TryGetProperty("currency", out var cur) ? cur.GetString() ?? "GBP" : "GBP");
            cmd.Parameters.AddWithValue("s", body.TryGetProperty("start_date", out var s) && s.ValueKind == JsonValueKind.String ? DateTime.Parse(s.GetString()!).Date : DBNull.Value);
            cmd.Parameters.AddWithValue("e", body.TryGetProperty("end_date", out var e) && e.ValueKind == JsonValueKind.String ? DateTime.Parse(e.GetString()!).Date : DBNull.Value);
            cmd.Parameters.AddWithValue("ar", body.TryGetProperty("auto_renew", out var ar) && ar.GetBoolean());
            cmd.Parameters.AddWithValue("rt", body.TryGetProperty("renewal_term_months", out var rt) && rt.ValueKind == JsonValueKind.Number ? rt.GetInt32() : DBNull.Value);
            cmd.Parameters.AddWithValue("rnd", body.TryGetProperty("renewal_notice_days", out var rnd) && rnd.ValueKind == JsonValueKind.Number ? rnd.GetInt32() : 90);
            cmd.Parameters.AddWithValue("o", body.TryGetProperty("owner_id", out var o) && o.ValueKind == JsonValueKind.Number ? o.GetInt32() : DBNull.Value);
            cmd.Parameters.AddWithValue("cp", body.TryGetProperty("counter_party", out var cp) ? cp.GetString() ?? "" : "");
            var id = (int)(await cmd.ExecuteScalarAsync())!;
            return Results.Created($"/api/crm/contracts/{id}", new { id, contract_number = num });
        });

        group.MapPost("/{id:int}/sign", async (int id, JsonElement body, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                @"UPDATE crm_contracts SET status = 'active', signed_at = NOW(), signed_by_name = @by
                  WHERE id = @id RETURNING id", conn);
            cmd.Parameters.AddWithValue("id", id);
            cmd.Parameters.AddWithValue("by", body.TryGetProperty("signed_by_name", out var by) ? by.GetString() ?? "" : "");
            var r = await cmd.ExecuteScalarAsync();
            return r is null ? ApiProblem.NotFound("Contract not found") : Results.Ok(new { id, signed = true });
        });

        // Clauses library
        group.MapGet("/clauses", async (DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                "SELECT * FROM crm_contract_clauses WHERE is_active = true ORDER BY category, clause_code", conn);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        // Milestones
        group.MapGet("/{id:int}/milestones", async (int id, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                "SELECT * FROM crm_contract_milestones WHERE contract_id = @id ORDER BY due_date", conn);
            cmd.Parameters.AddWithValue("id", id);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        return group;
    }
}

public static class SubscriptionEndpoints
{
    public static RouteGroupBuilder MapSubscriptionEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", async (DbConnectionFactory db, string? status, int? account_id) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            var where = new List<string>();
            var parms = new List<(string, object)>();
            if (!string.IsNullOrEmpty(status)) { where.Add("s.status = @st"); parms.Add(("st", status)); }
            if (account_id.HasValue)            { where.Add("s.account_id = @a"); parms.Add(("a", account_id.Value)); }
            var whereSql = where.Count > 0 ? "WHERE " + string.Join(" AND ", where) : "";
            await using var cmd = new NpgsqlCommand(
                $@"SELECT s.*, a.name AS account_name
                   FROM crm_subscriptions s
                   LEFT JOIN crm_accounts a ON a.id = s.account_id
                   {whereSql} ORDER BY s.start_date DESC LIMIT 500", conn);
            foreach (var (n, v) in parms) cmd.Parameters.AddWithValue(n, v);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        group.MapGet("/mrr-dashboard", async (DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand("SELECT * FROM crm_mrr_dashboard", conn);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        group.MapPost("/", async (JsonElement body, DbConnectionFactory db) =>
        {
            var num = body.TryGetProperty("subscription_number", out var sn) ? sn.GetString() ?? "" : $"SUB-{DateTime.UtcNow:yyyyMM}-{Guid.NewGuid().ToString()[..6].ToUpper()}";
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                @"INSERT INTO crm_subscriptions (account_id, contract_id, product_id, subscription_number, name,
                    quantity, unit_price, mrr, arr, currency, billing_period, start_date, end_date)
                  VALUES (@a, @c, @p, @n, @nm, @q, @up, @mrr, @arr, @cur, @bp, @s, @e) RETURNING id", conn);
            cmd.Parameters.AddWithValue("a", body.GetProperty("account_id").GetInt32());
            cmd.Parameters.AddWithValue("c", body.TryGetProperty("contract_id", out var c) && c.ValueKind == JsonValueKind.Number ? c.GetInt32() : DBNull.Value);
            cmd.Parameters.AddWithValue("p", body.TryGetProperty("product_id", out var p) && p.ValueKind == JsonValueKind.Number ? p.GetInt32() : DBNull.Value);
            cmd.Parameters.AddWithValue("n", num);
            cmd.Parameters.AddWithValue("nm", body.GetProperty("name").GetString() ?? "");
            var qty = body.TryGetProperty("quantity", out var q) && q.ValueKind == JsonValueKind.Number ? q.GetDecimal() : 1m;
            var unitPrice = body.GetProperty("unit_price").GetDecimal();
            var billingPeriod = body.TryGetProperty("billing_period", out var bp) ? bp.GetString() ?? "monthly" : "monthly";
            var lineTotal = qty * unitPrice;
            var mrr = billingPeriod switch { "monthly" => lineTotal, "annual" => lineTotal / 12m, "quarterly" => lineTotal / 3m, _ => lineTotal };
            cmd.Parameters.AddWithValue("q", qty);
            cmd.Parameters.AddWithValue("up", unitPrice);
            cmd.Parameters.AddWithValue("mrr", mrr);
            cmd.Parameters.AddWithValue("arr", mrr * 12);
            cmd.Parameters.AddWithValue("cur", body.TryGetProperty("currency", out var cur) ? cur.GetString() ?? "GBP" : "GBP");
            cmd.Parameters.AddWithValue("bp", billingPeriod);
            cmd.Parameters.AddWithValue("s", body.TryGetProperty("start_date", out var s) && s.ValueKind == JsonValueKind.String ? DateTime.Parse(s.GetString()!).Date : DateTime.UtcNow.Date);
            cmd.Parameters.AddWithValue("e", body.TryGetProperty("end_date", out var e) && e.ValueKind == JsonValueKind.String ? DateTime.Parse(e.GetString()!).Date : DBNull.Value);
            var id = (int)(await cmd.ExecuteScalarAsync())!;
            return Results.Created($"/api/crm/subscriptions/{id}", new { id });
        });

        group.MapPost("/{id:int}/cancel", async (int id, JsonElement body, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                @"UPDATE crm_subscriptions SET status = 'cancelled', cancelled_at = NOW(),
                  cancel_at = @ca WHERE id = @id RETURNING id", conn);
            cmd.Parameters.AddWithValue("id", id);
            cmd.Parameters.AddWithValue("ca", body.TryGetProperty("cancel_at", out var ca) && ca.ValueKind == JsonValueKind.String ? DateTime.Parse(ca.GetString()!).Date : DBNull.Value);
            var r = await cmd.ExecuteScalarAsync();
            return r is null ? ApiProblem.NotFound("Subscription not found") : Results.Ok(new { id, cancelled = true });
        });

        // Subscription events (history)
        group.MapGet("/{id:int}/events", async (int id, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                "SELECT * FROM crm_subscription_events WHERE subscription_id = @id ORDER BY occurred_at DESC", conn);
            cmd.Parameters.AddWithValue("id", id);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        return group;
    }
}

public static class RevenueEndpoints
{
    public static RouteGroupBuilder MapRevenueEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/schedules", async (DbConnectionFactory db, int? subscription_id, int? contract_id) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            var where = new List<string>();
            var parms = new List<(string, object)>();
            if (subscription_id.HasValue) { where.Add("subscription_id = @s"); parms.Add(("s", subscription_id.Value)); }
            if (contract_id.HasValue)     { where.Add("contract_id = @c");     parms.Add(("c", contract_id.Value)); }
            var whereSql = where.Count > 0 ? "WHERE " + string.Join(" AND ", where) : "";
            await using var cmd = new NpgsqlCommand(
                $"SELECT * FROM crm_revenue_schedules {whereSql} ORDER BY start_date", conn);
            foreach (var (n, v) in parms) cmd.Parameters.AddWithValue(n, v);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        group.MapPost("/schedules", async (JsonElement body, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                @"INSERT INTO crm_revenue_schedules
                    (subscription_id, contract_id, order_id, product_id, performance_obligation,
                     recognition_method, total_amount, currency, start_date, end_date, periods)
                  VALUES (@s, @c, @o, @p, @po, @rm, @t, @cur, @sd, @ed, @per) RETURNING id", conn);
            cmd.Parameters.AddWithValue("s", body.TryGetProperty("subscription_id", out var s) && s.ValueKind == JsonValueKind.Number ? s.GetInt32() : DBNull.Value);
            cmd.Parameters.AddWithValue("c", body.TryGetProperty("contract_id", out var c) && c.ValueKind == JsonValueKind.Number ? c.GetInt32() : DBNull.Value);
            cmd.Parameters.AddWithValue("o", body.TryGetProperty("order_id", out var o) && o.ValueKind == JsonValueKind.Number ? o.GetInt32() : DBNull.Value);
            cmd.Parameters.AddWithValue("p", body.TryGetProperty("product_id", out var p) && p.ValueKind == JsonValueKind.Number ? p.GetInt32() : DBNull.Value);
            cmd.Parameters.AddWithValue("po", body.GetProperty("performance_obligation").GetString() ?? "");
            cmd.Parameters.AddWithValue("rm", body.TryGetProperty("recognition_method", out var rm) ? rm.GetString() ?? "ratable" : "ratable");
            cmd.Parameters.AddWithValue("t", body.GetProperty("total_amount").GetDecimal());
            cmd.Parameters.AddWithValue("cur", body.TryGetProperty("currency", out var cur) ? cur.GetString() ?? "GBP" : "GBP");
            cmd.Parameters.AddWithValue("sd", DateTime.Parse(body.GetProperty("start_date").GetString() ?? "").Date);
            cmd.Parameters.AddWithValue("ed", body.TryGetProperty("end_date", out var ed) && ed.ValueKind == JsonValueKind.String ? DateTime.Parse(ed.GetString()!).Date : DBNull.Value);
            cmd.Parameters.AddWithValue("per", body.TryGetProperty("periods", out var per) && per.ValueKind == JsonValueKind.Number ? per.GetInt32() : 1);
            var id = (int)(await cmd.ExecuteScalarAsync())!;
            return Results.Created($"/api/crm/revenue/schedules/{id}", new { id });
        });

        group.MapPost("/schedules/{id:int}/generate-entries", async (int id, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand("SELECT generate_revenue_entries(@id)", conn);
            cmd.Parameters.AddWithValue("id", id);
            var count = (int)(await cmd.ExecuteScalarAsync())!;
            return Results.Ok(new { entries_generated = count });
        });

        group.MapGet("/entries", async (DbConnectionFactory db, int? schedule_id, DateTime? start, DateTime? end) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            var where = new List<string> { "is_reversed = false" };
            var parms = new List<(string, object)>();
            if (schedule_id.HasValue) { where.Add("schedule_id = @s");    parms.Add(("s", schedule_id.Value)); }
            if (start.HasValue)        { where.Add("period_start >= @st"); parms.Add(("st", start.Value.Date)); }
            if (end.HasValue)          { where.Add("period_end <= @en");   parms.Add(("en", end.Value.Date)); }
            var whereSql = "WHERE " + string.Join(" AND ", where);
            await using var cmd = new NpgsqlCommand(
                $"SELECT * FROM crm_revenue_entries {whereSql} ORDER BY period_start DESC LIMIT 1000", conn);
            foreach (var (n, v) in parms) cmd.Parameters.AddWithValue(n, v);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        return group;
    }
}

public static class OrderEndpoints
{
    public static RouteGroupBuilder MapOrderEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", async (DbConnectionFactory db, int? account_id, string? status) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            var where = new List<string>();
            var parms = new List<(string, object)>();
            if (account_id.HasValue)            { where.Add("o.account_id = @a"); parms.Add(("a", account_id.Value)); }
            if (!string.IsNullOrEmpty(status)) { where.Add("o.status = @s");     parms.Add(("s", status)); }
            var whereSql = where.Count > 0 ? "WHERE " + string.Join(" AND ", where) : "";
            await using var cmd = new NpgsqlCommand(
                $@"SELECT o.*, a.name AS account_name
                   FROM crm_orders o LEFT JOIN crm_accounts a ON a.id = o.account_id
                   {whereSql} ORDER BY o.order_date DESC LIMIT 500", conn);
            foreach (var (n, v) in parms) cmd.Parameters.AddWithValue(n, v);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        group.MapPost("/", async (JsonElement body, DbConnectionFactory db) =>
        {
            var num = body.TryGetProperty("order_number", out var on) ? on.GetString() ?? "" : $"ORD-{DateTime.UtcNow:yyyyMM}-{Guid.NewGuid().ToString()[..6].ToUpper()}";
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                @"INSERT INTO crm_orders (order_number, quote_id, deal_id, account_id, contact_id, contract_id,
                    status, currency, po_number, notes, owner_id)
                  VALUES (@n, @q, @d, @a, @c, @cn, @s, @cur, @po, @nt, @o) RETURNING id", conn);
            cmd.Parameters.AddWithValue("n", num);
            cmd.Parameters.AddWithValue("q", body.TryGetProperty("quote_id", out var q) && q.ValueKind == JsonValueKind.Number ? q.GetInt32() : DBNull.Value);
            cmd.Parameters.AddWithValue("d", body.TryGetProperty("deal_id", out var d) && d.ValueKind == JsonValueKind.Number ? d.GetInt32() : DBNull.Value);
            cmd.Parameters.AddWithValue("a", body.GetProperty("account_id").GetInt32());
            cmd.Parameters.AddWithValue("c", body.TryGetProperty("contact_id", out var ct) && ct.ValueKind == JsonValueKind.Number ? ct.GetInt32() : DBNull.Value);
            cmd.Parameters.AddWithValue("cn", body.TryGetProperty("contract_id", out var cn) && cn.ValueKind == JsonValueKind.Number ? cn.GetInt32() : DBNull.Value);
            cmd.Parameters.AddWithValue("s", body.TryGetProperty("status", out var s) ? s.GetString() ?? "draft" : "draft");
            cmd.Parameters.AddWithValue("cur", body.TryGetProperty("currency", out var cur) ? cur.GetString() ?? "GBP" : "GBP");
            cmd.Parameters.AddWithValue("po", body.TryGetProperty("po_number", out var po) ? po.GetString() ?? "" : "");
            cmd.Parameters.AddWithValue("nt", body.TryGetProperty("notes", out var nt) ? nt.GetString() ?? "" : "");
            cmd.Parameters.AddWithValue("o", body.TryGetProperty("owner_id", out var o) && o.ValueKind == JsonValueKind.Number ? o.GetInt32() : DBNull.Value);
            var id = (int)(await cmd.ExecuteScalarAsync())!;
            return Results.Created($"/api/crm/orders/{id}", new { id, order_number = num });
        });

        group.MapPost("/{id:int}/lines", async (int id, JsonElement body, DbConnectionFactory db) =>
        {
            var qty = body.GetProperty("quantity").GetDecimal();
            var unitPrice = body.GetProperty("unit_price").GetDecimal();
            var discountPct = body.TryGetProperty("discount_pct", out var dp) && dp.ValueKind == JsonValueKind.Number ? dp.GetDecimal() : 0m;
            var lineTotal = qty * unitPrice * (1 - discountPct / 100m);

            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                @"INSERT INTO crm_order_lines (order_id, product_id, bundle_id, sku, description,
                    quantity, unit_price, discount_pct, line_total, tax_pct, sort_order)
                  VALUES (@o, @p, @b, @sku, @desc, @q, @up, @dp, @lt, @tx, @so) RETURNING id", conn);
            cmd.Parameters.AddWithValue("o", id);
            cmd.Parameters.AddWithValue("p", body.TryGetProperty("product_id", out var pid) && pid.ValueKind == JsonValueKind.Number ? pid.GetInt32() : DBNull.Value);
            cmd.Parameters.AddWithValue("b", body.TryGetProperty("bundle_id", out var bid) && bid.ValueKind == JsonValueKind.Number ? bid.GetInt32() : DBNull.Value);
            cmd.Parameters.AddWithValue("sku", body.TryGetProperty("sku", out var sku) ? sku.GetString() ?? "" : "");
            cmd.Parameters.AddWithValue("desc", body.GetProperty("description").GetString() ?? "");
            cmd.Parameters.AddWithValue("q", qty);
            cmd.Parameters.AddWithValue("up", unitPrice);
            cmd.Parameters.AddWithValue("dp", discountPct);
            cmd.Parameters.AddWithValue("lt", lineTotal);
            cmd.Parameters.AddWithValue("tx", body.TryGetProperty("tax_pct", out var tx) && tx.ValueKind == JsonValueKind.Number ? tx.GetDecimal() : 0m);
            cmd.Parameters.AddWithValue("so", body.TryGetProperty("sort_order", out var so) && so.ValueKind == JsonValueKind.Number ? so.GetInt32() : 0);
            var lid = (int)(await cmd.ExecuteScalarAsync())!;
            return Results.Created($"/api/crm/orders/{id}/lines/{lid}", new { id = lid });
        });

        group.MapGet("/{id:int}/lines", async (int id, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                "SELECT * FROM crm_order_lines WHERE order_id = @id ORDER BY sort_order, id", conn);
            cmd.Parameters.AddWithValue("id", id);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        return group;
    }
}

// Shared CRUD helpers
internal static class CrmCrudHelpers
{
    public static async Task<IResult> InsertAsync(DbConnectionFactory db, string table, Dictionary<string, object?> body)
    {
        await using var conn = await db.OpenConnectionAsync();
        await using var cmd = conn.CreateCommand();
        var cols = new List<string>(); var p = new List<string>(); int i = 0;
        foreach (var kvp in body) { var pn = $"p{i++}"; cols.Add(kvp.Key); p.Add($"@{pn}"); cmd.Parameters.AddWithValue(pn, kvp.Value ?? DBNull.Value); }
        cmd.CommandText = $"INSERT INTO {table} ({string.Join(",", cols)}) VALUES ({string.Join(",", p)}) RETURNING id";
        var newId = await cmd.ExecuteScalarAsync();
        return Results.Created($"/{newId}", new { id = newId });
    }

    public static async Task<IResult> UpdateAsync(DbConnectionFactory db, string table, int id, Dictionary<string, object?> body)
    {
        if (body.Count == 0) return ApiProblem.ValidationError("No fields to update.");
        await using var conn = await db.OpenConnectionAsync();
        await using var cmd = conn.CreateCommand();
        var sets = new List<string>(); int i = 0;
        foreach (var kvp in body) { var pn = $"p{i++}"; sets.Add($"{kvp.Key} = @{pn}"); cmd.Parameters.AddWithValue(pn, kvp.Value ?? DBNull.Value); }
        cmd.CommandText = $"UPDATE {table} SET {string.Join(",", sets)}, updated_at = NOW() WHERE id = @id AND is_deleted IS NOT TRUE RETURNING id";
        cmd.Parameters.AddWithValue("id", id);
        var r = await cmd.ExecuteScalarAsync();
        return r is null ? ApiProblem.NotFound($"{table} {id} not found") : Results.Ok(new { id });
    }

    public static async Task<IResult> SoftDeleteAsync(DbConnectionFactory db, string table, int id)
    {
        await using var conn = await db.OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            $"UPDATE {table} SET is_deleted = true, deleted_at = NOW() WHERE id = @id AND is_deleted IS NOT TRUE RETURNING id", conn);
        cmd.Parameters.AddWithValue("id", id);
        var r = await cmd.ExecuteScalarAsync();
        return r is null ? ApiProblem.NotFound($"{table} {id} not found") : Results.NoContent();
    }
}
