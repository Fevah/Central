using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http;
using System.Text.Json;
using Central.Core.Auth;

namespace Central.Desktop.Auth.Providers;

/// <summary>
/// Okta OIDC authentication via Authorization Code + PKCE flow.
/// Uses system browser + localhost redirect. Okta handles MFA inline.
/// </summary>
public class OktaAuthProvider : IAuthenticationProvider
{
    private string _oktaDomain = "";
    private string _clientId = "";
    private string _authServerId = "default";
    private string _scopes = "openid profile email groups";
    private static readonly HttpClient Http = new();

    public string ProviderType => "okta";
    public string DisplayName => "Okta";
    public bool SupportsRefresh => true;
    public bool RequiresMfa => false;  // Okta handles MFA within the auth flow

    public Task InitializeAsync(IdentityProviderConfig config)
    {
        var cfg = ParseConfig(config.ConfigJson);
        _oktaDomain = cfg.GetValueOrDefault("okta_domain", "").TrimEnd('/');
        _clientId = cfg.GetValueOrDefault("client_id", "");
        _authServerId = cfg.GetValueOrDefault("authorization_server_id", "default");
        _scopes = cfg.GetValueOrDefault("scopes", "openid profile email groups");
        return Task.CompletedTask;
    }

    public async Task<AuthenticationResult> AuthenticateAsync(AuthenticationRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(_clientId) || string.IsNullOrEmpty(_oktaDomain))
            return AuthenticationResult.Fail("Okta not configured (missing okta_domain or client_id)");

        using var listener = new OAuthCallbackListener("/auth/okta");
        var redirectUri = listener.RedirectUri;
        var state = Guid.NewGuid().ToString("N");
        var codeVerifier = GenerateCodeVerifier();
        var codeChallenge = GenerateCodeChallenge(codeVerifier);
        var baseUrl = $"https://{_oktaDomain}/oauth2/{_authServerId}";

        var authUrl = $"{baseUrl}/v1/authorize" +
            $"?client_id={Uri.EscapeDataString(_clientId)}" +
            $"&response_type=code" +
            $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
            $"&scope={Uri.EscapeDataString(_scopes)}" +
            $"&state={state}" +
            $"&code_challenge={codeChallenge}" +
            $"&code_challenge_method=S256";

        if (!string.IsNullOrEmpty(request.Email))
            authUrl += $"&login_hint={Uri.EscapeDataString(request.Email)}";

        Process.Start(new ProcessStartInfo(authUrl) { UseShellExecute = true });

        var callbackParams = await listener.WaitForCallbackAsync(TimeSpan.FromMinutes(5), ct);

        if (callbackParams.TryGetValue("error", out var error))
            return AuthenticationResult.Fail($"Okta error: {error} - {callbackParams.GetValueOrDefault("error_description", "")}");

        if (!callbackParams.TryGetValue("code", out var code))
            return AuthenticationResult.Fail("No authorization code received");

        if (callbackParams.GetValueOrDefault("state", "") != state)
            return AuthenticationResult.Fail("State mismatch");

        // Exchange code for tokens
        var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = _clientId,
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = redirectUri,
            ["code_verifier"] = codeVerifier
        });

        var tokenUrl = $"{baseUrl}/v1/token";
        var response = await Http.PostAsync(tokenUrl, content, ct);
        if (!response.IsSuccessStatusCode)
            return AuthenticationResult.Fail($"Okta token exchange failed: {response.StatusCode}");

        var json = await response.Content.ReadAsStringAsync(ct);
        var doc = JsonDocument.Parse(json);
        var accessToken = doc.RootElement.GetProperty("access_token").GetString() ?? "";
        var idToken = doc.RootElement.TryGetProperty("id_token", out var id) ? id.GetString() ?? "" : "";
        var refreshToken = doc.RootElement.TryGetProperty("refresh_token", out var rt) ? rt.GetString() ?? "" : "";
        var expiresIn = doc.RootElement.TryGetProperty("expires_in", out var exp) ? exp.GetInt32() : 3600;

        var claims = ParseIdTokenClaims(idToken);
        var sub = claims.GetValueOrDefault("sub", new()).FirstOrDefault() ?? "";
        var email = claims.GetValueOrDefault("email", new()).FirstOrDefault() ?? "";
        var name = claims.GetValueOrDefault("name", new()).FirstOrDefault() ?? "";

        return new AuthenticationResult
        {
            Success = true,
            Username = !string.IsNullOrEmpty(email) && email.Contains('@') ? email.Split('@')[0] : sub,
            DisplayName = name,
            Email = email,
            ExternalId = sub,
            Claims = claims,
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            TokenExpiry = DateTime.UtcNow.AddSeconds(expiresIn),
            ProviderType = "okta"
        };
    }

    public async Task<AuthenticationResult?> TryRefreshAsync(string refreshToken, CancellationToken ct = default)
    {
        try
        {
            var baseUrl = $"https://{_oktaDomain}/oauth2/{_authServerId}";
            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = _clientId,
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = refreshToken,
                ["scope"] = _scopes
            });

            var response = await Http.PostAsync($"{baseUrl}/v1/token", content, ct);
            if (!response.IsSuccessStatusCode) return null;

            var json = await response.Content.ReadAsStringAsync(ct);
            var doc = JsonDocument.Parse(json);
            var claims = ParseIdTokenClaims(doc.RootElement.TryGetProperty("id_token", out var id) ? id.GetString() : null);

            return new AuthenticationResult
            {
                Success = true,
                AccessToken = doc.RootElement.GetProperty("access_token").GetString() ?? "",
                RefreshToken = doc.RootElement.TryGetProperty("refresh_token", out var rt) ? rt.GetString() : refreshToken,
                TokenExpiry = DateTime.UtcNow.AddSeconds(doc.RootElement.TryGetProperty("expires_in", out var exp) ? exp.GetInt32() : 3600),
                ProviderType = "okta",
                Claims = claims
            };
        }
        catch { return null; }
    }

    public Task LogoutAsync(string? accessToken = null) => Task.CompletedTask;

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
                if (!claims.ContainsKey(claim.Type)) claims[claim.Type] = new();
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
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static string GenerateCodeChallenge(string verifier)
    {
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.ASCII.GetBytes(verifier));
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static Dictionary<string, string> ParseConfig(string json)
    {
        try
        {
            var doc = JsonDocument.Parse(json);
            var result = new Dictionary<string, string>();
            foreach (var prop in doc.RootElement.EnumerateObject()) result[prop.Name] = prop.Value.ToString();
            return result;
        }
        catch { return new(); }
    }
}
