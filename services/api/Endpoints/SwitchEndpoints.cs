using Central.Persistence;

namespace Central.Api.Endpoints;

public static class SwitchEndpoints
{
    private static readonly HashSet<string> FilterableColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        "hostname", "site", "role", "management_ip", "last_ping_ok"
    };

    private static readonly string[] SearchableColumns =
        ["hostname", "site", "role", "management_ip"];

    public static RouteGroupBuilder MapSwitchEndpoints(this RouteGroupBuilder group)
    {
        // GET /api/switches?offset=0&limit=100&sort=hostname&search=core&filter=site:MEP-91
        group.MapGet("/", async (int? offset, int? limit, string? sort, string? order,
            string? search, string? filter, DbConnectionFactory db) =>
        {
            var q = new PaginatedQuery(offset, limit, sort, order, search, filter);

            await using var conn = await db.OpenConnectionAsync();

            var conditions = new List<string>();
            var allParams = new List<(string Name, object Value)>();

            var (filterWhere, filterParams) = PaginationHelpers.BuildFilterClause(filter, FilterableColumns);
            if (!string.IsNullOrEmpty(filterWhere)) { conditions.Add(filterWhere); allParams.AddRange(filterParams); }

            var (searchWhere, searchParams) = PaginationHelpers.BuildSearchClause(search, SearchableColumns);
            if (!string.IsNullOrEmpty(searchWhere)) { conditions.Add(searchWhere); allParams.AddRange(searchParams); }

            var where = conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : "";
            var sortClause = PaginationHelpers.BuildSortClause(q, "hostname ASC", FilterableColumns);

            // Count
            await using var countCmd = conn.CreateCommand();
            countCmd.CommandText = $"SELECT COUNT(*) FROM switches {where}";
            foreach (var (name, value) in allParams) countCmd.Parameters.AddWithValue(name, value);
            var total = Convert.ToInt32(await countCmd.ExecuteScalarAsync());

            // Data
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $@"
                SELECT id, hostname, site, role, management_ip, last_ping_ok, last_ping_ms
                FROM switches
                {where}
                ORDER BY {sortClause}
                LIMIT @limit OFFSET @offset";
            foreach (var (name, value) in allParams) cmd.Parameters.AddWithValue(name, value);
            cmd.Parameters.AddWithValue("limit", q.EffectiveLimit);
            cmd.Parameters.AddWithValue("offset", q.EffectiveOffset);

            await using var reader = await cmd.ExecuteReaderAsync();
            var result = await PaginationHelpers.ExecutePaginatedAsync(reader, total, q);
            return Results.Ok(result);
        });

        group.MapGet("/{id:int}", async (int id, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM switches WHERE id = @id";
            cmd.Parameters.AddWithValue("id", id);
            await using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                return ApiProblem.NotFound($"Switch {id} not found");
            var row = new Dictionary<string, object?>();
            for (int i = 0; i < reader.FieldCount; i++)
                row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            return Results.Ok(row);
        });

        group.MapGet("/{id:int}/interfaces", async (int id, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM switch_interfaces WHERE switch_id = @id ORDER BY interface_name";
            cmd.Parameters.AddWithValue("id", id);
            await using var reader = await cmd.ExecuteReaderAsync();
            var results = await EndpointHelpers.ReadRowsAsync(reader);
            return Results.Ok(results);
        });

        group.MapGet("/{id:int}/config-versions", async (int id, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM config_versions WHERE switch_id = @id ORDER BY created_at DESC";
            cmd.Parameters.AddWithValue("id", id);
            await using var reader = await cmd.ExecuteReaderAsync();
            var results = await EndpointHelpers.ReadRowsAsync(reader);
            return Results.Ok(results);
        });

        return group;
    }
}
