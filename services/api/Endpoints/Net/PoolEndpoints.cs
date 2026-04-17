using Central.Api.Endpoints;
using Central.Engine.Auth;
using Central.Engine.Net.Pools;
using Central.Persistence;
using Central.Persistence.Net;
using Central.Tenancy;

namespace Central.Api.Endpoints.Net;

/// <summary>
/// Networking engine Phase 3e — REST endpoints for the numbering-pool
/// system. Four concerns, four concentric layers of capability:
///
/// <list type="bullet">
///   <item>Reads (<c>net:pools:read</c>) — list / get every pool, block,
///     allocation, and shelf entry.</item>
///   <item>Writes (<c>net:pools:write</c>) — CRUD on pools, blocks,
///     subnets, VLAN templates, MSTP rules. Pool writes create capacity;
///     they do not hand out numbers.</item>
///   <item>Deletes (<c>net:pools:delete</c>) — soft-delete a pool or
///     block (still a tenant-wide retirement).</item>
///   <item>Allocations (<c>net:pools:allocate</c>) — hand out a new ASN /
///     VLAN / MLAG / IP / subnet, or push a value onto the reservation
///     shelf. Distinct from write because the trust boundary is different:
///     "can create pools" is not the same as "can consume from pools".</item>
/// </list>
///
/// Allocation endpoints never call the repository directly — they invoke
/// <see cref="AllocationService"/> / <see cref="IpAllocationService"/> so
/// the advisory-lock + shelf-cooldown invariants stay intact.
///
/// Every endpoint is tenant-scoped via <see cref="ITenantContext"/>; no
/// unscoped reads.
/// </summary>
public static class PoolEndpoints
{
    public static RouteGroupBuilder MapNetPoolEndpoints(this RouteGroupBuilder g)
    {
        // ═══════════════════════════════════════════════════════════════
        // ASN pools + blocks
        // ═══════════════════════════════════════════════════════════════

        g.MapGet("/asn-pools", async (ITenantContext tenant, DbConnectionFactory db) =>
        {
            if (!tenant.IsResolved) return ApiProblem.ValidationError("No tenant context.");
            var repo = new PoolsRepository(db.ConnectionString);
            return Results.Ok(await repo.ListAsnPoolsAsync(tenant.TenantId));
        }).RequireAuthorization(P.NetPoolsRead);

        g.MapGet("/asn-pools/{id:guid}", async (Guid id, ITenantContext tenant, DbConnectionFactory db) =>
        {
            if (!tenant.IsResolved) return ApiProblem.ValidationError("No tenant context.");
            var repo = new PoolsRepository(db.ConnectionString);
            var e = await repo.GetAsnPoolAsync(id, tenant.TenantId);
            return e is null ? ApiProblem.NotFound($"ASN pool {id} not found") : Results.Ok(e);
        }).RequireAuthorization(P.NetPoolsRead);

        g.MapPost("/asn-pools", async (AsnPool body, ITenantContext tenant, DbConnectionFactory db, HttpContext ctx) =>
        {
            if (!tenant.IsResolved) return ApiProblem.ValidationError("No tenant context.");
            body.OrganizationId = tenant.TenantId;
            var repo = new PoolsRepository(db.ConnectionString);
            try
            {
                var id = await repo.CreateAsnPoolAsync(body, UserIdOrNull(ctx));
                return Results.Created($"/api/net/asn-pools/{id}", new { id });
            }
            catch (Npgsql.PostgresException ex) when (ex.SqlState == "23505")
            {
                return ApiProblem.Conflict($"ASN pool code '{body.PoolCode}' already exists.");
            }
        }).RequireAuthorization(P.NetPoolsWrite);

        g.MapPut("/asn-pools/{id:guid}", async (Guid id, AsnPool body, ITenantContext tenant, DbConnectionFactory db, HttpContext ctx) =>
        {
            if (!tenant.IsResolved) return ApiProblem.ValidationError("No tenant context.");
            body.Id = id;
            body.OrganizationId = tenant.TenantId;
            var repo = new PoolsRepository(db.ConnectionString);
            try
            {
                var v = await repo.UpdateAsnPoolAsync(body, UserIdOrNull(ctx));
                return Results.Ok(new { id, version = v });
            }
            catch (ConcurrencyException ex) { return ApiProblem.Conflict(ex.Message); }
        }).RequireAuthorization(P.NetPoolsWrite);

        g.MapDelete("/asn-pools/{id:guid}", async (Guid id, ITenantContext tenant, DbConnectionFactory db, HttpContext ctx) =>
        {
            if (!tenant.IsResolved) return ApiProblem.ValidationError("No tenant context.");
            var repo = new PoolsRepository(db.ConnectionString);
            var deleted = await repo.SoftDeleteAsnPoolAsync(id, tenant.TenantId, UserIdOrNull(ctx));
            return deleted ? Results.NoContent() : ApiProblem.NotFound($"ASN pool {id} not found");
        }).RequireAuthorization(P.NetPoolsDelete);

        g.MapGet("/asn-blocks", async (Guid? poolId, ITenantContext tenant, DbConnectionFactory db) =>
        {
            if (!tenant.IsResolved) return ApiProblem.ValidationError("No tenant context.");
            var repo = new PoolsRepository(db.ConnectionString);
            return Results.Ok(await repo.ListAsnBlocksAsync(tenant.TenantId, poolId));
        }).RequireAuthorization(P.NetPoolsRead);

        g.MapGet("/asn-blocks/{id:guid}", async (Guid id, ITenantContext tenant, DbConnectionFactory db) =>
        {
            if (!tenant.IsResolved) return ApiProblem.ValidationError("No tenant context.");
            var repo = new PoolsRepository(db.ConnectionString);
            var e = await repo.GetAsnBlockAsync(id, tenant.TenantId);
            return e is null ? ApiProblem.NotFound($"ASN block {id} not found") : Results.Ok(e);
        }).RequireAuthorization(P.NetPoolsRead);

        g.MapPost("/asn-blocks", async (AsnBlock body, ITenantContext tenant, DbConnectionFactory db, HttpContext ctx) =>
        {
            if (!tenant.IsResolved) return ApiProblem.ValidationError("No tenant context.");
            body.OrganizationId = tenant.TenantId;
            var repo = new PoolsRepository(db.ConnectionString);
            try
            {
                var id = await repo.CreateAsnBlockAsync(body, UserIdOrNull(ctx));
                return Results.Created($"/api/net/asn-blocks/{id}", new { id });
            }
            catch (Npgsql.PostgresException ex) when (ex.SqlState == "23505")
            {
                return ApiProblem.Conflict($"ASN block code '{body.BlockCode}' already exists.");
            }
        }).RequireAuthorization(P.NetPoolsWrite);

        g.MapPut("/asn-blocks/{id:guid}", async (Guid id, AsnBlock body, ITenantContext tenant, DbConnectionFactory db, HttpContext ctx) =>
        {
            if (!tenant.IsResolved) return ApiProblem.ValidationError("No tenant context.");
            body.Id = id;
            body.OrganizationId = tenant.TenantId;
            var repo = new PoolsRepository(db.ConnectionString);
            try
            {
                var v = await repo.UpdateAsnBlockAsync(body, UserIdOrNull(ctx));
                return Results.Ok(new { id, version = v });
            }
            catch (ConcurrencyException ex) { return ApiProblem.Conflict(ex.Message); }
        }).RequireAuthorization(P.NetPoolsWrite);

        g.MapDelete("/asn-blocks/{id:guid}", async (Guid id, ITenantContext tenant, DbConnectionFactory db, HttpContext ctx) =>
        {
            if (!tenant.IsResolved) return ApiProblem.ValidationError("No tenant context.");
            var repo = new PoolsRepository(db.ConnectionString);
            var deleted = await repo.SoftDeleteAsnBlockAsync(id, tenant.TenantId, UserIdOrNull(ctx));
            return deleted ? Results.NoContent() : ApiProblem.NotFound($"ASN block {id} not found");
        }).RequireAuthorization(P.NetPoolsDelete);

        g.MapGet("/asn-allocations", async (Guid? blockId, ITenantContext tenant, DbConnectionFactory db) =>
        {
            if (!tenant.IsResolved) return ApiProblem.ValidationError("No tenant context.");
            var repo = new PoolsRepository(db.ConnectionString);
            return Results.Ok(await repo.ListAsnAllocationsAsync(tenant.TenantId, blockId));
        }).RequireAuthorization(P.NetPoolsRead);

        g.MapPost("/asn-allocations", async (AllocateAsnRequest req, ITenantContext tenant,
            DbConnectionFactory db, HttpContext ctx) =>
        {
            if (!tenant.IsResolved) return ApiProblem.ValidationError("No tenant context.");
            if (string.IsNullOrWhiteSpace(req.AllocatedToType))
                return ApiProblem.ValidationError("allocatedToType is required.");

            var svc = new AllocationService(db.ConnectionString);
            try
            {
                var alloc = await svc.AllocateAsnAsync(
                    req.BlockId, tenant.TenantId,
                    req.AllocatedToType, req.AllocatedToId,
                    UserIdOrNull(ctx));
                return Results.Created($"/api/net/asn-allocations/{alloc.Id}", alloc);
            }
            catch (PoolExhaustedException ex) { return ApiProblem.Conflict(ex.Message); }
            catch (AllocationContainerNotFoundException ex) { return ApiProblem.NotFound(ex.Message); }
        }).RequireAuthorization(P.NetPoolsAllocate);

        // ═══════════════════════════════════════════════════════════════
        // IP pools + subnets + addresses
        // ═══════════════════════════════════════════════════════════════

        g.MapGet("/ip-pools", async (ITenantContext tenant, DbConnectionFactory db) =>
        {
            if (!tenant.IsResolved) return ApiProblem.ValidationError("No tenant context.");
            var repo = new PoolsRepository(db.ConnectionString);
            return Results.Ok(await repo.ListIpPoolsAsync(tenant.TenantId));
        }).RequireAuthorization(P.NetPoolsRead);

        g.MapGet("/ip-pools/{id:guid}", async (Guid id, ITenantContext tenant, DbConnectionFactory db) =>
        {
            if (!tenant.IsResolved) return ApiProblem.ValidationError("No tenant context.");
            var repo = new PoolsRepository(db.ConnectionString);
            var e = await repo.GetIpPoolAsync(id, tenant.TenantId);
            return e is null ? ApiProblem.NotFound($"IP pool {id} not found") : Results.Ok(e);
        }).RequireAuthorization(P.NetPoolsRead);

        g.MapPost("/ip-pools", async (IpPool body, ITenantContext tenant, DbConnectionFactory db, HttpContext ctx) =>
        {
            if (!tenant.IsResolved) return ApiProblem.ValidationError("No tenant context.");
            body.OrganizationId = tenant.TenantId;
            var repo = new PoolsRepository(db.ConnectionString);
            try
            {
                var id = await repo.CreateIpPoolAsync(body, UserIdOrNull(ctx));
                return Results.Created($"/api/net/ip-pools/{id}", new { id });
            }
            catch (Npgsql.PostgresException ex) when (ex.SqlState == "23505")
            {
                return ApiProblem.Conflict($"IP pool code '{body.PoolCode}' already exists.");
            }
        }).RequireAuthorization(P.NetPoolsWrite);

        g.MapPut("/ip-pools/{id:guid}", async (Guid id, IpPool body, ITenantContext tenant, DbConnectionFactory db, HttpContext ctx) =>
        {
            if (!tenant.IsResolved) return ApiProblem.ValidationError("No tenant context.");
            body.Id = id;
            body.OrganizationId = tenant.TenantId;
            var repo = new PoolsRepository(db.ConnectionString);
            try
            {
                var v = await repo.UpdateIpPoolAsync(body, UserIdOrNull(ctx));
                return Results.Ok(new { id, version = v });
            }
            catch (ConcurrencyException ex) { return ApiProblem.Conflict(ex.Message); }
        }).RequireAuthorization(P.NetPoolsWrite);

        g.MapDelete("/ip-pools/{id:guid}", async (Guid id, ITenantContext tenant, DbConnectionFactory db, HttpContext ctx) =>
        {
            if (!tenant.IsResolved) return ApiProblem.ValidationError("No tenant context.");
            var repo = new PoolsRepository(db.ConnectionString);
            var deleted = await repo.SoftDeleteIpPoolAsync(id, tenant.TenantId, UserIdOrNull(ctx));
            return deleted ? Results.NoContent() : ApiProblem.NotFound($"IP pool {id} not found");
        }).RequireAuthorization(P.NetPoolsDelete);

        g.MapGet("/subnets", async (Guid? poolId, ITenantContext tenant, DbConnectionFactory db) =>
        {
            if (!tenant.IsResolved) return ApiProblem.ValidationError("No tenant context.");
            var repo = new PoolsRepository(db.ConnectionString);
            return Results.Ok(await repo.ListSubnetsAsync(tenant.TenantId, poolId));
        }).RequireAuthorization(P.NetPoolsRead);

        g.MapGet("/subnets/{id:guid}", async (Guid id, ITenantContext tenant, DbConnectionFactory db) =>
        {
            if (!tenant.IsResolved) return ApiProblem.ValidationError("No tenant context.");
            var repo = new PoolsRepository(db.ConnectionString);
            var e = await repo.GetSubnetAsync(id, tenant.TenantId);
            return e is null ? ApiProblem.NotFound($"Subnet {id} not found") : Results.Ok(e);
        }).RequireAuthorization(P.NetPoolsRead);

        g.MapPut("/subnets/{id:guid}", async (Guid id, Subnet body, ITenantContext tenant, DbConnectionFactory db, HttpContext ctx) =>
        {
            if (!tenant.IsResolved) return ApiProblem.ValidationError("No tenant context.");
            body.Id = id;
            body.OrganizationId = tenant.TenantId;
            var repo = new PoolsRepository(db.ConnectionString);
            try
            {
                var v = await repo.UpdateSubnetAsync(body, UserIdOrNull(ctx));
                return Results.Ok(new { id, version = v });
            }
            catch (ConcurrencyException ex) { return ApiProblem.Conflict(ex.Message); }
        }).RequireAuthorization(P.NetPoolsWrite);

        g.MapDelete("/subnets/{id:guid}", async (Guid id, ITenantContext tenant, DbConnectionFactory db, HttpContext ctx) =>
        {
            if (!tenant.IsResolved) return ApiProblem.ValidationError("No tenant context.");
            var repo = new PoolsRepository(db.ConnectionString);
            var deleted = await repo.SoftDeleteSubnetAsync(id, tenant.TenantId, UserIdOrNull(ctx));
            return deleted ? Results.NoContent() : ApiProblem.NotFound($"Subnet {id} not found");
        }).RequireAuthorization(P.NetPoolsDelete);

        // Subnet creation goes through the carver so overlap invariants hold.
        g.MapPost("/subnets/carve", async (CarveSubnetRequest req, ITenantContext tenant,
            DbConnectionFactory db, HttpContext ctx) =>
        {
            if (!tenant.IsResolved) return ApiProblem.ValidationError("No tenant context.");
            if (string.IsNullOrWhiteSpace(req.SubnetCode) || string.IsNullOrWhiteSpace(req.DisplayName))
                return ApiProblem.ValidationError("subnetCode and displayName are required.");
            if (!Enum.TryParse<PoolScopeLevel>(req.ScopeLevel ?? "Free", out var scope))
                return ApiProblem.ValidationError($"Invalid scope level '{req.ScopeLevel}'.");

            var svc = new IpAllocationService(db.ConnectionString);
            try
            {
                var subnet = await svc.AllocateSubnetAsync(
                    req.PoolId, tenant.TenantId,
                    req.PrefixLength, req.SubnetCode, req.DisplayName,
                    scope, req.ScopeEntityId, req.ParentSubnetId,
                    UserIdOrNull(ctx));
                return Results.Created($"/api/net/subnets/{subnet.Id}", subnet);
            }
            catch (PoolExhaustedException ex) { return ApiProblem.Conflict(ex.Message); }
            catch (AllocationContainerNotFoundException ex) { return ApiProblem.NotFound(ex.Message); }
            catch (AllocationRangeException ex) { return ApiProblem.ValidationError(ex.Message); }
        }).RequireAuthorization(P.NetPoolsAllocate);

        g.MapGet("/ip-addresses", async (Guid? subnetId, ITenantContext tenant, DbConnectionFactory db) =>
        {
            if (!tenant.IsResolved) return ApiProblem.ValidationError("No tenant context.");
            var repo = new PoolsRepository(db.ConnectionString);
            return Results.Ok(await repo.ListIpAddressesAsync(tenant.TenantId, subnetId));
        }).RequireAuthorization(P.NetPoolsRead);

        g.MapPost("/ip-addresses", async (AllocateIpRequest req, ITenantContext tenant,
            DbConnectionFactory db, HttpContext ctx) =>
        {
            if (!tenant.IsResolved) return ApiProblem.ValidationError("No tenant context.");
            var svc = new IpAllocationService(db.ConnectionString);
            try
            {
                var addr = await svc.AllocateNextIpAsync(
                    req.SubnetId, tenant.TenantId,
                    req.AssignedToType, req.AssignedToId,
                    UserIdOrNull(ctx));
                return Results.Created($"/api/net/ip-addresses/{addr.Id}", addr);
            }
            catch (PoolExhaustedException ex) { return ApiProblem.Conflict(ex.Message); }
            catch (AllocationContainerNotFoundException ex) { return ApiProblem.NotFound(ex.Message); }
        }).RequireAuthorization(P.NetPoolsAllocate);

        // ═══════════════════════════════════════════════════════════════
        // VLAN — template + pool + block + allocation
        // ═══════════════════════════════════════════════════════════════

        g.MapGet("/vlan-templates", async (ITenantContext tenant, DbConnectionFactory db) =>
        {
            if (!tenant.IsResolved) return ApiProblem.ValidationError("No tenant context.");
            var repo = new PoolsRepository(db.ConnectionString);
            return Results.Ok(await repo.ListVlanTemplatesAsync(tenant.TenantId));
        }).RequireAuthorization(P.NetPoolsRead);

        g.MapGet("/vlan-templates/{id:guid}", async (Guid id, ITenantContext tenant, DbConnectionFactory db) =>
        {
            if (!tenant.IsResolved) return ApiProblem.ValidationError("No tenant context.");
            var repo = new PoolsRepository(db.ConnectionString);
            var e = await repo.GetVlanTemplateAsync(id, tenant.TenantId);
            return e is null ? ApiProblem.NotFound($"VLAN template {id} not found") : Results.Ok(e);
        }).RequireAuthorization(P.NetPoolsRead);

        g.MapGet("/vlan-pools", async (ITenantContext tenant, DbConnectionFactory db) =>
        {
            if (!tenant.IsResolved) return ApiProblem.ValidationError("No tenant context.");
            var repo = new PoolsRepository(db.ConnectionString);
            return Results.Ok(await repo.ListVlanPoolsAsync(tenant.TenantId));
        }).RequireAuthorization(P.NetPoolsRead);

        g.MapGet("/vlan-pools/{id:guid}", async (Guid id, ITenantContext tenant, DbConnectionFactory db) =>
        {
            if (!tenant.IsResolved) return ApiProblem.ValidationError("No tenant context.");
            var repo = new PoolsRepository(db.ConnectionString);
            var e = await repo.GetVlanPoolAsync(id, tenant.TenantId);
            return e is null ? ApiProblem.NotFound($"VLAN pool {id} not found") : Results.Ok(e);
        }).RequireAuthorization(P.NetPoolsRead);

        g.MapGet("/vlan-blocks", async (Guid? poolId, ITenantContext tenant, DbConnectionFactory db) =>
        {
            if (!tenant.IsResolved) return ApiProblem.ValidationError("No tenant context.");
            var repo = new PoolsRepository(db.ConnectionString);
            return Results.Ok(await repo.ListVlanBlocksAsync(tenant.TenantId, poolId));
        }).RequireAuthorization(P.NetPoolsRead);

        g.MapGet("/vlans", async (Guid? blockId, ITenantContext tenant, DbConnectionFactory db) =>
        {
            if (!tenant.IsResolved) return ApiProblem.ValidationError("No tenant context.");
            var repo = new PoolsRepository(db.ConnectionString);
            return Results.Ok(await repo.ListVlansAsync(tenant.TenantId, blockId));
        }).RequireAuthorization(P.NetPoolsRead);

        g.MapPost("/vlans", async (AllocateVlanRequest req, ITenantContext tenant,
            DbConnectionFactory db, HttpContext ctx) =>
        {
            if (!tenant.IsResolved) return ApiProblem.ValidationError("No tenant context.");
            if (string.IsNullOrWhiteSpace(req.DisplayName))
                return ApiProblem.ValidationError("displayName is required.");
            if (!Enum.TryParse<PoolScopeLevel>(req.ScopeLevel ?? "Free", out var scope))
                return ApiProblem.ValidationError($"Invalid scope level '{req.ScopeLevel}'.");

            var svc = new AllocationService(db.ConnectionString);
            try
            {
                var vlan = await svc.AllocateVlanAsync(
                    req.BlockId, tenant.TenantId,
                    req.DisplayName, req.Description,
                    scope, req.ScopeEntityId, req.TemplateId,
                    UserIdOrNull(ctx));
                return Results.Created($"/api/net/vlans/{vlan.Id}", vlan);
            }
            catch (PoolExhaustedException ex) { return ApiProblem.Conflict(ex.Message); }
            catch (AllocationContainerNotFoundException ex) { return ApiProblem.NotFound(ex.Message); }
        }).RequireAuthorization(P.NetPoolsAllocate);

        // ═══════════════════════════════════════════════════════════════
        // MLAG
        // ═══════════════════════════════════════════════════════════════

        g.MapGet("/mlag-pools", async (ITenantContext tenant, DbConnectionFactory db) =>
        {
            if (!tenant.IsResolved) return ApiProblem.ValidationError("No tenant context.");
            var repo = new PoolsRepository(db.ConnectionString);
            return Results.Ok(await repo.ListMlagPoolsAsync(tenant.TenantId));
        }).RequireAuthorization(P.NetPoolsRead);

        g.MapGet("/mlag-pools/{id:guid}", async (Guid id, ITenantContext tenant, DbConnectionFactory db) =>
        {
            if (!tenant.IsResolved) return ApiProblem.ValidationError("No tenant context.");
            var repo = new PoolsRepository(db.ConnectionString);
            var e = await repo.GetMlagPoolAsync(id, tenant.TenantId);
            return e is null ? ApiProblem.NotFound($"MLAG pool {id} not found") : Results.Ok(e);
        }).RequireAuthorization(P.NetPoolsRead);

        g.MapGet("/mlag-domains", async (Guid? poolId, ITenantContext tenant, DbConnectionFactory db) =>
        {
            if (!tenant.IsResolved) return ApiProblem.ValidationError("No tenant context.");
            var repo = new PoolsRepository(db.ConnectionString);
            return Results.Ok(await repo.ListMlagDomainsAsync(tenant.TenantId, poolId));
        }).RequireAuthorization(P.NetPoolsRead);

        g.MapPost("/mlag-domains", async (AllocateMlagRequest req, ITenantContext tenant,
            DbConnectionFactory db, HttpContext ctx) =>
        {
            if (!tenant.IsResolved) return ApiProblem.ValidationError("No tenant context.");
            if (string.IsNullOrWhiteSpace(req.DisplayName))
                return ApiProblem.ValidationError("displayName is required.");
            if (!Enum.TryParse<PoolScopeLevel>(req.ScopeLevel ?? "Building", out var scope))
                return ApiProblem.ValidationError($"Invalid scope level '{req.ScopeLevel}'.");

            var svc = new AllocationService(db.ConnectionString);
            try
            {
                var dom = await svc.AllocateMlagDomainAsync(
                    req.PoolId, tenant.TenantId, req.DisplayName,
                    scope, req.ScopeEntityId, UserIdOrNull(ctx));
                return Results.Created($"/api/net/mlag-domains/{dom.Id}", dom);
            }
            catch (PoolExhaustedException ex) { return ApiProblem.Conflict(ex.Message); }
            catch (AllocationContainerNotFoundException ex) { return ApiProblem.NotFound(ex.Message); }
        }).RequireAuthorization(P.NetPoolsAllocate);

        // ═══════════════════════════════════════════════════════════════
        // MSTP (rules + steps + allocations — reads only here; writes land
        //       with the MSTP policy editor in a later phase)
        // ═══════════════════════════════════════════════════════════════

        g.MapGet("/mstp-rules", async (ITenantContext tenant, DbConnectionFactory db) =>
        {
            if (!tenant.IsResolved) return ApiProblem.ValidationError("No tenant context.");
            var repo = new PoolsRepository(db.ConnectionString);
            return Results.Ok(await repo.ListMstpRulesAsync(tenant.TenantId));
        }).RequireAuthorization(P.NetPoolsRead);

        g.MapGet("/mstp-rules/{id:guid}", async (Guid id, ITenantContext tenant, DbConnectionFactory db) =>
        {
            if (!tenant.IsResolved) return ApiProblem.ValidationError("No tenant context.");
            var repo = new PoolsRepository(db.ConnectionString);
            var e = await repo.GetMstpRuleAsync(id, tenant.TenantId);
            return e is null ? ApiProblem.NotFound($"MSTP rule {id} not found") : Results.Ok(e);
        }).RequireAuthorization(P.NetPoolsRead);

        g.MapPost("/mstp-rules", async (MstpPriorityRule body, ITenantContext tenant, DbConnectionFactory db, HttpContext ctx) =>
        {
            if (!tenant.IsResolved) return ApiProblem.ValidationError("No tenant context.");
            body.OrganizationId = tenant.TenantId;
            var repo = new PoolsRepository(db.ConnectionString);
            try
            {
                var id = await repo.CreateMstpRuleAsync(body, UserIdOrNull(ctx));
                return Results.Created($"/api/net/mstp-rules/{id}", new { id });
            }
            catch (Npgsql.PostgresException ex) when (ex.SqlState == "23505")
            {
                return ApiProblem.Conflict($"MSTP rule code '{body.RuleCode}' already exists.");
            }
        }).RequireAuthorization(P.NetPoolsWrite);

        g.MapPut("/mstp-rules/{id:guid}", async (Guid id, MstpPriorityRule body, ITenantContext tenant, DbConnectionFactory db, HttpContext ctx) =>
        {
            if (!tenant.IsResolved) return ApiProblem.ValidationError("No tenant context.");
            body.Id = id;
            body.OrganizationId = tenant.TenantId;
            var repo = new PoolsRepository(db.ConnectionString);
            try
            {
                var v = await repo.UpdateMstpRuleAsync(body, UserIdOrNull(ctx));
                return Results.Ok(new { id, version = v });
            }
            catch (ConcurrencyException ex) { return ApiProblem.Conflict(ex.Message); }
        }).RequireAuthorization(P.NetPoolsWrite);

        g.MapDelete("/mstp-rules/{id:guid}", async (Guid id, ITenantContext tenant, DbConnectionFactory db, HttpContext ctx) =>
        {
            if (!tenant.IsResolved) return ApiProblem.ValidationError("No tenant context.");
            var repo = new PoolsRepository(db.ConnectionString);
            var deleted = await repo.SoftDeleteMstpRuleAsync(id, tenant.TenantId, UserIdOrNull(ctx));
            return deleted ? Results.NoContent() : ApiProblem.NotFound($"MSTP rule {id} not found");
        }).RequireAuthorization(P.NetPoolsDelete);

        g.MapGet("/mstp-rules/{ruleId:guid}/steps", async (Guid ruleId, ITenantContext tenant, DbConnectionFactory db) =>
        {
            if (!tenant.IsResolved) return ApiProblem.ValidationError("No tenant context.");
            var repo = new PoolsRepository(db.ConnectionString);
            return Results.Ok(await repo.ListMstpStepsAsync(tenant.TenantId, ruleId));
        }).RequireAuthorization(P.NetPoolsRead);

        g.MapPost("/mstp-rules/{ruleId:guid}/steps", async (Guid ruleId, MstpPriorityRuleStep body,
            ITenantContext tenant, DbConnectionFactory db, HttpContext ctx) =>
        {
            if (!tenant.IsResolved) return ApiProblem.ValidationError("No tenant context.");
            body.OrganizationId = tenant.TenantId;
            body.RuleId = ruleId;
            if (body.Priority % 4096 != 0 || body.Priority is < 0 or > 61440)
                return ApiProblem.ValidationError("Priority must be 0..61440 and divisible by 4096.");

            var repo = new PoolsRepository(db.ConnectionString);
            try
            {
                var id = await repo.CreateMstpStepAsync(body, UserIdOrNull(ctx));
                return Results.Created($"/api/net/mstp-steps/{id}", new { id });
            }
            catch (Npgsql.PostgresException ex) when (ex.SqlState == "23505")
            {
                return ApiProblem.Conflict($"Step order {body.StepOrder} already exists for this rule.");
            }
        }).RequireAuthorization(P.NetPoolsWrite);

        g.MapPut("/mstp-steps/{id:guid}", async (Guid id, MstpPriorityRuleStep body,
            ITenantContext tenant, DbConnectionFactory db, HttpContext ctx) =>
        {
            if (!tenant.IsResolved) return ApiProblem.ValidationError("No tenant context.");
            body.Id = id;
            body.OrganizationId = tenant.TenantId;
            if (body.Priority % 4096 != 0 || body.Priority is < 0 or > 61440)
                return ApiProblem.ValidationError("Priority must be 0..61440 and divisible by 4096.");

            var repo = new PoolsRepository(db.ConnectionString);
            try
            {
                var v = await repo.UpdateMstpStepAsync(body, UserIdOrNull(ctx));
                return Results.Ok(new { id, version = v });
            }
            catch (ConcurrencyException ex) { return ApiProblem.Conflict(ex.Message); }
        }).RequireAuthorization(P.NetPoolsWrite);

        g.MapDelete("/mstp-steps/{id:guid}", async (Guid id, ITenantContext tenant, DbConnectionFactory db, HttpContext ctx) =>
        {
            if (!tenant.IsResolved) return ApiProblem.ValidationError("No tenant context.");
            var repo = new PoolsRepository(db.ConnectionString);
            var deleted = await repo.SoftDeleteMstpStepAsync(id, tenant.TenantId, UserIdOrNull(ctx));
            return deleted ? Results.NoContent() : ApiProblem.NotFound($"MSTP step {id} not found");
        }).RequireAuthorization(P.NetPoolsDelete);

        g.MapGet("/mstp-allocations", async (Guid? ruleId, ITenantContext tenant, DbConnectionFactory db) =>
        {
            if (!tenant.IsResolved) return ApiProblem.ValidationError("No tenant context.");
            var repo = new PoolsRepository(db.ConnectionString);
            return Results.Ok(await repo.ListMstpAllocationsAsync(tenant.TenantId, ruleId));
        }).RequireAuthorization(P.NetPoolsRead);

        // ═══════════════════════════════════════════════════════════════
        // Reservation shelf
        // ═══════════════════════════════════════════════════════════════

        g.MapGet("/shelf", async (string? resourceType, ITenantContext tenant, DbConnectionFactory db) =>
        {
            if (!tenant.IsResolved) return ApiProblem.ValidationError("No tenant context.");
            ShelfResourceType? rt = null;
            if (!string.IsNullOrEmpty(resourceType))
            {
                if (!Enum.TryParse<ShelfResourceType>(NormaliseShelfType(resourceType), out var parsed))
                    return ApiProblem.ValidationError($"Invalid resourceType '{resourceType}'.");
                rt = parsed;
            }
            var repo = new PoolsRepository(db.ConnectionString);
            return Results.Ok(await repo.ListShelfEntriesAsync(tenant.TenantId, rt));
        }).RequireAuthorization(P.NetPoolsRead);

        g.MapPost("/shelf/retire", async (RetireRequest req, ITenantContext tenant,
            DbConnectionFactory db, HttpContext ctx) =>
        {
            if (!tenant.IsResolved) return ApiProblem.ValidationError("No tenant context.");
            if (string.IsNullOrWhiteSpace(req.ResourceKey))
                return ApiProblem.ValidationError("resourceKey is required.");
            if (!Enum.TryParse<ShelfResourceType>(NormaliseShelfType(req.ResourceType), out var rt))
                return ApiProblem.ValidationError($"Invalid resourceType '{req.ResourceType}'.");
            if (req.CooldownDays < 0)
                return ApiProblem.ValidationError("cooldownDays must be non-negative.");

            var svc = new AllocationService(db.ConnectionString);
            try
            {
                var entry = await svc.RetireAsync(
                    tenant.TenantId, rt, req.ResourceKey,
                    TimeSpan.FromDays(req.CooldownDays),
                    req.PoolId, req.BlockId, req.Reason,
                    UserIdOrNull(ctx));
                return Results.Created($"/api/net/shelf/{entry.Id}", entry);
            }
            catch (ArgumentOutOfRangeException ex) { return ApiProblem.ValidationError(ex.Message); }
        }).RequireAuthorization(P.NetPoolsAllocate);

        return g;
    }

