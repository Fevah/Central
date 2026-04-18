using Central.Api.Endpoints;
using Central.Engine.Auth;
using Central.Engine.Net.Servers;
using Central.Persistence;
using Central.Persistence.Net;
using Central.Tenancy;

namespace Central.Api.Endpoints.Net;

/// <summary>
/// Phase 6c — REST surface for the server catalog. Three families
/// under <c>/api/net</c>:
/// <list type="bullet">
///   <item>server_profile   — <c>/server-profiles</c>, full CRUD</item>
///   <item>server           — <c>/servers</c>, full CRUD + <c>/ping</c>
///                            fast-path probe update</item>
///   <item>server_nic       — <c>/servers/{serverId}/nics</c> (nested
///                            list + create) plus top-level
///                            <c>/server-nics/{id}</c> for update/delete</item>
/// </list>
/// Permission codes:
/// <c>net:servers:read</c> / <c>write</c> / <c>delete</c>.
/// </summary>
public static class ServerEndpoints
{
    public static RouteGroupBuilder MapNetServerEndpoints(this RouteGroupBuilder g)
    {
        // ═══════════════════════════════════════════════════════════════
        // server_profile
        // ═══════════════════════════════════════════════════════════════
        g.MapGet("/server-profiles", async (ITenantContext t, DbConnectionFactory db) =>
        {
            if (!t.IsResolved) return ApiProblem.ValidationError("No tenant context.");
            return Results.Ok(await new ServersRepository(db.ConnectionString).ListProfilesAsync(t.TenantId));
        }).RequireAuthorization(P.NetServersRead);

        g.MapGet("/server-profiles/{id:guid}", async (Guid id, ITenantContext t, DbConnectionFactory db) =>
        {
            if (!t.IsResolved) return ApiProblem.ValidationError("No tenant context.");
            var e = await new ServersRepository(db.ConnectionString).GetProfileAsync(id, t.TenantId);
            return e is null ? ApiProblem.NotFound($"Server profile {id} not found") : Results.Ok(e);
        }).RequireAuthorization(P.NetServersRead);

        g.MapPost("/server-profiles", async (ServerProfile body, ITenantContext t, DbConnectionFactory db, HttpContext ctx) =>
        {
            if (!t.IsResolved) return ApiProblem.ValidationError("No tenant context.");
            body.OrganizationId = t.TenantId;
            try
            {
                var id = await new ServersRepository(db.ConnectionString).CreateProfileAsync(body, UserIdOrNull(ctx));
                return Results.Created($"/api/net/server-profiles/{id}", new { id });
            }
            catch (Npgsql.PostgresException ex) when (ex.SqlState == "23505")
            {
                return ApiProblem.Conflict($"Server profile code '{body.ProfileCode}' already exists.");
            }
        }).RequireAuthorization(P.NetServersWrite);

        g.MapPut("/server-profiles/{id:guid}", async (Guid id, ServerProfile body, ITenantContext t, DbConnectionFactory db, HttpContext ctx) =>
        {
            if (!t.IsResolved) return ApiProblem.ValidationError("No tenant context.");
            body.Id = id; body.OrganizationId = t.TenantId;
            try
            {
                var v = await new ServersRepository(db.ConnectionString).UpdateProfileAsync(body, UserIdOrNull(ctx));
                return Results.Ok(new { id, version = v });
            }
            catch (ConcurrencyException ex) { return ApiProblem.Conflict(ex.Message); }
        }).RequireAuthorization(P.NetServersWrite);

        g.MapDelete("/server-profiles/{id:guid}", async (Guid id, ITenantContext t, DbConnectionFactory db, HttpContext ctx) =>
        {
            if (!t.IsResolved) return ApiProblem.ValidationError("No tenant context.");
            var ok = await new ServersRepository(db.ConnectionString)
                .SoftDeleteProfileAsync(id, t.TenantId, UserIdOrNull(ctx));
            return ok ? Results.NoContent() : ApiProblem.NotFound($"Server profile {id} not found");
        }).RequireAuthorization(P.NetServersDelete);

        // ═══════════════════════════════════════════════════════════════
        // server
        // ═══════════════════════════════════════════════════════════════
        g.MapGet("/servers", async (Guid? buildingId, ITenantContext t, DbConnectionFactory db) =>
        {
            if (!t.IsResolved) return ApiProblem.ValidationError("No tenant context.");
            return Results.Ok(await new ServersRepository(db.ConnectionString)
                .ListServersAsync(t.TenantId, buildingId));
        }).RequireAuthorization(P.NetServersRead);

        g.MapGet("/servers/{id:guid}", async (Guid id, ITenantContext t, DbConnectionFactory db) =>
        {
            if (!t.IsResolved) return ApiProblem.ValidationError("No tenant context.");
            var e = await new ServersRepository(db.ConnectionString).GetServerAsync(id, t.TenantId);
            return e is null ? ApiProblem.NotFound($"Server {id} not found") : Results.Ok(e);
        }).RequireAuthorization(P.NetServersRead);

        g.MapPost("/servers", async (Server body, ITenantContext t, DbConnectionFactory db, HttpContext ctx) =>
        {
            if (!t.IsResolved) return ApiProblem.ValidationError("No tenant context.");
            if (string.IsNullOrWhiteSpace(body.Hostname))
                return ApiProblem.ValidationError("hostname is required.");
            body.OrganizationId = t.TenantId;
            try
            {
                var id = await new ServersRepository(db.ConnectionString).CreateServerAsync(body, UserIdOrNull(ctx));
                return Results.Created($"/api/net/servers/{id}", new { id });
            }
            catch (Npgsql.PostgresException ex) when (ex.SqlState == "23505")
            {
                return ApiProblem.Conflict($"Hostname '{body.Hostname}' already exists in this tenant.");
            }
        }).RequireAuthorization(P.NetServersWrite);

        g.MapPut("/servers/{id:guid}", async (Guid id, Server body, ITenantContext t, DbConnectionFactory db, HttpContext ctx) =>
        {
            if (!t.IsResolved) return ApiProblem.ValidationError("No tenant context.");
            body.Id = id; body.OrganizationId = t.TenantId;
            try
            {
                var v = await new ServersRepository(db.ConnectionString).UpdateServerAsync(body, UserIdOrNull(ctx));
                return Results.Ok(new { id, version = v });
            }
            catch (ConcurrencyException ex) { return ApiProblem.Conflict(ex.Message); }
        }).RequireAuthorization(P.NetServersWrite);

        g.MapDelete("/servers/{id:guid}", async (Guid id, ITenantContext t, DbConnectionFactory db, HttpContext ctx) =>
        {
            if (!t.IsResolved) return ApiProblem.ValidationError("No tenant context.");
            var ok = await new ServersRepository(db.ConnectionString)
                .SoftDeleteServerAsync(id, t.TenantId, UserIdOrNull(ctx));
            return ok ? Results.NoContent() : ApiProblem.NotFound($"Server {id} not found");
        }).RequireAuthorization(P.NetServersDelete);

        g.MapPost("/servers/{id:guid}/ping", async (Guid id, PingResultRequest req,
            ITenantContext t, DbConnectionFactory db) =>
        {
            if (!t.IsResolved) return ApiProblem.ValidationError("No tenant context.");
            await new ServersRepository(db.ConnectionString)
                .RecordPingAsync(id, t.TenantId, req.Ok, req.LatencyMs);
            return Results.NoContent();
        }).RequireAuthorization(P.NetServersWrite);

        // ═══════════════════════════════════════════════════════════════
        // server_nic — nested under a server for list/create, flat for edit/delete
        // ═══════════════════════════════════════════════════════════════
        g.MapGet("/servers/{serverId:guid}/nics", async (Guid serverId, ITenantContext t, DbConnectionFactory db) =>
        {
            if (!t.IsResolved) return ApiProblem.ValidationError("No tenant context.");
            return Results.Ok(await new ServersRepository(db.ConnectionString)
                .ListNicsAsync(t.TenantId, serverId));
        }).RequireAuthorization(P.NetServersRead);

        g.MapPost("/servers/{serverId:guid}/nics", async (Guid serverId, ServerNic body,
            ITenantContext t, DbConnectionFactory db, HttpContext ctx) =>
        {
            if (!t.IsResolved) return ApiProblem.ValidationError("No tenant context.");
            body.OrganizationId = t.TenantId;
            body.ServerId = serverId;
            try
            {
                var id = await new ServersRepository(db.ConnectionString).CreateNicAsync(body, UserIdOrNull(ctx));
                return Results.Created($"/api/net/server-nics/{id}", new { id });
            }
            catch (Npgsql.PostgresException ex) when (ex.SqlState == "23505")
            {
                return ApiProblem.Conflict($"NIC index {body.NicIndex} already exists on this server.");
            }
        }).RequireAuthorization(P.NetServersWrite);

        g.MapPut("/server-nics/{id:guid}", async (Guid id, ServerNic body,
            ITenantContext t, DbConnectionFactory db, HttpContext ctx) =>
        {
            if (!t.IsResolved) return ApiProblem.ValidationError("No tenant context.");
            body.Id = id; body.OrganizationId = t.TenantId;
            try
            {
                var v = await new ServersRepository(db.ConnectionString).UpdateNicAsync(body, UserIdOrNull(ctx));
                return Results.Ok(new { id, version = v });
            }
            catch (ConcurrencyException ex) { return ApiProblem.Conflict(ex.Message); }
        }).RequireAuthorization(P.NetServersWrite);

        g.MapDelete("/server-nics/{id:guid}", async (Guid id, ITenantContext t, DbConnectionFactory db, HttpContext ctx) =>
        {
            if (!t.IsResolved) return ApiProblem.ValidationError("No tenant context.");
            var ok = await new ServersRepository(db.ConnectionString)
                .SoftDeleteNicAsync(id, t.TenantId, UserIdOrNull(ctx));
            return ok ? Results.NoContent() : ApiProblem.NotFound($"Server NIC {id} not found");
        }).RequireAuthorization(P.NetServersDelete);

        return g;
    }

    private static int? UserIdOrNull(HttpContext ctx)
    {
        var uid = ctx.User.FindFirst("sub")?.Value ?? ctx.User.FindFirst("user_id")?.Value;
        return int.TryParse(uid, out var i) ? i : null;
    }

    public record PingResultRequest(bool Ok, decimal? LatencyMs);
}
