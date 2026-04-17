using Npgsql;

namespace Central.Data.Repositories;

/// <summary>
/// Reads permission grants for roles from the new permissions_v2 tables.
/// Falls back to legacy role_permissions if new tables don't exist.
/// </summary>
public class PermissionRepository : RepositoryBase
{
    public PermissionRepository(string dsn) : base(dsn) { }

    /// <summary>
    /// Get all permission codes granted to a role by role name.
    /// Uses new role_permission_grants + permissions tables.
    /// Falls back to legacy boolean mapping if new tables missing.
    /// </summary>
    public async Task<List<string>> GetPermissionCodesForRoleAsync(string roleName)
    {
        var codes = new List<string>();

        try
        {
            // Try new permission tables first
            await using var conn = CreateConnection();
            await conn.OpenAsync();

            // Check if new tables exist
            await using var check = new NpgsqlCommand(
                "SELECT EXISTS(SELECT 1 FROM information_schema.tables WHERE table_name = 'permissions')", conn);
            var hasNew = (bool)(await check.ExecuteScalarAsync())!;

            if (hasNew)
            {
                await using var cmd = new NpgsqlCommand(@"
                    SELECT p.code FROM permissions p
                    JOIN role_permission_grants rpg ON rpg.permission_id = p.id
                    JOIN roles r ON r.id = rpg.role_id
                    WHERE r.name = @role", conn);
                cmd.Parameters.AddWithValue("role", roleName);
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    codes.Add(reader.GetString(0));
            }

            // Fallback: map legacy boolean permissions to codes
            if (codes.Count == 0)
            {
                codes = await GetLegacyPermissionCodesAsync(conn, roleName);
            }

            return codes;
        }
        catch (Exception ex)
        {
            AppLogger.LogException("auth", ex, nameof(GetPermissionCodesForRoleAsync));
            return codes;
        }
    }

    /// <summary>
    /// Map old role_permissions (module, can_view, can_edit, can_delete) to module:action codes.
    /// </summary>
    private async Task<List<string>> GetLegacyPermissionCodesAsync(NpgsqlConnection conn, string roleName)
    {
        var codes = new List<string>();

        await using var cmd = new NpgsqlCommand(
            "SELECT module, can_view, can_edit, can_delete FROM role_permissions WHERE role = @role", conn);
        cmd.Parameters.AddWithValue("role", roleName);
        await using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            var module = reader.GetString(0);
            var canView = reader.GetBoolean(1);
            var canEdit = reader.GetBoolean(2);
            var canDelete = reader.GetBoolean(3);

            // Map legacy module names to new permission categories
            var category = module switch
            {
                "ipam" => "devices",
                "switches" => "switches",
                "admin" => "admin",
                _ => module
            };

            if (canView) codes.Add($"{category}:read");
            if (canEdit) codes.Add($"{category}:write");
            if (canDelete) codes.Add($"{category}:delete");

            // Legacy admin role gets extra permissions
            if (module == "switches" && canEdit)
            {
                codes.Add("switches:ping");
                codes.Add("switches:ssh");
                codes.Add("switches:sync");
            }
            if (module == "ipam" && canView)
            {
                codes.Add("devices:reserved");
                codes.Add("devices:export");
            }
            if (module == "admin" && canView)
            {
                codes.Add("admin:users");
                codes.Add("admin:roles");
                codes.Add("admin:lookups");
                codes.Add("admin:settings");
                codes.Add("admin:audit");
            }

            // Links and BGP inherit from switches for legacy
            if (module == "switches")
            {
                if (canView) { codes.Add("links:read"); codes.Add("bgp:read"); codes.Add("vlans:read"); }
                if (canEdit) { codes.Add("links:write"); codes.Add("bgp:write"); codes.Add("bgp:sync"); codes.Add("vlans:write"); }
                if (canDelete) codes.Add("links:delete");
            }
        }

        return codes.Distinct().ToList();
    }

    /// <summary>Get allowed site/building names for a role.</summary>
    public async Task<List<string>> GetAllowedSitesAsync(string roleName)
    {
        var sites = new List<string>();
        try
        {
            await using var conn = CreateConnection();
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand(
                "SELECT building FROM role_sites WHERE role = @role AND allowed = TRUE", conn);
            cmd.Parameters.AddWithValue("role", roleName);
            await using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                sites.Add(reader.GetString(0));
        }
        catch (Exception ex)
        {
            AppLogger.LogException("auth", ex, nameof(GetAllowedSitesAsync));
        }
        return sites;
    }

