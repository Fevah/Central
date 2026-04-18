using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Web;

namespace Central.ApiClient;

/// <summary>
/// Typed wrapper for the Rust networking-engine service (Phase 6.5+ port of
/// <c>libs/persistence/Net/*.cs</c>). Method shapes mirror the original
/// <c>AllocationService</c>, <c>IpAllocationService</c>, and
/// <c>ServerCreationService</c> so the WPF side can swap the concrete type
/// at DI registration and keep every call-site unchanged during the cutover
/// window.
///
/// <para>The Rust service binds to port 8091 by default (<c>BIND_ADDR</c>);
/// in-cluster it's reachable via <c>http://networking-engine.central:8091</c>
/// per <c>infra/k8s/base/networking-engine.yaml</c>.</para>
///
/// <para>Errors come back as RFC-7807 problem+json from axum; the client maps
/// common HTTP statuses to typed <see cref="NetworkingEngineException"/>
/// subclasses (pool exhausted -> <c>PoolExhaustedException</c>, 404 ->
/// <c>NotFoundException</c>, lock violation -> <c>LockViolationException</c>).
/// </para>
/// </summary>
public class NetworkingEngineClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly bool _ownsClient;

    /// <summary>Create a new client bound to <paramref name="baseUrl"/>.
    /// Pass an explicit <see cref="HttpClient"/> via the other constructor
    /// when plugging into an <c>IHttpClientFactory</c>.</summary>
    public NetworkingEngineClient(string baseUrl = "http://localhost:8091")
    {
        _http = new HttpClient { BaseAddress = new Uri(baseUrl.TrimEnd('/')) };
        _ownsClient = true;
    }

    public NetworkingEngineClient(HttpClient http)
    {
        _http = http;
        _ownsClient = false;
    }

    /// <summary>Set the actor user id that stamps into audit rows. Threaded
    /// into the <c>X-User-Id</c> header which every mutation endpoint reads.</summary>
    public void SetActorUserId(int userId)
    {
        _http.DefaultRequestHeaders.Remove("X-User-Id");
        _http.DefaultRequestHeaders.Add("X-User-Id", userId.ToString());
    }

    public void SetAuthToken(string token)
        => _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    public void Dispose()
    {
        if (_ownsClient) _http.Dispose();
        GC.SuppressFinalize(this);
    }

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    // ─── Allocation ──────────────────────────────────────────────────────

    public Task<AsnAllocationDto> AllocateAsnAsync(Guid blockId, Guid organizationId,
        string allocatedToType, Guid allocatedToId, CancellationToken ct = default)
        => PostAsync<AsnAllocationDto>("/api/net/allocate/asn",
            new { blockId, organizationId, allocatedToType, allocatedToId }, ct);

    public Task<VlanDto> AllocateVlanAsync(Guid blockId, Guid organizationId,
        string displayName, string? description, string scopeLevel,
        Guid? scopeEntityId = null, Guid? templateId = null, CancellationToken ct = default)
        => PostAsync<VlanDto>("/api/net/allocate/vlan",
            new { blockId, organizationId, displayName, description,
                  scopeLevel, scopeEntityId, templateId }, ct);

    public Task<MlagDomainDto> AllocateMlagDomainAsync(Guid poolId, Guid organizationId,
        string displayName, string scopeLevel, Guid? scopeEntityId = null,
        CancellationToken ct = default)
        => PostAsync<MlagDomainDto>("/api/net/allocate/mlag",
            new { poolId, organizationId, displayName, scopeLevel, scopeEntityId }, ct);

    public Task<IpAddressDto> AllocateNextIpAsync(Guid subnetId, Guid organizationId,
        string? assignedToType = null, Guid? assignedToId = null, CancellationToken ct = default)
        => PostAsync<IpAddressDto>("/api/net/allocate/ip",
            new { subnetId, organizationId, assignedToType, assignedToId }, ct);

    public Task<SubnetDto> AllocateSubnetAsync(Guid poolId, Guid organizationId,
        int prefixLength, string subnetCode, string displayName,
        string scopeLevel, Guid? scopeEntityId = null, Guid? parentSubnetId = null,
        CancellationToken ct = default)
        => PostAsync<SubnetDto>("/api/net/allocate/subnet",
            new { poolId, organizationId, prefixLength, subnetCode, displayName,
                  scopeLevel, scopeEntityId, parentSubnetId }, ct);

    public Task<ReservationShelfEntryDto> RetireAsync(Guid organizationId,
        string resourceType, string resourceKey, TimeSpan cooldown,
        Guid? poolId = null, Guid? blockId = null, string? reason = null,
        CancellationToken ct = default)
        => PostAsync<ReservationShelfEntryDto>("/api/net/allocate/retire",
            new { organizationId, resourceType, resourceKey,
                  cooldownSeconds = (long)cooldown.TotalSeconds,
                  poolId, blockId, reason }, ct);

    public async Task<bool> IsOnShelfAsync(Guid organizationId, string resourceType,
        string resourceKey, CancellationToken ct = default)
    {
        var qs = $"?organizationId={organizationId}";
        var url = $"/api/net/allocate/shelf/{Uri.EscapeDataString(resourceType)}/" +
                  $"{Uri.EscapeDataString(resourceKey)}{qs}";
        var body = await GetAsync<IsOnShelfResponse>(url, ct);
        return body.OnShelf;
    }

    // ─── Server fan-out ──────────────────────────────────────────────────

    public Task<ServerCreationResultDto> CreateServerWithFanOutAsync(
        ServerCreationRequestDto request, CancellationToken ct = default)
        => PostAsync<ServerCreationResultDto>("/api/net/servers/create-with-fanout", request, ct);

    // ─── Naming ──────────────────────────────────────────────────────────

    public Task<NamePreviewResponse> PreviewLinkNameAsync(string template,
        LinkNamingContextDto context, CancellationToken ct = default)
        => PostAsync<NamePreviewResponse>("/api/net/naming/link/preview",
            new { template, context }, ct);

    public Task<NamePreviewResponse> PreviewDeviceNameAsync(string template,
        DeviceNamingContextDto context, CancellationToken ct = default)
        => PostAsync<NamePreviewResponse>("/api/net/naming/device/preview",
            new { template, context }, ct);

    public Task<NamePreviewResponse> PreviewServerNameAsync(string template,
        ServerNamingContextDto context, CancellationToken ct = default)
        => PostAsync<NamePreviewResponse>("/api/net/naming/server/preview",
            new { template, context }, ct);

    public Task<ResolveTemplateResponse> ResolveNamingTemplateAsync(
        ResolveTemplateRequest request, CancellationToken ct = default)
        => PostAsync<ResolveTemplateResponse>("/api/net/naming/resolve", request, ct);

    public Task<RegeneratePreviewResponse> PreviewRegenerateAsync(
        RegeneratePreviewRequest request, CancellationToken ct = default)
        => PostAsync<RegeneratePreviewResponse>("/api/net/naming/regenerate/preview", request, ct);

    public Task<RegenerateApplyResponse> ApplyRegenerateAsync(
        RegenerateApplyRequest request, CancellationToken ct = default)
        => PostAsync<RegenerateApplyResponse>("/api/net/naming/regenerate/apply", request, ct);

    // ─── Change Sets (Phase 8) ───────────────────────────────────────────

    public Task<ChangeSetDto> CreateChangeSetAsync(CreateChangeSetRequest request,
        CancellationToken ct = default)
        => PostAsync<ChangeSetDto>("/api/net/change-sets", request, ct);

    public Task<List<ChangeSetDto>> ListChangeSetsAsync(Guid organizationId,
        string? status = null, int? limit = null, CancellationToken ct = default)
    {
        var qs = BuildQuery(("organizationId", organizationId.ToString()),
                            ("status", status), ("limit", limit?.ToString()));
        return GetAsync<List<ChangeSetDto>>($"/api/net/change-sets{qs}", ct);
    }

    public Task<ChangeSetDetailDto> GetChangeSetAsync(Guid id, Guid organizationId,
        CancellationToken ct = default)
        => GetAsync<ChangeSetDetailDto>($"/api/net/change-sets/{id}?organizationId={organizationId}", ct);

    /// <summary>Resolve a Change Set from the correlation id stamped on its
    /// audit entries. Returns null when the id doesn't map to a Set (the
    /// common case for entity-level audits that happened outside any
    /// Change Set, like ad-hoc allocation retires).</summary>
    public async Task<ChangeSetDetailDto?> GetChangeSetByCorrelationAsync(
        Guid correlationId, Guid organizationId, CancellationToken ct = default)
    {
        var resp = await _http.GetAsync(
            $"/api/net/change-sets/by-correlation/{correlationId}?organizationId={organizationId}", ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        await EnsureSuccessAsync(resp, ct);
        return await resp.Content.ReadFromJsonAsync<ChangeSetDetailDto>(Json, ct);
    }

    public Task<ChangeSetItemDto> AddChangeSetItemAsync(Guid setId, Guid organizationId,
        AddChangeSetItemRequest request, CancellationToken ct = default)
        => PostAsync<ChangeSetItemDto>(
            $"/api/net/change-sets/{setId}/items?organizationId={organizationId}", request, ct);

    public Task<ChangeSetDto> SubmitChangeSetAsync(Guid setId, Guid organizationId,
        int requiredApprovals = 1, CancellationToken ct = default)
        => PostAsync<ChangeSetDto>(
            $"/api/net/change-sets/{setId}/submit?organizationId={organizationId}",
            new { requiredApprovals }, ct);

    public Task<DecisionResultDto> RecordDecisionAsync(Guid setId, Guid organizationId,
        string decision, string? approverDisplay = null, string? notes = null,
        CancellationToken ct = default)
        => PostAsync<DecisionResultDto>(
            $"/api/net/change-sets/{setId}/decisions?organizationId={organizationId}",
            new { decision, approverDisplay, notes }, ct);

    public Task<List<ApprovalDto>> ListApprovalsAsync(Guid setId, Guid organizationId,
        CancellationToken ct = default)
        => GetAsync<List<ApprovalDto>>(
            $"/api/net/change-sets/{setId}/decisions?organizationId={organizationId}", ct);

    // ─── Device list (WPF picker read) ──────────────────────────────────

    public Task<List<DeviceListRowDto>> ListDevicesAsync(Guid organizationId,
        CancellationToken ct = default)
        => GetAsync<List<DeviceListRowDto>>(
            $"/api/net/devices?organizationId={organizationId}", ct);

    /// <summary>List VLAN blocks + per-block availability. Powers the
    /// WPF Create VLAN picker so admins see "VLAN 100-199 · 12 free"
    /// instead of a UUID they'd have to copy from another tool.</summary>
    public Task<List<VlanBlockDto>> ListVlanBlocksAsync(Guid organizationId,
        CancellationToken ct = default)
        => GetAsync<List<VlanBlockDto>>(
            $"/api/net/vlan-blocks?organizationId={organizationId}", ct);

    public Task<ChangeSetDto> CancelChangeSetAsync(Guid setId, Guid organizationId,
        string? notes = null, CancellationToken ct = default)
        => PostAsync<ChangeSetDto>(
            $"/api/net/change-sets/{setId}/cancel?organizationId={organizationId}",
            new { notes }, ct);

    public Task<ApplyResultDto> ApplyChangeSetAsync(Guid setId, Guid organizationId,
        CancellationToken ct = default)
        => PostAsync<ApplyResultDto>(
            $"/api/net/change-sets/{setId}/apply?organizationId={organizationId}",
            new { }, ct);

    public Task<RollbackResultDto> RollbackChangeSetAsync(Guid setId, Guid organizationId,
        CancellationToken ct = default)
        => PostAsync<RollbackResultDto>(
            $"/api/net/change-sets/{setId}/rollback?organizationId={organizationId}",
            new { }, ct);

    // ─── Audit (Phase 9) ─────────────────────────────────────────────────

    public Task<List<AuditRowDto>> ListAuditAsync(ListAuditRequest request,
        CancellationToken ct = default)
    {
        var qs = BuildQuery(
            ("organizationId", request.OrganizationId.ToString()),
            ("entityType", request.EntityType),
            ("entityId", request.EntityId?.ToString()),
            ("action", request.Action),
            ("actorUserId", request.ActorUserId?.ToString()),
            ("correlationId", request.CorrelationId?.ToString()),
            ("fromAt", request.FromAt?.ToString("o")),
            ("toAt", request.ToAt?.ToString("o")),
            ("limit", request.Limit?.ToString()));
        return GetAsync<List<AuditRowDto>>($"/api/net/audit{qs}", ct);
    }

    public Task<List<AuditRowDto>> GetEntityTimelineAsync(Guid organizationId,
        string entityType, Guid entityId, int? limit = null, CancellationToken ct = default)
    {
        var qs = BuildQuery(("organizationId", organizationId.ToString()),
                            ("limit", limit?.ToString()));
        return GetAsync<List<AuditRowDto>>(
            $"/api/net/audit/entity/{Uri.EscapeDataString(entityType)}/{entityId}{qs}", ct);
    }

    public Task<VerifyChainResponse> VerifyAuditChainAsync(Guid organizationId,
        long? fromSequenceId = null, long? limit = null, CancellationToken ct = default)
    {
        var qs = BuildQuery(("organizationId", organizationId.ToString()),
                            ("fromSequenceId", fromSequenceId?.ToString()),
                            ("limit", limit?.ToString()));
        return GetAsync<VerifyChainResponse>($"/api/net/audit/verify{qs}", ct);
    }

    /// <summary>Export audit rows as CSV or NDJSON. Returns the raw body
    /// as a string (text/csv or application/x-ndjson).</summary>
    public async Task<string> ExportAuditAsync(Guid organizationId, string format,
        string? entityType = null, Guid? entityId = null, DateTime? fromAt = null,
        DateTime? toAt = null, long? limit = null, Guid? correlationId = null,
        CancellationToken ct = default)
    {
        var qs = BuildQuery(("organizationId", organizationId.ToString()),
                            ("format", format),
                            ("entityType", entityType),
                            ("entityId", entityId?.ToString()),
                            ("correlationId", correlationId?.ToString()),
                            ("fromAt", fromAt?.ToString("o")),
                            ("toAt", toAt?.ToString("o")),
                            ("limit", limit?.ToString()));
        var resp = await _http.GetAsync($"/api/net/audit/export{qs}", ct);
        await EnsureSuccessAsync(resp, ct);
        return await resp.Content.ReadAsStringAsync(ct);
    }

    // ─── Validation (Phase 9a) ───────────────────────────────────────────

    public Task<List<ResolvedRuleDto>> ListValidationRulesAsync(Guid organizationId,
        CancellationToken ct = default)
        => GetAsync<List<ResolvedRuleDto>>(
            $"/api/net/validation/rules?organizationId={organizationId}", ct);

    public async Task SetRuleConfigAsync(Guid organizationId, string ruleCode,
        bool? enabled = null, string? severityOverride = null, string? notes = null,
        CancellationToken ct = default)
    {
        var resp = await _http.PutAsJsonAsync(
            $"/api/net/validation/rules/{Uri.EscapeDataString(ruleCode)}?organizationId={organizationId}",
            new { enabled, severityOverride, notes }, Json, ct);
        await EnsureSuccessAsync(resp, ct);
    }

    public Task<ValidationRunResultDto> RunValidationAsync(Guid organizationId,
        string? ruleCode = null, CancellationToken ct = default)
        => PostAsync<ValidationRunResultDto>("/api/net/validation/run",
            new { organizationId, ruleCode }, ct);

    // ─── Locks (Phase 8f) ────────────────────────────────────────────────

    public Task<LockChangeResultDto> SetEntityLockAsync(string table, Guid id,
        Guid organizationId, string lockState, string? lockReason = null,
        CancellationToken ct = default)
    {
        var req = new HttpRequestMessage(HttpMethod.Patch,
            $"/api/net/locks/{Uri.EscapeDataString(table)}/{id}?organizationId={organizationId}")
        {
            Content = JsonContent.Create(new { lockState, lockReason }, options: Json),
        };
        return SendAsync<LockChangeResultDto>(req, ct);
    }

    /// <summary>List every non-Open row across the five lock-enforced
    /// numbering tables (asn_allocation / vlan / mlag_domain / subnet /
    /// ip_address). Pass <paramref name="table"/> to narrow to one; pass
    /// <paramref name="lockState"/> to filter by specific state.</summary>
    public Task<List<LockedRowDto>> ListLockedAsync(Guid organizationId,
        string? table = null, string? lockState = null, CancellationToken ct = default)
    {
        var qs = BuildQuery(("organizationId", organizationId.ToString()),
                            ("table", table),
                            ("lockState", lockState));
        return GetAsync<List<LockedRowDto>>($"/api/net/locks{qs}", ct);
    }

    // ─── Transport helpers ──────────────────────────────────────────────

    private static string BuildQuery(params (string key, string? value)[] parts)
    {
        var qs = string.Join("&", parts
            .Where(p => !string.IsNullOrWhiteSpace(p.value))
            .Select(p => $"{p.key}={HttpUtility.UrlEncode(p.value)}"));
        return qs.Length == 0 ? "" : "?" + qs;
    }

    private async Task<T> GetAsync<T>(string url, CancellationToken ct)
    {
        var resp = await _http.GetAsync(url, ct);
        await EnsureSuccessAsync(resp, ct);
        return (await resp.Content.ReadFromJsonAsync<T>(Json, ct))
            ?? throw new NetworkingEngineException(0, $"GET {url} returned null body");
    }

    private async Task<T> PostAsync<T>(string url, object body, CancellationToken ct)
    {
        var resp = await _http.PostAsJsonAsync(url, body, Json, ct);
        await EnsureSuccessAsync(resp, ct);
        return (await resp.Content.ReadFromJsonAsync<T>(Json, ct))
            ?? throw new NetworkingEngineException(0, $"POST {url} returned null body");
    }

    private async Task<T> SendAsync<T>(HttpRequestMessage req, CancellationToken ct)
    {
        var resp = await _http.SendAsync(req, ct);
        await EnsureSuccessAsync(resp, ct);
        return (await resp.Content.ReadFromJsonAsync<T>(Json, ct))
            ?? throw new NetworkingEngineException(0, $"{req.Method} {req.RequestUri} returned null body");
    }

    /// <summary>Translate the axum <c>problem+json</c> body into a typed
    /// exception. The Rust side stamps one of a small set of <c>error</c>
    /// codes — we route the ones the caller most often cares about to
    /// named exception subclasses, the rest fall through to a generic
    /// <see cref="NetworkingEngineException"/>.</summary>
    private static async Task EnsureSuccessAsync(HttpResponseMessage resp, CancellationToken ct)
    {
        if (resp.IsSuccessStatusCode) return;

        string? errorCode = null, message = null;
        try
        {
            var body = await resp.Content.ReadFromJsonAsync<ErrorBody>(Json, ct);
            errorCode = body?.Error;
            message = body?.Message;
        }
        catch (JsonException) { /* body wasn't JSON — fall through */ }

        message ??= $"HTTP {(int)resp.StatusCode} {resp.ReasonPhrase}";
        throw errorCode switch
        {
            "pool_exhausted" =>
                new PoolExhaustedException(message),
            "container_not_found" or "server_profile_not_found" =>
                new NotFoundException((int)resp.StatusCode, message),
            "lock_violation" =>
                new LockViolationException(message),
            _ =>
                new NetworkingEngineException((int)resp.StatusCode, message),
        };
    }

    private record ErrorBody(string? Error, string? Message);
}

