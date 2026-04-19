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

/// Thin reservation-shelf list row — matches
/// `ReservationShelfListRow` in the engine. `availableAfter` in
/// the past means the cooldown has elapsed + the recycler should
/// have already promoted the row; these are worth flagging in the
/// UI as a "background job not running" hint.
export interface ReservationShelfListRow {
  id: string;
  resourceType: string;
  resourceKey: string;
  poolId: string | null;
  blockId: string | null;
  retiredAt: string;
  availableAfter: string;
  retiredReason: string | null;
  status: string;
  version: number;
}

/// Thin MSTP priority rule list row — matches `MstpRuleListRow`
/// in the engine. stepCount is a correlated subquery on
/// net.mstp_priority_rule_step; empty-rule = step_count == 0.
export interface MstpRuleListRow {
  id: string;
  ruleCode: string;
  displayName: string;
  scopeLevel: string;
  scopeEntityId: string | null;
  stepCount: number;
  status: string;
  version: number;
}

/// Thin module list row — matches `ModuleListRow` in the engine.
/// Represents physical hardware modules (linecards / transceivers
/// / PSUs / fans) inside a device.
export interface ModuleListRow {
  id: string;
  deviceId: string;
  deviceHostname: string | null;
  slot: string;
  moduleType: string;
  model: string | null;
  serialNumber: string | null;
  partNumber: string | null;
  status: string;
  version: number;
}

/// Thin MLAG domain list row — matches `MlagDomainListRow` in
/// the engine. `poolCode` is resolved via LEFT JOIN so the grid
/// shows the parent pool without a second call.
export interface MlagDomainListRow {
  id: string;
  poolId: string;
  poolCode: string | null;
  domainId: number;
  displayName: string;
  scopeLevel: string;
  status: string;
  version: number;
}

/// ASN allocation list row — matches `AsnAllocationListRow` in
/// the engine. `targetDisplay` is resolved via LEFT JOIN on
/// net.device / net.server when `allocatedToType` is Device /
/// Server; null for other target types (e.g. Building).
export interface AsnAllocationListRow {
  id: string;
  blockId: string;
  blockCode: string | null;
  asn: number;
  allocatedToType: string;
  allocatedToId: string;
  targetDisplay: string | null;
  status: string;
}

/// VLAN block list row — matches `VlanBlockListRow` in the engine.
/// `available` = vlan_last - vlan_first + 1 - COUNT(allocated VLANs).
export interface VlanBlockListRow {
  id: string;
  blockCode: string;
  displayName: string;
  vlanFirst: number;
  vlanLast: number;
  available: number;
}

/// ASN block list row — matches `AsnBlockListRow`. Range fields are
/// bigint on the wire (string-safe JSON numbers for 2-byte ASN ranges
/// but bigint-fine for 4-byte 32-bit ASNs).
export interface AsnBlockListRow {
  id: string;
  blockCode: string;
  displayName: string;
  asnFirst: number;
  asnLast: number;
  available: number;
}

/// Thin aggregate-ethernet list row — matches
/// `AggregateEthernetListRow` in the engine. memberCount is a
/// correlated subquery on net.port so under-populated bundles
/// (member < min_links) surface in the grid without a second query.
export interface AggregateEthernetListRow {
  id: string;
  deviceId: string;
  deviceHostname: string | null;
  aeName: string;
  lacpMode: string;
  minLinks: number;
  memberCount: number;
  description: string | null;
  status: string;
  version: number;
}

/// Thin port-list row — matches `PortListRow` in the engine.
/// Device hostname resolved via LEFT JOIN. Natural sort handling
/// (xe-1/1/2 before xe-1/1/10) is a client-side concern; the
/// engine just returns alphabetical ORDER BY so `xe-1/1/10` sorts
/// before `xe-1/1/2` on the wire.
export interface PortListRow {
  id: string;
  deviceId: string;
  deviceHostname: string | null;
  interfaceName: string;
  interfacePrefix: string;
  speedMbps: number | null;
  adminUp: boolean;
  description: string | null;
  portMode: string;
  nativeVlanId: number | null;
  status: string;
  version: number;
}

/// Thin link-endpoint list row — matches `LinkEndpointListRow` in
/// the engine. LEFT JOINs resolve device hostname, port
/// interface_name (from net.port), the free-text interface_name
/// column (fallback), IP host string, and VLAN tag. Endpoint order
/// 0 = A-side, 1 = B-side by convention (hub/spoke link types
/// may use higher values).
export interface LinkEndpointListRow {
  id: string;
  linkId: string;
  linkCode: string | null;
  endpointOrder: number;
  deviceHostname: string | null;
  portInterface: string | null;
  interfaceName: string | null;
  ipAddress: string | null;
  vlanTag: number | null;
  description: string | null;
  status: string;
}

/// Thin server-NIC list row — matches `ServerNicListRow` in the
/// engine. LEFT JOINs resolve display fields (server hostname,
/// target device hostname, port interface, IP host string, MAC)
/// so the grid renders without per-row round-trips.
export interface ServerNicListRow {
  id: string;
  serverId: string;
  serverHostname: string | null;
  nicIndex: number;
  nicName: string | null;
  mlagSide: string | null;
  targetDeviceHostname: string | null;
  targetPortInterface: string | null;
  ipAddress: string | null;
  macAddress: string | null;
  adminUp: boolean;
  status: string;
}

