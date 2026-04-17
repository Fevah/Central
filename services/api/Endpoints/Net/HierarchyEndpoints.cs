using System.Text.Json;
using Central.Api.Endpoints;
using Central.Engine.Auth;
using Central.Engine.Net;
using Central.Engine.Net.Hierarchy;
using Central.Persistence;
using Central.Persistence.Net;
using Central.Tenancy;

namespace Central.Api.Endpoints.Net;

/// <summary>
/// Networking engine Phase 2b — REST endpoints for the geographic hierarchy.
/// List + Get for every entity; Create / Update / Soft-delete for Region,
/// Site, Building (the levels users routinely CRUD). Profiles + Floor / Room
/// / Rack get read endpoints here; their write endpoints land when the WPF
/// panels for them are built (Phase 2c).
///
/// Every endpoint is tenant-scoped via <see cref="ITenantContext"/>. No
/// unscoped reads — a request without a resolved tenant is rejected.
/// </summary>
public static class HierarchyEndpoints
{
    public static RouteGroupBuilder MapNetHierarchyEndpoints(this RouteGroupBuilder g)
    {
        // ── Region ──────────────────────────────────────────────────────
        g.MapGet("/regions", async (ITenantContext tenant, DbConnectionFactory db) =>
        {
            if (!tenant.IsResolved) return ApiProblem.ValidationError("No tenant context.");
            var repo = new HierarchyRepository(db.ConnectionString);
            return Results.Ok(await repo.ListRegionsAsync(tenant.TenantId));
        }).RequireAuthorization();

        g.MapGet("/regions/{id:guid}", async (Guid id, ITenantContext tenant, DbConnectionFactory db) =>
        {
            if (!tenant.IsResolved) return ApiProblem.ValidationError("No tenant context.");
            var repo = new HierarchyRepository(db.ConnectionString);
            var e = await repo.GetRegionAsync(id, tenant.TenantId);
            return e is null ? ApiProblem.NotFound($"Region {id} not found") : Results.Ok(e);
        }).RequireAuthorization();

        g.MapPost("/regions", async (Region body, ITenantContext tenant, DbConnectionFactory db, HttpContext ctx) =>
        {
            if (!tenant.IsResolved) return ApiProblem.ValidationError("No tenant context.");
            body.OrganizationId = tenant.TenantId;
            var repo = new HierarchyRepository(db.ConnectionString);
            try
            {
                var id = await repo.CreateRegionAsync(body, UserIdOrNull(ctx));
                return Results.Created($"/api/net/regions/{id}", new { id });
            }
            catch (Npgsql.PostgresException ex) when (ex.SqlState == "23505")
            {
                return ApiProblem.Conflict($"Region code '{body.RegionCode}' already exists for this organisation.");
            }
        }).RequireAuthorization(P.NetHierarchyWrite);

        g.MapPut("/regions/{id:guid}", async (Guid id, Region body, ITenantContext tenant, DbConnectionFactory db, HttpContext ctx) =>
        {
            if (!tenant.IsResolved) return ApiProblem.ValidationError("No tenant context.");
            body.Id = id;
            body.OrganizationId = tenant.TenantId;
            var repo = new HierarchyRepository(db.ConnectionString);
            try
            {
                var newVersion = await repo.UpdateRegionAsync(body, UserIdOrNull(ctx));
                return Results.Ok(new { id, version = newVersion });
            }
            catch (ConcurrencyException ex)
            {
                return ApiProblem.Conflict(ex.Message);
            }
        }).RequireAuthorization(P.NetHierarchyWrite);

        g.MapDelete("/regions/{id:guid}", async (Guid id, ITenantContext tenant, DbConnectionFactory db, HttpContext ctx) =>
        {
            if (!tenant.IsResolved) return ApiProblem.ValidationError("No tenant context.");
            var repo = new HierarchyRepository(db.ConnectionString);
            var deleted = await repo.SoftDeleteRegionAsync(id, tenant.TenantId, UserIdOrNull(ctx));
            return deleted ? Results.NoContent() : ApiProblem.NotFound($"Region {id} not found");
        }).RequireAuthorization(P.NetHierarchyDelete);

        // ── Sites (list + get) ──────────────────────────────────────────
        g.MapGet("/sites", async (Guid? regionId, ITenantContext tenant, DbConnectionFactory db) =>
        {
            if (!tenant.IsResolved) return ApiProblem.ValidationError("No tenant context.");
            var repo = new HierarchyRepository(db.ConnectionString);
            return Results.Ok(await repo.ListSitesAsync(tenant.TenantId, regionId));
        }).RequireAuthorization();

        g.MapGet("/sites/{id:guid}", async (Guid id, ITenantContext tenant, DbConnectionFactory db) =>
        {
            if (!tenant.IsResolved) return ApiProblem.ValidationError("No tenant context.");
            var repo = new HierarchyRepository(db.ConnectionString);
            var e = await repo.GetSiteAsync(id, tenant.TenantId);
            return e is null ? ApiProblem.NotFound($"Site {id} not found") : Results.Ok(e);
        }).RequireAuthorization();

        // ── Buildings (list + get) ─────────────────────────────────────
        g.MapGet("/buildings", async (Guid? siteId, ITenantContext tenant, DbConnectionFactory db) =>
        {
            if (!tenant.IsResolved) return ApiProblem.ValidationError("No tenant context.");
            var repo = new HierarchyRepository(db.ConnectionString);
            return Results.Ok(await repo.ListBuildingsAsync(tenant.TenantId, siteId));
        }).RequireAuthorization();

        g.MapGet("/buildings/{id:guid}", async (Guid id, ITenantContext tenant, DbConnectionFactory db) =>
        {
            if (!tenant.IsResolved) return ApiProblem.ValidationError("No tenant context.");
            var repo = new HierarchyRepository(db.ConnectionString);
            var e = await repo.GetBuildingAsync(id, tenant.TenantId);
            return e is null ? ApiProblem.NotFound($"Building {id} not found") : Results.Ok(e);
        }).RequireAuthorization();

        // ── Floors ─────────────────────────────────────────────────────
        g.MapGet("/floors", async (Guid? buildingId, ITenantContext tenant, DbConnectionFactory db) =>
        {
            if (!tenant.IsResolved) return ApiProblem.ValidationError("No tenant context.");
            var repo = new HierarchyRepository(db.ConnectionString);
            return Results.Ok(await repo.ListFloorsAsync(tenant.TenantId, buildingId));
        }).RequireAuthorization();

        g.MapGet("/floors/{id:guid}", async (Guid id, ITenantContext tenant, DbConnectionFactory db) =>
        {
            if (!tenant.IsResolved) return ApiProblem.ValidationError("No tenant context.");
            var repo = new HierarchyRepository(db.ConnectionString);
            var e = await repo.GetFloorAsync(id, tenant.TenantId);
            return e is null ? ApiProblem.NotFound($"Floor {id} not found") : Results.Ok(e);
        }).RequireAuthorization();

        // ── Rooms ──────────────────────────────────────────────────────
        g.MapGet("/rooms", async (Guid? floorId, ITenantContext tenant, DbConnectionFactory db) =>
        {
            if (!tenant.IsResolved) return ApiProblem.ValidationError("No tenant context.");
            var repo = new HierarchyRepository(db.ConnectionString);
            return Results.Ok(await repo.ListRoomsAsync(tenant.TenantId, floorId));
        }).RequireAuthorization();

        g.MapGet("/rooms/{id:guid}", async (Guid id, ITenantContext tenant, DbConnectionFactory db) =>
        {
            if (!tenant.IsResolved) return ApiProblem.ValidationError("No tenant context.");
            var repo = new HierarchyRepository(db.ConnectionString);
            var e = await repo.GetRoomAsync(id, tenant.TenantId);
            return e is null ? ApiProblem.NotFound($"Room {id} not found") : Results.Ok(e);
        }).RequireAuthorization();

        // ── Racks ──────────────────────────────────────────────────────
        g.MapGet("/racks", async (Guid? roomId, ITenantContext tenant, DbConnectionFactory db) =>
        {
            if (!tenant.IsResolved) return ApiProblem.ValidationError("No tenant context.");
            var repo = new HierarchyRepository(db.ConnectionString);
            return Results.Ok(await repo.ListRacksAsync(tenant.TenantId, roomId));
        }).RequireAuthorization();

        g.MapGet("/racks/{id:guid}", async (Guid id, ITenantContext tenant, DbConnectionFactory db) =>
        {
            if (!tenant.IsResolved) return ApiProblem.ValidationError("No tenant context.");
            var repo = new HierarchyRepository(db.ConnectionString);
            var e = await repo.GetRackAsync(id, tenant.TenantId);
            return e is null ? ApiProblem.NotFound($"Rack {id} not found") : Results.Ok(e);
        }).RequireAuthorization();

        // ── Profiles (list + get only in 2b) ───────────────────────────
        g.MapGet("/site-profiles", async (ITenantContext tenant, DbConnectionFactory db) =>
        {
            if (!tenant.IsResolved) return ApiProblem.ValidationError("No tenant context.");
            var repo = new HierarchyRepository(db.ConnectionString);
            return Results.Ok(await repo.ListSiteProfilesAsync(tenant.TenantId));
        }).RequireAuthorization();

        g.MapGet("/building-profiles", async (ITenantContext tenant, DbConnectionFactory db) =>
        {
            if (!tenant.IsResolved) return ApiProblem.ValidationError("No tenant context.");
            var repo = new HierarchyRepository(db.ConnectionString);
            return Results.Ok(await repo.ListBuildingProfilesAsync(tenant.TenantId));
        }).RequireAuthorization();

        g.MapGet("/floor-profiles", async (ITenantContext tenant, DbConnectionFactory db) =>
        {
            if (!tenant.IsResolved) return ApiProblem.ValidationError("No tenant context.");
            var repo = new HierarchyRepository(db.ConnectionString);
            return Results.Ok(await repo.ListFloorProfilesAsync(tenant.TenantId));
        }).RequireAuthorization();

        return g;
    }

    private static int? UserIdOrNull(HttpContext ctx)
    {
        var uid = ctx.User.FindFirst("sub")?.Value ?? ctx.User.FindFirst("user_id")?.Value;
        return int.TryParse(uid, out var i) ? i : null;
    }
}
