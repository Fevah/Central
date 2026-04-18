//! Axum routing. Mirrors the surface the desktop's `Central.ApiClient.NetworkingEngine`
//! wrapper will call. Every handler delegates straight to a service type — handlers
//! stay thin so the tests focus on the services, not the transport.

use axum::{
    extract::{Path, State, Query},
    http::{HeaderMap, StatusCode},
    response::{IntoResponse, Response},
    routing::{get, post},
    Json, Router,
};
use chrono::Duration;
use serde::{Deserialize, Serialize};
use sqlx::PgPool;
use uuid::Uuid;

use crate::allocation::AllocationService;
use crate::audit::{self, ExportQuery, ListAuditQuery, VerifyChainQuery};
use crate::change_sets::{
    AddItemBody, CancelBody, ChangeSetRepo, CreateChangeSetBody, DecisionBody,
    GetChangeSetQuery, ListChangeSetsQuery, SubmitBody,
};
use crate::error::EngineError;
use crate::ip_allocation::IpAllocationService;
use crate::locks::{self, ListLockedQuery, SetLockBody};
use crate::models::{PoolScopeLevel, ShelfResourceType};
use crate::naming::{self, DeviceNamingContext, LinkNamingContext, ServerNamingContext};
use crate::naming_overrides::{
    CreateOverrideBody, ListOverridesQuery, NamingOverrideRepo, UpdateOverrideBody,
};
use crate::naming_resolver::{NamingResolver, ResolveRequest};
use crate::regenerate::{self, RegenerateApplyRequest, RegeneratePreviewRequest};
use crate::server_fanout::{ServerCreationRequest, ServerCreationService};
use crate::tenant_config::{TenantConfigRepo, UpsertTenantConfigBody};
use crate::validation::{self, ListRulesQuery, RunValidationBody, SetRuleConfigBody};

#[derive(Clone)]
pub struct AppState {
    pub pool: PgPool,
}

pub fn build_router(state: AppState) -> Router {
    Router::new()
        .route("/health", get(|| async { "ok" }))
        // Allocation
        .route("/api/net/allocate/asn", post(allocate_asn))
        .route("/api/net/allocate/vlan", post(allocate_vlan))
        .route("/api/net/allocate/mlag", post(allocate_mlag))
        .route("/api/net/allocate/ip", post(allocate_ip))
        .route("/api/net/allocate/subnet", post(allocate_subnet))
        .route("/api/net/allocate/retire", post(retire))
        .route("/api/net/allocate/shelf/:resource_type/:resource_key", get(is_on_shelf))
        // Servers
        .route("/api/net/servers/create-with-fanout", post(create_with_fanout))
        // Naming
        .route("/api/net/naming/link/preview", post(preview_link))
        .route("/api/net/naming/device/preview", post(preview_device))
        .route("/api/net/naming/server/preview", post(preview_server))
        // Naming overrides (Phase 7a)
        .route("/api/net/naming/resolve", post(resolve_naming))
        .route("/api/net/naming/overrides", get(list_overrides).post(create_override))
        .route("/api/net/naming/overrides/:id",
               get(get_override).put(update_override).delete(delete_override))
        // Tenant naming config (Phase 7b) + regenerate preview (Phase 7c)
        .route("/api/net/naming/tenant-config", get(get_tenant_config).put(upsert_tenant_config))
        .route("/api/net/naming/regenerate/preview", post(regenerate_preview))
        .route("/api/net/naming/regenerate/apply", post(regenerate_apply))
        // Audit (Phase 8 foundation)
        .route("/api/net/audit", get(list_audit))
        .route("/api/net/audit/verify", get(verify_audit_chain))
        .route("/api/net/audit/entity/:entity_type/:entity_id", get(entity_audit_timeline))
        .route("/api/net/audit/export", get(export_audit))
        // Change Sets (Phase 8a)
        .route("/api/net/change-sets", get(list_change_sets).post(create_change_set))
        .route("/api/net/change-sets/:id", get(get_change_set))
        .route("/api/net/change-sets/by-correlation/:correlation_id", get(get_change_set_by_correlation))
        .route("/api/net/change-sets/:id/items", post(add_change_set_item))
        .route("/api/net/change-sets/:id/submit", post(submit_change_set))
        .route("/api/net/change-sets/:id/decisions", post(record_decision).get(list_decisions))
        .route("/api/net/change-sets/:id/cancel", post(cancel_change_set))
        .route("/api/net/change-sets/:id/apply", post(apply_change_set))
        .route("/api/net/change-sets/:id/rollback", post(rollback_change_set))
        // Lock-state management (Phase 8f)
        .route("/api/net/locks", get(list_locked))
        .route("/api/net/locks/:table/:id", axum::routing::patch(set_entity_lock))
        // Device list (thin read — powers WPF pickers)
        .route("/api/net/devices", get(list_devices))
        // Thin pool/block reads (WPF convenience-form pickers)
        .route("/api/net/vlan-blocks", get(list_vlan_blocks))
        .route("/api/net/asn-blocks",  get(list_asn_blocks))
        .route("/api/net/mlag-pools",  get(list_mlag_pools))
        .route("/api/net/ip-pools",    get(list_ip_pools))
        // Validation rules (Phase 9a)
        .route("/api/net/validation/rules", get(list_validation_rules))
        .route("/api/net/validation/rules/:code", axum::routing::put(set_validation_rule_config))
        .route("/api/net/validation/run", post(run_validation_route))
        .with_state(state)
}

