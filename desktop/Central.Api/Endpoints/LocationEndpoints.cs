using System.Text.Json;
using Npgsql;
using Central.Data;

namespace Central.Api.Endpoints;

public static class LocationEndpoints
{
    public static RouteGroupBuilder MapLocationEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/countries", async (DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand("SELECT * FROM countries ORDER BY sort_order, name", conn);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        group.MapPut("/countries", async (DbConnectionFactory db, JsonElement body) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            var id = body.TryGetProperty("id", out var idp) ? idp.GetInt32() : 0;
            var code = body.GetProperty("code").GetString() ?? "";
            var name = body.GetProperty("name").GetString() ?? "";
            var sortOrder = body.TryGetProperty("sort_order", out var so) ? so.GetInt32() : 0;
            await using var cmd = new NpgsqlCommand(id > 0
                ? "UPDATE countries SET code=@c, name=@n, sort_order=@s WHERE id=@id"
                : "INSERT INTO countries (code, name, sort_order) VALUES (@c, @n, @s) RETURNING id", conn);
            if (id > 0) cmd.Parameters.AddWithValue("id", id);
            cmd.Parameters.AddWithValue("c", code); cmd.Parameters.AddWithValue("n", name); cmd.Parameters.AddWithValue("s", sortOrder);
            if (id == 0) id = (int)(await cmd.ExecuteScalarAsync())!; else await cmd.ExecuteNonQueryAsync();
            return Results.Ok(new { id });
        });

        group.MapDelete("/countries/{id:int}", async (int id, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand("DELETE FROM countries WHERE id=@id", conn);
            cmd.Parameters.AddWithValue("id", id); await cmd.ExecuteNonQueryAsync();
            return Results.Ok();
        });

        group.MapGet("/regions", async (DbConnectionFactory db, int? countryId) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            var where = countryId.HasValue ? "WHERE r.country_id=@cid" : "";
            await using var cmd = new NpgsqlCommand($"SELECT r.*, c.name as country_name FROM regions r JOIN countries c ON c.id=r.country_id {where} ORDER BY c.name, r.sort_order", conn);
            if (countryId.HasValue) cmd.Parameters.AddWithValue("cid", countryId.Value);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        group.MapPut("/regions", async (DbConnectionFactory db, JsonElement body) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            var id = body.TryGetProperty("id", out var idp) ? idp.GetInt32() : 0;
            var countryId = body.GetProperty("country_id").GetInt32();
            var code = body.GetProperty("code").GetString() ?? "";
            var name = body.GetProperty("name").GetString() ?? "";
            var sortOrder = body.TryGetProperty("sort_order", out var so) ? so.GetInt32() : 0;
            await using var cmd = new NpgsqlCommand(id > 0
                ? "UPDATE regions SET country_id=@cid, code=@c, name=@n, sort_order=@s WHERE id=@id"
                : "INSERT INTO regions (country_id, code, name, sort_order) VALUES (@cid, @c, @n, @s) RETURNING id", conn);
            if (id > 0) cmd.Parameters.AddWithValue("id", id);
            cmd.Parameters.AddWithValue("cid", countryId); cmd.Parameters.AddWithValue("c", code);
            cmd.Parameters.AddWithValue("n", name); cmd.Parameters.AddWithValue("s", sortOrder);
            if (id == 0) id = (int)(await cmd.ExecuteScalarAsync())!; else await cmd.ExecuteNonQueryAsync();
            return Results.Ok(new { id });
        });

        group.MapGet("/references", async (DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand("SELECT * FROM reference_config ORDER BY entity_type", conn);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        group.MapPost("/references/next/{entityType}", async (string entityType, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand("SELECT next_reference(@t)", conn);
            cmd.Parameters.AddWithValue("t", entityType);
            var result = (string)(await cmd.ExecuteScalarAsync())!;
            return Results.Ok(new { reference = result });
        });

        return group;
    }
}
