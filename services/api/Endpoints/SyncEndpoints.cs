using System.Text.Json;
using Npgsql;
using Central.Data;

namespace Central.Api.Endpoints;

public static class SyncEndpoints
{
    public static RouteGroupBuilder MapSyncEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/configs", async (DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                "SELECT id, name, agent_type, is_enabled, direction, schedule_cron, interval_minutes, max_concurrent, config_json::text, last_sync_at, last_sync_status, last_error FROM sync_configs ORDER BY name", conn);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        group.MapPut("/configs", async (DbConnectionFactory db, JsonElement body) =>
        {
            var repo = new DbRepository(db.ConnectionString);
            var config = new Central.Core.Integration.SyncConfig
            {
                Id = body.TryGetProperty("id", out var idp) ? idp.GetInt32() : 0,
                Name = body.GetProperty("name").GetString() ?? "",
                AgentType = body.GetProperty("agent_type").GetString() ?? "",
                IsEnabled = !body.TryGetProperty("is_enabled", out var en) || en.GetBoolean(),
                Direction = body.TryGetProperty("direction", out var dir) ? dir.GetString() ?? "pull" : "pull",
                IntervalMinutes = body.TryGetProperty("interval_minutes", out var im) ? im.GetInt32() : 60,
                MaxConcurrent = body.TryGetProperty("max_concurrent", out var mc) ? mc.GetInt32() : 1,
                ConfigJson = body.TryGetProperty("config_json", out var cfg) ? cfg.GetRawText() : "{}"
            };
            await repo.UpsertSyncConfigAsync(config);
            return Results.Ok(new { config.Id });
        });

        group.MapGet("/configs/{id:int}/entity-maps", async (int id, DbConnectionFactory db) =>
        {
            var repo = new DbRepository(db.ConnectionString);
            var maps = await repo.GetSyncEntityMapsAsync(id);
            return Results.Ok(maps);
        });

        group.MapGet("/configs/{id:int}/log", async (int id, DbConnectionFactory db, int? limit) =>
        {
            var repo = new DbRepository(db.ConnectionString);
            var log = await repo.GetSyncLogAsync(id, limit ?? 50);
            return Results.Ok(log);
        });

        group.MapPost("/configs/{id:int}/run", async (int id, DbConnectionFactory db) =>
        {
            var repo = new DbRepository(db.ConnectionString);
            var configs = await repo.GetSyncConfigsAsync();
            var config = configs.FirstOrDefault(c => c.Id == id);
            if (config == null) return Results.NotFound("Sync config not found");

            var entityMaps = await repo.GetSyncEntityMapsAsync(id);
            var allFieldMaps = new List<Central.Core.Integration.SyncFieldMap>();
            foreach (var em in entityMaps)
                allFieldMaps.AddRange(await repo.GetSyncFieldMapsAsync(em.Id));

            var engine = Central.Core.Integration.SyncEngine.Instance;
            engine.SetLogCallback(async (cid, status, entity, read, created, updated, failed, error) =>
                await repo.InsertSyncLogAsync(cid, status, entity, read, created, updated, failed, error));

            var result = await engine.ExecuteSyncAsync(config, entityMaps, allFieldMaps,
                async (table, fields, key) => await repo.UpsertSyncRecordAsync(table, fields, key));

            await repo.UpdateSyncStatusAsync(id, result.Status, result.ErrorMessage);
            return Results.Ok(result);
        });

        group.MapGet("/agent-types", () =>
        {
            var engine = Central.Core.Integration.SyncEngine.Instance;
            return Results.Ok(engine.GetAgentTypes());
        });

        group.MapGet("/converter-types", () =>
        {
            var engine = Central.Core.Integration.SyncEngine.Instance;
            return Results.Ok(engine.GetConverterTypes());
        });

        return group;
    }
}
