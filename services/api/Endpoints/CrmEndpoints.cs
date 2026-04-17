using System.Security.Claims;
using System.Text.Json;
using Npgsql;
using Central.Data;

namespace Central.Api.Endpoints;

/// <summary>CRM Accounts — customer/prospect/partner/vendor records.</summary>
public static class CrmAccountEndpoints
{
    private static readonly HashSet<string> WritableColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        "company_id", "name", "account_type", "account_owner_id", "annual_revenue",
        "employee_count", "industry", "rating", "source", "next_follow_up", "stage",
        "website", "description", "tags", "metadata", "is_active"
    };
    private static readonly HashSet<string> FilterableColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        "account_type", "account_owner_id", "industry", "rating", "source", "stage", "is_active"
    };
    private static readonly string[] SearchableColumns = ["name", "industry", "website", "description"];

    public static RouteGroupBuilder MapCrmAccountEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", async (int? offset, int? limit, string? sort, string? order, string? search, string? filter, DbConnectionFactory db) =>
        {
            var q = new PaginatedQuery(offset, limit, sort, order, search, filter);
            await using var conn = await db.OpenConnectionAsync();

            var conditions = new List<string> { "a.is_deleted IS NOT TRUE" };
            var allParams = new List<(string, object)>();
            var (fw, fp) = PaginationHelpers.BuildFilterClause(filter, FilterableColumns);
            if (!string.IsNullOrEmpty(fw)) { conditions.Add(fw); allParams.AddRange(fp); }
            var (sw, sp) = PaginationHelpers.BuildSearchClause(search, SearchableColumns);
            if (!string.IsNullOrEmpty(sw)) { conditions.Add(sw); allParams.AddRange(sp); }

            var where = string.Join(" AND ", conditions);
            var sortClause = PaginationHelpers.BuildSortClause(q, "a.name ASC", FilterableColumns);

            await using var countCmd = conn.CreateCommand();
            countCmd.CommandText = $"SELECT COUNT(*) FROM crm_accounts a WHERE {where}";
            foreach (var (n, v) in allParams) countCmd.Parameters.AddWithValue(n, v);
            var total = Convert.ToInt32(await countCmd.ExecuteScalarAsync());

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $@"
                SELECT a.id, a.name, a.account_type, a.industry, a.rating, a.stage,
                       a.annual_revenue, a.employee_count, a.source,
                       a.account_owner_id, COALESCE(u.display_name,'') AS owner_name,
                       a.last_activity_at, a.next_follow_up, a.website, a.created_at
                FROM crm_accounts a
                LEFT JOIN app_users u ON u.id = a.account_owner_id
                WHERE {where} ORDER BY {sortClause}
                LIMIT @limit OFFSET @offset";
            foreach (var (n, v) in allParams) cmd.Parameters.AddWithValue(n, v);
            cmd.Parameters.AddWithValue("limit", q.EffectiveLimit);
            cmd.Parameters.AddWithValue("offset", q.EffectiveOffset);

            await using var reader = await cmd.ExecuteReaderAsync();
            return Results.Ok(await PaginationHelpers.ExecutePaginatedAsync(reader, total, q));
        });

        group.MapGet("/{id:int}", async (int id, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand("SELECT * FROM crm_accounts WHERE id = @id AND is_deleted IS NOT TRUE", conn);
            cmd.Parameters.AddWithValue("id", id);
            await using var r = await cmd.ExecuteReaderAsync();
            if (!await r.ReadAsync()) return ApiProblem.NotFound($"Account {id} not found");
            var row = new Dictionary<string, object?>();
            for (int i = 0; i < r.FieldCount; i++) row[r.GetName(i)] = r.IsDBNull(i) ? null : r.GetValue(i);
            return Results.Ok(row);
        });

        group.MapPost("/", async (Dictionary<string, object?> body, DbConnectionFactory db) =>
        {
            var invalid = EndpointHelpers.ValidateColumns(body.Keys, WritableColumns);
            if (invalid is not null) return invalid;
            if (!body.ContainsKey("name")) return ApiProblem.ValidationError("Account name is required.");

            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = conn.CreateCommand();
            var cols = new List<string>(); var p = new List<string>(); int i = 0;
            foreach (var kvp in body) { var pn = $"p{i++}"; cols.Add(kvp.Key); p.Add($"@{pn}"); cmd.Parameters.AddWithValue(pn, kvp.Value ?? DBNull.Value); }
            cmd.CommandText = $"INSERT INTO crm_accounts ({string.Join(",", cols)}) VALUES ({string.Join(",", p)}) RETURNING id";
            var newId = await cmd.ExecuteScalarAsync();
            return Results.Created($"/api/crm/accounts/{newId}", new { id = newId });
        });

        group.MapPut("/{id:int}", async (int id, Dictionary<string, object?> body, DbConnectionFactory db) =>
        {
            if (body.Count == 0) return ApiProblem.ValidationError("No fields to update.");
            var invalid = EndpointHelpers.ValidateColumns(body.Keys, WritableColumns);
            if (invalid is not null) return invalid;

            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = conn.CreateCommand();
            var sets = new List<string>(); int i = 0;
            foreach (var kvp in body) { var pn = $"p{i++}"; sets.Add($"{kvp.Key} = @{pn}"); cmd.Parameters.AddWithValue(pn, kvp.Value ?? DBNull.Value); }
            cmd.CommandText = $"UPDATE crm_accounts SET {string.Join(",", sets)}, updated_at = NOW() WHERE id = @id AND is_deleted IS NOT TRUE RETURNING id";
            cmd.Parameters.AddWithValue("id", id);
            var result = await cmd.ExecuteScalarAsync();
            return result is null ? ApiProblem.NotFound($"Account {id} not found") : Results.Ok(new { id });
        });

        group.MapDelete("/{id:int}", async (int id, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand("UPDATE crm_accounts SET is_deleted = true, deleted_at = NOW() WHERE id = @id AND is_deleted IS NOT TRUE RETURNING id", conn);
            cmd.Parameters.AddWithValue("id", id);
            var r = await cmd.ExecuteScalarAsync();
            return r is null ? ApiProblem.NotFound($"Account {id} not found") : Results.NoContent();
        });

        // Account ↔ Contact linking
        group.MapGet("/{id:int}/contacts", async (int id, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                @"SELECT ac.id, ac.contact_id, c.first_name, c.last_name, c.email,
                         ac.role_in_account, ac.is_primary, ac.added_at
                  FROM crm_account_contacts ac
                  JOIN contacts c ON c.id = ac.contact_id
                  WHERE ac.account_id = @id ORDER BY ac.is_primary DESC, c.last_name", conn);
            cmd.Parameters.AddWithValue("id", id);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        group.MapPost("/{id:int}/contacts", async (int id, JsonElement body, DbConnectionFactory db) =>
        {
            var contactId = body.GetProperty("contact_id").GetInt32();
            var role = body.TryGetProperty("role_in_account", out var r) ? r.GetString() ?? "user" : "user";
            var isPrimary = body.TryGetProperty("is_primary", out var ip) && ip.GetBoolean();

            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                @"INSERT INTO crm_account_contacts (account_id, contact_id, role_in_account, is_primary)
                  VALUES (@a, @c, @r, @p)
                  ON CONFLICT (account_id, contact_id) DO UPDATE SET role_in_account = @r, is_primary = @p
                  RETURNING id", conn);
            cmd.Parameters.AddWithValue("a", id);
            cmd.Parameters.AddWithValue("c", contactId);
            cmd.Parameters.AddWithValue("r", role);
            cmd.Parameters.AddWithValue("p", isPrimary);
            var linkId = (int)(await cmd.ExecuteScalarAsync())!;
            return Results.Ok(new { id = linkId });
        });

        return group;
    }
}

