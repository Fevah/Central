using System.Text.Json;
using Npgsql;
using Central.Persistence;

namespace Central.Api.Endpoints;

public static class AppointmentEndpoints
{
    public static RouteGroupBuilder MapAppointmentEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", async (DbConnectionFactory db, DateTime? start, DateTime? end) =>
        {
            var s = start ?? DateTime.UtcNow.AddMonths(-1);
            var e = end ?? DateTime.UtcNow.AddMonths(2);
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                "SELECT * FROM appointments WHERE start_time < @e AND end_time > @s ORDER BY start_time", conn);
            cmd.Parameters.AddWithValue("s", s); cmd.Parameters.AddWithValue("e", e);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        group.MapPut("/", async (DbConnectionFactory db, JsonElement body) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            var id = body.TryGetProperty("id", out var idp) ? idp.GetInt32() : 0;
            var subject = body.GetProperty("subject").GetString() ?? "";
            var startTime = body.GetProperty("start_time").GetDateTime();
            var endTime = body.GetProperty("end_time").GetDateTime();
            var description = body.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "";
            var location = body.TryGetProperty("location", out var l) ? l.GetString() ?? "" : "";
            var allDay = body.TryGetProperty("all_day", out var ad) && ad.GetBoolean();
            var resourceId = body.TryGetProperty("resource_id", out var rid) && rid.ValueKind != JsonValueKind.Null ? rid.GetInt32() : (int?)null;

            await using var cmd = new NpgsqlCommand(id > 0
                ? "UPDATE appointments SET subject=@sub, description=@desc, start_time=@s, end_time=@e, all_day=@ad, location=@loc, resource_id=@rid, updated_at=NOW() WHERE id=@id"
                : "INSERT INTO appointments (subject, description, start_time, end_time, all_day, location, resource_id) VALUES (@sub, @desc, @s, @e, @ad, @loc, @rid) RETURNING id", conn);
            if (id > 0) cmd.Parameters.AddWithValue("id", id);
            cmd.Parameters.AddWithValue("sub", subject); cmd.Parameters.AddWithValue("desc", description);
            cmd.Parameters.AddWithValue("s", startTime); cmd.Parameters.AddWithValue("e", endTime);
            cmd.Parameters.AddWithValue("ad", allDay); cmd.Parameters.AddWithValue("loc", location);
            cmd.Parameters.AddWithValue("rid", (object?)resourceId ?? DBNull.Value);
            if (id == 0) id = (int)(await cmd.ExecuteScalarAsync())!;
            else await cmd.ExecuteNonQueryAsync();
            return Results.Ok(new { id });
        });

        group.MapDelete("/{id:int}", async (int id, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand("DELETE FROM appointments WHERE id=@id", conn);
            cmd.Parameters.AddWithValue("id", id);
            await cmd.ExecuteNonQueryAsync();
            return Results.Ok();
        });

        group.MapGet("/resources", async (DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand("SELECT * FROM appointment_resources ORDER BY display_name", conn);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        return group;
    }
}
