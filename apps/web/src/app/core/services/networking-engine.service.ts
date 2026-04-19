import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';

/// One ranked hit from the engine's global search.
/// Matches `SearchResult` in services/networking-engine/src/search.rs
/// (camelCase on the wire via `#[serde(rename_all = "camelCase")]`).
export interface SearchResult {
  entityType: string;
  id: string;
  label: string;
  rank: number;
  snippet: string;
}

/// One row in a search-facets response — matches `SearchFacet` in
/// services/networking-engine/src/search.rs. Used to render a
/// per-entity-type count bar in the search UI so operators can narrow
/// without running the full search first.
export interface SearchFacet {
  entityType: string;
  count: number;
}

/// Saved search — per-user named query state. Matches
/// `SavedViewDto` in the engine's saved_views module.
export interface SavedView {
  id: string;
  organizationId: string;
  userId: number;
  name: string;
  q: string;
  entityTypes: string | null;
  filters: unknown;
  status: string;
  version: number;
  createdAt: string;
  updatedAt: string;
  notes: string | null;
}

/// Audit row — matches `AuditRowDto` from the engine.
export interface AuditRow {
  sequenceId: number;
  createdAt: string;
  entityType: string;
  entityId: string | null;
  action: string;
  actorUserId: number | null;
  actorDisplay: string | null;
  correlationId: string | null;
  details: unknown;
}

/// Thin device-list row — matches `DeviceListRow` in the engine's
/// list_devices handler. Used by callers that need hostname → uuid
/// resolution (e.g. device-detail auditing where the WPF/legacy
/// model carries switch_guide's numeric id, not net.device.id).
export interface DeviceListRow {
  id: string;
  hostname: string;
  roleCode: string | null;
  buildingCode: string | null;
  status: string;
  version: number;
}

/// Thin VLAN-list row — matches `VlanListRow` in the engine. Used by
/// pickers + the web VLAN grid. `blockCode` is the parent VLAN block's
/// code (null when the VLAN isn't allocated from a block yet).
export interface VlanListRow {
  id: string;
  vlanId: number;
  displayName: string;
  blockCode: string | null;
  scopeLevel: string;
  status: string;
  version: number;
}

/// Thin link-list row — matches `LinkListRow` in the engine. deviceA
/// / deviceB are the hostnames of endpoint_order=0 / 1 resolved via
/// LEFT JOIN so the grid doesn't need a second round-trip per row.
export interface LinkListRow {
  id: string;
  linkCode: string;
  linkType: string | null;
  deviceA: string | null;
  deviceB: string | null;
  status: string;
  version: number;
}

/// Thin server-list row — matches `ServerListRow` in the engine.
export interface ServerListRow {
  id: string;
  hostname: string;
  profileCode: string | null;
  buildingCode: string | null;
  status: string;
  version: number;
}

/// Thin subnet-list row — matches `SubnetListRow` in the engine.
/// network is pre-rendered as a CIDR string; vlanTag is the numeric
/// VLAN id when a net.vlan row is linked, null otherwise.
export interface SubnetListRow {
  id: string;
  subnetCode: string;
  displayName: string;
  network: string;
  scopeLevel: string;
  poolCode: string | null;
  vlanTag: number | null;
  status: string;
  version: number;
}

/// DHCP relay target — matches `DhcpRelayTarget` in
/// services/networking-engine/src/dhcp_relay.rs. `serverIp` ships as
/// the bare host string (e.g. `"10.11.120.10"`) via a custom
/// serialize_with, not the CIDR form.
export interface DhcpRelayTargetRow {
  id: string;
  organizationId: string;
  vlanId: string;
  serverIp: string;
  ipAddressId: string | null;
  priority: number;
  status: string;
  version: number;
  createdAt: string;
  updatedAt: string;
  notes: string | null;
}

/// One violation from the validation rule engine. Matches `Violation`
/// in services/networking-engine/src/validation.rs.
export interface Violation {
  ruleCode: string;
  severity: 'Error' | 'Warning' | 'Info';
  entityType: string;
  entityId: string | null;
  message: string;
}