/// Thin IP-address list row — matches `IpAddressListRow` in the
/// engine. `address` is pre-rendered as a bare host string via
/// `host(ip.address)` on the server — no CIDR suffix.
export interface IpAddressListRow {
  id: string;
  subnetId: string;
  subnetCode: string | null;
  address: string;
  assignedToType: string | null;
  assignedToId: string | null;
  isReserved: boolean;
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

/// One point in an audit trend series — bucketed timestamp + count.
/// Matches `AuditTrendPoint` in services/networking-engine/src/audit.rs.
export interface AuditTrendPoint {
  bucketAt: string;
  count: number;
}

/// One actor's audit activity rollup. Matches `TopActor` in
/// services/networking-engine/src/audit.rs. `actorUserId` +
/// `actorDisplay` are both nullable for service-origin rows.
export interface TopActor {
  actorUserId: number | null;
  actorDisplay: string | null;
  totalCount: number;
  distinctEntityTypes: number;
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

/// Who-am-i summary — matches `WhoAmIResponse` in the engine.
/// Drives the web session banner. Service calls (no X-User-Id
/// header) get userId=null + grantCount=0 + empty arrays.
export interface WhoAmI {
  userId: number | null;
  grantCount: number;
  actions: string[];
  entityTypes: string[];
}

/// Permission-check decision returned by /api/net/scope-grants/check.
/// Matches `PermissionDecision` in the engine. `matchedGrantId` is
/// the uuid of the specific grant that authorised the action — nice
/// for "why was this allowed?" feedback. None when `allowed = false`.
export interface PermissionDecision {
  allowed: boolean;
  matchedGrantId: string | null;
}

/// CLI flavor metadata + resolved tenant override. Matches
/// `ResolvedFlavor` in the engine (which `#[serde(flatten)]`s the
/// inner `FlavorMeta` alongside the resolver fields). Status is
/// one of "Ga" / "Beta" / "Stub"; `effectiveEnabled` accounts for
/// the tenant override falling back to `defaultEnabled`.
export interface ResolvedCliFlavor {
  code: string;
  displayName: string;
  vendor: string;
  description: string;
  status: string;
  defaultEnabled: boolean;
  effectiveEnabled: boolean;
  isDefault: boolean;
  hasTenantRow: boolean;
  updatedAt: string | null;
  notes: string | null;
}

/// Pool utilization row — matches `PoolUtilizationRow` in the
/// engine. `poolKind` is one of "ASN" / "VLAN" / "IP:Subnets" /
/// "IP:Addresses". `percentFull` caps at 999 for data-quality
/// outliers; `capacity` is 0 when the kind doesn't report a
/// capacity dimension (IP:Subnets row only reports subnet count).
export interface PoolUtilizationRow {
  poolKind: string;
  poolId: string;
  poolCode: string;
  displayName: string;
  used: number;
  capacity: number;
  percentFull: number;
  status: string;
}

/// Render history row — matches `RenderedConfigSummary` in the
/// engine (no body; the list endpoint omits the body to keep
/// payload sizes down on tenants with many renders).
export interface RenderedConfigSummary {
  id: string;
  deviceId: string;
  flavorCode: string;
  bodySha256: string;
  lineCount: number;
  renderDurationMs: number | null;
  previousRenderId: string | null;
  renderedAt: string;
  renderedBy: number | null;
}

/// Full render record with body — matches `RenderedConfigRecord`.
/// Used by the detail + diff flows.
export interface RenderedConfigRecord {
  id: string;
  deviceId: string;
  flavorCode: string;
  body: string;
  bodySha256: string;
  lineCount: number;
  renderDurationMs: number | null;
  previousRenderId: string | null;
  renderedAt: string;
  renderedBy: number | null;
}

/// Per-device error inside a building / site / region turn-up pack.
/// Matches `DeviceRenderError` — hostname is denormalised for the
/// UI so a broken template on device 17 shows "MEP-91-SW17" rather
/// than a uuid the operator has to cross-reference.
export interface DeviceRenderError {
  deviceId: string;
  hostname: string;
  error: string;
}

/// Turn-up pack result — matches `BuildingRenderResult` /
/// `SiteRenderResult` / `RegionRenderResult`. Same shape across
/// scopes so one grid + summary card renders them uniformly.
export interface RenderPackResult {
  buildingId?: string;
  buildingCode?: string | null;
  siteId?: string;
  siteCode?: string | null;
  regionId?: string;
  regionCode?: string | null;
  totalDevices: number;
  succeeded: number;
  failed: number;
  renders: RenderedConfigResponse[];
  errors: DeviceRenderError[];
}

/// Freshly-rendered config returned from POST
/// /api/net/devices/:id/render-config. Same wire shape as
/// `RenderedConfig` in the engine — id + previousRenderId are
/// serialized only when non-null, so declare them as optional.
export interface RenderedConfigResponse {
  deviceId: string;
  flavorCode: string;
  body: string;
  bodySha256: string;
  lineCount: number;
  renderedAt: string;
  id?: string;
  previousRenderId?: string;
  renderDurationMs?: number;
}

/// Line-set diff between a render + its previous_render_id chain.
/// Matches `RenderDiff` in the engine. Added / removed in source-
/// body order; unchangedCount is just the cardinality for a "how
/// stable is this config" scan without shipping the whole body.
export interface RenderDiff {
  renderId: string;
  previousRenderId: string | null;
  added: string[];
  removed: string[];
  unchangedCount: number;
}

/// Naming template override row — matches `NamingOverride` in
/// services/networking-engine/src/naming_overrides.rs. Overrides
/// let admins replace the default template on
/// net.{device_role,link_type,server_profile} at a scope
/// (Global / Region / Site / Building) with or without a subtype
/// discriminator.
export interface NamingOverride {
  id: string;
  organizationId: string;
  entityType: string;
  subtypeCode: string | null;
  scopeLevel: string;
  scopeEntityId: string | null;
  namingTemplate: string;
  status: string;
  version: number;
  createdAt: string;
  updatedAt: string;
  notes: string | null;
}

/// Naming context shapes — match `LinkNamingContext` /
/// `DeviceNamingContext` / `ServerNamingContext` in the engine.
/// `instance` / `vlan_id` are nullable numbers; every string token
/// is nullable so partial contexts render reasonably.
export interface LinkNamingContext {
  siteA?: string | null;
  siteB?: string | null;
  deviceA?: string | null;
  deviceB?: string | null;
  portA?: string | null;
  portB?: string | null;
  roleA?: string | null;
  roleB?: string | null;
  vlanId?: number | null;
  subnet?: string | null;
  description?: string | null;
  linkCode?: string | null;
}
export interface DeviceNamingContext {
  regionCode?: string | null;
  siteCode?: string | null;
  buildingCode?: string | null;
  rackCode?: string | null;
  roleCode?: string | null;
  instance?: number | null;
  instancePadding?: number;
}
export interface ServerNamingContext {
  regionCode?: string | null;
  siteCode?: string | null;
  buildingCode?: string | null;
  rackCode?: string | null;
  profileCode?: string | null;
  instance?: number | null;
  instancePadding?: number;
}

/// Response envelope from any naming-preview endpoint. The engine
/// returns `{ expanded: "..." }` regardless of which flavour was
/// previewed.
export interface NamingPreviewResponse {
  expanded: string;
}

/// Naming-resolve response — matches `ResolveResponse` in the
/// engine's naming_resolver module. `source` is PascalCase enum:
/// BuildingSpecificSubtype / BuildingAnySubtype / Site* / Region* /
/// Global* / Default. `overrideId` is null for the Default case.
export interface NamingResolveResponse {
  template: string;
  source: string;
  overrideId: string | null;
}

/// One locked row — matches `LockedRow` in the engine. Projection
/// is uniform across the 5 numbering tables that lock enforcement
/// covers (asn_allocation / vlan / mlag_domain / subnet / ip_address);
/// `tableName` disambiguates.
export interface LockedRow {
  id: string;
  tableName: string;
  displayLabel: string;
  lockState: string;
  lockReason: string | null;
  lockedBy: number | null;
  lockedAt: string | null;
  version: number;
}

/// Change-set lifecycle row — matches `ChangeSet` in the engine. Status
/// values are PascalCase on the wire (`#[serde(rename_all = "PascalCase")]`):
/// Draft / Submitted / Approved / Rejected / Applied / RolledBack /
/// Cancelled. `itemCount` is a computed COUNT aggregate, not a
/// stored column.
export interface ChangeSet {
  id: string;
  organizationId: string;
  title: string;
  description: string | null;
  status: string;
  requestedBy: number | null;
  requestedByDisplay: string | null;
  submittedBy: number | null;
  submittedAt: string | null;
  approvedAt: string | null;
  appliedAt: string | null;
  rolledBackAt: string | null;
  cancelledAt: string | null;
  requiredApprovals: number | null;
  correlationId: string;
  version: number;
  itemCount: number;
  createdAt: string;
  updatedAt: string;
}

/// One item inside a change-set's item list — matches the engine's
/// `ChangeSetItem` struct. `beforeJson` / `afterJson` are raw JSONB
/// snapshots taken at Set-submit time; the web detail page renders
/// them as pretty-printed code blocks.
export interface ChangeSetItem {
  id: string;
  changeSetId: string;
  itemOrder: number;
  entityType: string;
  entityId: string | null;
  action: string;
  beforeJson: unknown | null;
  afterJson: unknown | null;
  expectedVersion: number | null;
  appliedAt: string | null;
  applyError: string | null;
  notes: string | null;
  createdAt: string;
}

/// Envelope returned by GET /api/net/change-sets/:id — the header
/// row + the items array in one shot. Matches `ChangeSetWithItems`
/// in the engine.
export interface ChangeSetWithItems {
  set: ChangeSet;
  items: ChangeSetItem[];
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

