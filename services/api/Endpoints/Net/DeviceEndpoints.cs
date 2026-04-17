using Central.Api.Endpoints;
using Central.Engine.Auth;
using Central.Engine.Net.Devices;
using Central.Persistence;
using Central.Persistence.Net;
using Central.Tenancy;

namespace Central.Api.Endpoints.Net;

/// <summary>
/// Networking engine Phase 4c — REST endpoints for the device catalog.
/// CRUD on device_role, device, module, port, aggregate_ethernet,
/// loopback. Same idiom as Phase 3e's pool endpoints: tenant-scoped
/// via <see cref="ITenantContext"/>, 409 on concurrency / unique-code
/// collisions, 404 when the row doesn't exist or belongs to a
/// different tenant.
///
/// Three permission codes:
///   net:devices:read   list / get every table
///   net:devices:write  create / update any of the six
///   net:devices:delete soft-delete
/// </summary>
public static class DeviceEndpoints
{
    public static RouteGroupBuilder MapNetDeviceEndpoints(this RouteGroupBuilder g)
    {
        // ═══════════════════════════════════════════════════════════════
        // device_role
        // ═══════════════════════════════════════════════════════════════
        g.MapGet("/device-roles", async (ITenantContext t, DbConnectionFactory db) =>
        {
            if (!t.IsResolved) return ApiProblem.ValidationError("No tenant context.");
            return Results.Ok(await new DevicesRepository(db.ConnectionString).ListRolesAsync(t.TenantId));
        }).RequireAuthorization(P.NetDevicesRead);

        g.MapGet("/device-roles/{id:guid}", async (Guid id, ITenantContext t, DbConnectionFactory db) =>
        {
            if (!t.IsResolved) return ApiProblem.ValidationError("No tenant context.");
            var e = await new DevicesRepository(db.ConnectionString).GetRoleAsync(id, t.TenantId);
            return e is null ? ApiProblem.NotFound($"Role {id} not found") : Results.Ok(e);
        }).RequireAuthorization(P.NetDevicesRead);

        g.MapPost("/device-roles", async (DeviceRole body, ITenantContext t, DbConnectionFactory db, HttpContext ctx) =>
        {
            if (!t.IsResolved) return ApiProblem.ValidationError("No tenant context.");
            body.OrganizationId = t.TenantId;
            try
            {
                var id = await new DevicesRepository(db.ConnectionString).CreateRoleAsync(body, UserIdOrNull(ctx));
                return Results.Created($"/api/net/device-roles/{id}", new { id });
            }
            catch (Npgsql.PostgresException ex) when (ex.SqlState == "23505")
            {
                return ApiProblem.Conflict($"Role code '{body.RoleCode}' already exists.");
            }
        }).RequireAuthorization(P.NetDevicesWrite);

        g.MapPut("/device-roles/{id:guid}", async (Guid id, DeviceRole body, ITenantContext t, DbConnectionFactory db, HttpContext ctx) =>
        {
            if (!t.IsResolved) return ApiProblem.ValidationError("No tenant context.");
            body.Id = id; body.OrganizationId = t.TenantId;
            try
            {
                var v = await new DevicesRepository(db.ConnectionString).UpdateRoleAsync(body, UserIdOrNull(ctx));
                return Results.Ok(new { id, version = v });
            }
            catch (ConcurrencyException ex) { return ApiProblem.Conflict(ex.Message); }
        }).RequireAuthorization(P.NetDevicesWrite);

        g.MapDelete("/device-roles/{id:guid}", async (Guid id, ITenantContext t, DbConnectionFactory db, HttpContext ctx) =>
        {
            if (!t.IsResolved) return ApiProblem.ValidationError("No tenant context.");
            var ok = await new DevicesRepository(db.ConnectionString)
                .SoftDeleteRoleAsync(id, t.TenantId, UserIdOrNull(ctx));
            return ok ? Results.NoContent() : ApiProblem.NotFound($"Role {id} not found");
        }).RequireAuthorization(P.NetDevicesDelete);

        // ═══════════════════════════════════════════════════════════════
        // device
        // ═══════════════════════════════════════════════════════════════
        g.MapGet("/devices", async (Guid? buildingId, ITenantContext t, DbConnectionFactory db) =>
        {
            if (!t.IsResolved) return ApiProblem.ValidationError("No tenant context.");
            return Results.Ok(await new DevicesRepository(db.ConnectionString)
                .ListDevicesAsync(t.TenantId, buildingId));
        }).RequireAuthorization(P.NetDevicesRead);

        g.MapGet("/devices/{id:guid}", async (Guid id, ITenantContext t, DbConnectionFactory db) =>
        {
            if (!t.IsResolved) return ApiProblem.ValidationError("No tenant context.");
            var e = await new DevicesRepository(db.ConnectionString).GetDeviceAsync(id, t.TenantId);
            return e is null ? ApiProblem.NotFound($"Device {id} not found") : Results.Ok(e);
        }).RequireAuthorization(P.NetDevicesRead);

        g.MapPost("/devices", async (Device body, ITenantContext t, DbConnectionFactory db, HttpContext ctx) =>
        {
            if (!t.IsResolved) return ApiProblem.ValidationError("No tenant context.");
            if (string.IsNullOrWhiteSpace(body.Hostname))
                return ApiProblem.ValidationError("hostname is required.");
            body.OrganizationId = t.TenantId;
            try
            {
                var id = await new DevicesRepository(db.ConnectionString).CreateDeviceAsync(body, UserIdOrNull(ctx));
                return Results.Created($"/api/net/devices/{id}", new { id });
            }
            catch (Npgsql.PostgresException ex) when (ex.SqlState == "23505")
            {
                return ApiProblem.Conflict($"Hostname '{body.Hostname}' already exists in this tenant.");
            }
        }).RequireAuthorization(P.NetDevicesWrite);

        g.MapPut("/devices/{id:guid}", async (Guid id, Device body, ITenantContext t, DbConnectionFactory db, HttpContext ctx) =>
        {
            if (!t.IsResolved) return ApiProblem.ValidationError("No tenant context.");
            body.Id = id; body.OrganizationId = t.TenantId;
            try
            {
                var v = await new DevicesRepository(db.ConnectionString).UpdateDeviceAsync(body, UserIdOrNull(ctx));
                return Results.Ok(new { id, version = v });
            }
            catch (ConcurrencyException ex) { return ApiProblem.Conflict(ex.Message); }
        }).RequireAuthorization(P.NetDevicesWrite);

        g.MapDelete("/devices/{id:guid}", async (Guid id, ITenantContext t, DbConnectionFactory db, HttpContext ctx) =>
        {
            if (!t.IsResolved) return ApiProblem.ValidationError("No tenant context.");
            var ok = await new DevicesRepository(db.ConnectionString)
                .SoftDeleteDeviceAsync(id, t.TenantId, UserIdOrNull(ctx));
            return ok ? Results.NoContent() : ApiProblem.NotFound($"Device {id} not found");
        }).RequireAuthorization(P.NetDevicesDelete);

        // Ping probe result — fast path, no version bump, no full write.
        g.MapPost("/devices/{id:guid}/ping", async (Guid id, PingResultRequest req,
            ITenantContext t, DbConnectionFactory db) =>
        {
            if (!t.IsResolved) return ApiProblem.ValidationError("No tenant context.");
            await new DevicesRepository(db.ConnectionString)
                .RecordPingAsync(id, t.TenantId, req.Ok, req.LatencyMs);
            return Results.NoContent();
        }).RequireAuthorization(P.NetDevicesWrite);

        // ═══════════════════════════════════════════════════════════════
        // module / port / aggregate / loopback — abbreviated CRUD set.
        // Same shape as the parent resources, narrowed by the URL.
        // ═══════════════════════════════════════════════════════════════

        MapChildResource<Module>(g, "/modules",
            (repo, org, parent) => repo.ListModulesAsync(org, parent),
            (repo, row, uid) => repo.CreateModuleAsync(row, uid),
            (repo, row, uid) => repo.UpdateModuleAsync(row, uid),
            (repo, id, org, uid) => repo.SoftDeleteModuleAsync(id, org, uid),
            "deviceId", "Module");

        MapChildResource<Port>(g, "/ports",
            (repo, org, parent) => repo.ListPortsAsync(org, parent),
            (repo, row, uid) => repo.CreatePortAsync(row, uid),
            (repo, row, uid) => repo.UpdatePortAsync(row, uid),
            (repo, id, org, uid) => repo.SoftDeletePortAsync(id, org, uid),
            "deviceId", "Port");

        MapChildResource<AggregateEthernet>(g, "/aggregate-ethernets",
            (repo, org, parent) => repo.ListAggregatesAsync(org, parent),
            (repo, row, uid) => repo.CreateAggregateAsync(row, uid),
            (repo, row, uid) => repo.UpdateAggregateAsync(row, uid),
            (repo, id, org, uid) => repo.SoftDeleteAggregateAsync(id, org, uid),
            "deviceId", "Aggregate");

        MapChildResource<Loopback>(g, "/loopbacks",
            (repo, org, parent) => repo.ListLoopbacksAsync(org, parent),
            (repo, row, uid) => repo.CreateLoopbackAsync(row, uid),
            (repo, row, uid) => repo.UpdateLoopbackAsync(row, uid),
            (repo, id, org, uid) => repo.SoftDeleteLoopbackAsync(id, org, uid),
            "deviceId", "Loopback");

        return g;
    }

