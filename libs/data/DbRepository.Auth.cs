using Npgsql;
using Central.Core.Auth;

namespace Central.Data;

public partial class DbRepository
{
    // ── Identity Providers ────────────────────────────────────────────────

    public async Task<List<IdentityProviderConfig>> GetIdentityProvidersAsync(bool enabledOnly = false)
    {
        var list = new List<IdentityProviderConfig>();
        await using var conn = await OpenConnectionAsync();
        var where = enabledOnly ? "WHERE is_enabled = true" : "";
        await using var cmd = new NpgsqlCommand(
            $"SELECT id, provider_type, name, is_enabled, is_default, priority, config_json::text, metadata_url FROM identity_providers {where} ORDER BY priority", conn);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new IdentityProviderConfig
            {
                Id = r.GetInt32(0), ProviderType = r.GetString(1), Name = r.GetString(2),
                IsEnabled = r.GetBoolean(3), IsDefault = r.GetBoolean(4), Priority = r.GetInt32(5),
                ConfigJson = r.GetString(6), MetadataUrl = r.IsDBNull(7) ? null : r.GetString(7)
            });
        return list;
    }

    public async Task UpsertIdentityProviderAsync(IdentityProviderConfig p)
    {
        await using var conn = await OpenConnectionAsync();
        if (p.Id == 0)
        {
            await using var cmd = new NpgsqlCommand(
                @"INSERT INTO identity_providers (provider_type, name, is_enabled, is_default, priority, config_json, metadata_url)
                  VALUES (@pt, @n, @en, @def, @pri, @cfg::jsonb, @mu) RETURNING id", conn);
            cmd.Parameters.AddWithValue("pt", p.ProviderType); cmd.Parameters.AddWithValue("n", p.Name);
            cmd.Parameters.AddWithValue("en", p.IsEnabled); cmd.Parameters.AddWithValue("def", p.IsDefault);
            cmd.Parameters.AddWithValue("pri", p.Priority); cmd.Parameters.AddWithValue("cfg", p.ConfigJson);
            cmd.Parameters.AddWithValue("mu", (object?)p.MetadataUrl ?? DBNull.Value);
            p.Id = (int)(await cmd.ExecuteScalarAsync())!;
        }
        else
        {
            await using var cmd = new NpgsqlCommand(
                @"UPDATE identity_providers SET provider_type=@pt, name=@n, is_enabled=@en, is_default=@def,
                  priority=@pri, config_json=@cfg::jsonb, metadata_url=@mu, updated_at=NOW() WHERE id=@id", conn);
            cmd.Parameters.AddWithValue("id", p.Id); cmd.Parameters.AddWithValue("pt", p.ProviderType);
            cmd.Parameters.AddWithValue("n", p.Name); cmd.Parameters.AddWithValue("en", p.IsEnabled);
            cmd.Parameters.AddWithValue("def", p.IsDefault); cmd.Parameters.AddWithValue("pri", p.Priority);
            cmd.Parameters.AddWithValue("cfg", p.ConfigJson); cmd.Parameters.AddWithValue("mu", (object?)p.MetadataUrl ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    public async Task DeleteIdentityProviderAsync(int id)
    {
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand("DELETE FROM identity_providers WHERE id=@id", conn);
        cmd.Parameters.AddWithValue("id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    // ── Domain Mappings ───────────────────────────────────────────────────

    public async Task<IdentityProviderConfig?> GetProviderByDomainAsync(string emailDomain)
    {
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            @"SELECT p.id, p.provider_type, p.name, p.is_enabled, p.is_default, p.priority, p.config_json::text, p.metadata_url
              FROM identity_providers p JOIN idp_domain_mappings d ON d.provider_id = p.id
              WHERE d.email_domain = @dom AND p.is_enabled = true", conn);
        cmd.Parameters.AddWithValue("dom", emailDomain.ToLowerInvariant());
        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return null;
        return new IdentityProviderConfig
        {
            Id = r.GetInt32(0), ProviderType = r.GetString(1), Name = r.GetString(2),
            IsEnabled = r.GetBoolean(3), IsDefault = r.GetBoolean(4), Priority = r.GetInt32(5),
            ConfigJson = r.GetString(6), MetadataUrl = r.IsDBNull(7) ? null : r.GetString(7)
        };
    }

    public async Task<List<IdpDomainMapping>> GetDomainMappingsAsync()
    {
        var list = new List<IdpDomainMapping>();
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT d.id, d.email_domain, d.provider_id, p.name FROM idp_domain_mappings d JOIN identity_providers p ON p.id=d.provider_id ORDER BY d.email_domain", conn);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new IdpDomainMapping { Id = r.GetInt32(0), EmailDomain = r.GetString(1), ProviderId = r.GetInt32(2), ProviderName = r.GetString(3) });
        return list;
    }

    public async Task UpsertDomainMappingAsync(string emailDomain, int providerId)
    {
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            @"INSERT INTO idp_domain_mappings (email_domain, provider_id) VALUES (@dom, @pid)
              ON CONFLICT (email_domain) DO UPDATE SET provider_id=@pid", conn);
        cmd.Parameters.AddWithValue("dom", emailDomain.ToLowerInvariant());
        cmd.Parameters.AddWithValue("pid", providerId);
        await cmd.ExecuteNonQueryAsync();
    }

    // ── Claim Mappings ────────────────────────────────────────────────────

    public async Task<List<ClaimMapping>> GetClaimMappingsAsync(int? providerId = null)
    {
        var list = new List<ClaimMapping>();
        await using var conn = await OpenConnectionAsync();
        var where = providerId.HasValue ? "WHERE provider_id=@pid" : "";
        await using var cmd = new NpgsqlCommand(
            $"SELECT id, provider_id, claim_type, claim_value, target_role, priority, is_enabled FROM claim_mappings {where} ORDER BY priority", conn);
        if (providerId.HasValue) cmd.Parameters.AddWithValue("pid", providerId.Value);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new ClaimMapping
            {
                Id = r.GetInt32(0), ProviderId = r.IsDBNull(1) ? null : r.GetInt32(1),
                ClaimType = r.GetString(2), ClaimValue = r.GetString(3),
                TargetRole = r.GetString(4), Priority = r.GetInt32(5), IsEnabled = r.GetBoolean(6)
            });
        return list;
    }

    public async Task UpsertClaimMappingAsync(ClaimMapping cm)
    {
        await using var conn = await OpenConnectionAsync();
        if (cm.Id == 0)
        {
            await using var cmd = new NpgsqlCommand(
                @"INSERT INTO claim_mappings (provider_id, claim_type, claim_value, target_role, priority, is_enabled)
                  VALUES (@pid, @ct, @cv, @tr, @pri, @en) RETURNING id", conn);
            cmd.Parameters.AddWithValue("pid", (object?)cm.ProviderId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("ct", cm.ClaimType); cmd.Parameters.AddWithValue("cv", cm.ClaimValue);
            cmd.Parameters.AddWithValue("tr", cm.TargetRole); cmd.Parameters.AddWithValue("pri", cm.Priority);
            cmd.Parameters.AddWithValue("en", cm.IsEnabled);
            cm.Id = (int)(await cmd.ExecuteScalarAsync())!;
        }
        else
        {
            await using var cmd = new NpgsqlCommand(
                @"UPDATE claim_mappings SET provider_id=@pid, claim_type=@ct, claim_value=@cv,
                  target_role=@tr, priority=@pri, is_enabled=@en WHERE id=@id", conn);
            cmd.Parameters.AddWithValue("id", cm.Id); cmd.Parameters.AddWithValue("pid", (object?)cm.ProviderId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("ct", cm.ClaimType); cmd.Parameters.AddWithValue("cv", cm.ClaimValue);
            cmd.Parameters.AddWithValue("tr", cm.TargetRole); cmd.Parameters.AddWithValue("pri", cm.Priority);
            cmd.Parameters.AddWithValue("en", cm.IsEnabled);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    public async Task DeleteClaimMappingAsync(int id)
    {
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand("DELETE FROM claim_mappings WHERE id=@id", conn);
        cmd.Parameters.AddWithValue("id", id);
        await cmd.ExecuteNonQueryAsync();
    }

    // ── External Identities ───────────────────────────────────────────────

    public async Task<List<UserExternalIdentity>> GetUserExternalIdentitiesAsync(int? userId = null)
    {
        var list = new List<UserExternalIdentity>();
        await using var conn = await OpenConnectionAsync();
        var where = userId.HasValue ? "WHERE e.user_id=@uid" : "";
        await using var cmd = new NpgsqlCommand(
            $@"SELECT e.id, e.user_id, e.provider_id, e.external_id, e.external_email, e.linked_at, e.last_login_at, p.name, u.username
               FROM user_external_identities e
               JOIN identity_providers p ON p.id=e.provider_id
               JOIN app_users u ON u.id=e.user_id
               {where} ORDER BY e.linked_at DESC", conn);
        if (userId.HasValue) cmd.Parameters.AddWithValue("uid", userId.Value);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new UserExternalIdentity
            {
                Id = r.GetInt32(0), UserId = r.GetInt32(1), ProviderId = r.GetInt32(2),
                ExternalId = r.GetString(3), ExternalEmail = r.IsDBNull(4) ? null : r.GetString(4),
                LinkedAt = r.IsDBNull(5) ? null : r.GetDateTime(5),
                LastLoginAt = r.IsDBNull(6) ? null : r.GetDateTime(6),
                ProviderName = r.GetString(7), Username = r.GetString(8)
            });
        return list;
    }

    public async Task LinkExternalIdentityAsync(int userId, int providerId, string externalId, string? email)
    {
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            @"INSERT INTO user_external_identities (user_id, provider_id, external_id, external_email)
              VALUES (@uid, @pid, @eid, @email)
              ON CONFLICT (provider_id, external_id) DO UPDATE SET external_email=@email, last_login_at=NOW()", conn);
        cmd.Parameters.AddWithValue("uid", userId); cmd.Parameters.AddWithValue("pid", providerId);
        cmd.Parameters.AddWithValue("eid", externalId); cmd.Parameters.AddWithValue("email", (object?)email ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync();
    }

    // ── Auth Events ───────────────────────────────────────────────────────

    public async Task LogAuthEventAsync(string eventType, string? username, bool success,
        string? providerType = null, int? userId = null, string? errorMessage = null)
    {
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            @"INSERT INTO auth_events (event_type, provider_type, username, user_id, success, error_message)
              VALUES (@et, @pt, @u, @uid, @s, @err)", conn);
        cmd.Parameters.AddWithValue("et", eventType);
        cmd.Parameters.AddWithValue("pt", (object?)providerType ?? DBNull.Value);
        cmd.Parameters.AddWithValue("u", (object?)username ?? DBNull.Value);
        cmd.Parameters.AddWithValue("uid", (object?)userId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("s", success);
        cmd.Parameters.AddWithValue("err", (object?)errorMessage ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<AuthEvent>> GetAuthEventsAsync(int limit = 200)
    {
        var list = new List<AuthEvent>();
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            $"SELECT id, timestamp, event_type, provider_type, username, user_id, success, error_message FROM auth_events ORDER BY timestamp DESC LIMIT {limit}", conn);
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new AuthEvent
            {
                Id = r.GetInt64(0), Timestamp = r.GetDateTime(1), EventType = r.GetString(2),
                ProviderType = r.IsDBNull(3) ? null : r.GetString(3),
                Username = r.IsDBNull(4) ? null : r.GetString(4),
                UserId = r.IsDBNull(5) ? null : r.GetInt32(5),
                Success = r.GetBoolean(6), ErrorMessage = r.IsDBNull(7) ? null : r.GetString(7)
            });
        return list;
    }

    // ── Brute-Force Lockout ───────────────────────────────────────────────

    public async Task IncrementFailedLoginAsync(string username)
    {
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            @"UPDATE app_users SET failed_login_count = COALESCE(failed_login_count, 0) + 1 WHERE username=@u", conn);
        cmd.Parameters.AddWithValue("u", username);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task LockUserAsync(string username, int lockoutMinutes)
    {
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            @"UPDATE app_users SET locked_until = NOW() + @dur * INTERVAL '1 minute' WHERE username=@u", conn);
        cmd.Parameters.AddWithValue("u", username);
        cmd.Parameters.AddWithValue("dur", lockoutMinutes);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task ResetFailedLoginAsync(string username)
    {
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            @"UPDATE app_users SET failed_login_count = 0, locked_until = NULL WHERE username=@u", conn);
        cmd.Parameters.AddWithValue("u", username);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<(int failedCount, DateTime? lockedUntil)> GetLockoutStatusAsync(string username)
    {
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            "SELECT COALESCE(failed_login_count,0), locked_until FROM app_users WHERE username=@u", conn);
        cmd.Parameters.AddWithValue("u", username);
        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return (0, null);
        return (r.GetInt32(0), r.IsDBNull(1) ? null : r.GetDateTime(1));
    }

    /// <summary>Re-hash a user's password (e.g., migrating from SHA256 to Argon2id).</summary>
    public async Task UpdatePasswordHashAsync(int userId, string newHash, string newSalt)
    {
        await using var conn = await OpenConnectionAsync();
        await using var cmd = new NpgsqlCommand(
            "UPDATE app_users SET password_hash = @hash, salt = @salt WHERE id = @id", conn);
        cmd.Parameters.AddWithValue("hash", newHash);
        cmd.Parameters.AddWithValue("salt", newSalt);
        cmd.Parameters.AddWithValue("id", userId);
        await cmd.ExecuteNonQueryAsync();
    }
}
