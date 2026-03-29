using System.Collections.Concurrent;
using System.Security.Claims;
using Central.Data;
using Central.Data.Repositories;

namespace Central.Api.Auth;

public static class AuthEndpoints
{
    // Account lockout: 5 failed attempts in 15 minutes = 15 minute lockout
    private static readonly ConcurrentDictionary<string, (int Attempts, DateTime FirstAttempt)> _failedLogins = new();
    private const int MaxAttempts = 5;
    private static readonly TimeSpan LockoutWindow = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    public static RouteGroupBuilder MapAuthEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/login", async (LoginRequest req, TokenService tokens, DbConnectionFactory db) =>
        {
            if (string.IsNullOrWhiteSpace(req.Username))
                return Results.BadRequest(new { error = "Username required" });

            var key = req.Username.ToLowerInvariant();

            // Check lockout
            if (_failedLogins.TryGetValue(key, out var state))
            {
                if (state.Attempts >= MaxAttempts && DateTime.UtcNow - state.FirstAttempt < LockoutDuration)
                    return Results.Json(new { error = $"Account locked. Try again in {(int)(LockoutDuration - (DateTime.UtcNow - state.FirstAttempt)).TotalMinutes} minutes." },
                        statusCode: 429);

                // Reset if window expired
                if (DateTime.UtcNow - state.FirstAttempt > LockoutWindow)
                    _failedLogins.TryRemove(key, out _);
            }

            var permRepo = new PermissionRepository(db.ConnectionString);
            var user = await permRepo.GetUserByUsernameAsync(req.Username);

            if (user == null)
            {
                // Record failed attempt
                _failedLogins.AddOrUpdate(key,
                    _ => (1, DateTime.UtcNow),
                    (_, prev) => (prev.Attempts + 1, prev.FirstAttempt));
                return Results.Unauthorized();
            }

            // Success — clear failed attempts
            _failedLogins.TryRemove(key, out _);

            // Update last login
            try
            {
                await using var conn = await db.OpenConnectionAsync();
                await using var cmd = new Npgsql.NpgsqlCommand(
                    "UPDATE app_users SET last_login_at=NOW(), login_count=COALESCE(login_count,0)+1 WHERE username=@u", conn);
                cmd.Parameters.AddWithValue("u", req.Username);
                await cmd.ExecuteNonQueryAsync();
            }
            catch { /* best-effort login tracking */ }

            var permissions = await permRepo.GetPermissionCodesForRoleAsync(user.RoleName);

            // Resolve tenant memberships from platform schema
            Guid? tenantId = null;
            string? tenantSlug = null;
            string? tenantTier = null;
            List<TenantInfo>? availableTenants = null;

            try
            {
                await using var conn = await db.OpenConnectionAsync();
                await using var tenantCmd = new Npgsql.NpgsqlCommand(
                    @"SELECT t.id, t.slug, t.tier, t.display_name
                      FROM central_platform.tenant_memberships m
                      JOIN central_platform.global_users g ON g.id = m.user_id
                      JOIN central_platform.tenants t ON t.id = m.tenant_id
                      WHERE LOWER(g.email) = LOWER(@u) OR LOWER(g.display_name) = LOWER(@u)
                      ORDER BY m.joined_at", conn);
                tenantCmd.Parameters.AddWithValue("u", req.Username);
                await using var tr = await tenantCmd.ExecuteReaderAsync();
                var tenants = new List<TenantInfo>();
                while (await tr.ReadAsync())
                    tenants.Add(new TenantInfo(tr.GetGuid(0), tr.GetString(1), tr.GetString(2), tr.GetString(3)));

                if (tenants.Count == 1)
                {
                    tenantId = tenants[0].Id;
                    tenantSlug = tenants[0].Slug;
                    tenantTier = tenants[0].Tier;
                }
                else if (tenants.Count > 1)
                {
                    // If tenant_slug was provided in the request, select it
                    var requested = req.TenantSlug;
                    var match = !string.IsNullOrEmpty(requested)
                        ? tenants.FirstOrDefault(t => t.Slug == requested) : null;
                    if (match != null)
                    {
                        tenantId = match.Id;
                        tenantSlug = match.Slug;
                        tenantTier = match.Tier;
                    }
                    else
                    {
                        availableTenants = tenants;
                    }
                }
            }
            catch { /* platform schema may not exist yet — single-tenant fallback */ }

            // Check global admin status
            var isGlobalAdmin = false;
            try
            {
                await using var gaConn = await db.OpenConnectionAsync();
                await using var gaCmd = new Npgsql.NpgsqlCommand(
                    "SELECT is_global_admin FROM central_platform.global_users WHERE LOWER(email) = LOWER(@u) OR LOWER(display_name) = LOWER(@u)", gaConn);
                gaCmd.Parameters.AddWithValue("u", req.Username);
                var gaResult = await gaCmd.ExecuteScalarAsync();
                if (gaResult is bool ga) isGlobalAdmin = ga;
            }
            catch { /* platform schema may not exist */ }

            // Default tenant for single-tenant / legacy users
            tenantSlug ??= "default";
            tenantTier ??= "enterprise";

            var token = tokens.GenerateToken(user.Username, user.RoleName, permissions,
                tenantId, tenantSlug, tenantTier, isGlobalAdmin);

            return Results.Ok(new
            {
                token,
                username = user.Username,
                role = user.RoleName,
                permissions = permissions.ToArray(),
                is_global_admin = isGlobalAdmin,
                tenant = new { id = tenantId, slug = tenantSlug, tier = tenantTier },
                available_tenants = availableTenants
            });
        })
        .AllowAnonymous();

        // GET /api/auth/me — returns current user info from JWT claims
        group.MapGet("/me", (ClaimsPrincipal user) =>
        {
            var name = user.Identity?.Name ?? "";
            var role = user.FindFirst("role")?.Value ?? "";
            var perms = user.FindAll("perm").Select(c => c.Value).ToArray();
            var tenantSlug = user.FindFirst("tenant_slug")?.Value;
            var tenantTier = user.FindFirst("tenant_tier")?.Value;
            return Results.Ok(new { username = name, role, permissions = perms, tenant_slug = tenantSlug, tenant_tier = tenantTier });
        })
        .RequireAuthorization();

        return group;
    }

    private record LoginRequest(string Username, string? TenantSlug = null);
    private record TenantInfo(Guid Id, string Slug, string Tier, string DisplayName);
}
