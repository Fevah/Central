using Npgsql;
using Central.Tenancy;

namespace Central.Licensing;

/// <summary>
/// Manages per-tenant module licensing.
/// Modules map to IModule.PermissionCategory (devices, switches, servicedesk, etc.)
/// </summary>
public class ModuleLicenseService
{
    private readonly string _platformDsn;

    public ModuleLicenseService(string platformDsn) => _platformDsn = platformDsn;

    /// <summary>Check if a tenant has a license for a specific module.</summary>
    public async Task<bool> IsModuleLicensedAsync(Guid tenantId, string moduleCode)
    {
        await using var conn = new NpgsqlConnection(_platformDsn);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            @"SELECT COUNT(*) FROM central_platform.tenant_module_licenses l
              JOIN central_platform.module_catalog m ON m.id = l.module_id
              WHERE l.tenant_id = @tid AND m.code = @code
              AND (l.expires_at IS NULL OR l.expires_at > NOW())", conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("code", moduleCode);
        return (long)(await cmd.ExecuteScalarAsync())! > 0;
    }

    /// <summary>Get all modules with their license status for a tenant.</summary>
    public async Task<List<ModuleLicense>> GetModulesAsync(Guid tenantId)
    {
        var modules = new List<ModuleLicense>();
        await using var conn = new NpgsqlConnection(_platformDsn);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            @"SELECT m.id, m.code, m.display_name, m.is_base,
                     l.tenant_id IS NOT NULL AS is_licensed, l.expires_at
              FROM central_platform.module_catalog m
              LEFT JOIN central_platform.tenant_module_licenses l ON l.module_id = m.id AND l.tenant_id = @tid
              ORDER BY m.id", conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            modules.Add(new ModuleLicense
            {
                ModuleId = r.GetInt32(0), Code = r.GetString(1), DisplayName = r.GetString(2),
                IsBase = r.GetBoolean(3), IsLicensed = r.GetBoolean(4),
                ExpiresAt = r.IsDBNull(5) ? null : r.GetDateTime(5)
            });
        return modules;
    }

    /// <summary>Grant a module license to a tenant.</summary>
    public async Task GrantModuleAsync(Guid tenantId, string moduleCode, DateTime? expiresAt = null)
    {
        await using var conn = new NpgsqlConnection(_platformDsn);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            @"INSERT INTO central_platform.tenant_module_licenses (tenant_id, module_id, expires_at)
              SELECT @tid, id, @exp FROM central_platform.module_catalog WHERE code = @code
              ON CONFLICT (tenant_id, module_id) DO UPDATE SET expires_at = @exp, granted_at = NOW()", conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("code", moduleCode);
        cmd.Parameters.AddWithValue("exp", (object?)expiresAt ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>Revoke a module license from a tenant.</summary>
    public async Task RevokeModuleAsync(Guid tenantId, string moduleCode)
    {
        await using var conn = new NpgsqlConnection(_platformDsn);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            @"DELETE FROM central_platform.tenant_module_licenses
              WHERE tenant_id = @tid AND module_id = (SELECT id FROM central_platform.module_catalog WHERE code = @code)", conn);
        cmd.Parameters.AddWithValue("tid", tenantId);
        cmd.Parameters.AddWithValue("code", moduleCode);
        await cmd.ExecuteNonQueryAsync();
    }
}
