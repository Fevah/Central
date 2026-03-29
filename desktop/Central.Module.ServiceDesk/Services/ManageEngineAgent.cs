using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Central.Core.Integration;

namespace Central.Module.ServiceDesk.Services;

/// <summary>
/// IIntegrationAgent adapter for ManageEngine SDP On-Demand.
/// Wraps the existing ManageEngineSyncService patterns into the generic sync engine contract.
/// Supports: requests, technicians, groups, requesters entities.
/// </summary>
public class ManageEngineAgent : IIntegrationAgent
{
    private readonly HttpClient _http = new();
    private string _baseUrl = "";
    private string _oauthUrl = "";
    private string? _clientId;
    private string? _clientSecret;
    private string? _refreshToken;
    private string? _accessToken;
    private string _fieldsRequired = "priority,urgency,impact";
    private int _pageSize = 100;
    private int _maxPages = 500;

    public string AgentType => "manage_engine";
    public string DisplayName => "ManageEngine SDP";

    public Task InitializeAsync(Dictionary<string, string> config)
    {
        _baseUrl = config.GetValueOrDefault("base_url", "https://sdpondemand.manageengine.eu/api/v3/").TrimEnd('/');
        _oauthUrl = config.GetValueOrDefault("oauth_url", "https://accounts.zoho.eu/oauth/v2/token");
        _clientId = config.GetValueOrDefault("client_id");
        _clientSecret = config.GetValueOrDefault("client_secret");
        _refreshToken = config.GetValueOrDefault("refresh_token");
        _fieldsRequired = config.GetValueOrDefault("fields_required", "priority,urgency,impact");
        if (int.TryParse(config.GetValueOrDefault("page_size", "100"), out var ps)) _pageSize = ps;
        if (int.TryParse(config.GetValueOrDefault("max_pages", "500"), out var mp)) _maxPages = mp;
        return Task.CompletedTask;
    }