// ─── Auth helper ─────────────────────────────────────────────────────────

/// Pull the caller's user id from the `X-User-Id` header. Optional — handlers
/// still work without it (audit columns go NULL). Gateway adds this header
/// from the JWT claim during the cutover; direct desktop calls pass nothing.
fn header_user_id(headers: &HeaderMap) -> Option<i32> {
    headers.get("x-user-id").and_then(|v| v.to_str().ok()).and_then(|s| s.parse().ok())
}

// ─── Allocation handlers ─────────────────────────────────────────────────

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
struct AllocateAsnBody {
    block_id: Uuid,
    organization_id: Uuid,
    allocated_to_type: String,
    allocated_to_id: Uuid,
}

async fn allocate_asn(
    State(s): State<AppState>,
    headers: HeaderMap,
    Json(req): Json<AllocateAsnBody>,
) -> Result<impl IntoResponse, EngineError> {
    let svc = AllocationService::new(s.pool);
    let user_id = header_user_id(&headers);
    let result = svc.allocate_asn(
        req.block_id, req.organization_id,
        &req.allocated_to_type, req.allocated_to_id, user_id).await?;
    Ok((StatusCode::CREATED, Json(result)))
}

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
struct AllocateVlanBody {
    block_id: Uuid,
    organization_id: Uuid,
    display_name: String,
    description: Option<String>,
    scope_level: PoolScopeLevel,
    scope_entity_id: Option<Uuid>,
    template_id: Option<Uuid>,
}

async fn allocate_vlan(
    State(s): State<AppState>,
    headers: HeaderMap,
    Json(req): Json<AllocateVlanBody>,
) -> Result<impl IntoResponse, EngineError> {
    let svc = AllocationService::new(s.pool);
    let user_id = header_user_id(&headers);
    let result = svc.allocate_vlan(
        req.block_id, req.organization_id,
        &req.display_name, req.description.as_deref(),
        req.scope_level, req.scope_entity_id, req.template_id, user_id).await?;
    Ok((StatusCode::CREATED, Json(result)))
}

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
struct AllocateMlagBody {
    pool_id: Uuid,
    organization_id: Uuid,
    display_name: String,
    scope_level: PoolScopeLevel,
    scope_entity_id: Option<Uuid>,
}