    /// <summary>
    /// Factor out the boilerplate for the four child resources
    /// (module / port / AE / loopback). Each is a device-scoped table
    /// with the same List-by-parent / CRUD shape — this helper keeps
    /// the endpoint file readable instead of duplicating the pattern
    /// four times.
    /// </summary>
    private static void MapChildResource<T>(
        RouteGroupBuilder g,
        string path,
        Func<DevicesRepository, Guid, Guid?, Task<List<T>>> list,
        Func<DevicesRepository, T, int?, Task<Guid>> create,
        Func<DevicesRepository, T, int?, Task<int>> update,
        Func<DevicesRepository, Guid, Guid, int?, Task<bool>> softDelete,
        string parentQueryName,
        string label)
        where T : Central.Engine.Net.Hierarchy.EntityBase
    {
        g.MapGet(path, async (HttpContext http, ITenantContext t, DbConnectionFactory db) =>
        {
            if (!t.IsResolved) return ApiProblem.ValidationError("No tenant context.");
            Guid? parentId = null;
            if (http.Request.Query.TryGetValue(parentQueryName, out var raw)
                && Guid.TryParse(raw, out var parsed))
                parentId = parsed;
            return Results.Ok(await list(new DevicesRepository(db.ConnectionString), t.TenantId, parentId));
        }).RequireAuthorization(P.NetDevicesRead);

        g.MapPost(path, async (T body, ITenantContext t, DbConnectionFactory db, HttpContext ctx) =>
        {
            if (!t.IsResolved) return ApiProblem.ValidationError("No tenant context.");
            body.OrganizationId = t.TenantId;
            try
            {
                var id = await create(new DevicesRepository(db.ConnectionString), body, UserIdOrNull(ctx));
                return Results.Created($"/api/net{path}/{id}", new { id });
            }
            catch (Npgsql.PostgresException ex) when (ex.SqlState == "23505")
            {
                return ApiProblem.Conflict($"{label} natural-key conflict.");
            }
        }).RequireAuthorization(P.NetDevicesWrite);

        g.MapPut($"{path}/{{id:guid}}", async (Guid id, T body, ITenantContext t, DbConnectionFactory db, HttpContext ctx) =>
        {
            if (!t.IsResolved) return ApiProblem.ValidationError("No tenant context.");
            body.Id = id; body.OrganizationId = t.TenantId;
            try
            {
                var v = await update(new DevicesRepository(db.ConnectionString), body, UserIdOrNull(ctx));
                return Results.Ok(new { id, version = v });
            }
            catch (ConcurrencyException ex) { return ApiProblem.Conflict(ex.Message); }
        }).RequireAuthorization(P.NetDevicesWrite);

        g.MapDelete($"{path}/{{id:guid}}", async (Guid id, ITenantContext t, DbConnectionFactory db, HttpContext ctx) =>
        {
            if (!t.IsResolved) return ApiProblem.ValidationError("No tenant context.");
            var ok = await softDelete(new DevicesRepository(db.ConnectionString), id, t.TenantId, UserIdOrNull(ctx));
            return ok ? Results.NoContent() : ApiProblem.NotFound($"{label} {id} not found");
        }).RequireAuthorization(P.NetDevicesDelete);
    }

    private static int? UserIdOrNull(HttpContext ctx)
    {
        var uid = ctx.User.FindFirst("sub")?.Value ?? ctx.User.FindFirst("user_id")?.Value;
        return int.TryParse(uid, out var i) ? i : null;
    }

    public record PingResultRequest(bool Ok, decimal? LatencyMs);
}