/// Outcome of a validation run. Matches `ValidationRunResult`.
export interface ValidationRunResult {
  violations: Violation[];
  rulesRun: number;
  rulesWithFindings: number;
  totalViolations: number;
}

/// One entity-type's audit activity stats. Matches
/// `EntityTypeStats` from services/networking-engine/src/audit.rs.
export interface EntityTypeStats {
  entityType: string;
  totalCount: number;
  distinctActors: number;
  lastSeenAt: string | null;
}

/// Per-row outcome from a bulk CSV import. Matches
/// `ImportRowOutcomeDto` from the Rust side. Ok=false means the row
/// failed validation; the errors array carries the details the
/// operator needs to fix.
export interface ImportRowOutcome {
  rowNumber: number;
  ok: boolean;
  errors: string[];
  identifier: string;
}

/// Top-level bulk validate/apply result. DryRun=true + Applied=false
/// is the validate-only path used by the web today; DryRun=false +
/// Applied=true is the real apply path (WPF-only for this slice).
export interface ImportValidationResult {
  totalRows: number;
  valid: number;
  invalid: number;
  dryRun: boolean;
  applied: boolean;
  outcomes: ImportRowOutcome[];
}

/// Pool row shapes — mirror the EntityBase subclasses served by
/// Central.Api's /api/net/{asn,vlan,ip}-{pools,blocks} endpoints.
/// PascalCase because the .NET side ships records as-is.
export interface AsnPoolRow {
  Id: string;
  PoolCode: string;
  DisplayName: string;
  AsnFirst: number;
  AsnLast: number;
  Status: string;
}
export interface AsnBlockRow {
  Id: string;
  PoolId: string;
  BlockCode: string;
  DisplayName: string;
  AsnFirst: number;
  AsnLast: number;
  ScopeLevel: string;
  Status: string;
}
export interface VlanPoolRow {
  Id: string;
  PoolCode: string;
  DisplayName: string;
  VlanFirst: number;
  VlanLast: number;
  Status: string;
}
export interface VlanBlockRow {
  Id: string;
  PoolId: string;
  BlockCode: string;
  DisplayName: string;
  VlanStart: number;
  VlanEnd: number;
  Status: string;
}
export interface IpPoolRow {
  Id: string;
  PoolCode: string;
  DisplayName: string;
  PoolCidr: string;
  AddressFamily: string;
  Status: string;
}

/// Hierarchy row shapes — mirror the Central.Api `/api/net/regions`
/// / `/sites` / `/buildings` / `/floors` endpoints which return
/// PascalCase from the .NET models. Kept as flat DTOs per level
/// because the WPF client uses a flat node list + parent ids for
/// its TreeListControl.
export interface RegionRow {
  Id: string;
  RegionCode: string;
  DisplayName: string;
  Status: string;
}
export interface SiteRow {
  Id: string;
  RegionId: string;
  SiteCode: string;
  DisplayName: string;
  Status: string;
}
export interface BuildingRow {
  Id: string;
  SiteId: string;
  BuildingCode: string;
  DisplayName: string;
  Status: string;
}
export interface FloorRow {
  Id: string;
  BuildingId: string;
  FloorCode: string;
  DisplayName: string | null;
  Status: string;
}

/// One scope_grant tuple. Matches `ScopeGrantDto` from the engine.
export interface ScopeGrant {
  id: string;
  organizationId: string;
  userId: number;
  action: string;
  entityType: string;
  scopeType: string;
  scopeEntityId: string | null;
  status: string;
  version: number;
  createdAt: string;
  updatedAt: string;
  notes: string | null;
}

/// Thin Phase-10 surface for the Angular web client. Parallel to
/// the WPF `NetworkingEngineClient`; covers the operator-facing
/// endpoints (search, saved views, audit timeline) that web
/// features consume. Bulk / scope-grants / CRUD are out of scope
/// for this slice — they'd land alongside matching components.
@Injectable({ providedIn: 'root' })
export class NetworkingEngineService {
  private readonly base = environment.networkingEngineUrl;

  constructor(private http: HttpClient) {}