async fn allocate_mlag(
    State(s): State<AppState>,
    headers: HeaderMap,
    Json(req): Json<AllocateMlagBody>,
) -> Result<impl IntoResponse, EngineError> {
    let svc = AllocationService::new(s.pool);
    let user_id = header_user_id(&headers);
    let result = svc.allocate_mlag_domain(
        req.pool_id, req.organization_id,
        &req.display_name, req.scope_level, req.scope_entity_id, user_id).await?;
    Ok((StatusCode::CREATED, Json(result)))
}

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
struct AllocateIpBody {
    subnet_id: Uuid,
    organization_id: Uuid,
    assigned_to_type: Option<String>,
    assigned_to_id: Option<Uuid>,
}

async fn allocate_ip(
    State(s): State<AppState>,
    headers: HeaderMap,
    Json(req): Json<AllocateIpBody>,
) -> Result<impl IntoResponse, EngineError> {
    let svc = IpAllocationService::new(s.pool);
    let user_id = header_user_id(&headers);
    let result = svc.allocate_next_ip(
        req.subnet_id, req.organization_id,
        req.assigned_to_type.as_deref(), req.assigned_to_id, user_id).await?;
    Ok((StatusCode::CREATED, Json(result)))
}

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
struct AllocateSubnetBody {
    pool_id: Uuid,
    organization_id: Uuid,
    prefix_length: u32,
    subnet_code: String,
    display_name: String,
    scope_level: PoolScopeLevel,
    scope_entity_id: Option<Uuid>,
    parent_subnet_id: Option<Uuid>,
}

async fn allocate_subnet(
    State(s): State<AppState>,
    headers: HeaderMap,
    Json(req): Json<AllocateSubnetBody>,
) -> Result<impl IntoResponse, EngineError> {
    let svc = IpAllocationService::new(s.pool);
    let user_id = header_user_id(&headers);
    let result = svc.allocate_subnet(
        req.pool_id, req.organization_id, req.prefix_length,
        &req.subnet_code, &req.display_name, req.scope_level,
        req.scope_entity_id, req.parent_subnet_id, user_id).await?;
    Ok((StatusCode::CREATED, Json(result)))
}

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
struct RetireBody {
    organization_id: Uuid,
    resource_type: ShelfResourceType,
    resource_key: String,
    cooldown_seconds: i64,
    pool_id: Option<Uuid>,
    block_id: Option<Uuid>,
    reason: Option<String>,
}

async fn retire(
    State(s): State<AppState>,
    headers: HeaderMap,
    Json(req): Json<RetireBody>,
) -> Result<impl IntoResponse, EngineError> {
    let svc = AllocationService::new(s.pool);
    let user_id = header_user_id(&headers);
    let result = svc.retire(
        req.organization_id, req.resource_type, &req.resource_key,
        Duration::seconds(req.cooldown_seconds),
        req.pool_id, req.block_id, req.reason.as_deref(), user_id).await?;
    Ok((StatusCode::CREATED, Json(result)))
}

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
struct IsOnShelfQuery {
    organization_id: Uuid,
}

#[derive(Serialize)]
struct IsOnShelfResponse { on_shelf: bool }

async fn is_on_shelf(
    State(s): State<AppState>,
    Path((resource_type, resource_key)): Path<(ShelfResourceType, String)>,
    Query(q): Query<IsOnShelfQuery>,
) -> Result<impl IntoResponse, EngineError> {
    let svc = AllocationService::new(s.pool);
    let on_shelf = svc.is_on_shelf(q.organization_id, resource_type, &resource_key).await?;
    Ok(Json(IsOnShelfResponse { on_shelf }))
}

// ─── Server fan-out ──────────────────────────────────────────────────────

async fn create_with_fanout(
    State(s): State<AppState>,
    Json(req): Json<ServerCreationRequest>,
) -> Result<impl IntoResponse, EngineError> {
    let svc = ServerCreationService::new(s.pool);
    let result = svc.create_with_fanout(req).await?;
    Ok((StatusCode::CREATED, Json(result)))
}

// ─── Naming previews ─────────────────────────────────────────────────────

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
struct LinkPreviewBody { template: String, context: LinkNamingContext }

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
struct DevicePreviewBody { template: String, context: DeviceNamingContext }

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
struct ServerPreviewBody { template: String, context: ServerNamingContext }

