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
        await using var conn = new NpgsqlConnection(_connectionString);
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
        await using var conn = new NpgsqlConnection(_connectionString);
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
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
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
        await using var conn = new NpgsqlConnection(_connectionString);
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
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
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
}
