using Central.Persistence;

namespace Central.Api.Endpoints;

public static class BgpEndpoints
{
    public static RouteGroupBuilder MapBgpEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", async (DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM bgp_config ORDER BY id";
            await using var reader = await cmd.ExecuteReaderAsync();
            var results = await EndpointHelpers.ReadRowsAsync(reader);
            return Results.Ok(results);
        });

        group.MapGet("/{id:int}/neighbors", async (int id, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM bgp_neighbors WHERE bgp_id = @id ORDER BY id";
            cmd.Parameters.AddWithValue("id", id);
            await using var reader = await cmd.ExecuteReaderAsync();
            var results = await EndpointHelpers.ReadRowsAsync(reader);
            return Results.Ok(results);
        });

        group.MapGet("/{id:int}/networks", async (int id, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM bgp_networks WHERE bgp_id = @id ORDER BY id";
            cmd.Parameters.AddWithValue("id", id);
            await using var reader = await cmd.ExecuteReaderAsync();
            var results = await EndpointHelpers.ReadRowsAsync(reader);
            return Results.Ok(results);
        });

        return group;
    }
}
