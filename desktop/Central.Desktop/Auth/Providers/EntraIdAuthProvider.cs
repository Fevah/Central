using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http;
using System.Text.Json;
using Central.Core.Auth;

namespace Central.Desktop.Auth.Providers;

/// <summary>
/// Microsoft Entra ID (Azure AD) authentication via OIDC Authorization Code flow.
/// Uses system browser + localhost redirect (RFC 8252 for native apps).
/// Can optionally use MSAL when Microsoft.Identity.Client is available.
/// </summary>
public class EntraIdAuthProvider : IAuthenticationProvider
{
    private string _tenantId = "";
    private string _clientId = "";
    private string _authority = "";
    private string _scopes = "openid profile email";
    private static readonly HttpClient Http = new();

    public string ProviderType => "entra_id";
    public string DisplayName => "Microsoft Entra ID";
    public bool SupportsRefresh => true;
    public bool RequiresMfa => false;

    public Task InitializeAsync(IdentityProviderConfig config)
    {
        var cfg = ParseConfig(config.ConfigJson);
        _tenantId = cfg.GetValueOrDefault("tenant_id", "");
        _clientId = cfg.GetValueOrDefault("client_id", "");
        _authority = cfg.GetValueOrDefault("authority", $"https://login.microsoftonline.com/{_tenantId}");
        _scopes = cfg.GetValueOrDefault("scopes", "openid profile email");
        return Task.CompletedTask;
    }

