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
use crate::bulk_edit::{
    self, BulkEditDevicesBody, BulkEditDhcpRelayTargetsBody, BulkEditQuery,
    BulkEditServersBody, BulkEditSubnetsBody, BulkEditVlansBody,
};
use crate::bulk_export;
use crate::bulk_import;
use crate::xlsx_codec;
use crate::cli_flavor::{self, ListFlavorsQuery, SetFlavorConfigBody};
use crate::config_gen;
use crate::dhcp_relay::{
    CreateDhcpRelayBody, DhcpRelayRepo, ListDhcpRelayQuery, UpdateDhcpRelayBody,
};
use crate::saved_views::{
    CreateSavedViewBody, ListSavedViewsQuery, SavedViewRepo, UpdateSavedViewBody,
};
use crate::scope_grants::{
    self, CreateScopeGrantBody, ListScopeGrantsQuery, ScopeGrantRepo,
};
use crate::search;
use crate::change_sets::{
    self, AddItemBody, CancelBody, ChangeSetRepo, CreateChangeSetBody, DecisionBody,
    GetChangeSetQuery, ListChangeSetsQuery, SubmitBody,
};
use crate::error::EngineError;
use crate::ip_allocation::IpAllocationService;
use crate::pool_utilization;
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
        .route("/api/net/allocate/subnet/preview", post(preview_carve_subnet))
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
        .route("/api/net/audit/stats",  get(audit_stats_by_entity_type))
        .route("/api/net/audit/trend",  get(audit_trend_handler))
        .route("/api/net/audit/top-actors", get(audit_top_actors_handler))
        .route("/api/net/audit/correlations", get(audit_correlations_handler))
        .route("/api/net/audit/actions", get(audit_actions_handler))
        // Change Sets (Phase 8a)
        .route("/api/net/change-sets", get(list_change_sets).post(create_change_set))
        .route("/api/net/change-sets/summary", get(change_set_summary_handler))
        .route("/api/net/change-sets/:id", get(get_change_set))
        .route("/api/net/change-sets/by-correlation/:correlation_id", get(get_change_set_by_correlation))
        .route("/api/net/change-sets/:id/items", post(add_change_set_item))
        .route("/api/net/change-sets/:id/items/:item_id",
               axum::routing::delete(delete_change_set_item))
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
        .route("/api/net/vlans",   get(list_vlans))
        .route("/api/net/links",   get(list_links))
        .route("/api/net/servers", get(list_servers))
        .route("/api/net/subnets", get(list_subnets))
        .route("/api/net/ip-addresses", get(list_ip_addresses))
        .route("/api/net/server-nics",  get(list_server_nics))
        .route("/api/net/link-endpoints", get(list_link_endpoints))
        .route("/api/net/ports",          get(list_ports))
        .route("/api/net/aggregate-ethernet", get(list_aggregate_ethernet))
        .route("/api/net/mlag-domains",      get(list_mlag_domains))
        .route("/api/net/modules",           get(list_modules))
        .route("/api/net/mstp-rules",        get(list_mstp_rules))
        .route("/api/net/reservation-shelf", get(list_reservation_shelf))
        .route("/api/net/asn-allocations",   get(list_asn_allocations))
        .route("/api/net/rooms",             get(list_rooms))
        .route("/api/net/racks",             get(list_racks))
        // Thin pool/block reads (WPF convenience-form pickers)
        .route("/api/net/vlan-blocks", get(list_vlan_blocks))
        .route("/api/net/asn-blocks",  get(list_asn_blocks))
        .route("/api/net/mlag-pools",  get(list_mlag_pools))
        .route("/api/net/ip-pools",    get(list_ip_pools))
        .route("/api/net/pools/utilization", get(pool_utilization_handler))
        // Validation rules (Phase 9a)
        .route("/api/net/validation/rules", get(list_validation_rules))
        .route("/api/net/validation/rules/:code", axum::routing::put(set_validation_rule_config))
        .route("/api/net/validation/run", post(run_validation_route))
        // CLI flavors (Phase 10)
        .route("/api/net/cli-flavors", get(list_cli_flavors))
        .route("/api/net/cli-flavors/:code",
               axum::routing::put(set_cli_flavor_config))
        // Device config render (Phase 10 PicOS starter)
        .route("/api/net/devices/:id/render-config",        post(render_device_config))
        .route("/api/net/devices/:id/renders",              get(list_device_renders))
        .route("/api/net/renders/:id",                      get(get_render_by_id))
        .route("/api/net/renders/:id/diff",                 get(diff_render_by_id))
        // Building- / site- / region-level turn-up packs: fan-out render + persist
        .route("/api/net/buildings/:id/render-configs",     post(render_building_configs))
        .route("/api/net/sites/:id/render-configs",         post(render_site_configs))
        .route("/api/net/regions/:id/render-configs",       post(render_region_configs))
        // DHCP relay targets: M:N between VLANs + DHCP server IPs,
        // consumed by config-gen. CRUD follows the NamingOverride shape.
        .route("/api/net/dhcp-relay-targets",               get(list_dhcp_relay).post(create_dhcp_relay))
        .route("/api/net/dhcp-relay-targets/:id",
               get(get_dhcp_relay)
                   .put(update_dhcp_relay)
                   .delete(delete_dhcp_relay))
        // Bulk export: flat CSV dumps of the core net.* entities
        // operators need for spreadsheet workflows + BI ingestion.
        .route("/api/net/devices/export",                   get(export_devices))
        .route("/api/net/vlans/export",                     get(export_vlans))
        .route("/api/net/ip-addresses/export",              get(export_ip_addresses))
        .route("/api/net/links/export",                     get(export_links))
        .route("/api/net/servers/export",                   get(export_servers))
        .route("/api/net/subnets/export",                   get(export_subnets))
        .route("/api/net/asn-allocations/export",           get(export_asn_allocations))
        .route("/api/net/mlag-domains/export",              get(export_mlag_domains))
        .route("/api/net/dhcp-relay-targets/export",        get(export_dhcp_relay_targets))
        // Bulk import (Phase 10) — POST the CSV body. `dryRun=true`
        // (the default) runs validate-only + returns per-row outcomes.
        // `dryRun=false` applies via a single transaction, rolling
        // back the whole import if any row fails.
        .route("/api/net/devices/import",                   post(import_devices_csv))
        .route("/api/net/vlans/import",                     post(import_vlans_csv))
        .route("/api/net/subnets/import",                   post(import_subnets_csv))
        .route("/api/net/servers/import",                   post(import_servers_csv))
        .route("/api/net/dhcp-relay-targets/import",        post(import_dhcp_relay_targets_csv))
        .route("/api/net/links/import",                     post(import_links_csv))
        // Global search (Phase 10) — tsvector-based full-text across
        // 6 entity types. RBAC filters results post-fetch so the
        // caller only sees rows they have read on.
        .route("/api/net/search",                           get(global_search_handler))
        .route("/api/net/search/facets",                    get(search_facets_handler))
        .route("/api/net/whoami",                           get(whoami_handler))
        .route("/api/net/whoami/grants",                    get(whoami_grants_handler))
        // Saved views (Phase 10) — per-user named search queries,
        // managed personally not through scope_grants. Ownership
        // check is baked into every handler via user_id from the
        // X-User-Id header.
        .route("/api/net/saved-views",                      get(list_saved_views).post(create_saved_view))
        .route("/api/net/saved-views/:id",
               get(get_saved_view).put(update_saved_view).delete(delete_saved_view))
        // XLSX round-trip (Phase 10) — xlsx is a pure transport
        // adapter over the CSV paths. Every entity gets .xlsx on
        // both sides symmetrically with the CSV surface.
        .route("/api/net/devices/export.xlsx",              get(export_devices_xlsx))
        .route("/api/net/vlans/export.xlsx",                get(export_vlans_xlsx))
        .route("/api/net/subnets/export.xlsx",              get(export_subnets_xlsx))
        .route("/api/net/servers/export.xlsx",              get(export_servers_xlsx))
        .route("/api/net/links/export.xlsx",                get(export_links_xlsx))
        .route("/api/net/dhcp-relay-targets/export.xlsx",   get(export_dhcp_relay_xlsx))
        .route("/api/net/devices/import.xlsx",              post(import_devices_xlsx))
        .route("/api/net/vlans/import.xlsx",                post(import_vlans_xlsx))
        .route("/api/net/subnets/import.xlsx",              post(import_subnets_xlsx))
        .route("/api/net/servers/import.xlsx",              post(import_servers_xlsx))
        .route("/api/net/links/import.xlsx",                post(import_links_xlsx))
        .route("/api/net/dhcp-relay-targets/import.xlsx",   post(import_dhcp_relay_xlsx))
        // Bulk edit (Phase 10) — same-value-for-all transactional
        // update across a selected set of rows; dryRun preview.
        .route("/api/net/devices/bulk-edit",                post(bulk_edit_devices_handler))
        .route("/api/net/vlans/bulk-edit",                  post(bulk_edit_vlans_handler))
        .route("/api/net/subnets/bulk-edit",                post(bulk_edit_subnets_handler))
        .route("/api/net/servers/bulk-edit",                post(bulk_edit_servers_handler))
        .route("/api/net/dhcp-relay-targets/bulk-edit",     post(bulk_edit_dhcp_relay_handler))
        // Scope grants (Phase 10 RBAC foundation) — CRUD + resolver.
        // Engine is available now but not-yet-blocking anywhere;
        // per-endpoint enforcement lands in follow-on slices.
        .route("/api/net/scope-grants",                     get(list_scope_grants).post(create_scope_grant))
        .route("/api/net/scope-grants/:id",
               get(get_scope_grant).delete(delete_scope_grant))
        .route("/api/net/scope-grants/check",               get(check_permission_handler))
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
struct CarvePreviewBody {
    pool_id: Uuid,
    organization_id: Uuid,
    prefix_length: u32,
}

