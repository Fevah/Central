using Npgsql;
using Central.Persistence;

namespace Central.Api.Endpoints;

public static class BackupEndpoints
{
    public static RouteGroupBuilder MapBackupEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/history", async (DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                "SELECT * FROM backup_history ORDER BY started_at DESC LIMIT 100", conn);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        group.MapPost("/run", async (DbConnectionFactory db, HttpContext ctx) =>
        {
            var dsn = db.ConnectionString;
            var svc = new BackupService(dsn);
            var username = ctx.User.Identity?.Name ?? "api";
            var outputDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "backups");
            Directory.CreateDirectory(outputDir);
            var outputPath = Path.Combine(outputDir, $"central_backup_{DateTime.UtcNow:yyyyMMdd_HHmmss}.dump");

            var record = await svc.BackupAsync(outputPath, "full", username);
            return Results.Ok(new { record.Id, record.FilePath, record.FileSizeBytes, record.Status });
        });

        group.MapGet("/tables", async (DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                "SELECT tablename FROM pg_tables WHERE schemaname='public' ORDER BY tablename", conn);
            var tables = new List<string>();
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync()) tables.Add(r.GetString(0));
            return Results.Ok(tables);
        });

        group.MapGet("/migrations", async (DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                "SELECT * FROM migration_history ORDER BY migration_name", conn);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        group.MapGet("/purge-counts", async (DbConnectionFactory db) =>
        {
            var repo = new DbRepository(db.ConnectionString);
            var counts = await repo.GetSoftDeletedCountsAsync();
            return Results.Ok(counts);
        });

        group.MapPost("/purge/{table}", async (string table, DbConnectionFactory db) =>
        {
            var repo = new DbRepository(db.ConnectionString);
            var purged = await repo.PurgeSoftDeletedAsync(table);
            return Results.Ok(new { table, purged });
        });

        return group;
    }
}
