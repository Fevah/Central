namespace Central.Engine.Auth;

/// <summary>
/// Result of an authentication attempt from any provider.
/// Normalized regardless of protocol (SAML, OIDC, local password).
/// </summary>
public class AuthenticationResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public string? ExternalId { get; init; }
    public string? Email { get; init; }
    public string? DisplayName { get; init; }
    public string? Username { get; init; }
    public Dictionary<string, List<string>> Claims { get; init; } = new();
    public string? AccessToken { get; init; }
    public string? RefreshToken { get; init; }
    public DateTime? TokenExpiry { get; init; }
    public string? ProviderType { get; init; }
    public int? ProviderId { get; init; }

    /// <summary>True if MFA verification is required before login completes.</summary>
    public bool RequiresMfa { get; init; }
    /// <summary>Available MFA methods (totp, webauthn, sms, email, backup_codes).</summary>
    public string[]? MfaMethods { get; init; }
    /// <summary>Session ID for MFA continuation (pass back to verify endpoint).</summary>
    public string? SessionId { get; init; }

    public static AuthenticationResult Fail(string error) => new() { Success = false, ErrorMessage = error };
    public static AuthenticationResult Ok(string username, string? displayName = null, string? email = null) =>
        new() { Success = true, Username = username, DisplayName = displayName, Email = email };
}

/// <summary>Request object for authentication attempts.</summary>
public class AuthenticationRequest
{
    public string? Username { get; init; }
    public string? Password { get; init; }
    public string? Email { get; init; }
    public string? MfaCode { get; init; }
    public Dictionary<string, string>? ExtraParameters { get; init; }
}

/// <summary>
/// Abstraction for an authentication provider.
/// Implementations live in Central.Desktop (they need browser, DPAPI, etc.)
/// </summary>
public interface IAuthenticationProvider
{
    string ProviderType { get; }
    string DisplayName { get; }

    /// <summary>Configure the provider from DB-stored JSON config.</summary>
    Task InitializeAsync(IdentityProviderConfig config);

    /// <summary>
    /// Perform the full authentication flow.
    /// For OIDC/SAML: launches browser, waits for callback.
    /// For local: validates credentials directly.
    /// </summary>
    Task<AuthenticationResult> AuthenticateAsync(AuthenticationRequest request, CancellationToken ct = default);

    /// <summary>Attempt silent token refresh without user interaction.</summary>
    Task<AuthenticationResult?> TryRefreshAsync(string refreshToken, CancellationToken ct = default);

    /// <summary>Perform logout (revoke tokens, SAML SLO, etc.)</summary>
    Task LogoutAsync(string? accessToken = null);

    bool SupportsRefresh { get; }
    bool RequiresMfa { get; }
}
