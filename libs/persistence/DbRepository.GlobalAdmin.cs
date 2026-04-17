using Central.Engine.Models;
using Npgsql;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Central.Persistence;

public partial class DbRepository
{
    // ── Global Admin — Typed queries ─────────────────────────────────────

    public async Task<List<TenantRecord>> GetTenantsTypedAsync()
    {
        var list = new List<TenantRecord>();
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            @"SELECT t.id, t.slug, t.display_name, t.domain, t.tier, t.is_active,
                     t.created_at, t.updated_at,
                     (SELECT COUNT(*) FROM central_platform.tenant_memberships m WHERE m.tenant_id = t.id)::int AS user_count,
                     sp.display_name AS plan_name
              FROM central_platform.tenants t
              LEFT JOIN central_platform.tenant_subscriptions ts ON ts.tenant_id = t.id AND ts.status IN ('active','trial')
              LEFT JOIN central_platform.subscription_plans sp ON sp.id = ts.plan_id
              ORDER BY t.created_at DESC", conn);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new TenantRecord
            {
                Id = r.GetGuid(0), Slug = r.GetString(1), DisplayName = r.GetString(2),
                Domain = r.IsDBNull(3) ? null : r.GetString(3), Tier = r.GetString(4),
                IsActive = r.GetBoolean(5), CreatedAt = r.GetDateTime(6), UpdatedAt = r.GetDateTime(7),
                UserCount = r.GetInt32(8), PlanName = r.IsDBNull(9) ? null : r.GetString(9)
            });
        return list;
    }

    public async Task<List<GlobalUserRecord>> GetGlobalUsersTypedAsync()
    {
        var list = new List<GlobalUserRecord>();
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            @"SELECT u.id, u.email, u.display_name, u.email_verified, u.is_global_admin,
                     u.created_at,
                     (SELECT COUNT(*) FROM central_platform.tenant_memberships m WHERE m.user_id = u.id)::int AS tenant_count,
                     (SELECT string_agg(t.slug, ', ')
                      FROM central_platform.tenant_memberships m
                      JOIN central_platform.tenants t ON t.id = m.tenant_id
                      WHERE m.user_id = u.id) AS tenant_slugs
              FROM central_platform.global_users u
              ORDER BY u.created_at DESC", conn);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new GlobalUserRecord
            {
                Id = r.GetGuid(0), Email = r.GetString(1),
                DisplayName = r.IsDBNull(2) ? null : r.GetString(2),
                EmailVerified = r.GetBoolean(3), IsGlobalAdmin = r.GetBoolean(4),
                CreatedAt = r.GetDateTime(5), TenantCount = r.GetInt32(6),
                TenantSlugs = r.IsDBNull(7) ? null : r.GetString(7)
            });
        return list;
    }

    public async Task<List<SubscriptionRecord>> GetSubscriptionsTypedAsync()
    {
        var list = new List<SubscriptionRecord>();
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            @"SELECT s.id, s.tenant_id, t.slug, t.display_name, p.tier, p.display_name AS plan_name,
                     p.max_users, p.max_devices, s.status, s.started_at, s.expires_at, s.stripe_sub_id
              FROM central_platform.tenant_subscriptions s
              JOIN central_platform.tenants t ON t.id = s.tenant_id
              JOIN central_platform.subscription_plans p ON p.id = s.plan_id
              ORDER BY s.started_at DESC", conn);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new SubscriptionRecord
            {
                Id = r.GetInt32(0), TenantId = r.GetGuid(1), TenantSlug = r.GetString(2),
                TenantName = r.GetString(3), Tier = r.GetString(4), PlanName = r.GetString(5),
                MaxUsers = r.IsDBNull(6) ? null : r.GetInt32(6),
                MaxDevices = r.IsDBNull(7) ? null : r.GetInt32(7),
                Status = r.GetString(8), StartedAt = r.GetDateTime(9),
                ExpiresAt = r.IsDBNull(10) ? null : r.GetDateTime(10),
                StripeSubId = r.IsDBNull(11) ? null : r.GetString(11)
            });
        return list;
    }

    public async Task<List<ModuleLicenseRecord>> GetLicensesTypedAsync()
    {
        var list = new List<ModuleLicenseRecord>();
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            @"SELECT l.id, l.tenant_id, t.slug, t.display_name,
                     l.module_id, m.code, m.display_name, m.is_base,
                     l.granted_at, l.expires_at
              FROM central_platform.tenant_module_licenses l
              JOIN central_platform.tenants t ON t.id = l.tenant_id
              JOIN central_platform.module_catalog m ON m.id = l.module_id
              ORDER BY t.slug, m.code", conn);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new ModuleLicenseRecord
            {
                Id = r.GetInt32(0), TenantId = r.GetGuid(1), TenantSlug = r.GetString(2),
                TenantName = r.GetString(3), ModuleId = r.GetInt32(4), ModuleCode = r.GetString(5),
                ModuleName = r.GetString(6), IsBase = r.GetBoolean(7),
                GrantedAt = r.GetDateTime(8), ExpiresAt = r.IsDBNull(9) ? null : r.GetDateTime(9)
            });
        return list;
    }

    // ── Typed mutations (UUID-based) ─────────────────────────────────────

    public async Task<Guid> CreateTenantTypedAsync(TenantRecord tenant)
    {
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            @"INSERT INTO central_platform.tenants (slug, display_name, domain, tier, is_active)
              VALUES (@slug, @name, @domain, @tier, true) RETURNING id", conn);
        cmd.Parameters.AddWithValue("slug", tenant.Slug);
        cmd.Parameters.AddWithValue("name", tenant.DisplayName);
        cmd.Parameters.AddWithValue("domain", (object?)tenant.Domain ?? DBNull.Value);
        cmd.Parameters.AddWithValue("tier", tenant.Tier);
        return (Guid)(await cmd.ExecuteScalarAsync())!;
    }

    public async Task UpdateTenantAsync(TenantRecord tenant)
    {
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            @"UPDATE central_platform.tenants
              SET display_name=@name, domain=@domain, tier=@tier, updated_at=now()
              WHERE id=@id", conn);
        cmd.Parameters.AddWithValue("id", tenant.Id);
        cmd.Parameters.AddWithValue("name", tenant.DisplayName);
        cmd.Parameters.AddWithValue("domain", (object?)tenant.Domain ?? DBNull.Value);
        cmd.Parameters.AddWithValue("tier", tenant.Tier);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteTenantAsync(Guid tenantId)
    {
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            "DELETE FROM central_platform.tenants WHERE id=@id", conn);
        cmd.Parameters.AddWithValue("id", tenantId);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task SuspendTenantByIdAsync(Guid tenantId)
    {
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            "UPDATE central_platform.tenants SET is_active=false, updated_at=now() WHERE id=@id", conn);
        cmd.Parameters.AddWithValue("id", tenantId);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task ActivateTenantByIdAsync(Guid tenantId)
    {
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            "UPDATE central_platform.tenants SET is_active=true, updated_at=now() WHERE id=@id", conn);
        cmd.Parameters.AddWithValue("id", tenantId);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task ToggleGlobalAdminByIdAsync(Guid userId)
    {
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            "UPDATE central_platform.global_users SET is_global_admin = NOT is_global_admin WHERE id=@id", conn);
        cmd.Parameters.AddWithValue("id", userId);
        await cmd.ExecuteNonQueryAsync();
    }

    // ── User management ────────────────────────────────────────────────

    public async Task<Guid> CreateGlobalUserAsync(string email, string? displayName, string passwordHash, string salt, bool isGlobalAdmin)
    {
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            @"INSERT INTO central_platform.global_users (email, display_name, password_hash, salt, email_verified, is_global_admin)
              VALUES (@email, @name, @hash, @salt, false, @admin) RETURNING id", conn);
        cmd.Parameters.AddWithValue("email", email);
        cmd.Parameters.AddWithValue("name", (object?)displayName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("hash", passwordHash);
        cmd.Parameters.AddWithValue("salt", salt);
        cmd.Parameters.AddWithValue("admin", isGlobalAdmin);
        return (Guid)(await cmd.ExecuteScalarAsync())!;
    }

    public async Task ResetGlobalUserPasswordAsync(Guid userId, string passwordHash, string salt, bool resetVerification)
    {
        await using var conn = await OpenConnectionAsync();
        var sql = resetVerification
            ? "UPDATE central_platform.global_users SET password_hash=@hash, salt=@salt, email_verified=false WHERE id=@id"
            : "UPDATE central_platform.global_users SET password_hash=@hash, salt=@salt WHERE id=@id";
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", userId);
        cmd.Parameters.AddWithValue("hash", passwordHash);
        cmd.Parameters.AddWithValue("salt", salt);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteGlobalUserAsync(Guid userId)
    {
        await using var conn = await OpenConnectionAsync();
        // Remove memberships first (FK constraint)
        await using var cmd1 = new NpgsqlCommand("DELETE FROM central_platform.tenant_memberships WHERE user_id=@id", conn);
        cmd1.Parameters.AddWithValue("id", userId);
        await cmd1.ExecuteNonQueryAsync();
        await using var cmd2 = new NpgsqlCommand("DELETE FROM central_platform.global_users WHERE id=@id", conn);
        cmd2.Parameters.AddWithValue("id", userId);
        await cmd2.ExecuteNonQueryAsync();
    }

    // ── Membership CRUD ──────────────────────────────────────────────────

    public async Task<int> AddTenantMembershipAsync(Guid userId, Guid tenantId, string role)
    {
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            @"INSERT INTO central_platform.tenant_memberships (user_id, tenant_id, role, joined_at)
              VALUES (@uid, @tid, @role, now()) RETURNING id", conn);
        cmd.Parameters.AddWithValue("uid", userId);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("role", role);
        return (int)(await cmd.ExecuteScalarAsync())!;
    }

    public async Task RemoveTenantMembershipAsync(int membershipId)
    {
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand("DELETE FROM central_platform.tenant_memberships WHERE id=@id", conn);
        cmd.Parameters.AddWithValue("id", membershipId);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task UpdateMembershipRoleAsync(int membershipId, string newRole)
    {
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand("UPDATE central_platform.tenant_memberships SET role=@role WHERE id=@id", conn);
        cmd.Parameters.AddWithValue("id", membershipId);
        cmd.Parameters.AddWithValue("role", newRole);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<Central.Engine.Models.MembershipRow>> GetUserMembershipsAsync(Guid userId)
    {
        var list = new List<Central.Engine.Models.MembershipRow>();
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            @"SELECT m.id, m.user_id, m.tenant_id, t.slug, t.display_name, m.role, m.joined_at
              FROM central_platform.tenant_memberships m
              JOIN central_platform.tenants t ON t.id = m.tenant_id
              WHERE m.user_id = @uid ORDER BY t.slug", conn);
        cmd.Parameters.AddWithValue("uid", userId);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new Central.Engine.Models.MembershipRow
            {
                Id = r.GetInt32(0), UserId = r.GetGuid(1), TenantId = r.GetGuid(2),
                TenantSlug = r.GetString(3), TenantName = r.GetString(4),
                Role = r.GetString(5), JoinedAt = r.GetDateTime(6)
            });
        return list;
    }

    public async Task<List<Central.Engine.Models.TenantOption>> GetTenantOptionsAsync()
    {
        var list = new List<Central.Engine.Models.TenantOption>();
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT id, slug, display_name FROM central_platform.tenants WHERE is_active ORDER BY slug", conn);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new Central.Engine.Models.TenantOption
            {
                Id = r.GetGuid(0), Slug = r.GetString(1), DisplayName = r.GetString(2)
            });
        return list;
    }

    // ── Per-tenant detail queries ─────────────────────────────────────────

    public async Task<List<SubscriptionRecord>> GetTenantSubscriptionsAsync(Guid tenantId)
    {
        var list = new List<SubscriptionRecord>();
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            @"SELECT s.id, s.tenant_id, t.slug, t.display_name, p.tier, p.display_name AS plan_name,
                     p.max_users, p.max_devices, s.status, s.started_at, s.expires_at, s.stripe_sub_id
              FROM central_platform.tenant_subscriptions s
              JOIN central_platform.tenants t ON t.id = s.tenant_id
              JOIN central_platform.subscription_plans p ON p.id = s.plan_id
              WHERE s.tenant_id = @tid ORDER BY s.started_at DESC", conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new SubscriptionRecord
            {
                Id = r.GetInt32(0), TenantId = r.GetGuid(1), TenantSlug = r.GetString(2),
                TenantName = r.GetString(3), Tier = r.GetString(4), PlanName = r.GetString(5),
                MaxUsers = r.IsDBNull(6) ? null : r.GetInt32(6),
                MaxDevices = r.IsDBNull(7) ? null : r.GetInt32(7),
                Status = r.GetString(8), StartedAt = r.GetDateTime(9),
                ExpiresAt = r.IsDBNull(10) ? null : r.GetDateTime(10),
                StripeSubId = r.IsDBNull(11) ? null : r.GetString(11)
            });
        return list;
    }

    public async Task<List<ModuleLicenseRecord>> GetTenantModulesAsync(Guid tenantId)
    {
        var list = new List<ModuleLicenseRecord>();
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            @"SELECT l.id, l.tenant_id, t.slug, t.display_name,
                     l.module_id, m.code, m.display_name, m.is_base, l.granted_at, l.expires_at
              FROM central_platform.tenant_module_licenses l
              JOIN central_platform.tenants t ON t.id = l.tenant_id
              JOIN central_platform.module_catalog m ON m.id = l.module_id
              WHERE l.tenant_id = @tid ORDER BY m.code", conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new ModuleLicenseRecord
            {
                Id = r.GetInt32(0), TenantId = r.GetGuid(1), TenantSlug = r.GetString(2),
                TenantName = r.GetString(3), ModuleId = r.GetInt32(4), ModuleCode = r.GetString(5),
                ModuleName = r.GetString(6), IsBase = r.GetBoolean(7),
                GrantedAt = r.GetDateTime(8), ExpiresAt = r.IsDBNull(9) ? null : r.GetDateTime(9)
            });
        return list;
    }

    public async Task<List<Central.Engine.Models.TenantMemberRecord>> GetTenantMembersAsync(Guid tenantId)
    {
        var list = new List<Central.Engine.Models.TenantMemberRecord>();
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            @"SELECT m.user_id, u.email, u.display_name, m.role, m.joined_at
              FROM central_platform.tenant_memberships m
              JOIN central_platform.global_users u ON u.id = m.user_id
              WHERE m.tenant_id = @tid ORDER BY m.joined_at", conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new Central.Engine.Models.TenantMemberRecord
            {
                UserId = r.GetGuid(0), Email = r.GetString(1),
                DisplayName = r.IsDBNull(2) ? null : r.GetString(2),
                Role = r.IsDBNull(3) ? "Viewer" : r.GetString(3),
                JoinedAt = r.GetDateTime(4)
            });
        return list;
    }

    // ── Subscription CRUD ────────────────────────────────────────────────

    public async Task<int> CreateSubscriptionAsync(Guid tenantId, int planId, string status, DateTime? expiresAt)
    {
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            @"INSERT INTO central_platform.tenant_subscriptions (tenant_id, plan_id, status, started_at, expires_at)
              VALUES (@tid, @pid, @status, now(), @exp) RETURNING id", conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("pid", planId);
        cmd.Parameters.AddWithValue("status", status);
        cmd.Parameters.AddWithValue("exp", (object?)expiresAt ?? DBNull.Value);
        return (int)(await cmd.ExecuteScalarAsync())!;
    }

    public async Task CancelSubscriptionAsync(int subscriptionId)
    {
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            "UPDATE central_platform.tenant_subscriptions SET status='cancelled' WHERE id=@id", conn);
        cmd.Parameters.AddWithValue("id", subscriptionId);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<Central.Engine.Models.PlanItem>> GetPlanItemsAsync()
    {
        var list = new List<Central.Engine.Models.PlanItem>();
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT id, tier, display_name, max_users, max_devices, price_monthly FROM central_platform.subscription_plans ORDER BY id", conn);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new Central.Engine.Models.PlanItem
            {
                Id = r.GetInt32(0), Tier = r.GetString(1), DisplayName = r.GetString(2),
                MaxUsers = r.IsDBNull(3) ? null : r.GetInt32(3),
                MaxDevices = r.IsDBNull(4) ? null : r.GetInt32(4),
                PriceMonthly = r.IsDBNull(5) ? null : r.GetDecimal(5)
            });
        return list;
    }

    // ── Bulk module license operations ───────────────────────────────────

    public async Task BulkGrantModulesAsync(Guid tenantId, List<int> moduleIds, DateTime? expiresAt = null)
    {
        await using var conn = await OpenConnectionAsync();
        foreach (var mid in moduleIds)
        {
            await using var cmd = new NpgsqlCommand(
                @"INSERT INTO central_platform.tenant_module_licenses (tenant_id, module_id, granted_at, expires_at)
                  VALUES (@tid, @mid, now(), @exp) ON CONFLICT (tenant_id, module_id) DO NOTHING", conn);
            cmd.Parameters.AddWithValue("tid", tenantId);
            cmd.Parameters.AddWithValue("mid", mid);
            cmd.Parameters.AddWithValue("exp", (object?)expiresAt ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    public async Task<List<Central.Engine.Models.ModuleItem>> GetModuleItemsAsync(Guid? tenantId = null)
    {
        var list = new List<Central.Engine.Models.ModuleItem>();
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT id, code, display_name, is_base FROM central_platform.module_catalog ORDER BY display_name", conn);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new Central.Engine.Models.ModuleItem
            {
                Id = r.GetInt32(0), Code = r.GetString(1), DisplayName = r.GetString(2), IsBase = r.GetBoolean(3)
            });

        // Mark already-granted modules if tenantId provided
        if (tenantId.HasValue)
        {
            var granted = new HashSet<int>();
            await using var cmd2 = new NpgsqlCommand(
                "SELECT module_id FROM central_platform.tenant_module_licenses WHERE tenant_id=@tid", conn);
            cmd2.Parameters.AddWithValue("tid", tenantId.Value);
            await using var r2 = await cmd2.ExecuteReaderAsync();
            while (await r2.ReadAsync()) granted.Add(r2.GetInt32(0));
            foreach (var m in list) m.AlreadyGranted = granted.Contains(m.Id);
        }

        return list;
    }

    // ── Audit log ─────────────────────────────────────────────────────────

    public async Task InsertGlobalAdminAuditAsync(string actorEmail, string action, string? entityType, string? entityId, string? details)
    {
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            @"INSERT INTO central_platform.global_admin_audit_log (actor_email, action, entity_type, entity_id, details)
              VALUES (@actor, @action, @etype, @eid, @details::jsonb)", conn);
        cmd.Parameters.AddWithValue("actor", actorEmail);
        cmd.Parameters.AddWithValue("action", action);
        cmd.Parameters.AddWithValue("etype", (object?)entityType ?? DBNull.Value);
        cmd.Parameters.AddWithValue("eid", (object?)entityId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("details", (object?)details ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<AuditLogEntry>> GetGlobalAdminAuditAsync(int limit = 500, string? actionFilter = null)
    {
        var list = new List<AuditLogEntry>();
        await using var conn = await OpenConnectionAsync();
        var sql = @"SELECT id, actor_user_id, actor_email, action, entity_type, entity_id, details::text, created_at
                    FROM central_platform.global_admin_audit_log";
        if (!string.IsNullOrEmpty(actionFilter)) sql += " WHERE action = @filter";
        sql += " ORDER BY created_at DESC LIMIT @limit";
        await using var cmd = new NpgsqlCommand(sql, conn);
        if (!string.IsNullOrEmpty(actionFilter)) cmd.Parameters.AddWithValue("filter", actionFilter);
        cmd.Parameters.AddWithValue("limit", limit);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new AuditLogEntry
            {
                Id = r.GetInt32(0), ActorUserId = r.IsDBNull(1) ? null : r.GetGuid(1),
                ActorEmail = r.GetString(2), Action = r.GetString(3),
                EntityType = r.IsDBNull(4) ? null : r.GetString(4),
                EntityId = r.IsDBNull(5) ? null : r.GetString(5),
                Details = r.IsDBNull(6) ? null : r.GetString(6),
                CreatedAt = r.GetDateTime(7)
            });
        return list;
    }

    // ── Dashboard chart queries ──────────────────────────────────────────

    public async Task<List<(string Month, int Count)>> GetTenantGrowthAsync()
    {
        var list = new List<(string, int)>();
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            @"SELECT to_char(created_at, 'YYYY-MM') AS month, COUNT(*)::int
              FROM central_platform.tenants
              GROUP BY 1 ORDER BY 1", conn);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) list.Add((r.GetString(0), r.GetInt32(1)));
        return list;
    }

    public async Task<List<(string Tier, int Count)>> GetSubscriptionDistributionAsync()
    {
        var list = new List<(string, int)>();
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            @"SELECT p.tier, COUNT(*)::int
              FROM central_platform.tenant_subscriptions s
              JOIN central_platform.subscription_plans p ON p.id = s.plan_id
              WHERE s.status IN ('active','trial')
              GROUP BY p.tier ORDER BY p.tier", conn);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) list.Add((r.GetString(0), r.GetInt32(1)));
        return list;
    }

    public async Task<List<(string Module, int TenantCount)>> GetModuleAdoptionAsync()
    {
        var list = new List<(string, int)>();
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            @"SELECT m.display_name, COUNT(DISTINCT l.tenant_id)::int
              FROM central_platform.module_catalog m
              LEFT JOIN central_platform.tenant_module_licenses l ON l.module_id = m.id
              GROUP BY m.display_name ORDER BY 2 DESC", conn);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) list.Add((r.GetString(0), r.GetInt32(1)));
        return list;
    }

    public async Task<List<(string TenantSlug, int UserCount)>> GetTopTenantsByUsersAsync(int limit = 10)
    {
        var list = new List<(string, int)>();
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            @"SELECT t.slug, COUNT(m.user_id)::int
              FROM central_platform.tenants t
              LEFT JOIN central_platform.tenant_memberships m ON m.tenant_id = t.id
              GROUP BY t.slug ORDER BY 2 DESC LIMIT @limit", conn);
        cmd.Parameters.AddWithValue("limit", limit);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) list.Add((r.GetString(0), r.GetInt32(1)));
        return list;
    }

    // ── Tenant Addresses (many-to-one) ─────────────────────────────────

    public async Task<List<TenantAddressRecord>> GetTenantAddressesAsync(Guid tenantId)
    {
        var list = new List<TenantAddressRecord>();
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            @"SELECT id, tenant_id, address_type, label, line1, line2, city, state, postal_code, country, is_primary
              FROM central_platform.tenant_addresses WHERE tenant_id=@tid ORDER BY is_primary DESC, address_type", conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new TenantAddressRecord
            {
                Id = r.GetInt32(0), TenantId = r.GetGuid(1), AddressType = r.GetString(2),
                Label = r.IsDBNull(3) ? null : r.GetString(3), Line1 = r.GetString(4),
                Line2 = r.IsDBNull(5) ? null : r.GetString(5), City = r.GetString(6),
                State = r.IsDBNull(7) ? null : r.GetString(7),
                PostalCode = r.IsDBNull(8) ? null : r.GetString(8),
                Country = r.GetString(9), IsPrimary = r.GetBoolean(10)
            });
        return list;
    }

    public async Task<int> InsertTenantAddressAsync(TenantAddressRecord addr)
    {
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            @"INSERT INTO central_platform.tenant_addresses (tenant_id, address_type, label, line1, line2, city, state, postal_code, country, is_primary)
              VALUES (@tid, @type, @label, @l1, @l2, @city, @state, @zip, @country, @primary) RETURNING id", conn);
        cmd.Parameters.AddWithValue("tid", addr.TenantId);
        cmd.Parameters.AddWithValue("type", addr.AddressType);
        cmd.Parameters.AddWithValue("label", (object?)addr.Label ?? DBNull.Value);
        cmd.Parameters.AddWithValue("l1", addr.Line1);
        cmd.Parameters.AddWithValue("l2", (object?)addr.Line2 ?? DBNull.Value);
        cmd.Parameters.AddWithValue("city", addr.City);
        cmd.Parameters.AddWithValue("state", (object?)addr.State ?? DBNull.Value);
        cmd.Parameters.AddWithValue("zip", (object?)addr.PostalCode ?? DBNull.Value);
        cmd.Parameters.AddWithValue("country", addr.Country);
        cmd.Parameters.AddWithValue("primary", addr.IsPrimary);
        return (int)(await cmd.ExecuteScalarAsync())!;
    }

    public async Task UpdateTenantAddressAsync(TenantAddressRecord addr)
    {
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            @"UPDATE central_platform.tenant_addresses
              SET address_type=@type, label=@label, line1=@l1, line2=@l2, city=@city,
                  state=@state, postal_code=@zip, country=@country, is_primary=@primary, updated_at=now()
              WHERE id=@id", conn);
        cmd.Parameters.AddWithValue("id", addr.Id);
        cmd.Parameters.AddWithValue("type", addr.AddressType);
        cmd.Parameters.AddWithValue("label", (object?)addr.Label ?? DBNull.Value);
        cmd.Parameters.AddWithValue("l1", addr.Line1);
        cmd.Parameters.AddWithValue("l2", (object?)addr.Line2 ?? DBNull.Value);
        cmd.Parameters.AddWithValue("city", addr.City);
        cmd.Parameters.AddWithValue("state", (object?)addr.State ?? DBNull.Value);
        cmd.Parameters.AddWithValue("zip", (object?)addr.PostalCode ?? DBNull.Value);
        cmd.Parameters.AddWithValue("country", addr.Country);
        cmd.Parameters.AddWithValue("primary", addr.IsPrimary);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteTenantAddressAsync(int addressId)
    {
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand("DELETE FROM central_platform.tenant_addresses WHERE id=@id", conn);
        cmd.Parameters.AddWithValue("id", addressId);
        await cmd.ExecuteNonQueryAsync();
    }

    // ── Contacts (many-to-many via tenant_contacts) ──────────────────────

    public async Task<List<ContactRecord>> GetAllContactsAsync()
    {
        var list = new List<ContactRecord>();
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT id, first_name, last_name, email, phone, mobile, job_title, company, notes FROM central_platform.contacts ORDER BY last_name, first_name", conn);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new ContactRecord
            {
                Id = r.GetInt32(0), FirstName = r.GetString(1), LastName = r.GetString(2),
                Email = r.IsDBNull(3) ? null : r.GetString(3), Phone = r.IsDBNull(4) ? null : r.GetString(4),
                Mobile = r.IsDBNull(5) ? null : r.GetString(5), JobTitle = r.IsDBNull(6) ? null : r.GetString(6),
                Company = r.IsDBNull(7) ? null : r.GetString(7), Notes = r.IsDBNull(8) ? null : r.GetString(8)
            });
        return list;
    }

    public async Task<int> InsertContactAsync(ContactRecord contact)
    {
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            @"INSERT INTO central_platform.contacts (first_name, last_name, email, phone, mobile, job_title, company, notes)
              VALUES (@fn, @ln, @email, @phone, @mobile, @title, @company, @notes) RETURNING id", conn);
        cmd.Parameters.AddWithValue("fn", contact.FirstName);
        cmd.Parameters.AddWithValue("ln", contact.LastName);
        cmd.Parameters.AddWithValue("email", (object?)contact.Email ?? DBNull.Value);
        cmd.Parameters.AddWithValue("phone", (object?)contact.Phone ?? DBNull.Value);
        cmd.Parameters.AddWithValue("mobile", (object?)contact.Mobile ?? DBNull.Value);
        cmd.Parameters.AddWithValue("title", (object?)contact.JobTitle ?? DBNull.Value);
        cmd.Parameters.AddWithValue("company", (object?)contact.Company ?? DBNull.Value);
        cmd.Parameters.AddWithValue("notes", (object?)contact.Notes ?? DBNull.Value);
        return (int)(await cmd.ExecuteScalarAsync())!;
    }

    public async Task UpdateContactAsync(ContactRecord contact)
    {
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            @"UPDATE central_platform.contacts
              SET first_name=@fn, last_name=@ln, email=@email, phone=@phone, mobile=@mobile,
                  job_title=@title, company=@company, notes=@notes, updated_at=now()
              WHERE id=@id", conn);
        cmd.Parameters.AddWithValue("id", contact.Id);
        cmd.Parameters.AddWithValue("fn", contact.FirstName);
        cmd.Parameters.AddWithValue("ln", contact.LastName);
        cmd.Parameters.AddWithValue("email", (object?)contact.Email ?? DBNull.Value);
        cmd.Parameters.AddWithValue("phone", (object?)contact.Phone ?? DBNull.Value);
        cmd.Parameters.AddWithValue("mobile", (object?)contact.Mobile ?? DBNull.Value);
        cmd.Parameters.AddWithValue("title", (object?)contact.JobTitle ?? DBNull.Value);
        cmd.Parameters.AddWithValue("company", (object?)contact.Company ?? DBNull.Value);
        cmd.Parameters.AddWithValue("notes", (object?)contact.Notes ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task DeleteContactAsync(int contactId)
    {
        await using var conn = await OpenConnectionAsync();
        // Junction rows cascade-deleted by FK
        await using var cmd = new NpgsqlCommand("DELETE FROM central_platform.contacts WHERE id=@id", conn);
        cmd.Parameters.AddWithValue("id", contactId);
        await cmd.ExecuteNonQueryAsync();
    }

    // ── Tenant-Contact junction ──────────────────────────────────────────

    public async Task<List<TenantContactRecord>> GetTenantContactsAsync(Guid tenantId)
    {
        var list = new List<TenantContactRecord>();
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            @"SELECT tc.id, tc.tenant_id, tc.contact_id, tc.role, tc.is_primary,
                     t.slug, c.first_name || ' ' || c.last_name, c.email
              FROM central_platform.tenant_contacts tc
              JOIN central_platform.tenants t ON t.id = tc.tenant_id
              JOIN central_platform.contacts c ON c.id = tc.contact_id
              WHERE tc.tenant_id = @tid ORDER BY tc.is_primary DESC, tc.role", conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new TenantContactRecord
            {
                Id = r.GetInt32(0), TenantId = r.GetGuid(1), ContactId = r.GetInt32(2),
                Role = r.GetString(3), IsPrimary = r.GetBoolean(4),
                TenantSlug = r.GetString(5), ContactName = r.GetString(6),
                ContactEmail = r.IsDBNull(7) ? null : r.GetString(7)
            });
        return list;
    }

    public async Task<int> AssignContactToTenantAsync(Guid tenantId, int contactId, string role, bool isPrimary = false)
    {
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            @"INSERT INTO central_platform.tenant_contacts (tenant_id, contact_id, role, is_primary)
              VALUES (@tid, @cid, @role, @primary) ON CONFLICT (tenant_id, contact_id) DO UPDATE SET role=@role, is_primary=@primary
              RETURNING id", conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("cid", contactId);
        cmd.Parameters.AddWithValue("role", role);
        cmd.Parameters.AddWithValue("primary", isPrimary);
        return (int)(await cmd.ExecuteScalarAsync())!;
    }

    public async Task UnassignContactFromTenantAsync(int junctionId)
    {
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand("DELETE FROM central_platform.tenant_contacts WHERE id=@id", conn);
        cmd.Parameters.AddWithValue("id", junctionId);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<TenantContactRecord>> GetContactTenantsAsync(int contactId)
    {
        var list = new List<TenantContactRecord>();
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            @"SELECT tc.id, tc.tenant_id, tc.contact_id, tc.role, tc.is_primary,
                     t.slug, c.first_name || ' ' || c.last_name, c.email
              FROM central_platform.tenant_contacts tc
              JOIN central_platform.tenants t ON t.id = tc.tenant_id
              JOIN central_platform.contacts c ON c.id = tc.contact_id
              WHERE tc.contact_id = @cid ORDER BY t.slug", conn);
        cmd.Parameters.AddWithValue("cid", contactId);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new TenantContactRecord
            {
                Id = r.GetInt32(0), TenantId = r.GetGuid(1), ContactId = r.GetInt32(2),
                Role = r.GetString(3), IsPrimary = r.GetBoolean(4),
                TenantSlug = r.GetString(5), ContactName = r.GetString(6),
                ContactEmail = r.IsDBNull(7) ? null : r.GetString(7)
            });
        return list;
    }

    // ── Global Admin — Legacy Dictionary queries (kept for backward compat) ──

    public async Task<List<Dictionary<string, object?>>> GetGlobalTenantsAsync()
    {
        var rows = new List<Dictionary<string, object?>>();
        await using var conn = await OpenConnectionAsync();
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
        while (await r.ReadAsync())
        {
            var row = new Dictionary<string, object?>();
            for (int i = 0; i < r.FieldCount; i++)
                row[r.GetName(i)] = r.IsDBNull(i) ? null : r.GetValue(i);
            rows.Add(row);
        }
        return rows;
    }

    public async Task<List<Dictionary<string, object?>>> GetGlobalUsersAsync()
    {
        var rows = new List<Dictionary<string, object?>>();
        await using var conn = await OpenConnectionAsync();
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
        while (await r.ReadAsync())
        {
            var row = new Dictionary<string, object?>();
            for (int i = 0; i < r.FieldCount; i++)
                row[r.GetName(i)] = r.IsDBNull(i) ? null : r.GetValue(i);
            rows.Add(row);
        }
        return rows;
    }

    public async Task<List<Dictionary<string, object?>>> GetGlobalSubscriptionsAsync()
    {
        var rows = new List<Dictionary<string, object?>>();
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            @"SELECT s.id, t.slug AS tenant_slug, t.display_name AS tenant_name,
                     p.tier, p.display_name AS plan_name, p.max_users, p.max_devices,
                     s.status, s.started_at, s.expires_at
              FROM central_platform.tenant_subscriptions s
              JOIN central_platform.tenants t ON t.id = s.tenant_id
              JOIN central_platform.subscription_plans p ON p.id = s.plan_id
              ORDER BY s.started_at DESC", conn);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            var row = new Dictionary<string, object?>();
            for (int i = 0; i < r.FieldCount; i++)
                row[r.GetName(i)] = r.IsDBNull(i) ? null : r.GetValue(i);
            rows.Add(row);
        }
        return rows;
    }

    public async Task<List<Dictionary<string, object?>>> GetGlobalLicensesAsync()
    {
        var rows = new List<Dictionary<string, object?>>();
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            @"SELECT l.id, t.slug AS tenant_slug, t.display_name AS tenant_name,
                     m.code AS module_code, m.display_name AS module_name, m.is_base,
                     l.granted_at, l.expires_at
              FROM central_platform.tenant_module_licenses l
              JOIN central_platform.tenants t ON t.id = l.tenant_id
              JOIN central_platform.module_catalog m ON m.id = l.module_id
              ORDER BY t.slug, m.code", conn);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            var row = new Dictionary<string, object?>();
            for (int i = 0; i < r.FieldCount; i++)
                row[r.GetName(i)] = r.IsDBNull(i) ? null : r.GetValue(i);
            rows.Add(row);
        }
        return rows;
    }

    public async Task<(long TotalTenants, long ActiveTenants, long TotalUsers, long VerifiedUsers, long ActiveSubs)> GetPlatformMetricsAsync()
    {
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            @"SELECT
                (SELECT COUNT(*) FROM central_platform.tenants),
                (SELECT COUNT(*) FROM central_platform.tenants WHERE is_active),
                (SELECT COUNT(*) FROM central_platform.global_users),
                (SELECT COUNT(*) FROM central_platform.global_users WHERE email_verified),
                (SELECT COUNT(*) FROM central_platform.tenant_subscriptions WHERE status IN ('active','trial'))", conn);
        await using var r = await cmd.ExecuteReaderAsync();
        await r.ReadAsync();
        return (r.GetInt64(0), r.GetInt64(1), r.GetInt64(2), r.GetInt64(3), r.GetInt64(4));
    }

    // ── Mutations ────────────────────────────────────────────────────────

    public async Task<int> CreateTenantAsync(string slug, string displayName, string? domain, string tier = "free")
    {
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            @"INSERT INTO central_platform.tenants (slug, display_name, domain, tier, is_active)
              VALUES (@slug, @name, @domain, @tier, true) RETURNING id", conn);
        cmd.Parameters.AddWithValue("slug", slug);
        cmd.Parameters.AddWithValue("name", displayName);
        cmd.Parameters.AddWithValue("domain", (object?)domain ?? DBNull.Value);
        cmd.Parameters.AddWithValue("tier", tier);
        return (int)(await cmd.ExecuteScalarAsync())!;
    }

    public async Task SuspendTenantAsync(int tenantId)
    {
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            "UPDATE central_platform.tenants SET is_active = false, updated_at = now() WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("id", tenantId);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task ActivateTenantAsync(int tenantId)
    {
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            "UPDATE central_platform.tenants SET is_active = true, updated_at = now() WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("id", tenantId);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task ToggleGlobalAdminAsync(int userId)
    {
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            "UPDATE central_platform.global_users SET is_global_admin = NOT is_global_admin WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("id", userId);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task GrantModuleLicenseAsync(int tenantId, int moduleId)
    {
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            @"INSERT INTO central_platform.tenant_module_licenses (tenant_id, module_id, granted_at)
              VALUES (@tid, @mid, now()) ON CONFLICT (tenant_id, module_id) DO NOTHING", conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("mid", moduleId);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task RevokeModuleLicenseAsync(int licenseId)
    {
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            "DELETE FROM central_platform.tenant_module_licenses WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("id", licenseId);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task ChangeTenantPlanAsync(int tenantId, int planId)
    {
        await using var conn = await OpenConnectionAsync();
        // Deactivate old subscription
        await using var cmd1 = new NpgsqlCommand(
            "UPDATE central_platform.tenant_subscriptions SET status = 'cancelled' WHERE tenant_id = @tid AND status = 'active'", conn);
        cmd1.Parameters.AddWithValue("tid", tenantId);
        await cmd1.ExecuteNonQueryAsync();

        // Create new subscription
        await using var cmd2 = new NpgsqlCommand(
            @"INSERT INTO central_platform.tenant_subscriptions (tenant_id, plan_id, status, started_at)
              VALUES (@tid, @pid, 'active', now())", conn);
        cmd2.Parameters.AddWithValue("tid", tenantId);
        cmd2.Parameters.AddWithValue("pid", planId);
        await cmd2.ExecuteNonQueryAsync();
    }

    public async Task<List<Dictionary<string, object?>>> GetSubscriptionPlansAsync()
    {
        var rows = new List<Dictionary<string, object?>>();
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT id, tier, display_name, max_users, max_devices, price_monthly FROM central_platform.subscription_plans ORDER BY price_monthly", conn);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            var row = new Dictionary<string, object?>();
            for (int i = 0; i < r.FieldCount; i++)
                row[r.GetName(i)] = r.IsDBNull(i) ? null : r.GetValue(i);
            rows.Add(row);
        }
        return rows;
    }

    public async Task<List<Dictionary<string, object?>>> GetModuleCatalogAsync()
    {
        var rows = new List<Dictionary<string, object?>>();
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT id, code, display_name, is_base FROM central_platform.module_catalog ORDER BY display_name", conn);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            var row = new Dictionary<string, object?>();
            for (int i = 0; i < r.FieldCount; i++)
                row[r.GetName(i)] = r.IsDBNull(i) ? null : r.GetValue(i);
            rows.Add(row);
        }
        return rows;
    }
}