/// <summary>Deals / Opportunities pipeline.</summary>
public static class CrmDealEndpoints
{
    private static readonly HashSet<string> WritableColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        "account_id", "contact_id", "title", "description", "value", "currency",
        "stage_id", "stage", "probability", "expected_close", "actual_close",
        "owner_id", "source", "competitor", "loss_reason", "next_step", "tags", "metadata"
    };
    private static readonly HashSet<string> FilterableColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        "account_id", "owner_id", "stage", "source", "currency"
    };
    private static readonly string[] SearchableColumns = ["title", "description", "next_step"];

    public static RouteGroupBuilder MapCrmDealEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/stages", async (DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand("SELECT * FROM crm_deal_stages WHERE is_active = true ORDER BY sort_order", conn);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        group.MapGet("/", async (int? offset, int? limit, string? sort, string? order, string? search, string? filter, DbConnectionFactory db) =>
        {
            var q = new PaginatedQuery(offset, limit, sort, order, search, filter);
            await using var conn = await db.OpenConnectionAsync();

            var conditions = new List<string> { "d.is_deleted IS NOT TRUE" };
            var allParams = new List<(string, object)>();
            var (fw, fp) = PaginationHelpers.BuildFilterClause(filter, FilterableColumns);
            if (!string.IsNullOrEmpty(fw)) { conditions.Add(fw); allParams.AddRange(fp); }
            var (sw, sp) = PaginationHelpers.BuildSearchClause(search, SearchableColumns);
            if (!string.IsNullOrEmpty(sw)) { conditions.Add(sw); allParams.AddRange(sp); }

            var where = string.Join(" AND ", conditions);
            var sortClause = PaginationHelpers.BuildSortClause(q, "d.updated_at DESC", FilterableColumns);

            await using var countCmd = conn.CreateCommand();
            countCmd.CommandText = $"SELECT COUNT(*) FROM crm_deals d WHERE {where}";
            foreach (var (n, v) in allParams) countCmd.Parameters.AddWithValue(n, v);
            var total = Convert.ToInt32(await countCmd.ExecuteScalarAsync());

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $@"
                SELECT d.id, d.title, d.value, d.currency, d.stage, d.probability,
                       d.expected_close, d.actual_close, d.source,
                       d.account_id, COALESCE(a.name,'') AS account_name,
                       d.contact_id, COALESCE(c.first_name||' '||c.last_name,'') AS contact_name,
                       d.owner_id, COALESCE(u.display_name,'') AS owner_name,
                       d.created_at, d.updated_at
                FROM crm_deals d
                LEFT JOIN crm_accounts a ON a.id = d.account_id
                LEFT JOIN contacts c ON c.id = d.contact_id
                LEFT JOIN app_users u ON u.id = d.owner_id
                WHERE {where} ORDER BY {sortClause}
                LIMIT @limit OFFSET @offset";
            foreach (var (n, v) in allParams) cmd.Parameters.AddWithValue(n, v);
            cmd.Parameters.AddWithValue("limit", q.EffectiveLimit);
            cmd.Parameters.AddWithValue("offset", q.EffectiveOffset);

            await using var reader = await cmd.ExecuteReaderAsync();
            return Results.Ok(await PaginationHelpers.ExecutePaginatedAsync(reader, total, q));
        });

        group.MapPost("/", async (Dictionary<string, object?> body, DbConnectionFactory db) =>
        {
            var invalid = EndpointHelpers.ValidateColumns(body.Keys, WritableColumns);
            if (invalid is not null) return invalid;
            if (!body.ContainsKey("title")) return ApiProblem.ValidationError("Deal title is required.");

            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = conn.CreateCommand();
            var cols = new List<string>(); var p = new List<string>(); int i = 0;
            foreach (var kvp in body) { var pn = $"p{i++}"; cols.Add(kvp.Key); p.Add($"@{pn}"); cmd.Parameters.AddWithValue(pn, kvp.Value ?? DBNull.Value); }
            cmd.CommandText = $"INSERT INTO crm_deals ({string.Join(",", cols)}) VALUES ({string.Join(",", p)}) RETURNING id";
            var newId = await cmd.ExecuteScalarAsync();
            return Results.Created($"/api/crm/deals/{newId}", new { id = newId });
        });

        group.MapPut("/{id:int}", async (int id, Dictionary<string, object?> body, DbConnectionFactory db) =>
        {
            if (body.Count == 0) return ApiProblem.ValidationError("No fields to update.");
            var invalid = EndpointHelpers.ValidateColumns(body.Keys, WritableColumns);
            if (invalid is not null) return invalid;

            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = conn.CreateCommand();
            var sets = new List<string>(); int i = 0;
            foreach (var kvp in body) { var pn = $"p{i++}"; sets.Add($"{kvp.Key} = @{pn}"); cmd.Parameters.AddWithValue(pn, kvp.Value ?? DBNull.Value); }
            cmd.CommandText = $"UPDATE crm_deals SET {string.Join(",", sets)}, updated_at = NOW() WHERE id = @id AND is_deleted IS NOT TRUE RETURNING id";
            cmd.Parameters.AddWithValue("id", id);
            var r = await cmd.ExecuteScalarAsync();
            return r is null ? ApiProblem.NotFound($"Deal {id} not found") : Results.Ok(new { id });
        });

        group.MapDelete("/{id:int}", async (int id, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand("UPDATE crm_deals SET is_deleted = true, deleted_at = NOW() WHERE id = @id AND is_deleted IS NOT TRUE RETURNING id", conn);
            cmd.Parameters.AddWithValue("id", id);
            var r = await cmd.ExecuteScalarAsync();
            return r is null ? ApiProblem.NotFound($"Deal {id} not found") : Results.NoContent();
        });

        // Pipeline summary (value + count per stage)
        group.MapGet("/pipeline", async (DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                @"SELECT d.stage, COUNT(*) AS deal_count, COALESCE(SUM(d.value), 0) AS total_value,
                         COALESCE(SUM(d.value * d.probability / 100.0), 0) AS weighted_value
                  FROM crm_deals d WHERE d.is_deleted IS NOT TRUE
                    AND d.actual_close IS NULL
                  GROUP BY d.stage ORDER BY MIN(COALESCE((SELECT sort_order FROM crm_deal_stages s WHERE s.name = d.stage), 100))", conn);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        return group;
    }
}

