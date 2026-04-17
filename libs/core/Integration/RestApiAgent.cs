using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Central.Core.Integration;

/// <summary>
/// Generic REST API integration agent.
/// Connects to any JSON REST API with configurable auth, pagination, and field extraction.
/// Config: { "base_url", "auth_type" (none/bearer/basic/api_key), "auth_value", "api_key_header",
///           "list_endpoint", "list_data_path", "pagination_type" (offset/cursor/page), "page_size" }
/// </summary>
public class RestApiAgent : IIntegrationAgent
{
    private readonly HttpClient _http = new();
    private string _baseUrl = "";
    private string _authType = "none";
    private string _authValue = "";
    private string _apiKeyHeader = "X-API-Key";
    private string _listEndpoint = "";
    private string _listDataPath = "data";
    private string _paginationType = "offset";
    private int _pageSize = 100;

    public string AgentType => "rest_api";
    public string DisplayName => "REST API";

    public Task InitializeAsync(Dictionary<string, string> config)
    {
        _baseUrl = config.GetValueOrDefault("base_url", "").TrimEnd('/');
        _authType = config.GetValueOrDefault("auth_type", "none");
        _authValue = config.GetValueOrDefault("auth_value", "");
        _apiKeyHeader = config.GetValueOrDefault("api_key_header", "X-API-Key");
        _listEndpoint = config.GetValueOrDefault("list_endpoint", "");
        _listDataPath = config.GetValueOrDefault("list_data_path", "data");
        _paginationType = config.GetValueOrDefault("pagination_type", "offset");
        if (int.TryParse(config.GetValueOrDefault("page_size", "100"), out var ps)) _pageSize = ps;
        return Task.CompletedTask;
    }

    public async Task<AgentTestResult> TestConnectionAsync()
    {
        if (string.IsNullOrEmpty(_baseUrl))
            return AgentTestResult.Fail("No base_url configured");

        try
        {
            var req = BuildRequest(HttpMethod.Get, _listEndpoint + "?limit=1");
            var resp = await _http.SendAsync(req);
            return resp.IsSuccessStatusCode
                ? AgentTestResult.Ok($"Connected — HTTP {(int)resp.StatusCode}")
                : AgentTestResult.Fail($"HTTP {(int)resp.StatusCode}: {resp.ReasonPhrase}");
        }
        catch (Exception ex) { return AgentTestResult.Fail(ex.Message); }
    }

    public async Task<AgentReadResult> ReadAsync(ReadRequest request)
    {
        var records = new List<Dictionary<string, object?>>();
        int offset = 0;
        bool hasMore = true;

        while (hasMore && records.Count < request.MaxRecords)
        {
            try
            {
                var endpoint = request.EntityName;
                if (string.IsNullOrEmpty(endpoint)) endpoint = _listEndpoint;

                var sep = endpoint.Contains('?') ? '&' : '?';
                var url = _paginationType switch
                {
                    "offset" => $"{endpoint}{sep}offset={offset}&limit={_pageSize}",
                    "page" => $"{endpoint}{sep}page={offset / _pageSize + 1}&per_page={_pageSize}",
                    _ => $"{endpoint}{sep}limit={request.MaxRecords}"
                };

                var req = BuildRequest(HttpMethod.Get, url);
                var resp = await _http.SendAsync(req);
                if (!resp.IsSuccessStatusCode) break;

                var json = await resp.Content.ReadAsStringAsync();
                var doc = JsonDocument.Parse(json);

                // Navigate to data array via configured path
                var dataElement = NavigateToPath(doc.RootElement, _listDataPath);
                if (dataElement.ValueKind != JsonValueKind.Array)
                {
                    // Try root if it's an array
                    if (doc.RootElement.ValueKind == JsonValueKind.Array)
                        dataElement = doc.RootElement;
                    else
                        break;
                }

                int pageCount = 0;
                foreach (var item in dataElement.EnumerateArray())
                {
                    var record = FlattenJsonElement(item);
                    records.Add(record);
                    pageCount++;
                }

                hasMore = pageCount >= _pageSize;
                offset += pageCount;

                if (_paginationType == "none") hasMore = false;
            }
            catch { break; }
        }

        return new AgentReadResult
        {
            Success = true,
            Records = records,
            TotalAvailable = records.Count
        };
    }

    public async Task<AgentWriteResult> WriteAsync(WriteRequest request)
    {
        try
        {
            var method = request.IsUpdate ? HttpMethod.Put : HttpMethod.Post;
            var endpoint = request.IsUpdate
                ? $"{request.EntityName}/{request.ExternalId}"
                : request.EntityName;

            var json = JsonSerializer.Serialize(request.Fields);
            var req = BuildRequest(method, endpoint);
            req.Content = new StringContent(json, Encoding.UTF8, "application/json");

            var resp = await _http.SendAsync(req);
            if (!resp.IsSuccessStatusCode)
                return AgentWriteResult.Fail($"HTTP {(int)resp.StatusCode}");

            var respJson = await resp.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(respJson);
            var id = doc.RootElement.TryGetProperty("id", out var idProp) ? idProp.ToString() : request.ExternalId;
            return AgentWriteResult.Ok(id);
        }
        catch (Exception ex) { return AgentWriteResult.Fail(ex.Message); }
    }

    public Task<AgentWriteResult> DeleteAsync(string entityName, string externalId) =>
        Task.FromResult(AgentWriteResult.Fail("Delete not implemented for generic REST agent"));

    public Task<List<string>> GetEntityNamesAsync() =>
        Task.FromResult(new List<string> { string.IsNullOrEmpty(_listEndpoint) ? "data" : _listEndpoint.Trim('/') });

    public Task<List<AgentFieldInfo>> GetFieldsAsync(string entityName) =>
        Task.FromResult(new List<AgentFieldInfo>()); // Discovery requires a sample read

    // ── Helpers ──

    private HttpRequestMessage BuildRequest(HttpMethod method, string endpoint)
    {
        var url = endpoint.StartsWith("http") ? endpoint : $"{_baseUrl}/{endpoint.TrimStart('/')}";
        var req = new HttpRequestMessage(method, url);

        switch (_authType)
        {
            case "bearer":
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _authValue);
                break;
            case "basic":
                var bytes = Encoding.ASCII.GetBytes(_authValue); // "user:pass"
                req.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(bytes));
                break;
            case "api_key":
                req.Headers.Add(_apiKeyHeader, _authValue);
                break;
        }

        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return req;
    }

    private static JsonElement NavigateToPath(JsonElement root, string path)
    {
        var current = root;
        foreach (var part in path.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            if (current.TryGetProperty(part, out var next))
                current = next;
            else
                return default;
        }
        return current;
    }

    private static Dictionary<string, object?> FlattenJsonElement(JsonElement element)
    {
        var record = new Dictionary<string, object?>();
        foreach (var prop in element.EnumerateObject())
        {
            record[prop.Name] = prop.Value.ValueKind switch
            {
                JsonValueKind.String => prop.Value.GetString(),
                JsonValueKind.Number => prop.Value.TryGetInt64(out var l) ? l : prop.Value.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Null => null,
                JsonValueKind.Object => prop.Value.TryGetProperty("name", out var n) ? n.GetString() :
                                       prop.Value.TryGetProperty("id", out var id) ? id.ToString() :
                                       prop.Value.GetRawText(),
                JsonValueKind.Array => prop.Value.GetArrayLength() > 0 ? prop.Value.GetRawText() : null,
                _ => prop.Value.GetRawText()
            };
        }
        return record;
    }
}
