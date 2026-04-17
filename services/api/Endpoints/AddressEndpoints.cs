using System.Text.Json;
using Npgsql;
using Central.Persistence;

namespace Central.Api.Endpoints;

/// <summary>Phase 4: Unified polymorphic address CRUD.</summary>
public static class AddressEndpoints
{
    public static RouteGroupBuilder MapAddressEndpoints(this RouteGroupBuilder group)
    {
        // GET /api/addresses?entity_type=company&entity_id=42
        group.MapGet("/", async (string entity_type, int entity_id, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                "SELECT * FROM addresses WHERE entity_type = @et AND entity_id = @eid ORDER BY is_primary DESC, address_type", conn);
            cmd.Parameters.AddWithValue("et", entity_type);
            cmd.Parameters.AddWithValue("eid", entity_id);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        group.MapPost("/", async (JsonElement body, DbConnectionFactory db) =>
        {
            var entityType = body.GetProperty("entity_type").GetString() ?? "";
            var entityId = body.GetProperty("entity_id").GetInt32();
            if (string.IsNullOrEmpty(entityType)) return ApiProblem.ValidationError("entity_type is required.");

            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                @"INSERT INTO addresses (entity_type, entity_id, address_type, label, line1, line2, line3,
                  city, state_region, postal_code, country_code, latitude, longitude, is_primary)
                  VALUES (@et, @eid, @at, @label, @l1, @l2, @l3, @city, @sr, @pc, @cc, @lat, @lon, @prim)
                  RETURNING id", conn);
            cmd.Parameters.AddWithValue("et", entityType);
            cmd.Parameters.AddWithValue("eid", entityId);
            cmd.Parameters.AddWithValue("at", body.TryGetProperty("address_type", out var at) ? at.GetString() ?? "hq" : "hq");
            cmd.Parameters.AddWithValue("label", body.TryGetProperty("label", out var lb) ? lb.GetString() ?? "" : "");
            cmd.Parameters.AddWithValue("l1", body.TryGetProperty("line1", out var l1) ? l1.GetString() ?? "" : "");
            cmd.Parameters.AddWithValue("l2", body.TryGetProperty("line2", out var l2) ? (object)(l2.GetString() ?? "") : DBNull.Value);
            cmd.Parameters.AddWithValue("l3", body.TryGetProperty("line3", out var l3) ? (object)(l3.GetString() ?? "") : DBNull.Value);
            cmd.Parameters.AddWithValue("city", body.TryGetProperty("city", out var ct) ? ct.GetString() ?? "" : "");
            cmd.Parameters.AddWithValue("sr", body.TryGetProperty("state_region", out var sr) ? (object)(sr.GetString() ?? "") : DBNull.Value);
            cmd.Parameters.AddWithValue("pc", body.TryGetProperty("postal_code", out var pc) ? (object)(pc.GetString() ?? "") : DBNull.Value);
            cmd.Parameters.AddWithValue("cc", body.TryGetProperty("country_code", out var cc) ? cc.GetString() ?? "GB" : "GB");
            cmd.Parameters.AddWithValue("lat", body.TryGetProperty("latitude", out var lat) && lat.ValueKind == JsonValueKind.Number ? lat.GetDecimal() : DBNull.Value);
            cmd.Parameters.AddWithValue("lon", body.TryGetProperty("longitude", out var lon) && lon.ValueKind == JsonValueKind.Number ? lon.GetDecimal() : DBNull.Value);
            cmd.Parameters.AddWithValue("prim", body.TryGetProperty("is_primary", out var prim) && prim.GetBoolean());
            var id = (int)(await cmd.ExecuteScalarAsync())!;
            return Results.Created($"/api/addresses/{id}", new { id });
        });

        group.MapPut("/{id:int}", async (int id, JsonElement body, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                @"UPDATE addresses SET address_type = @at, label = @label, line1 = @l1, line2 = @l2, line3 = @l3,
                  city = @city, state_region = @sr, postal_code = @pc, country_code = @cc,
                  latitude = @lat, longitude = @lon, is_primary = @prim, updated_at = NOW()
                  WHERE id = @id RETURNING id", conn);
            cmd.Parameters.AddWithValue("id", id);
            cmd.Parameters.AddWithValue("at", body.TryGetProperty("address_type", out var at) ? at.GetString() ?? "hq" : "hq");
            cmd.Parameters.AddWithValue("label", body.TryGetProperty("label", out var lb) ? lb.GetString() ?? "" : "");
            cmd.Parameters.AddWithValue("l1", body.TryGetProperty("line1", out var l1) ? l1.GetString() ?? "" : "");
            cmd.Parameters.AddWithValue("l2", body.TryGetProperty("line2", out var l2) ? (object)(l2.GetString() ?? "") : DBNull.Value);
            cmd.Parameters.AddWithValue("l3", body.TryGetProperty("line3", out var l3) ? (object)(l3.GetString() ?? "") : DBNull.Value);
            cmd.Parameters.AddWithValue("city", body.TryGetProperty("city", out var ct) ? ct.GetString() ?? "" : "");
            cmd.Parameters.AddWithValue("sr", body.TryGetProperty("state_region", out var sr) ? (object)(sr.GetString() ?? "") : DBNull.Value);
            cmd.Parameters.AddWithValue("pc", body.TryGetProperty("postal_code", out var pc) ? (object)(pc.GetString() ?? "") : DBNull.Value);
            cmd.Parameters.AddWithValue("cc", body.TryGetProperty("country_code", out var cc) ? cc.GetString() ?? "GB" : "GB");
            cmd.Parameters.AddWithValue("lat", body.TryGetProperty("latitude", out var lat) && lat.ValueKind == JsonValueKind.Number ? lat.GetDecimal() : DBNull.Value);
            cmd.Parameters.AddWithValue("lon", body.TryGetProperty("longitude", out var lon) && lon.ValueKind == JsonValueKind.Number ? lon.GetDecimal() : DBNull.Value);
            cmd.Parameters.AddWithValue("prim", body.TryGetProperty("is_primary", out var prim) && prim.GetBoolean());
            var result = await cmd.ExecuteScalarAsync();
            return result is null ? ApiProblem.NotFound($"Address {id} not found") : Results.Ok(new { id });
        });

        group.MapDelete("/{id:int}", async (int id, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand("DELETE FROM addresses WHERE id = @id RETURNING id", conn);
            cmd.Parameters.AddWithValue("id", id);
            var result = await cmd.ExecuteScalarAsync();
            return result is null ? ApiProblem.NotFound($"Address {id} not found") : Results.NoContent();
        });

        return group;
    }
}