    private static int? UserIdOrNull(HttpContext ctx)
    {
        var uid = ctx.User.FindFirst("sub")?.Value ?? ctx.User.FindFirst("user_id")?.Value;
        return int.TryParse(uid, out var i) ? i : null;
    }

    /// <summary>
    /// Accept both the lowercase DB form ("asn") and the C#-enum form
    /// ("Asn") so callers don't have to care.
    /// </summary>
    private static string NormaliseShelfType(string? s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return char.ToUpperInvariant(s[0]) + s[1..].ToLowerInvariant();
    }

    // ═══════════════════════════════════════════════════════════════════
    // Request DTOs
    // ═══════════════════════════════════════════════════════════════════

    public record AllocateAsnRequest(
        Guid BlockId,
        string AllocatedToType,
        Guid AllocatedToId);

    public record AllocateVlanRequest(
        Guid BlockId,
        string DisplayName,
        string? Description,
        string? ScopeLevel,
        Guid? ScopeEntityId,
        Guid? TemplateId);

    public record AllocateMlagRequest(
        Guid PoolId,
        string DisplayName,
        string? ScopeLevel,
        Guid? ScopeEntityId);

    public record AllocateIpRequest(
        Guid SubnetId,
        string? AssignedToType,
        Guid? AssignedToId);

    public record CarveSubnetRequest(
        Guid PoolId,
        int PrefixLength,
        string SubnetCode,
        string DisplayName,
        string? ScopeLevel,
        Guid? ScopeEntityId,
        Guid? ParentSubnetId);

    public record RetireRequest(
        string ResourceType,
        string ResourceKey,
        int CooldownDays,
        Guid? PoolId,
        Guid? BlockId,
        string? Reason);
}
