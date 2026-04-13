using Npgsql;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Central.Data;

public partial class DbRepository
{
    // ── Global Admin — central_platform schema queries ────────────────────

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
