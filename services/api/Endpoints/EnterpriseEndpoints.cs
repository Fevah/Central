using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using Npgsql;
using Central.Data;

namespace Central.Api.Endpoints;

/// <summary>Feature flags CRUD.</summary>
public static class FeatureFlagEndpoints
{
    public static RouteGroupBuilder MapFeatureFlagEndpoints(this RouteGroupBuilder group)
    {
        // List global flags with tenant overrides merged
        group.MapGet("/", async (DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand("SELECT * FROM feature_flags ORDER BY category, flag_key", conn);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        // Get tenant-specific flags (or global default)
        group.MapGet("/tenant/{tenantId:guid}", async (Guid tenantId, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(@"
                SELECT f.flag_key, f.name, f.category,
                       COALESCE(tf.is_enabled, f.default_enabled) as is_enabled,
                       tf.rollout_pct, tf.enabled_at, tf.disabled_at
                FROM feature_flags f
                LEFT JOIN tenant_feature_flags tf ON tf.flag_key = f.flag_key AND tf.tenant_id = @tid
                ORDER BY f.category, f.flag_key", conn);
            cmd.Parameters.AddWithValue("tid", tenantId);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        // Set a tenant-specific flag
        group.MapPost("/tenant/{tenantId:guid}", async (Guid tenantId, JsonElement body, DbConnectionFactory db) =>
        {
            var flagKey = body.GetProperty("flag_key").GetString() ?? "";
            var enabled = body.GetProperty("is_enabled").GetBoolean();
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(@"
                INSERT INTO tenant_feature_flags (tenant_id, flag_key, is_enabled, enabled_at)
                VALUES (@tid, @fk, @en, CASE WHEN @en THEN NOW() ELSE NULL END)
                ON CONFLICT (tenant_id, flag_key) DO UPDATE SET is_enabled = @en,
                    enabled_at = CASE WHEN @en THEN NOW() ELSE tenant_feature_flags.enabled_at END,
                    disabled_at = CASE WHEN NOT @en THEN NOW() ELSE NULL END
                RETURNING id", conn);
            cmd.Parameters.AddWithValue("tid", tenantId);
            cmd.Parameters.AddWithValue("fk", flagKey);
            cmd.Parameters.AddWithValue("en", enabled);
            var id = (int)(await cmd.ExecuteScalarAsync())!;
            return Results.Ok(new { id, flag_key = flagKey, is_enabled = enabled });
        });

        return group;
    }
}

/// <summary>IP allowlist / blocklist endpoints.</summary>
public static class IpRulesEndpoints
{
    public static RouteGroupBuilder MapIpRulesEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", async (DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                "SELECT * FROM ip_access_rules WHERE is_active = true ORDER BY rule_type, cidr", conn);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        group.MapPost("/", async (JsonElement body, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                @"INSERT INTO ip_access_rules (cidr, rule_type, label, applies_to, expires_at)
                  VALUES (@c::cidr, @t, @l, @a, @e) RETURNING id", conn);
            cmd.Parameters.AddWithValue("c", body.GetProperty("cidr").GetString() ?? "");
            cmd.Parameters.AddWithValue("t", body.TryGetProperty("rule_type", out var t) ? t.GetString() ?? "allow" : "allow");
            cmd.Parameters.AddWithValue("l", body.TryGetProperty("label", out var l) ? l.GetString() ?? "" : "");
            cmd.Parameters.AddWithValue("a", body.TryGetProperty("applies_to", out var a) ? a.GetString() ?? "api" : "api");
            cmd.Parameters.AddWithValue("e", body.TryGetProperty("expires_at", out var e) && e.ValueKind == JsonValueKind.String ? DateTime.Parse(e.GetString()!) : DBNull.Value);
            var id = (int)(await cmd.ExecuteScalarAsync())!;
            return Results.Created($"/api/security/ip-rules/{id}", new { id });
        });

        group.MapDelete("/{id:int}", async (int id, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand("UPDATE ip_access_rules SET is_active = false WHERE id = @id RETURNING id", conn);
            cmd.Parameters.AddWithValue("id", id);
            var r = await cmd.ExecuteScalarAsync();
            return r is null ? ApiProblem.NotFound("Rule not found") : Results.NoContent();
        });

        return group;
    }
}

/// <summary>User SSH public key management.</summary>
public static class UserKeyEndpoints
{
    public static RouteGroupBuilder MapUserKeyEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", async (ClaimsPrincipal principal, DbConnectionFactory db) =>
        {
            var username = principal.Identity?.Name;
            if (string.IsNullOrEmpty(username)) return Results.Unauthorized();
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(@"
                SELECT k.id, k.label, k.key_type, k.fingerprint, k.is_active, k.last_used_at, k.created_at, k.expires_at
                FROM user_ssh_keys k JOIN app_users u ON u.id = k.user_id
                WHERE u.username = @u ORDER BY k.created_at DESC", conn);
            cmd.Parameters.AddWithValue("u", username);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        group.MapPost("/", async (ClaimsPrincipal principal, JsonElement body, DbConnectionFactory db) =>
        {
            var username = principal.Identity?.Name;
            if (string.IsNullOrEmpty(username)) return Results.Unauthorized();
            var publicKey = body.GetProperty("public_key").GetString() ?? "";
            var label = body.GetProperty("label").GetString() ?? "";
            if (string.IsNullOrWhiteSpace(publicKey)) return ApiProblem.ValidationError("public_key required");

            // Parse key type + compute fingerprint
            var parts = publicKey.Trim().Split(' ');
            var keyType = parts.Length > 0 ? parts[0] : "unknown";
            var fingerprint = "";
            try
            {
                if (parts.Length > 1)
                {
                    var keyBytes = Convert.FromBase64String(parts[1]);
                    var hash = SHA256.HashData(keyBytes);
                    fingerprint = "SHA256:" + Convert.ToBase64String(hash).TrimEnd('=');
                }
            }
            catch { return ApiProblem.ValidationError("Invalid public key format"); }

            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(@"
                INSERT INTO user_ssh_keys (user_id, label, key_type, public_key, fingerprint)
                SELECT u.id, @l, @t, @pk, @fp FROM app_users u WHERE u.username = @u
                RETURNING id", conn);
            cmd.Parameters.AddWithValue("u", username);
            cmd.Parameters.AddWithValue("l", label);
            cmd.Parameters.AddWithValue("t", keyType);
            cmd.Parameters.AddWithValue("pk", publicKey);
            cmd.Parameters.AddWithValue("fp", fingerprint);
            var id = (int)(await cmd.ExecuteScalarAsync())!;
            return Results.Created($"/api/user-keys/{id}", new { id, fingerprint });
        });

        group.MapDelete("/{id:int}", async (int id, ClaimsPrincipal principal, DbConnectionFactory db) =>
        {
            var username = principal.Identity?.Name;
            if (string.IsNullOrEmpty(username)) return Results.Unauthorized();
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(@"
                DELETE FROM user_ssh_keys WHERE id = @id AND user_id IN (SELECT id FROM app_users WHERE username = @u)
                RETURNING id", conn);
            cmd.Parameters.AddWithValue("id", id);
            cmd.Parameters.AddWithValue("u", username);
            var r = await cmd.ExecuteScalarAsync();
            return r is null ? ApiProblem.NotFound("Key not found") : Results.NoContent();
        });

        return group;
    }
}

/// <summary>Password reset + email verification + ToS acceptance.</summary>
public static class AccountRecoveryEndpoints
{
    public static RouteGroupBuilder MapAccountRecoveryEndpoints(this RouteGroupBuilder group)
    {
        // Request password reset (anonymous)
        group.MapPost("/password-reset/request", async (JsonElement body, DbConnectionFactory db) =>
        {
            var email = body.GetProperty("email").GetString() ?? "";
            if (string.IsNullOrWhiteSpace(email)) return ApiProblem.ValidationError("Email required");

            await using var conn = await db.OpenConnectionAsync();
            await using var lookup = new NpgsqlCommand("SELECT id FROM app_users WHERE LOWER(email) = LOWER(@e)", conn);
            lookup.Parameters.AddWithValue("e", email);
            var userId = await lookup.ExecuteScalarAsync();
            // Always return success to prevent user enumeration
            if (userId is not int uid) return Results.Ok(new { sent = true });

            var token = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
            var tokenHash = Convert.ToBase64String(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)));

            await using var ins = new NpgsqlCommand(
                @"INSERT INTO password_reset_tokens (user_id, token_hash, expires_at)
                  VALUES (@u, @h, NOW() + interval '1 hour') RETURNING id", conn);
            ins.Parameters.AddWithValue("u", uid);
            ins.Parameters.AddWithValue("h", tokenHash);
            await ins.ExecuteScalarAsync();

            // In production: send email with reset link containing the raw token
            return Results.Ok(new { sent = true, token });  // token returned for dev; remove in prod
        }).AllowAnonymous();

        // Complete password reset (anonymous, needs token)
        group.MapPost("/password-reset/confirm", async (JsonElement body, DbConnectionFactory db) =>
        {
            var token = body.GetProperty("token").GetString() ?? "";
            var newPassword = body.GetProperty("new_password").GetString() ?? "";
            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(newPassword))
                return ApiProblem.ValidationError("Token and new_password required");

            var tokenHash = Convert.ToBase64String(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)));

            // Validate policy
            var policy = Central.Core.Auth.PasswordPolicy.Default;
            var validation = policy.Validate(newPassword);
            if (!validation.IsValid)
                return ApiProblem.ValidationError(validation.ErrorSummary);

            await using var conn = await db.OpenConnectionAsync();
            await using var lookup = new NpgsqlCommand(
                @"SELECT id, user_id FROM password_reset_tokens
                  WHERE token_hash = @h AND used_at IS NULL AND expires_at > NOW()", conn);
            lookup.Parameters.AddWithValue("h", tokenHash);
            await using var rd = await lookup.ExecuteReaderAsync();
            if (!await rd.ReadAsync()) return ApiProblem.NotFound("Invalid or expired token");
            var tokenId = rd.GetInt32(0);
            var userId = rd.GetInt32(1);
            await rd.CloseAsync();

            var salt = Central.Core.Auth.PasswordHasher.GenerateSalt();
            var hash = Central.Core.Auth.PasswordHasher.Hash(newPassword, salt);

            await using var update = new NpgsqlCommand(
                @"UPDATE app_users SET password_hash = @h, salt = @s, password_changed_at = NOW(),
                  must_change_password = false WHERE id = @u", conn);
            update.Parameters.AddWithValue("h", hash);
            update.Parameters.AddWithValue("s", salt);
            update.Parameters.AddWithValue("u", userId);
            await update.ExecuteNonQueryAsync();

            await using var mark = new NpgsqlCommand("UPDATE password_reset_tokens SET used_at = NOW() WHERE id = @id", conn);
            mark.Parameters.AddWithValue("id", tokenId);
            await mark.ExecuteNonQueryAsync();

            return Results.Ok(new { password_reset = true });
        }).AllowAnonymous();

        // Verify email (anonymous)
        group.MapPost("/verify-email", async (JsonElement body, DbConnectionFactory db) =>
        {
            var token = body.GetProperty("token").GetString() ?? "";
            var tokenHash = Convert.ToBase64String(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token)));

            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                @"UPDATE email_verification_tokens SET verified_at = NOW()
                  WHERE token_hash = @h AND verified_at IS NULL AND expires_at > NOW()
                  RETURNING user_id, email", conn);
            cmd.Parameters.AddWithValue("h", tokenHash);
            await using var r = await cmd.ExecuteReaderAsync();
            if (!await r.ReadAsync()) return ApiProblem.NotFound("Invalid or expired token");
            var uid = r.GetInt32(0);
            var email = r.GetString(1);
            await r.CloseAsync();

            await using var mark = new NpgsqlCommand("UPDATE app_users SET email_verified_at = NOW() WHERE id = @u", conn);
            mark.Parameters.AddWithValue("u", uid);
            await mark.ExecuteNonQueryAsync();

            return Results.Ok(new { email_verified = true, email });
        }).AllowAnonymous();

        // Current ToS
        group.MapGet("/tos/current", async (DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                "SELECT * FROM terms_of_service ORDER BY effective_date DESC LIMIT 1", conn);
            await using var r = await cmd.ExecuteReaderAsync();
            if (!await r.ReadAsync()) return Results.Ok(new { tos = (object?)null });
            var row = new Dictionary<string, object?>();
            for (int i = 0; i < r.FieldCount; i++) row[r.GetName(i)] = r.IsDBNull(i) ? null : r.GetValue(i);
            return Results.Ok(row);
        }).AllowAnonymous();

        // Accept ToS
        group.MapPost("/tos/accept", async (ClaimsPrincipal principal, JsonElement body, HttpContext ctx, DbConnectionFactory db) =>
        {
            var username = principal.Identity?.Name;
            if (string.IsNullOrEmpty(username)) return Results.Unauthorized();
            var tosId = body.GetProperty("tos_id").GetInt32();

            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                @"INSERT INTO user_tos_acceptance (user_id, tos_id, ip_address, user_agent)
                  SELECT u.id, @t, @ip::inet, @ua FROM app_users u WHERE u.username = @un
                  ON CONFLICT (user_id, tos_id) DO NOTHING RETURNING id", conn);
            cmd.Parameters.AddWithValue("un", username);
            cmd.Parameters.AddWithValue("t", tosId);
            cmd.Parameters.AddWithValue("ip", (object)(ctx.Connection.RemoteIpAddress?.ToString() ?? "0.0.0.0"));
            cmd.Parameters.AddWithValue("ua", (object)(ctx.Request.Headers.UserAgent.ToString() ?? ""));
            await cmd.ExecuteScalarAsync();
            return Results.Ok(new { accepted = true });
        });

        return group;
    }
}

/// <summary>Social OAuth provider management (admin-only).</summary>
public static class SocialProviderEndpoints
{
    public static RouteGroupBuilder MapSocialProviderEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/", async (DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                "SELECT provider, display_name, client_id, scope, is_enabled, button_color FROM social_providers ORDER BY display_name", conn);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        });