    public async Task<AuthenticationResult> AuthenticateAsync(AuthenticationRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_clientId) || string.IsNullOrEmpty(_tenantId))
            return AuthenticationResult.Fail("Entra ID not configured (missing tenant_id or client_id)");

        using var listener = new OAuthCallbackListener("/auth/entra");
        var redirectUri = listener.RedirectUri;
        var state = Guid.NewGuid().ToString("N");
        var codeVerifier = GenerateCodeVerifier();
        var codeChallenge = GenerateCodeChallenge(codeVerifier);

        // Build authorization URL
        var authUrl = $"{_authority}/oauth2/v2.0/authorize" +
            $"?client_id={Uri.EscapeDataString(_clientId)}" +
            $"&response_type=code" +
            $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
            $"&scope={Uri.EscapeDataString(_scopes)}" +
            $"&state={state}" +
            $"&code_challenge={codeChallenge}" +
            $"&code_challenge_method=S256" +
            $"&response_mode=query";

        if (!string.IsNullOrEmpty(request.Email))
            authUrl += $"&login_hint={Uri.EscapeDataString(request.Email)}";

        // Open system browser
        Process.Start(new ProcessStartInfo(authUrl) { UseShellExecute = true });

        // Wait for callback
        var callbackParams = await listener.WaitForCallbackAsync(TimeSpan.FromMinutes(5), ct);

        if (callbackParams.TryGetValue("error", out var error))
            return AuthenticationResult.Fail($"Entra ID error: {error} - {callbackParams.GetValueOrDefault("error_description", "")}");

        if (!callbackParams.TryGetValue("code", out var code))
            return AuthenticationResult.Fail("No authorization code received");

        if (callbackParams.GetValueOrDefault("state", "") != state)
            return AuthenticationResult.Fail("State mismatch — possible CSRF attack");

        // Exchange code for tokens
        var tokenResponse = await ExchangeCodeAsync(code, redirectUri, codeVerifier, ct);
        if (tokenResponse == null)
            return AuthenticationResult.Fail("Token exchange failed");

        // Parse ID token claims
        var claims = ParseIdTokenClaims(tokenResponse.IdToken);
        var username = claims.GetValueOrDefault("preferred_username", new()).FirstOrDefault() ?? "";
        var displayName = claims.GetValueOrDefault("name", new()).FirstOrDefault() ?? username;
        var email = claims.GetValueOrDefault("email", new()).FirstOrDefault() ??
                    claims.GetValueOrDefault("preferred_username", new()).FirstOrDefault() ?? "";
        var sub = claims.GetValueOrDefault("sub", new()).FirstOrDefault() ?? "";

        return new AuthenticationResult
        {
            Success = true,
            Username = username.Contains('@') ? username.Split('@')[0] : username,
            DisplayName = displayName,
            Email = email,
            ExternalId = sub,
            Claims = claims,
            AccessToken = tokenResponse.AccessToken,
            RefreshToken = tokenResponse.RefreshToken,
            TokenExpiry = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn),
            ProviderType = "entra_id"
        };
    }

    public async Task<AuthenticationResult?> TryRefreshAsync(string refreshToken, CancellationToken ct = default)
    {
        try
        {
            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = _clientId,
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = refreshToken,
                ["scope"] = _scopes
            });

            var response = await Http.PostAsync($"{_authority}/oauth2/v2.0/token", content, ct);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync(ct);
            var tokenResponse = ParseTokenResponse(json);
            if (tokenResponse == null) return null;

            var claims = ParseIdTokenClaims(tokenResponse.IdToken);
            return new AuthenticationResult
            {
                Success = true,
                Username = claims.GetValueOrDefault("preferred_username", new()).FirstOrDefault() ?? "",
                AccessToken = tokenResponse.AccessToken,
                RefreshToken = tokenResponse.RefreshToken,
                TokenExpiry = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn),
                ProviderType = "entra_id",
                Claims = claims
            };
        }
        catch { return null; }
    }

    public Task LogoutAsync(string? accessToken = null)
    {
        // Open Entra logout URL in browser
        var logoutUrl = $"{_authority}/oauth2/v2.0/logout?post_logout_redirect_uri={Uri.EscapeDataString("http://localhost")}";
        try { Process.Start(new ProcessStartInfo(logoutUrl) { UseShellExecute = true }); } catch { }
        return Task.CompletedTask;
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private async Task<TokenResponse?> ExchangeCodeAsync(string code, string redirectUri, string codeVerifier, CancellationToken ct)
    {
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = _clientId,
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = redirectUri,
            ["code_verifier"] = codeVerifier
        });

        var response = await Http.PostAsync($"{_authority}/oauth2/v2.0/token", content, ct);
        if (!response.IsSuccessStatusCode) return null;

        var json = await response.Content.ReadAsStringAsync(ct);
        return ParseTokenResponse(json);
    }

    private static TokenResponse? ParseTokenResponse(string json)
    {
        try
        {
            var doc = JsonDocument.Parse(json);
            return new TokenResponse
            {
                AccessToken = doc.RootElement.GetProperty("access_token").GetString() ?? "",
                IdToken = doc.RootElement.TryGetProperty("id_token", out var id) ? id.GetString() ?? "" : "",
                RefreshToken = doc.RootElement.TryGetProperty("refresh_token", out var rt) ? rt.GetString() ?? "" : "",
                ExpiresIn = doc.RootElement.TryGetProperty("expires_in", out var exp) ? exp.GetInt32() : 3600
            };
        }
        catch { return null; }
    }

    private static Dictionary<string, List<string>> ParseIdTokenClaims(string? idToken)
    {
        var claims = new Dictionary<string, List<string>>();
        if (string.IsNullOrEmpty(idToken)) return claims;

        try
        {
            var handler = new JwtSecurityTokenHandler();
            var jwt = handler.ReadJwtToken(idToken);
            foreach (var claim in jwt.Claims)
            {
                if (!claims.ContainsKey(claim.Type))
                    claims[claim.Type] = new List<string>();
                claims[claim.Type].Add(claim.Value);
            }
        }
        catch { }
        return claims;
    }

    private static string GenerateCodeVerifier()
    {
        var bytes = new byte[32];
        System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static string GenerateCodeChallenge(string verifier)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.ASCII.GetBytes(verifier));
        return Convert.ToBase64String(bytes)
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static Dictionary<string, string> ParseConfig(string json)
    {
        try
        {
            var doc = JsonDocument.Parse(json);
            var result = new Dictionary<string, string>();
            foreach (var prop in doc.RootElement.EnumerateObject())
                result[prop.Name] = prop.Value.ToString();
            return result;
        }
        catch { return new(); }
    }

    private class TokenResponse
    {
        public string AccessToken { get; set; } = "";
        public string IdToken { get; set; } = "";
        public string RefreshToken { get; set; } = "";
        public int ExpiresIn { get; set; }
    }
}
