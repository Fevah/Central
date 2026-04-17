using System.Text.Json;
using Central.Engine.Integration;
using Central.Persistence;

namespace Central.Api.Endpoints;

/// <summary>
/// Data import API — accepts JSON records and upserts into target table.
/// Used by: Import Wizard, external tools, CI/CD pipelines.
/// </summary>
public static class ImportEndpoints
{
    public static RouteGroupBuilder MapImportEndpoints(this RouteGroupBuilder group)
    {
        // Import records into a target table with field mapping
        group.MapPost("/", async (DbConnectionFactory db, JsonElement body) =>
        {
            var targetTable = body.GetProperty("target_table").GetString() ?? "";
            var upsertKey = body.TryGetProperty("upsert_key", out var uk) ? uk.GetString() ?? "id" : "id";

            if (!body.TryGetProperty("records", out var recordsEl) || recordsEl.ValueKind != JsonValueKind.Array)
                return Results.BadRequest("Missing 'records' array");

            var repo = new DbRepository(db.ConnectionString);
            int imported = 0, failed = 0;

            foreach (var record in recordsEl.EnumerateArray())
            {
                try
                {
                    var fields = new Dictionary<string, object?>();
                    foreach (var prop in record.EnumerateObject())
                    {
                        fields[prop.Name] = prop.Value.ValueKind switch
                        {
                            JsonValueKind.String => prop.Value.GetString(),
                            JsonValueKind.Number => prop.Value.TryGetInt64(out var l) ? l : prop.Value.GetDouble(),
                            JsonValueKind.True => true,
                            JsonValueKind.False => false,
                            JsonValueKind.Null => null,
                            _ => prop.Value.GetRawText()
                        };
                    }

                    await repo.UpsertSyncRecordAsync(targetTable, fields, upsertKey);
                    imported++;
                }
                catch { failed++; }
            }

            return Results.Ok(new { imported, failed, target_table = targetTable });
        });

        // List available tables for import target
        group.MapGet("/tables", async (DbConnectionFactory db) =>
        {
            var repo = new DbRepository(db.ConnectionString);
            return Results.Ok(await repo.GetTableListForSyncPublicAsync());
        });

        return group;
    }
}
