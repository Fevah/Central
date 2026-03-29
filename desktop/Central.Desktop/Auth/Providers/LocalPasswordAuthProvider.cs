using Central.Core.Auth;
using Central.Data;
using Central.Data.Repositories;

namespace Central.Desktop.Auth.Providers;

/// <summary>
/// Local username/password provider with brute-force lockout.
/// Wraps existing PasswordHasher.Verify flow.
/// </summary>
public class LocalPasswordAuthProvider : IAuthenticationProvider
{
    private readonly string _dsn;
    private int _lockoutThreshold = 5;
    private int _lockoutDurationMinutes = 30;

    public LocalPasswordAuthProvider(string dsn) => _dsn = dsn;

    public string ProviderType => "local";
    public string DisplayName => "Local Authentication";
    public bool SupportsRefresh => false;
    public bool RequiresMfa => false;

    public Task InitializeAsync(IdentityProviderConfig config)
    {
        if (config.Config.TryGetValue("lockout_threshold", out var lt) && int.TryParse(lt, out var threshold))
            _lockoutThreshold = threshold;
        if (config.Config.TryGetValue("lockout_duration_minutes", out var ld) && int.TryParse(ld, out var dur))
            _lockoutDurationMinutes = dur;
        return Task.CompletedTask;
    }

    public async Task<AuthenticationResult> AuthenticateAsync(AuthenticationRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(request.Username) || string.IsNullOrEmpty(request.Password))
            return AuthenticationResult.Fail("Username and password are required");

        var repo = new PermissionRepository(_dsn);
        var dbRepo = new DbRepository(_dsn);
        var user = await repo.GetUserByUsernameAsync(request.Username);

        if (user == null)
            return AuthenticationResult.Fail("Invalid username or password");
        if (!user.IsActive)
            return AuthenticationResult.Fail("Account is disabled");

        // Check lockout
        var (failedCount, lockedUntil) = await dbRepo.GetLockoutStatusAsync(request.Username);
        if (lockedUntil.HasValue && lockedUntil.Value > DateTime.UtcNow)
            return AuthenticationResult.Fail($"Account locked until {lockedUntil.Value:HH:mm}. Too many failed attempts.");

        // Verify password
        var hash = PasswordHasher.Hash(request.Password, user.Salt);
        if (hash != user.PasswordHash)
        {
            await dbRepo.IncrementFailedLoginAsync(request.Username);
            if (failedCount + 1 >= _lockoutThreshold)
            {
                await dbRepo.LockUserAsync(request.Username, _lockoutDurationMinutes);
                Central.Core.Services.NotificationService.Instance?.NotifyEvent(
                    "auth_lockout", $"Account Locked: {request.Username}",
                    $"Locked after {failedCount + 1} failed attempts for {_lockoutDurationMinutes} minutes",
                    Central.Core.Services.NotificationType.Warning);
            }

            await dbRepo.LogAuthEventAsync("failed", request.Username, false, "local",
                errorMessage: $"Invalid password (attempt {failedCount + 1})");
            return AuthenticationResult.Fail("Invalid username or password");
        }

        // Success — reset lockout counter
        await dbRepo.ResetFailedLoginAsync(request.Username);

        var permCodes = await repo.GetPermissionCodesForRoleAsync(user.RoleName);
        return new AuthenticationResult
        {
            Success = true,
            Username = user.Username,
            DisplayName = user.DisplayName,
            Email = user.Email,
            ProviderType = "local",
            Claims = new Dictionary<string, List<string>>
            {
                ["role"] = [user.RoleName],
                ["permissions"] = permCodes.ToList()
            }
        };
    }

    public Task<AuthenticationResult?> TryRefreshAsync(string refreshToken, CancellationToken ct = default)
        => Task.FromResult<AuthenticationResult?>(null);

    public Task LogoutAsync(string? accessToken = null) => Task.CompletedTask;
}