// ═══════════════════════════════════════════════════════════════════════════
// Exceptions
// ═══════════════════════════════════════════════════════════════════════════

public class NetworkingEngineException : Exception
{
    public int StatusCode { get; }
    public NetworkingEngineException(int status, string message) : base(message)
        => StatusCode = status;
}

public class NotFoundException : NetworkingEngineException
{
    public NotFoundException(int status, string message) : base(status, message) {}
}

public class PoolExhaustedException : NetworkingEngineException
{
    public PoolExhaustedException(string message) : base(409, message) {}
}

public class LockViolationException : NetworkingEngineException
{
    public LockViolationException(string message) : base(409, message) {}
}

// ═══════════════════════════════════════════════════════════════════════════
// DTOs (kept in one file for read-the-wrapper-cold readability; split off
// if this grows past ~1000 lines)
// ═══════════════════════════════════════════════════════════════════════════

public record AsnAllocationDto(Guid Id, Guid OrganizationId, Guid BlockId, long Asn,
    string AllocatedToType, Guid AllocatedToId, DateTime AllocatedAt);

public record VlanDto(Guid Id, Guid OrganizationId, Guid BlockId, Guid? TemplateId,
    int VlanId, string DisplayName, string? Description, string ScopeLevel, Guid? ScopeEntityId);

