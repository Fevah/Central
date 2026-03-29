using Central.Data;

namespace Central.Api.Endpoints;

public static class DeviceEndpoints
{
    public static RouteGroupBuilder MapDeviceEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", async (DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT id, switch_name, building, device_type, primary_ip, status
                FROM switch_guide
                WHERE is_deleted IS NOT TRUE
                ORDER BY switch_name";
            await using var reader = await cmd.ExecuteReaderAsync();
            var results = await EndpointHelpers.ReadRowsAsync(reader);
            return Results.Ok(results);
        });

        group.MapGet("/{id:int}", async (int id, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM switch_guide WHERE id = @id AND is_deleted IS NOT TRUE";
            cmd.Parameters.AddWithValue("id", id);
            await using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                return Results.NotFound();
            var row = new Dictionary<string, object?>();
            for (int i = 0; i < reader.FieldCount; i++)
                row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            return Results.Ok(row);
        });

        group.MapPut("/{id:int}", async (int id, Dictionary<string, object?> body, DbConnectionFactory db) =>
        {
            if (body.Count == 0)
                return Results.BadRequest("No fields to update.");

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
            return result is null ? Results.NotFound() : Results.Ok(new { id });
        });

        group.MapPost("/", async (Dictionary<string, object?> body, DbConnectionFactory db) =>
        {
            if (body.Count == 0)
                return Results.BadRequest("No fields provided.");

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

        group.MapDelete("/{id:int}", async (int id, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE switch_guide SET is_deleted = true WHERE id = @id AND is_deleted IS NOT TRUE RETURNING id";
            cmd.Parameters.AddWithValue("id", id);
            var result = await cmd.ExecuteScalarAsync();
            return result is null ? Results.NotFound() : Results.NoContent();
        });

        return group;
    }
}
