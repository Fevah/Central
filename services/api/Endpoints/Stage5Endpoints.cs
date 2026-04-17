using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Npgsql;
using Central.Persistence;

namespace Central.Api.Endpoints;

// ══════════════════════════════════════════════════════════════════════
// Stage 5: Portal auth (magic link) + Partner Portal + Community + KB
// ══════════════════════════════════════════════════════════════════════

public static class PortalEndpoints
{
    public static RouteGroupBuilder MapPortalEndpoints(this RouteGroupBuilder group)
    {
        // Portal users CRUD (admin-only — portal users are managed by tenant admins)
        group.MapGet("/users", async (DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(@"
                SELECT pu.*, COALESCE(a.name,'') AS account_name
                FROM portal_users pu
                LEFT JOIN crm_accounts a ON a.id = pu.account_id
                WHERE pu.is_active = true
                ORDER BY pu.email LIMIT 500", conn);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        group.MapPost("/users", async (JsonElement body, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                @"INSERT INTO portal_users (email, display_name, portal_type, contact_id, account_id, company_id)
                  VALUES (@e, @n, @pt, @c, @a, @co) RETURNING id", conn);
            cmd.Parameters.AddWithValue("e", body.GetProperty("email").GetString() ?? "");
            cmd.Parameters.AddWithValue("n", body.TryGetProperty("display_name", out var n) ? n.GetString() ?? "" : "");
            cmd.Parameters.AddWithValue("pt", body.TryGetProperty("portal_type", out var pt) ? pt.GetString() ?? "customer" : "customer");
            cmd.Parameters.AddWithValue("c", body.TryGetProperty("contact_id", out var c) && c.ValueKind == JsonValueKind.Number ? c.GetInt32() : DBNull.Value);
            cmd.Parameters.AddWithValue("a", body.TryGetProperty("account_id", out var a) && a.ValueKind == JsonValueKind.Number ? a.GetInt32() : DBNull.Value);
            cmd.Parameters.AddWithValue("co", body.TryGetProperty("company_id", out var co) && co.ValueKind == JsonValueKind.Number ? co.GetInt32() : DBNull.Value);
            var id = (int)(await cmd.ExecuteScalarAsync())!;
            return Results.Created($"/api/portal/users/{id}", new { id });
        });

        // ── Magic link request (anonymous) ──
        group.MapPost("/auth/request-link", async (JsonElement body, DbConnectionFactory db) =>
        {
            var email = body.GetProperty("email").GetString() ?? "";
            await using var conn = await db.OpenConnectionAsync();
            await using var findCmd = new NpgsqlCommand(
                "SELECT id FROM portal_users WHERE LOWER(email) = LOWER(@e) AND is_active = true", conn);
            findCmd.Parameters.AddWithValue("e", email);
            var uid = await findCmd.ExecuteScalarAsync();

            // Always return success (prevent enumeration)
            if (uid is not int userId)
                return Results.Ok(new { sent = true });

            var token = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
            var hash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
            await using var ins = new NpgsqlCommand(
                "INSERT INTO portal_magic_links (user_id, token_hash) VALUES (@u, @h)", conn);
            ins.Parameters.AddWithValue("u", userId);
            ins.Parameters.AddWithValue("h", hash);
            await ins.ExecuteNonQueryAsync();

            // TODO: send email with link containing raw token
            return Results.Ok(new { sent = true, dev_token = token }); // dev_token returned only in dev
        }).AllowAnonymous();

        // ── Magic link verify + issue session (anonymous) ──
        group.MapPost("/auth/verify-link", async (JsonElement body, HttpContext ctx, DbConnectionFactory db) =>
        {
            var token = body.GetProperty("token").GetString() ?? "";
            var hash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

            await using var conn = await db.OpenConnectionAsync();
            await using var findCmd = new NpgsqlCommand(
                @"UPDATE portal_magic_links SET used_at = NOW()
                  WHERE token_hash = @h AND used_at IS NULL AND expires_at > NOW()
                  RETURNING user_id", conn);
            findCmd.Parameters.AddWithValue("h", hash);
            var uid = await findCmd.ExecuteScalarAsync();
            if (uid is not int userId)
                return ApiProblem.NotFound("Link invalid or expired");

            var sessionToken = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
            await using var sessCmd = new NpgsqlCommand(
                @"INSERT INTO portal_sessions (user_id, session_token, ip_address, user_agent, expires_at)
                  VALUES (@u, @t, @ip::inet, @ua, NOW() + INTERVAL '24 hours') RETURNING id", conn);
            sessCmd.Parameters.AddWithValue("u", userId);
            sessCmd.Parameters.AddWithValue("t", sessionToken);
            sessCmd.Parameters.AddWithValue("ip", (object)(ctx.Connection.RemoteIpAddress?.ToString() ?? "0.0.0.0"));
            sessCmd.Parameters.AddWithValue("ua", (object)(ctx.Request.Headers.UserAgent.ToString() ?? ""));
            await sessCmd.ExecuteNonQueryAsync();

            // Update login stats
            await using var statCmd = new NpgsqlCommand(
                "UPDATE portal_users SET last_login_at = NOW(), login_count = login_count + 1 WHERE id = @u", conn);
            statCmd.Parameters.AddWithValue("u", userId);
            await statCmd.ExecuteNonQueryAsync();

            return Results.Ok(new { session_token = sessionToken, expires_in_hours = 24 });
        }).AllowAnonymous();

        // ── Partner portal: deal registration ──
        group.MapGet("/deal-registrations", async (DbConnectionFactory db, string? status) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            var where = string.IsNullOrEmpty(status) ? "" : "WHERE r.status = @s";
            await using var cmd = new NpgsqlCommand(
                $@"SELECT r.*, p.email AS partner_email
                   FROM partner_deal_registrations r
                   JOIN portal_users p ON p.id = r.partner_user_id
                   {where} ORDER BY r.submitted_at DESC LIMIT 500", conn);
            if (!string.IsNullOrEmpty(status)) cmd.Parameters.AddWithValue("s", status);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        group.MapPost("/deal-registrations", async (JsonElement body, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                @"INSERT INTO partner_deal_registrations
                    (partner_user_id, customer_company_name, customer_contact_name, customer_contact_email,
                     estimated_value, currency, products_of_interest, notes)
                  VALUES (@p, @cc, @cn, @ce, @v, @cur, @pr, @n) RETURNING id", conn);
            cmd.Parameters.AddWithValue("p", body.GetProperty("partner_user_id").GetInt32());
            cmd.Parameters.AddWithValue("cc", body.GetProperty("customer_company_name").GetString() ?? "");
            cmd.Parameters.AddWithValue("cn", body.TryGetProperty("customer_contact_name", out var cn) ? cn.GetString() ?? "" : "");
            cmd.Parameters.AddWithValue("ce", body.TryGetProperty("customer_contact_email", out var ce) ? ce.GetString() ?? "" : "");
            cmd.Parameters.AddWithValue("v", body.TryGetProperty("estimated_value", out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDecimal() : DBNull.Value);
            cmd.Parameters.AddWithValue("cur", body.TryGetProperty("currency", out var cur) ? cur.GetString() ?? "GBP" : "GBP");
            cmd.Parameters.AddWithValue("pr", body.TryGetProperty("products_of_interest", out var pi) && pi.ValueKind == JsonValueKind.Array
                ? pi.EnumerateArray().Select(e => e.GetString() ?? "").ToArray() : Array.Empty<string>());
            cmd.Parameters.AddWithValue("n", body.TryGetProperty("notes", out var nt) ? nt.GetString() ?? "" : "");
            var id = (int)(await cmd.ExecuteScalarAsync())!;
            return Results.Created($"/api/portal/deal-registrations/{id}", new { id });
        });

        group.MapPost("/deal-registrations/{id:int}/approve", async (int id, ClaimsPrincipal principal, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                @"UPDATE partner_deal_registrations SET status = 'approved', reviewed_at = NOW(),
                  reviewed_by = (SELECT id FROM app_users WHERE username = @u) WHERE id = @id RETURNING id", conn);
            cmd.Parameters.AddWithValue("u", principal.Identity?.Name ?? "");
            cmd.Parameters.AddWithValue("id", id);
            var r = await cmd.ExecuteScalarAsync();
            return r is null ? ApiProblem.NotFound($"Registration {id} not found") : Results.Ok(new { id, approved = true });
        });

        return group;
    }
}

public static class KbCommunityEndpoints
{
    public static RouteGroupBuilder MapKbCommunityEndpoints(this RouteGroupBuilder group)
    {
        // KB Articles
        group.MapGet("/kb/articles", async (DbConnectionFactory db, string? q, string? category) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            var conditions = new List<string> { "a.status = 'published'" };
            var parms = new List<(string, object)>();
            if (!string.IsNullOrEmpty(q))        { conditions.Add("a.search_vector @@ plainto_tsquery('english', @q)"); parms.Add(("q", q)); }
            if (!string.IsNullOrEmpty(category)) { conditions.Add("c.slug = @cat"); parms.Add(("cat", category)); }
            var where = string.Join(" AND ", conditions);

            await using var cmd = new NpgsqlCommand(
                $@"SELECT a.id, a.slug, a.title, a.category_id, c.name AS category_name,
                          a.visibility, a.view_count, a.helpful_count, a.not_helpful_count,
                          a.tags, a.published_at
                   FROM kb_articles a LEFT JOIN kb_categories c ON c.id = a.category_id
                   WHERE {where} ORDER BY a.published_at DESC LIMIT 200", conn);
            foreach (var (n, v) in parms) cmd.Parameters.AddWithValue(n, v);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        }).AllowAnonymous();

        group.MapGet("/kb/articles/{slug}", async (string slug, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                "SELECT * FROM kb_articles WHERE slug = @s AND status = 'published'", conn);
            cmd.Parameters.AddWithValue("s", slug);
            await using var r = await cmd.ExecuteReaderAsync();
            if (!await r.ReadAsync()) return Results.NotFound();
            var row = new Dictionary<string, object?>();
            for (int i = 0; i < r.FieldCount; i++) row[r.GetName(i)] = r.IsDBNull(i) ? null : r.GetValue(i);
            await r.CloseAsync();
            await using var inc = new NpgsqlCommand("UPDATE kb_articles SET view_count = view_count + 1 WHERE slug = @s", conn);
            inc.Parameters.AddWithValue("s", slug);
            await inc.ExecuteNonQueryAsync();
            return Results.Ok(row);
        }).AllowAnonymous();

        group.MapPost("/kb/articles", async (JsonElement body, ClaimsPrincipal principal, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                @"INSERT INTO kb_articles (slug, category_id, title, content_html, content_markdown, status, visibility, tags, author_id, published_at)
                  SELECT @s, @c, @t, @h, @md, @st, @v, @tg,
                         (SELECT id FROM app_users WHERE username = @u),
                         CASE WHEN @st = 'published' THEN NOW() END
                  RETURNING id", conn);
            cmd.Parameters.AddWithValue("u", principal.Identity?.Name ?? "");
            cmd.Parameters.AddWithValue("s", body.GetProperty("slug").GetString() ?? "");
            cmd.Parameters.AddWithValue("c", body.TryGetProperty("category_id", out var c) && c.ValueKind == JsonValueKind.Number ? c.GetInt32() : DBNull.Value);
            cmd.Parameters.AddWithValue("t", body.GetProperty("title").GetString() ?? "");
            cmd.Parameters.AddWithValue("h", body.TryGetProperty("content_html", out var h) ? h.GetString() ?? "" : "");
            cmd.Parameters.AddWithValue("md", body.TryGetProperty("content_markdown", out var md) ? md.GetString() ?? "" : "");
            cmd.Parameters.AddWithValue("st", body.TryGetProperty("status", out var st) ? st.GetString() ?? "draft" : "draft");
            cmd.Parameters.AddWithValue("v", body.TryGetProperty("visibility", out var v) ? v.GetString() ?? "public" : "public");
            cmd.Parameters.AddWithValue("tg", body.TryGetProperty("tags", out var tg) && tg.ValueKind == JsonValueKind.Array
                ? tg.EnumerateArray().Select(e => e.GetString() ?? "").ToArray() : Array.Empty<string>());
            var id = (int)(await cmd.ExecuteScalarAsync())!;
            return Results.Created($"/api/portal/kb/articles/{id}", new { id });
        });

        // Community threads
        group.MapGet("/community/threads", async (DbConnectionFactory db, string? category) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            var where = string.IsNullOrEmpty(category) ? "" : "WHERE category = @cat";
            await using var cmd = new NpgsqlCommand(
                $@"SELECT t.*, COALESCE(u.display_name, pu.display_name, '(anon)') AS author_name
                   FROM community_threads t
                   LEFT JOIN app_users u ON u.id = t.author_user_id
                   LEFT JOIN portal_users pu ON pu.id = t.author_portal_user_id
                   {where}
                   ORDER BY t.is_pinned DESC, COALESCE(t.last_reply_at, t.created_at) DESC LIMIT 200", conn);
            if (!string.IsNullOrEmpty(category)) cmd.Parameters.AddWithValue("cat", category);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        }).AllowAnonymous();

        group.MapPost("/community/threads", async (ClaimsPrincipal principal, JsonElement body, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                @"INSERT INTO community_threads (category, title, body_markdown, author_user_id, tags)
                  SELECT @c, @t, @b, (SELECT id FROM app_users WHERE username = @u), @tg
                  RETURNING id", conn);
            cmd.Parameters.AddWithValue("u", principal.Identity?.Name ?? "");
            cmd.Parameters.AddWithValue("c", body.TryGetProperty("category", out var c) ? c.GetString() ?? "general" : "general");
            cmd.Parameters.AddWithValue("t", body.GetProperty("title").GetString() ?? "");
            cmd.Parameters.AddWithValue("b", body.GetProperty("body_markdown").GetString() ?? "");
            cmd.Parameters.AddWithValue("tg", body.TryGetProperty("tags", out var tg) && tg.ValueKind == JsonValueKind.Array
                ? tg.EnumerateArray().Select(e => e.GetString() ?? "").ToArray() : Array.Empty<string>());
            var id = (int)(await cmd.ExecuteScalarAsync())!;
            return Results.Created($"/api/portal/community/threads/{id}", new { id });
        });

        group.MapGet("/community/threads/{id:int}/posts", async (int id, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                @"SELECT p.*, COALESCE(u.display_name, pu.display_name, '(anon)') AS author_name
                  FROM community_posts p
                  LEFT JOIN app_users u ON u.id = p.author_user_id
                  LEFT JOIN portal_users pu ON pu.id = p.author_portal_user_id
                  WHERE p.thread_id = @id
                  ORDER BY p.created_at", conn);
            cmd.Parameters.AddWithValue("id", id);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        }).AllowAnonymous();

        group.MapPost("/community/threads/{id:int}/posts", async (int id, ClaimsPrincipal principal, JsonElement body, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                @"INSERT INTO community_posts (thread_id, parent_post_id, body_markdown, author_user_id)
                  SELECT @t, @p, @b, (SELECT id FROM app_users WHERE username = @u)
                  RETURNING id", conn);
            cmd.Parameters.AddWithValue("u", principal.Identity?.Name ?? "");
            cmd.Parameters.AddWithValue("t", id);
            cmd.Parameters.AddWithValue("p", body.TryGetProperty("parent_post_id", out var pp) && pp.ValueKind == JsonValueKind.Number ? pp.GetInt32() : DBNull.Value);
            cmd.Parameters.AddWithValue("b", body.GetProperty("body_markdown").GetString() ?? "");
            var pid = (int)(await cmd.ExecuteScalarAsync())!;
            return Results.Created($"/api/portal/community/posts/{pid}", new { id = pid });
        });

        return group;
    }
}

// ══════════════════════════════════════════════════════════════════════
// Stage 5: Rule engines (validation + workflow — integrates with Elsa)
// ══════════════════════════════════════════════════════════════════════

public static class RuleEngineEndpoints
{
    public static RouteGroupBuilder MapRuleEngineEndpoints(this RouteGroupBuilder group)
    {
        // Validation rules
        group.MapGet("/validation", async (DbConnectionFactory db, string? entity_type) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            var where = string.IsNullOrEmpty(entity_type) ? "WHERE is_active = true"
                : "WHERE is_active = true AND entity_type = @et";
            await using var cmd = new NpgsqlCommand(
                $"SELECT * FROM validation_rules {where} ORDER BY entity_type, priority", conn);
            if (!string.IsNullOrEmpty(entity_type)) cmd.Parameters.AddWithValue("et", entity_type);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        group.MapPost("/validation", async (JsonElement body, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                @"INSERT INTO validation_rules
                    (entity_type, name, description, rule_expr, error_message, error_field, severity, applies_on, priority)
                  VALUES (@et, @n, @d, @r::jsonb, @em, @ef, @s, @ao, @p) RETURNING id", conn);
            cmd.Parameters.AddWithValue("et", body.GetProperty("entity_type").GetString() ?? "");
            cmd.Parameters.AddWithValue("n", body.GetProperty("name").GetString() ?? "");
            cmd.Parameters.AddWithValue("d", body.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "");
            cmd.Parameters.AddWithValue("r", body.GetProperty("rule_expr").GetRawText());
            cmd.Parameters.AddWithValue("em", body.GetProperty("error_message").GetString() ?? "");
            cmd.Parameters.AddWithValue("ef", body.TryGetProperty("error_field", out var ef) ? ef.GetString() ?? "" : "");
            cmd.Parameters.AddWithValue("s", body.TryGetProperty("severity", out var sv) ? sv.GetString() ?? "error" : "error");
            cmd.Parameters.AddWithValue("ao", body.TryGetProperty("applies_on", out var ao) && ao.ValueKind == JsonValueKind.Array
                ? ao.EnumerateArray().Select(e => e.GetString() ?? "").ToArray() : new[] { "insert", "update" });
            cmd.Parameters.AddWithValue("p", body.TryGetProperty("priority", out var p) && p.ValueKind == JsonValueKind.Number ? p.GetInt32() : 100);
            var id = (int)(await cmd.ExecuteScalarAsync())!;
            return Results.Created($"/api/rules/validation/{id}", new { id });
        });

        // Workflow rules (Elsa integration)
        group.MapGet("/workflow", async (DbConnectionFactory db, string? entity_type) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            var where = string.IsNullOrEmpty(entity_type) ? "WHERE is_active = true"
                : "WHERE is_active = true AND entity_type = @et";
            await using var cmd = new NpgsqlCommand(
                $"SELECT * FROM workflow_rules {where} ORDER BY entity_type, execution_order", conn);
            if (!string.IsNullOrEmpty(entity_type)) cmd.Parameters.AddWithValue("et", entity_type);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        group.MapPost("/workflow", async (JsonElement body, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                @"INSERT INTO workflow_rules
                    (entity_type, name, description, trigger_type, trigger_fields, condition_expr,
                     action_type, elsa_workflow_definition_id, inline_action, execution_order)
                  VALUES (@et, @n, @d, @tt, @tf, @ce::jsonb, @at, @wf, @ia::jsonb, @eo) RETURNING id", conn);
            cmd.Parameters.AddWithValue("et", body.GetProperty("entity_type").GetString() ?? "");
            cmd.Parameters.AddWithValue("n", body.GetProperty("name").GetString() ?? "");
            cmd.Parameters.AddWithValue("d", body.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "");
            cmd.Parameters.AddWithValue("tt", body.GetProperty("trigger_type").GetString() ?? "on_update");
            cmd.Parameters.AddWithValue("tf", body.TryGetProperty("trigger_fields", out var tf) && tf.ValueKind == JsonValueKind.Array
                ? tf.EnumerateArray().Select(e => e.GetString() ?? "").ToArray() : Array.Empty<string>());
            cmd.Parameters.AddWithValue("ce", body.TryGetProperty("condition_expr", out var ce) ? ce.GetRawText() : "{}");
            cmd.Parameters.AddWithValue("at", body.TryGetProperty("action_type", out var at) ? at.GetString() ?? "elsa_workflow" : "elsa_workflow");
            cmd.Parameters.AddWithValue("wf", body.TryGetProperty("elsa_workflow_definition_id", out var wf) ? wf.GetString() ?? "" : "");
            cmd.Parameters.AddWithValue("ia", body.TryGetProperty("inline_action", out var ia) ? ia.GetRawText() : "{}");
            cmd.Parameters.AddWithValue("eo", body.TryGetProperty("execution_order", out var eo) && eo.ValueKind == JsonValueKind.Number ? eo.GetInt32() : 100);
            var id = (int)(await cmd.ExecuteScalarAsync())!;
            return Results.Created($"/api/rules/workflow/{id}", new { id });
        });

        // Execution log
        group.MapGet("/execution-log", async (DbConnectionFactory db, string? rule_type, int? rule_id, int? entity_id) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            var where = new List<string>();
            var parms = new List<(string, object)>();
            if (!string.IsNullOrEmpty(rule_type)) { where.Add("rule_type = @rt"); parms.Add(("rt", rule_type)); }
            if (rule_id.HasValue)                  { where.Add("rule_id = @ri"); parms.Add(("ri", rule_id.Value)); }
            if (entity_id.HasValue)                { where.Add("entity_id = @ei"); parms.Add(("ei", entity_id.Value)); }
            var whereSql = where.Count > 0 ? "WHERE " + string.Join(" AND ", where) : "";
            await using var cmd = new NpgsqlCommand(
                $"SELECT * FROM rule_execution_log {whereSql} ORDER BY triggered_at DESC LIMIT 200", conn);
            foreach (var (n, v) in parms) cmd.Parameters.AddWithValue(n, v);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        return group;
    }
}

// ══════════════════════════════════════════════════════════════════════
// Stage 5: Custom Objects + Field Permissions
// ══════════════════════════════════════════════════════════════════════

public static class CustomObjectEndpoints
{
    public static RouteGroupBuilder MapCustomObjectEndpoints(this RouteGroupBuilder group)
    {
        // Custom entities
        group.MapGet("/entities", async (DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                "SELECT * FROM custom_entities WHERE is_active = true ORDER BY label", conn);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        group.MapPost("/entities", async (JsonElement body, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                @"INSERT INTO custom_entities (api_name, label, plural_label, description, icon, color, record_name_format)
                  VALUES (@a, @l, @pl, @d, @i, @c, @rnf) RETURNING id", conn);
            cmd.Parameters.AddWithValue("a", body.GetProperty("api_name").GetString() ?? "");
            cmd.Parameters.AddWithValue("l", body.GetProperty("label").GetString() ?? "");
            cmd.Parameters.AddWithValue("pl", body.GetProperty("plural_label").GetString() ?? "");
            cmd.Parameters.AddWithValue("d", body.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "");
            cmd.Parameters.AddWithValue("i", body.TryGetProperty("icon", out var ic) ? ic.GetString() ?? "" : "");
            cmd.Parameters.AddWithValue("c", body.TryGetProperty("color", out var col) ? col.GetString() ?? "#5B8AF5" : "#5B8AF5");
            cmd.Parameters.AddWithValue("rnf", body.TryGetProperty("record_name_format", out var rnf) ? rnf.GetString() ?? "{{name}}" : "{{name}}");
            var id = (int)(await cmd.ExecuteScalarAsync())!;
            return Results.Created($"/api/custom-objects/entities/{id}", new { id });
        });

        // Custom fields
        group.MapGet("/fields", async (DbConnectionFactory db, string? entity_type) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            var where = string.IsNullOrEmpty(entity_type) ? "WHERE is_active = true"
                : "WHERE is_active = true AND entity_type = @et";
            await using var cmd = new NpgsqlCommand(
                $"SELECT * FROM custom_fields {where} ORDER BY entity_type, sort_order", conn);
            if (!string.IsNullOrEmpty(entity_type)) cmd.Parameters.AddWithValue("et", entity_type);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        group.MapPost("/fields", async (JsonElement body, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                @"INSERT INTO custom_fields
                    (entity_type, api_name, label, field_type, is_required, is_unique, is_external_id,
                     default_value, help_text, max_length, precision_value, picklist_values, lookup_entity, sort_order)
                  VALUES (@et, @an, @l, @ft, @rq, @un, @ex, @dv, @ht, @ml, @pr, @pv, @le, @so) RETURNING id", conn);
            cmd.Parameters.AddWithValue("et", body.GetProperty("entity_type").GetString() ?? "");
            cmd.Parameters.AddWithValue("an", body.GetProperty("api_name").GetString() ?? "");
            cmd.Parameters.AddWithValue("l", body.GetProperty("label").GetString() ?? "");
            cmd.Parameters.AddWithValue("ft", body.GetProperty("field_type").GetString() ?? "text");
            cmd.Parameters.AddWithValue("rq", body.TryGetProperty("is_required", out var rq) && rq.GetBoolean());
            cmd.Parameters.AddWithValue("un", body.TryGetProperty("is_unique", out var un) && un.GetBoolean());
            cmd.Parameters.AddWithValue("ex", body.TryGetProperty("is_external_id", out var ex) && ex.GetBoolean());
            cmd.Parameters.AddWithValue("dv", body.TryGetProperty("default_value", out var dv) ? dv.GetString() ?? "" : "");
            cmd.Parameters.AddWithValue("ht", body.TryGetProperty("help_text", out var ht) ? ht.GetString() ?? "" : "");
            cmd.Parameters.AddWithValue("ml", body.TryGetProperty("max_length", out var ml) && ml.ValueKind == JsonValueKind.Number ? ml.GetInt32() : DBNull.Value);
            cmd.Parameters.AddWithValue("pr", body.TryGetProperty("precision_value", out var pr) && pr.ValueKind == JsonValueKind.Number ? pr.GetInt32() : DBNull.Value);
            cmd.Parameters.AddWithValue("pv", body.TryGetProperty("picklist_values", out var pv) && pv.ValueKind == JsonValueKind.Array
                ? pv.EnumerateArray().Select(e => e.GetString() ?? "").ToArray() : Array.Empty<string>());
            cmd.Parameters.AddWithValue("le", body.TryGetProperty("lookup_entity", out var le) ? le.GetString() ?? "" : "");
            cmd.Parameters.AddWithValue("so", body.TryGetProperty("sort_order", out var so) && so.ValueKind == JsonValueKind.Number ? so.GetInt32() : 100);
            var id = (int)(await cmd.ExecuteScalarAsync())!;
            return Results.Created($"/api/custom-objects/fields/{id}", new { id });
        });

        // Custom entity records (CRUD on user-defined entities)
        group.MapGet("/records/{entity}", async (string entity, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                @"SELECT r.id, r.values, r.created_at, r.updated_at
                  FROM custom_entity_records r
                  JOIN custom_entities e ON e.id = r.entity_id
                  WHERE e.api_name = @e AND r.is_deleted = false
                  ORDER BY r.updated_at DESC LIMIT 500", conn);
            cmd.Parameters.AddWithValue("e", entity);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        group.MapPost("/records/{entity}", async (string entity, JsonElement body, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                @"INSERT INTO custom_entity_records (entity_id, values)
                  SELECT e.id, @v::jsonb FROM custom_entities e WHERE e.api_name = @e
                  RETURNING id", conn);
            cmd.Parameters.AddWithValue("e", entity);
            cmd.Parameters.AddWithValue("v", body.GetRawText());
            var id = await cmd.ExecuteScalarAsync();
            if (id is null) return ApiProblem.NotFound($"Custom entity '{entity}' not found");
            return Results.Created($"/api/custom-objects/records/{entity}/{id}", new { id });
        });

        // Field permissions
        group.MapGet("/permissions", async (DbConnectionFactory db, string? role, string? entity_type) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            var where = new List<string>();
            var parms = new List<(string, object)>();
            if (!string.IsNullOrEmpty(role))        { where.Add("role_name = @r");    parms.Add(("r", role)); }
            if (!string.IsNullOrEmpty(entity_type)) { where.Add("entity_type = @et"); parms.Add(("et", entity_type)); }
            var whereSql = where.Count > 0 ? "WHERE " + string.Join(" AND ", where) : "";
            await using var cmd = new NpgsqlCommand(
                $"SELECT * FROM field_permissions {whereSql} ORDER BY role_name, entity_type, field_name", conn);
            foreach (var (n, v) in parms) cmd.Parameters.AddWithValue(n, v);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        group.MapPut("/permissions", async (JsonElement body, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                @"INSERT INTO field_permissions (role_name, entity_type, field_name, permission)
                  VALUES (@r, @et, @fn, @p)
                  ON CONFLICT (tenant_id, role_name, entity_type, field_name)
                  DO UPDATE SET permission = @p RETURNING id", conn);
            cmd.Parameters.AddWithValue("r", body.GetProperty("role_name").GetString() ?? "");
            cmd.Parameters.AddWithValue("et", body.GetProperty("entity_type").GetString() ?? "");
            cmd.Parameters.AddWithValue("fn", body.GetProperty("field_name").GetString() ?? "");
            cmd.Parameters.AddWithValue("p", body.GetProperty("permission").GetString() ?? "read");
            var id = (int)(await cmd.ExecuteScalarAsync())!;
            return Results.Ok(new { id });
        });

        return group;
    }
}

// ══════════════════════════════════════════════════════════════════════
// Stage 5: Import wizard + Commerce
// ══════════════════════════════════════════════════════════════════════

public static class ImportCommerceEndpoints
{
    public static RouteGroupBuilder MapImportCommerceEndpoints(this RouteGroupBuilder group)
    {
        // Import jobs
        group.MapGet("/import", async (DbConnectionFactory db, string? status) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            var where = string.IsNullOrEmpty(status) ? "" : "WHERE status = @s";
            await using var cmd = new NpgsqlCommand(
                $"SELECT * FROM import_jobs {where} ORDER BY created_at DESC LIMIT 100", conn);
            if (!string.IsNullOrEmpty(status)) cmd.Parameters.AddWithValue("s", status);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        group.MapPost("/import", async (JsonElement body, ClaimsPrincipal principal, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                @"INSERT INTO import_jobs
                    (entity_type, source_type, filename, file_path, field_mappings, dedup_strategy, dedup_match_field, dry_run, created_by)
                  SELECT @et, @st, @fn, @fp, @fm::jsonb, @ds, @df, @dr, u.id
                  FROM app_users u WHERE u.username = @u
                  RETURNING id", conn);
            cmd.Parameters.AddWithValue("u", principal.Identity?.Name ?? "");
            cmd.Parameters.AddWithValue("et", body.GetProperty("entity_type").GetString() ?? "");
            cmd.Parameters.AddWithValue("st", body.TryGetProperty("source_type", out var st) ? st.GetString() ?? "csv" : "csv");
            cmd.Parameters.AddWithValue("fn", body.TryGetProperty("filename", out var fn) ? fn.GetString() ?? "" : "");
            cmd.Parameters.AddWithValue("fp", body.TryGetProperty("file_path", out var fp) ? fp.GetString() ?? "" : "");
            cmd.Parameters.AddWithValue("fm", body.TryGetProperty("field_mappings", out var fm) ? fm.GetRawText() : "{}");
            cmd.Parameters.AddWithValue("ds", body.TryGetProperty("dedup_strategy", out var ds) ? ds.GetString() ?? "create_new" : "create_new");
            cmd.Parameters.AddWithValue("df", body.TryGetProperty("dedup_match_field", out var df) ? df.GetString() ?? "" : "");
            cmd.Parameters.AddWithValue("dr", !body.TryGetProperty("dry_run", out var dr) || dr.GetBoolean());
            var id = (int)(await cmd.ExecuteScalarAsync())!;
            // Queue processing via pg_notify
            await using var notify = new NpgsqlCommand($"SELECT pg_notify('import_queue', '{id}')", conn);
            await notify.ExecuteNonQueryAsync();
            return Results.Created($"/api/import/{id}", new { id, status = "queued" });
        });

        group.MapGet("/import/{id:int}/rows", async (int id, DbConnectionFactory db, string? status) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            var where = string.IsNullOrEmpty(status) ? "WHERE job_id = @id"
                : "WHERE job_id = @id AND status = @s";
            await using var cmd = new NpgsqlCommand(
                $"SELECT * FROM import_job_rows {where} ORDER BY row_number LIMIT 1000", conn);
            cmd.Parameters.AddWithValue("id", id);
            if (!string.IsNullOrEmpty(status)) cmd.Parameters.AddWithValue("s", status);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        // Shopping cart
        group.MapGet("/cart", async (ClaimsPrincipal principal, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            var username = principal.Identity?.Name ?? "";
            // For authenticated users, find their active cart (via portal_user lookup by email)
            await using var cmd = new NpgsqlCommand(
                @"SELECT c.*
                  FROM shopping_carts c
                  JOIN app_users u ON LOWER(u.email) = LOWER((SELECT email FROM portal_users pu WHERE pu.id = c.portal_user_id))
                  WHERE u.username = @u AND c.status = 'active'
                  ORDER BY c.updated_at DESC LIMIT 1", conn);
            cmd.Parameters.AddWithValue("u", username);
            await using var r = await cmd.ExecuteReaderAsync();
            if (!await r.ReadAsync()) return Results.Ok(new { });
            var row = new Dictionary<string, object?>();
            for (int i = 0; i < r.FieldCount; i++) row[r.GetName(i)] = r.IsDBNull(i) ? null : r.GetValue(i);
            return Results.Ok(row);
        });

        group.MapPost("/cart/items", async (JsonElement body, DbConnectionFactory db) =>
        {
            var cartId = body.GetProperty("cart_id").GetInt32();
            var qty = body.TryGetProperty("quantity", out var q) && q.ValueKind == JsonValueKind.Number ? q.GetDecimal() : 1m;
            var unitPrice = body.GetProperty("unit_price").GetDecimal();
            var discountPct = body.TryGetProperty("discount_pct", out var dp) && dp.ValueKind == JsonValueKind.Number ? dp.GetDecimal() : 0m;
            var lineTotal = qty * unitPrice * (1 - discountPct / 100m);

            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                @"INSERT INTO cart_items (cart_id, product_id, bundle_id, sku, name, quantity, unit_price, discount_pct, line_total)
                  VALUES (@c, @p, @b, @sku, @n, @q, @up, @dp, @lt) RETURNING id", conn);
            cmd.Parameters.AddWithValue("c", cartId);
            cmd.Parameters.AddWithValue("p", body.TryGetProperty("product_id", out var p) && p.ValueKind == JsonValueKind.Number ? p.GetInt32() : DBNull.Value);
            cmd.Parameters.AddWithValue("b", body.TryGetProperty("bundle_id", out var b) && b.ValueKind == JsonValueKind.Number ? b.GetInt32() : DBNull.Value);
            cmd.Parameters.AddWithValue("sku", body.TryGetProperty("sku", out var sku) ? sku.GetString() ?? "" : "");
            cmd.Parameters.AddWithValue("n", body.GetProperty("name").GetString() ?? "");
            cmd.Parameters.AddWithValue("q", qty);
            cmd.Parameters.AddWithValue("up", unitPrice);
            cmd.Parameters.AddWithValue("dp", discountPct);
            cmd.Parameters.AddWithValue("lt", lineTotal);
            var id = (int)(await cmd.ExecuteScalarAsync())!;
            return Results.Ok(new { id });
        });

        // Checkout — converts cart to order
        group.MapPost("/cart/{id:int}/checkout", async (int id, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var tx = await conn.BeginTransactionAsync();
            try
            {
                // Get cart
                await using var cartCmd = new NpgsqlCommand(
                    "SELECT account_id, subtotal, discount_amount, tax_amount, total, currency FROM shopping_carts WHERE id = @id AND status = 'active'", conn);
                cartCmd.Transaction = tx;
                cartCmd.Parameters.AddWithValue("id", id);
                await using var cr = await cartCmd.ExecuteReaderAsync();
                if (!await cr.ReadAsync()) return ApiProblem.NotFound("Cart not found");
                var accountId = cr.IsDBNull(0) ? 0 : cr.GetInt32(0);
                var subtotal = cr.GetDecimal(1);
                var discount = cr.GetDecimal(2);
                var tax = cr.GetDecimal(3);
                var total = cr.GetDecimal(4);
                var currency = cr.GetString(5);
                await cr.CloseAsync();

                if (accountId == 0) return ApiProblem.ValidationError("Cart must be linked to an account for checkout.");

                // Create order
                var orderNum = $"ORD-{DateTime.UtcNow:yyyyMM}-{Guid.NewGuid().ToString()[..6].ToUpper()}";
                await using var ord = new NpgsqlCommand(
                    @"INSERT INTO crm_orders (order_number, account_id, status, subtotal, discount_amount, tax_amount, total, currency)
                      VALUES (@n, @a, 'submitted', @s, @d, @t, @tot, @c) RETURNING id", conn);
                ord.Transaction = tx;
                ord.Parameters.AddWithValue("n", orderNum);
                ord.Parameters.AddWithValue("a", accountId);
                ord.Parameters.AddWithValue("s", subtotal);
                ord.Parameters.AddWithValue("d", discount);
                ord.Parameters.AddWithValue("t", tax);
                ord.Parameters.AddWithValue("tot", total);
                ord.Parameters.AddWithValue("c", currency);
                var orderId = (int)(await ord.ExecuteScalarAsync())!;

                // Copy items
                await using var copy = new NpgsqlCommand(
                    @"INSERT INTO crm_order_lines (order_id, product_id, bundle_id, sku, description, quantity, unit_price, discount_pct, line_total)
                      SELECT @o, product_id, bundle_id, sku, name, quantity, unit_price, discount_pct, line_total
                      FROM cart_items WHERE cart_id = @c", conn);
                copy.Transaction = tx;
                copy.Parameters.AddWithValue("o", orderId);
                copy.Parameters.AddWithValue("c", id);
                await copy.ExecuteNonQueryAsync();

                // Mark cart converted
                await using var mark = new NpgsqlCommand(
                    "UPDATE shopping_carts SET status = 'converted' WHERE id = @id", conn);
                mark.Transaction = tx;
                mark.Parameters.AddWithValue("id", id);
                await mark.ExecuteNonQueryAsync();

                await tx.CommitAsync();
                return Results.Ok(new { order_id = orderId, order_number = orderNum });
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                return ApiProblem.ServerError($"Checkout failed: {ex.Message}");
            }
        });

        // Payments
        group.MapGet("/payments", async (DbConnectionFactory db, int? order_id, string? status) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            var where = new List<string>();
            var parms = new List<(string, object)>();
            if (order_id.HasValue)              { where.Add("order_id = @o");    parms.Add(("o", order_id.Value)); }
            if (!string.IsNullOrEmpty(status)) { where.Add("status = @s");      parms.Add(("s", status)); }
            var whereSql = where.Count > 0 ? "WHERE " + string.Join(" AND ", where) : "";
            await using var cmd = new NpgsqlCommand(
                $"SELECT * FROM payments {whereSql} ORDER BY created_at DESC LIMIT 200", conn);
            foreach (var (n, v) in parms) cmd.Parameters.AddWithValue(n, v);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        group.MapPost("/payments", async (JsonElement body, DbConnectionFactory db) =>
        {
            var num = $"PAY-{DateTime.UtcNow:yyyyMM}-{Guid.NewGuid().ToString()[..6].ToUpper()}";
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                @"INSERT INTO payments (payment_number, order_id, cart_id, account_id, amount, currency, status, payment_method)
                  VALUES (@n, @o, @c, @a, @am, @cur, @s, @pm) RETURNING id", conn);
            cmd.Parameters.AddWithValue("n", num);
            cmd.Parameters.AddWithValue("o", body.TryGetProperty("order_id", out var o) && o.ValueKind == JsonValueKind.Number ? o.GetInt32() : DBNull.Value);
            cmd.Parameters.AddWithValue("c", body.TryGetProperty("cart_id", out var ca) && ca.ValueKind == JsonValueKind.Number ? ca.GetInt32() : DBNull.Value);
            cmd.Parameters.AddWithValue("a", body.TryGetProperty("account_id", out var a) && a.ValueKind == JsonValueKind.Number ? a.GetInt32() : DBNull.Value);
            cmd.Parameters.AddWithValue("am", body.GetProperty("amount").GetDecimal());
            cmd.Parameters.AddWithValue("cur", body.TryGetProperty("currency", out var cur) ? cur.GetString() ?? "GBP" : "GBP");
            cmd.Parameters.AddWithValue("s", body.TryGetProperty("status", out var s) ? s.GetString() ?? "pending" : "pending");
            cmd.Parameters.AddWithValue("pm", body.GetProperty("payment_method").GetString() ?? "card");
            var id = (int)(await cmd.ExecuteScalarAsync())!;
            return Results.Created($"/api/commerce/payments/{id}", new { id, payment_number = num });
        });

        return group;
    }
}