public record MlagDomainDto(Guid Id, Guid OrganizationId, Guid PoolId, int DomainId,
    string DisplayName, string ScopeLevel, Guid? ScopeEntityId);

public record IpAddressDto(Guid Id, Guid OrganizationId, Guid SubnetId, string Address,
    string? AssignedToType, Guid? AssignedToId, bool IsReserved, DateTime AssignedAt);

public record SubnetDto(Guid Id, Guid OrganizationId, Guid PoolId, Guid? ParentSubnetId,
    string SubnetCode, string DisplayName, string Network, string ScopeLevel, Guid? ScopeEntityId);

public record ReservationShelfEntryDto(Guid Id, Guid OrganizationId, string ResourceType,
    string ResourceKey, Guid? PoolId, Guid? BlockId, DateTime RetiredAt,
    DateTime AvailableAfter, string? RetiredReason);

public record IsOnShelfResponse(bool OnShelf);

// Server fan-out
public record ServerCreationRequestDto(Guid OrganizationId, Guid ServerProfileId,
    string Hostname, Guid? BuildingId = null, Guid? RoomId = null, Guid? RackId = null,
    Guid? AsnBlockId = null, Guid? LoopbackSubnetId = null, Guid? NicSubnetId = null,
    string? SideAHostname = null, string? SideBHostname = null,
    string? DisplayName = null, int? UserId = null);

