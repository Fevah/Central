//! Axum routing. Mirrors the surface the desktop's `Central.ApiClient.NetworkingEngine`
//! wrapper will call. Every handler delegates straight to a service type — handlers
//! stay thin so the tests focus on the services, not the transport.

use axum::{
    extract::{Path, State, Query},
    http::{HeaderMap, StatusCode},
    response::IntoResponse,
    routing::{get, post},
    Json, Router,
};
use chrono::Duration;
use serde::{Deserialize, Serialize};
use sqlx::PgPool;
use uuid::Uuid;

use crate::allocation::AllocationService;
use crate::error::EngineError;
use crate::ip_allocation::IpAllocationService;
use crate::models::{PoolScopeLevel, ShelfResourceType};
use crate::naming::{self, DeviceNamingContext, LinkNamingContext, ServerNamingContext};
use crate::naming_overrides::{
    CreateOverrideBody, ListOverridesQuery, NamingOverrideRepo, UpdateOverrideBody,
};
use crate::naming_resolver::{NamingResolver, ResolveRequest};
use crate::server_fanout::{ServerCreationRequest, ServerCreationService};

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
