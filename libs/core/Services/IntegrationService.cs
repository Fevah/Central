using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Central.Core.Services;

/// <summary>
/// Engine integration service — manages OAuth2 token lifecycle for external APIs.
/// Supports ManageEngine/Zoho, ServiceNow, and other OAuth2 integrations.
///
/// Flow: refresh_token → access_token (auto-refresh on expiry) → API calls with Bearer header
/// Credentials stored encrypted in DB via CredentialEncryptor.
/// </summary>
public class IntegrationService
{
    private readonly HttpClient _http;
    private string? _accessToken;
    private DateTime _tokenExpiry = DateTime.MinValue;

    public string IntegrationName { get; }
    public string OAuthUrl { get; set; } = "";
    public string BaseUrl { get; set; } = "";
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public string? RefreshToken { get; set; }

    public bool IsConfigured => !string.IsNullOrEmpty(ClientId) && !string.IsNullOrEmpty(RefreshToken);
    public bool HasValidToken => !string.IsNullOrEmpty(_accessToken) && DateTime.UtcNow < _tokenExpiry;

    public IntegrationService(string name)
    {
        IntegrationName = name;
        _http = new HttpClient();
    }

    /// <summary>Exchange authorization code for refresh + access tokens (first-time setup).</summary>
    public async Task<(string? RefreshToken, string? AccessToken, string? Error)> ExchangeAuthCodeAsync(string authCode)
    {
        try
        {
            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["code"] = authCode,
                ["client_id"] = ClientId ?? "",
                ["client_secret"] = ClientSecret ?? ""
            });

            var resp = await _http.PostAsync(OAuthUrl, content);
            var json = await resp.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("refresh_token", out var rt) &&
                doc.RootElement.TryGetProperty("access_token", out var at))
            {
                var refreshToken = rt.GetString();
                var accessToken = at.GetString();
                var expiresIn = doc.RootElement.TryGetProperty("expires_in", out var ei) ? ei.GetInt32() : 3600;

                _accessToken = accessToken;
                _tokenExpiry = DateTime.UtcNow.AddSeconds(expiresIn - 60); // 1 min buffer
                RefreshToken = refreshToken;

                return (refreshToken, accessToken, null);
            }

            var error = doc.RootElement.TryGetProperty("error", out var err) ? err.GetString() : json;
            return (null, null, error);
        }
        catch (Exception ex) { return (null, null, ex.Message); }
    }

    /// <summary>Get a valid access token — auto-refreshes if expired.</summary>
    public async Task<string?> GetAccessTokenAsync()
    {
        if (HasValidToken) return _accessToken;
        if (string.IsNullOrEmpty(RefreshToken)) return null;

        try
        {
            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = RefreshToken,
                ["client_id"] = ClientId ?? "",
                ["client_secret"] = ClientSecret ?? ""
            });

            var resp = await _http.PostAsync(OAuthUrl, content);
            var json = await resp.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("access_token", out var at))
            {
                _accessToken = at.GetString();
                var expiresIn = doc.RootElement.TryGetProperty("expires_in", out var ei) ? ei.GetInt32() : 3600;
                _tokenExpiry = DateTime.UtcNow.AddSeconds(expiresIn - 60);
                return _accessToken;
            }

            return null;
        }
        catch { return null; }
    }

    /// <summary>Make an authenticated API call. Auto-refreshes token if needed.</summary>
    public async Task<(string? Body, int StatusCode, string? Error)> CallApiAsync(
        HttpMethod method, string endpoint, string? jsonBody = null)
    {
        var token = await GetAccessTokenAsync();
        if (token == null) return (null, 0, "No valid access token — configure integration first");

        try
        {
            var url = BaseUrl.TrimEnd('/') + "/" + endpoint.TrimStart('/');
            var request = new HttpRequestMessage(method, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Zoho-oauthtoken", token);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.manageengine.sdp.v3+json"));

            if (jsonBody != null)
                request.Content = new StringContent(jsonBody, System.Text.Encoding.UTF8, "application/json");

            var resp = await _http.SendAsync(request);
            var body = await resp.Content.ReadAsStringAsync();

            return (body, (int)resp.StatusCode, resp.IsSuccessStatusCode ? null : $"HTTP {(int)resp.StatusCode}");
        }
        catch (Exception ex) { return (null, 0, ex.Message); }
    }

    /// <summary>Test the connection by calling a simple API endpoint.</summary>
    public async Task<(bool Ok, string Message)> TestConnectionAsync()
    {
        var token = await GetAccessTokenAsync();
        if (token == null) return (false, "Failed to get access token");

        var (body, status, error) = await CallApiAsync(HttpMethod.Get, "api/v3/requests?input_data=%7B%22list_info%22%3A%7B%22row_count%22%3A1%7D%7D");
        if (error != null) return (false, $"API error: {error}");

        return (true, $"Connected — HTTP {status}");
    }
}
