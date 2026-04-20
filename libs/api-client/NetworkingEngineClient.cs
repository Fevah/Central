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

    /// <summary>Read-only dry-run of the subnet carver. Shows the
    /// CIDR that <see cref="AllocateSubnetAsync"/> would produce
    /// next for <paramref name="poolId"/> at the given prefix
    /// length, without inserting a row or taking the pool lock.
    /// 404 → pool not found; 409 → prefix out of range or pool
    /// exhausted (caller should inspect the message).</summary>
    public Task<CarvePreviewDto> PreviewSubnetCarveAsync(Guid poolId,
        Guid organizationId, int prefixLength, CancellationToken ct = default)
        => PostAsync<CarvePreviewDto>("/api/net/allocate/subnet/preview",
            new { poolId, organizationId, prefixLength }, ct);

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

    /// <summary>Soft-delete a pending item from a Draft change-set.
    /// 400 when the parent Set isn't Draft; 404 when the set or
    /// item is missing. Audits "ItemRemoved" with the Set's
    /// correlation_id.</summary>
    public async Task DeleteChangeSetItemAsync(Guid setId, Guid itemId,
        Guid organizationId, CancellationToken ct = default)
    {
        var url = $"/api/net/change-sets/{setId}/items/{itemId}?organizationId={organizationId}";
        var resp = await _http.DeleteAsync(url, ct);
        await EnsureSuccessAsync(resp, ct);
    }

    /// <summary>Convenience: full audit timeline for a change-set.
    /// Chains <see cref="GetChangeSetAsync"/> (to resolve the set's
    /// correlation_id) with <see cref="ListAuditAsync"/> narrowed
    /// to that correlation id. Captures both the set's own
    /// lifecycle events AND any child-entity audits stamped during
    /// apply time (rename / carve / grant-change) that share the
    /// same correlation id. Mirrors the web change-set detail
    /// Timeline section.</summary>
    public async Task<List<AuditRowDto>> ListChangeSetTimelineAsync(Guid setId,
        Guid organizationId, int limit = 500, CancellationToken ct = default)
    {
        var detail = await GetChangeSetAsync(setId, organizationId, ct);
        return await ListAuditAsync(new ListAuditRequest
        {
            OrganizationId = organizationId,
            CorrelationId  = detail.CorrelationId,
            Limit          = limit,
        }, ct);
    }

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

    /// <summary>Thin VLAN list — same purpose as ListDevicesAsync
    /// (WPF picker + hostname/code → uuid resolution). Capped at
    /// 5000 rows server-side; tenants beyond that should use the
    /// search endpoint for narrowing.</summary>
    public Task<List<VlanListRowDto>> ListVlansAsync(Guid organizationId,
        CancellationToken ct = default)
        => GetAsync<List<VlanListRowDto>>(
            $"/api/net/vlans?organizationId={organizationId}", ct);

    /// <summary>Thin link list — same pattern as devices + vlans.
    /// Carries link_code + resolved link_type + the two endpoint
    /// hostnames (A at endpoint_order 0, B at 1) for picker display
    /// without a second round-trip. Used by the WPF P2P/B2B/FW
    /// grids' audit-drill handlers to resolve link_code → net.link
    /// uuid.</summary>
    public Task<List<LinkListRowDto>> ListLinksAsync(Guid organizationId,
        CancellationToken ct = default)
        => GetAsync<List<LinkListRowDto>>(
            $"/api/net/links?organizationId={organizationId}", ct);

    /// <summary>Thin server list — hostname + profileCode +
    /// buildingCode resolved via LEFT JOIN on the engine side.
    /// Same 5000-row cap as the other thin lists.</summary>
    public Task<List<ServerListRowDto>> ListServersAsync(Guid organizationId,
        CancellationToken ct = default)
        => GetAsync<List<ServerListRowDto>>(
            $"/api/net/servers?organizationId={organizationId}", ct);

    /// <summary>Thin subnet list — subnet_code + CIDR + pool_code +
    /// linked vlan tag. network rendered as a CIDR string on the
    /// server so the wire shape is stable across sqlx feature flags.
    /// Optional <paramref name="poolId"/> narrows the result to one
    /// IP pool (backs the pool-detail Subnets tab).</summary>
    public Task<List<SubnetListRowDto>> ListSubnetsAsync(Guid organizationId,
        Guid? poolId = null, CancellationToken ct = default)
    {
        var qs = BuildQuery(
            ("organizationId", organizationId.ToString()),
            ("poolId",         poolId?.ToString()));
        return GetAsync<List<SubnetListRowDto>>($"/api/net/subnets{qs}", ct);
    }

    /// <summary>List VLAN blocks + per-block availability. Powers the
    /// WPF Create VLAN picker so admins see "VLAN 100-199 · 12 free"
    /// instead of a UUID they'd have to copy from another tool.</summary>
    public Task<List<VlanBlockDto>> ListVlanBlocksAsync(Guid organizationId,
        CancellationToken ct = default)
        => GetAsync<List<VlanBlockDto>>(
            $"/api/net/vlan-blocks?organizationId={organizationId}", ct);

    public Task<List<AsnBlockDto>> ListAsnBlocksAsync(Guid organizationId,
        CancellationToken ct = default)
        => GetAsync<List<AsnBlockDto>>(
            $"/api/net/asn-blocks?organizationId={organizationId}", ct);

    public Task<List<MlagPoolDto>> ListMlagPoolsAsync(Guid organizationId,
        CancellationToken ct = default)
        => GetAsync<List<MlagPoolDto>>(
            $"/api/net/mlag-pools?organizationId={organizationId}", ct);

    public Task<List<IpPoolDto>> ListIpPoolsAsync(Guid organizationId,
        CancellationToken ct = default)
        => GetAsync<List<IpPoolDto>>(
            $"/api/net/ip-pools?organizationId={organizationId}", ct);

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

    /// <summary>Convenience: fetch the current caller's recent audit
    /// activity. Chains <see cref="WhoAmIAsync"/> (to resolve the
    /// user id from the X-User-Id header) with
    /// <see cref="ListAuditAsync"/>. Returns an empty list when the
    /// caller is service-origin (no X-User-Id header). Pass
    /// <paramref name="fromAt"/> as an ISO-8601 datetime to window
    /// the result.</summary>
    public async Task<List<AuditRowDto>> ListMyActivityAsync(Guid organizationId,
        DateTime? fromAt = null, int limit = 100, CancellationToken ct = default)
    {
        var me = await WhoAmIAsync(organizationId, ct);
        if (me.UserId is null) return new List<AuditRowDto>();
        return await ListAuditAsync(new ListAuditRequest
        {
            OrganizationId = organizationId,
            ActorUserId    = me.UserId.Value,
            FromAt         = fromAt,
            Limit          = limit,
        }, ct);
    }

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

    /// <summary>Per-entity-type audit activity summary — COUNT +
    /// COUNT(DISTINCT actor) + MAX(created_at) grouped by entity type.
    /// Optional fromAt/toAt window.</summary>
    public Task<List<EntityTypeStatsDto>> AuditStatsAsync(Guid organizationId,
        DateTime? fromAt = null, DateTime? toAt = null,
        string? entityTypes = null, CancellationToken ct = default)
    {
        var qs = BuildQuery(("organizationId", organizationId.ToString()),
                            ("fromAt", fromAt?.ToString("o")),
                            ("toAt", toAt?.ToString("o")),
                            ("entityTypes", entityTypes));
        return GetAsync<List<EntityTypeStatsDto>>($"/api/net/audit/stats{qs}", ct);
    }

    /// <summary>Time-bucketed audit count series. `bucketBy` accepts
    /// hour / day / week (default day). Optional entityType narrower.</summary>
    public Task<List<AuditTrendPointDto>> AuditTrendAsync(Guid organizationId,
        DateTime? fromAt = null, DateTime? toAt = null, string? bucketBy = null,
        string? entityType = null, CancellationToken ct = default)
    {
        var qs = BuildQuery(("organizationId", organizationId.ToString()),
                            ("fromAt", fromAt?.ToString("o")),
                            ("toAt", toAt?.ToString("o")),
                            ("bucketBy", bucketBy),
                            ("entityType", entityType));
        return GetAsync<List<AuditTrendPointDto>>($"/api/net/audit/trend{qs}", ct);
    }

    /// <summary>Top N audit actors by count in the window. Clamped
    /// 1..=100 server-side, default 20. Service-origin rows (null
    /// actor_user_id) bucket together rather than being dropped.</summary>
    /// <summary>Recent distinct correlation ids across the tenant —
    /// "what bulk operations happened lately?" at a glance. Pairs
    /// with the web /network/correlations page; each row summarises
    /// a correlation_id with entry count + distinct entity types +
    /// first/last-seen + optional change-set metadata (null when
    /// the correlation doesn't have a wrapper set — ad-hoc allocation
    /// retires, bulk edits, etc.). Ordered by lastSeenAt DESC.
    /// </summary>
    public Task<List<RecentCorrelationDto>> AuditCorrelationsAsync(Guid organizationId,
        int? limit = null, DateTime? fromAt = null, DateTime? toAt = null,
        CancellationToken ct = default)
    {
        var qs = BuildQuery(("organizationId", organizationId.ToString()),
                            ("limit", limit?.ToString()),
                            ("fromAt", fromAt?.ToString("o")),
                            ("toAt", toAt?.ToString("o")));
        return GetAsync<List<RecentCorrelationDto>>($"/api/net/audit/correlations{qs}", ct);
    }

    /// <summary>Distinct audit-action catalog for the tenant. One
    /// row per action string seen in the log, ordered by last-seen
    /// DESC. Drives UI action-filter dropdowns + populates the
    /// "what actions fire on Device?" narrow view (pass the
    /// <paramref name="entityType"/> filter). Default limit 100,
    /// clamped server-side to 1..=500.</summary>
    public Task<List<DistinctActionDto>> AuditActionsAsync(Guid organizationId,
        string? entityType = null, int? limit = null, CancellationToken ct = default)
    {
        var qs = BuildQuery(("organizationId", organizationId.ToString()),
                            ("entityType", entityType),
                            ("limit", limit?.ToString()));
        return GetAsync<List<DistinctActionDto>>($"/api/net/audit/actions{qs}", ct);
    }

    public Task<List<TopActorDto>> AuditTopActorsAsync(Guid organizationId,
        DateTime? fromAt = null, DateTime? toAt = null, string? entityType = null,
        int? limit = null, CancellationToken ct = default)
    {
        var qs = BuildQuery(("organizationId", organizationId.ToString()),
                            ("fromAt", fromAt?.ToString("o")),
                            ("toAt", toAt?.ToString("o")),
                            ("entityType", entityType),
                            ("limit", limit?.ToString()));
        return GetAsync<List<TopActorDto>>($"/api/net/audit/top-actors{qs}", ct);
    }

    // ─── Search facets (Phase 10b) ───────────────────────────────────────

    /// <summary>Per-entity-type hit counts for a search query. UNION-
    /// ALL across the six searchable tables in one round trip so the
    /// UI can render a "Device(12) · Vlan(4)" narrowing bar without
    /// running the full ranked search first.</summary>
    public Task<List<SearchFacetDto>> SearchFacetsAsync(Guid organizationId,
        string q, string? entityTypes = null, CancellationToken ct = default)
    {
        var qs = BuildQuery(("organizationId", organizationId.ToString()),
                            ("q", q),
                            ("entityTypes", entityTypes));
        return GetAsync<List<SearchFacetDto>>($"/api/net/search/facets{qs}", ct);
    }

    // ─── Pool utilization (Phase 10b) ────────────────────────────────────

    /// <summary>Per-pool used vs capacity across ASN / VLAN / IP pool
    /// families. IP pools emit two rows ("IP:Subnets" + "IP:Addresses")
    /// so both dimensions surface without a second call.</summary>
    public Task<List<PoolUtilizationRowDto>> PoolUtilizationAsync(Guid organizationId,
        CancellationToken ct = default)
        => GetAsync<List<PoolUtilizationRowDto>>(
            $"/api/net/pools/utilization?organizationId={organizationId}", ct);

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

    // ─── CLI flavors + Config Generation (Phase 10) ──────────────────────

    /// <summary>List the multi-vendor CLI flavor catalog with this tenant's
    /// per-flavor enable + is_default state. Used by the admin flavor picker
    /// panel — PicOS is Ga today, the rest are stubs.</summary>
    public Task<List<CliFlavorConfigDto>> ListCliFlavorsAsync(Guid organizationId,
        CancellationToken ct = default)
        => GetAsync<List<CliFlavorConfigDto>>(
            $"/api/net/cli-flavors?organizationId={organizationId}", ct);

    /// <summary>Upsert the tenant's config for one flavor. Setting
    /// <paramref name="isDefault"/> to true clears the flag on any other
    /// row for the same tenant (partial-unique index enforces one
    /// default per tenant).</summary>
    public async Task SetCliFlavorConfigAsync(Guid organizationId, string flavorCode,
        bool? enabled = null, bool? isDefault = null, string? notes = null,
        CancellationToken ct = default)
    {
        var resp = await _http.PutAsJsonAsync(
            $"/api/net/cli-flavors/{Uri.EscapeDataString(flavorCode)}?organizationId={organizationId}",
            new { enabled, isDefault, notes }, Json, ct);
        await EnsureSuccessAsync(resp, ct);
    }

    /// <summary>Render + persist one device's config. Writes a new row
    /// to net.rendered_config chained to the previous render; returns
    /// the RenderedConfigDto with id + previousRenderId populated.</summary>
    public Task<RenderedConfigDto> RenderDeviceConfigAsync(Guid deviceId,
        Guid organizationId, CancellationToken ct = default)
        => PostAsync<RenderedConfigDto>(
            $"/api/net/devices/{deviceId}/render-config?organizationId={organizationId}",
            new { }, ct);

    /// <summary>Recent renders for a device — body NOT included, summaries
    /// only. Limit clamps to [1, 500] server-side (default 50).</summary>
    public Task<List<RenderedConfigSummaryDto>> ListDeviceRendersAsync(Guid deviceId,
        Guid organizationId, int? limit = null, CancellationToken ct = default)
    {
        var qs = BuildQuery(("organizationId", organizationId.ToString()),
                            ("limit", limit?.ToString()));
        return GetAsync<List<RenderedConfigSummaryDto>>(
            $"/api/net/devices/{deviceId}/renders{qs}", ct);
    }

    /// <summary>Fetch one render by id with full body — for the diff /
    /// view-one flow.</summary>
    public Task<RenderedConfigRecordDto> GetRenderAsync(Guid renderId,
        Guid organizationId, CancellationToken ct = default)
        => GetAsync<RenderedConfigRecordDto>(
            $"/api/net/renders/{renderId}?organizationId={organizationId}", ct);

    /// <summary>"What changed since last render" — added/removed line
    /// lists against the previous_render_id chain entry. First-ever
    /// render returns the whole body as added with zero removed.</summary>
    public Task<RenderDiffDto> DiffRenderAsync(Guid renderId, Guid organizationId,
        CancellationToken ct = default)
        => GetAsync<RenderDiffDto>(
            $"/api/net/renders/{renderId}/diff?organizationId={organizationId}", ct);

    /// <summary>Turn-up pack: render + persist every device in a building.
    /// Per-device errors are tolerated — one broken naming template can't
    /// block the other N-1 devices.</summary>
    public Task<BuildingRenderResultDto> RenderBuildingConfigsAsync(Guid buildingId,
        Guid organizationId, CancellationToken ct = default)
        => PostAsync<BuildingRenderResultDto>(
            $"/api/net/buildings/{buildingId}/render-configs?organizationId={organizationId}",
            new { }, ct);

    /// <summary>Site-level turn-up pack. Counters roll up across
    /// buildings; devices ordered (building_code, hostname).</summary>
    public Task<SiteRenderResultDto> RenderSiteConfigsAsync(Guid siteId,
        Guid organizationId, CancellationToken ct = default)
        => PostAsync<SiteRenderResultDto>(
            $"/api/net/sites/{siteId}/render-configs?organizationId={organizationId}",
            new { }, ct);

    /// <summary>Whole-estate render — every device across every site.
    /// For single-region tenants this is the greenfield deployment
    /// call.</summary>
    public Task<RegionRenderResultDto> RenderRegionConfigsAsync(Guid regionId,
        Guid organizationId, CancellationToken ct = default)
        => PostAsync<RegionRenderResultDto>(
            $"/api/net/regions/{regionId}/render-configs?organizationId={organizationId}",
            new { }, ct);

    // ─── DHCP relay targets (Phase 10) ───────────────────────────────────

    public Task<List<DhcpRelayTargetDto>> ListDhcpRelayTargetsAsync(Guid organizationId,
        Guid? vlanId = null, CancellationToken ct = default)
    {
        var qs = BuildQuery(("organizationId", organizationId.ToString()),
                            ("vlanId", vlanId?.ToString()));
        return GetAsync<List<DhcpRelayTargetDto>>($"/api/net/dhcp-relay-targets{qs}", ct);
    }

    public Task<DhcpRelayTargetDto> GetDhcpRelayTargetAsync(Guid id, Guid organizationId,
        CancellationToken ct = default)
        => GetAsync<DhcpRelayTargetDto>(
            $"/api/net/dhcp-relay-targets/{id}?organizationId={organizationId}", ct);

    public Task<DhcpRelayTargetDto> CreateDhcpRelayTargetAsync(
        CreateDhcpRelayTargetRequest request, CancellationToken ct = default)
        => PostAsync<DhcpRelayTargetDto>("/api/net/dhcp-relay-targets", request, ct);

    public async Task<DhcpRelayTargetDto> UpdateDhcpRelayTargetAsync(Guid id,
        Guid organizationId, int priority, Guid? ipAddressId, string? notes, int version,
        CancellationToken ct = default)
    {
        var resp = await _http.PutAsJsonAsync(
            $"/api/net/dhcp-relay-targets/{id}?organizationId={organizationId}",
            new { priority, ipAddressId, notes, version }, Json, ct);
        await EnsureSuccessAsync(resp, ct);
        return (await resp.Content.ReadFromJsonAsync<DhcpRelayTargetDto>(Json, ct))
            ?? throw new NetworkingEngineException(0, "PUT returned null body");
    }

    public async Task DeleteDhcpRelayTargetAsync(Guid id, Guid organizationId,
        CancellationToken ct = default)
    {
        var resp = await _http.DeleteAsync(
            $"/api/net/dhcp-relay-targets/{id}?organizationId={organizationId}", ct);
        await EnsureSuccessAsync(resp, ct);
    }

    // ─── Bulk export + import (Phase 10) ─────────────────────────────────

    /// <summary>Download a CSV dump of the tenant's devices. Returned as
    /// the raw body string (text/csv) so callers can save-as-file or
    /// parse directly.</summary>
    public Task<string> ExportDevicesCsvAsync(Guid organizationId, CancellationToken ct = default)
        => GetStringAsync($"/api/net/devices/export?organizationId={organizationId}", ct);

    public Task<string> ExportVlansCsvAsync(Guid organizationId, CancellationToken ct = default)
        => GetStringAsync($"/api/net/vlans/export?organizationId={organizationId}", ct);

    public Task<string> ExportIpAddressesCsvAsync(Guid organizationId, CancellationToken ct = default)
        => GetStringAsync($"/api/net/ip-addresses/export?organizationId={organizationId}", ct);

    public Task<string> ExportLinksCsvAsync(Guid organizationId, CancellationToken ct = default)
        => GetStringAsync($"/api/net/links/export?organizationId={organizationId}", ct);

    public Task<string> ExportServersCsvAsync(Guid organizationId, CancellationToken ct = default)
        => GetStringAsync($"/api/net/servers/export?organizationId={organizationId}", ct);

    public Task<string> ExportSubnetsCsvAsync(Guid organizationId, CancellationToken ct = default)
        => GetStringAsync($"/api/net/subnets/export?organizationId={organizationId}", ct);

    public Task<string> ExportAsnAllocationsCsvAsync(Guid organizationId, CancellationToken ct = default)
        => GetStringAsync($"/api/net/asn-allocations/export?organizationId={organizationId}", ct);

    public Task<string> ExportMlagDomainsCsvAsync(Guid organizationId, CancellationToken ct = default)
        => GetStringAsync($"/api/net/mlag-domains/export?organizationId={organizationId}", ct);

    public Task<string> ExportDhcpRelayTargetsCsvAsync(Guid organizationId, CancellationToken ct = default)
        => GetStringAsync($"/api/net/dhcp-relay-targets/export?organizationId={organizationId}", ct);

    /// <summary>Validate or apply a CSV bulk import of devices.
    /// <paramref name="dryRun"/>=true runs per-row validation only
    /// and returns the structured outcome without writing;
    /// <paramref name="dryRun"/>=false applies via a single
    /// transaction — the whole import rolls back on any row-level
    /// failure.
    ///
    /// <paramref name="mode"/> = "create" (default) rejects
    /// existing hostnames as per-row errors; "upsert" updates
    /// existing rows via version-checked UPDATE and inserts new
    /// hostnames the same way create does. Pass null/unset for
    /// create-only semantics (matches the original import
    /// contract).</summary>
    public Task<ImportValidationResultDto> ImportDevicesCsvAsync(Guid organizationId,
        string csvBody, bool dryRun = true, string? mode = null, CancellationToken ct = default)
        => PostCsvAsync("/api/net/devices/import", organizationId, csvBody, dryRun, mode, ct);

    /// <summary>Bulk import VLANs. Required columns match what
    /// <see cref="ExportVlansCsvAsync"/> emits (vlan_id, display_name,
    /// description, scope_level, template_code, block_code, status);
    /// `block_code` must resolve to an existing net.vlan_block row.
    ///
    /// <paramref name="mode"/>="upsert" updates the existing
    /// (block_code, vlan_id) pair via version-checked UPDATE instead
    /// of rejecting it; new pairs INSERT either way. VLAN CSVs don't
    /// carry a version column, so upsert applies against current DB
    /// version (no client-side concurrency snapshot).</summary>
    public Task<ImportValidationResultDto> ImportVlansCsvAsync(Guid organizationId,
        string csvBody, bool dryRun = true, string? mode = null, CancellationToken ct = default)
        => PostCsvAsync("/api/net/vlans/import", organizationId, csvBody, dryRun, mode, ct);

    /// <summary>Bulk import subnets. `pool_code` is required and must
    /// resolve; the `vlan_id` column is accepted but ignored on apply
    /// (operators link via the CRUD panel — numeric vlan_id can be
    /// ambiguous across multiple blocks).
    ///
    /// <paramref name="mode"/>="upsert" updates the existing
    /// `subnet_code` via version-checked UPDATE instead of rejecting
    /// it; new codes INSERT either way.</summary>
    public Task<ImportValidationResultDto> ImportSubnetsCsvAsync(Guid organizationId,
        string csvBody, bool dryRun = true, string? mode = null, CancellationToken ct = default)
        => PostCsvAsync("/api/net/subnets/import", organizationId, csvBody, dryRun, mode, ct);

    /// <summary>Bulk import servers. Required: hostname; optional:
    /// profile_code, building_code, management_ip, status. ASN +
    /// loopback + NIC count are accepted-for-reference-but-ignored-
    /// on-apply (same semantic as ASN on the device importer —
    /// operators wire them up via the allocation service or CRUD).
    ///
    /// <paramref name="mode"/>="upsert" updates the existing hostname
    /// via version-checked UPDATE; new hostnames INSERT either way.</summary>
    public Task<ImportValidationResultDto> ImportServersCsvAsync(Guid organizationId,
        string csvBody, bool dryRun = true, string? mode = null, CancellationToken ct = default)
        => PostCsvAsync("/api/net/servers/import", organizationId, csvBody, dryRun, mode, ct);

    /// <summary>Bulk import links. One CSV row → 1 net.link +
    /// 2 net.link_endpoint rows in a single transaction. Required:
    /// link_code, link_type (must resolve), device_a, device_b
    /// (hostnames must exist). Optional: vlan_id, subnet_code,
    /// port_a, port_b, status. ip_a + ip_b are accepted but
    /// ignored on apply — resolving them to net.ip_address rows
    /// needs subnet context the import flow doesn't carry; operators
    /// wire IPs via the IP-allocation CRUD after the link import
    /// lands.
    ///
    /// <paramref name="mode"/>="upsert" updates the existing link_code
    /// (link_type, vlan, subnet, status) AND rewrites both endpoints
    /// from the CSV row in the same transaction — port_a / port_b on
    /// the upsert row are the source of truth, not the previous
    /// values. New link_codes INSERT either way.</summary>
    public Task<ImportValidationResultDto> ImportLinksCsvAsync(Guid organizationId,
        string csvBody, bool dryRun = true, string? mode = null, CancellationToken ct = default)
        => PostCsvAsync("/api/net/links/import", organizationId, csvBody, dryRun, mode, ct);

    /// <summary>Bulk import DHCP relay targets. Required: vlan_id
    /// (must exist in the tenant's VLAN catalog), server_ip. First-
    /// wins when multiple blocks have the same numeric vlan_id —
    /// operators in multi-block tenants should use the CRUD panel
    /// for fine-grained control until a block-qualifier column
    /// lands.
    ///
    /// <paramref name="mode"/>="upsert" updates the existing
    /// (vlan_id, server_ip) pair (priority + notes) via version-
    /// checked UPDATE; new pairs INSERT either way.</summary>
    public Task<ImportValidationResultDto> ImportDhcpRelayTargetsCsvAsync(Guid organizationId,
        string csvBody, bool dryRun = true, string? mode = null, CancellationToken ct = default)
        => PostCsvAsync("/api/net/dhcp-relay-targets/import", organizationId, csvBody, dryRun, mode, ct);

    /// <summary>Shared transport for every `*/import` POST — keeps
    /// the entity-specific helpers to one line each. `mode` is
    /// forwarded on the query string; unset means server default
    /// (today: create).</summary>
    private async Task<ImportValidationResultDto> PostCsvAsync(string path,
        Guid organizationId, string csvBody, bool dryRun,
        string? mode = null, CancellationToken ct = default)
    {
        var qs = BuildQuery(
            ("organizationId", organizationId.ToString()),
            ("dryRun",         dryRun ? "true" : "false"),
            ("mode",           mode));
        var url = $"{path}{qs}";
        var content = new StringContent(csvBody, System.Text.Encoding.UTF8, "text/csv");
        var resp = await _http.PostAsync(url, content, ct);
        await EnsureSuccessAsync(resp, ct);
        return (await resp.Content.ReadFromJsonAsync<ImportValidationResultDto>(Json, ct))
            ?? throw new NetworkingEngineException(0, $"POST {path} returned null body");
    }

    // ─── Scope grants (Phase 10 RBAC foundation) ─────────────────────────

    /// <summary>List scope grants in the tenant, optionally narrowed
    /// by userId / action / entityType. Engine is CRUD-ready today;
    /// per-endpoint enforcement lands in follow-on slices.</summary>
    public Task<List<ScopeGrantDto>> ListScopeGrantsAsync(Guid organizationId,
        int? userId = null, string? action = null, string? entityType = null,
        CancellationToken ct = default)
    {
        var qs = BuildQuery(("organizationId", organizationId.ToString()),
                            ("userId", userId?.ToString()),
                            ("action", action),
                            ("entityType", entityType));
        return GetAsync<List<ScopeGrantDto>>($"/api/net/scope-grants{qs}", ct);
    }

    public Task<ScopeGrantDto> GetScopeGrantAsync(Guid id, Guid organizationId,
        CancellationToken ct = default)
        => GetAsync<ScopeGrantDto>(
            $"/api/net/scope-grants/{id}?organizationId={organizationId}", ct);

    public Task<ScopeGrantDto> CreateScopeGrantAsync(CreateScopeGrantRequest request,
        CancellationToken ct = default)
        => PostAsync<ScopeGrantDto>("/api/net/scope-grants", request, ct);

    public async Task DeleteScopeGrantAsync(Guid id, Guid organizationId,
        CancellationToken ct = default)
    {
        var resp = await _http.DeleteAsync(
            $"/api/net/scope-grants/{id}?organizationId={organizationId}", ct);
        await EnsureSuccessAsync(resp, ct);
    }

    /// <summary>Dry-run the permission resolver without enforcing —
    /// useful for UI feedback ("save would be denied") and for
    /// verifying a newly-created grant actually lets the right user
    /// do the right thing.</summary>
    public Task<PermissionDecisionDto> CheckPermissionAsync(Guid organizationId,
        int userId, string action, string entityType, Guid? entityId = null,
        CancellationToken ct = default)
    {
        var qs = BuildQuery(("organizationId", organizationId.ToString()),
                            ("userId", userId.ToString()),
                            ("action", action),
                            ("entityType", entityType),
                            ("entityId", entityId?.ToString()));
        return GetAsync<PermissionDecisionDto>($"/api/net/scope-grants/check{qs}", ct);
    }

    /// <summary>Bulk edit — apply the same field/value change to a set
    /// of devices in one transaction. Whitelisted fields: status,
    /// role_code, building_code, management_ip, notes. Any per-row
    /// failure rolls back the whole batch; dryRun=true previews.
    /// </summary>
    public Task<BulkEditResultDto> BulkEditDevicesAsync(Guid organizationId,
        IReadOnlyList<Guid> deviceIds, string field, string value,
        bool dryRun = true, CancellationToken ct = default)
        => BulkEditAsync("/api/net/devices/bulk-edit", organizationId,
            new { deviceIds, field, value }, dryRun, ct);

    /// <summary>Bulk edit VLANs. Whitelisted fields: display_name,
    /// description, scope_level, status, template_code, notes.
    /// template_code resolves to net.vlan_template.id; non-existent
    /// codes return 400 before any write.</summary>
    public Task<BulkEditResultDto> BulkEditVlansAsync(Guid organizationId,
        IReadOnlyList<Guid> vlanIds, string field, string value,
        bool dryRun = true, CancellationToken ct = default)
        => BulkEditAsync("/api/net/vlans/bulk-edit", organizationId,
            new { vlanIds, field, value }, dryRun, ct);

    /// <summary>Bulk edit subnets. Whitelisted fields: display_name,
    /// scope_level, status, notes. Network + pool_id + vlan_id stay
    /// gated behind single-row CRUD — bulk-editing a subnet's CIDR at
    /// scale is rarely what operators actually want.</summary>
    public Task<BulkEditResultDto> BulkEditSubnetsAsync(Guid organizationId,
        IReadOnlyList<Guid> subnetIds, string field, string value,
        bool dryRun = true, CancellationToken ct = default)
        => BulkEditAsync("/api/net/subnets/bulk-edit", organizationId,
            new { subnetIds, field, value }, dryRun, ct);

    /// <summary>Bulk edit servers. Whitelisted fields: profile_code,
    /// building_code, management_ip, status, notes. hostname stays
    /// gated behind single-row CRUD; ASN / loopback /
    /// server_profile_id are mutated via the allocation service
    /// not here.</summary>
    public Task<BulkEditResultDto> BulkEditServersAsync(Guid organizationId,
        IReadOnlyList<Guid> serverIds, string field, string value,
        bool dryRun = true, CancellationToken ct = default)
        => BulkEditAsync("/api/net/servers/bulk-edit", organizationId,
            new { serverIds, field, value }, dryRun, ct);

    /// <summary>Bulk edit DHCP relay targets. Whitelisted fields:
    /// priority, status, notes. vlan_id + server_ip stay gated —
    /// changing the identity of a relay at scale makes no sense.</summary>
    public Task<BulkEditResultDto> BulkEditDhcpRelayTargetsAsync(Guid organizationId,
        IReadOnlyList<Guid> targetIds, string field, string value,
        bool dryRun = true, CancellationToken ct = default)
        => BulkEditAsync("/api/net/dhcp-relay-targets/bulk-edit", organizationId,
            new { targetIds, field, value }, dryRun, ct);

    /// <summary>Shared POST helper for every bulk-edit endpoint.
    /// Each entity's method differs only in the body shape (its id
    /// list field name) + the URL path.</summary>
    private async Task<BulkEditResultDto> BulkEditAsync(string path,
        Guid organizationId, object body, bool dryRun, CancellationToken ct)
    {
        var url = $"{path}?organizationId={organizationId}&dryRun={(dryRun ? "true" : "false")}";
        var resp = await _http.PostAsJsonAsync(url, body, Json, ct);
        await EnsureSuccessAsync(resp, ct);
        return (await resp.Content.ReadFromJsonAsync<BulkEditResultDto>(Json, ct))
            ?? throw new NetworkingEngineException(0, $"POST {path} returned null body");
    }

    // ─── Saved views (Phase 10) ──────────────────────────────────────────

    /// <summary>List the caller's own saved views (scoped by
    /// X-User-Id). Service calls without the header get an empty
    /// list — saved views are personal state.</summary>
    public Task<List<SavedViewDto>> ListSavedViewsAsync(Guid organizationId,
        CancellationToken ct = default)
        => GetAsync<List<SavedViewDto>>(
            $"/api/net/saved-views?organizationId={organizationId}", ct);

    public Task<SavedViewDto> GetSavedViewAsync(Guid id, Guid organizationId,
        CancellationToken ct = default)
        => GetAsync<SavedViewDto>(
            $"/api/net/saved-views/{id}?organizationId={organizationId}", ct);

    public Task<SavedViewDto> CreateSavedViewAsync(CreateSavedViewRequest request,
        CancellationToken ct = default)
        => PostAsync<SavedViewDto>("/api/net/saved-views", request, ct);

    public async Task<SavedViewDto> UpdateSavedViewAsync(Guid id, Guid organizationId,
        string name, string q, string? entityTypes, object? filters,
        string? notes, int version, CancellationToken ct = default)
    {
        var resp = await _http.PutAsJsonAsync(
            $"/api/net/saved-views/{id}?organizationId={organizationId}",
            new { name, q, entityTypes, filters, notes, version }, Json, ct);
        await EnsureSuccessAsync(resp, ct);
        return (await resp.Content.ReadFromJsonAsync<SavedViewDto>(Json, ct))
            ?? throw new NetworkingEngineException(0, "PUT returned null body");
    }

    public async Task DeleteSavedViewAsync(Guid id, Guid organizationId,
        CancellationToken ct = default)
    {
        var resp = await _http.DeleteAsync(
            $"/api/net/saved-views/{id}?organizationId={organizationId}", ct);
        await EnsureSuccessAsync(resp, ct);
    }

    // ─── Global search (Phase 10) ────────────────────────────────────────

    /// <summary>Full-text search across devices / vlans / subnets /
    /// servers / links / dhcp-relay-targets. Results are ranked by
    /// Postgres `ts_rank` + filtered to what the caller has read
    /// permission on. Empty `q` returns an empty list.</summary>
    public Task<List<SearchResultDto>> GlobalSearchAsync(Guid organizationId,
        string q, IReadOnlyList<string>? entityTypes = null, int? limit = null,
        CancellationToken ct = default)
    {
        var entityTypesParam = entityTypes is null or { Count: 0 }
            ? null : string.Join(",", entityTypes);
        var qs = BuildQuery(("organizationId", organizationId.ToString()),
                            ("q", q),
                            ("entityTypes", entityTypesParam),
                            ("limit", limit?.ToString()));
        return GetAsync<List<SearchResultDto>>($"/api/net/search{qs}", ct);
    }

    // ─── XLSX round-trip (Phase 10) ──────────────────────────────────────

    /// <summary>Download a tenant's devices as an XLSX workbook.
    /// Returns the raw file bytes so callers can save-as or stream
    /// to a browser. Same columns as <see cref="ExportDevicesCsvAsync"/>
    /// — xlsx is a pure transport adapter over the CSV path.</summary>
    public Task<byte[]> ExportDevicesXlsxAsync(Guid organizationId, CancellationToken ct = default)
        => GetBytesAsync($"/api/net/devices/export.xlsx?organizationId={organizationId}", ct);

    public Task<byte[]> ExportVlansXlsxAsync(Guid organizationId, CancellationToken ct = default)
        => GetBytesAsync($"/api/net/vlans/export.xlsx?organizationId={organizationId}", ct);

    public Task<byte[]> ExportSubnetsXlsxAsync(Guid organizationId, CancellationToken ct = default)
        => GetBytesAsync($"/api/net/subnets/export.xlsx?organizationId={organizationId}", ct);

    public Task<byte[]> ExportServersXlsxAsync(Guid organizationId, CancellationToken ct = default)
        => GetBytesAsync($"/api/net/servers/export.xlsx?organizationId={organizationId}", ct);

    public Task<byte[]> ExportLinksXlsxAsync(Guid organizationId, CancellationToken ct = default)
        => GetBytesAsync($"/api/net/links/export.xlsx?organizationId={organizationId}", ct);

    public Task<byte[]> ExportDhcpRelayTargetsXlsxAsync(Guid organizationId, CancellationToken ct = default)
        => GetBytesAsync($"/api/net/dhcp-relay-targets/export.xlsx?organizationId={organizationId}", ct);

    public Task<ImportValidationResultDto> ImportDevicesXlsxAsync(Guid organizationId,
        byte[] xlsxBytes, bool dryRun = true, string? mode = null, CancellationToken ct = default)
        => PostXlsxAsync("/api/net/devices/import.xlsx", organizationId, xlsxBytes, dryRun, mode, ct);

    public Task<ImportValidationResultDto> ImportVlansXlsxAsync(Guid organizationId,
        byte[] xlsxBytes, bool dryRun = true, string? mode = null, CancellationToken ct = default)
        => PostXlsxAsync("/api/net/vlans/import.xlsx", organizationId, xlsxBytes, dryRun, mode, ct);

    public Task<ImportValidationResultDto> ImportSubnetsXlsxAsync(Guid organizationId,
        byte[] xlsxBytes, bool dryRun = true, string? mode = null, CancellationToken ct = default)
        => PostXlsxAsync("/api/net/subnets/import.xlsx", organizationId, xlsxBytes, dryRun, mode, ct);

    public Task<ImportValidationResultDto> ImportServersXlsxAsync(Guid organizationId,
        byte[] xlsxBytes, bool dryRun = true, string? mode = null, CancellationToken ct = default)
        => PostXlsxAsync("/api/net/servers/import.xlsx", organizationId, xlsxBytes, dryRun, mode, ct);

    public Task<ImportValidationResultDto> ImportLinksXlsxAsync(Guid organizationId,
        byte[] xlsxBytes, bool dryRun = true, string? mode = null, CancellationToken ct = default)
        => PostXlsxAsync("/api/net/links/import.xlsx", organizationId, xlsxBytes, dryRun, mode, ct);

    public Task<ImportValidationResultDto> ImportDhcpRelayTargetsXlsxAsync(Guid organizationId,
        byte[] xlsxBytes, bool dryRun = true, string? mode = null, CancellationToken ct = default)
        => PostXlsxAsync("/api/net/dhcp-relay-targets/import.xlsx", organizationId, xlsxBytes, dryRun, mode, ct);

    // ─── Thin lists added in Phase 10b wave N (ApiClient parity) ──────────

    public Task<List<IpAddressListRowDto>> ListIpAddressesAsync(Guid organizationId,
        CancellationToken ct = default)
        => GetAsync<List<IpAddressListRowDto>>(
            $"/api/net/ip-addresses?organizationId={organizationId}", ct);

    public Task<List<PortListRowDto>> ListPortsAsync(Guid organizationId,
        CancellationToken ct = default)
        => GetAsync<List<PortListRowDto>>(
            $"/api/net/ports?organizationId={organizationId}", ct);

    public Task<List<AggregateEthernetListRowDto>> ListAggregateEthernetAsync(Guid organizationId,
        CancellationToken ct = default)
        => GetAsync<List<AggregateEthernetListRowDto>>(
            $"/api/net/aggregate-ethernet?organizationId={organizationId}", ct);

    public Task<List<MlagDomainListRowDto>> ListMlagDomainsAsync(Guid organizationId,
        CancellationToken ct = default)
        => GetAsync<List<MlagDomainListRowDto>>(
            $"/api/net/mlag-domains?organizationId={organizationId}", ct);

    public Task<List<ModuleListRowDto>> ListModulesAsync(Guid organizationId,
        CancellationToken ct = default)
        => GetAsync<List<ModuleListRowDto>>(
            $"/api/net/modules?organizationId={organizationId}", ct);

    public Task<List<MstpRuleListRowDto>> ListMstpRulesAsync(Guid organizationId,
        CancellationToken ct = default)
        => GetAsync<List<MstpRuleListRowDto>>(
            $"/api/net/mstp-rules?organizationId={organizationId}", ct);

    public Task<List<ReservationShelfListRowDto>> ListReservationShelfAsync(Guid organizationId,
        CancellationToken ct = default)
        => GetAsync<List<ReservationShelfListRowDto>>(
            $"/api/net/reservation-shelf?organizationId={organizationId}", ct);

    public Task<List<AsnAllocationListRowDto>> ListAsnAllocationsAsync(Guid organizationId,
        CancellationToken ct = default)
        => GetAsync<List<AsnAllocationListRowDto>>(
            $"/api/net/asn-allocations?organizationId={organizationId}", ct);

    public Task<List<RoomListRowDto>> ListRoomsAsync(Guid organizationId,
        CancellationToken ct = default)
        => GetAsync<List<RoomListRowDto>>(
            $"/api/net/rooms?organizationId={organizationId}", ct);

    public Task<List<RackListRowDto>> ListRacksAsync(Guid organizationId,
        CancellationToken ct = default)
        => GetAsync<List<RackListRowDto>>(
            $"/api/net/racks?organizationId={organizationId}", ct);

    public Task<List<LinkEndpointListRowDto>> ListLinkEndpointsAsync(Guid organizationId,
        CancellationToken ct = default)
        => GetAsync<List<LinkEndpointListRowDto>>(
            $"/api/net/link-endpoints?organizationId={organizationId}", ct);

    public Task<List<ServerNicListRowDto>> ListServerNicsAsync(Guid organizationId,
        CancellationToken ct = default)
        => GetAsync<List<ServerNicListRowDto>>(
            $"/api/net/server-nics?organizationId={organizationId}", ct);

    // ─── Session / identity (Phase 10b) ──────────────────────────────────

    /// <summary>Who-am-i — summary of the caller's effective scope
    /// grants. Drives the WPF session banner + "am I allowed to do
    /// this?" pre-flight checks. Service-origin calls (no X-User-Id
    /// header) come back with userId=null + grantCount=0.</summary>
    public Task<WhoAmIDto> WhoAmIAsync(Guid organizationId, CancellationToken ct = default)
        => GetAsync<WhoAmIDto>($"/api/net/whoami?organizationId={organizationId}", ct);

    /// <summary>Full scope-grant list for the current caller. Bypasses
    /// the read:ScopeGrant gate (reading your own access is always
    /// permitted). Service-origin calls come back with an empty list.
    /// Rows ordered by entityType + action + scopeType — no client-
    /// side sort needed for a clean table render.</summary>
    public Task<List<ScopeGrantDto>> ListMyGrantsAsync(Guid organizationId,
        CancellationToken ct = default)
        => GetAsync<List<ScopeGrantDto>>(
            $"/api/net/whoami/grants?organizationId={organizationId}", ct);

    // ─── Naming overrides (Phase 10b) ────────────────────────────────────

    public Task<List<NamingOverrideDto>> ListNamingOverridesAsync(Guid organizationId,
        string? entityType = null, string? scopeLevel = null, CancellationToken ct = default)
    {
        var qs = BuildQuery(
            ("organizationId", organizationId.ToString()),
            ("entityType",     entityType),
            ("scopeLevel",     scopeLevel));
        return GetAsync<List<NamingOverrideDto>>($"/api/net/naming/overrides{qs}", ct);
    }

    public Task<NamingOverrideDto> CreateNamingOverrideAsync(
        CreateNamingOverrideRequest request, CancellationToken ct = default)
        => PostAsync<NamingOverrideDto>("/api/net/naming/overrides", request, ct);

    public async Task<NamingOverrideDto> UpdateNamingOverrideAsync(Guid id,
        UpdateNamingOverrideRequest request, Guid organizationId, CancellationToken ct = default)
    {
        var url = $"/api/net/naming/overrides/{id}?organizationId={organizationId}";
        var req = new HttpRequestMessage(HttpMethod.Put, url)
        {
            Content = JsonContent.Create(request, options: Json),
        };
        return await SendAsync<NamingOverrideDto>(req, ct);
    }

    public async Task DeleteNamingOverrideAsync(Guid id, Guid organizationId,
        CancellationToken ct = default)
    {
        var url = $"/api/net/naming/overrides/{id}?organizationId={organizationId}";
        var resp = await _http.DeleteAsync(url, ct);
        await EnsureSuccessAsync(resp, ct);
    }

    /// <summary>Resolve which naming template wins for an entity at
    /// the given hierarchy position. Returns both the resolved
    /// template + which tier supplied it (BuildingSpecificSubtype /
    /// BuildingAnySubtype / Site* / Region* / Global* / Default).</summary>
    public Task<NamingResolveResponseDto> ResolveNamingAsync(
        ResolveNamingRequest request, CancellationToken ct = default)
        => PostAsync<NamingResolveResponseDto>("/api/net/naming/resolve", request, ct);

    private async Task<ImportValidationResultDto> PostXlsxAsync(string path,
        Guid organizationId, byte[] xlsxBytes, bool dryRun,
        string? mode = null, CancellationToken ct = default)
    {
        var qs = BuildQuery(
            ("organizationId", organizationId.ToString()),
            ("dryRun",         dryRun ? "true" : "false"),
            ("mode",           mode));
        var url = $"{path}{qs}";
        var content = new ByteArrayContent(xlsxBytes);
        content.Headers.ContentType = new MediaTypeHeaderValue(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        var resp = await _http.PostAsync(url, content, ct);
        await EnsureSuccessAsync(resp, ct);
        return (await resp.Content.ReadFromJsonAsync<ImportValidationResultDto>(Json, ct))
            ?? throw new NetworkingEngineException(0, $"POST {path} returned null body");
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

    /// <summary>GET the raw response body as a string. Used by the
    /// bulk-export endpoints that return text/csv rather than JSON.</summary>
    private async Task<string> GetStringAsync(string url, CancellationToken ct)
    {
        var resp = await _http.GetAsync(url, ct);
        await EnsureSuccessAsync(resp, ct);
        return await resp.Content.ReadAsStringAsync(ct);
    }

    /// <summary>GET the raw response body as bytes. Used by the
    /// .xlsx export endpoints — XLSX is a binary format that must
    /// stay exactly as the server produced it.</summary>
    private async Task<byte[]> GetBytesAsync(string url, CancellationToken ct)
    {
        var resp = await _http.GetAsync(url, ct);
        await EnsureSuccessAsync(resp, ct);
        return await resp.Content.ReadAsByteArrayAsync(ct);
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

// VLAN list (picker / audit-drill hostname resolution)
public record VlanListRowDto(Guid Id, int VlanId, string DisplayName,
    string? BlockCode, string ScopeLevel, string Status, int Version);

// Link list (picker / audit-drill link_code resolution)
public record LinkListRowDto(Guid Id, string LinkCode, string? LinkType,
    string? DeviceA, string? DeviceB, string Status, int Version);

// VLAN block list (picker)
public record VlanBlockDto(Guid Id, string BlockCode, string DisplayName,
    int VlanFirst, int VlanLast, long Available);

// ASN block list (picker)
public record AsnBlockDto(Guid Id, string BlockCode, string DisplayName,
    long AsnFirst, long AsnLast, long Available);

// MLAG pool list (picker)
public record MlagPoolDto(Guid Id, string PoolCode, string DisplayName,
    int DomainFirst, int DomainLast, long Available);

// IP pool list (picker) — availability depends on prefix length the
// admin wants to carve, so pre-computation would be misleading
public record IpPoolDto(Guid Id, string PoolCode, string DisplayName,
    string Network, int Family);

// ─── Config Generation / CLI flavors (Phase 10) ────────────────────────

public record CliFlavorConfigDto(string Code, string DisplayName, string Vendor,
    string? Description, string Status, bool DefaultEnabled,
    bool Enabled, bool IsDefault, string? Notes);

/// <summary>Dry-run or persisted render response. `Id`,
/// `PreviousRenderId`, `RenderDurationMs` populate only on the
/// persisted (`POST`) path — on in-memory dry-runs they come back null.</summary>
public record RenderedConfigDto(Guid DeviceId, string FlavorCode, string Body,
    string BodySha256, int LineCount, DateTime RenderedAt,
    Guid? Id, Guid? PreviousRenderId, int? RenderDurationMs);

/// <summary>Lightweight list row — no body.</summary>
public record RenderedConfigSummaryDto(Guid Id, Guid DeviceId, string FlavorCode,
    string BodySha256, int LineCount, int? RenderDurationMs,
    Guid? PreviousRenderId, DateTime RenderedAt, int? RenderedBy);

/// <summary>Full record including body — for the diff / view-one flow.</summary>
public record RenderedConfigRecordDto(Guid Id, Guid DeviceId, string FlavorCode,
    string Body, string BodySha256, int LineCount, int? RenderDurationMs,
    Guid? PreviousRenderId, DateTime RenderedAt, int? RenderedBy);

/// <summary>Line-level set diff between this render and its chain
/// predecessor. First-ever render returns whole body as `Added`.</summary>
public record RenderDiffDto(Guid RenderId, Guid? PreviousRenderId,
    List<string> Added, List<string> Removed, int UnchangedCount);

public record DeviceRenderErrorDto(Guid DeviceId, string Hostname, string Error);

public record BuildingRenderResultDto(Guid BuildingId, string? BuildingCode,
    int TotalDevices, int Succeeded, int Failed,
    List<RenderedConfigDto> Renders, List<DeviceRenderErrorDto> Errors);

public record SiteRenderResultDto(Guid SiteId, string? SiteCode,
    int TotalDevices, int Succeeded, int Failed,
    List<RenderedConfigDto> Renders, List<DeviceRenderErrorDto> Errors);

public record RegionRenderResultDto(Guid RegionId, string? RegionCode,
    int TotalDevices, int Succeeded, int Failed,
    List<RenderedConfigDto> Renders, List<DeviceRenderErrorDto> Errors);

public record DhcpRelayTargetDto(Guid Id, Guid OrganizationId, Guid VlanId,
    string ServerIp, Guid? IpAddressId, int Priority, string Status,
    int Version, DateTime CreatedAt, DateTime UpdatedAt, string? Notes);

public record CreateDhcpRelayTargetRequest(Guid OrganizationId, Guid VlanId,
    string ServerIp, Guid? IpAddressId = null, int Priority = 10,
    string? Notes = null);

// ─── Bulk import (Phase 10) ────────────────────────────────────────────

/// <summary>Per-row outcome from a bulk CSV import. <c>RowNumber</c> is
/// 1-based so it matches spreadsheet row display; <c>Identifier</c>
/// echoes the key column (hostname for devices, vlan_id for VLANs,
/// etc.) so UIs can show which record failed without the operator
/// counting rows.</summary>
public record ImportRowOutcomeDto(int RowNumber, bool Ok, List<string> Errors, string Identifier);

/// <summary>Top-level result of a bulk import request. <c>Applied</c>
/// is false when <c>DryRun</c> was true (or when apply isn't yet
/// wired for the entity); <c>Valid</c> + <c>Invalid</c> partition
/// the per-row outcomes so UIs can drive a summary banner.</summary>
public record ImportValidationResultDto(int TotalRows, int Valid, int Invalid,
    bool DryRun, bool Applied, List<ImportRowOutcomeDto> Outcomes);

// ─── Bulk edit (Phase 10) ──────────────────────────────────────────────

/// <summary>Per-row outcome for a bulk edit. Single-string Error
/// field (not a list) because bulk-edit changes one column per row
/// so there's at most one reason it could fail per row.</summary>
public record BulkEditOutcomeDto(Guid Id, string Hostname, bool Ok, string? Error);

/// <summary>Response envelope for a bulk-edit request. Matches the
/// import-validation shape closely so UIs can drive both with one
/// summary-banner + per-row-list component.</summary>
public record BulkEditResultDto(int Total, int Succeeded, int Failed,
    bool DryRun, bool Applied, List<BulkEditOutcomeDto> Outcomes);

// ─── Scope grants (Phase 10 RBAC) ─────────────────────────────────────

public record ScopeGrantDto(Guid Id, Guid OrganizationId, int UserId,
    string Action, string EntityType, string ScopeType, Guid? ScopeEntityId,
    string Status, int Version, DateTime CreatedAt, DateTime UpdatedAt,
    string? Notes);

public record CreateScopeGrantRequest(Guid OrganizationId, int UserId,
    string Action, string EntityType, string ScopeType, Guid? ScopeEntityId = null,
    string? Notes = null);

/// <summary>Resolver decision. <c>MatchedGrantId</c> identifies which
/// grant allowed the action when <c>Allowed=true</c> — lets UIs
/// show "you have access via grant X" and lets audit log the
/// specific grant that authorised a later action.</summary>
public record PermissionDecisionDto(bool Allowed, Guid? MatchedGrantId);

// ─── Global search (Phase 10) ─────────────────────────────────────────

/// <summary>One ranked hit from a global search. <c>EntityType</c>
/// is one of Device / Vlan / Subnet / Server / Link /
/// DhcpRelayTarget; <c>Label</c> is the human-facing display
/// (e.g. hostname, "vlan 120 Servers"); <c>Rank</c> comes from
/// Postgres ts_rank and is higher for better matches.</summary>
public record SearchResultDto(string EntityType, Guid Id, string Label,
    float Rank, string Snippet);

// ─── Saved views (Phase 10) ───────────────────────────────────────────

public record SavedViewDto(Guid Id, Guid OrganizationId, int UserId, string Name,
    string Q, string? EntityTypes, object Filters, string Status, int Version,
    DateTime CreatedAt, DateTime UpdatedAt, string? Notes);

public record CreateSavedViewRequest(Guid OrganizationId, string Name,
    string Q = "", string? EntityTypes = null, object? Filters = null,
    string? Notes = null);

// ─── Thin lists added in Phase 10b (servers + subnets) ────────────────

/// <summary>Thin server list row — matches `ServerListRow` in the
/// engine. Capped at 5000 server-side; LEFT JOINs
/// net.server_profile + net.building for display.</summary>
public record ServerListRowDto(Guid Id, string Hostname, string? ProfileCode,
    string? BuildingCode, string Status, int Version);

/// <summary>Thin subnet list row — matches `SubnetListRow`. Network
/// is pre-rendered as a CIDR string on the server so the wire shape
/// is stable. VlanTag is null when the subnet has no linked VLAN.
/// <paramref name="PoolId"/> ships alongside <paramref name="PoolCode"/>
/// so detail pages can drill to the pool without a code→id lookup.</summary>
public record SubnetListRowDto(Guid Id, string SubnetCode, string DisplayName,
    string Network, string ScopeLevel, Guid? PoolId, string? PoolCode, int? VlanTag,
    string Status, int Version);

// ─── Audit rollups (Phase 10b) ────────────────────────────────────────

/// <summary>Per-entity-type audit rollup — matches `EntityTypeStats`.</summary>
public record EntityTypeStatsDto(string EntityType, long TotalCount,
    long DistinctActors, DateTime? LastSeenAt);

/// <summary>Time-bucketed audit count point — matches `AuditTrendPoint`.
/// BucketAt is the bucket start (date_trunc(bucket, created_at)).</summary>
public record AuditTrendPointDto(DateTime BucketAt, long Count);

/// <summary>Top-actor audit rollup — matches `TopActor`. ActorUserId
/// + ActorDisplay are both nullable for service-origin rows.</summary>
public record TopActorDto(int? ActorUserId, string? ActorDisplay,
    long TotalCount, long DistinctEntityTypes, DateTime? LastSeenAt);

/// <summary>Recent correlation rollup — matches `RecentCorrelation`
/// in services/networking-engine/src/audit.rs. <c>SetId</c> /
/// <c>SetTitle</c> / <c>SetStatus</c> are non-null only when a
/// net.change_set shares the correlation id.</summary>
public record RecentCorrelationDto(Guid CorrelationId, long EntryCount,
    long DistinctEntityTypes, DateTime FirstSeenAt, DateTime LastSeenAt,
    Guid? SetId, string? SetTitle, string? SetStatus);

/// <summary>Distinct audit-action row — matches `DistinctAction` in
/// services/networking-engine/src/audit.rs. Rows ordered by
/// lastSeenAt DESC so rare-but-recent actions bubble to the top
/// of filter pickers.</summary>
public record DistinctActionDto(string Action, long Count, DateTime LastSeenAt);

// ─── Search facets (Phase 10b) ────────────────────────────────────────

/// <summary>Per-entity-type search-hit count — matches `SearchFacet`.
/// Drives the "narrow by type" chip bar in the search UI.</summary>
public record SearchFacetDto(string EntityType, long Count);

// ─── Pool utilization (Phase 10b) ─────────────────────────────────────

/// <summary>One pool dimension's utilization — matches
/// `PoolUtilizationRow`. <c>PoolKind</c> disambiguates IP pool's
/// two-row contribution ("IP:Subnets" + "IP:Addresses"). Capacity=0
/// on the IP:Subnets row (we don't carry a subnet-count capacity);
/// PercentFull caps at 999 to avoid UI overflow on data-quality
/// outliers.</summary>
public record PoolUtilizationRowDto(string PoolKind, Guid PoolId,
    string PoolCode, string DisplayName, long Used, long Capacity,
    int PercentFull, string Status);

// ─── Thin-list DTOs added in Phase 10b (ApiClient parity) ─────────────

public record IpAddressListRowDto(Guid Id, Guid SubnetId, string? SubnetCode,
    string Address, string? AssignedToType, Guid? AssignedToId,
    bool IsReserved, string Status, int Version);

public record PortListRowDto(Guid Id, Guid DeviceId, string? DeviceHostname,
    string InterfaceName, string InterfacePrefix, int? SpeedMbps, bool AdminUp,
    string? Description, string PortMode, int? NativeVlanId,
    Guid? AggregateEthernetId, string Status, int Version);

public record AggregateEthernetListRowDto(Guid Id, Guid DeviceId,
    string? DeviceHostname, string AeName, string LacpMode, int MinLinks,
    int MemberCount, string? Description, string Status, int Version);

public record MlagDomainListRowDto(Guid Id, Guid PoolId, string? PoolCode,
    int DomainId, string DisplayName, string ScopeLevel, string Status, int Version);

public record ModuleListRowDto(Guid Id, Guid DeviceId, string? DeviceHostname,
    string Slot, string ModuleType, string? Model, string? SerialNumber,
    string? PartNumber, string Status, int Version);

public record MstpRuleListRowDto(Guid Id, string RuleCode, string DisplayName,
    string ScopeLevel, Guid? ScopeEntityId, int StepCount, string Status, int Version);

public record ReservationShelfListRowDto(Guid Id, string ResourceType,
    string ResourceKey, Guid? PoolId, Guid? BlockId, DateTime RetiredAt,
    DateTime AvailableAfter, string? RetiredReason, string Status, int Version);

public record AsnAllocationListRowDto(Guid Id, Guid BlockId, string? BlockCode,
    long Asn, string AllocatedToType, Guid AllocatedToId, string? TargetDisplay, string Status);

public record RoomListRowDto(Guid Id, Guid FloorId, string RoomCode,
    string RoomType, int? MaxRacks, string Status);

public record RackListRowDto(Guid Id, Guid RoomId, string RackCode, int UHeight,
    string? Row, int? Position, int? MaxDevices, string Status);

public record LinkEndpointListRowDto(Guid Id, Guid LinkId, string? LinkCode,
    int EndpointOrder, string? DeviceHostname, string? PortInterface,
    string? InterfaceName, string? IpAddress, int? VlanTag,
    string? Description, string Status);

public record ServerNicListRowDto(Guid Id, Guid ServerId, string? ServerHostname,
    int NicIndex, string? NicName, string? MlagSide, string? TargetDeviceHostname,
    string? TargetPortInterface, string? IpAddress, string? MacAddress,
    bool AdminUp, string Status);

// ─── Session / identity DTOs (Phase 10b) ─────────────────────────────

/// <summary>Who-am-i summary — matches `WhoAmIResponse` in the engine.
/// Service-origin calls come back with userId=null + grantCount=0.</summary>
public record WhoAmIDto(int? UserId, int GrantCount,
    List<string> Actions, List<string> EntityTypes);

// ─── Naming override DTOs (Phase 10b) ────────────────────────────────

public record NamingOverrideDto(Guid Id, Guid OrganizationId, string EntityType,
    string? SubtypeCode, string ScopeLevel, Guid? ScopeEntityId,
    string NamingTemplate, string Status, int Version,
    DateTime CreatedAt, DateTime UpdatedAt, string? Notes);

public record CreateNamingOverrideRequest(Guid OrganizationId, string EntityType,
    string? SubtypeCode, string ScopeLevel, Guid? ScopeEntityId,
    string NamingTemplate, string? Notes = null);

public record UpdateNamingOverrideRequest(string NamingTemplate,
    int Version, string? Notes = null);

public record ResolveNamingRequest(Guid OrganizationId, string EntityType,
    string? SubtypeCode = null, Guid? RegionId = null, Guid? SiteId = null,
    Guid? BuildingId = null, string? DefaultTemplate = null);

/// <summary>Naming-resolve response — matches `ResolveResponse` in
/// the engine. <c>Source</c> is one of
/// BuildingSpecificSubtype / BuildingAnySubtype / SiteSpecificSubtype /
/// SiteAnySubtype / RegionSpecificSubtype / RegionAnySubtype /
/// GlobalSpecificSubtype / GlobalAnySubtype / Default. <c>OverrideId</c>
/// is null for the Default case.</summary>
public record NamingResolveResponseDto(string Template, string Source, Guid? OverrideId);

/// <summary>Subnet carver preview — matches `CarvePreview` in
/// services/networking-engine/src/ip_allocation.rs. Read-only
/// dry-run: no net.subnet row is inserted. Carries pool metadata
/// alongside the candidate so UIs can render "next /24 in
/// 10.0.0.0/16 → 10.0.4.0/24".</summary>
public record CarvePreviewDto(Guid PoolId, string PoolCidr,
    int PoolPrefixLength, int RequestedPrefixLength,
    string CandidateCidr, bool IsIpv6);
