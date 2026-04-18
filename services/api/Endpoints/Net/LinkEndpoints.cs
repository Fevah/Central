using Central.Api.Endpoints;
using Central.Engine.Auth;
using Central.Engine.Net.Links;
using Central.Persistence;
using Central.Persistence.Net;
using Central.Tenancy;

namespace Central.Api.Endpoints.Net;

/// <summary>
/// Phase 5c — REST surface for the unified link model. Three families:
/// <list type="bullet">
///   <item>link_type (catalog) — read + admin CRUD</item>
///   <item>link (the network link rows) — operator CRUD, two filter axes
///     (by link_type, by building)</item>
///   <item>link_endpoint (A + B sides) — nested under a link</item>
/// </list>
///
/// Permission split matches the pool surface:
///   net:links:read / write / delete.
///
/// All endpoints tenant-scoped via <see cref="ITenantContext"/>;
/// 23505 natural-key collision -> 409; <see cref="ConcurrencyException"/>
/// -> 409; missing row -> 404.
/// </summary>
public static class LinkEndpoints
{
    public static RouteGroupBuilder MapNetLinkEndpoints(this RouteGroupBuilder g)
    {
        // ═══════════════════════════════════════════════════════════════
        // link_type
        // ═══════════════════════════════════════════════════════════════
        g.MapGet("/link-types", async (ITenantContext t, DbConnectionFactory db) =>
        {
            if (!t.IsResolved) return ApiProblem.ValidationError("No tenant context.");
            return Results.Ok(await new LinksRepository(db.ConnectionString).ListTypesAsync(t.TenantId));
        }).RequireAuthorization(P.NetLinksRead);

        g.MapGet("/link-types/{id:guid}", async (Guid id, ITenantContext t, DbConnectionFactory db) =>
        {
            if (!t.IsResolved) return ApiProblem.ValidationError("No tenant context.");
            var e = await new LinksRepository(db.ConnectionString).GetTypeAsync(id, t.TenantId);
            return e is null ? ApiProblem.NotFound($"Link type {id} not found") : Results.Ok(e);
        }).RequireAuthorization(P.NetLinksRead);

        g.MapPost("/link-types", async (LinkType body, ITenantContext t, DbConnectionFactory db, HttpContext ctx) =>
        {
            if (!t.IsResolved) return ApiProblem.ValidationError("No tenant context.");
            body.OrganizationId = t.TenantId;
            try
            {
                var id = await new LinksRepository(db.ConnectionString).CreateTypeAsync(body, UserIdOrNull(ctx));
                return Results.Created($"/api/net/link-types/{id}", new { id });
            }
            catch (Npgsql.PostgresException ex) when (ex.SqlState == "23505")
            {
                return ApiProblem.Conflict($"Link type code '{body.TypeCode}' already exists.");
            }
        }).RequireAuthorization(P.NetLinksWrite);

        g.MapPut("/link-types/{id:guid}", async (Guid id, LinkType body, ITenantContext t, DbConnectionFactory db, HttpContext ctx) =>
        {
            if (!t.IsResolved) return ApiProblem.ValidationError("No tenant context.");
            body.Id = id; body.OrganizationId = t.TenantId;
            try
            {
                var v = await new LinksRepository(db.ConnectionString).UpdateTypeAsync(body, UserIdOrNull(ctx));
                return Results.Ok(new { id, version = v });
            }
            catch (ConcurrencyException ex) { return ApiProblem.Conflict(ex.Message); }
        }).RequireAuthorization(P.NetLinksWrite);

        g.MapDelete("/link-types/{id:guid}", async (Guid id, ITenantContext t, DbConnectionFactory db, HttpContext ctx) =>
        {
            if (!t.IsResolved) return ApiProblem.ValidationError("No tenant context.");
            var ok = await new LinksRepository(db.ConnectionString)
                .SoftDeleteTypeAsync(id, t.TenantId, UserIdOrNull(ctx));
            return ok ? Results.NoContent() : ApiProblem.NotFound($"Link type {id} not found");
        }).RequireAuthorization(P.NetLinksDelete);

        // ═══════════════════════════════════════════════════════════════
        // link
        // ═══════════════════════════════════════════════════════════════
        g.MapGet("/links", async (Guid? linkTypeId, Guid? buildingId, ITenantContext t, DbConnectionFactory db) =>
        {
            if (!t.IsResolved) return ApiProblem.ValidationError("No tenant context.");
            return Results.Ok(await new LinksRepository(db.ConnectionString)
                .ListLinksAsync(t.TenantId, linkTypeId, buildingId));
        }).RequireAuthorization(P.NetLinksRead);

        g.MapGet("/links/{id:guid}", async (Guid id, ITenantContext t, DbConnectionFactory db) =>
        {
            if (!t.IsResolved) return ApiProblem.ValidationError("No tenant context.");
            var e = await new LinksRepository(db.ConnectionString).GetLinkAsync(id, t.TenantId);
            return e is null ? ApiProblem.NotFound($"Link {id} not found") : Results.Ok(e);
        }).RequireAuthorization(P.NetLinksRead);

        g.MapPost("/links", async (Link body, ITenantContext t, DbConnectionFactory db, HttpContext ctx) =>
        {
            if (!t.IsResolved) return ApiProblem.ValidationError("No tenant context.");
            if (string.IsNullOrWhiteSpace(body.LinkCode))
                return ApiProblem.ValidationError("linkCode is required.");
            body.OrganizationId = t.TenantId;
            try
            {
                var id = await new LinksRepository(db.ConnectionString).CreateLinkAsync(body, UserIdOrNull(ctx));
                return Results.Created($"/api/net/links/{id}", new { id });
            }
            catch (Npgsql.PostgresException ex) when (ex.SqlState == "23505")
            {
                return ApiProblem.Conflict($"Link code '{body.LinkCode}' already exists in this tenant.");
            }
        }).RequireAuthorization(P.NetLinksWrite);

        g.MapPut("/links/{id:guid}", async (Guid id, Link body, ITenantContext t, DbConnectionFactory db, HttpContext ctx) =>
        {
            if (!t.IsResolved) return ApiProblem.ValidationError("No tenant context.");
            body.Id = id; body.OrganizationId = t.TenantId;
            try
            {
                var v = await new LinksRepository(db.ConnectionString).UpdateLinkAsync(body, UserIdOrNull(ctx));
                return Results.Ok(new { id, version = v });
            }
            catch (ConcurrencyException ex) { return ApiProblem.Conflict(ex.Message); }
        }).RequireAuthorization(P.NetLinksWrite);

        g.MapDelete("/links/{id:guid}", async (Guid id, ITenantContext t, DbConnectionFactory db, HttpContext ctx) =>
        {
            if (!t.IsResolved) return ApiProblem.ValidationError("No tenant context.");
            var ok = await new LinksRepository(db.ConnectionString)
                .SoftDeleteLinkAsync(id, t.TenantId, UserIdOrNull(ctx));
            return ok ? Results.NoContent() : ApiProblem.NotFound($"Link {id} not found");
        }).RequireAuthorization(P.NetLinksDelete);

        // ═══════════════════════════════════════════════════════════════
        // link_endpoint (nested under a link)
        // ═══════════════════════════════════════════════════════════════
        g.MapGet("/links/{linkId:guid}/endpoints", async (Guid linkId, ITenantContext t, DbConnectionFactory db) =>
        {
            if (!t.IsResolved) return ApiProblem.ValidationError("No tenant context.");
            return Results.Ok(await new LinksRepository(db.ConnectionString)
                .ListEndpointsAsync(t.TenantId, linkId));
        }).RequireAuthorization(P.NetLinksRead);

        g.MapPost("/links/{linkId:guid}/endpoints", async (Guid linkId, LinkEndpoint body,
            ITenantContext t, DbConnectionFactory db, HttpContext ctx) =>
        {
            if (!t.IsResolved) return ApiProblem.ValidationError("No tenant context.");
            body.OrganizationId = t.TenantId;
            body.LinkId = linkId;
            try
            {
                var id = await new LinksRepository(db.ConnectionString).CreateEndpointAsync(body, UserIdOrNull(ctx));
                return Results.Created($"/api/net/link-endpoints/{id}", new { id });
            }
            catch (Npgsql.PostgresException ex) when (ex.SqlState == "23505")
            {
                return ApiProblem.Conflict($"Endpoint order {body.EndpointOrder} already exists on this link.");
            }
        }).RequireAuthorization(P.NetLinksWrite);

        g.MapPut("/link-endpoints/{id:guid}", async (Guid id, LinkEndpoint body,
            ITenantContext t, DbConnectionFactory db, HttpContext ctx) =>
        {
            if (!t.IsResolved) return ApiProblem.ValidationError("No tenant context.");
            body.Id = id; body.OrganizationId = t.TenantId;
            try
            {
                var v = await new LinksRepository(db.ConnectionString).UpdateEndpointAsync(body, UserIdOrNull(ctx));
                return Results.Ok(new { id, version = v });
            }
            catch (ConcurrencyException ex) { return ApiProblem.Conflict(ex.Message); }
        }).RequireAuthorization(P.NetLinksWrite);

        g.MapDelete("/link-endpoints/{id:guid}", async (Guid id, ITenantContext t, DbConnectionFactory db, HttpContext ctx) =>
        {
            if (!t.IsResolved) return ApiProblem.ValidationError("No tenant context.");
            var ok = await new LinksRepository(db.ConnectionString)
                .SoftDeleteEndpointAsync(id, t.TenantId, UserIdOrNull(ctx));
            return ok ? Results.NoContent() : ApiProblem.NotFound($"Endpoint {id} not found");
        }).RequireAuthorization(P.NetLinksDelete);

        return g;
    }

    private static int? UserIdOrNull(HttpContext ctx)
    {
        var uid = ctx.User.FindFirst("sub")?.Value ?? ctx.User.FindFirst("user_id")?.Value;
        return int.TryParse(uid, out var i) ? i : null;
    }
}