    /// <summary>Get user by Windows username.</summary>
    public async Task<Central.Core.Auth.AuthUser?> GetUserByUsernameAsync(string username)
    {
        try
        {
            await using var conn = CreateConnection();
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand(@"
                SELECT u.id, u.username, u.display_name, u.role, u.is_active,
                       COALESCE(r.id, 0) as role_id, COALESCE(r.priority, 0) as priority,
                       COALESCE(u.password_hash, '') as password_hash,
                       COALESCE(u.salt, '') as salt,
                       COALESCE(u.user_type, 'ActiveDirectory') as user_type,
                       COALESCE(u.email, '') as email,
                       COALESCE(u.mfa_enabled, false) as mfa_enabled,
                       COALESCE(u.mfa_secret_enc, '') as mfa_secret_enc,
                       u.password_changed_at
                FROM app_users u
                LEFT JOIN roles r ON r.name = u.role
                WHERE u.username = @username", conn);
            cmd.Parameters.AddWithValue("username", username);
            await using var reader = await cmd.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                return new Central.Core.Auth.AuthUser
                {
                    Id = reader.GetInt32(0),
                    Username = reader.GetString(1),
                    DisplayName = reader.IsDBNull(2) ? reader.GetString(1) : reader.GetString(2),
                    RoleId = reader.GetInt32(5),
                    RoleName = reader.GetString(3),
                    Priority = reader.GetInt32(6),
                    IsActive = reader.GetBoolean(4),
                    PasswordHash = reader.GetString(7),
                    Salt = reader.GetString(8),
                    UserType = reader.GetString(9),
                    Email = reader.GetString(10),
                    MfaEnabled = reader.GetBoolean(11),
                    MfaSecretEnc = reader.GetString(12),
                    PasswordChangedAt = reader.IsDBNull(13) ? null : reader.GetDateTime(13)
                };
            }
        }
        catch (Exception ex)
        {
            AppLogger.LogException("auth", ex, nameof(GetUserByUsernameAsync));
        }
        return null;
    }

    public async Task<Central.Core.Auth.AuthUser?> GetUserByIdAsync(int userId)
    {
        try
        {
            await using var conn = CreateConnection();
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand(@"
                SELECT u.id, u.username, u.display_name, u.role, u.is_active,
                       COALESCE(r.id, 0) as role_id, COALESCE(r.priority, 0) as priority,
                       COALESCE(u.password_hash, '') as password_hash,
                       COALESCE(u.salt, '') as salt,
                       COALESCE(u.user_type, 'Standard') as user_type,
                       COALESCE(u.email, '') as email
                FROM app_users u
                LEFT JOIN roles r ON r.name = u.role
                WHERE u.id = @id", conn);
            cmd.Parameters.AddWithValue("id", userId);
            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new Central.Core.Auth.AuthUser
                {
                    Id = reader.GetInt32(0), Username = reader.GetString(1),
                    DisplayName = reader.IsDBNull(2) ? reader.GetString(1) : reader.GetString(2),
                    RoleId = reader.GetInt32(5), RoleName = reader.GetString(3),
                    Priority = reader.GetInt32(6), IsActive = reader.GetBoolean(4),
                    PasswordHash = reader.GetString(7), Salt = reader.GetString(8),
                    UserType = reader.GetString(9), Email = reader.GetString(10)
                };
            }
        }
        catch (Exception ex)
        {
            AppLogger.LogException("auth", ex, nameof(GetUserByIdAsync));
        }
        return null;
    }

    public async Task<Central.Core.Auth.AuthUser?> GetUserByEmailAsync(string email)
    {
        try
        {
            await using var conn = CreateConnection();
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand(@"
                SELECT u.id, u.username, u.display_name, u.role, u.is_active,
                       COALESCE(r.id, 0) as role_id, COALESCE(r.priority, 0) as priority,
                       COALESCE(u.password_hash, '') as password_hash,
                       COALESCE(u.salt, '') as salt,
                       COALESCE(u.user_type, 'Standard') as user_type,
                       COALESCE(u.email, '') as email
                FROM app_users u
                LEFT JOIN roles r ON r.name = u.role
                WHERE LOWER(u.email) = LOWER(@email)", conn);
            cmd.Parameters.AddWithValue("email", email);
            await using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new Central.Core.Auth.AuthUser
                {
                    Id = reader.GetInt32(0), Username = reader.GetString(1),
                    DisplayName = reader.IsDBNull(2) ? reader.GetString(1) : reader.GetString(2),
                    RoleId = reader.GetInt32(5), RoleName = reader.GetString(3),
                    Priority = reader.GetInt32(6), IsActive = reader.GetBoolean(4),
                    PasswordHash = reader.GetString(7), Salt = reader.GetString(8),
                    UserType = reader.GetString(9), Email = reader.GetString(10)
                };
            }
        }
        catch (Exception ex)
        {
            AppLogger.LogException("auth", ex, nameof(GetUserByEmailAsync));
        }
        return null;
    }
}
