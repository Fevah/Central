using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Central.Data;
using Central.Tenancy;
using Npgsql;

namespace Central.Api.Endpoints;

/// <summary>
/// Platform-level admin endpoints for managing tenants, global users, subscriptions,
/// and module licenses. Protected by is_global_admin JWT claim.
/// </summary>
public static class GlobalAdminEndpoints
{
    public static RouteGroupBuilder MapGlobalAdminEndpoints(this RouteGroupBuilder group)
    {
        // ══════════════════════════════════════════════════════════════════
        // TENANTS
        // ══════════════════════════════════════════════════════════════════

        // GET /tenants — list all tenants with user and device counts
        group.MapGet("/tenants", async (DbConnectionFactory db) =>
        {
            await using var conn = new NpgsqlConnection(db.ConnectionString);
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand(
                @"SELECT t.id, t.slug, t.display_name, t.domain, t.tier, t.is_active,
                         t.created_at, t.updated_at,
                         (SELECT COUNT(*) FROM central_platform.tenant_memberships m WHERE m.tenant_id = t.id) AS user_count,
                         sp.display_name AS plan_name
                  FROM central_platform.tenants t
                  LEFT JOIN central_platform.tenant_subscriptions ts ON ts.tenant_id = t.id AND ts.status IN ('active','trial')
                  LEFT JOIN central_platform.subscription_plans sp ON sp.id = ts.plan_id
                  ORDER BY t.created_at DESC", conn);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        // GET /tenants/{id} — single tenant detail
        group.MapGet("/tenants/{id:guid}", async (Guid id, DbConnectionFactory db) =>
        {
            await using var conn = new NpgsqlConnection(db.ConnectionString);
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand(
                @"SELECT t.id, t.slug, t.display_name, t.domain, t.tier, t.is_active,
                         t.metadata::text, t.created_at, t.updated_at
                  FROM central_platform.tenants t WHERE t.id = @id", conn);
            cmd.Parameters.AddWithValue("id", id);
            await using var r = await cmd.ExecuteReaderAsync();
            var rows = await EndpointHelpers.ReadRowsAsync(r);
            return rows.Count > 0 ? Results.Ok(rows[0]) : Results.NotFound();
        });

        // PUT /tenants/{id} — update tenant
        group.MapPut("/tenants/{id:guid}", async (Guid id, DbConnectionFactory db, JsonElement body) =>
        {
            await using var conn = new NpgsqlConnection(db.ConnectionString);
            await conn.OpenAsync();
            var sets = new List<string>();
            var cmd = new NpgsqlCommand { Connection = conn };

            if (body.TryGetProperty("display_name", out var dn))
            { sets.Add("display_name = @dn"); cmd.Parameters.AddWithValue("dn", dn.GetString() ?? ""); }
            if (body.TryGetProperty("domain", out var dom))
            { sets.Add("domain = @dom"); cmd.Parameters.AddWithValue("dom", (object?)dom.GetString() ?? DBNull.Value); }
            if (body.TryGetProperty("tier", out var tier))
            { sets.Add("tier = @tier"); cmd.Parameters.AddWithValue("tier", tier.GetString() ?? "free"); }
            if (body.TryGetProperty("metadata", out var meta))
            { sets.Add("metadata = @meta::jsonb"); cmd.Parameters.AddWithValue("meta", meta.GetRawText()); }

            if (sets.Count == 0) return Results.BadRequest(new { error = "No fields to update" });

            sets.Add("updated_at = NOW()");
            cmd.CommandText = $"UPDATE central_platform.tenants SET {string.Join(", ", sets)} WHERE id = @id";
            cmd.Parameters.AddWithValue("id", id);
            var rows = await cmd.ExecuteNonQueryAsync();
            return rows > 0 ? Results.Ok(new { updated = true }) : Results.NotFound();
        });

        // POST /tenants/{id}/suspend
        group.MapPost("/tenants/{id:guid}/suspend", async (Guid id, DbConnectionFactory db) =>
        {
            await using var conn = new NpgsqlConnection(db.ConnectionString);
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand(
                "UPDATE central_platform.tenants SET is_active = false, updated_at = NOW() WHERE id = @id", conn);
            cmd.Parameters.AddWithValue("id", id);
            var rows = await cmd.ExecuteNonQueryAsync();
            return rows > 0 ? Results.Ok(new { suspended = true }) : Results.NotFound();
        });

        // POST /tenants/{id}/activate
        group.MapPost("/tenants/{id:guid}/activate", async (Guid id, DbConnectionFactory db) =>
        {
            await using var conn = new NpgsqlConnection(db.ConnectionString);
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand(
                "UPDATE central_platform.tenants SET is_active = true, updated_at = NOW() WHERE id = @id", conn);
            cmd.Parameters.AddWithValue("id", id);
            var rows = await cmd.ExecuteNonQueryAsync();
            return rows > 0 ? Results.Ok(new { activated = true }) : Results.NotFound();
        });

        // DELETE /tenants/{id} — deprovision (drop schema + deactivate)
        group.MapDelete("/tenants/{id:guid}", async (Guid id, DbConnectionFactory db) =>
        {
            await using var conn = new NpgsqlConnection(db.ConnectionString);
            await conn.OpenAsync();

            // Get slug for schema name
            await using var slugCmd = new NpgsqlCommand(
                "SELECT slug FROM central_platform.tenants WHERE id = @id", conn);
            slugCmd.Parameters.AddWithValue("id", id);
            var slug = await slugCmd.ExecuteScalarAsync() as string;
            if (slug == null) return Results.NotFound();
            if (slug == "default") return Results.BadRequest(new { error = "Cannot deprovision the default tenant" });

            // Drop tenant schema
            var schemaManager = new TenantSchemaManager(db.ConnectionString);
            await schemaManager.DropTenantSchemaAsync($"tenant_{slug}");

            // Mark tenant inactive
            await using var deactivateCmd = new NpgsqlCommand(
                "UPDATE central_platform.tenants SET is_active = false, updated_at = NOW() WHERE id = @id", conn);
            deactivateCmd.Parameters.AddWithValue("id", id);
            await deactivateCmd.ExecuteNonQueryAsync();

            return Results.Ok(new { deprovisioned = true, slug });
        });

        // POST /tenants/{id}/provision — manually provision tenant schema
        group.MapPost("/tenants/{id:guid}/provision", async (Guid id, DbConnectionFactory db) =>
        {
            await using var conn = new NpgsqlConnection(db.ConnectionString);
            await conn.OpenAsync();
            await using var slugCmd = new NpgsqlCommand(
                "SELECT slug FROM central_platform.tenants WHERE id = @id", conn);
            slugCmd.Parameters.AddWithValue("id", id);
            var slug = await slugCmd.ExecuteScalarAsync() as string;
            if (slug == null) return Results.NotFound();

            var schemaManager = new TenantSchemaManager(db.ConnectionString);
            var migrationsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "migrations");
            await schemaManager.ProvisionTenantAsync($"tenant_{slug}", migrationsDir);

            return Results.Ok(new { provisioned = true, schema = $"tenant_{slug}" });
        });

        // ══════════════════════════════════════════════════════════════════
        // GLOBAL USERS
        // ══════════════════════════════════════════════════════════════════

        // GET /users — list all global users with membership counts
        group.MapGet("/users", async (DbConnectionFactory db) =>
        {
            await using var conn = new NpgsqlConnection(db.ConnectionString);
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand(
                @"SELECT u.id, u.email, u.display_name, u.email_verified, u.is_global_admin,
                         u.created_at,
                         (SELECT COUNT(*) FROM central_platform.tenant_memberships m WHERE m.user_id = u.id) AS tenant_count,
                         (SELECT string_agg(t.slug, ', ')
                          FROM central_platform.tenant_memberships m
                          JOIN central_platform.tenants t ON t.id = m.tenant_id
                          WHERE m.user_id = u.id) AS tenant_slugs
                  FROM central_platform.global_users u
                  ORDER BY u.created_at DESC", conn);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        // PUT /users/{id} — update global user
        group.MapPut("/users/{id:guid}", async (Guid id, DbConnectionFactory db, JsonElement body) =>
        {
            await using var conn = new NpgsqlConnection(db.ConnectionString);
            await conn.OpenAsync();
            var sets = new List<string>();
            var cmd = new NpgsqlCommand { Connection = conn };

            if (body.TryGetProperty("display_name", out var dn))
            { sets.Add("display_name = @dn"); cmd.Parameters.AddWithValue("dn", dn.GetString() ?? ""); }
            if (body.TryGetProperty("email", out var email))
            { sets.Add("email = @email"); cmd.Parameters.AddWithValue("email", email.GetString() ?? ""); }
            if (body.TryGetProperty("is_global_admin", out var ga))
            { sets.Add("is_global_admin = @ga"); cmd.Parameters.AddWithValue("ga", ga.GetBoolean()); }

            if (sets.Count == 0) return Results.BadRequest(new { error = "No fields to update" });

            cmd.CommandText = $"UPDATE central_platform.global_users SET {string.Join(", ", sets)} WHERE id = @id";
            cmd.Parameters.AddWithValue("id", id);
            var rows = await cmd.ExecuteNonQueryAsync();
            return rows > 0 ? Results.Ok(new { updated = true }) : Results.NotFound();
        });

        // POST /users/{id}/reset-password
        group.MapPost("/users/{id:guid}/reset-password", async (Guid id, DbConnectionFactory db, JsonElement body) =>
        {
            var newPassword = body.GetProperty("password").GetString() ?? "";
            if (newPassword.Length < 8)
                return Results.BadRequest(new { error = "Password must be at least 8 characters" });

            var salt = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));
            var hash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(newPassword + salt)));

            await using var conn = new NpgsqlConnection(db.ConnectionString);
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand(
                "UPDATE central_platform.global_users SET password_hash = @h, salt = @s WHERE id = @id", conn);
            cmd.Parameters.AddWithValue("h", hash);
            cmd.Parameters.AddWithValue("s", salt);
            cmd.Parameters.AddWithValue("id", id);
            var rows = await cmd.ExecuteNonQueryAsync();
            return rows > 0 ? Results.Ok(new { password_reset = true }) : Results.NotFound();
        });

        // DELETE /users/{id} — deactivate (remove all memberships)
        group.MapDelete("/users/{id:guid}", async (Guid id, DbConnectionFactory db) =>
        {
            await using var conn = new NpgsqlConnection(db.ConnectionString);
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand(
                "DELETE FROM central_platform.tenant_memberships WHERE user_id = @id", conn);
            cmd.Parameters.AddWithValue("id", id);
            await cmd.ExecuteNonQueryAsync();

            await using var delCmd = new NpgsqlCommand(
                "DELETE FROM central_platform.global_users WHERE id = @id", conn);
            delCmd.Parameters.AddWithValue("id", id);
            var rows = await delCmd.ExecuteNonQueryAsync();
            return rows > 0 ? Results.Ok(new { deleted = true }) : Results.NotFound();
        });

        // ══════════════════════════════════════════════════════════════════
        // SUBSCRIPTIONS
        // ══════════════════════════════════════════════════════════════════

        // GET /subscriptions — all subscriptions with plan + tenant info
        group.MapGet("/subscriptions", async (DbConnectionFactory db) =>
        {
            await using var conn = new NpgsqlConnection(db.ConnectionString);
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand(
                @"SELECT s.id, t.slug AS tenant_slug, t.display_name AS tenant_name,
                         p.tier, p.display_name AS plan_name, p.max_users, p.max_devices,
                         s.status, s.started_at, s.expires_at, s.stripe_sub_id
                  FROM central_platform.tenant_subscriptions s
                  JOIN central_platform.tenants t ON t.id = s.tenant_id
                  JOIN central_platform.subscription_plans p ON p.id = s.plan_id
                  ORDER BY s.started_at DESC", conn);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        // PUT /subscriptions/{id} — change plan, status, or expiry
        group.MapPut("/subscriptions/{id:int}", async (int id, DbConnectionFactory db, JsonElement body) =>
        {
            await using var conn = new NpgsqlConnection(db.ConnectionString);
            await conn.OpenAsync();
            var sets = new List<string>();
            var cmd = new NpgsqlCommand { Connection = conn };

            if (body.TryGetProperty("plan_tier", out var tier))
            {
                // Resolve plan_id from tier name
                await using var planCmd = new NpgsqlCommand(
                    "SELECT id FROM central_platform.subscription_plans WHERE tier = @t", conn);
                planCmd.Parameters.AddWithValue("t", tier.GetString() ?? "");
                var planId = await planCmd.ExecuteScalarAsync();
                if (planId != null)
                { sets.Add("plan_id = @pid"); cmd.Parameters.AddWithValue("pid", (int)planId); }
            }
            if (body.TryGetProperty("status", out var status))
            { sets.Add("status = @status"); cmd.Parameters.AddWithValue("status", status.GetString() ?? "active"); }
            if (body.TryGetProperty("expires_at", out var exp))
            {
                if (exp.ValueKind == JsonValueKind.Null)
                { sets.Add("expires_at = NULL"); }
                else
                { sets.Add("expires_at = @exp"); cmd.Parameters.AddWithValue("exp", DateTime.Parse(exp.GetString()!)); }
            }

            if (sets.Count == 0) return Results.BadRequest(new { error = "No fields to update" });

            cmd.CommandText = $"UPDATE central_platform.tenant_subscriptions SET {string.Join(", ", sets)} WHERE id = @id";
            cmd.Parameters.AddWithValue("id", id);
            var rows = await cmd.ExecuteNonQueryAsync();
            return rows > 0 ? Results.Ok(new { updated = true }) : Results.NotFound();
        });

        // ══════════════════════════════════════════════════════════════════
        // MODULE LICENSES
        // ══════════════════════════════════════════════════════════════════

        // GET /licenses — all module licenses across tenants
        group.MapGet("/licenses", async (DbConnectionFactory db) =>
        {
            await using var conn = new NpgsqlConnection(db.ConnectionString);
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand(
                @"SELECT l.id, t.slug AS tenant_slug, t.display_name AS tenant_name,
                         m.code AS module_code, m.display_name AS module_name, m.is_base,
                         l.granted_at, l.expires_at
                  FROM central_platform.tenant_module_licenses l
                  JOIN central_platform.tenants t ON t.id = l.tenant_id
                  JOIN central_platform.module_catalog m ON m.id = l.module_id
                  ORDER BY t.slug, m.code", conn);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        // POST /licenses — grant module to tenant
        group.MapPost("/licenses", async (DbConnectionFactory db, JsonElement body) =>
        {
            var tenantId = Guid.Parse(body.GetProperty("tenant_id").GetString() ?? "");
            var moduleCode = body.GetProperty("module_code").GetString() ?? "";
            DateTime? expiresAt = body.TryGetProperty("expires_at", out var exp) && exp.ValueKind != JsonValueKind.Null
                ? DateTime.Parse(exp.GetString()!)
                : null;

            await using var conn = new NpgsqlConnection(db.ConnectionString);
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand(
                @"INSERT INTO central_platform.tenant_module_licenses (tenant_id, module_id, expires_at)
                  SELECT @tid, id, @exp FROM central_platform.module_catalog WHERE code = @code
                  ON CONFLICT (tenant_id, module_id) DO UPDATE SET expires_at = @exp, granted_at = NOW()
                  RETURNING id", conn);
            cmd.Parameters.AddWithValue("tid", tenantId);
            cmd.Parameters.AddWithValue("code", moduleCode);
            cmd.Parameters.AddWithValue("exp", (object?)expiresAt ?? DBNull.Value);
            var id = await cmd.ExecuteScalarAsync();
            return Results.Ok(new { id, tenant_id = tenantId, module_code = moduleCode });
        });

        // DELETE /licenses/{id}
        group.MapDelete("/licenses/{id:int}", async (int id, DbConnectionFactory db) =>
        {
            await using var conn = new NpgsqlConnection(db.ConnectionString);
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand(
                "DELETE FROM central_platform.tenant_module_licenses WHERE id = @id", conn);
            cmd.Parameters.AddWithValue("id", id);
            var rows = await cmd.ExecuteNonQueryAsync();
            return rows > 0 ? Results.Ok(new { deleted = true }) : Results.NotFound();
        });

        // ══════════════════════════════════════════════════════════════════
        // PLATFORM DASHBOARD
        // ══════════════════════════════════════════════════════════════════

        group.MapGet("/dashboard", async (DbConnectionFactory db) =>
        {
            await using var conn = new NpgsqlConnection(db.ConnectionString);
            await conn.OpenAsync();

            // Aggregate metrics
            await using var cmd = new NpgsqlCommand(
                @"SELECT
                    (SELECT COUNT(*) FROM central_platform.tenants) AS total_tenants,
                    (SELECT COUNT(*) FROM central_platform.tenants WHERE is_active) AS active_tenants,
                    (SELECT COUNT(*) FROM central_platform.global_users) AS total_users,
                    (SELECT COUNT(*) FROM central_platform.global_users WHERE email_verified) AS verified_users,
                    (SELECT COUNT(*) FROM central_platform.tenant_subscriptions WHERE status IN ('active','trial')) AS active_subscriptions", conn);
            await using var r = await cmd.ExecuteReaderAsync();
            await r.ReadAsync();

            // Tier distribution
            await using var tierCmd = new NpgsqlCommand(
                @"SELECT t.tier, COUNT(*) AS count
                  FROM central_platform.tenants t
                  WHERE t.is_active GROUP BY t.tier ORDER BY t.tier", conn);

            // Module distribution
            await using var modCmd = new NpgsqlCommand(
                @"SELECT m.code, m.display_name, COUNT(l.id) AS licensed_count
                  FROM central_platform.module_catalog m
                  LEFT JOIN central_platform.tenant_module_licenses l ON l.module_id = m.id
                  GROUP BY m.id, m.code, m.display_name ORDER BY m.code", conn);

            return Results.Ok(new
            {
                total_tenants = r.GetInt64(0),
                active_tenants = r.GetInt64(1),
                total_users = r.GetInt64(2),
                verified_users = r.GetInt64(3),
                active_subscriptions = r.GetInt64(4)
            });
        });

        return group;
    }
}
