using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Npgsql;

namespace Central.Module.ServiceDesk.Services;

/// <summary>
/// Reads and writes ManageEngine SDP requests.
/// - Sync: pulls from ME API into local DB (incremental — only changed records)
/// - Write-back: pushes local changes to ME API (status, technician, notes, priority)
/// All config (URLs, credentials) loaded from the integrations DB tables.
/// </summary>
public class ManageEngineSyncService
{
    private readonly string _dsn;
    private readonly HttpClient _http = new();
    private string? _accessToken;

    public string BaseUrl { get; set; } = "";
    public string OAuthUrl { get; set; } = "";
    public string PortalUrl { get; set; } = "";
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }
    public string? RefreshToken { get; set; }

    public event Action<string>? StatusChanged;
    public event Action<int, int>? ProgressChanged; // current, total

    public int NewCount { get; private set; }
    public int UpdatedCount { get; private set; }

    public ManageEngineSyncService(string dsn) => _dsn = dsn;

    // ── Auth ──────────────────────────────────────────────────────────────

    /// <summary>Fired when Zoho returns a new refresh token — caller must persist it.</summary>
    public event Action<string>? RefreshTokenRotated;

    public async Task<bool> RefreshAccessTokenAsync()
    {
        if (string.IsNullOrEmpty(RefreshToken) || string.IsNullOrEmpty(ClientId)) return false;
        try
        {
            var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = RefreshToken,
                ["client_id"] = ClientId,
                ["client_secret"] = ClientSecret ?? ""
            });
            var resp = await _http.PostAsync(OAuthUrl, content);
            var json = await resp.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("access_token", out var at))
            {
                _accessToken = at.GetString();

                // Zoho may rotate the refresh token — save the new one if returned
                if (doc.RootElement.TryGetProperty("refresh_token", out var newRt))
                {
                    var newToken = newRt.GetString();
                    if (!string.IsNullOrEmpty(newToken) && newToken != RefreshToken)
                    {
                        RefreshToken = newToken;
                        RefreshTokenRotated?.Invoke(newToken);
                    }
                }
                return true;
            }
        }
        catch { }
        return false;
    }

    private HttpRequestMessage BuildRequest(HttpMethod method, string endpoint, string? inputData = null)
    {
        var url = $"{BaseUrl.TrimEnd('/')}/api/v3/{endpoint.TrimStart('/')}";
        if (inputData != null && method == HttpMethod.Get)
            url += $"?input_data={Uri.EscapeDataString(inputData)}";

        var req = new HttpRequestMessage(method, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Zoho-oauthtoken", _accessToken);
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.manageengine.sdp.v3+json"));

        if (inputData != null && method != HttpMethod.Get)
            req.Content = new StringContent("{\"request\":" + inputData + "}", Encoding.UTF8, "application/json");

        return req;
    }

    // ── Read (Sync from ME → local DB) ────────────────────────────────────

    public string? LastError { get; private set; }

    public async Task<int> SyncRequestsAsync(int maxRequests = 50000)
    {
        if (!await RefreshAccessTokenAsync())
        {
            LastError = "OAuth token refresh failed — check client_id, client_secret, and refresh_token in Integrations";
            StatusChanged?.Invoke(LastError);
            return -1;
        }

        NewCount = 0;
        UpdatedCount = 0;
        int synced = 0, page = 1;
        bool hasMore = true;

        await using var conn = new NpgsqlConnection(_dsn);
        await conn.OpenAsync();

        var lastSyncMs = await GetMaxUpdatedTimeAsync(conn);
        var syncMode = lastSyncMs.HasValue ? "incremental" : "full";
        StatusChanged?.Invoke($"Starting {syncMode} sync...");

        while (hasMore && synced < maxRequests)
        {
            StatusChanged?.Invoke($"Fetching page {page}... ({synced} synced, {NewCount} new, {UpdatedCount} updated)");

            var listInfo = BuildListInfo(synced + 1, 100, lastSyncMs);
            var request = BuildRequest(HttpMethod.Get, "requests", listInfo);

            var resp = await _http.SendAsync(request);
            var json = await resp.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("requests", out var requests)) break;

            foreach (var r in requests.EnumerateArray())
            {
                var wasNew = await UpsertRequestAsync(conn, r);
                if (wasNew) NewCount++; else UpdatedCount++;
                synced++;
            }

            hasMore = doc.RootElement.TryGetProperty("list_info", out var li) &&
                      li.TryGetProperty("has_more_rows", out var hmr) && hmr.GetBoolean();
            page++;
            ProgressChanged?.Invoke(synced, maxRequests);
        }

        StatusChanged?.Invoke($"Sync complete: {synced} requests ({NewCount} new, {UpdatedCount} updated)");
        return synced;
    }

    // ── Write (push local changes → ME API) ──────────────────────────────

    /// <summary>Update a request's status in ManageEngine.</summary>
    public async Task<(bool Ok, string Message)> UpdateStatusAsync(long requestId, string newStatus)
    {
        if (!await RefreshAccessTokenAsync()) return (false, "Auth failed");

        var inputData = $"{{\"status\":{{\"name\":\"{EscapeJson(newStatus)}\"}}}}";
        var req = BuildRequest(HttpMethod.Put, $"requests/{requestId}", inputData);
        return await SendWriteAsync(req, "status");
    }

    /// <summary>Update a request's priority in ManageEngine.</summary>
    public async Task<(bool Ok, string Message)> UpdatePriorityAsync(long requestId, string newPriority)
    {
        if (!await RefreshAccessTokenAsync()) return (false, "Auth failed");

        var inputData = $"{{\"priority\":{{\"name\":\"{EscapeJson(newPriority)}\"}}}}";
        var req = BuildRequest(HttpMethod.Put, $"requests/{requestId}", inputData);
        return await SendWriteAsync(req, "priority");
    }

    /// <summary>Assign a technician to a request in ManageEngine.</summary>
    public async Task<(bool Ok, string Message)> AssignTechnicianAsync(long requestId, string technicianName)
    {
        if (!await RefreshAccessTokenAsync()) return (false, "Auth failed");

        var inputData = $"{{\"technician\":{{\"name\":\"{EscapeJson(technicianName)}\"}}}}";
        var req = BuildRequest(HttpMethod.Put, $"requests/{requestId}", inputData);
        return await SendWriteAsync(req, "technician");
    }

    /// <summary>Add a note/reply to a request in ManageEngine.</summary>
    public async Task<(bool Ok, string Message)> AddNoteAsync(long requestId, string description, bool isPublic = false)
    {
        if (!await RefreshAccessTokenAsync()) return (false, "Auth failed");

        var url = $"{BaseUrl.TrimEnd('/')}/api/v3/requests/{requestId}/notes";
        var body = new
        {
            note = new
            {
                description,
                show_to_requester = isPublic
            }
        };
        var jsonBody = JsonSerializer.Serialize(body);

        var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Headers.Authorization = new AuthenticationHeaderValue("Zoho-oauthtoken", _accessToken);
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.manageengine.sdp.v3+json"));
        req.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

        return await SendWriteAsync(req, "note");
    }

    /// <summary>Update multiple fields on a request in one call.</summary>
    public async Task<(bool Ok, string Message)> UpdateRequestAsync(long requestId, string inputDataJson)
    {
        if (!await RefreshAccessTokenAsync()) return (false, "Auth failed");

        var req = BuildRequest(HttpMethod.Put, $"requests/{requestId}", inputDataJson);
        return await SendWriteAsync(req, "update");
    }

    private async Task<(bool Ok, string Message)> SendWriteAsync(HttpRequestMessage req, string action)
    {
        try
        {
            var resp = await _http.SendAsync(req);
            var json = await resp.Content.ReadAsStringAsync();

            if (resp.IsSuccessStatusCode)
                return (true, $"{action} updated successfully");

            // Parse ME error
            try
            {
                var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("response_status", out var rs) &&
                    rs.TryGetProperty("messages", out var msgs))
                {
                    var firstMsg = msgs.EnumerateArray().FirstOrDefault();
                    var errMsg = firstMsg.TryGetProperty("message", out var m) ? m.GetString() : json;
                    return (false, $"{action} failed: {errMsg}");
                }
            }
            catch { }

            return (false, $"{action} failed: HTTP {(int)resp.StatusCode}");
        }
        catch (Exception ex) { return (false, $"{action} error: {ex.Message}"); }
    }

    private static string EscapeJson(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");

    // ── Helpers ───────────────────────────────────────────────────────────

    // Fields to request — priority, urgency, impact are NOT included in default ME list view
    private const string FieldsRequired =
        "\"fields_required\":[\"id\",\"display_id\",\"subject\",\"status\",\"priority\",\"urgency\",\"impact\"," +
        "\"group\",\"category\",\"technician\",\"requester\",\"template\",\"created_time\",\"updated_time\"," +
        "\"completed_time\",\"due_by_time\",\"is_service_request\"]";

    private static string BuildListInfo(int startIndex, int rowCount, long? lastSyncMs)
    {
        if (lastSyncMs.HasValue)
        {
            var filterMs = lastSyncMs.Value - 60000;
            return "{\"list_info\":{\"row_count\":" + rowCount +
                   ",\"start_index\":" + startIndex +
                   ",\"sort_field\":\"updated_time\",\"sort_order\":\"desc\"," +
                   FieldsRequired +
                   ",\"search_criteria\":{\"field\":\"updated_time\",\"condition\":\"greater than\",\"value\":\"" + filterMs + "\"}}}";
        }
        return "{\"list_info\":{\"row_count\":" + rowCount +
               ",\"start_index\":" + startIndex +
               ",\"sort_field\":\"created_time\",\"sort_order\":\"desc\"," +
               FieldsRequired + "}}";
    }

    private static async Task<long?> GetMaxUpdatedTimeAsync(NpgsqlConnection conn)
    {
        await using var cmd = new NpgsqlCommand("SELECT MAX(me_updated_time) FROM sd_requests", conn);
        var result = await cmd.ExecuteScalarAsync();
        return result is long val ? val : null;
    }

    private async Task<bool> UpsertRequestAsync(NpgsqlConnection conn, JsonElement r)
    {
        var id = long.Parse(r.GetProperty("id").GetString()!);
        var displayId = GetStr(r, "display_id");
        var subject = GetStr(r, "subject");
        var status = GetObj(r, "status", "name");
        var priority = GetObj(r, "priority", "name");
        var urgency = GetObj(r, "urgency", "name");
        var impact = GetObj(r, "impact", "name");
        var group = GetObj(r, "group", "name");
        var category = GetObj(r, "category", "name");
        var template = GetObj(r, "template", "name");

        var tech = r.TryGetProperty("technician", out var t) && t.ValueKind != JsonValueKind.Null ? t : (JsonElement?)null;
        var techId = tech.HasValue && tech.Value.TryGetProperty("id", out var tid) ? long.Parse(tid.GetString()!) : (long?)null;
        var techName = tech.HasValue ? GetStr(tech.Value, "name") : "";

        var req = r.TryGetProperty("requester", out var rq) && rq.ValueKind != JsonValueKind.Null ? rq : (JsonElement?)null;
        var reqId = req.HasValue && req.Value.TryGetProperty("id", out var rid) ? long.Parse(rid.GetString()!) : (long?)null;
        var reqName = req.HasValue ? GetStr(req.Value, "name") : "";
        var reqEmail = req.HasValue ? GetStr(req.Value, "email_id") : "";
        var reqSite = req.HasValue ? GetObj(req.Value, "site", "name") : "";
        var reqDept = req.HasValue ? GetObj(req.Value, "department", "name") : "";

        var createdMs = GetTime(r, "created_time");
        var updatedMs = GetTime(r, "updated_time");
        var completedMs = GetTime(r, "completed_time");
        // Fallback: if Resolved/Closed but no completed_time, use updated_time as resolution date
        if (completedMs == null && (status == "Resolved" || status == "Closed"))
            completedMs = updatedMs;
        var dueMs = GetTime(r, "due_by_time");
        var isServiceReq = r.TryGetProperty("is_service_request", out var isr) && isr.GetBoolean();

        var ticketUrl = !string.IsNullOrEmpty(displayId) && !string.IsNullOrEmpty(PortalUrl)
            ? $"{PortalUrl.TrimEnd('/')}/app/itdesk/ui/requests/{id}/details" : "";

        if (reqId.HasValue)
        {
            await using var rCmd = new NpgsqlCommand(
                @"INSERT INTO sd_requesters (id, name, email, site, department, synced_at)
                  VALUES (@id, @n, @e, @s, @d, NOW())
                  ON CONFLICT (id) DO UPDATE SET name=@n, email=@e, site=@s, department=@d, synced_at=NOW()", conn);
            rCmd.Parameters.AddWithValue("id", reqId.Value);
            rCmd.Parameters.AddWithValue("n", reqName);
            rCmd.Parameters.AddWithValue("e", reqEmail);
            rCmd.Parameters.AddWithValue("s", reqSite);
            rCmd.Parameters.AddWithValue("d", reqDept);
            await rCmd.ExecuteNonQueryAsync();
        }

        if (techId.HasValue && !string.IsNullOrEmpty(techName))
        {
            await using var tCmd = new NpgsqlCommand(
                @"INSERT INTO sd_technicians (id, name, synced_at)
                  VALUES (@id, @n, NOW())
                  ON CONFLICT (id) DO UPDATE SET name=@n, synced_at=NOW()", conn);
            tCmd.Parameters.AddWithValue("id", techId.Value);
            tCmd.Parameters.AddWithValue("n", techName);
            await tCmd.ExecuteNonQueryAsync();
        }

        await using var cmd = new NpgsqlCommand(
            @"INSERT INTO sd_requests (id, display_id, subject, status, priority, urgency, impact, group_name, category,
                technician_id, technician_name, requester_id, requester_name, requester_email,
                site, department, template, is_service_request, created_at, due_by, resolved_at,
                me_created_time, me_updated_time, me_completed_time, ticket_url, synced_at)
              VALUES (@id, @did, @sub, @st, @pri, @urg, @imp, @grp, @cat, @tid, @tn, @rid, @rn, @re,
                @site, @dept, @tpl, @isr, @ca, @due, @ra, @mct, @mut, @mcomp, @url, NOW())
              ON CONFLICT (id) DO UPDATE SET
                subject=@sub, status=@st, priority=@pri, urgency=@urg, impact=@imp, group_name=@grp, category=@cat,
                technician_id=@tid, technician_name=@tn, requester_id=@rid, requester_name=@rn,
                requester_email=@re, site=@site, department=@dept, template=@tpl,
                is_service_request=@isr, due_by=@due, resolved_at=@ra,
                me_updated_time=@mut, me_completed_time=@mcomp, ticket_url=@url, synced_at=NOW()
              RETURNING (xmax = 0) AS is_new", conn);

        cmd.Parameters.AddWithValue("id", id);
        cmd.Parameters.AddWithValue("did", displayId);
        cmd.Parameters.AddWithValue("sub", subject);
        cmd.Parameters.AddWithValue("st", status);
        cmd.Parameters.AddWithValue("pri", priority);
        cmd.Parameters.AddWithValue("urg", urgency);
        cmd.Parameters.AddWithValue("imp", impact);
        cmd.Parameters.AddWithValue("grp", group);
        cmd.Parameters.AddWithValue("cat", category);
        cmd.Parameters.AddWithValue("tid", (object?)techId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("tn", techName);
        cmd.Parameters.AddWithValue("rid", (object?)reqId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("rn", reqName);
        cmd.Parameters.AddWithValue("re", reqEmail);
        cmd.Parameters.AddWithValue("site", reqSite);
        cmd.Parameters.AddWithValue("dept", reqDept);
        cmd.Parameters.AddWithValue("tpl", template);
        cmd.Parameters.AddWithValue("isr", isServiceReq);
        cmd.Parameters.AddWithValue("ca", createdMs.HasValue ? DateTimeOffset.FromUnixTimeMilliseconds(createdMs.Value).UtcDateTime : (object)DBNull.Value);
        cmd.Parameters.AddWithValue("due", dueMs.HasValue ? DateTimeOffset.FromUnixTimeMilliseconds(dueMs.Value).UtcDateTime : (object)DBNull.Value);
        cmd.Parameters.AddWithValue("ra", completedMs.HasValue ? DateTimeOffset.FromUnixTimeMilliseconds(completedMs.Value).UtcDateTime : (object)DBNull.Value);
        cmd.Parameters.AddWithValue("mct", (object?)createdMs ?? DBNull.Value);
        cmd.Parameters.AddWithValue("mut", (object?)updatedMs ?? DBNull.Value);
        cmd.Parameters.AddWithValue("mcomp", (object?)completedMs ?? DBNull.Value);
        cmd.Parameters.AddWithValue("url", ticketUrl);

        var result = await cmd.ExecuteScalarAsync();
        return result is bool isNew && isNew;
    }

    private static string GetStr(JsonElement e, string prop) =>
        e.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

    private static string GetObj(JsonElement e, string prop, string subProp) =>
        e.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.Object && v.TryGetProperty(subProp, out var sv)
            ? sv.GetString() ?? "" : "";

    private static long? GetTime(JsonElement e, string prop) =>
        e.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.Object &&
        v.TryGetProperty("value", out var val) && val.ValueKind == JsonValueKind.String &&
        long.TryParse(val.GetString(), out var ms) ? ms : null;
}