#[derive(Serialize)]
struct PreviewResponse { expanded: String }

async fn preview_link(Json(req): Json<LinkPreviewBody>) -> impl IntoResponse {
    Json(PreviewResponse { expanded: naming::expand_link(&req.template, &req.context) })
}

async fn preview_device(Json(req): Json<DevicePreviewBody>) -> impl IntoResponse {
    Json(PreviewResponse { expanded: naming::expand_device(&req.template, &req.context) })
}

async fn preview_server(Json(req): Json<ServerPreviewBody>) -> impl IntoResponse {
    Json(PreviewResponse { expanded: naming::expand_server(&req.template, &req.context) })
}

// ─── Naming overrides (Phase 7a) ─────────────────────────────────────────

async fn resolve_naming(
    State(s): State<AppState>,
    Json(req): Json<ResolveRequest>,
) -> Result<impl IntoResponse, EngineError> {
    let svc = NamingResolver::new(s.pool);
    let out = svc.resolve(&req).await?;
    Ok(Json(out))
}

async fn list_overrides(
    State(s): State<AppState>,
    Query(q): Query<ListOverridesQuery>,
) -> Result<impl IntoResponse, EngineError> {
    let repo = NamingOverrideRepo::new(s.pool);
    Ok(Json(repo.list(&q).await?))
}

async fn create_override(
    State(s): State<AppState>,
    headers: HeaderMap,
    Json(body): Json<CreateOverrideBody>,
) -> Result<impl IntoResponse, EngineError> {
    let repo = NamingOverrideRepo::new(s.pool);
    let user_id = header_user_id(&headers);
    let out = repo.create(&body, user_id).await?;
    Ok((StatusCode::CREATED, Json(out)))
}

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
struct OrgQuery { organization_id: Uuid }

async fn get_override(
    State(s): State<AppState>,
    Path(id): Path<Uuid>,
    Query(q): Query<OrgQuery>,
) -> Result<impl IntoResponse, EngineError> {
    let repo = NamingOverrideRepo::new(s.pool);
    Ok(Json(repo.get(id, q.organization_id).await?))
}

async fn update_override(
    State(s): State<AppState>,
    Path(id): Path<Uuid>,
    Query(q): Query<OrgQuery>,
    headers: HeaderMap,
    Json(body): Json<UpdateOverrideBody>,
) -> Result<impl IntoResponse, EngineError> {
    let repo = NamingOverrideRepo::new(s.pool);
    let user_id = header_user_id(&headers);
    Ok(Json(repo.update(id, q.organization_id, &body, user_id).await?))
}

async fn delete_override(
    State(s): State<AppState>,
    Path(id): Path<Uuid>,
    Query(q): Query<OrgQuery>,
    headers: HeaderMap,
) -> Result<impl IntoResponse, EngineError> {
    let repo = NamingOverrideRepo::new(s.pool);
    let user_id = header_user_id(&headers);
    repo.delete(id, q.organization_id, user_id).await?;
    Ok(StatusCode::NO_CONTENT)
}

// ─── Tenant naming config (Phase 7b) ─────────────────────────────────────

async fn get_tenant_config(
    State(s): State<AppState>,
    Query(q): Query<OrgQuery>,
) -> Result<impl IntoResponse, EngineError> {
    let repo = TenantConfigRepo::new(s.pool);
    Ok(Json(repo.get(q.organization_id).await?))
}

async fn upsert_tenant_config(
    State(s): State<AppState>,
    Query(q): Query<OrgQuery>,
    headers: HeaderMap,
    Json(body): Json<UpsertTenantConfigBody>,
) -> Result<impl IntoResponse, EngineError> {
    let repo = TenantConfigRepo::new(s.pool);
    let user_id = header_user_id(&headers);
    Ok(Json(repo.upsert(q.organization_id, &body, user_id).await?))
}

// ─── Regenerate preview (Phase 7c) ───────────────────────────────────────

async fn regenerate_preview(
    State(s): State<AppState>,
    Json(req): Json<RegeneratePreviewRequest>,
) -> Result<impl IntoResponse, EngineError> {
    Ok(Json(regenerate::preview(&s.pool, &req).await?))
}

