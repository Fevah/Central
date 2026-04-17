using Npgsql;
using Central.Data;

namespace Central.Api.Endpoints;

public static class ContactEndpoints
{
    private static readonly HashSet<string> WritableColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        "company_id", "prefix", "first_name", "last_name", "email", "phone", "mobile",
        "job_title", "department", "linkedin_url", "is_primary", "contact_type",
        "status", "source", "tags", "notes", "avatar_url"
    };

    private static readonly HashSet<string> FilterableColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        "company_id", "contact_type", "status", "source", "department"
    };

    private static readonly string[] SearchableColumns =
        ["first_name", "last_name", "email", "phone", "job_title", "department"];

    public static RouteGroupBuilder MapContactEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", async (int? offset, int? limit, string? sort, string? order,
            string? search, string? filter, DbConnectionFactory db) =>
        {
            var q = new PaginatedQuery(offset, limit, sort, order, search, filter);
            await using var conn = await db.OpenConnectionAsync();

            var conditions = new List<string> { "c.is_deleted IS NOT TRUE" };
            var allParams = new List<(string Name, object Value)>();
            var (fw, fp) = PaginationHelpers.BuildFilterClause(filter, FilterableColumns);
            if (!string.IsNullOrEmpty(fw)) { conditions.Add(fw); allParams.AddRange(fp); }
            var (sw, sp) = PaginationHelpers.BuildSearchClause(search, SearchableColumns);
            if (!string.IsNullOrEmpty(sw)) { conditions.Add(sw); allParams.AddRange(sp); }

            var where = string.Join(" AND ", conditions);
            var sortClause = PaginationHelpers.BuildSortClause(q, "c.last_name ASC", FilterableColumns);

            await using var countCmd = conn.CreateCommand();
            countCmd.CommandText = $"SELECT COUNT(*) FROM contacts c WHERE {where}";
            foreach (var (n, v) in allParams) countCmd.Parameters.AddWithValue(n, v);
            var total = Convert.ToInt32(await countCmd.ExecuteScalarAsync());

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $@"
                SELECT c.id, c.first_name, c.last_name, c.email, c.phone, c.mobile,
                       c.job_title, c.contact_type, c.status, c.company_id,
                       COALESCE(co.name, '') as company_name, c.created_at
                FROM contacts c
                LEFT JOIN companies co ON co.id = c.company_id
                WHERE {where}
                ORDER BY {sortClause}
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
            await using var cmd = new NpgsqlCommand(
                @"SELECT c.*, COALESCE(co.name, '') as company_name
                  FROM contacts c LEFT JOIN companies co ON co.id = c.company_id
                  WHERE c.id = @id AND c.is_deleted IS NOT TRUE", conn);
            cmd.Parameters.AddWithValue("id", id);
            await using var r = await cmd.ExecuteReaderAsync();
            if (!await r.ReadAsync()) return ApiProblem.NotFound($"Contact {id} not found");
            var row = new Dictionary<string, object?>();
            for (int i = 0; i < r.FieldCount; i++) row[r.GetName(i)] = r.IsDBNull(i) ? null : r.GetValue(i);
            return Results.Ok(row);
        });

        group.MapPost("/", async (Dictionary<string, object?> body, DbConnectionFactory db) =>
        {
            var invalid = EndpointHelpers.ValidateColumns(body.Keys, WritableColumns);
            if (invalid is not null) return invalid;
            if (!body.ContainsKey("first_name") || !body.ContainsKey("last_name"))
                return ApiProblem.ValidationError("first_name and last_name are required.");

            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = conn.CreateCommand();
            var cols = new List<string>(); var pnames = new List<string>(); int pi = 0;
            foreach (var kvp in body) { var pn = $"p{pi++}"; cols.Add(kvp.Key); pnames.Add($"@{pn}"); cmd.Parameters.AddWithValue(pn, kvp.Value ?? DBNull.Value); }
            cmd.CommandText = $"INSERT INTO contacts ({string.Join(", ", cols)}) VALUES ({string.Join(", ", pnames)}) RETURNING id";
            var newId = await cmd.ExecuteScalarAsync();
            return Results.Created($"/api/contacts/{newId}", new { id = newId });
        });

        group.MapPut("/{id:int}", async (int id, Dictionary<string, object?> body, DbConnectionFactory db) =>
        {
            if (body.Count == 0) return ApiProblem.ValidationError("No fields to update.");
            var invalid = EndpointHelpers.ValidateColumns(body.Keys, WritableColumns);
            if (invalid is not null) return invalid;

            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = conn.CreateCommand();
            var sets = new List<string>(); int pi = 0;
            foreach (var kvp in body) { var pn = $"p{pi++}"; sets.Add($"{kvp.Key} = @{pn}"); cmd.Parameters.AddWithValue(pn, kvp.Value ?? DBNull.Value); }
            cmd.CommandText = $"UPDATE contacts SET {string.Join(", ", sets)}, updated_at = NOW() WHERE id = @id AND is_deleted IS NOT TRUE RETURNING id";
            cmd.Parameters.AddWithValue("id", id);
            var result = await cmd.ExecuteScalarAsync();
            return result is null ? ApiProblem.NotFound($"Contact {id} not found") : Results.Ok(new { id });
        });

        group.MapDelete("/{id:int}", async (int id, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                "UPDATE contacts SET is_deleted = true, deleted_at = NOW() WHERE id = @id AND is_deleted IS NOT TRUE RETURNING id", conn);
            cmd.Parameters.AddWithValue("id", id);
            var result = await cmd.ExecuteScalarAsync();
            return result is null ? ApiProblem.NotFound($"Contact {id} not found") : Results.NoContent();
        });

        // GET /api/contacts/{id}/communications
        group.MapGet("/{id:int}/communications", async (int id, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                @"SELECT cc.*, COALESCE(u.display_name, '') as logged_by_name
                  FROM contact_communications cc
                  LEFT JOIN app_users u ON u.id = cc.logged_by
                  WHERE cc.contact_id = @id ORDER BY cc.occurred_at DESC", conn);
            cmd.Parameters.AddWithValue("id", id);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        // POST /api/contacts/{id}/communications
        group.MapPost("/{id:int}/communications", async (int id, System.Text.Json.JsonElement body, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                @"INSERT INTO contact_communications (contact_id, channel, direction, subject, body, logged_by)
                  VALUES (@cid, @ch, @dir, @sub, @body, @by) RETURNING id", conn);
            cmd.Parameters.AddWithValue("cid", id);
            cmd.Parameters.AddWithValue("ch", body.GetProperty("channel").GetString() ?? "note");
            cmd.Parameters.AddWithValue("dir", body.TryGetProperty("direction", out var d) ? d.GetString() ?? "" : "");
            cmd.Parameters.AddWithValue("sub", body.TryGetProperty("subject", out var s) ? s.GetString() ?? "" : "");
            cmd.Parameters.AddWithValue("body", body.TryGetProperty("body", out var b) ? b.GetString() ?? "" : "");
            cmd.Parameters.AddWithValue("by", body.TryGetProperty("logged_by", out var lb) ? lb.GetInt32() : DBNull.Value);
            var newId = await cmd.ExecuteScalarAsync();
            return Results.Created($"/api/contacts/{id}/communications/{newId}", new { id = newId });
        });

        return group;
    }
}
