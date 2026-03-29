using Central.Data;

namespace Central.Api.Endpoints;

public static class VlanEndpoints
{
    public static RouteGroupBuilder MapVlanEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", async (DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM vlan_inventory ORDER BY vlan_id";
            await using var reader = await cmd.ExecuteReaderAsync();
            var results = await EndpointHelpers.ReadRowsAsync(reader);
            return Results.Ok(results);
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

            cmd.CommandText = $"UPDATE vlan_inventory SET {string.Join(", ", setClauses)} WHERE id = @id RETURNING id";
            cmd.Parameters.AddWithValue("id", id);

            var result = await cmd.ExecuteScalarAsync();
            return result is null ? Results.NotFound() : Results.Ok(new { id });
        });

        return group;
    }
}