// ─── Audit (Phase 8 foundation) ──────────────────────────────────────────

async fn list_audit(
    State(s): State<AppState>,
    Query(q): Query<ListAuditQuery>,
) -> Result<impl IntoResponse, EngineError> {
    Ok(Json(audit::list(&s.pool, &q).await?))
}

async fn verify_audit_chain(
    State(s): State<AppState>,
    Query(q): Query<VerifyChainQuery>,
) -> Result<impl IntoResponse, EngineError> {
    Ok(Json(audit::verify_chain(&s.pool, &q).await?))
}

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
struct EntityTimelineQuery {
    organization_id: Uuid,
    #[serde(default)]
    limit: Option<i64>,
}

async fn entity_audit_timeline(
    State(s): State<AppState>,
    Path((entity_type, entity_id)): Path<(String, Uuid)>,
    Query(q): Query<EntityTimelineQuery>,
) -> Result<impl IntoResponse, EngineError> {
    Ok(Json(audit::entity_timeline(
        &s.pool, q.organization_id, &entity_type, entity_id, q.limit).await?))
}

async fn export_audit(
    State(s): State<AppState>,
    Query(q): Query<ExportQuery>,
) -> Result<Response, EngineError> {
    let (body, content_type) = audit::export(&s.pool, &q).await?;
    let filename = match q.format {
        audit::ExportFormat::Csv => "audit.csv",
        audit::ExportFormat::Ndjson => "audit.ndjson",
    };
    let mut resp = body.into_response();
    resp.headers_mut().insert(
        axum::http::header::CONTENT_TYPE,
        axum::http::HeaderValue::from_static(content_type));
    resp.headers_mut().insert(
        axum::http::header::CONTENT_DISPOSITION,
        axum::http::HeaderValue::from_str(
            &format!("attachment; filename=\"{filename}\""))
            .unwrap_or(axum::http::HeaderValue::from_static("attachment")));
    Ok(resp)
}

// ─── Device list (thin read) ─────────────────────────────────────────────

#[derive(Serialize, sqlx::FromRow)]
#[serde(rename_all = "camelCase")]
struct DeviceListRow {
    id: Uuid,
    hostname: String,
    role_code: Option<String>,
    building_code: Option<String>,
    status: String,
    version: i32,
}

#[derive(Serialize, sqlx::FromRow)]
#[serde(rename_all = "camelCase")]
struct VlanBlockListRow {
    id: Uuid,
    block_code: String,
    display_name: String,
    vlan_first: i32,
    vlan_last: i32,
    available: i64,   // vlan_last - vlan_first + 1 - allocated count
}

