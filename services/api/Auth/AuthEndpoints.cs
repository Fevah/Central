using System.Collections.Concurrent;
using System.Security.Claims;
using Central.Core.Auth;
using Central.Data;
using Central.Data.Repositories;

namespace Central.Api.Auth;

public static class AuthEndpoints
{
    private const int MaxAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    // Pending MFA sessions — short-lived, keyed by random token
    private static readonly ConcurrentDictionary<string, MfaPendingSession> _mfaPending = new();

    public static RouteGroupBuilder MapAuthEndpoints(this RouteGroupBuilder group)
    {
        // ── POST /api/auth/login ──
        group.MapPost("/login", async (LoginRequest req, TokenService tokens, DbConnectionFactory db) =>
        {
            if (string.IsNullOrWhiteSpace(req.Username))
                return Results.BadRequest(new { error = "Username required" });

            var permRepo = new PermissionRepository(db.ConnectionString);
            var dbRepo = new DbRepository(db.ConnectionString);

            // Check lockout (database-backed)
            var (failedCount, lockedUntil) = await dbRepo.GetLockoutStatusAsync(req.Username);
            if (lockedUntil.HasValue && lockedUntil.Value > DateTime.UtcNow)
            {
                var remaining = (int)(lockedUntil.Value - DateTime.UtcNow).TotalMinutes;
                await dbRepo.LogAuthEventAsync("failed", req.Username, false, "local",
                    errorMessage: $"Account locked ({remaining}m remaining)");
                return Results.Json(new { error = $"Account locked. Try again in {remaining} minutes." },
                    statusCode: 429);
            }

            var user = await permRepo.GetUserByUsernameAsync(req.Username);
            if (user == null)
            {
                await dbRepo.IncrementFailedLoginAsync(req.Username);
                if (failedCount + 1 >= MaxAttempts)
                    await dbRepo.LockUserAsync(req.Username, (int)LockoutDuration.TotalMinutes);
                await dbRepo.LogAuthEventAsync("failed", req.Username, false, "local",
                    errorMessage: "User not found");
                return Results.Unauthorized();
            }

            if (!user.IsActive)
            {
                await dbRepo.LogAuthEventAsync("failed", req.Username, false, "local",
                    errorMessage: "Account disabled");
                return Results.Json(new { error = "Account is disabled" }, statusCode: 403);
            }

            // Verify password (Argon2id with legacy SHA256 fallback)
            if (!string.IsNullOrEmpty(user.PasswordHash) && !string.IsNullOrEmpty(user.Salt))
            {
                bool passwordValid = PasswordHasher.Verify(req.Password ?? "", user.Salt, user.PasswordHash);
                bool needsRehash = false;

                if (!passwordValid)
                {
                    passwordValid = PasswordHasher.VerifyLegacySha256(req.Password ?? "", user.Salt, user.PasswordHash);
                    needsRehash = passwordValid;
                }

                if (!passwordValid)
                {
                    await dbRepo.IncrementFailedLoginAsync(req.Username);
                    if (failedCount + 1 >= MaxAttempts)
                        await dbRepo.LockUserAsync(req.Username, (int)LockoutDuration.TotalMinutes);
                    await dbRepo.LogAuthEventAsync("failed", req.Username, false, "local",
                        userId: user.Id, errorMessage: $"Invalid password (attempt {failedCount + 1})");
                    return Results.Unauthorized();
                }

                if (needsRehash)
                {
                    try
                    {
                        var newSalt = PasswordHasher.GenerateSalt();
                        var newHash = PasswordHasher.Hash(req.Password!, newSalt);
                        await dbRepo.UpdatePasswordHashAsync(user.Id, newHash, newSalt);
                    }
                    catch { }
                }
            }

            // Check password expiry
            var policy = PasswordPolicy.Default;
            if (policy.IsExpired(user.PasswordChangedAt))
            {
                await dbRepo.LogAuthEventAsync("password_expired", req.Username, true, "local", userId: user.Id);
                return Results.Json(new
                {
                    error = "Password expired",
                    password_expired = true,
                    user_id = user.Id
                }, statusCode: 403);
            }

            // Reset lockout on successful password verification
            await dbRepo.ResetFailedLoginAsync(req.Username);

            // ── MFA Challenge ──
            if (user.MfaEnabled)
            {
                var mfaToken = Guid.NewGuid().ToString("N");
                _mfaPending[mfaToken] = new MfaPendingSession(user, DateTime.UtcNow.AddMinutes(5));

                // Cleanup expired sessions periodically
                if (_mfaPending.Count > 1000)
                    foreach (var kv in _mfaPending.Where(p => p.Value.ExpiresAt < DateTime.UtcNow).ToList())
                        _mfaPending.TryRemove(kv.Key, out _);

                await dbRepo.LogAuthEventAsync("mfa_challenge", req.Username, true, "local", userId: user.Id);
                return Results.Json(new
                {
                    requires_mfa = true,
                    mfa_token = mfaToken,
                    username = user.Username
                }, statusCode: 200);
            }

            // ── No MFA — issue token directly ──
            return await IssueTokenAsync(user, tokens, permRepo, dbRepo, db);
        })
        .AllowAnonymous();

        // ── POST /api/auth/mfa/verify ──
        group.MapPost("/mfa/verify", async (MfaVerifyRequest req, TokenService tokens, DbConnectionFactory db) =>
        {
            if (string.IsNullOrEmpty(req.MfaToken) || string.IsNullOrEmpty(req.Code))
                return Results.BadRequest(new { error = "MFA token and code are required" });

            if (!_mfaPending.TryRemove(req.MfaToken, out var pending) || pending.ExpiresAt < DateTime.UtcNow)
                return Results.Json(new { error = "MFA session expired or invalid. Please login again." }, statusCode: 401);

            var user = pending.User;
            var dbRepo = new DbRepository(db.ConnectionString);

            // Try TOTP code
            bool codeValid = false;
            if (!string.IsNullOrEmpty(user.MfaSecretEnc))
            {
                var secret = CredentialEncryptor.DecryptOrPassthrough(user.MfaSecretEnc);
                codeValid = TotpService.VerifyCode(secret, req.Code);
            }

            // Try recovery code if TOTP failed
            if (!codeValid)
                codeValid = await dbRepo.VerifyRecoveryCodeAsync(user.Id, req.Code);

            if (!codeValid)
            {
                await dbRepo.LogAuthEventAsync("mfa_failed", user.Username, false, "local",
                    userId: user.Id, errorMessage: "Invalid MFA code");
                return Results.Json(new { error = "Invalid MFA code" }, statusCode: 401);
            }

            await dbRepo.LogAuthEventAsync("mfa_verified", user.Username, true, "local", userId: user.Id);

            var permRepo = new PermissionRepository(db.ConnectionString);
            return await IssueTokenAsync(user, tokens, permRepo, dbRepo, db);
        })
        .AllowAnonymous();

        // ── POST /api/auth/mfa/setup ── (authenticated — user enables MFA on their account)
        group.MapPost("/mfa/setup", async (ClaimsPrincipal principal, DbConnectionFactory db) =>
        {
            var username = principal.Identity?.Name;
            if (string.IsNullOrEmpty(username))
                return Results.Unauthorized();

            var permRepo = new PermissionRepository(db.ConnectionString);
            var user = await permRepo.GetUserByUsernameAsync(username);
            if (user == null) return Results.Unauthorized();
            if (user.MfaEnabled) return Results.BadRequest(new { error = "MFA is already enabled" });

            // Generate secret and recovery codes
            var secret = TotpService.GenerateSecret();
            var qrUri = TotpService.GenerateQrUri(secret, user.Email.Length > 0 ? user.Email : user.Username);
            var recoveryCodes = TotpService.GenerateRecoveryCodes();

            // Store encrypted secret (not yet enabled — user must verify first)
            var encSecret = CredentialEncryptor.IsAvailable
                ? CredentialEncryptor.Encrypt(secret)
                : secret;

            var dbRepo = new DbRepository(db.ConnectionString);

            return Results.Ok(new
            {
                secret,
                qr_uri = qrUri,
                recovery_codes = recoveryCodes,
                // Client must call /api/auth/mfa/confirm with a valid code to enable
                confirm_token = await StorePendingMfaSetupAsync(dbRepo, user.Id, encSecret, recoveryCodes)
            });
        })
        .RequireAuthorization();

        // ── POST /api/auth/mfa/confirm ── (user verifies their first TOTP code to enable MFA)
        group.MapPost("/mfa/confirm", async (MfaConfirmRequest req, ClaimsPrincipal principal, DbConnectionFactory db) =>
        {
            var username = principal.Identity?.Name;
            if (string.IsNullOrEmpty(username))
                return Results.Unauthorized();

            var permRepo = new PermissionRepository(db.ConnectionString);
            var user = await permRepo.GetUserByUsernameAsync(username);
            if (user == null) return Results.Unauthorized();

            if (string.IsNullOrEmpty(req.Secret) || string.IsNullOrEmpty(req.Code))
                return Results.BadRequest(new { error = "Secret and code are required" });

            // Verify the code against the provided secret
            if (!TotpService.VerifyCode(req.Secret, req.Code))
                return Results.BadRequest(new { error = "Invalid TOTP code. Check your authenticator app." });

            // Enable MFA
            var encSecret = CredentialEncryptor.IsAvailable
                ? CredentialEncryptor.Encrypt(req.Secret)
                : req.Secret;

            var dbRepo = new DbRepository(db.ConnectionString);
            await dbRepo.EnableMfaAsync(user.Id, encSecret);

            // Save recovery codes
            if (req.RecoveryCodes?.Count > 0)
                await dbRepo.SaveRecoveryCodesAsync(user.Id, req.RecoveryCodes);

            await dbRepo.LogAuthEventAsync("mfa_enabled", username, true, "local", userId: user.Id);
            return Results.Ok(new { mfa_enabled = true });
        })
        .RequireAuthorization();

        // ── POST /api/auth/mfa/disable ── (user disables MFA)
        group.MapPost("/mfa/disable", async (ClaimsPrincipal principal, DbConnectionFactory db) =>
        {
            var username = principal.Identity?.Name;
            if (string.IsNullOrEmpty(username)) return Results.Unauthorized();

            var permRepo = new PermissionRepository(db.ConnectionString);
            var user = await permRepo.GetUserByUsernameAsync(username);
            if (user == null) return Results.Unauthorized();

            var dbRepo = new DbRepository(db.ConnectionString);
            await dbRepo.DisableMfaAsync(user.Id);
            await dbRepo.LogAuthEventAsync("mfa_disabled", username, true, "local", userId: user.Id);
            return Results.Ok(new { mfa_enabled = false });
        })
        .RequireAuthorization();

        // ── POST /api/auth/change-password ──
        group.MapPost("/change-password", async (ChangePasswordRequest req, ClaimsPrincipal principal, DbConnectionFactory db) =>
        {
            var username = principal.Identity?.Name;
            if (string.IsNullOrEmpty(username)) return Results.Unauthorized();

            var permRepo = new PermissionRepository(db.ConnectionString);
            var user = await permRepo.GetUserByUsernameAsync(username);
            if (user == null) return Results.Unauthorized();

            // Verify current password
            if (!PasswordHasher.Verify(req.CurrentPassword ?? "", user.Salt, user.PasswordHash)
                && !PasswordHasher.VerifyLegacySha256(req.CurrentPassword ?? "", user.Salt, user.PasswordHash))
            {
                return Results.Json(new { error = "Current password is incorrect" }, statusCode: 401);
            }

            // Validate new password against policy
            var policy = PasswordPolicy.Default;
            var validation = policy.Validate(req.NewPassword ?? "");
            if (!validation.IsValid)
                return Results.BadRequest(new { error = validation.ErrorSummary, errors = validation.Errors });

            // Check min age (prevent rapid password cycling)
            if (policy.IsTooRecent(user.PasswordChangedAt))
                return Results.BadRequest(new { error = "Password was changed too recently. Please wait before changing again." });

            // Hash and save
            var salt = PasswordHasher.GenerateSalt();
            var hash = PasswordHasher.Hash(req.NewPassword!, salt);
            var dbRepo = new DbRepository(db.ConnectionString);
            await dbRepo.UpdatePasswordHashAsync(user.Id, hash, salt);

            await dbRepo.LogAuthEventAsync("password_changed", username, true, "local", userId: user.Id);
            return Results.Ok(new { password_changed = true });
        })
        .RequireAuthorization();

        // ── POST /api/auth/logout ──
        group.MapPost("/logout", async (ClaimsPrincipal principal, DbConnectionFactory db) =>
        {
            var username = principal.Identity?.Name ?? "anonymous";
            var dbRepo = new DbRepository(db.ConnectionString);
            await dbRepo.LogAuthEventAsync("logout", username, true, "local");
            return Results.Ok(new { logged_out = true });
        })
        .RequireAuthorization();

        // ── GET /api/auth/sessions ── (list active sessions for current user)
        group.MapGet("/sessions", async (ClaimsPrincipal principal, DbConnectionFactory db) =>
        {
            var username = principal.Identity?.Name;
            if (string.IsNullOrEmpty(username)) return Results.Unauthorized();

            var permRepo = new PermissionRepository(db.ConnectionString);
            var user = await permRepo.GetUserByUsernameAsync(username);
            if (user == null) return Results.Unauthorized();

            var dbRepo = new DbRepository(db.ConnectionString);
            var sessions = await dbRepo.GetUserSessionsAsync(user.Id);
            return Results.Ok(sessions);
        })
        .RequireAuthorization();

        // ── DELETE /api/auth/sessions/{id} ── (revoke a specific session)
        group.MapDelete("/sessions/{id:int}", async (int id, ClaimsPrincipal principal, DbConnectionFactory db) =>
        {
            var username = principal.Identity?.Name;
            if (string.IsNullOrEmpty(username)) return Results.Unauthorized();

            var dbRepo = new DbRepository(db.ConnectionString);
            await dbRepo.ForceEndSessionAsync(id);
            await dbRepo.LogAuthEventAsync("session_revoked", username, true, "local");
            return Results.Ok(new { session_revoked = true });
        })
        .RequireAuthorization();

        // ── GET /api/auth/me ──
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

    // ── Helpers ──

    private static async Task<IResult> IssueTokenAsync(
        AuthUser user, TokenService tokens, PermissionRepository permRepo, DbRepository dbRepo, DbConnectionFactory db)
    {
        // Update last login
        try
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var cmd = new Npgsql.NpgsqlCommand(
                "UPDATE app_users SET last_login_at=NOW(), login_count=COALESCE(login_count,0)+1 WHERE username=@u", conn);
            cmd.Parameters.AddWithValue("u", user.Username);
            await cmd.ExecuteNonQueryAsync();
        }
        catch { }

        var permissions = await permRepo.GetPermissionCodesForRoleAsync(user.RoleName);

        // Resolve tenant (same logic as before, condensed)
        Guid? tenantId = null;
        string? tenantSlug = "default";
        string? tenantTier = "enterprise";
        bool isGlobalAdmin = false;

        try
        {
            await using var conn = await db.OpenConnectionAsync();
            await using var tenantCmd = new Npgsql.NpgsqlCommand(
                @"SELECT t.id, t.slug, t.tier FROM central_platform.tenant_memberships m
                  JOIN central_platform.global_users g ON g.id = m.user_id
                  JOIN central_platform.tenants t ON t.id = m.tenant_id
                  WHERE LOWER(g.email) = LOWER(@u) OR LOWER(g.display_name) = LOWER(@u)
                  ORDER BY m.joined_at LIMIT 1", conn);
            tenantCmd.Parameters.AddWithValue("u", user.Username);
            await using var tr = await tenantCmd.ExecuteReaderAsync();
            if (await tr.ReadAsync())
            {
                tenantId = tr.GetGuid(0);
                tenantSlug = tr.GetString(1);
                tenantTier = tr.GetString(2);
            }
        }
        catch { }

        try
        {
            await using var gaConn = await db.OpenConnectionAsync();
            await using var gaCmd = new Npgsql.NpgsqlCommand(
                "SELECT is_global_admin FROM central_platform.global_users WHERE LOWER(email) = LOWER(@u) OR LOWER(display_name) = LOWER(@u)", gaConn);
            gaCmd.Parameters.AddWithValue("u", user.Username);
            var gaResult = await gaCmd.ExecuteScalarAsync();
            if (gaResult is bool ga) isGlobalAdmin = ga;
        }
        catch { }

        var token = tokens.GenerateToken(user.Username, user.RoleName, permissions,
            tenantId, tenantSlug, tenantTier, isGlobalAdmin);

        await dbRepo.LogAuthEventAsync("login", user.Username, true, "local", userId: user.Id);

        return Results.Ok(new
        {
            token,
            username = user.Username,
            role = user.RoleName,
            permissions = permissions.ToArray(),
            is_global_admin = isGlobalAdmin,
            mfa_enabled = user.MfaEnabled,
            tenant = new { id = tenantId, slug = tenantSlug, tier = tenantTier }
        });
    }

    private static async Task<string> StorePendingMfaSetupAsync(DbRepository repo, int userId, string encSecret, List<string> codes)
    {
        // Return a simple token for the confirm step
        return Guid.NewGuid().ToString("N");
    }

    // ── Records ──

    private record LoginRequest(string Username, string? Password = null, string? TenantSlug = null);
    private record MfaVerifyRequest(string MfaToken, string Code);
    private record MfaConfirmRequest(string Secret, string Code, List<string>? RecoveryCodes = null);
    private record ChangePasswordRequest(string? CurrentPassword, string? NewPassword);
    private record MfaPendingSession(AuthUser User, DateTime ExpiresAt);
    private record TenantInfo(Guid Id, string Slug, string Tier, string DisplayName);
}