  /// Full-text search across 6 tenant-owned entities. Empty q →
  /// empty result set server-side, matches the engine contract.
  search(
    organizationId: string,
    q: string,
    entityTypes?: string[],
    limit?: number,
  ): Observable<SearchResult[]> {
    let params = new HttpParams()
      .set('organizationId', organizationId)
      .set('q', q);
    if (entityTypes && entityTypes.length > 0) {
      params = params.set('entityTypes', entityTypes.join(','));
    }
    if (limit !== undefined) {
      params = params.set('limit', limit.toString());
    }
    return this.http.get<SearchResult[]>(`${this.base}/api/net/search`, { params });
  }

  /// Per-entity-type facet counts for a search query. Returns one row
  /// per entity type matching the query (including zeros when the
  /// caller restricted `entityTypes`). Renders a narrowing hint bar
  /// in the search UI without running the full ranked query.
  searchFacets(
    organizationId: string,
    q: string,
    entityTypes?: string[],
  ): Observable<SearchFacet[]> {
    let params = new HttpParams()
      .set('organizationId', organizationId)
      .set('q', q);
    if (entityTypes && entityTypes.length > 0) {
      params = params.set('entityTypes', entityTypes.join(','));
    }
    return this.http.get<SearchFacet[]>(`${this.base}/api/net/search/facets`, { params });
  }

  /// List the caller's saved views (X-User-Id scoped on the engine
  /// side; service calls without the header get an empty list).
  listSavedViews(organizationId: string): Observable<SavedView[]> {
    const params = new HttpParams().set('organizationId', organizationId);
    return this.http.get<SavedView[]>(`${this.base}/api/net/saved-views`, { params });
  }

  /// Create a saved view owned by the caller. Name is unique per
  /// (tenant, user); the engine returns 409 on collision.
  createSavedView(body: {
    organizationId: string;
    name: string;
    q: string;
    entityTypes?: string | null;
    notes?: string | null;
  }): Observable<SavedView> {
    return this.http.post<SavedView>(`${this.base}/api/net/saved-views`, {
      organizationId: body.organizationId,
      name:           body.name,
      q:              body.q,
      entityTypes:    body.entityTypes ?? null,
      notes:          body.notes ?? null,
      filters:        {},
    });
  }

  /// Soft-delete a saved view. Engine returns 404 when the view
  /// belongs to another user (ownership-as-auth; doesn't leak
  /// existence).
  deleteSavedView(id: string, organizationId: string): Observable<void> {
    const params = new HttpParams().set('organizationId', organizationId);
    return this.http.delete<void>(`${this.base}/api/net/saved-views/${id}`, { params });
  }

  /// Thin device list — capped at 5000 rows per tenant. Used by
  /// callers needing hostname → net.device uuid resolution (the
  /// WPF grid's selectId handler does the same thing).
  listDevices(organizationId: string): Observable<DeviceListRow[]> {
    const params = new HttpParams().set('organizationId', organizationId);
    return this.http.get<DeviceListRow[]>(`${this.base}/api/net/devices`, { params });
  }

  /// Thin VLAN list — 5000 row cap, ordered by VLAN tag.
  listVlans(organizationId: string): Observable<VlanListRow[]> {
    const params = new HttpParams().set('organizationId', organizationId);
    return this.http.get<VlanListRow[]>(`${this.base}/api/net/vlans`, { params });
  }

  /// Thin link list — 5000 row cap, endpoint hostnames pre-resolved.
  listLinks(organizationId: string): Observable<LinkListRow[]> {
    const params = new HttpParams().set('organizationId', organizationId);
    return this.http.get<LinkListRow[]>(`${this.base}/api/net/links`, { params });
  }

  /// Thin server list — 5000 row cap, profile + building code joined.
  listServers(organizationId: string): Observable<ServerListRow[]> {
    const params = new HttpParams().set('organizationId', organizationId);
    return this.http.get<ServerListRow[]>(`${this.base}/api/net/servers`, { params });
  }

  /// Thin subnet list — 5000 row cap, pool code + linked VLAN tag
  /// resolved, network rendered as a CIDR string.
  listSubnets(organizationId: string): Observable<SubnetListRow[]> {
    const params = new HttpParams().set('organizationId', organizationId);
    return this.http.get<SubnetListRow[]>(`${this.base}/api/net/subnets`, { params });
  }

