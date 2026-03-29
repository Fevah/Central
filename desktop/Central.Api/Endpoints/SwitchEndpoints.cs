using Central.Data;

namespace Central.Api.Endpoints;

public static class SwitchEndpoints
{
    public static RouteGroupBuilder MapSwitchEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", async (DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT id, hostname, site, role, management_ip, last_ping_ok, last_ping_ms
                FROM switches
                ORDER BY hostname";
            await using var reader = await cmd.ExecuteReaderAsync();
            var results = await EndpointHelpers.ReadRowsAsync(reader);
            return Results.Ok(results);
        });

        group.MapGet("/{id:int}", async (int id, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM switches WHERE id = @id";
            cmd.Parameters.AddWithValue("id", id);
            await using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
                return Results.NotFound();
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
