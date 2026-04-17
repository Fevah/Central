using System.Net.Http;
using Central.Core.Auth;
using Central.Api.Client;

namespace Central.Desktop.Auth.Providers;

/// <summary>
/// Authentication provider that delegates to the Rust auth-service.
/// Primary auth path — falls back to local DB auth if auth-service is unreachable.
/// </summary>
public class RustAuthServiceProvider : IAuthenticationProvider
{
    private readonly AuthServiceClient _client;

    public string ProviderType => "auth-service";
    public string DisplayName => "Central Auth Service";
    public bool SupportsRefresh => true;
    public bool RequiresMfa => false; // MFA is per-user, not per-provider

    public RustAuthServiceProvider(string authServiceUrl = "http://192.168.56.10:30081")
    {
        _client = new AuthServiceClient(authServiceUrl);
    }

    public Task InitializeAsync(IdentityProviderConfig config)
    {
        return Task.CompletedTask;
    }

    public async Task<AuthenticationResult> AuthenticateAsync(AuthenticationRequest request, CancellationToken ct = default)
    {
        try
        {
            var email = request.Email ?? request.Username ?? "";
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(request.Password))
                return AuthenticationResult.Fail("Email and password required");

            if (!email.Contains('@'))
                email = $"{email}@central.local";

            var result = await _client.LoginAsync(email, request.Password!);

            if (!result.Success)
                return AuthenticationResult.Fail(result.Error ?? "Authentication failed");

            if (result.MfaRequired)
            {
                return new AuthenticationResult
                {
                    Success = false,
                    RequiresMfa = true,
                    MfaMethods = result.MfaMethods,
                    SessionId = result.SessionId,
                    ErrorMessage = "MFA required"
                };
            }

            return new AuthenticationResult
            {
                Success = true,
                Username = result.User?.Email ?? email,
                DisplayName = result.User?.DisplayName ?? "",
                Email = result.User?.Email ?? email,
                ExternalId = result.User?.Id,
                AccessToken = result.AccessToken,
                RefreshToken = result.RefreshToken,
                TokenExpiry = DateTime.UtcNow.AddSeconds(result.ExpiresIn),
                ProviderType = "auth-service",
                Claims = new Dictionary<string, List<string>>
                {
                    ["role"] = result.User?.Roles?.ToList() ?? ["member"],
                    ["permissions"] = result.User?.Permissions?.ToList() ?? []
                }
            };
        }
        catch (HttpRequestException)
        {
            return AuthenticationResult.Fail("Auth service unreachable — try local login");
        }
        catch (TaskCanceledException)
        {
            return AuthenticationResult.Fail("Auth service timeout — try local login");
        }
    }

    public async Task<AuthenticationResult?> TryRefreshAsync(string refreshToken, CancellationToken ct = default)
    {
        try
        {
            var result = await _client.RefreshTokenAsync(refreshToken);
            if (!result.Success) return null;
            return new AuthenticationResult
            {
                Success = true,
                AccessToken = result.AccessToken,
                RefreshToken = result.RefreshToken,
                TokenExpiry = DateTime.UtcNow.AddSeconds(result.ExpiresIn),
                ProviderType = "auth-service"
            };
        }
        catch { return null; }
    }

    public async Task LogoutAsync(string? accessToken = null)
    {
        if (!string.IsNullOrEmpty(accessToken))
            await _client.LogoutAsync(accessToken);
    }

    public async Task<bool> IsAvailableAsync() => await _client.IsHealthyAsync();
}
