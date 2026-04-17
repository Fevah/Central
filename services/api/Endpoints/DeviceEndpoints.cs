using Central.Persistence;

namespace Central.Api.Endpoints;

public static class DeviceEndpoints
{
    // Whitelist of columns that clients may write to via PUT/POST.
    private static readonly HashSet<string> WritableColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        "switch_name", "site", "building", "floor", "rack", "model",
        "serial_number", "management_ip", "uplink_switch", "uplink_port",
        "notes", "enabled", "raw_data",
        "device_type", "primary_ip", "status", "hostname", "location",
        "department", "contact", "asset_tag", "purchase_date", "warranty_end"
    };

    // Columns allowed for filtering/sorting
    private static readonly HashSet<string> FilterableColumns = new(StringComparer.OrdinalIgnoreCase)
    {
        "switch_name", "site", "building", "floor", "rack", "model",
        "device_type", "primary_ip", "status", "hostname", "location",
        "department", "enabled"
    };

    // Columns searched by free-text search
    private static readonly string[] SearchableColumns =
        ["switch_name", "building", "model", "device_type", "primary_ip", "hostname", "notes"];

    public static RouteGroupBuilder MapDeviceEndpoints(this RouteGroupBuilder group)
    {
        // GET /api/devices?offset=0&limit=100&sort=switch_name&order=asc&search=core&filter=building:MEP-91
        group.MapGet("/", async (int? offset, int? limit, string? sort, string? order,
            string? search, string? filter, DbConnectionFactory db) =>
        {
            var q = new PaginatedQuery(offset, limit, sort, order, search, filter);

            await using var conn = await db.OpenConnectionAsync();

            // Build WHERE conditions
            var conditions = new List<string> { "is_deleted IS NOT TRUE" };
            var allParams = new List<(string Name, object Value)>();

            var (filterWhere, filterParams) = PaginationHelpers.BuildFilterClause(filter, FilterableColumns);
            if (!string.IsNullOrEmpty(filterWhere)) { conditions.Add(filterWhere); allParams.AddRange(filterParams); }

            var (searchWhere, searchParams) = PaginationHelpers.BuildSearchClause(search, SearchableColumns);
            if (!string.IsNullOrEmpty(searchWhere)) { conditions.Add(searchWhere); allParams.AddRange(searchParams); }

            var where = string.Join(" AND ", conditions);
            var sortClause = PaginationHelpers.BuildSortClause(q, "switch_name ASC", FilterableColumns);

            // Count query
            await using var countCmd = conn.CreateCommand();
            countCmd.CommandText = $"SELECT COUNT(*) FROM switch_guide WHERE {where}";
            foreach (var (name, value) in allParams) countCmd.Parameters.AddWithValue(name, value);
            var total = Convert.ToInt32(await countCmd.ExecuteScalarAsync());

            // Data query
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $@"
                SELECT id, switch_name, building, device_type, primary_ip, status
                FROM switch_guide
                WHERE {where}
                ORDER BY {sortClause}
                LIMIT @limit OFFSET @offset";
            foreach (var (name, value) in allParams) cmd.Parameters.AddWithValue(name, value);
            cmd.Parameters.AddWithValue("limit", q.EffectiveLimit);
            cmd.Parameters.AddWithValue("offset", q.EffectiveOffset);

            await using var reader = await cmd.ExecuteReaderAsync();
            var result = await PaginationHelpers.ExecutePaginatedAsync(reader, total, q);
            return Results.Ok(result);
        });

        // GET /api/devices/{id}
        group.MapGet("/{id:int}", async (int id, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM switch_guide WHERE id = @id AND is_deleted IS NOT TRUE";
            cmd.Parameters.AddWithValue("id", id);
            await using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                return ApiProblem.NotFound($"Device {id} not found");
            var row = new Dictionary<string, object?>();
            for (int i = 0; i < reader.FieldCount; i++)
                row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            return Results.Ok(row);
        });

        // PUT /api/devices/{id}
        group.MapPut("/{id:int}", async (int id, Dictionary<string, object?> body, DbConnectionFactory db) =>
        {
            if (body.Count == 0)
                return ApiProblem.ValidationError("No fields to update.");

            var invalid = EndpointHelpers.ValidateColumns(body.Keys, WritableColumns);
            if (invalid is not null) return invalid;

            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = conn.CreateCommand();

            var setClauses = new List<string>();
            int paramIndex = 0;
            foreach (var kvp in body)
            {
                var paramName = $"p{paramIndex++}";
                setClauses.Add($"{kvp.Key} = @{paramName}");
                cmd.Parameters.AddWithValue(paramName, kvp.Value ?? DBNull.Value);
            }

            cmd.CommandText = $"UPDATE switch_guide SET {string.Join(", ", setClauses)} WHERE id = @id AND is_deleted IS NOT TRUE RETURNING id";
            cmd.Parameters.AddWithValue("id", id);

            var result = await cmd.ExecuteScalarAsync();
            return result is null ? ApiProblem.NotFound($"Device {id} not found") : Results.Ok(new { id });
        });

        // POST /api/devices
        group.MapPost("/", async (Dictionary<string, object?> body, DbConnectionFactory db) =>
        {
            if (body.Count == 0)
                return ApiProblem.ValidationError("No fields provided.");

            var invalid = EndpointHelpers.ValidateColumns(body.Keys, WritableColumns);
            if (invalid is not null) return invalid;

            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = conn.CreateCommand();

            var columns = new List<string>();
            var paramNames = new List<string>();
            int paramIndex = 0;
            foreach (var kvp in body)
            {
                var paramName = $"p{paramIndex++}";
                columns.Add(kvp.Key);
                paramNames.Add($"@{paramName}");
                cmd.Parameters.AddWithValue(paramName, kvp.Value ?? DBNull.Value);
            }

            cmd.CommandText = $"INSERT INTO switch_guide ({string.Join(", ", columns)}) VALUES ({string.Join(", ", paramNames)}) RETURNING id";

            var newId = await cmd.ExecuteScalarAsync();
            return Results.Created($"/{newId}", new { id = newId });
        });

        // DELETE /api/devices/{id}
        group.MapDelete("/{id:int}", async (int id, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE switch_guide SET is_deleted = true WHERE id = @id AND is_deleted IS NOT TRUE RETURNING id";
            cmd.Parameters.AddWithValue("id", id);
            var result = await cmd.ExecuteScalarAsync();
            return result is null ? ApiProblem.NotFound($"Device {id} not found") : Results.NoContent();
        });

        // POST /api/devices/batch — bulk update
        group.MapPost("/batch", async (List<Dictionary<string, object?>> items, DbConnectionFactory db) =>
        {
            if (items.Count == 0)
                return ApiProblem.ValidationError("No items provided.");
            if (items.Count > 500)
                return ApiProblem.ValidationError("Batch size limited to 500 items.");

            var results = new List<BulkItemResult>();
            await using var conn = await db.OpenConnectionAsync();
            await using var tx = await conn.BeginTransactionAsync();

            try
            {
                foreach (var item in items)
                {
                    try
                    {
                        if (!item.TryGetValue("id", out var idObj) || idObj is null)
                        {
                            results.Add(new BulkItemResult(null, false, "Missing 'id' field"));
                            continue;
                        }

                        var id = Convert.ToInt32(idObj);
                        var fields = item.Where(k => k.Key != "id").ToDictionary(k => k.Key, k => k.Value);

                        var colInvalid = EndpointHelpers.ValidateColumns(fields.Keys, WritableColumns);
                        if (colInvalid is not null)
                        {
                            results.Add(new BulkItemResult(id, false, "Invalid column name"));
                            continue;
                        }

                        await using var cmd = conn.CreateCommand();
                        cmd.Transaction = tx;
                        var setClauses = new List<string>();
                        int pi = 0;
                        foreach (var kvp in fields)
                        {
                            var pn = $"p{pi++}";
                            setClauses.Add($"{kvp.Key} = @{pn}");
                            cmd.Parameters.AddWithValue(pn, kvp.Value ?? DBNull.Value);
                        }
                        cmd.CommandText = $"UPDATE switch_guide SET {string.Join(", ", setClauses)} WHERE id = @id AND is_deleted IS NOT TRUE RETURNING id";
                        cmd.Parameters.AddWithValue("id", id);
                        var res = await cmd.ExecuteScalarAsync();
                        results.Add(new BulkItemResult(id, res is not null, res is null ? "Not found" : null));
                    }
                    catch (Exception ex)
                    {
                        results.Add(new BulkItemResult(item.GetValueOrDefault("id"), false, ex.Message));
                    }
                }

                await tx.CommitAsync();
            }
            catch
            {
                await tx.RollbackAsync();
                return ApiProblem.ServerError("Batch operation failed — all changes rolled back.");
            }

            return Results.Ok(new BulkResult(
                results.Count(r => r.Success),
                results.Count(r => !r.Success),
                results));
        });

        // DELETE /api/devices/batch — bulk delete
        group.MapDelete("/batch", async (List<int> ids, DbConnectionFactory db) =>
        {
            if (ids.Count == 0)
                return ApiProblem.ValidationError("No IDs provided.");
            if (ids.Count > 500)
                return ApiProblem.ValidationError("Batch size limited to 500 items.");

            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE switch_guide SET is_deleted = true WHERE id = ANY(@ids) AND is_deleted IS NOT TRUE RETURNING id";
            cmd.Parameters.AddWithValue("ids", ids.ToArray());
            await using var reader = await cmd.ExecuteReaderAsync();
            var deleted = new List<int>();
            while (await reader.ReadAsync()) deleted.Add(reader.GetInt32(0));
            return Results.Ok(new { deleted_count = deleted.Count, deleted_ids = deleted });
        });

        return group;
    }
}
