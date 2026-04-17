using Central.Persistence;

namespace Central.Api.Endpoints;

public static class DashboardEndpoints
{
    public static RouteGroupBuilder MapDashboardEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", async (DbConnectionFactory db) =>
        {
            var repo = new DbRepository(db.ConnectionString);
            var data = await repo.GetDashboardDataAsync();
            return Results.Ok(data);
        });

        return group;
    }
}