public record ServerCreationResultDto(ServerRowDto Server, List<ServerNicRowDto> Nics,
    AsnAllocationDto? AsnAllocation, IpAddressDto? LoopbackIp);

public record ServerRowDto(Guid Id, Guid OrganizationId, Guid ServerProfileId,
    Guid? BuildingId, string Hostname, string? DisplayName, DateTime CreatedAt);

public record ServerNicRowDto(Guid Id, Guid ServerId, int NicIndex, Guid? TargetDeviceId,
    Guid? IpAddressId, Guid? SubnetId, string MlagSide, bool AdminUp);

// Naming
public record LinkNamingContextDto(string? SiteA = null, string? SiteB = null,
    string? DeviceA = null, string? DeviceB = null, string? PortA = null, string? PortB = null,
    string? RoleA = null, string? RoleB = null, int? VlanId = null, string? Subnet = null,
    string? Description = null, string? LinkCode = null);

public record DeviceNamingContextDto(string? RegionCode = null, string? SiteCode = null,
    string? BuildingCode = null, string? RackCode = null, string? RoleCode = null,
    int? Instance = null, int InstancePadding = 2);

public record ServerNamingContextDto(string? RegionCode = null, string? SiteCode = null,
    string? BuildingCode = null, string? RackCode = null, string? ProfileCode = null,
    int? Instance = null, int InstancePadding = 2);

