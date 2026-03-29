using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Central.Api.Client;

/// <summary>
/// Client for the Rust auth-service. Handles login, MFA, token refresh.
/// Used by both WPF desktop and API server.
/// </summary>
public class AuthServiceClient
{
    private readonly HttpClient _http;
    private readonly string _baseUrl;
    private readonly string _tenantId;

    public AuthServiceClient(string baseUrl = "http://localhost:8081", string tenantId = "00000000-0000-0000-0000-000000000001")
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _tenantId = tenantId;
        _http = new HttpClient { BaseAddress = new Uri(_baseUrl) };
        _http.DefaultRequestHeaders.Add("X-Tenant-ID", _tenantId);
    }

    /// <summary>Login with email + password. Returns tokens or MFA challenge.</summary>
    public async Task<AuthResult> LoginAsync(string email, string password)
    {
        var response = await _http.PostAsJsonAsync("/api/v1/auth/login", new { email, password });
        var json = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            var error = JsonSerializer.Deserialize<AuthError>(json, JsonOpts);
            return new AuthResult { Success = false, Error = error?.Error ?? "Login failed", ErrorCode = error?.Code ?? "" };
        }

        var result = JsonSerializer.Deserialize<LoginResponse>(json, JsonOpts);
        if (result == null) return new AuthResult { Success = false, Error = "Invalid response" };

        return new AuthResult
        {
            Success = true,
            AccessToken = result.AccessToken,
            RefreshToken = result.RefreshToken,
            SessionId = result.SessionId,
            ExpiresIn = result.ExpiresIn,
            MfaRequired = result.MfaRequired,
            MfaMethods = result.MfaMethods ?? [],
            User = result.User
        };
    }

    /// <summary>Verify MFA code (TOTP, WebAuthn, etc.).</summary>
    public async Task<AuthResult> VerifyMfaAsync(string sessionId, string code, string method = "totp")
    {
        var response = await _http.PostAsJsonAsync("/api/v1/auth/mfa/verify", new { session_id = sessionId, code, method });
        var json = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            var error = JsonSerializer.Deserialize<AuthError>(json, JsonOpts);
            return new AuthResult { Success = false, Error = error?.Error ?? "MFA verification failed" };
        }

        var result = JsonSerializer.Deserialize<LoginResponse>(json, JsonOpts);
        return new AuthResult
        {
            Success = true,
            AccessToken = result?.AccessToken,
            RefreshToken = result?.RefreshToken,
            SessionId = result?.SessionId,
            ExpiresIn = result?.ExpiresIn ?? 900
        };
    }

    /// <summary>Refresh access token using refresh token.</summary>
    public async Task<AuthResult> RefreshTokenAsync(string refreshToken)
    {
        var response = await _http.PostAsJsonAsync("/api/v1/auth/refresh", new { refresh_token = refreshToken });
        var json = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            return new AuthResult { Success = false, Error = "Token refresh failed" };

        var result = JsonSerializer.Deserialize<LoginResponse>(json, JsonOpts);
        return new AuthResult
        {
            Success = true,
            AccessToken = result?.AccessToken,
            RefreshToken = result?.RefreshToken,
            ExpiresIn = result?.ExpiresIn ?? 900
        };
    }

    /// <summary>Logout (revoke session).</summary>
    public async Task LogoutAsync(string accessToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/logout");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        await _http.SendAsync(request);
    }

    /// <summary>Check auth-service health.</summary>
    public async Task<bool> IsHealthyAsync()
    {
        try
        {
            var response = await _http.GetAsync("/health");
            return response.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}

public class AuthResult
{
    public bool Success { get; set; }
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public string? SessionId { get; set; }
    public int ExpiresIn { get; set; }
    public bool MfaRequired { get; set; }
    public string[] MfaMethods { get; set; } = [];
    public AuthUser? User { get; set; }
    public string? Error { get; set; }
    public string? ErrorCode { get; set; }
}

public class AuthUser
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("email")] public string Email { get; set; } = "";
    [JsonPropertyName("display_name")] public string DisplayName { get; set; } = "";
    [JsonPropertyName("first_name")] public string? FirstName { get; set; }
    [JsonPropertyName("last_name")] public string? LastName { get; set; }
    [JsonPropertyName("roles")] public string[] Roles { get; set; } = [];
    [JsonPropertyName("permissions")] public string[] Permissions { get; set; } = [];
    [JsonPropertyName("mfa_enabled")] public bool MfaEnabled { get; set; }
}

internal class LoginResponse
{
    [JsonPropertyName("access_token")] public string? AccessToken { get; set; }
    [JsonPropertyName("refresh_token")] public string? RefreshToken { get; set; }
    [JsonPropertyName("session_id")] public string? SessionId { get; set; }
    [JsonPropertyName("expires_in")] public int ExpiresIn { get; set; }
    [JsonPropertyName("token_type")] public string? TokenType { get; set; }
    [JsonPropertyName("mfa_required")] public bool MfaRequired { get; set; }
    [JsonPropertyName("mfa_methods")] public string[]? MfaMethods { get; set; }
    [JsonPropertyName("user")] public AuthUser? User { get; set; }
}

internal class AuthError
{
    [JsonPropertyName("error")] public string? Error { get; set; }
    [JsonPropertyName("code")] public string? Code { get; set; }
}
