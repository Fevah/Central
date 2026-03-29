using Central.Core.Auth;
using Central.Data;
using Central.Data.Repositories;

namespace Central.Desktop.Auth;

/// <summary>
/// Orchestrates authentication across all configured providers.
/// Handles IdP discovery, claims mapping, JIT provisioning, session establishment.
/// </summary>
public class AuthenticationService : IAuthenticationService
{
    private readonly string _dsn;
    private readonly DbRepository _repo;
    private readonly PermissionRepository _permRepo;
    private readonly IClaimsMappingService _claimsMapper;
    private readonly IUserProvisioningService _provisioner;
    private readonly IAuthEventLogger _logger;
    private readonly Dictionary<string, IAuthenticationProvider> _providers = new();

    public AuthenticationService(string dsn)
    {
        _dsn = dsn;
        _repo = new DbRepository(dsn);
        _permRepo = new PermissionRepository(dsn);
        _claimsMapper = new ClaimsMappingService(_repo);
        _provisioner = new UserProvisioningService(dsn);
        _logger = new AuthEventLogger(_repo);

        // Register all providers — auth-service (Rust) is primary, local DB is fallback
        var authServiceUrl = Environment.GetEnvironmentVariable("AUTH_SERVICE_URL") ?? "http://localhost:8081";
        RegisterProvider(new Providers.RustAuthServiceProvider(authServiceUrl));
        RegisterProvider(new Providers.WindowsAuthProvider(dsn));
        RegisterProvider(new Providers.LocalPasswordAuthProvider(dsn));
        RegisterProvider(new Providers.EntraIdAuthProvider());
        RegisterProvider(new Providers.OktaAuthProvider());
        RegisterProvider(new Providers.Saml2AuthProvider());
    }

    public void RegisterProvider(IAuthenticationProvider provider)
    {
        _providers[provider.ProviderType] = provider;
    }

    public async Task<List<IdentityProviderConfig>> GetAvailableProvidersAsync()
    {
        return await _repo.GetIdentityProvidersAsync(enabledOnly: true);
    }

    public async Task<IdentityProviderConfig?> DiscoverProviderAsync(string email)
    {
        if (string.IsNullOrEmpty(email) || !email.Contains('@')) return null;
        var domain = email.Split('@')[1].ToLowerInvariant();
        return await _repo.GetProviderByDomainAsync(domain);
    }

    public async Task<AuthenticationResult> AuthenticateAsync(int providerId, AuthenticationRequest request, CancellationToken ct = default)
    {
        var providers = await _repo.GetIdentityProvidersAsync(enabledOnly: true);
        var config = providers.FirstOrDefault(p => p.Id == providerId);
        if (config == null)
            return AuthenticationResult.Fail("Provider not found or disabled");

        if (!_providers.TryGetValue(config.ProviderType, out var provider))
            return AuthenticationResult.Fail($"No implementation for provider type: {config.ProviderType}");

        await provider.InitializeAsync(config);
        var result = await provider.AuthenticateAsync(request, ct);
        // Stamp provider info onto the result
        if (result.Success)
        {
            return new AuthenticationResult
            {
                Success = true, Username = result.Username, DisplayName = result.DisplayName,
                Email = result.Email, ExternalId = result.ExternalId, Claims = result.Claims,
                AccessToken = result.AccessToken, RefreshToken = result.RefreshToken,
                TokenExpiry = result.TokenExpiry,
                ProviderId = providerId, ProviderType = config.ProviderType
            };
        }

        await _logger.LogAsync(result.Success ? "login" : "failed",
            result.Username ?? request.Username, result.Success,
            config.ProviderType, errorMessage: result.ErrorMessage);

        return result;
    }

    public async Task<AuthenticationResult> AuthenticateWindowsAsync()
    {
        var provider = _providers["windows"];
        var result = await provider.AuthenticateAsync(new AuthenticationRequest { Username = Environment.UserName });

        await _logger.LogAsync(result.Success ? "login" : "failed",
            Environment.UserName, result.Success, "windows",
            errorMessage: result.ErrorMessage);

        return result;
    }

