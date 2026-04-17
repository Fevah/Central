namespace Central.Engine.Auth;

/// <summary>
/// Orchestrates authentication across all configured providers.
/// Handles IdP discovery, claims mapping, JIT provisioning, and session management.
/// </summary>
public interface IAuthenticationService
{
    /// <summary>Get all enabled providers ordered by priority.</summary>
    Task<List<IdentityProviderConfig>> GetAvailableProvidersAsync();

    /// <summary>Discover the appropriate provider for an email address.</summary>
    Task<IdentityProviderConfig?> DiscoverProviderAsync(string email);

    /// <summary>Authenticate using a specific provider.</summary>
    Task<AuthenticationResult> AuthenticateAsync(int providerId, AuthenticationRequest request, CancellationToken ct = default);

    /// <summary>Authenticate using the legacy Windows auto-login flow.</summary>
    Task<AuthenticationResult> AuthenticateWindowsAsync();

    /// <summary>Authenticate using legacy local password.</summary>
    Task<AuthenticationResult> AuthenticateLocalAsync(string username, string password);

    /// <summary>
    /// Process a successful AuthenticationResult: map claims to role, JIT provision user,
    /// load permissions, and call AuthContext.SetSession().
    /// </summary>
    Task<bool> EstablishSessionAsync(AuthenticationResult result);

    /// <summary>Refresh the current session silently.</summary>
    Task<bool> TryRefreshSessionAsync();

    /// <summary>Full logout with session revocation.</summary>
    Task LogoutAsync();
}