async fn preview_carve_subnet(
    State(s): State<AppState>,
    Json(req): Json<CarvePreviewBody>,
) -> Result<impl IntoResponse, EngineError> {
    // Read-only dry-run of the subnet carver — shows what the next
    // `allocate_subnet` call would produce without mutating anything.
    // No RBAC gate because it's a computed read over pool + existing
    // subnets the caller already has a handle on via the pool id;
    // if the caller can't see the pool, it'll come back as
    // container_not_found from fetch_pool_cidr.
    let svc = IpAllocationService::new(s.pool);
    let result = svc.preview_carve(
        req.pool_id, req.organization_id, req.prefix_length).await?;
    Ok(Json(result))
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

/// Audit stats per entity_type — dashboard-style rollup.
/// Query params: organizationId (required), fromAt + toAt
/// (optional ISO timestamps for the activity window).
async fn audit_stats_by_entity_type(
    State(s): State<AppState>,
    Query(q): Query<audit::AuditStatsQuery>,
) -> Result<impl IntoResponse, EngineError> {
    Ok(Json(audit::stats_by_entity_type(&s.pool, &q).await?))
}

/// Audit trend — time-bucketed count series.
/// Query params: organizationId (required), fromAt + toAt + bucketBy
/// (optional: hour / day / week, default day), entityType
/// (optional narrower).
async fn audit_trend_handler(
    State(s): State<AppState>,
    Query(q): Query<audit::AuditTrendQuery>,
) -> Result<impl IntoResponse, EngineError> {
    Ok(Json(audit::trend(&s.pool, &q).await?))
}

/// Audit top actors — per-user rollup ordered by count DESC.
/// Query params: organizationId (required), fromAt + toAt
/// (optional window), entityType (optional narrower), limit
/// (default 20, clamped to 1..=100).
async fn audit_top_actors_handler(
    State(s): State<AppState>,
    Query(q): Query<audit::TopActorsQuery>,
) -> Result<impl IntoResponse, EngineError> {
    Ok(Json(audit::top_actors(&s.pool, &q).await?))
}

/// Recent distinct correlation ids — "what bulk operations happened
/// lately?" at a glance. One row per correlation_id with entry count
/// + distinct entity types + first/last-seen + change-set metadata
/// (when a net.change_set shares the correlation).
///
/// Query params: organizationId (required), limit (default 50,
/// clamped to 1..=500), fromAt + toAt (optional ISO timestamps
/// bounding the activity window — correlation in-scope if any of
/// its entries land inside).
#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
struct AuditCorrelationsQuery {
    organization_id: Uuid,
    limit: Option<i64>,
    from_at: Option<chrono::DateTime<chrono::Utc>>,
    to_at: Option<chrono::DateTime<chrono::Utc>>,
}

async fn audit_correlations_handler(
    State(s): State<AppState>,
    Query(q): Query<AuditCorrelationsQuery>,
) -> Result<impl IntoResponse, EngineError> {
    Ok(Json(audit::recent_correlations(
        &s.pool, q.organization_id, q.limit, q.from_at, q.to_at).await?))
}

/// Distinct audit actions seen in the tenant, for populating
/// filter dropdowns. Optional entityType narrower + limit
/// (default 100, clamped 1..=500).
#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
struct AuditActionsQuery {
    organization_id: Uuid,
    entity_type: Option<String>,
    limit: Option<i64>,
}

async fn audit_actions_handler(
    State(s): State<AppState>,
    Query(q): Query<AuditActionsQuery>,
) -> Result<impl IntoResponse, EngineError> {
    Ok(Json(audit::distinct_actions(
        &s.pool, q.organization_id, q.entity_type.as_deref(), q.limit).await?))
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

/// Pool utilization dashboard — rollup of ASN / VLAN / IP pool
/// usage vs capacity. One call returns every pool family so the
/// UI renders a single grid without fan-out.
async fn pool_utilization_handler(
    State(s): State<AppState>,
    Query(q): Query<OrgQuery>,
) -> Result<impl IntoResponse, EngineError> {
    Ok(Json(pool_utilization::list_utilization(&s.pool, q.organization_id).await?))
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

// ─── VLAN thin list (picker + hostname→uuid lookup) ─────────────────────

#[derive(Serialize, sqlx::FromRow)]
#[serde(rename_all = "camelCase")]
struct VlanListRow {
    id: Uuid,
    vlan_id: i32,
    display_name: String,
    block_code: Option<String>,
    scope_level: String,
    status: String,
    version: i32,
}

/// Thin list of VLANs — same shape + cap as list_devices. Used by the
/// WPF VLAN grid's audit-drill handler to resolve a (vlan_id, block)
/// row to its net.vlan uuid + by future pickers that need a cheap
/// full-catalog read.
async fn list_vlans(
    State(s): State<AppState>,
    Query(q): Query<OrgQuery>,
) -> Result<impl IntoResponse, EngineError> {
    let rows: Vec<VlanListRow> = sqlx::query_as(
        "SELECT v.id,
                v.vlan_id,
                v.display_name,
                b.block_code,
                v.scope_level,
                v.status::text AS status,
                v.version
           FROM net.vlan v
           LEFT JOIN net.vlan_block b ON b.id = v.block_id
          WHERE v.organization_id = $1 AND v.deleted_at IS NULL
          ORDER BY v.vlan_id
          LIMIT 5000")
        .bind(q.organization_id)
        .fetch_all(&s.pool)
        .await?;
    Ok(Json(rows))
}

// ─── Link thin list (picker + link_code→uuid lookup) ────────────────────

#[derive(Serialize, sqlx::FromRow)]
#[serde(rename_all = "camelCase")]
struct LinkListRow {
    id: Uuid,
    link_code: String,
    link_type: Option<String>,      // resolved type_code from net.link_type
    device_a: Option<String>,       // hostname of endpoint_order=0
    device_b: Option<String>,       // hostname of endpoint_order=1
    status: String,
    version: i32,
}

/// Thin list of links — mirrors list_devices + list_vlans (5000 row cap,
/// ordered for stable consumption). Resolves link_type + endpoint
/// hostnames via three LEFT JOINs so WPF grids can drive a context-menu
/// lookup without a second round-trip per row. Endpoint hostnames come
/// from the A-side (endpoint_order=0) and B-side (endpoint_order=1)
/// entries in net.link_endpoint.
async fn list_links(
    State(s): State<AppState>,
    Query(q): Query<OrgQuery>,
) -> Result<impl IntoResponse, EngineError> {
    let rows: Vec<LinkListRow> = sqlx::query_as(
        "SELECT l.id,
                l.link_code,
                lt.type_code AS link_type,
                da.hostname  AS device_a,
                db.hostname  AS device_b,
                l.status::text AS status,
                l.version
           FROM net.link l
           LEFT JOIN net.link_type     lt ON lt.id = l.link_type_id
           LEFT JOIN net.link_endpoint ea ON ea.link_id = l.id AND ea.endpoint_order = 0
           LEFT JOIN net.device        da ON da.id = ea.device_id
           LEFT JOIN net.link_endpoint eb ON eb.link_id = l.id AND eb.endpoint_order = 1
           LEFT JOIN net.device        db ON db.id = eb.device_id
          WHERE l.organization_id = $1 AND l.deleted_at IS NULL
          ORDER BY l.link_code
          LIMIT 5000")
        .bind(q.organization_id)
        .fetch_all(&s.pool)
        .await?;
    Ok(Json(rows))
}

// ─── Server thin list ───────────────────────────────────────────────────

#[derive(Serialize, sqlx::FromRow)]
#[serde(rename_all = "camelCase")]
struct ServerListRow {
    id: Uuid,
    hostname: String,
    profile_code: Option<String>,    // resolved from net.server_profile
    building_code: Option<String>,   // resolved from net.building
    status: String,
    version: i32,
}

/// Thin list of servers — same shape + cap as list_devices. Resolves
/// the profile + building code via LEFT JOINs so context-menu lookups
/// (e.g. web `selectId:{guid}:{label}` drill) don't need a second
/// round-trip per row.
async fn list_servers(
    State(s): State<AppState>,
    Query(q): Query<OrgQuery>,
) -> Result<impl IntoResponse, EngineError> {
    let rows: Vec<ServerListRow> = sqlx::query_as(
        "SELECT sv.id,
                sv.hostname,
                sp.profile_code,
                b.building_code,
                sv.status::text AS status,
                sv.version
           FROM net.server sv
           LEFT JOIN net.server_profile sp ON sp.id = sv.server_profile_id
           LEFT JOIN net.building       b  ON b.id  = sv.building_id
          WHERE sv.organization_id = $1 AND sv.deleted_at IS NULL
          ORDER BY sv.hostname
          LIMIT 5000")
        .bind(q.organization_id)
        .fetch_all(&s.pool)
        .await?;
    Ok(Json(rows))
}

// ─── Subnet thin list ───────────────────────────────────────────────────

#[derive(Serialize, sqlx::FromRow)]
#[serde(rename_all = "camelCase")]
struct SubnetListRow {
    id: Uuid,
    subnet_code: String,
    display_name: String,
    network: String,             // rendered via ::text for stable wire repr
    scope_level: String,
    // poolId alongside poolCode so detail pages can drill to
    // /network/ip-pool/:id without a second round-trip for the
    // code → id mapping.
    pool_id: Option<Uuid>,
    pool_code: Option<String>,
    vlan_tag: Option<i32>,       // resolved from net.vlan.vlan_id if linked
    status: String,
    version: i32,
}

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
struct SubnetListQuery {
    organization_id: Uuid,
    /// Optional narrower — when set, the grid returns only subnets in
    /// the given IP pool. Lets the pool-utilization page drill into
    /// "what subnets are carved out of this /16?" without a new
    /// endpoint.
    pool_id: Option<Uuid>,
}

/// Thin list of subnets — mirrors list_vlans (5000 row cap, ORDER BY
/// subnet_code). Resolves the parent pool code + optional VLAN tag so
/// pickers + grid rows render without extra joins client-side. network
/// is cast to text so the wire shape is a stable CIDR string rather
/// than the sqlx Ipv4Network/Ipv6Network enum which needs extra feature
/// flags to deserialise in consumers.
async fn list_subnets(
    State(s): State<AppState>,
    Query(q): Query<SubnetListQuery>,
) -> Result<impl IntoResponse, EngineError> {
    let rows: Vec<SubnetListRow> = sqlx::query_as(
        "SELECT sn.id,
                sn.subnet_code,
                sn.display_name,
                sn.network::text AS network,
                sn.scope_level,
                sn.pool_id       AS pool_id,
                p.pool_code      AS pool_code,
                v.vlan_id        AS vlan_tag,
                sn.status::text  AS status,
                sn.version
           FROM net.subnet sn
           LEFT JOIN net.ip_pool p ON p.id = sn.pool_id
           LEFT JOIN net.vlan    v ON v.id = sn.vlan_id
          WHERE sn.organization_id = $1 AND sn.deleted_at IS NULL
            AND ($2::uuid IS NULL OR sn.pool_id = $2)
          ORDER BY sn.subnet_code
          LIMIT 5000")
        .bind(q.organization_id)
        .bind(q.pool_id)
        .fetch_all(&s.pool)
        .await?;
    Ok(Json(rows))
}

// ─── IP address thin list ───────────────────────────────────────────────

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
struct IpAddressListQuery {
    organization_id: Uuid,
    /// Optional — narrow to one subnet. The subnet-detail web page
    /// uses this to show only the addresses under the selected row.
    subnet_id: Option<Uuid>,
}

#[derive(Serialize, sqlx::FromRow)]
#[serde(rename_all = "camelCase")]
struct IpAddressListRow {
    id: Uuid,
    subnet_id: Uuid,
    subnet_code: Option<String>,
    address: String,             // rendered as plain host string via host()
    assigned_to_type: Option<String>,
    assigned_to_id: Option<Uuid>,
    is_reserved: bool,
    status: String,
    version: i32,
}

/// Thin list of IP addresses — same 5000-row cap as the other net.*
/// thin lists. Optional `subnetId` narrows to one subnet (drill
/// target from the subnet detail page). LEFT JOIN on net.subnet
/// for subnet_code so the caller doesn't need a second round-trip
/// to render "IP 10.11.120.10 in SUB-MGMT-120".
async fn list_ip_addresses(
    State(s): State<AppState>,
    Query(q): Query<IpAddressListQuery>,
) -> Result<impl IntoResponse, EngineError> {
    let rows: Vec<IpAddressListRow> = sqlx::query_as(
        "SELECT ip.id,
                ip.subnet_id,
                sn.subnet_code,
                host(ip.address)   AS address,
                ip.assigned_to_type,
                ip.assigned_to_id,
                ip.is_reserved,
                ip.status::text    AS status,
                ip.version
           FROM net.ip_address ip
           LEFT JOIN net.subnet sn ON sn.id = ip.subnet_id
          WHERE ip.organization_id = $1
            AND ip.deleted_at IS NULL
            AND ($2::uuid IS NULL OR ip.subnet_id = $2)
          ORDER BY ip.address
          LIMIT 5000")
        .bind(q.organization_id)
        .bind(q.subnet_id)
        .fetch_all(&s.pool)
        .await?;
    Ok(Json(rows))
}

// ─── Server NIC thin list ───────────────────────────────────────────────

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
struct ServerNicListQuery {
    organization_id: Uuid,
    /// Optional — narrow to one server. The server-detail NICs tab
    /// uses this so the grid only carries the server's own NICs.
    server_id: Option<Uuid>,
}

#[derive(Serialize, sqlx::FromRow)]
#[serde(rename_all = "camelCase")]
struct ServerNicListRow {
    id: Uuid,
    server_id: Uuid,
    server_hostname: Option<String>,
    nic_index: i32,
    nic_name: Option<String>,
    mlag_side: Option<String>,
    target_device_hostname: Option<String>,
    target_port_interface: Option<String>,
    ip_address: Option<String>,             // host() — bare string, no /prefix
    mac_address: Option<String>,            // macaddr::text
    admin_up: bool,
    status: String,
}

/// Thin list of net.server_nic rows. 5000-row cap. Optional
/// serverId narrower (drill target from the server detail NICs
/// tab). LEFT JOINs resolve server hostname + target device hostname
/// + target port interface_name + ip_address host — everything the
/// grid needs to render without per-row round-trips.
async fn list_server_nics(
    State(s): State<AppState>,
    Query(q): Query<ServerNicListQuery>,
) -> Result<impl IntoResponse, EngineError> {
    let rows: Vec<ServerNicListRow> = sqlx::query_as(
        "SELECT n.id,
                n.server_id,
                srv.hostname            AS server_hostname,
                n.nic_index,
                n.nic_name,
                n.mlag_side,
                d.hostname              AS target_device_hostname,
                p.interface_name        AS target_port_interface,
                host(ip.address)        AS ip_address,
                n.mac_address::text     AS mac_address,
                n.admin_up,
                n.status::text          AS status
           FROM net.server_nic n
           LEFT JOIN net.server     srv ON srv.id = n.server_id
           LEFT JOIN net.device     d   ON d.id   = n.target_device_id
           LEFT JOIN net.port       p   ON p.id   = n.target_port_id
           LEFT JOIN net.ip_address ip  ON ip.id  = n.ip_address_id
          WHERE n.organization_id = $1
            AND n.deleted_at IS NULL
            AND ($2::uuid IS NULL OR n.server_id = $2)
          ORDER BY srv.hostname, n.nic_index
          LIMIT 5000")
        .bind(q.organization_id)
        .bind(q.server_id)
        .fetch_all(&s.pool)
        .await?;
    Ok(Json(rows))
}

// ─── Link endpoint thin list ────────────────────────────────────────────

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
struct LinkEndpointListQuery {
    organization_id: Uuid,
    /// Optional — narrow to one link. The link-detail page's
    /// Endpoints tab uses this so the grid only carries the pair
    /// (or hub + N spokes) of the selected link.
    link_id: Option<Uuid>,
}

#[derive(Serialize, sqlx::FromRow)]
#[serde(rename_all = "camelCase")]
struct LinkEndpointListRow {
    id: Uuid,
    link_id: Uuid,
    link_code: Option<String>,
    endpoint_order: i32,
    device_hostname: Option<String>,
    port_interface: Option<String>,     // resolved from port_id
    interface_name: Option<String>,     // free-text fallback column
    ip_address: Option<String>,         // host() — bare string
    vlan_tag: Option<i32>,
    description: Option<String>,
    status: String,
}

/// Thin list of net.link_endpoint rows. 5000-row cap. Optional
/// linkId narrower (drill target from the link detail page).
/// LEFT JOINs resolve display fields in one query so the grid
/// renders without per-row round-trips.
async fn list_link_endpoints(
    State(s): State<AppState>,
    Query(q): Query<LinkEndpointListQuery>,
) -> Result<impl IntoResponse, EngineError> {
    let rows: Vec<LinkEndpointListRow> = sqlx::query_as(
        "SELECT e.id,
                e.link_id,
                l.link_code,
                e.endpoint_order,
                d.hostname           AS device_hostname,
                p.interface_name     AS port_interface,
                e.interface_name     AS interface_name,
                host(ip.address)     AS ip_address,
                v.vlan_id            AS vlan_tag,
                e.description,
                e.status::text       AS status
           FROM net.link_endpoint e
           LEFT JOIN net.link       l  ON l.id  = e.link_id
           LEFT JOIN net.device     d  ON d.id  = e.device_id
           LEFT JOIN net.port       p  ON p.id  = e.port_id
           LEFT JOIN net.ip_address ip ON ip.id = e.ip_address_id
           LEFT JOIN net.vlan       v  ON v.id  = e.vlan_id
          WHERE e.organization_id = $1
            AND ($2::uuid IS NULL OR e.link_id = $2)
          ORDER BY l.link_code, e.endpoint_order
          LIMIT 5000")
        .bind(q.organization_id)
        .bind(q.link_id)
        .fetch_all(&s.pool)
        .await?;
    Ok(Json(rows))
}

// ─── Port thin list ─────────────────────────────────────────────────────

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
struct PortListQuery {
    organization_id: Uuid,
    /// Optional — narrow to one device (the device-detail Ports tab
    /// drill). Without it, 5000-row cap across the tenant.
    device_id: Option<Uuid>,
}

#[derive(Serialize, sqlx::FromRow)]
#[serde(rename_all = "camelCase")]
struct PortListRow {
    id: Uuid,
    device_id: Uuid,
    device_hostname: Option<String>,
    interface_name: String,
    interface_prefix: String,
    speed_mbps: Option<i64>,
    admin_up: bool,
    description: Option<String>,
    port_mode: String,
    native_vlan_id: Option<i32>,
    aggregate_ethernet_id: Option<Uuid>,
    status: String,
    version: i32,
}

/// Thin list of net.port rows. 5000-row cap. Optional deviceId
/// narrower (drill target from the device detail Ports tab).
/// Device hostname resolved via LEFT JOIN so the grid renders
/// without per-row round-trips when viewing the tenant-wide list.
async fn list_ports(
    State(s): State<AppState>,
    Query(q): Query<PortListQuery>,
) -> Result<impl IntoResponse, EngineError> {
    let rows: Vec<PortListRow> = sqlx::query_as(
        "SELECT p.id,
                p.device_id,
                d.hostname          AS device_hostname,
                p.interface_name,
                p.interface_prefix,
                p.speed_mbps,
                p.admin_up,
                p.description,
                p.port_mode,
                p.native_vlan_id,
                p.aggregate_ethernet_id,
                p.status::text      AS status,
                p.version
           FROM net.port p
           LEFT JOIN net.device d ON d.id = p.device_id
          WHERE p.organization_id = $1
            AND p.deleted_at IS NULL
            AND ($2::uuid IS NULL OR p.device_id = $2)
          ORDER BY d.hostname, p.interface_name
          LIMIT 5000")
        .bind(q.organization_id)
        .bind(q.device_id)
        .fetch_all(&s.pool)
        .await?;
    Ok(Json(rows))
}

// ─── Aggregate ethernet thin list ───────────────────────────────────────

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
struct AggregateEthernetListQuery {
    organization_id: Uuid,
    /// Optional — narrow to one device. The device-detail page can
    /// use this once an AE tab lands.
    device_id: Option<Uuid>,
}

#[derive(Serialize, sqlx::FromRow)]
#[serde(rename_all = "camelCase")]
struct AggregateEthernetListRow {
    id: Uuid,
    device_id: Uuid,
    device_hostname: Option<String>,
    ae_name: String,
    lacp_mode: String,
    min_links: i32,
    member_count: i64,     // COUNT(net.port WHERE aggregate_ethernet_id = ae.id)
    description: Option<String>,
    status: String,
    version: i32,
}

/// Thin list of aggregate_ethernet rows. 5000-row cap. Optional
/// deviceId narrower. Member count resolved via a correlated
/// subquery so operators can spot under-populated bundles at a
/// glance.
async fn list_aggregate_ethernet(
    State(s): State<AppState>,
    Query(q): Query<AggregateEthernetListQuery>,
) -> Result<impl IntoResponse, EngineError> {
    let rows: Vec<AggregateEthernetListRow> = sqlx::query_as(
        "SELECT ae.id,
                ae.device_id,
                d.hostname              AS device_hostname,
                ae.ae_name,
                ae.lacp_mode,
                ae.min_links,
                (SELECT COUNT(*)::bigint
                   FROM net.port p
                  WHERE p.aggregate_ethernet_id = ae.id
                    AND p.deleted_at IS NULL) AS member_count,
                ae.description,
                ae.status::text         AS status,
                ae.version
           FROM net.aggregate_ethernet ae
           LEFT JOIN net.device d ON d.id = ae.device_id
          WHERE ae.organization_id = $1
            AND ae.deleted_at IS NULL
            AND ($2::uuid IS NULL OR ae.device_id = $2)
          ORDER BY d.hostname, ae.ae_name
          LIMIT 5000")
        .bind(q.organization_id)
        .bind(q.device_id)
        .fetch_all(&s.pool)
        .await?;
    Ok(Json(rows))
}

// ─── MLAG domain thin list ──────────────────────────────────────────────

#[derive(Serialize, sqlx::FromRow)]
#[serde(rename_all = "camelCase")]
struct MlagDomainListRow {
    id: Uuid,
    pool_id: Uuid,
    pool_code: Option<String>,
    domain_id: i32,
    display_name: String,
    scope_level: String,
    status: String,
    version: i32,
}

/// Thin list of net.mlag_domain rows. 5000-row cap. LEFT JOIN
/// net.mlag_domain_pool for pool_code display. Used by the MLAG
/// grid + the device-detail "MLAG pairs" drill (eventually).
async fn list_mlag_domains(
    State(s): State<AppState>,
    Query(q): Query<OrgQuery>,
) -> Result<impl IntoResponse, EngineError> {
    let rows: Vec<MlagDomainListRow> = sqlx::query_as(
        "SELECT m.id,
                m.pool_id,
                p.pool_code,
                m.domain_id,
                m.display_name,
                m.scope_level,
                m.status::text AS status,
                m.version
           FROM net.mlag_domain m
           LEFT JOIN net.mlag_domain_pool p ON p.id = m.pool_id
          WHERE m.organization_id = $1 AND m.deleted_at IS NULL
          ORDER BY m.domain_id
          LIMIT 5000")
        .bind(q.organization_id)
        .fetch_all(&s.pool)
        .await?;
    Ok(Json(rows))
}

// ─── Module thin list ───────────────────────────────────────────────────

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
struct ModuleListQuery {
    organization_id: Uuid,
    /// Optional — narrow to one device. Future device-detail
    /// "Modules" tab uses this.
    device_id: Option<Uuid>,
}

#[derive(Serialize, sqlx::FromRow)]
#[serde(rename_all = "camelCase")]
struct ModuleListRow {
    id: Uuid,
    device_id: Uuid,
    device_hostname: Option<String>,
    slot: String,
    module_type: String,
    model: Option<String>,
    serial_number: Option<String>,
    part_number: Option<String>,
    status: String,
    version: i32,
}

/// Thin list of net.module rows (linecard / transceiver / PSU /
/// fan hardware). 5000-row cap. Optional deviceId narrower.
async fn list_modules(
    State(s): State<AppState>,
    Query(q): Query<ModuleListQuery>,
) -> Result<impl IntoResponse, EngineError> {
    let rows: Vec<ModuleListRow> = sqlx::query_as(
        "SELECT m.id,
                m.device_id,
                d.hostname      AS device_hostname,
                m.slot,
                m.module_type,
                m.model,
                m.serial_number,
                m.part_number,
                m.status::text  AS status,
                m.version
           FROM net.module m
           LEFT JOIN net.device d ON d.id = m.device_id
          WHERE m.organization_id = $1
            AND m.deleted_at IS NULL
            AND ($2::uuid IS NULL OR m.device_id = $2)
          ORDER BY d.hostname, m.slot
          LIMIT 5000")
        .bind(q.organization_id)
        .bind(q.device_id)
        .fetch_all(&s.pool)
        .await?;
    Ok(Json(rows))
}

// ─── MSTP priority rule thin list ───────────────────────────────────────

#[derive(Serialize, sqlx::FromRow)]
#[serde(rename_all = "camelCase")]
struct MstpRuleListRow {
    id: Uuid,
    rule_code: String,
    display_name: String,
    scope_level: String,
    scope_entity_id: Option<Uuid>,
    step_count: i64,    // COUNT(net.mstp_priority_rule_step WHERE rule_id = r.id)
    status: String,
    version: i32,
}

/// Thin list of net.mstp_priority_rule rows. 5000-row cap.
/// stepCount comes from a correlated subquery so operators can
/// tell at a glance which rules have steps defined (empty rules
/// are a config-gen smell).
async fn list_mstp_rules(
    State(s): State<AppState>,
    Query(q): Query<OrgQuery>,
) -> Result<impl IntoResponse, EngineError> {
    let rows: Vec<MstpRuleListRow> = sqlx::query_as(
        "SELECT r.id,
                r.rule_code,
                r.display_name,
                r.scope_level,
                r.scope_entity_id,
                (SELECT COUNT(*)::bigint
                   FROM net.mstp_priority_rule_step s
                  WHERE s.rule_id = r.id
                    AND s.deleted_at IS NULL) AS step_count,
                r.status::text AS status,
                r.version
           FROM net.mstp_priority_rule r
          WHERE r.organization_id = $1
            AND r.deleted_at IS NULL
          ORDER BY r.rule_code
          LIMIT 5000")
        .bind(q.organization_id)
        .fetch_all(&s.pool)
        .await?;
    Ok(Json(rows))
}

// ─── Reservation shelf thin list ────────────────────────────────────────

#[derive(Serialize, sqlx::FromRow)]
#[serde(rename_all = "camelCase")]
struct ReservationShelfListRow {
    id: Uuid,
    resource_type: String,
    resource_key: String,
    pool_id: Option<Uuid>,
    block_id: Option<Uuid>,
    retired_at: chrono::DateTime<chrono::Utc>,
    available_after: chrono::DateTime<chrono::Utc>,
    retired_reason: Option<String>,
    status: String,
    version: i32,
}

/// Thin list of net.reservation_shelf rows — retired resources in
/// cooldown before recycling. Ordered by retired_at DESC so the
/// freshest sit at top. Operators use this to eye-ball "is the
/// recycler running?" — rows where available_after is in the past
/// should have been recycled by the scheduler.
async fn list_reservation_shelf(
    State(s): State<AppState>,
    Query(q): Query<OrgQuery>,
) -> Result<impl IntoResponse, EngineError> {
    let rows: Vec<ReservationShelfListRow> = sqlx::query_as(
        "SELECT id,
                resource_type,
                resource_key,
                pool_id,
                block_id,
                retired_at,
                available_after,
                retired_reason,
                status::text AS status,
                version
           FROM net.reservation_shelf
          WHERE organization_id = $1
            AND deleted_at IS NULL
          ORDER BY retired_at DESC
          LIMIT 5000")
        .bind(q.organization_id)
        .fetch_all(&s.pool)
        .await?;
    Ok(Json(rows))
}

// ─── ASN allocation thin list ───────────────────────────────────────────

#[derive(Serialize, sqlx::FromRow)]
#[serde(rename_all = "camelCase")]
struct AsnAllocationListRow {
    id: Uuid,
    block_id: Uuid,
    block_code: Option<String>,
    asn: i64,
    allocated_to_type: String,
    allocated_to_id: Uuid,
    /// Resolved hostname when allocated_to_type is Device / Server
    /// — LEFT JOIN both tables, COALESCE to the first match. For
    /// Building / other target types, falls back to the uuid.
    target_display: Option<String>,
    status: String,
}

/// Thin list of net.asn_allocation rows. 5000-row cap. LEFT JOIN
/// net.asn_block for block_code + LEFT JOIN net.device / net.server
/// for target display. Ordered by ASN ASC so gaps are visible.
async fn list_asn_allocations(
    State(s): State<AppState>,
    Query(q): Query<OrgQuery>,
) -> Result<impl IntoResponse, EngineError> {
    let rows: Vec<AsnAllocationListRow> = sqlx::query_as(
        "SELECT a.id,
                a.block_id,
                b.block_code,
                a.asn,
                a.allocated_to_type,
                a.allocated_to_id,
                COALESCE(d.hostname, s.hostname) AS target_display,
                a.status::text AS status
           FROM net.asn_allocation a
           LEFT JOIN net.asn_block b ON b.id = a.block_id
           LEFT JOIN net.device    d ON d.id = a.allocated_to_id AND a.allocated_to_type = 'Device'
           LEFT JOIN net.server    s ON s.id = a.allocated_to_id AND a.allocated_to_type = 'Server'
          WHERE a.organization_id = $1
            AND a.deleted_at IS NULL
          ORDER BY a.asn
          LIMIT 5000")
        .bind(q.organization_id)
        .fetch_all(&s.pool)
        .await?;
    Ok(Json(rows))
}

// ─── Room + rack thin lists ─────────────────────────────────────────────

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
struct RoomListQuery {
    organization_id: Uuid,
    floor_id: Option<Uuid>,
}

#[derive(Serialize, sqlx::FromRow)]
#[serde(rename_all = "camelCase")]
struct RoomListRow {
    id: Uuid,
    floor_id: Uuid,
    room_code: String,
    room_type: String,
    max_racks: Option<i32>,
    status: String,
}

async fn list_rooms(
    State(s): State<AppState>,
    Query(q): Query<RoomListQuery>,
) -> Result<impl IntoResponse, EngineError> {
    let rows: Vec<RoomListRow> = sqlx::query_as(
        "SELECT id, floor_id, room_code, room_type, max_racks, status::text AS status
           FROM net.room
          WHERE organization_id = $1
            AND deleted_at IS NULL
            AND ($2::uuid IS NULL OR floor_id = $2)
          ORDER BY room_code
          LIMIT 5000")
        .bind(q.organization_id)
        .bind(q.floor_id)
        .fetch_all(&s.pool).await?;
    Ok(Json(rows))
}

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
struct RackListQuery {
    organization_id: Uuid,
    room_id: Option<Uuid>,
}

#[derive(Serialize, sqlx::FromRow)]
#[serde(rename_all = "camelCase")]
struct RackListRow {
    id: Uuid,
    room_id: Uuid,
    rack_code: String,
    u_height: i32,
    row: Option<String>,
    position: Option<i32>,
    max_devices: Option<i32>,
    status: String,
}

async fn list_racks(
    State(s): State<AppState>,
    Query(q): Query<RackListQuery>,
) -> Result<impl IntoResponse, EngineError> {
    let rows: Vec<RackListRow> = sqlx::query_as(
        "SELECT id, room_id, rack_code, u_height, row, position, max_devices, status::text AS status
           FROM net.rack
          WHERE organization_id = $1
            AND deleted_at IS NULL
            AND ($2::uuid IS NULL OR room_id = $2)
          ORDER BY rack_code
          LIMIT 5000")
        .bind(q.organization_id)
        .bind(q.room_id)
        .fetch_all(&s.pool).await?;
    Ok(Json(rows))
}

async fn regenerate_apply(
    State(s): State<AppState>,
    Json(req): Json<RegenerateApplyRequest>,
) -> Result<impl IntoResponse, EngineError> {
    Ok(Json(regenerate::apply(&s.pool, &req).await?))
}

// ─── Change Sets (Phase 8a) ──────────────────────────────────────────────

async fn change_set_summary_handler(
    State(s): State<AppState>,
    Query(q): Query<OrgQuery>,
) -> Result<impl IntoResponse, EngineError> {
    // Status-rollup for change-set dashboards — one SQL pass +
    // fixed 7-row output (includes zero-count statuses). No RBAC
    // gate beyond tenant scoping since the counts alone don't
    // leak set content.
    Ok(Json(change_sets::status_summary(&s.pool, q.organization_id).await?))
}

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

async fn delete_change_set_item(
    State(s): State<AppState>,
    Path((id, item_id)): Path<(Uuid, Uuid)>,
    Query(q): Query<OrgQuery>,
    headers: HeaderMap,
) -> Result<impl IntoResponse, EngineError> {
    // Soft-delete a pending item from a Draft Set. Parent-status
    // guard lives in the repo — Draft-only is enforced there.
    let repo = ChangeSetRepo::new(s.pool);
    let user_id = header_user_id(&headers);
    repo.delete_item(id, item_id, q.organization_id, user_id).await?;
    Ok(StatusCode::NO_CONTENT)
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

// ─── CLI flavors (Phase 10) ──────────────────────────────────────────────

async fn list_cli_flavors(
    State(s): State<AppState>,
    Query(q): Query<ListFlavorsQuery>,
) -> Result<impl IntoResponse, EngineError> {
    Ok(Json(cli_flavor::list_flavors(&s.pool, &q).await?))
}

async fn set_cli_flavor_config(
    State(s): State<AppState>,
    Path(code): Path<String>,
    Query(q): Query<OrgQuery>,
    headers: HeaderMap,
    Json(body): Json<SetFlavorConfigBody>,
) -> Result<impl IntoResponse, EngineError> {
    let user_id = header_user_id(&headers);
    cli_flavor::set_flavor_config(&s.pool, q.organization_id, &code, &body, user_id).await?;
    Ok(StatusCode::NO_CONTENT)
}

async fn render_device_config(
    State(s): State<AppState>,
    Path(device_id): Path<Uuid>,
    Query(q): Query<OrgQuery>,
    headers: HeaderMap,
) -> Result<impl IntoResponse, EngineError> {
    // POST → render + persist to net.rendered_config (chains to the
    // previous render via previous_render_id so "what changed" is a
    // two-row join rather than a full-text diff). RBAC: write:Device
    // at the device's scope (hierarchy-expanded via the resolver).
    let rendered_by = header_user_id(&headers);
    scope_grants::require_permission(
        &s.pool, q.organization_id, rendered_by, "write", "Device", Some(device_id),
    ).await?;
    Ok(Json(config_gen::render_device_persisted(
        &s.pool, q.organization_id, device_id, rendered_by
    ).await?))
}

#[derive(Deserialize)]
struct RenderListQuery {
    organization_id: Uuid,
    limit: Option<i64>,
}

async fn list_device_renders(
    State(s): State<AppState>,
    Path(device_id): Path<Uuid>,
    Query(q): Query<RenderListQuery>,
    headers: HeaderMap,
) -> Result<impl IntoResponse, EngineError> {
    // RBAC — reading render history is gated on read:Device at the
    // target's scope (history reveals config content so it inherits
    // the device's access control).
    let user_id = header_user_id(&headers);
    scope_grants::require_permission(
        &s.pool, q.organization_id, user_id, "read", "Device", Some(device_id),
    ).await?;
    let limit = config_gen::clamp_render_list_limit(q.limit);
    Ok(Json(config_gen::list_renders(&s.pool, q.organization_id, device_id, limit).await?))
}

async fn get_render_by_id(
    State(s): State<AppState>,
    Path(render_id): Path<Uuid>,
    Query(q): Query<OrgQuery>,
    headers: HeaderMap,
) -> Result<impl IntoResponse, EngineError> {
    // Fetch the render first so we can resolve its device_id for the
    // permission check. A bit wasteful when the caller isn't allowed
    // (we load the record only to reject), but keeps the semantic
    // clean: user needs read:Device on the device the render targets.
    let record = config_gen::get_render(&s.pool, q.organization_id, render_id).await?;
    let user_id = header_user_id(&headers);
    scope_grants::require_permission(
        &s.pool, q.organization_id, user_id, "read", "Device", Some(record.device_id),
    ).await?;
    Ok(Json(record))
}

async fn diff_render_by_id(
    State(s): State<AppState>,
    Path(render_id): Path<Uuid>,
    Query(q): Query<OrgQuery>,
    headers: HeaderMap,
) -> Result<impl IntoResponse, EngineError> {
    // Diff reveals content — same read:Device gate as get_render.
    let record = config_gen::get_render(&s.pool, q.organization_id, render_id).await?;
    let user_id = header_user_id(&headers);
    scope_grants::require_permission(
        &s.pool, q.organization_id, user_id, "read", "Device", Some(record.device_id),
    ).await?;
    Ok(Json(config_gen::diff_render(&s.pool, q.organization_id, render_id).await?))
}

async fn render_building_configs(
    State(s): State<AppState>,
    Path(building_id): Path<Uuid>,
    Query(q): Query<OrgQuery>,
    headers: HeaderMap,
) -> Result<impl IntoResponse, EngineError> {
    // POST → turn-up pack: render + persist every device in the
    // building; per-device errors surface in the result's `errors`
    // array rather than aborting the whole pack. RBAC: write:Building
    // — treated as the authorising scope for rendering all devices
    // in it. A user wanting to render a single device uses the
    // single-device endpoint (finer-grained scope check).
    let rendered_by = header_user_id(&headers);
    scope_grants::require_permission(
        &s.pool, q.organization_id, rendered_by, "write", "Building", Some(building_id),
    ).await?;
    Ok(Json(config_gen::render_building_persisted(
        &s.pool, q.organization_id, building_id, rendered_by
    ).await?))
}

async fn render_site_configs(
    State(s): State<AppState>,
    Path(site_id): Path<Uuid>,
    Query(q): Query<OrgQuery>,
    headers: HeaderMap,
) -> Result<impl IntoResponse, EngineError> {
    // Same fan-out semantics as building-level, one scope up:
    // every device across every building in the site. RBAC:
    // write:Site — expands through hierarchy so a Region-scoped
    // grant covers this too.
    let rendered_by = header_user_id(&headers);
    scope_grants::require_permission(
        &s.pool, q.organization_id, rendered_by, "write", "Site", Some(site_id),
    ).await?;
    Ok(Json(config_gen::render_site_persisted(
        &s.pool, q.organization_id, site_id, rendered_by
    ).await?))
}

async fn render_region_configs(
    State(s): State<AppState>,
    Path(region_id): Path<Uuid>,
    Query(q): Query<OrgQuery>,
    headers: HeaderMap,
) -> Result<impl IntoResponse, EngineError> {
    // Whole-estate turn-up: every device across every site in the
    // region. For single-region tenants this renders the entire
    // network in one call. RBAC: write:Region at the target id, so
    // only Global or explicit Region-scoped grants authorise.
    let rendered_by = header_user_id(&headers);
    scope_grants::require_permission(
        &s.pool, q.organization_id, rendered_by, "write", "Region", Some(region_id),
    ).await?;
    Ok(Json(config_gen::render_region_persisted(
        &s.pool, q.organization_id, region_id, rendered_by
    ).await?))
}

// ─── DHCP relay target handlers ──────────────────────────────────────────

async fn list_dhcp_relay(
    State(s): State<AppState>,
    Query(q): Query<ListDhcpRelayQuery>,
    headers: HeaderMap,
) -> Result<impl IntoResponse, EngineError> {
    let user_id = header_user_id(&headers);
    scope_grants::require_permission(
        &s.pool, q.organization_id, user_id, "read", "DhcpRelayTarget", None,
    ).await?;
    let repo = DhcpRelayRepo::new(s.pool);
    Ok(Json(repo.list(&q).await?))
}

async fn get_dhcp_relay(
    State(s): State<AppState>,
    Path(id): Path<Uuid>,
    Query(q): Query<OrgQuery>,
    headers: HeaderMap,
) -> Result<impl IntoResponse, EngineError> {
    let user_id = header_user_id(&headers);
    scope_grants::require_permission(
        &s.pool, q.organization_id, user_id, "read", "DhcpRelayTarget", Some(id),
    ).await?;
    let repo = DhcpRelayRepo::new(s.pool);
    Ok(Json(repo.get(id, q.organization_id).await?))
}

async fn create_dhcp_relay(
    State(s): State<AppState>,
    headers: HeaderMap,
    Json(body): Json<CreateDhcpRelayBody>,
) -> Result<impl IntoResponse, EngineError> {
    let user_id = header_user_id(&headers);
    scope_grants::require_permission(
        &s.pool, body.organization_id, user_id, "write", "DhcpRelayTarget", None,
    ).await?;
    let repo = DhcpRelayRepo::new(s.pool);
    let out = repo.create(&body, user_id).await?;
    Ok((StatusCode::CREATED, Json(out)))
}

async fn update_dhcp_relay(
    State(s): State<AppState>,
    Path(id): Path<Uuid>,
    Query(q): Query<OrgQuery>,
    headers: HeaderMap,
    Json(body): Json<UpdateDhcpRelayBody>,
) -> Result<impl IntoResponse, EngineError> {
    let user_id = header_user_id(&headers);
    scope_grants::require_permission(
        &s.pool, q.organization_id, user_id, "write", "DhcpRelayTarget", Some(id),
    ).await?;
    let repo = DhcpRelayRepo::new(s.pool);
    Ok(Json(repo.update(id, q.organization_id, &body, user_id).await?))
}

async fn delete_dhcp_relay(
    State(s): State<AppState>,
    Path(id): Path<Uuid>,
    Query(q): Query<OrgQuery>,
    headers: HeaderMap,
) -> Result<impl IntoResponse, EngineError> {
    let user_id = header_user_id(&headers);
    scope_grants::require_permission(
        &s.pool, q.organization_id, user_id, "delete", "DhcpRelayTarget", Some(id),
    ).await?;
    let repo = DhcpRelayRepo::new(s.pool);
    repo.soft_delete(id, q.organization_id, user_id).await?;
    Ok(StatusCode::NO_CONTENT)
}

// ─── Bulk export handlers ────────────────────────────────────────────────

/// Shared helper: standard text/csv response headers with a
/// Content-Disposition that names the download file. Keeps the
/// three export handlers DRY — they differ only in body-producer
/// and filename.
fn csv_download_headers(filename: &'static str) -> [(axum::http::header::HeaderName, axum::http::HeaderValue); 2] {
    use axum::http::{HeaderValue, header};
    // filename is a `&'static str` so from_static works for both.
    let disp = match filename {
        "devices.csv"      => HeaderValue::from_static("attachment; filename=\"devices.csv\""),
        "vlans.csv"        => HeaderValue::from_static("attachment; filename=\"vlans.csv\""),
        "ip-addresses.csv" => HeaderValue::from_static("attachment; filename=\"ip-addresses.csv\""),
        "links.csv"        => HeaderValue::from_static("attachment; filename=\"links.csv\""),
        "servers.csv"          => HeaderValue::from_static("attachment; filename=\"servers.csv\""),
        "subnets.csv"          => HeaderValue::from_static("attachment; filename=\"subnets.csv\""),
        "asn-allocations.csv"  => HeaderValue::from_static("attachment; filename=\"asn-allocations.csv\""),
        "mlag-domains.csv"       => HeaderValue::from_static("attachment; filename=\"mlag-domains.csv\""),
        "dhcp-relay-targets.csv" => HeaderValue::from_static("attachment; filename=\"dhcp-relay-targets.csv\""),
        _                        => HeaderValue::from_static("attachment"),
    };
    [
        (header::CONTENT_TYPE, HeaderValue::from_static("text/csv; charset=utf-8")),
        (header::CONTENT_DISPOSITION, disp),
    ]
}

async fn export_devices(
    State(s): State<AppState>,
    Query(q): Query<OrgQuery>,
) -> Result<impl IntoResponse, EngineError> {
    let body = bulk_export::export_devices_csv(&s.pool, q.organization_id).await?;
    Ok((csv_download_headers("devices.csv"), body))
}

async fn export_vlans(
    State(s): State<AppState>,
    Query(q): Query<OrgQuery>,
) -> Result<impl IntoResponse, EngineError> {
    let body = bulk_export::export_vlans_csv(&s.pool, q.organization_id).await?;
    Ok((csv_download_headers("vlans.csv"), body))
}

async fn export_ip_addresses(
    State(s): State<AppState>,
    Query(q): Query<OrgQuery>,
) -> Result<impl IntoResponse, EngineError> {
    let body = bulk_export::export_ip_addresses_csv(&s.pool, q.organization_id).await?;
    Ok((csv_download_headers("ip-addresses.csv"), body))
}

async fn export_links(
    State(s): State<AppState>,
    Query(q): Query<OrgQuery>,
) -> Result<impl IntoResponse, EngineError> {
    let body = bulk_export::export_links_csv(&s.pool, q.organization_id).await?;
    Ok((csv_download_headers("links.csv"), body))
}

async fn export_servers(
    State(s): State<AppState>,
    Query(q): Query<OrgQuery>,
) -> Result<impl IntoResponse, EngineError> {
    let body = bulk_export::export_servers_csv(&s.pool, q.organization_id).await?;
    Ok((csv_download_headers("servers.csv"), body))
}

async fn export_subnets(
    State(s): State<AppState>,
    Query(q): Query<OrgQuery>,
) -> Result<impl IntoResponse, EngineError> {
    let body = bulk_export::export_subnets_csv(&s.pool, q.organization_id).await?;
    Ok((csv_download_headers("subnets.csv"), body))
}

async fn export_asn_allocations(
    State(s): State<AppState>,
    Query(q): Query<OrgQuery>,
) -> Result<impl IntoResponse, EngineError> {
    let body = bulk_export::export_asn_allocations_csv(&s.pool, q.organization_id).await?;
    Ok((csv_download_headers("asn-allocations.csv"), body))
}

async fn export_mlag_domains(
    State(s): State<AppState>,
    Query(q): Query<OrgQuery>,
) -> Result<impl IntoResponse, EngineError> {
    let body = bulk_export::export_mlag_domains_csv(&s.pool, q.organization_id).await?;
    Ok((csv_download_headers("mlag-domains.csv"), body))
}

async fn export_dhcp_relay_targets(
    State(s): State<AppState>,
    Query(q): Query<OrgQuery>,
) -> Result<impl IntoResponse, EngineError> {
    let body = bulk_export::export_dhcp_relay_targets_csv(&s.pool, q.organization_id).await?;
    Ok((csv_download_headers("dhcp-relay-targets.csv"), body))
}

// ─── Bulk import handler ─────────────────────────────────────────────────

async fn import_devices_csv(
    State(s): State<AppState>,
    Query(q): Query<bulk_import::ImportQuery>,
    headers: HeaderMap,
    body: String,
) -> Result<impl IntoResponse, EngineError> {
    // Accept CSV as the raw POST body (Content-Type text/csv or
    // application/octet-stream). Keeping the body a plain String
    // means operators can `curl --data-binary @devices.csv` without
    // multipart gymnastics; file-upload UIs still work because the
    // browser's FormData upload handlers will also produce text.
    let user_id = header_user_id(&headers);
    let mode = bulk_import::ImportMode::parse(q.mode.as_deref())?;
    let result = bulk_import::import_devices(
        &s.pool, q.organization_id, &body, q.dry_run, mode, user_id
    ).await?;
    Ok(Json(result))
}

async fn import_vlans_csv(
    State(s): State<AppState>,
    Query(q): Query<bulk_import::ImportQuery>,
    headers: HeaderMap,
    body: String,
) -> Result<impl IntoResponse, EngineError> {
    let user_id = header_user_id(&headers);
    let mode = bulk_import::ImportMode::parse(q.mode.as_deref())?;
    let result = bulk_import::import_vlans(
        &s.pool, q.organization_id, &body, q.dry_run, mode, user_id
    ).await?;
    Ok(Json(result))
}

async fn import_subnets_csv(
    State(s): State<AppState>,
    Query(q): Query<bulk_import::ImportQuery>,
    headers: HeaderMap,
    body: String,
) -> Result<impl IntoResponse, EngineError> {
    let user_id = header_user_id(&headers);
    let mode = bulk_import::ImportMode::parse(q.mode.as_deref())?;
    let result = bulk_import::import_subnets(
        &s.pool, q.organization_id, &body, q.dry_run, mode, user_id
    ).await?;
    Ok(Json(result))
}

async fn import_servers_csv(
    State(s): State<AppState>,
    Query(q): Query<bulk_import::ImportQuery>,
    headers: HeaderMap,
    body: String,
) -> Result<impl IntoResponse, EngineError> {
    let user_id = header_user_id(&headers);
    let mode = bulk_import::ImportMode::parse(q.mode.as_deref())?;
    let result = bulk_import::import_servers(
        &s.pool, q.organization_id, &body, q.dry_run, mode, user_id
    ).await?;
    Ok(Json(result))
}

async fn import_dhcp_relay_targets_csv(
    State(s): State<AppState>,
    Query(q): Query<bulk_import::ImportQuery>,
    headers: HeaderMap,
    body: String,
) -> Result<impl IntoResponse, EngineError> {
    let user_id = header_user_id(&headers);
    let mode = bulk_import::ImportMode::parse(q.mode.as_deref())?;
    let result = bulk_import::import_dhcp_relay_targets(
        &s.pool, q.organization_id, &body, q.dry_run, mode, user_id
    ).await?;
    Ok(Json(result))
}

async fn import_links_csv(
    State(s): State<AppState>,
    Query(q): Query<bulk_import::ImportQuery>,
    headers: HeaderMap,
    body: String,
) -> Result<impl IntoResponse, EngineError> {
    let user_id = header_user_id(&headers);
    let mode = bulk_import::ImportMode::parse(q.mode.as_deref())?;
    let result = bulk_import::import_links(
        &s.pool, q.organization_id, &body, q.dry_run, mode, user_id
    ).await?;
    Ok(Json(result))
}

// ─── XLSX transport adapters ─────────────────────────────────────────────
//
// Each export.xlsx handler runs the existing CSV export then wraps
// the output as a workbook via xlsx_codec::csv_body_to_xlsx. Each
// import.xlsx handler does the reverse: reads the uploaded xlsx
// body, converts to CSV, feeds to the existing CSV import path.
//
// Sheet name matches the entity; download filename matches the
// sheet so operators see a self-describing file in their downloads
// folder.

fn xlsx_download_headers(filename: &'static str) -> [(axum::http::header::HeaderName, axum::http::HeaderValue); 2] {
    use axum::http::{HeaderValue, header};
    let disp = match filename {
        "devices.xlsx"            => HeaderValue::from_static("attachment; filename=\"devices.xlsx\""),
        "vlans.xlsx"              => HeaderValue::from_static("attachment; filename=\"vlans.xlsx\""),
        "subnets.xlsx"            => HeaderValue::from_static("attachment; filename=\"subnets.xlsx\""),
        "servers.xlsx"            => HeaderValue::from_static("attachment; filename=\"servers.xlsx\""),
        "links.xlsx"              => HeaderValue::from_static("attachment; filename=\"links.xlsx\""),
        "dhcp-relay-targets.xlsx" => HeaderValue::from_static("attachment; filename=\"dhcp-relay-targets.xlsx\""),
        _                         => HeaderValue::from_static("attachment"),
    };
    [
        (header::CONTENT_TYPE,
         HeaderValue::from_static("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")),
        (header::CONTENT_DISPOSITION, disp),
    ]
}

async fn export_devices_xlsx(
    State(s): State<AppState>,
    Query(q): Query<OrgQuery>,
) -> Result<impl IntoResponse, EngineError> {
    let csv = bulk_export::export_devices_csv(&s.pool, q.organization_id).await?;
    let xlsx = xlsx_codec::csv_body_to_xlsx(&csv, "devices")?;
    Ok((xlsx_download_headers("devices.xlsx"), xlsx))
}

async fn export_vlans_xlsx(
    State(s): State<AppState>,
    Query(q): Query<OrgQuery>,
) -> Result<impl IntoResponse, EngineError> {
    let csv = bulk_export::export_vlans_csv(&s.pool, q.organization_id).await?;
    let xlsx = xlsx_codec::csv_body_to_xlsx(&csv, "vlans")?;
    Ok((xlsx_download_headers("vlans.xlsx"), xlsx))
}

async fn export_subnets_xlsx(
    State(s): State<AppState>,
    Query(q): Query<OrgQuery>,
) -> Result<impl IntoResponse, EngineError> {
    let csv = bulk_export::export_subnets_csv(&s.pool, q.organization_id).await?;
    let xlsx = xlsx_codec::csv_body_to_xlsx(&csv, "subnets")?;
    Ok((xlsx_download_headers("subnets.xlsx"), xlsx))
}

async fn export_servers_xlsx(
    State(s): State<AppState>,
    Query(q): Query<OrgQuery>,
) -> Result<impl IntoResponse, EngineError> {
    let csv = bulk_export::export_servers_csv(&s.pool, q.organization_id).await?;
    let xlsx = xlsx_codec::csv_body_to_xlsx(&csv, "servers")?;
    Ok((xlsx_download_headers("servers.xlsx"), xlsx))
}

async fn export_links_xlsx(
    State(s): State<AppState>,
    Query(q): Query<OrgQuery>,
) -> Result<impl IntoResponse, EngineError> {
    let csv = bulk_export::export_links_csv(&s.pool, q.organization_id).await?;
    let xlsx = xlsx_codec::csv_body_to_xlsx(&csv, "links")?;
    Ok((xlsx_download_headers("links.xlsx"), xlsx))
}

async fn export_dhcp_relay_xlsx(
    State(s): State<AppState>,
    Query(q): Query<OrgQuery>,
) -> Result<impl IntoResponse, EngineError> {
    let csv = bulk_export::export_dhcp_relay_targets_csv(&s.pool, q.organization_id).await?;
    let xlsx = xlsx_codec::csv_body_to_xlsx(&csv, "dhcp-relay-targets")?;
    Ok((xlsx_download_headers("dhcp-relay-targets.xlsx"), xlsx))
}

// Import.xlsx handlers take raw bytes (axum::body::Bytes) and
// delegate to the CSV import path after decoding.

async fn import_devices_xlsx(
    State(s): State<AppState>,
    Query(q): Query<bulk_import::ImportQuery>,
    headers: HeaderMap,
    body: axum::body::Bytes,
) -> Result<impl IntoResponse, EngineError> {
    let user_id = header_user_id(&headers);
    let mode = bulk_import::ImportMode::parse(q.mode.as_deref())?;
    let csv = xlsx_codec::xlsx_bytes_to_csv(&body)?;
    let result = bulk_import::import_devices(
        &s.pool, q.organization_id, &csv, q.dry_run, mode, user_id
    ).await?;
    Ok(Json(result))
}

async fn import_vlans_xlsx(
    State(s): State<AppState>,
    Query(q): Query<bulk_import::ImportQuery>,
    headers: HeaderMap,
    body: axum::body::Bytes,
) -> Result<impl IntoResponse, EngineError> {
    let user_id = header_user_id(&headers);
    let mode = bulk_import::ImportMode::parse(q.mode.as_deref())?;
    let csv = xlsx_codec::xlsx_bytes_to_csv(&body)?;
    let result = bulk_import::import_vlans(
        &s.pool, q.organization_id, &csv, q.dry_run, mode, user_id
    ).await?;
    Ok(Json(result))
}

async fn import_subnets_xlsx(
    State(s): State<AppState>,
    Query(q): Query<bulk_import::ImportQuery>,
    headers: HeaderMap,
    body: axum::body::Bytes,
) -> Result<impl IntoResponse, EngineError> {
    let user_id = header_user_id(&headers);
    let mode = bulk_import::ImportMode::parse(q.mode.as_deref())?;
    let csv = xlsx_codec::xlsx_bytes_to_csv(&body)?;
    let result = bulk_import::import_subnets(
        &s.pool, q.organization_id, &csv, q.dry_run, mode, user_id
    ).await?;
    Ok(Json(result))
}

async fn import_servers_xlsx(
    State(s): State<AppState>,
    Query(q): Query<bulk_import::ImportQuery>,
    headers: HeaderMap,
    body: axum::body::Bytes,
) -> Result<impl IntoResponse, EngineError> {
    let user_id = header_user_id(&headers);
    let mode = bulk_import::ImportMode::parse(q.mode.as_deref())?;
    let csv = xlsx_codec::xlsx_bytes_to_csv(&body)?;
    let result = bulk_import::import_servers(
        &s.pool, q.organization_id, &csv, q.dry_run, mode, user_id
    ).await?;
    Ok(Json(result))
}

async fn import_links_xlsx(
    State(s): State<AppState>,
    Query(q): Query<bulk_import::ImportQuery>,
    headers: HeaderMap,
    body: axum::body::Bytes,
) -> Result<impl IntoResponse, EngineError> {
    let user_id = header_user_id(&headers);
    let mode = bulk_import::ImportMode::parse(q.mode.as_deref())?;
    let csv = xlsx_codec::xlsx_bytes_to_csv(&body)?;
    let result = bulk_import::import_links(
        &s.pool, q.organization_id, &csv, q.dry_run, mode, user_id
    ).await?;
    Ok(Json(result))
}

// ─── Saved view handlers ─────────────────────────────────────────────────

async fn list_saved_views(
    State(s): State<AppState>,
    Query(q): Query<ListSavedViewsQuery>,
    headers: HeaderMap,
) -> Result<impl IntoResponse, EngineError> {
    let user_id = header_user_id(&headers);
    let repo = SavedViewRepo::new(s.pool);
    Ok(Json(repo.list(&q, user_id).await?))
}

async fn get_saved_view(
    State(s): State<AppState>,
    Path(id): Path<Uuid>,
    Query(q): Query<OrgQuery>,
    headers: HeaderMap,
) -> Result<impl IntoResponse, EngineError> {
    let user_id = header_user_id(&headers);
    let repo = SavedViewRepo::new(s.pool);
    Ok(Json(repo.get(id, q.organization_id, user_id).await?))
}

async fn create_saved_view(
    State(s): State<AppState>,
    headers: HeaderMap,
    Json(body): Json<CreateSavedViewBody>,
) -> Result<impl IntoResponse, EngineError> {
    let user_id = header_user_id(&headers);
    let repo = SavedViewRepo::new(s.pool);
    let out = repo.create(&body, user_id).await?;
    Ok((StatusCode::CREATED, Json(out)))
}

async fn update_saved_view(
    State(s): State<AppState>,
    Path(id): Path<Uuid>,
    Query(q): Query<OrgQuery>,
    headers: HeaderMap,
    Json(body): Json<UpdateSavedViewBody>,
) -> Result<impl IntoResponse, EngineError> {
    let user_id = header_user_id(&headers);
    let repo = SavedViewRepo::new(s.pool);
    Ok(Json(repo.update(id, q.organization_id, &body, user_id).await?))
}

async fn delete_saved_view(
    State(s): State<AppState>,
    Path(id): Path<Uuid>,
    Query(q): Query<OrgQuery>,
    headers: HeaderMap,
) -> Result<impl IntoResponse, EngineError> {
    let user_id = header_user_id(&headers);
    let repo = SavedViewRepo::new(s.pool);
    repo.soft_delete(id, q.organization_id, user_id).await?;
    Ok(StatusCode::NO_CONTENT)
}

async fn global_search_handler(
    State(s): State<AppState>,
    Query(q): Query<search::SearchQuery>,
    headers: HeaderMap,
) -> Result<impl IntoResponse, EngineError> {
    let user_id = header_user_id(&headers);
    let limit = search::clamp_search_limit(q.limit);
    let entity_types = search::parse_entity_types(q.entity_types.as_deref());

    let raw = search::global_search(
        &s.pool, q.organization_id, &q.q, entity_types.as_ref(), limit,
    ).await?;

    // RBAC post-filter. Drop rows the caller can't read. Service
    // calls (no X-User-Id) skip the check and see everything — same
    // rule as the rest of the surface.
    let Some(uid) = user_id else { return Ok(Json(raw)); };

    let mut filtered = Vec::with_capacity(raw.len());
    for r in raw {
        let decision = scope_grants::has_permission(
            &s.pool, q.organization_id, uid, "read", &r.entity_type, Some(r.id),
        ).await?;
        if decision.allowed { filtered.push(r); }
    }
    Ok(Json(filtered))
}

/// Who-am-i — identity + scope-grant summary for the caller.
/// Drives a web session banner ("signed in as user 3, 60 grants,
/// can write Device + read Server"). Service calls (no X-User-Id
/// header) get {userId: null, grantCount: 0, actions: [], entityTypes: []}.
#[derive(Serialize)]
#[serde(rename_all = "camelCase")]
struct WhoAmIResponse {
    user_id: Option<i32>,
    grant_count: i64,
    /// Distinct set of actions the caller holds ANY grant for, sorted.
    actions: Vec<String>,
    /// Distinct set of entity types the caller holds ANY grant for, sorted.
    entity_types: Vec<String>,
}

async fn whoami_handler(
    State(s): State<AppState>,
    Query(q): Query<OrgQuery>,
    headers: HeaderMap,
) -> Result<impl IntoResponse, EngineError> {
    let user_id = header_user_id(&headers);
    let Some(uid) = user_id else {
        return Ok(Json(WhoAmIResponse {
            user_id: None, grant_count: 0,
            actions: Vec::new(), entity_types: Vec::new(),
        }));
    };

    // One round-trip: aggregate count + distinct actions + distinct
    // entity types across the caller's active Global-and-scoped grants.
    let (grant_count, actions, entity_types): (i64, Vec<String>, Vec<String>) =
        sqlx::query_as(
            "SELECT COUNT(*)::bigint,
                    COALESCE(array_agg(DISTINCT action      ORDER BY action),      '{}')::text[],
                    COALESCE(array_agg(DISTINCT entity_type ORDER BY entity_type), '{}')::text[]
               FROM net.scope_grant
              WHERE organization_id = $1
                AND user_id = $2
                AND deleted_at IS NULL
                AND status = 'Active'::net.entity_status")
            .bind(q.organization_id)
            .bind(uid)
            .fetch_one(&s.pool)
            .await?;

    Ok(Json(WhoAmIResponse {
        user_id: Some(uid), grant_count, actions, entity_types,
    }))
}

/// Full grant list for the current caller. Unlike
/// `list_scope_grants` which is gated on `read:ScopeGrant` (a meta-
/// permission for admins reviewing other users' access), this
/// endpoint always returns the CALLER's own grants — reading your
/// own access is always permitted. Service-origin callers (no
/// X-User-Id header) get an empty list. Ordered by entity_type +
/// action so the UI can show a clean table without client-side
/// sort tricks.
async fn whoami_grants_handler(
    State(s): State<AppState>,
    Query(q): Query<OrgQuery>,
    headers: HeaderMap,
) -> Result<impl IntoResponse, EngineError> {
    let user_id = header_user_id(&headers);
    let Some(uid) = user_id else {
        return Ok(Json(Vec::<scope_grants::ScopeGrant>::new()));
    };
    let rows: Vec<scope_grants::ScopeGrant> = sqlx::query_as(
        "SELECT id, organization_id, user_id, action, entity_type,
                scope_type, scope_entity_id, status::text AS status,
                version, created_at, updated_at, notes
           FROM net.scope_grant
          WHERE organization_id = $1
            AND user_id = $2
            AND deleted_at IS NULL
          ORDER BY entity_type ASC, action ASC, scope_type ASC")
        .bind(q.organization_id)
        .bind(uid)
        .fetch_all(&s.pool)
        .await?;
    Ok(Json(rows))
}

/// Facet counts — one row per entity type that matches the query.
/// Used by the web + WPF search UIs to render a narrowing bar
/// ("Device(12) · Vlan(4) · Subnet(1)") before the operator commits
/// to a filter. Unlike `/api/net/search` itself, this endpoint returns
/// counts without enforcing the RBAC post-filter: the counts are a
/// hint for UI narrowing, and the facet-count + hit-count drift is
/// bounded by how many unreadable rows match the query (typically 0
/// for scoped operators). RBAC still enforced at drill time.
async fn search_facets_handler(
    State(s): State<AppState>,
    Query(q): Query<search::SearchQuery>,
) -> Result<impl IntoResponse, EngineError> {
    let entity_types = search::parse_entity_types(q.entity_types.as_deref());
    let rows = search::search_facets(
        &s.pool, q.organization_id, &q.q, entity_types.as_ref(),
    ).await?;
    Ok(Json(rows))
}

async fn import_dhcp_relay_xlsx(
    State(s): State<AppState>,
    Query(q): Query<bulk_import::ImportQuery>,
    headers: HeaderMap,
    body: axum::body::Bytes,
) -> Result<impl IntoResponse, EngineError> {
    let user_id = header_user_id(&headers);
    let mode = bulk_import::ImportMode::parse(q.mode.as_deref())?;
    let csv = xlsx_codec::xlsx_bytes_to_csv(&body)?;
    let result = bulk_import::import_dhcp_relay_targets(
        &s.pool, q.organization_id, &csv, q.dry_run, mode, user_id
    ).await?;
    Ok(Json(result))
}

async fn bulk_edit_devices_handler(
    State(s): State<AppState>,
    Query(q): Query<BulkEditQuery>,
    headers: HeaderMap,
    Json(body): Json<BulkEditDevicesBody>,
) -> Result<impl IntoResponse, EngineError> {
    let user_id = header_user_id(&headers);
    let result = bulk_edit::bulk_edit_devices(
        &s.pool, q.organization_id, &body, q.dry_run, user_id,
    ).await?;
    Ok(Json(result))
}

async fn bulk_edit_vlans_handler(
    State(s): State<AppState>,
    Query(q): Query<BulkEditQuery>,
    headers: HeaderMap,
    Json(body): Json<BulkEditVlansBody>,
) -> Result<impl IntoResponse, EngineError> {
    let user_id = header_user_id(&headers);
    let result = bulk_edit::bulk_edit_vlans(
        &s.pool, q.organization_id, &body, q.dry_run, user_id,
    ).await?;
    Ok(Json(result))
}

async fn bulk_edit_subnets_handler(
    State(s): State<AppState>,
    Query(q): Query<BulkEditQuery>,
    headers: HeaderMap,
    Json(body): Json<BulkEditSubnetsBody>,
) -> Result<impl IntoResponse, EngineError> {
    let user_id = header_user_id(&headers);
    let result = bulk_edit::bulk_edit_subnets(
        &s.pool, q.organization_id, &body, q.dry_run, user_id,
    ).await?;
    Ok(Json(result))
}

async fn bulk_edit_servers_handler(
    State(s): State<AppState>,
    Query(q): Query<BulkEditQuery>,
    headers: HeaderMap,
    Json(body): Json<BulkEditServersBody>,
) -> Result<impl IntoResponse, EngineError> {
    let user_id = header_user_id(&headers);
    let result = bulk_edit::bulk_edit_servers(
        &s.pool, q.organization_id, &body, q.dry_run, user_id,
    ).await?;
    Ok(Json(result))
}

async fn bulk_edit_dhcp_relay_handler(
    State(s): State<AppState>,
    Query(q): Query<BulkEditQuery>,
    headers: HeaderMap,
    Json(body): Json<BulkEditDhcpRelayTargetsBody>,
) -> Result<impl IntoResponse, EngineError> {
    let user_id = header_user_id(&headers);
    let result = bulk_edit::bulk_edit_dhcp_relay_targets(
        &s.pool, q.organization_id, &body, q.dry_run, user_id,
    ).await?;
    Ok(Json(result))
}

// ─── Scope grant handlers ────────────────────────────────────────────────

async fn list_scope_grants(
    State(s): State<AppState>,
    Query(q): Query<ListScopeGrantsQuery>,
    headers: HeaderMap,
) -> Result<impl IntoResponse, EngineError> {
    // Meta-protection: reading the grant catalog itself is gated
    // on read:ScopeGrant. Without this, a user with no grants could
    // inspect the whole auth table (which is both a privacy leak
    // — "who else has access?" — and a recon step towards privilege
    // escalation via other paths).
    let user_id = header_user_id(&headers);
    scope_grants::require_permission(
        &s.pool, q.organization_id, user_id, "read", "ScopeGrant", None,
    ).await?;
    let repo = ScopeGrantRepo::new(s.pool);
    Ok(Json(repo.list(&q).await?))
}

async fn get_scope_grant(
    State(s): State<AppState>,
    Path(id): Path<Uuid>,
    Query(q): Query<OrgQuery>,
    headers: HeaderMap,
) -> Result<impl IntoResponse, EngineError> {
    let user_id = header_user_id(&headers);
    scope_grants::require_permission(
        &s.pool, q.organization_id, user_id, "read", "ScopeGrant", Some(id),
    ).await?;
    let repo = ScopeGrantRepo::new(s.pool);
    Ok(Json(repo.get(id, q.organization_id).await?))
}

async fn create_scope_grant(
    State(s): State<AppState>,
    headers: HeaderMap,
    Json(body): Json<CreateScopeGrantBody>,
) -> Result<impl IntoResponse, EngineError> {
    // Meta-protection on create is CRITICAL: without gating, any
    // tenant user could elevate themselves to Global write on
    // everything by POSTing their own grant. write:ScopeGrant is
    // the bootstrap permission the root admin grants to themselves
    // (directly in the DB, or via a break-glass service path).
    let user_id = header_user_id(&headers);
    scope_grants::require_permission(
        &s.pool, body.organization_id, user_id, "write", "ScopeGrant", None,
    ).await?;
    let repo = ScopeGrantRepo::new(s.pool);
    let out = repo.create(&body, user_id).await?;
    Ok((StatusCode::CREATED, Json(out)))
}

async fn delete_scope_grant(
    State(s): State<AppState>,
    Path(id): Path<Uuid>,
    Query(q): Query<OrgQuery>,
    headers: HeaderMap,
) -> Result<impl IntoResponse, EngineError> {
    let user_id = header_user_id(&headers);
    scope_grants::require_permission(
        &s.pool, q.organization_id, user_id, "delete", "ScopeGrant", Some(id),
    ).await?;
    let repo = ScopeGrantRepo::new(s.pool);
    repo.soft_delete(id, q.organization_id, user_id).await?;
    Ok(StatusCode::NO_CONTENT)
}

#[derive(Deserialize)]
#[serde(rename_all = "camelCase")]
struct CheckPermissionQuery {
    organization_id: Uuid,
    user_id: i32,
    action: String,
    entity_type: String,
    entity_id: Option<Uuid>,
}

/// Dry-run the permission resolver without enforcing. Useful for UI
/// feedback ("you'd be denied if you hit save") and for confirming
/// a grant was set up correctly after creating it.
async fn check_permission_handler(
    State(s): State<AppState>,
    Query(q): Query<CheckPermissionQuery>,
) -> Result<impl IntoResponse, EngineError> {
    let decision = scope_grants::has_permission(
        &s.pool, q.organization_id, q.user_id, &q.action, &q.entity_type, q.entity_id,
    ).await?;
    Ok(Json(decision))
}