  /// Who-am-i summary for the current user — identity + scope-grant
  /// count + distinct actions + distinct entity types. Drives the
  /// session banner.
  whoAmI(organizationId: string): Observable<WhoAmI> {
    const params = new HttpParams().set('organizationId', organizationId);
    return this.http.get<WhoAmI>(`${this.base}/api/net/whoami`, { params });
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

  /// Update a saved view. Name collision 409, ownership 404 — same
  /// semantics as create + delete. version drives optimistic
  /// concurrency; pass the current row's version to detect stale
  /// client state.
  updateSavedView(id: string, body: {
    organizationId: string;
    name: string;
    q: string;
    entityTypes?: string | null;
    notes?: string | null;
    version: number;
  }): Observable<SavedView> {
    const params = new HttpParams().set('organizationId', body.organizationId);
    return this.http.put<SavedView>(`${this.base}/api/net/saved-views/${id}`, {
      name:        body.name,
      q:           body.q,
      entityTypes: body.entityTypes ?? null,
      notes:       body.notes ?? null,
      filters:     {},
      version:     body.version,
    }, { params });
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

  /// Thin IP-address list — 5000 row cap. Optional `subnetId`
  /// narrows to one subnet (the subnet-detail page's drill).
  /// Address pre-rendered as a bare host string by the engine.
  listIpAddresses(
    organizationId: string,
    subnetId?: string,
  ): Observable<IpAddressListRow[]> {
    let params = new HttpParams().set('organizationId', organizationId);
    if (subnetId) params = params.set('subnetId', subnetId);
    return this.http.get<IpAddressListRow[]>(
      `${this.base}/api/net/ip-addresses`, { params });
  }

  /// Thin server-NIC list — 5000 row cap. Optional `serverId`
  /// narrows to one server (the server-detail NICs tab drill).
  /// Target device + port + IP resolved server-side.
  listServerNics(
    organizationId: string,
    serverId?: string,
  ): Observable<ServerNicListRow[]> {
    let params = new HttpParams().set('organizationId', organizationId);
    if (serverId) params = params.set('serverId', serverId);
    return this.http.get<ServerNicListRow[]>(
      `${this.base}/api/net/server-nics`, { params });
  }

  /// Thin link-endpoint list — 5000 row cap. Optional `linkId`
  /// narrows to one link (the link-detail Endpoints tab drill).
  /// Device hostname + port interface + IP + VLAN tag resolved.
  listLinkEndpoints(
    organizationId: string,
    linkId?: string,
  ): Observable<LinkEndpointListRow[]> {
    let params = new HttpParams().set('organizationId', organizationId);
    if (linkId) params = params.set('linkId', linkId);
    return this.http.get<LinkEndpointListRow[]>(
      `${this.base}/api/net/link-endpoints`, { params });
  }

  /// Thin port list — 5000 row cap. Optional `deviceId` narrows to
  /// one device (the device-detail Ports tab drill).
  listPorts(
    organizationId: string,
    deviceId?: string,
  ): Observable<PortListRow[]> {
    let params = new HttpParams().set('organizationId', organizationId);
    if (deviceId) params = params.set('deviceId', deviceId);
    return this.http.get<PortListRow[]>(
      `${this.base}/api/net/ports`, { params });
  }

  /// Thin MLAG-domain list — 5000 row cap. LEFT JOIN resolves
  /// pool_code for display.
  listMlagDomains(organizationId: string): Observable<MlagDomainListRow[]> {
    const params = new HttpParams().set('organizationId', organizationId);
    return this.http.get<MlagDomainListRow[]>(
      `${this.base}/api/net/mlag-domains`, { params });
  }

  /// Thin MSTP priority rule list. 5000-row cap; stepCount from
  /// correlated subquery flags empty rules.
  listMstpRules(organizationId: string): Observable<MstpRuleListRow[]> {
    const params = new HttpParams().set('organizationId', organizationId);
    return this.http.get<MstpRuleListRow[]>(
      `${this.base}/api/net/mstp-rules`, { params });
  }

  /// Thin reservation-shelf list. Ordered retired_at DESC — freshest
  /// retirements at the top; check availableAfter < now() for rows
  /// that should have been recycled by a background job.
  listReservationShelf(organizationId: string): Observable<ReservationShelfListRow[]> {
    const params = new HttpParams().set('organizationId', organizationId);
    return this.http.get<ReservationShelfListRow[]>(
      `${this.base}/api/net/reservation-shelf`, { params });
  }

  /// Thin module list — 5000 row cap. Optional deviceId narrower.
  listModules(
    organizationId: string,
    deviceId?: string,
  ): Observable<ModuleListRow[]> {
    let params = new HttpParams().set('organizationId', organizationId);
    if (deviceId) params = params.set('deviceId', deviceId);
    return this.http.get<ModuleListRow[]>(
      `${this.base}/api/net/modules`, { params });
  }

  /// Thin aggregate-ethernet list — 5000 row cap. Optional deviceId
  /// narrower. memberCount (from correlated subquery) makes
  /// "bundle is below min_links" scans trivial.
  listAggregateEthernet(
    organizationId: string,
    deviceId?: string,
  ): Observable<AggregateEthernetListRow[]> {
    let params = new HttpParams().set('organizationId', organizationId);
    if (deviceId) params = params.set('deviceId', deviceId);
    return this.http.get<AggregateEthernetListRow[]>(
      `${this.base}/api/net/aggregate-ethernet`, { params });
  }

  /// List change-sets, optionally narrowed to a status. Capped at
  /// `limit` server-side (default 50 from the engine; 1..=500
  /// range). Ordered by updated_at DESC so the freshest sit at top.
  listChangeSets(
    organizationId: string,
    status?: string,
    limit?: number,
  ): Observable<ChangeSet[]> {
    let params = new HttpParams().set('organizationId', organizationId);
    if (status)                   params = params.set('status', status);
    if (limit !== undefined)      params = params.set('limit',  limit.toString());
    return this.http.get<ChangeSet[]>(`${this.base}/api/net/change-sets`, { params });
  }

  /// List CLI flavors with tenant-override resolution. Always
  /// returns one row per catalog entry so the UI can show every
  /// flavor even when no tenant override exists.
  listCliFlavors(organizationId: string): Observable<ResolvedCliFlavor[]> {
    const params = new HttpParams().set('organizationId', organizationId);
    return this.http.get<ResolvedCliFlavor[]>(
      `${this.base}/api/net/cli-flavors`, { params });
  }

  /// Upsert a tenant's config for one flavor. Pass `enabled`,
  /// `isDefault` (server enforces one-default-per-tenant by
  /// clearing the flag on any other row), or `notes` in the body.
  setCliFlavorConfig(
    code: string,
    organizationId: string,
    body: { enabled?: boolean; isDefault?: boolean; notes?: string | null },
  ): Observable<void> {
    const params = new HttpParams().set('organizationId', organizationId);
    return this.http.put<void>(
      `${this.base}/api/net/cli-flavors/${code}`, body, { params });
  }

  /// Pool utilization rollup across ASN / VLAN / IP pools. One
  /// call returns every pool family (IP pools emit two rows: one
  /// for subnets, one for addresses).
  poolUtilization(organizationId: string): Observable<PoolUtilizationRow[]> {
    const params = new HttpParams().set('organizationId', organizationId);
    return this.http.get<PoolUtilizationRow[]>(
      `${this.base}/api/net/pools/utilization`, { params });
  }

  /// Turn-up pack: render + persist every device in a building.
  /// Per-device errors are tolerated — surface in `errors` array.
  renderBuildingConfigs(
    buildingId: string, organizationId: string,
  ): Observable<RenderPackResult> {
    const params = new HttpParams().set('organizationId', organizationId);
    return this.http.post<RenderPackResult>(
      `${this.base}/api/net/buildings/${buildingId}/render-configs`, null, { params });
  }

  /// Turn-up pack: render every device in every building in the site.
  renderSiteConfigs(
    siteId: string, organizationId: string,
  ): Observable<RenderPackResult> {
    const params = new HttpParams().set('organizationId', organizationId);
    return this.http.post<RenderPackResult>(
      `${this.base}/api/net/sites/${siteId}/render-configs`, null, { params });
  }

  /// Turn-up pack: render every device in the region. Biggest scope.
  renderRegionConfigs(
    regionId: string, organizationId: string,
  ): Observable<RenderPackResult> {
    const params = new HttpParams().set('organizationId', organizationId);
    return this.http.post<RenderPackResult>(
      `${this.base}/api/net/regions/${regionId}/render-configs`, null, { params });
  }

  /// Trigger a fresh render for one device. Persists to
  /// net.rendered_config + chains via previousRenderId. Requires
  /// write:Device on the target device.
  renderDeviceConfig(
    deviceId: string,
    organizationId: string,
  ): Observable<RenderedConfigResponse> {
    const params = new HttpParams().set('organizationId', organizationId);
    return this.http.post<RenderedConfigResponse>(
      `${this.base}/api/net/devices/${deviceId}/render-config`, null, { params });
  }

  /// List render history for one device. Capped server-side at
  /// 500 rows (engine `clamp_render_list_limit`); default 50.
  /// Ordered rendered_at DESC so the freshest is first.
  listDeviceRenders(
    deviceId: string,
    organizationId: string,
    limit?: number,
  ): Observable<RenderedConfigSummary[]> {
    let params = new HttpParams().set('organizationId', organizationId);
    if (limit !== undefined) params = params.set('limit', limit.toString());
    return this.http.get<RenderedConfigSummary[]>(
      `${this.base}/api/net/devices/${deviceId}/renders`, { params });
  }

  /// Fetch one render with its full body.
  getRender(renderId: string, organizationId: string): Observable<RenderedConfigRecord> {
    const params = new HttpParams().set('organizationId', organizationId);
    return this.http.get<RenderedConfigRecord>(
      `${this.base}/api/net/renders/${renderId}`, { params });
  }

  /// Fetch the line-set diff between a render + its chained
  /// previousRenderId. Returns an empty diff + null previousRenderId
  /// when the render has no predecessor (the very first render).
  getRenderDiff(renderId: string, organizationId: string): Observable<RenderDiff> {
    const params = new HttpParams().set('organizationId', organizationId);
    return this.http.get<RenderDiff>(
      `${this.base}/api/net/renders/${renderId}/diff`, { params });
  }

  /// List naming template overrides. Optional entityType +
  /// scopeLevel filters — useful for scoping the UI to one
  /// entity family without client-side filtering.
  listNamingOverrides(
    organizationId: string,
    entityType?: string,
    scopeLevel?: string,
  ): Observable<NamingOverride[]> {
    let params = new HttpParams().set('organizationId', organizationId);
    if (entityType) params = params.set('entityType', entityType);
    if (scopeLevel) params = params.set('scopeLevel', scopeLevel);
    return this.http.get<NamingOverride[]>(
      `${this.base}/api/net/naming/overrides`, { params });
  }

  /// Create a naming override. Server validates scope_level +
  /// scope_entity_id consistency (Global requires null id; other
  /// levels require an id that resolves to the right hierarchy row).
  createNamingOverride(body: {
    organizationId: string;
    entityType: string;
    subtypeCode?: string | null;
    scopeLevel: string;
    scopeEntityId?: string | null;
    namingTemplate: string;
    notes?: string | null;
  }): Observable<NamingOverride> {
    return this.http.post<NamingOverride>(
      `${this.base}/api/net/naming/overrides`, body);
  }

  /// Update an override's naming_template + notes. Optimistic
  /// concurrency via version.
  updateNamingOverride(id: string, body: {
    organizationId: string;
    namingTemplate: string;
    notes?: string | null;
    version: number;
  }): Observable<NamingOverride> {
    const params = new HttpParams().set('organizationId', body.organizationId);
    return this.http.put<NamingOverride>(
      `${this.base}/api/net/naming/overrides/${id}`,
      { namingTemplate: body.namingTemplate, notes: body.notes ?? null, version: body.version },
      { params });
  }

  /// Soft-delete a naming override.
  deleteNamingOverride(id: string, organizationId: string): Observable<void> {
    const params = new HttpParams().set('organizationId', organizationId);
    return this.http.delete<void>(
      `${this.base}/api/net/naming/overrides/${id}`, { params });
  }

  /// Resolve which naming template applies to an entity at the
  /// given hierarchy position. Walks the override precedence:
  /// Building-specific → Building-any → Site-specific → Site-any →
  /// Region-specific → Region-any → Global-specific → Global-any →
  /// Default (from the caller's defaultTemplate). Answer shows
  /// both the template + which tier won.
  resolveNamingTemplate(body: {
    organizationId: string;
    entityType: string;
    subtypeCode?: string | null;
    regionId?: string | null;
    siteId?: string | null;
    buildingId?: string | null;
    defaultTemplate?: string | null;
  }): Observable<NamingResolveResponse> {
    return this.http.post<NamingResolveResponse>(
      `${this.base}/api/net/naming/resolve`, {
        organizationId:   body.organizationId,
        entityType:       body.entityType,
        subtypeCode:      body.subtypeCode ?? null,
        regionId:         body.regionId ?? null,
        siteId:           body.siteId ?? null,
        buildingId:       body.buildingId ?? null,
        defaultTemplate:  body.defaultTemplate ?? null,
      });
  }

  /// Naming preview — one endpoint per context shape. Each POSTs
  /// `{ template, context }` and returns `{ expanded }`. No tenant
  /// dependency (pure token substitution), so these don't thread
  /// organizationId.
  previewLinkName(template: string, context: LinkNamingContext): Observable<NamingPreviewResponse> {
    return this.http.post<NamingPreviewResponse>(
      `${this.base}/api/net/naming/link/preview`, { template, context });
  }
  previewDeviceName(template: string, context: DeviceNamingContext): Observable<NamingPreviewResponse> {
    return this.http.post<NamingPreviewResponse>(
      `${this.base}/api/net/naming/device/preview`, { template, context });
  }
  previewServerName(template: string, context: ServerNamingContext): Observable<NamingPreviewResponse> {
    return this.http.post<NamingPreviewResponse>(
      `${this.base}/api/net/naming/server/preview`, { template, context });
  }

  /// List locked rows across the 5 numbering tables (asn_allocation,
  /// vlan, mlag_domain, subnet, ip_address). Defaults to every
  /// non-Open state; optional table + lockState narrowers.
  listLockedRows(
    organizationId: string,
    table?: string,
    lockState?: string,
  ): Observable<LockedRow[]> {
    let params = new HttpParams().set('organizationId', organizationId);
    if (table)     params = params.set('table', table);
    if (lockState) params = params.set('lockState', lockState);
    return this.http.get<LockedRow[]>(`${this.base}/api/net/locks`, { params });
  }

  /// PATCH the lock state on one row. `table` must be one of the
  /// five whitelisted tables; `lockState` transitions are validated
  /// server-side (Immutable is terminal — can't be loosened; other
  /// transitions are permitted). 400 on a disallowed transition.
  setEntityLock(
    table: string,
    id: string,
    organizationId: string,
    body: { lockState: string; lockReason?: string | null },
  ): Observable<LockedRow> {
    const params = new HttpParams().set('organizationId', organizationId);
    return this.http.patch<LockedRow>(
      `${this.base}/api/net/locks/${encodeURIComponent(table)}/${id}`,
      { lockState: body.lockState, lockReason: body.lockReason ?? null },
      { params });
  }

  /// Create a new Draft change-set. Items are added separately via
  /// POST /api/net/change-sets/:id/items (not yet exposed on the
  /// web client — the WPF client adds items in a follow-up flow).
  createChangeSet(body: {
    organizationId: string;
    title: string;
    description?: string | null;
    requestedByDisplay?: string | null;
  }): Observable<ChangeSet> {
    return this.http.post<ChangeSet>(`${this.base}/api/net/change-sets`, {
      organizationId:     body.organizationId,
      title:              body.title,
      description:        body.description ?? null,
      requestedByDisplay: body.requestedByDisplay ?? null,
    });
  }

  /// Submit a Draft change-set for approval. `requiredApprovals`
  /// caps at >=1 server-side to prevent accidental auto-approval.
  submitChangeSet(
    id: string, organizationId: string, requiredApprovals = 1,
  ): Observable<ChangeSet> {
    const params = new HttpParams().set('organizationId', organizationId);
    return this.http.post<ChangeSet>(
      `${this.base}/api/net/change-sets/${id}/submit`,
      { requiredApprovals }, { params });
  }

  /// Cancel a change-set. Notes optional. Terminal state — can't
  /// reopen a cancelled set.
  cancelChangeSet(
    id: string, organizationId: string, notes?: string,
  ): Observable<ChangeSet> {
    const params = new HttpParams().set('organizationId', organizationId);
    const body = notes ? { notes } : {};
    return this.http.post<ChangeSet>(
      `${this.base}/api/net/change-sets/${id}/cancel`,
      body, { params });
  }

  /// Apply an Approved change-set. Runs every item in item_order
  /// sequence inside a transaction; partial apply on error rolls back.
  applyChangeSet(
    id: string, organizationId: string,
  ): Observable<ChangeSet> {
    const params = new HttpParams().set('organizationId', organizationId);
    return this.http.post<ChangeSet>(
      `${this.base}/api/net/change-sets/${id}/apply`,
      null, { params });
  }

  /// Record an Approve / Reject decision on a Submitted change-set.
  /// decision is PascalCase on the wire ('Approve' | 'Reject' —
  /// matches the engine's ChangeSetDecision enum serde config).
  recordChangeSetDecision(
    id: string, organizationId: string, body: {
      decision: 'Approve' | 'Reject';
      approverDisplay?: string | null;
      notes?: string | null;
    },
  ): Observable<unknown> {
    const params = new HttpParams().set('organizationId', organizationId);
    return this.http.post(
      `${this.base}/api/net/change-sets/${id}/decisions`,
      {
        decision:        body.decision,
        approverDisplay: body.approverDisplay ?? null,
        notes:           body.notes ?? null,
      },
      { params });
  }

  /// Add an item to a Draft change-set. `action` is one of
  /// Create / Update / Delete / Rename. beforeJson is NULL on
  /// Create; afterJson is NULL on Delete. Returns the created item.
  addChangeSetItem(id: string, organizationId: string, body: {
    entityType: string;
    entityId?: string | null;
    action: string;
    beforeJson?: unknown;
    afterJson?: unknown;
    expectedVersion?: number | null;
    notes?: string | null;
  }): Observable<ChangeSetItem> {
    const params = new HttpParams().set('organizationId', organizationId);
    return this.http.post<ChangeSetItem>(
      `${this.base}/api/net/change-sets/${id}/items`,
      {
        entityType:       body.entityType,
        entityId:         body.entityId ?? null,
        action:           body.action,
        beforeJson:       body.beforeJson ?? null,
        afterJson:        body.afterJson ?? null,
        expectedVersion:  body.expectedVersion ?? null,
        notes:            body.notes ?? null,
      },
      { params });
  }

  /// Fetch one change-set with its full item list.
  getChangeSet(id: string, organizationId: string): Observable<ChangeSetWithItems> {
    const params = new HttpParams().set('organizationId', organizationId);
    return this.http.get<ChangeSetWithItems>(
      `${this.base}/api/net/change-sets/${id}`, { params });
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

  /// Create a DHCP relay target. serverIp is the bare host string
  /// (`"10.11.120.10"`); priority defaults to 10 server-side.
  createDhcpRelayTarget(body: {
    organizationId: string;
    vlanId: string;
    serverIp: string;
    priority?: number;
    notes?: string | null;
  }): Observable<DhcpRelayTargetRow> {
    return this.http.post<DhcpRelayTargetRow>(
      `${this.base}/api/net/dhcp-relay-targets`, body);
  }

  /// Update a relay target — priority + notes + optional ipAddressId.
  /// Optimistic concurrency via version (pass the row's current value).
  updateDhcpRelayTarget(id: string, body: {
    organizationId: string;
    priority: number;
    notes?: string | null;
    ipAddressId?: string | null;
    version: number;
  }): Observable<DhcpRelayTargetRow> {
    const params = new HttpParams().set('organizationId', body.organizationId);
    return this.http.put<DhcpRelayTargetRow>(
      `${this.base}/api/net/dhcp-relay-targets/${id}`,
      {
        priority:    body.priority,
        notes:       body.notes ?? null,
        ipAddressId: body.ipAddressId ?? null,
        version:     body.version,
      }, { params });
  }

  /// Soft-delete a DHCP relay target.
  deleteDhcpRelayTarget(id: string, organizationId: string): Observable<void> {
    const params = new HttpParams().set('organizationId', organizationId);
    return this.http.delete<void>(
      `${this.base}/api/net/dhcp-relay-targets/${id}`, { params });
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

  /// Thin ASN allocation list — 5000-row cap, ORDER BY asn ASC
  /// so gaps in the allocated set are visible.
  listAsnAllocations(organizationId: string): Observable<AsnAllocationListRow[]> {
    const params = new HttpParams().set('organizationId', organizationId);
    return this.http.get<AsnAllocationListRow[]>(
      `${this.base}/api/net/asn-allocations`, { params });
  }

  /// Engine-backed thin VLAN block list with `available` count.
  /// Distinct from listVlanBlocks (Central.Api PascalCase) — this
  /// one threads organizationId + returns the engine's
  /// VlanBlockListRow with the availability subquery.
  listVlanBlockAvailability(organizationId: string): Observable<VlanBlockListRow[]> {
    const params = new HttpParams().set('organizationId', organizationId);
    return this.http.get<VlanBlockListRow[]>(
      `${this.base}/api/net/vlan-blocks`, { params });
  }

  /// Engine-backed thin ASN block list with `available` count.
  listAsnBlockAvailability(organizationId: string): Observable<AsnBlockListRow[]> {
    const params = new HttpParams().set('organizationId', organizationId);
    return this.http.get<AsnBlockListRow[]>(
      `${this.base}/api/net/asn-blocks`, { params });
  }

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

  /// Dry-run the permission resolver without enforcing. Useful for
  /// "would user X be allowed to do action Y on entityType Z?"
  /// lookups — e.g. pre-save form feedback or post-grant verification.
  checkPermission(opts: {
    organizationId: string;
    userId: number;
    action: string;
    entityType: string;
    entityId?: string;
  }): Observable<PermissionDecision> {
    let params = new HttpParams()
      .set('organizationId', opts.organizationId)
      .set('userId',         opts.userId.toString())
      .set('action',         opts.action)
      .set('entityType',     opts.entityType);
    if (opts.entityId) params = params.set('entityId', opts.entityId);
    return this.http.get<PermissionDecision>(
      `${this.base}/api/net/scope-grants/check`, { params });
  }

  /// Run the validation rule engine. Empty `ruleCode` runs every
  /// enabled rule; a specific code runs just that rule (useful for
  /// fix-it + re-run-to-confirm flows).
  runValidation(organizationId: string, ruleCode?: string): Observable<ValidationRunResult> {
    const body: Record<string, unknown> = { organizationId };
    if (ruleCode) body['ruleCode'] = ruleCode;
    return this.http.post<ValidationRunResult>(`${this.base}/api/net/validation/run`, body);
  }

  /// Top N audit actors by count in the window. Clamped server-side
  /// to `[1, 100]`; default 20 when omitted.
  auditTopActors(
    organizationId: string,
    opts: {
      fromAt?: string;
      toAt?: string;
      entityType?: string;
      limit?: number;
    } = {},
  ): Observable<TopActor[]> {
    let params = new HttpParams().set('organizationId', organizationId);
    if (opts.fromAt)     params = params.set('fromAt',     opts.fromAt);
    if (opts.toAt)       params = params.set('toAt',       opts.toAt);
    if (opts.entityType) params = params.set('entityType', opts.entityType);
    if (opts.limit !== undefined) params = params.set('limit', opts.limit.toString());
    return this.http.get<TopActor[]>(`${this.base}/api/net/audit/top-actors`, { params });
  }

  /// Time-bucketed audit activity. `bucketBy` accepts hour / day /
  /// week (defaults to day on the engine). Optional entityType
  /// narrows to one entity type for focused drill-downs; omit for
  /// total activity.
  auditTrend(
    organizationId: string,
    opts: {
      fromAt?: string;
      toAt?: string;
      bucketBy?: 'hour' | 'day' | 'week';
      entityType?: string;
    } = {},
  ): Observable<AuditTrendPoint[]> {
    let params = new HttpParams().set('organizationId', organizationId);
    if (opts.fromAt)     params = params.set('fromAt',     opts.fromAt);
    if (opts.toAt)       params = params.set('toAt',       opts.toAt);
    if (opts.bucketBy)   params = params.set('bucketBy',   opts.bucketBy);
    if (opts.entityType) params = params.set('entityType', opts.entityType);
    return this.http.get<AuditTrendPoint[]>(`${this.base}/api/net/audit/trend`, { params });
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