async fn list_vlan_blocks(
    State(s): State<AppState>,
    Query(q): Query<OrgQuery>,
) -> Result<impl IntoResponse, EngineError> {
    // Rows + per-block availability count — one query via a LEFT JOIN
    // on net.vlan so the picker can show "VLAN 100-199 · 12 free" at
    // a glance without a per-row round-trip.
    let rows: Vec<VlanBlockListRow> = sqlx::query_as(
        "SELECT b.id, b.block_code, b.display_name, b.vlan_first, b.vlan_last,
                (b.vlan_last - b.vlan_first + 1 - COALESCE((
                    SELECT COUNT(*) FROM net.vlan v
                     WHERE v.block_id = b.id AND v.deleted_at IS NULL
                ), 0))::bigint AS available
           FROM net.vlan_block b
          WHERE b.organization_id = $1 AND b.deleted_at IS NULL
          ORDER BY b.block_code")
        .bind(q.organization_id)
        .fetch_all(&s.pool)
        .await?;
    Ok(Json(rows))
}

#[derive(Serialize, sqlx::FromRow)]
#[serde(rename_all = "camelCase")]
struct AsnBlockListRow {
    id: Uuid,
    block_code: String,
    display_name: String,
    asn_first: i64,
    asn_last: i64,
    available: i64,
}

async fn list_asn_blocks(
    State(s): State<AppState>,
    Query(q): Query<OrgQuery>,
) -> Result<impl IntoResponse, EngineError> {
    // Same shape as list_vlan_blocks. ASN ranges are bigint so the
    // "available" math uses int8; a full-range check could overflow
    // an i32 but never an i64.
    let rows: Vec<AsnBlockListRow> = sqlx::query_as(
        "SELECT b.id, b.block_code, b.display_name, b.asn_first, b.asn_last,
                (b.asn_last - b.asn_first + 1 - COALESCE((
                    SELECT COUNT(*) FROM net.asn_allocation a
                     WHERE a.block_id = b.id AND a.deleted_at IS NULL
                ), 0))::bigint AS available
           FROM net.asn_block b
          WHERE b.organization_id = $1 AND b.deleted_at IS NULL
          ORDER BY b.block_code")
        .bind(q.organization_id)
        .fetch_all(&s.pool)
        .await?;
    Ok(Json(rows))
}

#[derive(Serialize, sqlx::FromRow)]
#[serde(rename_all = "camelCase")]
struct MlagPoolListRow {
    id: Uuid,
    pool_code: String,
    display_name: String,
    domain_first: i32,
    domain_last: i32,
    available: i64,
}

async fn list_mlag_pools(
    State(s): State<AppState>,
    Query(q): Query<OrgQuery>,
) -> Result<impl IntoResponse, EngineError> {
    // MLAG uniqueness is tenant-wide (per allocation.rs) — the "used"
    // count for a pool is effectively "any MLAG allocated from this
    // pool that isn't soft-deleted". AllocationService.allocate_mlag_domain
    // enforces the tenant-wide uniqueness via fetch_used_mlag_domains.
    let rows: Vec<MlagPoolListRow> = sqlx::query_as(
        "SELECT p.id, p.pool_code, p.display_name, p.domain_first, p.domain_last,
                (p.domain_last - p.domain_first + 1 - COALESCE((
                    SELECT COUNT(*) FROM net.mlag_domain m
                     WHERE m.pool_id = p.id AND m.deleted_at IS NULL
                ), 0))::bigint AS available
           FROM net.mlag_domain_pool p
          WHERE p.organization_id = $1 AND p.deleted_at IS NULL
          ORDER BY p.pool_code")
        .bind(q.organization_id)
        .fetch_all(&s.pool)
        .await?;
    Ok(Json(rows))
}

#[derive(Serialize, sqlx::FromRow)]
#[serde(rename_all = "camelCase")]
struct IpPoolListRow {
    id: Uuid,
    pool_code: String,
    display_name: String,
    network: String,  // cidr::text
    family: i32,      // 4 or 6
}

async fn list_ip_pools(
    State(s): State<AppState>,
    Query(q): Query<OrgQuery>,
) -> Result<impl IntoResponse, EngineError> {
    // IP-pool availability depends on the prefix_length the admin
    // wants to carve, so we don't pre-compute it here — the picker
    // shows the range and family, carving-failure surfaces cleanly
    // at apply time. This keeps the query O(pools) rather than
    // O(pools × allocated-subnets) which on a dense tenant is orders
    // of magnitude different.
    let rows: Vec<IpPoolListRow> = sqlx::query_as(
        "SELECT id, pool_code, display_name, network::text, family(network) AS family
           FROM net.ip_pool
          WHERE organization_id = $1 AND deleted_at IS NULL
          ORDER BY family(network), pool_code")
        .bind(q.organization_id)
        .fetch_all(&s.pool)
        .await?;
    Ok(Json(rows))
}

async fn list_devices(
    State(s): State<AppState>,
    Query(q): Query<OrgQuery>,
) -> Result<impl IntoResponse, EngineError> {
    // Thin list used by the WPF device-picker for Change Set Rename /
    // Update / Delete items. Includes role_code + building_code for
    // human-readable disambiguation in the picker, without forcing
    // callers to do a second round-trip per row.
    let rows: Vec<DeviceListRow> = sqlx::query_as(
        "SELECT d.id, d.hostname,
                r.role_code,
                b.building_code,
                d.status::text AS status,
                d.version
           FROM net.device d
           LEFT JOIN net.device_role r ON r.id = d.device_role_id
           LEFT JOIN net.building    b ON b.id = d.building_id
          WHERE d.organization_id = $1 AND d.deleted_at IS NULL
          ORDER BY d.hostname
          LIMIT 5000")
        .bind(q.organization_id)
        .fetch_all(&s.pool)
        .await?;
    Ok(Json(rows))
}

async fn regenerate_apply(
    State(s): State<AppState>,
    Json(req): Json<RegenerateApplyRequest>,
) -> Result<impl IntoResponse, EngineError> {
    Ok(Json(regenerate::apply(&s.pool, &req).await?))
}

// ─── Change Sets (Phase 8a) ──────────────────────────────────────────────

async fn list_change_sets(
    State(s): State<AppState>,
    Query(q): Query<ListChangeSetsQuery>,
) -> Result<impl IntoResponse, EngineError> {
    let repo = ChangeSetRepo::new(s.pool);
    Ok(Json(repo.list(&q).await?))
}

async fn create_change_set(
    State(s): State<AppState>,
    headers: HeaderMap,
    Json(body): Json<CreateChangeSetBody>,
) -> Result<impl IntoResponse, EngineError> {
    let repo = ChangeSetRepo::new(s.pool);
    let user_id = header_user_id(&headers);
    let out = repo.create(&body, user_id).await?;
    Ok((StatusCode::CREATED, Json(out)))
}

#[derive(Serialize)]
struct ChangeSetWithItems {
    #[serde(flatten)]
    set: crate::change_sets::ChangeSet,
    items: Vec<crate::change_sets::ChangeSetItem>,
}

async fn get_change_set(
    State(s): State<AppState>,
    Path(id): Path<Uuid>,
    Query(q): Query<GetChangeSetQuery>,
) -> Result<impl IntoResponse, EngineError> {
    let repo = ChangeSetRepo::new(s.pool);
    let (set, items) = repo.get(id, q.organization_id).await?;
    Ok(Json(ChangeSetWithItems { set, items }))
}

/// Look up a Set by the correlation id threaded into its audit entries.
/// 200 with the detail envelope when found, 404 when not — the caller
/// (WPF audit viewer drill-down) treats 404 as "this audit row isn't
/// tied to a Change Set, open nothing".
async fn get_change_set_by_correlation(
    State(s): State<AppState>,
    Path(correlation_id): Path<Uuid>,
    Query(q): Query<GetChangeSetQuery>,
) -> Result<Response, EngineError> {
    let repo = ChangeSetRepo::new(s.pool);
    match repo.get_by_correlation(correlation_id, q.organization_id).await? {
        Some((set, items)) => Ok(Json(ChangeSetWithItems { set, items }).into_response()),
        None => Ok(StatusCode::NOT_FOUND.into_response()),
    }
}

async fn add_change_set_item(
    State(s): State<AppState>,
    Path(id): Path<Uuid>,
    Query(q): Query<OrgQuery>,
    headers: HeaderMap,
    Json(body): Json<AddItemBody>,
) -> Result<impl IntoResponse, EngineError> {
    let repo = ChangeSetRepo::new(s.pool);
    let user_id = header_user_id(&headers);
    let out = repo.add_item(id, q.organization_id, &body, user_id).await?;
    Ok((StatusCode::CREATED, Json(out)))
}

async fn submit_change_set(
    State(s): State<AppState>,
    Path(id): Path<Uuid>,
    Query(q): Query<OrgQuery>,
    headers: HeaderMap,
    Json(body): Json<SubmitBody>,
) -> Result<impl IntoResponse, EngineError> {
    let repo = ChangeSetRepo::new(s.pool);
    let user_id = header_user_id(&headers);
    Ok(Json(repo.submit(id, q.organization_id, &body, user_id).await?))
}

async fn record_decision(
    State(s): State<AppState>,
    Path(id): Path<Uuid>,
    Query(q): Query<OrgQuery>,
    headers: HeaderMap,
    Json(body): Json<DecisionBody>,
) -> Result<impl IntoResponse, EngineError> {
    let approver = header_user_id(&headers).ok_or_else(|| EngineError::bad_request(
        "Approver user id required — pass X-User-Id header."))?;
    let repo = ChangeSetRepo::new(s.pool);
    Ok(Json(repo.record_decision(id, q.organization_id, approver, &body).await?))
}

async fn list_decisions(
    State(s): State<AppState>,
    Path(id): Path<Uuid>,
    Query(q): Query<OrgQuery>,
) -> Result<impl IntoResponse, EngineError> {
    let repo = ChangeSetRepo::new(s.pool);
    Ok(Json(repo.list_approvals(id, q.organization_id).await?))
}

async fn cancel_change_set(
    State(s): State<AppState>,
    Path(id): Path<Uuid>,
    Query(q): Query<OrgQuery>,
    headers: HeaderMap,
    // Body is optional — an empty POST is valid "cancel without a note".
    body: Option<Json<CancelBody>>,
) -> Result<impl IntoResponse, EngineError> {
    let repo = ChangeSetRepo::new(s.pool);
    let user_id = header_user_id(&headers);
    let notes = body.and_then(|b| b.0.notes);
    Ok(Json(repo.cancel(id, q.organization_id, user_id, notes.as_deref()).await?))
}

async fn apply_change_set(
    State(s): State<AppState>,
    Path(id): Path<Uuid>,
    Query(q): Query<OrgQuery>,
    headers: HeaderMap,
) -> Result<impl IntoResponse, EngineError> {
    let repo = ChangeSetRepo::new(s.pool);
    let user_id = header_user_id(&headers);
    Ok(Json(repo.apply(id, q.organization_id, user_id).await?))
}

async fn rollback_change_set(
    State(s): State<AppState>,
    Path(id): Path<Uuid>,
    Query(q): Query<OrgQuery>,
    headers: HeaderMap,
) -> Result<impl IntoResponse, EngineError> {
    let repo = ChangeSetRepo::new(s.pool);
    let user_id = header_user_id(&headers);
    Ok(Json(repo.rollback(id, q.organization_id, user_id).await?))
}

// ─── Lock state (Phase 8f) ───────────────────────────────────────────────

async fn set_entity_lock(
    State(s): State<AppState>,
    Path((table, id)): Path<(String, Uuid)>,
    Query(q): Query<OrgQuery>,
    headers: HeaderMap,
    Json(body): Json<SetLockBody>,
) -> Result<impl IntoResponse, EngineError> {
    let user_id = header_user_id(&headers);
    Ok(Json(locks::set_lock(&s.pool, &table, id, q.organization_id, &body, user_id).await?))
}

async fn list_locked(
    State(s): State<AppState>,
    Query(q): Query<ListLockedQuery>,
) -> Result<impl IntoResponse, EngineError> {
    Ok(Json(locks::list_locked(&s.pool, &q).await?))
}

// ─── Validation (Phase 9a) ───────────────────────────────────────────────

async fn list_validation_rules(
    State(s): State<AppState>,
    Query(q): Query<ListRulesQuery>,
) -> Result<impl IntoResponse, EngineError> {
    Ok(Json(validation::list_rules(&s.pool, &q).await?))
}

async fn set_validation_rule_config(
    State(s): State<AppState>,
    Path(code): Path<String>,
    Query(q): Query<OrgQuery>,
    headers: HeaderMap,
    Json(body): Json<SetRuleConfigBody>,
) -> Result<impl IntoResponse, EngineError> {
    let user_id = header_user_id(&headers);
    validation::set_rule_config(&s.pool, q.organization_id, &code, &body, user_id).await?;
    Ok(StatusCode::NO_CONTENT)
}

async fn run_validation_route(
    State(s): State<AppState>,
    Json(body): Json<RunValidationBody>,
) -> Result<impl IntoResponse, EngineError> {
    Ok(Json(validation::run_validation(&s.pool, &body).await?))
}
