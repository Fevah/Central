using System.Text.Json;
using Npgsql;
using Central.Persistence;

namespace Central.Api.Endpoints;

/// <summary>Phases 24-25: CRM Dashboards + Reports + Forecasting.</summary>
public static class CrmDashboardEndpoints
{
    public static RouteGroupBuilder MapCrmDashboardEndpoints(this RouteGroupBuilder group)
    {
        // ── Revenue dashboard (monthly rollup) ──
        group.MapGet("/revenue", async (DbConnectionFactory db, int? months) =>
        {
            var m = Math.Min(months ?? 12, 36);
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                @"SELECT month, currency, deals_won, deals_lost, deals_open,
                         revenue_won, revenue_lost, pipeline_weighted, avg_deal_size, avg_cycle_days
                  FROM crm_revenue_dashboard
                  WHERE month >= date_trunc('month', NOW()) - (@m || ' months')::interval
                  ORDER BY month DESC, currency", conn);
            cmd.Parameters.AddWithValue("m", m);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        // ── Activity dashboard (calls/emails/meetings per rep per week) ──
        group.MapGet("/activity", async (DbConnectionFactory db, int? weeks) =>
        {
            var w = Math.Min(weeks ?? 12, 52);
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                @"SELECT a.week, a.user_id, COALESCE(u.display_name,'') AS user_name,
                         a.activity_type, a.activity_count, a.total_minutes
                  FROM crm_activity_dashboard a
                  LEFT JOIN app_users u ON u.id = a.user_id
                  WHERE a.week >= date_trunc('week', NOW()) - (@w || ' weeks')::interval
                  ORDER BY a.week DESC, a.user_id", conn);
            cmd.Parameters.AddWithValue("w", w);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        // ── Lead source ROI ──
        group.MapGet("/leads", async (DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                @"SELECT source, total_leads, converted_leads, conversion_rate_pct,
                         revenue_generated, avg_days_to_convert
                  FROM crm_lead_source_roi
                  ORDER BY revenue_generated DESC, total_leads DESC", conn);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        // ── Account health ──
        group.MapGet("/accounts/health", async (DbConnectionFactory db, string? status) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            var where = string.IsNullOrEmpty(status) ? "" : "WHERE health_status = @status";
            await using var cmd = new NpgsqlCommand(
                $@"SELECT account_id, name, account_type, rating, health_status,
                          last_activity_at, open_deals, open_pipeline, lifetime_revenue
                   FROM crm_account_health {where}
                   ORDER BY lifetime_revenue DESC NULLS LAST LIMIT 500", conn);
            if (!string.IsNullOrEmpty(status)) cmd.Parameters.AddWithValue("status", status);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        // ── KPI summary (tiles on main dashboard) ──
        group.MapGet("/summary", async (DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(@"
                SELECT
                  (SELECT COUNT(*) FROM crm_accounts WHERE is_deleted IS NOT TRUE AND account_type='customer') AS customer_count,
                  (SELECT COUNT(*) FROM crm_accounts WHERE is_deleted IS NOT TRUE AND account_type='prospect') AS prospect_count,
                  (SELECT COUNT(*) FROM crm_deals WHERE is_deleted IS NOT TRUE AND actual_close IS NULL) AS open_deal_count,
                  (SELECT COALESCE(SUM(value),0) FROM crm_deals WHERE is_deleted IS NOT TRUE AND actual_close IS NULL) AS open_pipeline_value,
                  (SELECT COALESCE(SUM(value*probability/100.0),0) FROM crm_deals WHERE is_deleted IS NOT TRUE AND actual_close IS NULL) AS weighted_pipeline,
                  (SELECT COALESCE(SUM(value),0) FROM crm_deals WHERE stage='Closed Won' AND actual_close >= date_trunc('month',NOW())) AS revenue_this_month,
                  (SELECT COUNT(*) FROM crm_leads WHERE status='new' AND is_deleted IS NOT TRUE) AS new_leads,
                  (SELECT COUNT(*) FROM crm_activities WHERE due_at IS NOT NULL AND is_completed=false AND due_at < NOW()) AS overdue_activities", conn);
            await using var r = await cmd.ExecuteReaderAsync();
            if (!await r.ReadAsync()) return Results.Ok(new { });
            return Results.Ok(new
            {
                customer_count = r.GetInt64(0),
                prospect_count = r.GetInt64(1),
                open_deal_count = r.GetInt64(2),
                open_pipeline_value = r.GetDecimal(3),
                weighted_pipeline = r.GetDecimal(4),
                revenue_this_month = r.GetDecimal(5),
                new_leads = r.GetInt64(6),
                overdue_activities = r.GetInt64(7)
            });
        });

        // ── Refresh dashboards (admin trigger) ──
        group.MapPost("/refresh", async (DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand("SELECT refresh_crm_dashboards()", conn);
            await cmd.ExecuteNonQueryAsync();
            return Results.Ok(new { refreshed = true, at = DateTime.UtcNow });
        });

        return group;
    }
}

/// <summary>Phase 25: Saved reports + forecasting.</summary>
public static class CrmReportEndpoints
{
    public static RouteGroupBuilder MapCrmReportEndpoints(this RouteGroupBuilder group)
    {
        // ── Saved reports CRUD ──
        group.MapGet("/", async (DbConnectionFactory db, string? type) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            var where = string.IsNullOrEmpty(type) ? "" : "WHERE report_type = @t";
            await using var cmd = new NpgsqlCommand(
                $"SELECT * FROM crm_saved_reports {where} ORDER BY name", conn);
            if (!string.IsNullOrEmpty(type)) cmd.Parameters.AddWithValue("t", type);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        group.MapPost("/", async (JsonElement body, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                @"INSERT INTO crm_saved_reports (name, description, report_type, filters, columns, group_by, sort_by, schedule_cron, email_to, export_format, is_public)
                  VALUES (@n, @d, @t, @f::jsonb, @c, @g, @s, @cron, @em, @ex, @pub) RETURNING id", conn);
            cmd.Parameters.AddWithValue("n", body.GetProperty("name").GetString() ?? "");
            cmd.Parameters.AddWithValue("d", (object?)body.TryGetValue("description") ?? DBNull.Value);
            cmd.Parameters.AddWithValue("t", body.GetProperty("report_type").GetString() ?? "pipeline");
            cmd.Parameters.AddWithValue("f", body.TryGetProperty("filters", out var f) ? f.GetRawText() : "{}");
            cmd.Parameters.AddWithValue("c", ReadStringArr(body, "columns"));
            cmd.Parameters.AddWithValue("g", (object?)body.TryGetValue("group_by") ?? DBNull.Value);
            cmd.Parameters.AddWithValue("s", (object?)body.TryGetValue("sort_by") ?? DBNull.Value);
            cmd.Parameters.AddWithValue("cron", (object?)body.TryGetValue("schedule_cron") ?? DBNull.Value);
            cmd.Parameters.AddWithValue("em", ReadStringArr(body, "email_to"));
            cmd.Parameters.AddWithValue("ex", body.TryGetValue("export_format") ?? "pdf");
            cmd.Parameters.AddWithValue("pub", body.TryGetBool("is_public") ?? false);
            var id = (int)(await cmd.ExecuteScalarAsync())!;
            return Results.Created($"/api/crm/reports/{id}", new { id });
        });

        // ── Forecasting ──
        group.MapGet("/forecasts", async (DbConnectionFactory db, int? owner_id) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            var where = owner_id.HasValue ? "WHERE owner_id = @o" : "";
            await using var cmd = new NpgsqlCommand(
                $@"SELECT * FROM crm_forecast_snapshots {where}
                   ORDER BY snapshot_date DESC, period_start DESC LIMIT 100", conn);
            if (owner_id.HasValue) cmd.Parameters.AddWithValue("o", owner_id.Value);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        group.MapPost("/forecasts/generate", async (JsonElement body, DbConnectionFactory db) =>
        {
            var start = DateTime.Parse(body.GetProperty("period_start").GetString() ?? "");
            var end = DateTime.Parse(body.GetProperty("period_end").GetString() ?? "");
            var ownerId = body.TryGetInt("owner_id");

            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                "SELECT generate_crm_forecast(@s, @e, @o)", conn);
            cmd.Parameters.AddWithValue("s", start.Date);
            cmd.Parameters.AddWithValue("e", end.Date);
            cmd.Parameters.AddWithValue("o", ownerId.HasValue ? ownerId.Value : DBNull.Value);
            var id = (int)(await cmd.ExecuteScalarAsync())!;
            return Results.Created($"/api/crm/reports/forecasts/{id}", new { id });
        });

        // Built-in pipeline forecast report (live query)
        group.MapGet("/forecasts/live", async (DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(@"
                SELECT
                    date_trunc('month', expected_close) AS month,
                    COUNT(*) FILTER (WHERE probability >= 90) AS committed_deals,
                    COALESCE(SUM(value) FILTER (WHERE probability >= 90), 0) AS committed_value,
                    COUNT(*) FILTER (WHERE probability >= 50 AND probability < 90) AS best_case_deals,
                    COALESCE(SUM(value) FILTER (WHERE probability >= 50 AND probability < 90), 0) AS best_case_value,
                    COALESCE(SUM(value * probability / 100.0), 0) AS weighted_value
                FROM crm_deals
                WHERE is_deleted IS NOT TRUE AND actual_close IS NULL
                  AND expected_close >= CURRENT_DATE
                  AND expected_close < CURRENT_DATE + INTERVAL '6 months'
                GROUP BY date_trunc('month', expected_close)
                ORDER BY month", conn);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        return group;
    }

    private static string[] ReadStringArr(JsonElement body, string key)
    {
        if (!body.TryGetProperty(key, out var arr) || arr.ValueKind != JsonValueKind.Array) return [];
        return arr.EnumerateArray().Select(e => e.GetString() ?? "").Where(s => !string.IsNullOrEmpty(s)).ToArray();
    }
}

/// <summary>Phase 29: Outbound webhook subscriptions + document management endpoints.</summary>
public static class WebhookSubscriptionEndpoints
{
    public static RouteGroupBuilder MapWebhookSubscriptionEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/event-types", async (DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                "SELECT event_type, category, description FROM webhook_event_types ORDER BY category, event_type", conn);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        group.MapGet("/subscriptions", async (DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                @"SELECT id, name, target_url, event_types, is_active, retry_count,
                         last_delivery_at, last_delivery_status, failure_count, created_at
                  FROM webhook_subscriptions ORDER BY name", conn);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        group.MapPost("/subscriptions", async (JsonElement body, DbConnectionFactory db) =>
        {
            var events = body.GetProperty("event_types").EnumerateArray()
                .Select(e => e.GetString() ?? "").Where(s => !string.IsNullOrEmpty(s)).ToArray();

            // Generate signing secret, return it once (caller stores it)
            var secret = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
            var secretHash = Convert.ToBase64String(
                System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(secret)));

            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                @"INSERT INTO webhook_subscriptions (name, target_url, secret_hash, event_types, retry_count, timeout_seconds)
                  VALUES (@n, @u, @s, @e, @r, @t) RETURNING id", conn);
            cmd.Parameters.AddWithValue("n", body.GetProperty("name").GetString() ?? "");
            cmd.Parameters.AddWithValue("u", body.GetProperty("target_url").GetString() ?? "");
            cmd.Parameters.AddWithValue("s", secretHash);
            cmd.Parameters.AddWithValue("e", events);
            cmd.Parameters.AddWithValue("r", body.TryGetInt("retry_count") ?? 3);
            cmd.Parameters.AddWithValue("t", body.TryGetInt("timeout_seconds") ?? 30);
            var id = (int)(await cmd.ExecuteScalarAsync())!;
            return Results.Created($"/api/webhooks/subscriptions/{id}",
                new { id, secret, warning = "Store this secret — it cannot be retrieved again." });
        });

        group.MapDelete("/subscriptions/{id:int}", async (int id, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                "UPDATE webhook_subscriptions SET is_active = false WHERE id = @id RETURNING id", conn);
            cmd.Parameters.AddWithValue("id", id);
            var r = await cmd.ExecuteScalarAsync();
            return r is null ? ApiProblem.NotFound("Subscription not found") : Results.NoContent();
        });

        group.MapGet("/deliveries", async (DbConnectionFactory db, int? subscription_id, string? status) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            var where = new List<string>();
            var parms = new List<(string, object)>();
            if (subscription_id.HasValue) { where.Add("subscription_id = @sid"); parms.Add(("sid", subscription_id.Value)); }
            if (!string.IsNullOrEmpty(status))        { where.Add("status = @st");        parms.Add(("st", status)); }
            var whereSql = where.Count > 0 ? "WHERE " + string.Join(" AND ", where) : "";
            await using var cmd = new NpgsqlCommand(
                $@"SELECT id, subscription_id, event_type, status, http_status, attempt_count, delivered_at, error_message, created_at
                   FROM webhook_deliveries {whereSql}
                   ORDER BY created_at DESC LIMIT 200", conn);
            foreach (var (n, v) in parms) cmd.Parameters.AddWithValue(n, v);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        return group;
    }
}

/// <summary>Phase 28: Document management endpoints.</summary>
public static class CrmDocumentEndpoints
{
    public static RouteGroupBuilder MapCrmDocumentEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", async (DbConnectionFactory db, string? entity_type, int? entity_id, string? document_type) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            var where = new List<string>();
            var parms = new List<(string, object)>();
            if (!string.IsNullOrEmpty(entity_type))   { where.Add("entity_type = @et");   parms.Add(("et", entity_type)); }
            if (entity_id.HasValue)                    { where.Add("entity_id = @eid");   parms.Add(("eid", entity_id.Value)); }
            if (!string.IsNullOrEmpty(document_type)) { where.Add("document_type = @dt"); parms.Add(("dt", document_type)); }
            var whereSql = where.Count > 0 ? "WHERE " + string.Join(" AND ", where) : "";
            await using var cmd = new NpgsqlCommand(
                $@"SELECT * FROM crm_documents {whereSql} ORDER BY created_at DESC LIMIT 500", conn);
            foreach (var (n, v) in parms) cmd.Parameters.AddWithValue(n, v);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        group.MapPost("/", async (JsonElement body, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                @"INSERT INTO crm_documents (entity_type, entity_id, file_id, document_type, name, status)
                  VALUES (@et, @eid, @fid, @dt, @n, @s) RETURNING id", conn);
            cmd.Parameters.AddWithValue("et", body.GetProperty("entity_type").GetString() ?? "");
            cmd.Parameters.AddWithValue("eid", body.GetProperty("entity_id").GetInt32());
            cmd.Parameters.AddWithValue("fid", body.TryGetInt("file_id") ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("dt", body.GetProperty("document_type").GetString() ?? "other");
            cmd.Parameters.AddWithValue("n", body.GetProperty("name").GetString() ?? "");
            cmd.Parameters.AddWithValue("s", body.TryGetValue("status") ?? "draft");
            var id = (int)(await cmd.ExecuteScalarAsync())!;
            return Results.Created($"/api/crm/documents/{id}", new { id });
        });

        group.MapPost("/{id:int}/sign", async (int id, JsonElement body, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                @"UPDATE crm_documents SET status = 'signed', signed_at = NOW(),
                  signed_by_name = @by, signature_provider = @prov, signature_envelope_id = @env
                  WHERE id = @id RETURNING id", conn);
            cmd.Parameters.AddWithValue("id", id);
            cmd.Parameters.AddWithValue("by", body.TryGetValue("signed_by_name") ?? "");
            cmd.Parameters.AddWithValue("prov", body.TryGetValue("signature_provider") ?? "manual");
            cmd.Parameters.AddWithValue("env", (object?)body.TryGetValue("signature_envelope_id") ?? DBNull.Value);
            var r = await cmd.ExecuteScalarAsync();
            return r is null ? ApiProblem.NotFound("Document not found") : Results.Ok(new { id, signed = true });
        });

        // Templates
        group.MapGet("/templates", async (DbConnectionFactory db, string? type) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            var where = string.IsNullOrEmpty(type) ? "is_active = true" : "is_active = true AND template_type = @t";
            await using var cmd = new NpgsqlCommand(
                $"SELECT * FROM crm_document_templates WHERE {where} ORDER BY template_type, name", conn);
            if (!string.IsNullOrEmpty(type)) cmd.Parameters.AddWithValue("t", type);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        return group;
    }
}
