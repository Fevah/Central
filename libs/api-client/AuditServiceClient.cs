using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Central.ApiClient;

/// <summary>
/// Client for the Rust audit-service. Investigations, GDPR scoring, M365 log search,
/// evidence export. Routes via gateway /api/v1/audit/*.
/// </summary>
public class AuditServiceClient
{
    private readonly HttpClient _http;

    public AuditServiceClient(string baseUrl = "http://localhost:8000")
    {
        _http = new HttpClient { BaseAddress = new Uri(baseUrl.TrimEnd('/')) };
    }

    public void SetAuthToken(string token)
        => _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    public void SetTenant(string tenantId)
    {
        _http.DefaultRequestHeaders.Remove("X-Tenant-ID");
        _http.DefaultRequestHeaders.Add("X-Tenant-ID", tenantId);
    }

    // ── Investigations ──

    public async Task<List<AuditInvestigation>> GetInvestigationsAsync()
    {
        var resp = await _http.GetAsync("/api/v1/audit/investigations");
        if (!resp.IsSuccessStatusCode) return [];
        return await resp.Content.ReadFromJsonAsync<List<AuditInvestigation>>(JsonOpts) ?? [];
    }

    public async Task<AuditInvestigationDetail?> GetInvestigationAsync(string id)
    {
        var resp = await _http.GetAsync($"/api/v1/audit/investigations/{id}");
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadFromJsonAsync<AuditInvestigationDetail>(JsonOpts);
    }

    public async Task<AuditCreateResult?> CreateInvestigationAsync(string title, string? description = null, string? targetUser = null)
    {
        var resp = await _http.PostAsJsonAsync("/api/v1/audit/investigations",
            new { title, description, target_user = targetUser }, JsonOpts);
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadFromJsonAsync<AuditCreateResult>(JsonOpts);
    }

    public async Task<AuditEvidenceExport?> ExportEvidenceAsync(string investigationId)
    {
        var resp = await _http.PostAsync($"/api/v1/audit/investigations/{investigationId}/export", null);
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadFromJsonAsync<AuditEvidenceExport>(JsonOpts);
    }

    // ── GDPR ──

    public async Task<GdprScoreResult?> GetGdprScoreAsync()
    {
        var resp = await _http.GetAsync("/api/v1/audit/gdpr/score");
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadFromJsonAsync<GdprScoreResult>(JsonOpts);
    }

    public async Task<List<GdprArticleResult>> GetGdprArticlesAsync()
    {
        var resp = await _http.GetAsync("/api/v1/audit/gdpr/articles");
        if (!resp.IsSuccessStatusCode) return [];
        return await resp.Content.ReadFromJsonAsync<List<GdprArticleResult>>(JsonOpts) ?? [];
    }

    // ── M365 ──

    public async Task<List<M365LogEntry>> SearchLogsAsync(string? user = null, string? operation = null)
    {
        var qs = new List<string>();
        if (!string.IsNullOrEmpty(user)) qs.Add($"user={Uri.EscapeDataString(user)}");
        if (!string.IsNullOrEmpty(operation)) qs.Add($"operation={Uri.EscapeDataString(operation)}");
        var url = "/api/v1/audit/m365/logs" + (qs.Count > 0 ? "?" + string.Join("&", qs) : "");
        var resp = await _http.GetAsync(url);
        if (!resp.IsSuccessStatusCode) return [];
        return await resp.Content.ReadFromJsonAsync<List<M365LogEntry>>(JsonOpts) ?? [];
    }

    public async Task<M365UserActivity?> GetUserActivityAsync(string upn)
    {
        var resp = await _http.GetAsync($"/api/v1/audit/m365/users/{Uri.EscapeDataString(upn)}/activity");
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadFromJsonAsync<M365UserActivity>(JsonOpts);
    }

    public async Task<M365DocumentSharing?> GetDocumentSharingAsync(string docId)
    {
        var resp = await _http.GetAsync($"/api/v1/audit/documents/{Uri.EscapeDataString(docId)}/sharing");
        if (!resp.IsSuccessStatusCode) return null;
        return await resp.Content.ReadFromJsonAsync<M365DocumentSharing>(JsonOpts);
    }