  /// DHCP relay targets — optionally filter to one VLAN. Requires
  /// read:DhcpRelayTarget on the caller (engine returns 403 when the
  /// X-User-Id header is set + caller lacks the grant).
  listDhcpRelayTargets(
    organizationId: string,
    vlanId?: string,
  ): Observable<DhcpRelayTargetRow[]> {
    let params = new HttpParams().set('organizationId', organizationId);
    if (vlanId) params = params.set('vlanId', vlanId);
    return this.http.get<DhcpRelayTargetRow[]>(`${this.base}/api/net/dhcp-relay-targets`, { params });
  }

  /// Bulk validate — POSTs the CSV body to the engine's dry_run
  /// path and returns per-row outcomes. The web client sticks to
  /// validate-only; apply/upsert goes through the WPF BulkPanel
  /// today (needs a write-confirm dialog the web doesn't have yet).
  validateBulk(
    entity: 'devices' | 'vlans' | 'subnets' | 'servers' | 'links' | 'dhcp-relay-targets',
    organizationId: string,
    csvBody: string,
    mode: 'create' | 'upsert' = 'create',
  ): Observable<ImportValidationResult> {
    const params = new HttpParams()
      .set('organizationId', organizationId)
      .set('dryRun', 'true')
      .set('mode', mode);
    return this.http.post<ImportValidationResult>(
      `${this.base}/api/net/${entity}/import`,
      csvBody,
      { params, headers: { 'Content-Type': 'text/csv' } },
    );
  }

  /// Pool listings — ASN / VLAN / IP pool + block enumerations
  /// served by Central.Api (same /api/net/ namespace).

  listAsnPools():  Observable<AsnPoolRow[]>  { return this.http.get<AsnPoolRow[]>(`${this.base}/api/net/asn-pools`); }
  listAsnBlocks(): Observable<AsnBlockRow[]> { return this.http.get<AsnBlockRow[]>(`${this.base}/api/net/asn-blocks`); }
  listVlanPools():  Observable<VlanPoolRow[]>  { return this.http.get<VlanPoolRow[]>(`${this.base}/api/net/vlan-pools`); }
  listVlanBlocks(): Observable<VlanBlockRow[]> { return this.http.get<VlanBlockRow[]>(`${this.base}/api/net/vlan-blocks`); }
  listIpPools():   Observable<IpPoolRow[]>   { return this.http.get<IpPoolRow[]>(`${this.base}/api/net/ip-pools`); }

  /// Hierarchy listings — the endpoints are on Central.Api but the
  /// URL path is the same /api/net/ namespace the engine uses, so
  /// they share the `base`.

  listRegions(): Observable<RegionRow[]> {
    return this.http.get<RegionRow[]>(`${this.base}/api/net/regions`);
  }

  listSites(regionId?: string): Observable<SiteRow[]> {
    let params = new HttpParams();
    if (regionId) params = params.set('regionId', regionId);
    return this.http.get<SiteRow[]>(`${this.base}/api/net/sites`, { params });
  }

  listBuildings(siteId?: string): Observable<BuildingRow[]> {
    let params = new HttpParams();
    if (siteId) params = params.set('siteId', siteId);
    return this.http.get<BuildingRow[]>(`${this.base}/api/net/buildings`, { params });
  }

  listFloors(buildingId?: string): Observable<FloorRow[]> {
    let params = new HttpParams();
    if (buildingId) params = params.set('buildingId', buildingId);
    return this.http.get<FloorRow[]>(`${this.base}/api/net/floors`, { params });
  }

  /// List scope grants, optionally narrowed by userId / action /
  /// entityType.
  listScopeGrants(
    organizationId: string,
    userId?: number,
    action?: string,
    entityType?: string,
  ): Observable<ScopeGrant[]> {
    let params = new HttpParams().set('organizationId', organizationId);
    if (userId !== undefined)   params = params.set('userId', userId.toString());
    if (action)                 params = params.set('action', action);
    if (entityType)             params = params.set('entityType', entityType);
    return this.http.get<ScopeGrant[]>(`${this.base}/api/net/scope-grants`, { params });
  }