    public async Task<AuthenticationResult> AuthenticateLocalAsync(string username, string password)
    {
        // Try auth-service (Rust) first — enterprise auth with Argon2id, MFA, etc.
        if (_providers.TryGetValue("auth-service", out var rustAuth))
        {
            try
            {
                var rustResult = await rustAuth.AuthenticateAsync(new AuthenticationRequest
                {
                    Username = username,
                    Email = username.Contains('@') ? username : null,
                    Password = password
                });

                if (rustResult.Success || rustResult.RequiresMfa)
                    return rustResult;

                // Only fall through if auth-service is unreachable, not if credentials are wrong
                if (rustResult.ErrorMessage?.Contains("unreachable") != true &&
                    rustResult.ErrorMessage?.Contains("timeout") != true)
                    return rustResult;
            }
            catch { /* auth-service unavailable — fall through to local */ }
        }

        // Fallback: local DB auth
        var provider = _providers["local"];
        var providers = await _repo.GetIdentityProvidersAsync(enabledOnly: true);
        var localConfig = providers.FirstOrDefault(p => p.ProviderType == "local");
        if (localConfig != null) await provider.InitializeAsync(localConfig);

        var result = await provider.AuthenticateAsync(new AuthenticationRequest
        {
            Username = username,
            Password = password
        });

        return result;
    }

    public async Task<bool> EstablishSessionAsync(AuthenticationResult result)
    {
        if (!result.Success) return false;

        // Map claims to role
        var role = result.Claims.TryGetValue("role", out var roles) && roles.Count > 0
            ? roles[0]
            : result.ProviderId.HasValue
                ? await _claimsMapper.MapClaimsToRoleAsync(result.ProviderId.Value, result.Claims)
                : "Viewer";

        // Find or create user
        var authUser = await _provisioner.FindOrProvisionUserAsync(result, role);
        if (authUser == null) return false;

        // Load permissions and sites
        var permCodes = await _permRepo.GetPermissionCodesForRoleAsync(authUser.RoleName);
        var allowedSites = await _permRepo.GetAllowedSitesAsync(authUser.RoleName);

        // Determine auth state from provider type
        var authState = result.ProviderType switch
        {
            "windows" => AuthStates.Windows,
            "local" => AuthStates.Password,
            "entra_id" => AuthStates.EntraId,
            "okta" => AuthStates.Okta,
            "saml2" => AuthStates.Saml,
            _ => AuthStates.Local
        };

        AuthContext.Instance.SetSession(authUser, permCodes, allowedSites, authState);

        await _logger.LogAsync("session", authUser.Username, true, result.ProviderType, authUser.Id);

        // Track active session
        try
        {
            var sessionToken = Guid.NewGuid().ToString("N");
            await _repo.CreateSessionAsync(authUser.Id, sessionToken, result.ProviderType ?? "windows");
            _currentSessionToken = sessionToken;
        }
        catch { /* non-critical */ }

        return true;
    }

    private string? _currentSessionToken;

    public async Task<bool> TryRefreshSessionAsync()
    {
        // For now, re-validate the current session
        var user = AuthContext.Instance.CurrentUser;
        if (user == null) return false;

        var dbUser = await _permRepo.GetUserByUsernameAsync(user.Username);
        if (dbUser == null || !dbUser.IsActive) return false;

        var permCodes = await _permRepo.GetPermissionCodesForRoleAsync(dbUser.RoleName);
        var allowedSites = await _permRepo.GetAllowedSitesAsync(dbUser.RoleName);
        AuthContext.Instance.SetSession(dbUser, permCodes, allowedSites, AuthContext.Instance.AuthState);
        return true;
    }

    public async Task LogoutAsync()
    {
        var user = AuthContext.Instance.CurrentUser;
        if (user != null)
        {
            // Log the active provider if possible
            var providerType = AuthContext.Instance.AuthState switch
            {
                AuthStates.Windows => "windows",
                AuthStates.Password => "local",
                AuthStates.EntraId => "entra_id",
                AuthStates.Okta => "okta",
                AuthStates.Saml => "saml2",
                _ => "unknown"
            };

            if (_providers.TryGetValue(providerType, out var provider))
                await provider.LogoutAsync();

            await _logger.LogAsync("logout", user.Username, true, providerType, user.Id);

            // End active session
            if (!string.IsNullOrEmpty(_currentSessionToken))
            {
                try { await _repo.EndSessionAsync(_currentSessionToken); } catch { }
                _currentSessionToken = null;
            }
        }

        AuthContext.Instance.Logout();
    }
}