    public async Task<bool> IsHealthyAsync()
    {
        try { return (await _http.GetAsync("/health")).IsSuccessStatusCode; }
        catch { return false; }
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
}

// ── DTOs ──

public class AuditInvestigation
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("title")] public string Title { get; set; } = "";
    [JsonPropertyName("description")] public string? Description { get; set; }
    [JsonPropertyName("target_user")] public string? TargetUser { get; set; }
    [JsonPropertyName("status")] public string Status { get; set; } = "open";
    [JsonPropertyName("created_by")] public string? CreatedBy { get; set; }
    [JsonPropertyName("created_at")] public string CreatedAt { get; set; } = "";
}

public class AuditFinding
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("finding_type")] public string FindingType { get; set; } = "";
    [JsonPropertyName("severity")] public string Severity { get; set; } = "";
    [JsonPropertyName("title")] public string Title { get; set; } = "";
    [JsonPropertyName("detail")] public string? Detail { get; set; }
    [JsonPropertyName("evidence_json")] public JsonElement? EvidenceJson { get; set; }
    [JsonPropertyName("created_at")] public string CreatedAt { get; set; } = "";
}

public class AuditInvestigationDetail
{
    [JsonPropertyName("investigation")] public AuditInvestigation Investigation { get; set; } = new();
    [JsonPropertyName("findings")] public List<AuditFinding> Findings { get; set; } = [];
}

public class AuditCreateResult
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("findings")] public int Findings { get; set; }
}

public class AuditEvidenceExport
{
    [JsonPropertyName("export")] public JsonElement? Export { get; set; }
    [JsonPropertyName("integrity_hash")] public string IntegrityHash { get; set; } = "";
}

public class GdprScoreResult
{
    [JsonPropertyName("overall_score")] public double OverallScore { get; set; }
    [JsonPropertyName("overall_grade")] public string OverallGrade { get; set; } = "";
    [JsonPropertyName("compliant_count")] public int CompliantCount { get; set; }
    [JsonPropertyName("partial_count")] public int PartialCount { get; set; }
    [JsonPropertyName("non_compliant_count")] public int NonCompliantCount { get; set; }
}

public class GdprArticleResult
{
    [JsonPropertyName("article")] public string Article { get; set; } = "";
    [JsonPropertyName("title")] public string Title { get; set; } = "";
    [JsonPropertyName("score")] public double Score { get; set; }
    [JsonPropertyName("status")] public string Status { get; set; } = "";
    [JsonPropertyName("findings")] public List<string> Findings { get; set; } = [];
}

public class M365LogEntry
{
    [JsonPropertyName("timestamp")] public string Timestamp { get; set; } = "";
    [JsonPropertyName("user")] public string User { get; set; } = "";
    [JsonPropertyName("operation")] public string Operation { get; set; } = "";
    [JsonPropertyName("target")] public string Target { get; set; } = "";
    [JsonPropertyName("client_ip")] public string ClientIp { get; set; } = "";
    [JsonPropertyName("result")] public string Result { get; set; } = "";
}

public class M365UserActivity
{
    [JsonPropertyName("user")] public string User { get; set; } = "";
    [JsonPropertyName("timeline")] public List<M365LogEntry> Timeline { get; set; } = [];
    [JsonPropertyName("risk_score")] public double RiskScore { get; set; }
    [JsonPropertyName("anomalies")] public List<string> Anomalies { get; set; } = [];
}

public class M365DocumentSharing
{
    [JsonPropertyName("document_id")] public string DocumentId { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("shared_with")] public List<M365ShareRecord> SharedWith { get; set; } = [];
    [JsonPropertyName("dlp_violations")] public List<string> DlpViolations { get; set; } = [];
}

public class M365ShareRecord
{
    [JsonPropertyName("user")] public string User { get; set; } = "";
    [JsonPropertyName("permission")] public string Permission { get; set; } = "";
    [JsonPropertyName("shared_at")] public string SharedAt { get; set; } = "";
    [JsonPropertyName("is_external")] public bool IsExternal { get; set; }
}