public record NamePreviewResponse(string Expanded);

public record ResolveTemplateRequest(Guid OrganizationId, string EntityType,
    string? SubtypeCode = null, Guid? RegionId = null, Guid? SiteId = null,
    Guid? BuildingId = null, string? DefaultTemplate = null);

public record ResolveTemplateResponse(string Template, string Source, Guid? OverrideId);

public record RegeneratePreviewRequest(Guid OrganizationId, string EntityType,
    string? SubtypeCode = null, string? ScopeLevel = null, Guid? ScopeEntityId = null);

public record RegeneratePreviewResponse(List<RegenerateItemDto> Items, int Total, int WouldChange);

public record RegenerateApplyRequest(Guid OrganizationId, string EntityType,
    string? SubtypeCode = null, string? ScopeLevel = null, Guid? ScopeEntityId = null,
    List<Guid>? OnlyIds = null);

public record RegenerateApplyResponse(List<RegenerateItemDto> Renamed,
    List<RegenerateItemDto> Skipped, List<RegenerateApplyFailureDto> Failed,
    int RenamedCount, int FailedCount);

public record RegenerateItemDto(Guid Id, string? SubtypeCode, string CurrentName,
    string ProposedName, bool WouldChange, string TemplateSource);

public record RegenerateApplyFailureDto(Guid Id, string CurrentName, string ProposedName, string Error);

