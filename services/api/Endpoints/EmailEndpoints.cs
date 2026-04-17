using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using Npgsql;
using Central.Persistence;

namespace Central.Api.Endpoints;

/// <summary>Phase 20: Email integration — accounts, templates, messages, tracking.</summary>
public static class EmailEndpoints
{
    public static RouteGroupBuilder MapEmailEndpoints(this RouteGroupBuilder group)
    {
        // ── Email accounts ──
        group.MapGet("/accounts", async (ClaimsPrincipal principal, DbConnectionFactory db) =>
        {
            var username = principal.Identity?.Name ?? "";
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                @"SELECT ea.id, ea.label, ea.address, ea.provider, ea.auto_log_to_crm,
                         ea.is_shared, ea.is_active, ea.last_sync_at, ea.created_at
                  FROM email_accounts ea
                  JOIN app_users u ON u.id = ea.user_id
                  WHERE u.username = @u OR ea.is_shared = true
                  ORDER BY ea.is_shared, ea.label", conn);
            cmd.Parameters.AddWithValue("u", username);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        group.MapPost("/accounts", async (ClaimsPrincipal principal, JsonElement body, DbConnectionFactory db) =>
        {
            var username = principal.Identity?.Name ?? "";
            await using var conn = await db.OpenConnectionAsync();

            // Encrypt passwords
            string? smtpEnc = null, imapEnc = null;
            if (body.TryGetProperty("smtp_password", out var sp) && !string.IsNullOrEmpty(sp.GetString()))
                smtpEnc = Central.Engine.Auth.CredentialEncryptor.IsAvailable
                    ? Central.Engine.Auth.CredentialEncryptor.Encrypt(sp.GetString()!)
                    : sp.GetString();
            if (body.TryGetProperty("imap_password", out var ip) && !string.IsNullOrEmpty(ip.GetString()))
                imapEnc = Central.Engine.Auth.CredentialEncryptor.IsAvailable
                    ? Central.Engine.Auth.CredentialEncryptor.Encrypt(ip.GetString()!)
                    : ip.GetString();

            await using var cmd = new NpgsqlCommand(
                @"INSERT INTO email_accounts (user_id, label, address, provider,
                    smtp_host, smtp_port, smtp_user, smtp_password_enc, smtp_tls,
                    imap_host, imap_port, imap_user, imap_password_enc,
                    auto_log_to_crm, signature_html, is_shared)
                  SELECT id, @label, @addr, @prov, @sh, @sp, @su, @spw, @stls,
                         @ih, @ip, @iu, @ipw, @auto, @sig, @shared
                  FROM app_users WHERE username = @u RETURNING id", conn);
            cmd.Parameters.AddWithValue("u", username);
            cmd.Parameters.AddWithValue("label", body.GetProperty("label").GetString() ?? "");
            cmd.Parameters.AddWithValue("addr", body.GetProperty("address").GetString() ?? "");
            cmd.Parameters.AddWithValue("prov", body.GetProperty("provider").GetString() ?? "smtp");
            cmd.Parameters.AddWithValue("sh", (object?)body.TryGetValue("smtp_host") ?? DBNull.Value);
            cmd.Parameters.AddWithValue("sp", body.TryGetInt("smtp_port") ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("su", (object?)body.TryGetValue("smtp_user") ?? DBNull.Value);
            cmd.Parameters.AddWithValue("spw", (object?)smtpEnc ?? DBNull.Value);
            cmd.Parameters.AddWithValue("stls", body.TryGetBool("smtp_tls") ?? true);
            cmd.Parameters.AddWithValue("ih", (object?)body.TryGetValue("imap_host") ?? DBNull.Value);
            cmd.Parameters.AddWithValue("ip", body.TryGetInt("imap_port") ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("iu", (object?)body.TryGetValue("imap_user") ?? DBNull.Value);
            cmd.Parameters.AddWithValue("ipw", (object?)imapEnc ?? DBNull.Value);
            cmd.Parameters.AddWithValue("auto", body.TryGetBool("auto_log_to_crm") ?? true);
            cmd.Parameters.AddWithValue("sig", (object?)body.TryGetValue("signature_html") ?? DBNull.Value);
            cmd.Parameters.AddWithValue("shared", body.TryGetBool("is_shared") ?? false);
            var id = (int)(await cmd.ExecuteScalarAsync())!;
            return Results.Created($"/api/email/accounts/{id}", new { id });
        });

        // ── Templates ──
        group.MapGet("/templates", async (DbConnectionFactory db, string? category) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            var where = string.IsNullOrEmpty(category) ? "is_active = true"
                : "is_active = true AND category = @cat";
            await using var cmd = new NpgsqlCommand($"SELECT * FROM email_templates WHERE {where} ORDER BY category, name", conn);
            if (!string.IsNullOrEmpty(category)) cmd.Parameters.AddWithValue("cat", category);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        group.MapPost("/templates", async (JsonElement body, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            var bodyHtml = body.GetProperty("body_html").GetString() ?? "";
            var vars = ExtractMergeFields(bodyHtml + " " + (body.TryGetValue("subject") ?? ""));

            await using var cmd = new NpgsqlCommand(
                @"INSERT INTO email_templates (name, category, subject, body_html, body_text, variables)
                  VALUES (@n, @cat, @subj, @bh, @bt, @vars) RETURNING id", conn);
            cmd.Parameters.AddWithValue("n", body.GetProperty("name").GetString() ?? "");
            cmd.Parameters.AddWithValue("cat", (object?)body.TryGetValue("category") ?? DBNull.Value);
            cmd.Parameters.AddWithValue("subj", body.GetProperty("subject").GetString() ?? "");
            cmd.Parameters.AddWithValue("bh", bodyHtml);
            cmd.Parameters.AddWithValue("bt", (object?)body.TryGetValue("body_text") ?? DBNull.Value);
            cmd.Parameters.AddWithValue("vars", vars);
            var id = (int)(await cmd.ExecuteScalarAsync())!;
            return Results.Created($"/api/email/templates/{id}", new { id, variables = vars });
        });

        // ── Messages (send + list) ──
        group.MapGet("/messages", async (DbConnectionFactory db, int? account_id, int? contact_id, int? deal_id) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            var where = new List<string>();
            var parms = new List<(string, object)>();
            if (account_id.HasValue)   { where.Add("linked_account_id = @aid"); parms.Add(("aid", account_id.Value)); }
            if (contact_id.HasValue)   { where.Add("linked_contact_id = @cid"); parms.Add(("cid", contact_id.Value)); }
            if (deal_id.HasValue)      { where.Add("linked_deal_id = @did");    parms.Add(("did", deal_id.Value)); }
            var whereSql = where.Count > 0 ? "WHERE " + string.Join(" AND ", where) : "";

            await using var cmd = new NpgsqlCommand(
                $@"SELECT id, direction, status, from_address, to_addresses, subject, sent_at, received_at,
                          opened_at, open_count, click_count, linked_contact_id, linked_account_id, linked_deal_id
                   FROM email_messages {whereSql}
                   ORDER BY COALESCE(sent_at, received_at) DESC LIMIT 200", conn);
            foreach (var (n, v) in parms) cmd.Parameters.AddWithValue(n, v);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        group.MapPost("/messages/send", async (ClaimsPrincipal principal, JsonElement body, DbConnectionFactory db) =>
        {
            var username = principal.Identity?.Name ?? "";
            await using var conn = await db.OpenConnectionAsync();

            // Queue message for delivery (background worker sends via SMTP/Graph)
            await using var cmd = new NpgsqlCommand(
                @"INSERT INTO email_messages (account_id, direction, status, from_address, from_name,
                    to_addresses, cc_addresses, subject, body_html, body_text, template_id,
                    linked_contact_id, linked_account_id, linked_deal_id, linked_lead_id,
                    sent_by)
                  VALUES (@acc, 'outbound', 'pending', @from, @fname, @to, @cc, @subj, @bh, @bt, @tpl,
                          @contact, @account, @deal, @lead,
                          (SELECT id FROM app_users WHERE username = @u))
                  RETURNING id", conn);
            cmd.Parameters.AddWithValue("acc", body.GetProperty("account_id").GetInt32());
            cmd.Parameters.AddWithValue("from", body.GetProperty("from_address").GetString() ?? "");
            cmd.Parameters.AddWithValue("fname", (object?)body.TryGetValue("from_name") ?? DBNull.Value);
            cmd.Parameters.AddWithValue("to", ReadStringArray(body, "to_addresses"));
            cmd.Parameters.AddWithValue("cc", ReadStringArray(body, "cc_addresses"));
            cmd.Parameters.AddWithValue("subj", body.GetProperty("subject").GetString() ?? "");
            cmd.Parameters.AddWithValue("bh", body.TryGetValue("body_html") ?? "");
            cmd.Parameters.AddWithValue("bt", (object?)body.TryGetValue("body_text") ?? DBNull.Value);
            cmd.Parameters.AddWithValue("tpl", body.TryGetInt("template_id") ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("contact", body.TryGetInt("linked_contact_id") ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("account", body.TryGetInt("linked_account_id") ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("deal", body.TryGetInt("linked_deal_id") ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("lead", body.TryGetInt("linked_lead_id") ?? (object)DBNull.Value);
            cmd.Parameters.AddWithValue("u", username);
            var id = (long)(await cmd.ExecuteScalarAsync())!;

            // Emit pg_notify so a background worker picks up the send
            await using var notify = new NpgsqlCommand(
                $"SELECT pg_notify('email_send_queue', '{id}')", conn);
            await notify.ExecuteNonQueryAsync();

            return Results.Accepted($"/api/email/messages/{id}", new { id, status = "queued" });
        });

        // Tracking endpoints (pixel + click redirect, anonymous)
        group.MapGet("/track/open/{id:long}", async (long id, HttpContext ctx, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                @"UPDATE email_messages
                  SET opened_at = COALESCE(opened_at, NOW()), open_count = open_count + 1,
                      status = CASE WHEN status IN ('sent','delivered') THEN 'opened' ELSE status END
                  WHERE id = @id", conn);
            cmd.Parameters.AddWithValue("id", id);
            await cmd.ExecuteNonQueryAsync();

            await using var ev = new NpgsqlCommand(
                @"INSERT INTO email_tracking_events (message_id, event_type, ip_address, user_agent)
                  VALUES (@id, 'open', @ip::inet, @ua)", conn);
            ev.Parameters.AddWithValue("id", id);
            ev.Parameters.AddWithValue("ip", (object)(ctx.Connection.RemoteIpAddress?.ToString() ?? "0.0.0.0"));
            ev.Parameters.AddWithValue("ua", (object)(ctx.Request.Headers.UserAgent.ToString() ?? ""));
            await ev.ExecuteNonQueryAsync();

            // 1x1 transparent GIF
            var pixel = new byte[] { 0x47,0x49,0x46,0x38,0x39,0x61,0x01,0x00,0x01,0x00,0x80,0x00,0x00,0x00,0x00,0x00,
                                     0xff,0xff,0xff,0x21,0xf9,0x04,0x01,0x00,0x00,0x00,0x00,0x2c,0x00,0x00,0x00,0x00,
                                     0x01,0x00,0x01,0x00,0x00,0x02,0x02,0x44,0x01,0x00,0x3b };
            return Results.File(pixel, "image/gif");
        }).AllowAnonymous();

        group.MapGet("/track/click/{id:long}", async (long id, string url, HttpContext ctx, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                @"UPDATE email_messages SET clicked_at = COALESCE(clicked_at, NOW()), click_count = click_count + 1,
                  status = CASE WHEN status IN ('sent','delivered','opened') THEN 'clicked' ELSE status END
                  WHERE id = @id", conn);
            cmd.Parameters.AddWithValue("id", id);
            await cmd.ExecuteNonQueryAsync();

            await using var ev = new NpgsqlCommand(
                @"INSERT INTO email_tracking_events (message_id, event_type, url, ip_address)
                  VALUES (@id, 'click', @url, @ip::inet)", conn);
            ev.Parameters.AddWithValue("id", id);
            ev.Parameters.AddWithValue("url", url);
            ev.Parameters.AddWithValue("ip", (object)(ctx.Connection.RemoteIpAddress?.ToString() ?? "0.0.0.0"));
            await ev.ExecuteNonQueryAsync();

            return Results.Redirect(url);
        }).AllowAnonymous();

        return group;
    }

    // Helper: extract {{merge.fields}} from template body
    private static string[] ExtractMergeFields(string content)
    {
        var matches = Regex.Matches(content ?? "", @"\{\{\s*([a-zA-Z_][a-zA-Z0-9_.]*)\s*\}\}");
        return matches.Select(m => m.Groups[1].Value).Distinct().ToArray();
    }

    private static string[] ReadStringArray(JsonElement body, string key)
    {
        if (!body.TryGetProperty(key, out var arr) || arr.ValueKind != JsonValueKind.Array) return [];
        return arr.EnumerateArray().Select(e => e.GetString() ?? "").Where(s => !string.IsNullOrEmpty(s)).ToArray();
    }
}

// Extension helpers for cleaner JsonElement access
internal static class JsonElementExtensions
{
    public static string? TryGetValue(this JsonElement el, string prop)
        => el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
    public static int? TryGetInt(this JsonElement el, string prop)
        => el.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : null;
    public static bool? TryGetBool(this JsonElement el, string prop)
        => el.TryGetProperty(prop, out var v) && (v.ValueKind == JsonValueKind.True || v.ValueKind == JsonValueKind.False)
            ? v.GetBoolean() : null;
}
