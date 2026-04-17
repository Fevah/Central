using Central.Core.Auth;
using Central.Data.Repositories;

namespace Central.Desktop.Auth.Providers;

/// <summary>
/// Windows auto-login provider. Wraps the existing Environment.UserName + DB lookup flow.
/// Non-interactive — either succeeds silently or fails.
/// </summary>
public class WindowsAuthProvider : IAuthenticationProvider
{
    private readonly string _dsn;

    public WindowsAuthProvider(string dsn) => _dsn = dsn;

    public string ProviderType => "windows";
    public string DisplayName => "Windows Authentication";
    public bool SupportsRefresh => false;
    public bool RequiresMfa => false;

    public Task InitializeAsync(IdentityProviderConfig config) => Task.CompletedTask;

    public async Task<AuthenticationResult> AuthenticateAsync(AuthenticationRequest request, CancellationToken ct = default)
    {
        var username = request.Username ?? Environment.UserName;
        var repo = new PermissionRepository(_dsn);
        var user = await repo.GetUserByUsernameAsync(username);

        if (user == null)
            return AuthenticationResult.Fail($"No account found for Windows user: {username}");
        if (!user.IsActive)
            return AuthenticationResult.Fail($"Account disabled: {username}");

        var permCodes = await repo.GetPermissionCodesForRoleAsync(user.RoleName);
        var claims = new Dictionary<string, List<string>>
        {
            ["role"] = [user.RoleName],
            ["permissions"] = permCodes.ToList()
        };

        return new AuthenticationResult
        {
            Success = true,
            Username = user.Username,
            DisplayName = user.DisplayName,
            Email = user.Email,
            ProviderType = "windows",
            Claims = claims
        };
    }

    public Task<AuthenticationResult?> TryRefreshAsync(string refreshToken, CancellationToken ct = default)
        => Task.FromResult<AuthenticationResult?>(null);

    public Task LogoutAsync(string? accessToken = null) => Task.CompletedTask;
}
