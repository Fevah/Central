using Npgsql;
using Central.Api.Services;
using Central.Data;

namespace Central.Api.Endpoints;

public static class JobEndpoints
{
    public static RouteGroupBuilder MapJobEndpoints(this RouteGroupBuilder group)
    {
        // GET /api/jobs — list all schedules
        group.MapGet("/", async (DbConnectionFactory db) =>
        {
            await using var conn = new NpgsqlConnection(db.ConnectionString);
            await conn.OpenAsync();
            var list = new List<object>();
            await using var cmd = new NpgsqlCommand(
                "SELECT id, job_type, name, is_enabled, interval_minutes, last_run_at, next_run_at, created_by FROM job_schedules ORDER BY id", conn);
            await using var rdr = await cmd.ExecuteReaderAsync();
            while (await rdr.ReadAsync())
                list.Add(new
                {
                    id = rdr.GetInt32(0), job_type = rdr.GetString(1), name = rdr.GetString(2),
                    is_enabled = rdr.GetBoolean(3), interval_minutes = rdr.GetInt32(4),
                    last_run_at = rdr.IsDBNull(5) ? null : rdr.GetDateTime(5).ToString("o"),
                    next_run_at = rdr.IsDBNull(6) ? null : rdr.GetDateTime(6).ToString("o"),
                    created_by = rdr.IsDBNull(7) ? "" : rdr.GetString(7)
                });
            return Results.Ok(list);
        });

        // PUT /api/jobs/{id}/enable
        group.MapPut("/{id}/enable", async (int id, DbConnectionFactory db) =>
        {
            await using var conn = new NpgsqlConnection(db.ConnectionString);
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand(
                "UPDATE job_schedules SET is_enabled = true, next_run_at = now() WHERE id = @id", conn);
            cmd.Parameters.AddWithValue("id", id);
            await cmd.ExecuteNonQueryAsync();
            return Results.Ok(new { enabled = true });
        });

        // PUT /api/jobs/{id}/disable
        group.MapPut("/{id}/disable", async (int id, DbConnectionFactory db) =>
        {
            await using var conn = new NpgsqlConnection(db.ConnectionString);
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand(
                "UPDATE job_schedules SET is_enabled = false WHERE id = @id", conn);
            cmd.Parameters.AddWithValue("id", id);
            await cmd.ExecuteNonQueryAsync();
            return Results.Ok(new { enabled = false });
        });

        // PUT /api/jobs/{id}/interval — change interval
        group.MapPut("/{id}/interval", async (int id, IntervalRequest req, DbConnectionFactory db) =>
        {
            await using var conn = new NpgsqlConnection(db.ConnectionString);
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand(
                "UPDATE job_schedules SET interval_minutes = @min WHERE id = @id", conn);
            cmd.Parameters.AddWithValue("id", id);
            cmd.Parameters.AddWithValue("min", req.Minutes);
            await cmd.ExecuteNonQueryAsync();
            return Results.Ok(new { interval_minutes = req.Minutes });
        });

        // POST /api/jobs/{id}/run — trigger immediately
        group.MapPost("/{id}/run", async (int id, SshOperationsService ssh, DbConnectionFactory db, HttpContext ctx) =>
        {
            await using var conn = new NpgsqlConnection(db.ConnectionString);
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand("SELECT job_type FROM job_schedules WHERE id = @id", conn);
            cmd.Parameters.AddWithValue("id", id);
            var jobType = await cmd.ExecuteScalarAsync() as string;
            if (jobType == null) return Results.NotFound();

            var triggerBy = $"admin:{ctx.User?.Identity?.Name ?? "api"}";

            // Create history
            await using var histCmd = new NpgsqlCommand(
                "INSERT INTO job_history (schedule_id, job_type, triggered_by) VALUES (@sid, @type, @by) RETURNING id", conn);
            histCmd.Parameters.AddWithValue("sid", id);
            histCmd.Parameters.AddWithValue("type", jobType);
            histCmd.Parameters.AddWithValue("by", triggerBy);
            var historyId = (int)(await histCmd.ExecuteScalarAsync())!;

            // Run the job
            try
            {
                int total = 0, ok = 0;
                string summary;
                switch (jobType)
                {
                    case "ping_scan":
                        ok = await ssh.PingAllSwitchesAsync();
                        summary = $"{ok} reachable";
                        break;
                    default:
                        summary = "Manual trigger not implemented for this type";
                        break;
                }

                await using var doneCmd = new NpgsqlCommand(
                    "UPDATE job_history SET completed_at = now(), status = 'Success', result_summary = @s, items_succeeded = @ok WHERE id = @id", conn);
                doneCmd.Parameters.AddWithValue("id", historyId);
                doneCmd.Parameters.AddWithValue("s", summary);
                doneCmd.Parameters.AddWithValue("ok", ok);
                await doneCmd.ExecuteNonQueryAsync();

                return Results.Ok(new { history_id = historyId, status = "Success", summary });
            }
            catch (Exception ex)
            {
                await using var errCmd = new NpgsqlCommand(
                    "UPDATE job_history SET completed_at = now(), status = 'Failed', error_message = @err WHERE id = @id", conn);
                errCmd.Parameters.AddWithValue("id", historyId);
                errCmd.Parameters.AddWithValue("err", ex.Message);
                await errCmd.ExecuteNonQueryAsync();
                return Results.BadRequest(new { error = ex.Message });
            }
        });

        // GET /api/jobs/history — recent job executions
        group.MapGet("/history", async (DbConnectionFactory db) =>
        {
            await using var conn = new NpgsqlConnection(db.ConnectionString);
            await conn.OpenAsync();
            var list = new List<object>();
            await using var cmd = new NpgsqlCommand(
                "SELECT id, job_type, started_at, completed_at, status, result_summary, items_total, items_succeeded, items_failed, triggered_by FROM job_history ORDER BY started_at DESC LIMIT 50", conn);
            await using var rdr = await cmd.ExecuteReaderAsync();
            while (await rdr.ReadAsync())
                list.Add(new
                {
                    id = rdr.GetInt32(0), job_type = rdr.GetString(1),
                    started_at = rdr.GetDateTime(2).ToString("o"),
                    completed_at = rdr.IsDBNull(3) ? null : rdr.GetDateTime(3).ToString("o"),
                    status = rdr.GetString(4), result_summary = rdr.IsDBNull(5) ? "" : rdr.GetString(5),
                    items_total = rdr.GetInt32(6), items_succeeded = rdr.GetInt32(7), items_failed = rdr.GetInt32(8),
                    triggered_by = rdr.IsDBNull(9) ? "" : rdr.GetString(9)
                });
            return Results.Ok(list);
        });

        return group;
    }

    private record IntervalRequest(int Minutes);
}