    public async Task<AgentTestResult> TestConnectionAsync()
    {
        if (!await RefreshTokenAsync())
            return AgentTestResult.Fail("OAuth token refresh failed");

        try
        {
            var req = BuildRequest(HttpMethod.Get, "requests",
                BuildListInfo(1, 1, null));
            var resp = await _http.SendAsync(req);
            var json = await resp.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("list_info", out var li) &&
                li.TryGetProperty("total_count", out var tc))
                return AgentTestResult.Ok($"Connected — {tc.GetInt64()} total requests", (int)tc.GetInt64());

            return AgentTestResult.Ok("Connected");
        }
        catch (Exception ex)
        {
            return AgentTestResult.Fail(ex.Message);
        }
    }

    public async Task<AgentReadResult> ReadAsync(ReadRequest request)
    {
        if (!await RefreshTokenAsync())
            return new AgentReadResult { Success = false, ErrorMessage = "OAuth token refresh failed" };

        var records = new List<Dictionary<string, object?>>();
        int page = 1;
        bool hasMore = true;

        while (hasMore && page <= _maxPages && records.Count < request.MaxRecords)
        {
            try
            {
                var listInfo = BuildListInfo(records.Count + 1, _pageSize,
                    request.Watermark != null ? long.Parse(request.Watermark) : null);
                var httpReq = BuildRequest(HttpMethod.Get, $"{request.EntityName}", listInfo);
                var resp = await _http.SendAsync(httpReq);
                var json = await resp.Content.ReadAsStringAsync();
                var doc = JsonDocument.Parse(json);

                var entityKey = request.EntityName;
                if (!doc.RootElement.TryGetProperty(entityKey, out var items)) break;

                foreach (var item in items.EnumerateArray())
                {
                    var record = new Dictionary<string, object?>();
                    foreach (var prop in item.EnumerateObject())
                    {
                        record[prop.Name] = prop.Value.ValueKind switch
                        {
                            JsonValueKind.String => prop.Value.GetString(),
                            JsonValueKind.Number => prop.Value.TryGetInt64(out var l) ? l : prop.Value.GetDouble(),
                            JsonValueKind.True => true,
                            JsonValueKind.False => false,
                            JsonValueKind.Null => null,
                            JsonValueKind.Object => prop.Value.TryGetProperty("name", out var n) ? n.GetString() : prop.Value.GetRawText(),
                            _ => prop.Value.GetRawText()
                        };
                    }
                    records.Add(record);
                }

                hasMore = doc.RootElement.TryGetProperty("list_info", out var li) &&
                          li.TryGetProperty("has_more_rows", out var hmr) && hmr.GetBoolean();
                page++;
            }
            catch (Exception ex)
            {
                return new AgentReadResult { Success = false, ErrorMessage = ex.Message, Records = records };
            }
        }

        return new AgentReadResult
        {
            Success = true,
            Records = records,
            TotalAvailable = records.Count,
            NewWatermark = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString()
        };
    }

    public async Task<AgentWriteResult> WriteAsync(WriteRequest request)
    {
        if (!await RefreshTokenAsync())
            return AgentWriteResult.Fail("OAuth token refresh failed");

        try
        {
            var inputData = JsonSerializer.Serialize(request.Fields);
            var method = request.IsUpdate ? HttpMethod.Put : HttpMethod.Post;
            var endpoint = request.IsUpdate
                ? $"{request.EntityName}/{request.ExternalId}"
                : request.EntityName;

            var req = BuildRequest(method, endpoint, inputData);
            var resp = await _http.SendAsync(req);
            var json = await resp.Content.ReadAsStringAsync();

            return resp.IsSuccessStatusCode
                ? AgentWriteResult.Ok(request.ExternalId)
                : AgentWriteResult.Fail($"HTTP {resp.StatusCode}: {json}");
        }
        catch (Exception ex) { return AgentWriteResult.Fail(ex.Message); }
    }

    public async Task<AgentWriteResult> DeleteAsync(string entityName, string externalId)
    {
        if (!await RefreshTokenAsync())
            return AgentWriteResult.Fail("Auth failed");
        try
        {
            var req = BuildRequest(HttpMethod.Delete, $"{entityName}/{externalId}");
            var resp = await _http.SendAsync(req);
            return resp.IsSuccessStatusCode ? AgentWriteResult.Ok() : AgentWriteResult.Fail($"HTTP {resp.StatusCode}");
        }
        catch (Exception ex) { return AgentWriteResult.Fail(ex.Message); }
    }

    public Task<List<string>> GetEntityNamesAsync() =>
        Task.FromResult(new List<string> { "requests", "technicians", "groups", "requesters" });

    public Task<List<AgentFieldInfo>> GetFieldsAsync(string entityName)
    {
        var fields = entityName switch
        {
            "requests" => new List<AgentFieldInfo>
            {
                new() { Name = "id", Type = "long", IsReadOnly = true },
                new() { Name = "subject", Type = "string" },
                new() { Name = "description", Type = "string" },
                new() { Name = "status", Type = "object" },
                new() { Name = "priority", Type = "object" },
                new() { Name = "urgency", Type = "object" },
                new() { Name = "impact", Type = "object" },
                new() { Name = "technician", Type = "object" },
                new() { Name = "group", Type = "object" },
                new() { Name = "requester", Type = "object" },
                new() { Name = "category", Type = "object" },
                new() { Name = "created_time", Type = "datetime" },
                new() { Name = "due_by_time", Type = "datetime" },
                new() { Name = "completed_time", Type = "datetime" },
            },
            _ => new List<AgentFieldInfo>
            {
                new() { Name = "id", Type = "long", IsReadOnly = true },
                new() { Name = "name", Type = "string" },
                new() { Name = "email_id", Type = "string" },
            }
        };
        return Task.FromResult(fields);
    }

    // ── Private helpers ──

    private async Task<bool> RefreshTokenAsync()
    {
        if (string.IsNullOrEmpty(_refreshToken) || string.IsNullOrEmpty(_clientId)) return false;
        try
        {
            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = _refreshToken!,
                ["client_id"] = _clientId!,
                ["client_secret"] = _clientSecret ?? ""
            });
            var resp = await _http.PostAsync(_oauthUrl, content);
            var json = await resp.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("access_token", out var at))
            {
                _accessToken = at.GetString();
                if (doc.RootElement.TryGetProperty("refresh_token", out var newRt))
                {
                    var newToken = newRt.GetString();
                    if (!string.IsNullOrEmpty(newToken)) _refreshToken = newToken;
                }
                return true;
            }
        }
        catch { }
        return false;
    }

    private HttpRequestMessage BuildRequest(HttpMethod method, string endpoint, string? inputData = null)
    {
        var url = $"{_baseUrl}/{endpoint.TrimStart('/')}";
        if (inputData != null && method == HttpMethod.Get)
            url += $"?input_data={Uri.EscapeDataString(inputData)}";

        var req = new HttpRequestMessage(method, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Zoho-oauthtoken", _accessToken);
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.manageengine.sdp.v3+json"));

        if (inputData != null && method != HttpMethod.Get)
            req.Content = new StringContent("{\"request\":" + inputData + "}", Encoding.UTF8, "application/json");

        return req;
    }

    private string BuildListInfo(int startIndex, int rowCount, long? updatedSinceMs)
    {
        var filter = updatedSinceMs.HasValue
            ? $",\"search_criteria\":{{\"field\":\"me_updated_time\",\"condition\":\"greater than\",\"value\":\"{updatedSinceMs}\"}}"
            : "";
        return $"{{\"list_info\":{{\"start_index\":{startIndex},\"row_count\":{rowCount},\"sort_field\":\"id\",\"sort_order\":\"asc\",\"get_total_count\":true,\"fields_required\":[\"{string.Join("\",\"", _fieldsRequired.Split(','))}\"]{filter}}}}}";
    }
}
