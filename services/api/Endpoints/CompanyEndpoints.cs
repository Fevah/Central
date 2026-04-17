using System.Text.Json;
using Npgsql;
using Central.Persistence;

namespace Central.Api.Endpoints;

public static class CompanyEndpoints
{
    private static readonly HashSet<string> WritableColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        "name", "legal_name", "registration_no", "tax_id", "industry",
        "size_band", "website", "logo_url", "parent_id", "is_active", "metadata"
    };

    private static readonly HashSet<string> FilterableColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        "name", "industry", "size_band", "is_active"
    };

    private static readonly string[] SearchableColumns =
        ["name", "legal_name", "industry", "website"];

    public static RouteGroupBuilder MapCompanyEndpoints(this RouteGroupBuilder group)
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
            var sortClause = PaginationHelpers.BuildSortClause(q, "c.name ASC", FilterableColumns);

            await using var countCmd = conn.CreateCommand();
            countCmd.CommandText = $"SELECT COUNT(*) FROM companies c WHERE {where}";
            foreach (var (n, v) in allParams) countCmd.Parameters.AddWithValue(n, v);
            var total = Convert.ToInt32(await countCmd.ExecuteScalarAsync());

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $@"
                SELECT c.id, c.name, c.legal_name, c.industry, c.size_band, c.website,
                       c.is_active, c.parent_id, p.name as parent_name, c.created_at
                FROM companies c
                LEFT JOIN companies p ON p.id = c.parent_id
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
                "SELECT * FROM companies WHERE id = @id AND is_deleted IS NOT TRUE", conn);
            cmd.Parameters.AddWithValue("id", id);
            await using var r = await cmd.ExecuteReaderAsync();
            if (!await r.ReadAsync()) return ApiProblem.NotFound($"Company {id} not found");
            var row = new Dictionary<string, object?>();
            for (int i = 0; i < r.FieldCount; i++)
                row[r.GetName(i)] = r.IsDBNull(i) ? null : r.GetValue(i);
            return Results.Ok(row);
        });

        group.MapPost("/", async (Dictionary<string, object?> body, DbConnectionFactory db) =>
        {
            var invalid = EndpointHelpers.ValidateColumns(body.Keys, WritableColumns);
            if (invalid is not null) return invalid;
            if (!body.ContainsKey("name")) return ApiProblem.ValidationError("Company name is required.");

            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = conn.CreateCommand();
            var cols = new List<string>(); var pnames = new List<string>(); int pi = 0;
            foreach (var kvp in body) { var pn = $"p{pi++}"; cols.Add(kvp.Key); pnames.Add($"@{pn}"); cmd.Parameters.AddWithValue(pn, kvp.Value ?? DBNull.Value); }
            cmd.CommandText = $"INSERT INTO companies ({string.Join(", ", cols)}) VALUES ({string.Join(", ", pnames)}) RETURNING id";
            var newId = await cmd.ExecuteScalarAsync();
            return Results.Created($"/api/companies/{newId}", new { id = newId });
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
            cmd.CommandText = $"UPDATE companies SET {string.Join(", ", sets)}, updated_at = NOW() WHERE id = @id AND is_deleted IS NOT TRUE RETURNING id";
            cmd.Parameters.AddWithValue("id", id);
            var result = await cmd.ExecuteScalarAsync();
            return result is null ? ApiProblem.NotFound($"Company {id} not found") : Results.Ok(new { id });
        });

        group.MapDelete("/{id:int}", async (int id, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                "UPDATE companies SET is_deleted = true, deleted_at = NOW() WHERE id = @id AND is_deleted IS NOT TRUE RETURNING id", conn);
            cmd.Parameters.AddWithValue("id", id);
            var result = await cmd.ExecuteScalarAsync();
            return result is null ? ApiProblem.NotFound($"Company {id} not found") : Results.NoContent();
        });

        // GET /api/companies/{id}/contacts
        group.MapGet("/{id:int}/contacts", async (int id, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                @"SELECT id, first_name, last_name, email, phone, job_title, contact_type, status
                  FROM contacts WHERE company_id = @id AND is_deleted IS NOT TRUE ORDER BY last_name", conn);
            cmd.Parameters.AddWithValue("id", id);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        // GET /api/companies/{id}/addresses
        group.MapGet("/{id:int}/addresses", async (int id, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                "SELECT * FROM addresses WHERE entity_type = 'company' AND entity_id = @id ORDER BY is_primary DESC", conn);
            cmd.Parameters.AddWithValue("id", id);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        return group;
    }
}
