using System.Security.Cryptography;
using System.Text;
using Npgsql;
using Central.Tenancy;

namespace Central.Licensing;

/// <summary>
/// Handles user self-registration, tenant provisioning, and email verification.
/// Flow: Register → verify email → tenant schema created → user can login.
/// </summary>
public class RegistrationService
{
    private readonly string _platformDsn;
    private readonly TenantSchemaManager _schemaManager;

    public RegistrationService(string platformDsn)
    {
        _platformDsn = platformDsn;
        _schemaManager = new TenantSchemaManager(platformDsn);
    }

    /// <summary>Register a new user + create their tenant.</summary>
    public async Task<RegistrationResult> RegisterAsync(RegistrationRequest request)
    {
        // Validate
        if (string.IsNullOrWhiteSpace(request.Email) || !request.Email.Contains('@'))
            return RegistrationResult.Fail("Valid email required");
        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
            return RegistrationResult.Fail("Password must be at least 8 characters");
        if (string.IsNullOrWhiteSpace(request.CompanyName))
            return RegistrationResult.Fail("Company name required");

        var slug = GenerateSlug(request.CompanyName);

        await using var conn = new NpgsqlConnection(_platformDsn);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand("SET search_path TO central_platform", conn);
        await cmd.ExecuteNonQueryAsync();

        // Check uniqueness
        await using var checkEmail = new NpgsqlCommand("SELECT COUNT(*) FROM global_users WHERE email = @e", conn);
        checkEmail.Parameters.AddWithValue("e", request.Email.ToLowerInvariant());
        if ((long)(await checkEmail.ExecuteScalarAsync())! > 0)
            return RegistrationResult.Fail("Email already registered");

        await using var checkSlug = new NpgsqlCommand("SELECT COUNT(*) FROM tenants WHERE slug = @s", conn);
        checkSlug.Parameters.AddWithValue("s", slug);
        if ((long)(await checkSlug.ExecuteScalarAsync())! > 0)
            slug += "-" + Guid.NewGuid().ToString("N")[..6];

        // Create global user
        var salt = GenerateSalt();
        var hash = HashPassword(request.Password, salt);
        var verifyToken = Guid.NewGuid().ToString("N");
        var userId = Guid.NewGuid();

        await using var insertUser = new NpgsqlCommand(
            @"INSERT INTO global_users (id, email, display_name, password_hash, salt, verify_token)
              VALUES (@id, @email, @name, @hash, @salt, @token)", conn);
        insertUser.Parameters.AddWithValue("id", userId);
        insertUser.Parameters.AddWithValue("email", request.Email.ToLowerInvariant());
        insertUser.Parameters.AddWithValue("name", request.DisplayName ?? request.Email.Split('@')[0]);
        insertUser.Parameters.AddWithValue("hash", hash);
        insertUser.Parameters.AddWithValue("salt", salt);
        insertUser.Parameters.AddWithValue("token", verifyToken);
        await insertUser.ExecuteNonQueryAsync();

        // Create tenant
        var tenantId = Guid.NewGuid();
        await using var insertTenant = new NpgsqlCommand(
            @"INSERT INTO tenants (id, slug, display_name, domain, tier)
              VALUES (@id, @slug, @name, @domain, 'free')", conn);
        insertTenant.Parameters.AddWithValue("id", tenantId);
        insertTenant.Parameters.AddWithValue("slug", slug);
        insertTenant.Parameters.AddWithValue("name", request.CompanyName);
        insertTenant.Parameters.AddWithValue("domain", request.Email.Split('@')[1]);
        await insertTenant.ExecuteNonQueryAsync();

        // Create free subscription
        await using var insertSub = new NpgsqlCommand(
            @"INSERT INTO tenant_subscriptions (tenant_id, plan_id, status)
              SELECT @tid, id, 'trial' FROM subscription_plans WHERE tier = 'free'", conn);
        insertSub.Parameters.AddWithValue("tid", tenantId);
        await insertSub.ExecuteNonQueryAsync();

        // Grant base modules
        await using var grantModules = new NpgsqlCommand(
            @"INSERT INTO tenant_module_licenses (tenant_id, module_id)
              SELECT @tid, id FROM module_catalog WHERE is_base = true", conn);
        grantModules.Parameters.AddWithValue("tid", tenantId);
        await grantModules.ExecuteNonQueryAsync();

        // Link user to tenant as Admin
        await using var insertMembership = new NpgsqlCommand(
            @"INSERT INTO tenant_memberships (user_id, tenant_id, role) VALUES (@uid, @tid, 'Admin')", conn);
        insertMembership.Parameters.AddWithValue("uid", userId);
        insertMembership.Parameters.AddWithValue("tid", tenantId);
        await insertMembership.ExecuteNonQueryAsync();

        return new RegistrationResult
        {
            Success = true,
            UserId = userId,
            TenantId = tenantId,
            TenantSlug = slug,
            VerifyToken = verifyToken
        };
    }

    /// <summary>Provision the tenant's database schema (call after email verification).</summary>
    public async Task ProvisionTenantSchemaAsync(string slug, string migrationsDir)
    {
        var schemaName = $"tenant_{slug}";
        await _schemaManager.EnsurePlatformSchemaAsync();
        await _schemaManager.ProvisionTenantAsync(schemaName, migrationsDir);
    }

    /// <summary>Verify email with token.</summary>
    public async Task<bool> VerifyEmailAsync(string token)
    {
        await using var conn = new NpgsqlConnection(_platformDsn);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "UPDATE central_platform.global_users SET email_verified = true, verify_token = NULL WHERE verify_token = @t AND email_verified = false", conn);
        cmd.Parameters.AddWithValue("t", token);
        return await cmd.ExecuteNonQueryAsync() > 0;
    }

    /// <summary>Check if a slug is available.</summary>
    public async Task<bool> IsSlugAvailableAsync(string slug)
    {
        await using var conn = new NpgsqlConnection(_platformDsn);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT COUNT(*) FROM central_platform.tenants WHERE slug = @s", conn);
        cmd.Parameters.AddWithValue("s", slug);
        return (long)(await cmd.ExecuteScalarAsync())! == 0;
    }

    private static string GenerateSlug(string name)
    {
        return System.Text.RegularExpressions.Regex.Replace(
            name.ToLowerInvariant().Trim(), @"[^a-z0-9]+", "-").Trim('-');
    }

    private static string GenerateSalt()
    {
        var bytes = new byte[16];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes);
    }

    private static string HashPassword(string password, string salt)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(password + salt));
        return Convert.ToBase64String(bytes);
    }
}

public class RegistrationRequest
{
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
    public string CompanyName { get; set; } = "";
    public string? DisplayName { get; set; }
}

public class RegistrationResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public Guid? UserId { get; set; }
    public Guid? TenantId { get; set; }
    public string? TenantSlug { get; set; }
    public string? VerifyToken { get; set; }

    public static RegistrationResult Fail(string error) => new() { Success = false, ErrorMessage = error };
}