/// <summary>Leads + scoring + conversion to account/contact/deal.</summary>
public static class CrmLeadEndpoints
{
    private static readonly HashSet<string> WritableColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        "first_name", "last_name", "email", "phone", "company_name", "title", "source",
        "status", "score", "owner_id", "notes", "tags", "metadata"
    };

    public static RouteGroupBuilder MapCrmLeadEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", async (DbConnectionFactory db, string? status) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            var where = string.IsNullOrEmpty(status) ? "l.is_deleted IS NOT TRUE"
                : "l.is_deleted IS NOT TRUE AND l.status = @status";
            await using var cmd = new NpgsqlCommand($@"
                SELECT l.id, l.first_name, l.last_name, l.email, l.phone, l.company_name,
                       l.title, l.source, l.status, l.score,
                       l.owner_id, COALESCE(u.display_name, '') AS owner_name,
                       l.converted_at, l.created_at
                FROM crm_leads l
                LEFT JOIN app_users u ON u.id = l.owner_id
                WHERE {where} ORDER BY l.score DESC, l.created_at DESC LIMIT 500", conn);
            if (!string.IsNullOrEmpty(status)) cmd.Parameters.AddWithValue("status", status);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        group.MapPost("/", async (Dictionary<string, object?> body, DbConnectionFactory db) =>
        {
            var invalid = EndpointHelpers.ValidateColumns(body.Keys, WritableColumns);
            if (invalid is not null) return invalid;

            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = conn.CreateCommand();
            var cols = new List<string>(); var p = new List<string>(); int i = 0;
            foreach (var kvp in body) { var pn = $"p{i++}"; cols.Add(kvp.Key); p.Add($"@{pn}"); cmd.Parameters.AddWithValue(pn, kvp.Value ?? DBNull.Value); }
            cmd.CommandText = $"INSERT INTO crm_leads ({string.Join(",", cols)}) VALUES ({string.Join(",", p)}) RETURNING id";
            var newId = await cmd.ExecuteScalarAsync();
            return Results.Created($"/api/crm/leads/{newId}", new { id = newId });
        });

        group.MapPut("/{id:int}", async (int id, Dictionary<string, object?> body, DbConnectionFactory db) =>
        {
            var invalid = EndpointHelpers.ValidateColumns(body.Keys, WritableColumns);
            if (invalid is not null) return invalid;

            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = conn.CreateCommand();
            var sets = new List<string>(); int i = 0;
            foreach (var kvp in body) { var pn = $"p{i++}"; sets.Add($"{kvp.Key} = @{pn}"); cmd.Parameters.AddWithValue(pn, kvp.Value ?? DBNull.Value); }
            cmd.CommandText = $"UPDATE crm_leads SET {string.Join(",", sets)}, updated_at = NOW() WHERE id = @id AND is_deleted IS NOT TRUE RETURNING id";
            cmd.Parameters.AddWithValue("id", id);
            var r = await cmd.ExecuteScalarAsync();
            return r is null ? ApiProblem.NotFound($"Lead {id} not found") : Results.Ok(new { id });
        });

        // Convert lead → account + contact + deal
        group.MapPost("/{id:int}/convert", async (int id, JsonElement body, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var tx = await conn.BeginTransactionAsync();

            try
            {
                // Load lead
                await using var loadCmd = new NpgsqlCommand(
                    "SELECT first_name, last_name, email, phone, company_name, title, converted_at FROM crm_leads WHERE id = @id", conn);
                loadCmd.Parameters.AddWithValue("id", id);
                await using var lr = await loadCmd.ExecuteReaderAsync();
                if (!await lr.ReadAsync()) return ApiProblem.NotFound($"Lead {id} not found");
                if (!lr.IsDBNull(6)) return ApiProblem.Conflict("Lead already converted");
                var firstName = lr.IsDBNull(0) ? "" : lr.GetString(0);
                var lastName  = lr.IsDBNull(1) ? "" : lr.GetString(1);
                var email     = lr.IsDBNull(2) ? "" : lr.GetString(2);
                var phone     = lr.IsDBNull(3) ? "" : lr.GetString(3);
                var companyName = lr.IsDBNull(4) ? "" : lr.GetString(4);
                var title     = lr.IsDBNull(5) ? "" : lr.GetString(5);
                await lr.CloseAsync();

                // Create company (if not exists)
                await using var comp = new NpgsqlCommand(
                    "INSERT INTO companies (name) VALUES (@n) RETURNING id", conn);
                comp.Transaction = tx;
                comp.Parameters.AddWithValue("n", string.IsNullOrEmpty(companyName) ? $"{firstName} {lastName}" : companyName);
                var companyId = (int)(await comp.ExecuteScalarAsync())!;

                // Create contact
                await using var cont = new NpgsqlCommand(
                    @"INSERT INTO contacts (company_id, first_name, last_name, email, phone, job_title, source)
                      VALUES (@cid, @fn, @ln, @em, @ph, @t, 'lead_conversion') RETURNING id", conn);
                cont.Transaction = tx;
                cont.Parameters.AddWithValue("cid", companyId);
                cont.Parameters.AddWithValue("fn", firstName);
                cont.Parameters.AddWithValue("ln", lastName);
                cont.Parameters.AddWithValue("em", (object)email ?? DBNull.Value);
                cont.Parameters.AddWithValue("ph", (object)phone ?? DBNull.Value);
                cont.Parameters.AddWithValue("t", (object)title ?? DBNull.Value);
                var contactId = (int)(await cont.ExecuteScalarAsync())!;

                // Create account
                await using var acc = new NpgsqlCommand(
                    @"INSERT INTO crm_accounts (company_id, name, account_type, source)
                      VALUES (@cid, @n, 'prospect', 'lead_conversion') RETURNING id", conn);
                acc.Transaction = tx;
                acc.Parameters.AddWithValue("cid", companyId);
                acc.Parameters.AddWithValue("n", string.IsNullOrEmpty(companyName) ? $"{firstName} {lastName}" : companyName);
                var accountId = (int)(await acc.ExecuteScalarAsync())!;

                // Link contact to account
                await using var link = new NpgsqlCommand(
                    "INSERT INTO crm_account_contacts (account_id, contact_id, role_in_account, is_primary) VALUES (@a, @c, 'primary', true)", conn);
                link.Transaction = tx;
                link.Parameters.AddWithValue("a", accountId);
                link.Parameters.AddWithValue("c", contactId);
                await link.ExecuteNonQueryAsync();

                int? dealId = null;
                if (body.TryGetProperty("create_deal", out var cd) && cd.GetBoolean())
                {
                    var dealTitle = body.TryGetProperty("deal_title", out var dt) ? dt.GetString() ?? $"New deal — {firstName} {lastName}" : $"New deal — {firstName} {lastName}";
                    var value = body.TryGetProperty("deal_value", out var dv) && dv.ValueKind == JsonValueKind.Number ? dv.GetDecimal() : 0m;

                    await using var dcmd = new NpgsqlCommand(
                        @"INSERT INTO crm_deals (account_id, contact_id, title, value, currency, stage, probability)
                          VALUES (@a, @c, @t, @v, 'GBP', 'Qualification', 20) RETURNING id", conn);
                    dcmd.Transaction = tx;
                    dcmd.Parameters.AddWithValue("a", accountId);
                    dcmd.Parameters.AddWithValue("c", contactId);
                    dcmd.Parameters.AddWithValue("t", dealTitle);
                    dcmd.Parameters.AddWithValue("v", value);
                    dealId = (int)(await dcmd.ExecuteScalarAsync())!;
                }

                // Mark lead converted
                await using var mark = new NpgsqlCommand(
                    @"UPDATE crm_leads SET status = 'converted', converted_at = NOW(),
                      converted_account_id = @a, converted_contact_id = @c, converted_deal_id = @d
                      WHERE id = @id", conn);
                mark.Transaction = tx;
                mark.Parameters.AddWithValue("id", id);
                mark.Parameters.AddWithValue("a", accountId);
                mark.Parameters.AddWithValue("c", contactId);
                mark.Parameters.AddWithValue("d", dealId.HasValue ? dealId.Value : DBNull.Value);
                await mark.ExecuteNonQueryAsync();

                await tx.CommitAsync();

                return Results.Ok(new
                {
                    lead_id = id,
                    account_id = accountId,
                    contact_id = contactId,
                    deal_id = dealId,
                    company_id = companyId
                });
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                return ApiProblem.ServerError($"Conversion failed: {ex.Message}");
            }
        });

        // Lead scoring rules
        group.MapGet("/scoring-rules", async (DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand("SELECT * FROM crm_lead_scoring_rules WHERE is_enabled = true ORDER BY points DESC", conn);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        group.MapPost("/scoring-rules", async (JsonElement body, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                @"INSERT INTO crm_lead_scoring_rules (name, field, operator, value, points)
                  VALUES (@n, @f, @o, @v, @p) RETURNING id", conn);
            cmd.Parameters.AddWithValue("n", body.GetProperty("name").GetString() ?? "");
            cmd.Parameters.AddWithValue("f", body.GetProperty("field").GetString() ?? "");
            cmd.Parameters.AddWithValue("o", body.GetProperty("operator").GetString() ?? "equals");
            cmd.Parameters.AddWithValue("v", body.GetProperty("value").GetString() ?? "");
            cmd.Parameters.AddWithValue("p", body.TryGetProperty("points", out var pt) ? pt.GetInt32() : 10);
            var id = (int)(await cmd.ExecuteScalarAsync())!;
            return Results.Created($"/api/crm/leads/scoring-rules/{id}", new { id });
        });

        return group;
    }
}

/// <summary>CRM activities (unified timeline) + Products + Quotes.</summary>
public static class CrmActivityEndpoints
{
    public static RouteGroupBuilder MapCrmActivityEndpoints(this RouteGroupBuilder group)
    {
        // GET /api/crm/activities?entity_type=account&entity_id=5
        group.MapGet("/", async (string entity_type, int entity_id, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                @"SELECT a.*, COALESCE(u.display_name, '') AS logged_by_name
                  FROM crm_activities a
                  LEFT JOIN app_users u ON u.id = a.logged_by
                  WHERE a.entity_type = @et AND a.entity_id = @eid
                  ORDER BY a.occurred_at DESC LIMIT 500", conn);
            cmd.Parameters.AddWithValue("et", entity_type);
            cmd.Parameters.AddWithValue("eid", entity_id);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        group.MapPost("/", async (ClaimsPrincipal principal, JsonElement body, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                @"INSERT INTO crm_activities (entity_type, entity_id, activity_type, subject, body, direction, duration_minutes, due_at, is_completed, logged_by)
                  SELECT @et, @eid, @at, @sub, @bod, @dir, @dur, @due, @comp,
                         (SELECT id FROM app_users WHERE username = @u)
                  RETURNING id", conn);
            cmd.Parameters.AddWithValue("et", body.GetProperty("entity_type").GetString() ?? "");
            cmd.Parameters.AddWithValue("eid", body.GetProperty("entity_id").GetInt32());
            cmd.Parameters.AddWithValue("at", body.GetProperty("activity_type").GetString() ?? "note");
            cmd.Parameters.AddWithValue("sub", body.TryGetProperty("subject", out var s) ? s.GetString() ?? "" : "");
            cmd.Parameters.AddWithValue("bod", body.TryGetProperty("body", out var bo) ? bo.GetString() ?? "" : "");
            cmd.Parameters.AddWithValue("dir", body.TryGetProperty("direction", out var d) ? d.GetString() ?? "" : "");
            cmd.Parameters.AddWithValue("dur", body.TryGetProperty("duration_minutes", out var dm) && dm.ValueKind == JsonValueKind.Number ? dm.GetInt32() : DBNull.Value);
            cmd.Parameters.AddWithValue("due", body.TryGetProperty("due_at", out var du) && du.ValueKind == JsonValueKind.String ? DateTime.Parse(du.GetString()!) : DBNull.Value);
            cmd.Parameters.AddWithValue("comp", !body.TryGetProperty("is_completed", out var ic) || ic.GetBoolean());
            cmd.Parameters.AddWithValue("u", principal.Identity?.Name ?? "");
            var id = (long)(await cmd.ExecuteScalarAsync())!;
            return Results.Created($"/api/crm/activities/{id}", new { id });
        });

        // Mark activity completed
        group.MapPost("/{id:long}/complete", async (long id, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                "UPDATE crm_activities SET is_completed = true WHERE id = @id RETURNING id", conn);
            cmd.Parameters.AddWithValue("id", id);
            var r = await cmd.ExecuteScalarAsync();
            return r is null ? ApiProblem.NotFound("Activity not found") : Results.Ok(new { id });
        });

        return group;
    }
}

/// <summary>Products catalog + quotes with auto-recalculating totals.</summary>
public static class CrmProductQuoteEndpoints
{
    public static RouteGroupBuilder MapCrmProductQuoteEndpoints(this RouteGroupBuilder group)
    {
        // ── Products ──
        group.MapGet("/products", async (DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand("SELECT * FROM crm_products WHERE is_active = true ORDER BY category, name", conn);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        group.MapPost("/products", async (JsonElement body, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                @"INSERT INTO crm_products (sku, name, description, category, unit_price, currency, is_recurring, billing_period, tax_rate_pct, cost_price)
                  VALUES (@sku, @n, @d, @c, @p, @cur, @r, @bp, @tax, @cp) RETURNING id", conn);
            cmd.Parameters.AddWithValue("sku", body.GetProperty("sku").GetString() ?? "");
            cmd.Parameters.AddWithValue("n", body.GetProperty("name").GetString() ?? "");
            cmd.Parameters.AddWithValue("d", body.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "");
            cmd.Parameters.AddWithValue("c", body.TryGetProperty("category", out var c) ? c.GetString() ?? "" : "");
            cmd.Parameters.AddWithValue("p", body.GetProperty("unit_price").GetDecimal());
            cmd.Parameters.AddWithValue("cur", body.TryGetProperty("currency", out var cur) ? cur.GetString() ?? "GBP" : "GBP");
            cmd.Parameters.AddWithValue("r", body.TryGetProperty("is_recurring", out var r) && r.GetBoolean());
            cmd.Parameters.AddWithValue("bp", body.TryGetProperty("billing_period", out var bp) ? bp.GetString() ?? "" : "");
            cmd.Parameters.AddWithValue("tax", body.TryGetProperty("tax_rate_pct", out var t) && t.ValueKind == JsonValueKind.Number ? t.GetDecimal() : 0m);
            cmd.Parameters.AddWithValue("cp", body.TryGetProperty("cost_price", out var cp) && cp.ValueKind == JsonValueKind.Number ? cp.GetDecimal() : DBNull.Value);
            var id = (int)(await cmd.ExecuteScalarAsync())!;
            return Results.Created($"/api/crm/products/{id}", new { id });
        });

        // ── Quotes ──
        group.MapGet("/quotes", async (DbConnectionFactory db, int? deal_id, int? account_id) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            var where = "1=1";
            if (deal_id.HasValue) where += " AND q.deal_id = @dealId";
            if (account_id.HasValue) where += " AND q.account_id = @accountId";
            await using var cmd = new NpgsqlCommand($@"
                SELECT q.*, COALESCE(a.name,'') AS account_name, COALESCE(c.first_name||' '||c.last_name,'') AS contact_name
                FROM crm_quotes q
                LEFT JOIN crm_accounts a ON a.id = q.account_id
                LEFT JOIN contacts c ON c.id = q.contact_id
                WHERE {where} ORDER BY q.created_at DESC LIMIT 500", conn);
            if (deal_id.HasValue) cmd.Parameters.AddWithValue("dealId", deal_id.Value);
            if (account_id.HasValue) cmd.Parameters.AddWithValue("accountId", account_id.Value);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        group.MapPost("/quotes", async (JsonElement body, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            // Generate quote number
            var quoteNumber = body.TryGetProperty("quote_number", out var qn) ? qn.GetString() ?? ""
                : $"Q-{DateTime.UtcNow:yyyyMM}-{Guid.NewGuid().ToString()[..6].ToUpper()}";

            await using var cmd = new NpgsqlCommand(
                @"INSERT INTO crm_quotes (deal_id, account_id, contact_id, quote_number, status, currency,
                    discount_pct, tax_pct, valid_until, notes, terms)
                  VALUES (@d, @a, @c, @qn, @st, @cur, @dp, @tp, @vu, @n, @tm) RETURNING id", conn);
            cmd.Parameters.AddWithValue("d", body.TryGetProperty("deal_id", out var dl) && dl.ValueKind == JsonValueKind.Number ? dl.GetInt32() : DBNull.Value);
            cmd.Parameters.AddWithValue("a", body.TryGetProperty("account_id", out var ac) && ac.ValueKind == JsonValueKind.Number ? ac.GetInt32() : DBNull.Value);
            cmd.Parameters.AddWithValue("c", body.TryGetProperty("contact_id", out var ct) && ct.ValueKind == JsonValueKind.Number ? ct.GetInt32() : DBNull.Value);
            cmd.Parameters.AddWithValue("qn", quoteNumber);
            cmd.Parameters.AddWithValue("st", body.TryGetProperty("status", out var st) ? st.GetString() ?? "draft" : "draft");
            cmd.Parameters.AddWithValue("cur", body.TryGetProperty("currency", out var cur) ? cur.GetString() ?? "GBP" : "GBP");
            cmd.Parameters.AddWithValue("dp", body.TryGetProperty("discount_pct", out var dp) && dp.ValueKind == JsonValueKind.Number ? dp.GetDecimal() : 0m);
            cmd.Parameters.AddWithValue("tp", body.TryGetProperty("tax_pct", out var tp) && tp.ValueKind == JsonValueKind.Number ? tp.GetDecimal() : 0m);
            cmd.Parameters.AddWithValue("vu", body.TryGetProperty("valid_until", out var vu) && vu.ValueKind == JsonValueKind.String ? DateTime.Parse(vu.GetString()!) : DBNull.Value);
            cmd.Parameters.AddWithValue("n", body.TryGetProperty("notes", out var n) ? n.GetString() ?? "" : "");
            cmd.Parameters.AddWithValue("tm", body.TryGetProperty("terms", out var tm) ? tm.GetString() ?? "" : "");
            var id = (int)(await cmd.ExecuteScalarAsync())!;
            return Results.Created($"/api/crm/quotes/{id}", new { id, quote_number = quoteNumber });
        });

        group.MapGet("/quotes/{id:int}/lines", async (int id, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand("SELECT * FROM crm_quote_lines WHERE quote_id = @id ORDER BY sort_order, id", conn);
            cmd.Parameters.AddWithValue("id", id);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        group.MapPost("/quotes/{id:int}/lines", async (int id, JsonElement body, DbConnectionFactory db) =>
        {
            var qty = body.GetProperty("quantity").GetDecimal();
            var unitPrice = body.GetProperty("unit_price").GetDecimal();
            var discountPct = body.TryGetProperty("discount_pct", out var dp) && dp.ValueKind == JsonValueKind.Number ? dp.GetDecimal() : 0m;
            var lineTotal = qty * unitPrice * (1 - discountPct / 100m);

            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                @"INSERT INTO crm_quote_lines (quote_id, product_id, sku, description, quantity, unit_price, discount_pct, line_total, tax_pct, sort_order)
                  VALUES (@q, @p, @sku, @desc, @qty, @up, @dp, @lt, @tax, @so) RETURNING id", conn);
            cmd.Parameters.AddWithValue("q", id);
            cmd.Parameters.AddWithValue("p", body.TryGetProperty("product_id", out var pid) && pid.ValueKind == JsonValueKind.Number ? pid.GetInt32() : DBNull.Value);
            cmd.Parameters.AddWithValue("sku", body.TryGetProperty("sku", out var sku) ? sku.GetString() ?? "" : "");
            cmd.Parameters.AddWithValue("desc", body.GetProperty("description").GetString() ?? "");
            cmd.Parameters.AddWithValue("qty", qty);
            cmd.Parameters.AddWithValue("up", unitPrice);
            cmd.Parameters.AddWithValue("dp", discountPct);
            cmd.Parameters.AddWithValue("lt", lineTotal);
            cmd.Parameters.AddWithValue("tax", body.TryGetProperty("tax_pct", out var tx) && tx.ValueKind == JsonValueKind.Number ? tx.GetDecimal() : 0m);
            cmd.Parameters.AddWithValue("so", body.TryGetProperty("sort_order", out var so) && so.ValueKind == JsonValueKind.Number ? so.GetInt32() : 0);
            var lineId = (int)(await cmd.ExecuteScalarAsync())!;
            return Results.Created($"/api/crm/quotes/{id}/lines/{lineId}", new { id = lineId });
        });

        group.MapDelete("/quotes/{qid:int}/lines/{lid:int}", async (int qid, int lid, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand("DELETE FROM crm_quote_lines WHERE quote_id = @q AND id = @l RETURNING id", conn);
            cmd.Parameters.AddWithValue("q", qid);
            cmd.Parameters.AddWithValue("l", lid);
            var r = await cmd.ExecuteScalarAsync();
            return r is null ? ApiProblem.NotFound("Line not found") : Results.NoContent();
        });

        return group;
    }
}
