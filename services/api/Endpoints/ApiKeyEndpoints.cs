using System.Text.Json;
using Central.Persistence;

namespace Central.Api.Endpoints;

public static class ApiKeyEndpoints
{
    public static RouteGroupBuilder MapApiKeyEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", async (DbConnectionFactory db) =>
        {
            var repo = new DbRepository(db.ConnectionString);
            return Results.Ok(await repo.GetApiKeysAsync());
        });

        group.MapPost("/generate", async (DbConnectionFactory db, JsonElement body) =>
        {
            var name = body.GetProperty("name").GetString() ?? "";
            var role = body.TryGetProperty("role", out var r) ? r.GetString() ?? "Viewer" : "Viewer";
            var repo = new DbRepository(db.ConnectionString);
            var rawKey = await repo.CreateApiKeyAsync(name, role, 0); // 0 = API-created
            return Results.Ok(new { key = rawKey, name, role });
        });

        group.MapDelete("/{id:int}", async (int id, DbConnectionFactory db) =>
        {
            var repo = new DbRepository(db.ConnectionString);
            await repo.DeleteApiKeyAsync(id);
            return Results.Ok();
        });

        group.MapPost("/{id:int}/revoke", async (int id, DbConnectionFactory db) =>
        {
            var repo = new DbRepository(db.ConnectionString);
            await repo.RevokeApiKeyAsync(id);
            return Results.Ok();
        });

        return group;
    }
}