        group.MapPut("/{provider}", async (string provider, JsonElement body, DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(@"
                UPDATE social_providers SET client_id = @cid, client_secret_enc = @secret,
                    is_enabled = @en, scope = COALESCE(@sc, scope), updated_at = NOW()
                WHERE provider = @p RETURNING provider", conn);
            cmd.Parameters.AddWithValue("p", provider);
            cmd.Parameters.AddWithValue("cid", body.TryGetProperty("client_id", out var cid) ? cid.GetString() ?? "" : "");
            cmd.Parameters.AddWithValue("secret", body.TryGetProperty("client_secret", out var sec) && !string.IsNullOrEmpty(sec.GetString())
                ? Central.Core.Auth.CredentialEncryptor.Encrypt(sec.GetString()!) : "");
            cmd.Parameters.AddWithValue("en", body.TryGetProperty("is_enabled", out var en) && en.GetBoolean());
            cmd.Parameters.AddWithValue("sc", body.TryGetProperty("scope", out var sc) ? (object)(sc.GetString() ?? "") : DBNull.Value);
            var r = await cmd.ExecuteScalarAsync();
            return r is null ? ApiProblem.NotFound($"Provider {provider} not found") : Results.Ok(new { provider });
        });

        // Public list (only enabled providers)
        group.MapGet("/enabled", async (DbConnectionFactory db) =>
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(
                "SELECT provider, display_name, button_color, icon_url FROM social_providers WHERE is_enabled = true ORDER BY display_name", conn);
            await using var r = await cmd.ExecuteReaderAsync();
            return Results.Ok(await EndpointHelpers.ReadRowsAsync(r));
        }).AllowAnonymous();

        return group;
    }
}