// Change Sets
public record CreateChangeSetRequest(Guid OrganizationId, string Title,
    string? Description = null, string? RequestedByDisplay = null);

public record AddChangeSetItemRequest(string EntityType, Guid? EntityId, string Action,
    object? BeforeJson = null, object? AfterJson = null, int? ExpectedVersion = null,
    string? Notes = null);

public record ChangeSetDto(Guid Id, Guid OrganizationId, string Title, string? Description,
    string Status, int? RequestedBy, string? RequestedByDisplay, int? SubmittedBy,
    DateTime? SubmittedAt, DateTime? ApprovedAt, DateTime? AppliedAt,
    DateTime? RolledBackAt, DateTime? CancelledAt, int? RequiredApprovals,
    Guid CorrelationId, int Version, long ItemCount, DateTime CreatedAt, DateTime UpdatedAt);

public record ChangeSetDetailDto
{
    public Guid Id { get; init; }
    public Guid OrganizationId { get; init; }
    public string Title { get; init; } = "";
    public string? Description { get; init; }
    public string Status { get; init; } = "";
    public int? RequestedBy { get; init; }
    public string? RequestedByDisplay { get; init; }
    public int? SubmittedBy { get; init; }
    public DateTime? SubmittedAt { get; init; }
    public DateTime? ApprovedAt { get; init; }
    public DateTime? AppliedAt { get; init; }
    public DateTime? RolledBackAt { get; init; }
    public DateTime? CancelledAt { get; init; }
    public int? RequiredApprovals { get; init; }
    public Guid CorrelationId { get; init; }
    public int Version { get; init; }
    public long ItemCount { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public List<ChangeSetItemDto> Items { get; init; } = new();
}

public record ChangeSetItemDto(Guid Id, Guid ChangeSetId, int ItemOrder, string EntityType,
    Guid? EntityId, string Action, object? BeforeJson, object? AfterJson,
    int? ExpectedVersion, DateTime? AppliedAt, string? ApplyError, string? Notes,
    DateTime CreatedAt);

public record DecisionResultDto(ApprovalDto Approval, ChangeSetDto ChangeSet,
    long ApprovalsCount, int ApprovalsRequired);

public record ApprovalDto(Guid Id, Guid ChangeSetId, int ApproverUserId,
    string? ApproverDisplay, string Decision, DateTime DecidedAt, string? Notes);

public record ApplyResultDto(ChangeSetDto ChangeSet, List<ApplyItemOutcomeDto> Outcomes,
    int AppliedCount, int FailedCount);

public record ApplyItemOutcomeDto(Guid ItemId, int ItemOrder, bool Success,
    string? Error, bool Skipped);

public record RollbackResultDto(ChangeSetDto ChangeSet, List<RollbackItemOutcomeDto> Outcomes,
    int RevertedCount, int FailedCount);

public record RollbackItemOutcomeDto(Guid ItemId, int ItemOrder, bool Success,
    string? Error, bool Skipped);

// Audit
public class ListAuditRequest
{
    public Guid OrganizationId { get; set; }
    public string? EntityType { get; set; }
    public Guid? EntityId { get; set; }
    public string? Action { get; set; }
    public int? ActorUserId { get; set; }
    public Guid? CorrelationId { get; set; }
    public DateTime? FromAt { get; set; }
    public DateTime? ToAt { get; set; }
    public int? Limit { get; set; }
}

public record AuditRowDto(Guid Id, Guid OrganizationId, long SequenceId, string SourceService,
    string EntityType, Guid? EntityId, string Action, int? ActorUserId, string? ActorDisplay,
    Guid? CorrelationId, object? Details, string? PrevHash, string EntryHash,
    DateTime CreatedAt);

public record VerifyChainResponse(Guid OrganizationId, long RowsChecked,
    long? FirstSequenceId, long? LastSequenceId, bool Ok, List<VerifyMismatchDto> Mismatches);

public record VerifyMismatchDto(long SequenceId, Guid Id, string Reason,
    string? ExpectedHash, string StoredHash);

// Validation
public record ResolvedRuleDto(string Code, string Name, string Description, string Category,
    string DefaultSeverity, bool DefaultEnabled, string EffectiveSeverity,
    bool EffectiveEnabled, bool HasTenantOverride);

public record ValidationRunResultDto(List<ViolationDto> Violations, int RulesRun,
    int RulesWithFindings, int TotalViolations);

public record ViolationDto(string RuleCode, string Severity, string EntityType,
    Guid? EntityId, string Message);

// Locks
public record LockChangeResultDto(Guid Id, string LockState, string? LockReason,
    int? LockedBy, DateTime? LockedAt, int Version);

public record LockedRowDto(Guid Id, string TableName, string DisplayLabel,
    string LockState, string? LockReason, int? LockedBy, DateTime? LockedAt,
    int Version);

// Device list (picker)
public record DeviceListRowDto(Guid Id, string Hostname, string? RoleCode,
    string? BuildingCode, string Status, int Version);

// VLAN block list (picker)
public record VlanBlockDto(Guid Id, string BlockCode, string DisplayName,
    int VlanFirst, int VlanLast, long Available);
