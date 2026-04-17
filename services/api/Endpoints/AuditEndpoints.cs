using Central.Data;

namespace Central.Api.Endpoints;

public static class AuditEndpoints
{
    public static RouteGroupBuilder MapAuditEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", async (DbConnectionFactory db, int? limit, string? entityType, string? username) =>
        {
            var repo = new DbRepository(db.ConnectionString);
            var entries = await repo.GetAuditLogAsync(limit ?? 200, entityType, username);
            return Results.Ok(entries);
        });

        return group;
    }
}