  /// Create a scope grant. Server-side requires the caller to hold
  /// write:ScopeGrant — a 403 means the current user can't grant
  /// permissions (break-glass path is direct DB insert by root admin).
  createScopeGrant(body: {
    organizationId: string;
    userId: number;
    action: string;
    entityType: string;
    scopeType: string;
    scopeEntityId?: string;
    notes?: string;
  }): Observable<ScopeGrant> {
    return this.http.post<ScopeGrant>(`${this.base}/api/net/scope-grants`, body);
  }

  /// Soft-delete a scope grant. Server-side requires delete:ScopeGrant
  /// on that grant (or Global).
  deleteScopeGrant(id: string, organizationId: string): Observable<void> {
    const params = new HttpParams().set('organizationId', organizationId);
    return this.http.delete<void>(`${this.base}/api/net/scope-grants/${id}`, { params });
  }

  /// Run the validation rule engine. Empty `ruleCode` runs every
  /// enabled rule; a specific code runs just that rule (useful for
  /// fix-it + re-run-to-confirm flows).
  runValidation(organizationId: string, ruleCode?: string): Observable<ValidationRunResult> {
    const body: Record<string, unknown> = { organizationId };
    if (ruleCode) body['ruleCode'] = ruleCode;
    return this.http.post<ValidationRunResult>(`${this.base}/api/net/validation/run`, body);
  }

  /// Per-entity-type audit activity summary. Single SQL pass with
  /// COUNT + distinct-actor count + last-seen-at, grouped by entity
  /// type. Optional from/to bounds the window; omit both for all-time.
  auditStatsByEntityType(
    organizationId: string,
    fromAt?: string,
    toAt?: string,
  ): Observable<EntityTypeStats[]> {
    let params = new HttpParams().set('organizationId', organizationId);
    if (fromAt) params = params.set('fromAt', fromAt);
    if (toAt)   params = params.set('toAt', toAt);
    return this.http.get<EntityTypeStats[]>(`${this.base}/api/net/audit/stats`, { params });
  }

  /// Generic audit list — filter by entity type / entity id / action /
  /// actor / correlation / date window. Capped at `limit` rows server
  /// side (default 100, engine hard cap 500 for this endpoint).
  /// `beforeSequenceId` is the descending-pagination cursor — pass the
  /// min sequenceId from the previous page to fetch the next one.
  listAudit(
    organizationId: string,
    opts: {
      entityType?: string;
      entityId?: string;
      action?: string;
      actorUserId?: number;
      correlationId?: string;
      fromAt?: string;
      toAt?: string;
      limit?: number;
      beforeSequenceId?: number;
    } = {},
  ): Observable<AuditRow[]> {
    let params = new HttpParams().set('organizationId', organizationId);
    if (opts.entityType)                 params = params.set('entityType',       opts.entityType);
    if (opts.entityId)                   params = params.set('entityId',         opts.entityId);
    if (opts.action)                     params = params.set('action',           opts.action);
    if (opts.actorUserId !== undefined)  params = params.set('actorUserId',      opts.actorUserId.toString());
    if (opts.correlationId)              params = params.set('correlationId',    opts.correlationId);
    if (opts.fromAt)                     params = params.set('fromAt',           opts.fromAt);
    if (opts.toAt)                       params = params.set('toAt',             opts.toAt);
    if (opts.limit !== undefined)        params = params.set('limit',            opts.limit.toString());
    if (opts.beforeSequenceId !== undefined)
      params = params.set('beforeSequenceId', opts.beforeSequenceId.toString());
    return this.http.get<AuditRow[]>(`${this.base}/api/net/audit`, { params });
  }

  /// Fetch the entity's full audit timeline — no 500-row cap that
  /// the generic /api/net/audit list applies.
  getEntityTimeline(
    organizationId: string,
    entityType: string,
    entityId: string,
    limit?: number,
  ): Observable<AuditRow[]> {
    let params = new HttpParams().set('organizationId', organizationId);
    if (limit !== undefined) {
      params = params.set('limit', limit.toString());
    }
    return this.http.get<AuditRow[]>(
      `${this.base}/api/net/audit/entity/${encodeURIComponent(entityType)}/${entityId}`,
      { params },
    );
  }
}
