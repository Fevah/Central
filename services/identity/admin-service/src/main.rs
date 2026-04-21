//! Central identity admin-service — Phase 4 of docs/IDP_BUILDOUT.md.
//!
//! Separate binary from auth-service because:
//!   * Different blast radius. auth-service handles every login; a bad
//!     deploy locks users out. admin-service touches admin paths only;
//!     failures degrade UX for operators, not end-users.
//!   * Different auth posture. auth-service authenticates users;
//!     admin-service requires an already-issued JWT carrying role=
//!     global_admin. The JWT verification middleware lives here.
//!   * Different scale. admin-service sees low traffic + doesn't need
//!     to scale horizontally. Gets a smaller DB pool.
//!
//! Shares the same `secure_auth.*` DB + the same JWT signing secret
//! as auth-service — config comes from the same auth-service.toml
//! file (there's one tenant + one platform; one identity service's
//! worth of config). Port 8083 by default; `ADMIN_SERVICE_PORT` env
//! overrides.
//!
//! Phase 4 ships a subset of the endpoints the plan lists:
//!   * GET  /api/v1/admin/users
//!   * GET  /api/v1/admin/users/:id
//!   * PATCH /api/v1/admin/users/:id
//!   * POST /api/v1/admin/users/:id/unlock
//!   * POST /api/v1/admin/users/:id/revoke-all-sessions
//!   * POST /api/v1/admin/users/:id/force-reset
//!   * GET  /api/v1/admin/audit/logins
//!   * GET  /api/v1/admin/audit/sessions
//!   * GET  /health
//!
//! Deferred to Phase 4.B: POST /users (create), DELETE /users (soft),
//! tenants endpoints, SSO provider CRUD, password-changes + sso-
//! callbacks audit endpoints. Scaffolded here as stubs so the route
//! surface is visible + returns 501 rather than 404.

use std::{net::SocketAddr, path::PathBuf, sync::Arc};

use anyhow::Context;
use axum::{
    extract::{Path, Query, State},
    http::{HeaderMap, StatusCode},
    middleware::{self, Next},
    response::IntoResponse,
    routing::{get, patch, post},
    Json, Router,
};
use serde::{Deserialize, Serialize};
use sqlx::postgres::{PgPool, PgPoolOptions};
use uuid::Uuid;

use identity_core::jwt::{decode_with_leeway, Claims};

#[tokio::main]
async fn main() -> anyhow::Result<()> {
    tracing_subscriber::fmt()
        .with_env_filter(tracing_subscriber::EnvFilter::from_default_env())
        .json()
        .init();

    let cfg = load_config()?;
    let port: u16 = std::env::var("ADMIN_SERVICE_PORT")
        .ok().and_then(|v| v.parse().ok())
        .unwrap_or(8083);
    let bind: SocketAddr = format!("{}:{}", cfg.server.host, port)
        .parse().context("invalid server host/port")?;

    let pool = PgPoolOptions::new()
        .max_connections(cfg.database.pool_size.max(4) as u32)
        .acquire_timeout(std::time::Duration::from_secs(cfg.database.pool_timeout_seconds))
        .connect(&cfg.db_url())
        .await
        .context("connecting to admin-service database")?;

    let state = Arc::new(AppState {
        pool,
        jwt_secret: cfg.effective_jwt_secret(),
    });

    // Every /api/v1/admin/* route requires a global_admin claim; the
    // middleware rejects anything else with 401 / 403. /health skips
    // the middleware so ops tools can probe without auth.
    let admin_routes = Router::new()
        .route("/users",                                  get(users_list).post(users_create))
        .route("/users/:id",                              get(users_get).patch(users_patch).delete(users_delete))
        .route("/users/:id/unlock",                       post(users_unlock))
        .route("/users/:id/revoke-all-sessions",          post(users_revoke_all_sessions))
        .route("/users/:id/force-reset",                  post(users_force_reset))
        .route("/tenants",                                get(tenants_list))
        .route("/sso/providers",                          get(sso_providers_list_admin).post(sso_providers_create))
        .route("/sso/providers/:code",                    patch(sso_providers_update).delete(sso_providers_delete))
        .route("/audit/logins",                           get(audit_logins))
        .route("/audit/sessions",                         get(audit_sessions))
        .route("/audit/password-changes",                 get(audit_password_changes))
        .route("/audit/sso-callbacks",                    get(audit_sso_callbacks))
        .route_layer(middleware::from_fn_with_state(state.clone(), require_global_admin));

    let app = Router::new()
        .route("/health", get(health))
        .nest("/api/v1/admin", admin_routes)
        .layer(tower_http::cors::CorsLayer::permissive())
        .layer(tower_http::trace::TraceLayer::new_for_http())
        .with_state(state);

    tracing::info!(%bind, "admin-service listening");
    let listener = tokio::net::TcpListener::bind(bind).await?;
    axum::serve(listener, app.into_make_service()).await?;
    Ok(())
}

// ─── Config (shared shape with auth-service) ──────────────────────────────

#[derive(Debug, Deserialize)]
struct Config { server: ServerCfg, database: DbCfg, jwt: JwtCfg }

#[derive(Debug, Deserialize)]
struct ServerCfg { host: String, #[allow(dead_code)] port: u16 }

#[derive(Debug, Deserialize)]
struct DbCfg {
    host: String, port: u16, username: String, password: String,
    database: String, ssl_mode: String,
    pool_size: i32, pool_timeout_seconds: u64,
}

#[derive(Debug, Deserialize)]
#[allow(dead_code)]
struct JwtCfg {
    access_token_ttl_seconds: i64,
    #[serde(default)]
    refresh_token_ttl_seconds: i64,
    issuer: String, audience: String,
    #[serde(default)]
    secret: Option<String>,
}

impl Config {
    fn db_url(&self) -> String {
        format!("postgres://{}:{}@{}:{}/{}?sslmode={}",
            self.database.username, self.database.password,
            self.database.host, self.database.port,
            self.database.database, self.database.ssl_mode)
    }
    fn effective_jwt_secret(&self) -> String {
        // AUTH_SERVICE_JWT_SECRET wins so admin + auth agree on the
        // same secret without separate env vars — they're ONE
        // identity surface split across two binaries.
        if let Ok(v) = std::env::var("AUTH_SERVICE_JWT_SECRET") {
            if !v.is_empty() { return v; }
        }
        self.jwt.secret.clone().unwrap_or_else(|| {
            tracing::warn!("no JWT secret in config or env — tokens are insecure");
            "dev-only-insecure-secret".into()
        })
    }
}

fn load_config() -> anyhow::Result<Config> {
    let path = resolve_config_path()?;
    let text = std::fs::read_to_string(&path)
        .with_context(|| format!("reading config at {}", path.display()))?;
    let cfg: Config = toml::from_str(&text)
        .with_context(|| format!("parsing config at {}", path.display()))?;
    tracing::info!(path = %path.display(), "admin-service config loaded");
    Ok(cfg)
}

fn resolve_config_path() -> anyhow::Result<PathBuf> {
    if let Ok(v) = std::env::var("CENTRAL_AUTH_CONFIG") {
        return Ok(PathBuf::from(v));
    }
    let start = std::env::current_dir().context("cwd")?;
    let mut dir: Option<&std::path::Path> = Some(start.as_path());
    while let Some(d) = dir {
        let candidate = d.join("config").join("auth-service.toml");
        if candidate.exists() { return Ok(candidate); }
        dir = d.parent();
    }
    anyhow::bail!("config/auth-service.toml not found above cwd");
}

// ─── State + JWT middleware ───────────────────────────────────────────────

struct AppState {
    pool:       PgPool,
    jwt_secret: String,
}

#[derive(Clone)]
struct AdminPrincipal {
    user_id: Uuid,
    email:   String,
    #[allow(dead_code)]
    sid:     String,
}

/// Verify the bearer token, require role=global_admin in the DB
/// (not just the claim — claims can lie; the DB row is authoritative).
/// On success attaches [`AdminPrincipal`] to the request extensions
/// for handlers to pick up.
async fn require_global_admin(
    State(state): State<Arc<AppState>>,
    headers: HeaderMap,
    mut req: axum::extract::Request,
    next: Next,
) -> axum::response::Response {
    let token = match headers
        .get("authorization")
        .and_then(|v| v.to_str().ok())
        .and_then(|s| s.strip_prefix("Bearer "))
    {
        Some(t) if !t.is_empty() => t.to_owned(),
        _ => return auth_error(StatusCode::UNAUTHORIZED, "missing bearer token"),
    };

    let claims: Claims = match decode_with_leeway(&token, state.jwt_secret.as_bytes(), true) {
        Ok(c)  => c,
        Err(_) => return auth_error(StatusCode::UNAUTHORIZED, "invalid or expired token"),
    };
    let user_id = match Uuid::parse_str(&claims.sub) {
        Ok(u)  => u,
        Err(_) => return auth_error(StatusCode::UNAUTHORIZED, "malformed subject"),
    };

    // DB-authoritative role check. is_global_admin OR role = 'global_admin'
    // — both paths because the new role column is the canonical one but
    // Phase 1 users only had is_global_admin.
    let row: Result<Option<(bool, Option<String>, bool)>, _> = sqlx::query_as(
        "SELECT is_global_admin, role, is_active
           FROM secure_auth.users
          WHERE id = $1 AND deleted_at IS NULL",
    )
    .bind(user_id)
    .fetch_optional(&state.pool)
    .await;

    match row {
        Ok(Some((is_global_admin, role, is_active))) => {
            if !is_active {
                return auth_error(StatusCode::FORBIDDEN, "account disabled");
            }
            let role_match = role.as_deref() == Some("global_admin");
            if !is_global_admin && !role_match {
                return auth_error(StatusCode::FORBIDDEN, "requires global_admin role");
            }
        }
        Ok(None) => return auth_error(StatusCode::UNAUTHORIZED, "subject not found"),
        Err(e) => {
            tracing::error!(error = %e, "admin auth DB lookup failed");
            return auth_error(StatusCode::INTERNAL_SERVER_ERROR, "auth check failed");
        }
    }

    req.extensions_mut().insert(AdminPrincipal {
        user_id,
        email: claims.email.clone(),
        sid:   claims.sid.clone(),
    });
    next.run(req).await
}

fn auth_error(status: StatusCode, detail: &str) -> axum::response::Response {
    (status, Json(serde_json::json!({
        "error":  if status == StatusCode::UNAUTHORIZED { "unauthorized" } else { "forbidden" },
        "detail": detail,
    }))).into_response()
}

fn internal_error(detail: &'static str) -> axum::response::Response {
    (StatusCode::INTERNAL_SERVER_ERROR, Json(serde_json::json!({
        "error": "internal", "detail": detail,
    }))).into_response()
}

// ─── Health ───────────────────────────────────────────────────────────────

async fn health(State(state): State<Arc<AppState>>) -> impl IntoResponse {
    match sqlx::query_scalar::<_, i32>("SELECT 1").fetch_one(&state.pool).await {
        Ok(_)  => (StatusCode::OK, Json(serde_json::json!({"status": "ok"}))).into_response(),
        Err(e) => {
            tracing::error!(error = %e, "health db probe failed");
            (StatusCode::SERVICE_UNAVAILABLE, Json(serde_json::json!({
                "status": "degraded", "detail": "database unreachable",
            }))).into_response()
        }
    }
}

// ─── Users ────────────────────────────────────────────────────────────────

#[derive(Debug, Deserialize)]
struct UsersListQuery {
    #[serde(default)]     q:        Option<String>,
    #[serde(default = "page_default")]     page:     i64,
    #[serde(default = "per_page_default")] per_page: i64,
}
fn page_default() -> i64 { 1 }
fn per_page_default() -> i64 { 50 }

#[derive(Debug, Serialize, sqlx::FromRow)]
struct UserRow {
    id:               Uuid,
    email:            String,
    username:         Option<String>,
    display_name:     Option<String>,
    role:             Option<String>,
    is_active:        bool,
    is_global_admin:  bool,
    mfa_enabled:      bool,
    duo_enabled:      bool,
    last_login_at:    Option<chrono::DateTime<chrono::Utc>>,
    created_at:       chrono::DateTime<chrono::Utc>,
}

#[derive(Debug, Serialize)]
struct UsersListResponse {
    total:    i64,
    page:     i64,
    per_page: i64,
    users:    Vec<UserRow>,
}

async fn users_list(
    State(state): State<Arc<AppState>>,
    Query(q): Query<UsersListQuery>,
) -> impl IntoResponse {
    let per_page = q.per_page.clamp(1, 200);
    let page     = q.page.max(1);
    let offset   = (page - 1) * per_page;
    let search   = q.q.as_deref().unwrap_or("").trim().to_string();
    let like     = format!("%{}%", search.to_lowercase());

    // COUNT + page in parallel. Two queries but shares the pool —
    // admin-service's bound traffic keeps this fine.
    let total: Result<i64, _> = if search.is_empty() {
        sqlx::query_scalar("SELECT COUNT(*) FROM secure_auth.users WHERE deleted_at IS NULL")
            .fetch_one(&state.pool).await
    } else {
        sqlx::query_scalar(
            "SELECT COUNT(*) FROM secure_auth.users
              WHERE deleted_at IS NULL
                AND (lower(email) LIKE $1
                     OR lower(COALESCE(username, '')) LIKE $1
                     OR lower(COALESCE(display_name, '')) LIKE $1)",
        ).bind(&like).fetch_one(&state.pool).await
    };
    let total = match total {
        Ok(n) => n,
        Err(e) => { tracing::error!(error = %e, "users count"); return internal_error("database error"); }
    };

    let rows: Result<Vec<UserRow>, _> = if search.is_empty() {
        sqlx::query_as(
            "SELECT id, email, username, display_name, role, is_active,
                    is_global_admin, mfa_enabled, duo_enabled,
                    last_login_at, created_at
               FROM secure_auth.users
              WHERE deleted_at IS NULL
              ORDER BY created_at DESC
              LIMIT $1 OFFSET $2",
        ).bind(per_page).bind(offset).fetch_all(&state.pool).await
    } else {
        sqlx::query_as(
            "SELECT id, email, username, display_name, role, is_active,
                    is_global_admin, mfa_enabled, duo_enabled,
                    last_login_at, created_at
               FROM secure_auth.users
              WHERE deleted_at IS NULL
                AND (lower(email) LIKE $1
                     OR lower(COALESCE(username, '')) LIKE $1
                     OR lower(COALESCE(display_name, '')) LIKE $1)
              ORDER BY created_at DESC
              LIMIT $2 OFFSET $3",
        ).bind(&like).bind(per_page).bind(offset).fetch_all(&state.pool).await
    };
    let users = match rows {
        Ok(r)  => r,
        Err(e) => { tracing::error!(error = %e, "users list"); return internal_error("database error"); }
    };

    (StatusCode::OK, Json(UsersListResponse { total, page, per_page, users })).into_response()
}

async fn users_get(
    State(state): State<Arc<AppState>>,
    Path(id): Path<Uuid>,
) -> impl IntoResponse {
    let row: Result<Option<UserRow>, _> = sqlx::query_as(
        "SELECT id, email, username, display_name, role, is_active,
                is_global_admin, mfa_enabled, duo_enabled,
                last_login_at, created_at
           FROM secure_auth.users
          WHERE id = $1 AND deleted_at IS NULL",
    )
    .bind(id)
    .fetch_optional(&state.pool)
    .await;
    match row {
        Ok(Some(u)) => (StatusCode::OK, Json(u)).into_response(),
        Ok(None) => (
            StatusCode::NOT_FOUND,
            Json(serde_json::json!({"error":"not_found","detail":"user not found"})),
        ).into_response(),
        Err(e) => {
            tracing::error!(error = %e, "users_get");
            internal_error("database error")
        }
    }
}

#[derive(Debug, Deserialize)]
struct UsersPatchRequest {
    #[serde(default)] role:         Option<String>,
    #[serde(default)] is_active:    Option<bool>,
    #[serde(default)] display_name: Option<String>,
    #[serde(default)] first_name:   Option<String>,
    #[serde(default)] last_name:    Option<String>,
    #[serde(default)] email:        Option<String>,
    #[serde(default)] mfa_enabled:  Option<bool>,
    #[serde(default)] duo_enabled:  Option<bool>,
}

async fn users_patch(
    State(state): State<Arc<AppState>>,
    Path(id): Path<Uuid>,
    Json(req): Json<UsersPatchRequest>,
) -> impl IntoResponse {
    // Build the dynamic UPDATE. We use individual if-lets + separate
    // queries rather than assembling SQL string fragments — safer +
    // easier to read at the cost of a few extra round-trips that only
    // fire when the corresponding field is present.
    let mut tx = match state.pool.begin().await {
        Ok(t) => t,
        Err(e) => { tracing::error!(error = %e, "patch tx"); return internal_error("database error"); }
    };

    macro_rules! set_if_some {
        ($field:expr, $col:literal) => {
            if let Some(v) = $field.as_ref() {
                let _ = sqlx::query(concat!("UPDATE secure_auth.users SET ", $col, " = $1, updated_at = now() WHERE id = $2"))
                    .bind(v).bind(id).execute(&mut *tx).await;
            }
        };
    }
    set_if_some!(req.role,         "role");
    set_if_some!(req.is_active,    "is_active");
    set_if_some!(req.display_name, "display_name");
    set_if_some!(req.first_name,   "first_name");
    set_if_some!(req.last_name,    "last_name");
    set_if_some!(req.mfa_enabled,  "mfa_enabled");
    set_if_some!(req.duo_enabled,  "duo_enabled");

    if let Some(email) = req.email.as_ref() {
        let lowered = email.to_lowercase();
        let _ = sqlx::query("UPDATE secure_auth.users SET email = $1, updated_at = now() WHERE id = $2")
            .bind(&lowered).bind(id).execute(&mut *tx).await;
    }

    if tx.commit().await.is_err() {
        return internal_error("database error");
    }
    users_get(State(state), Path(id)).await.into_response()
}

// ─── User actions ─────────────────────────────────────────────────────────

async fn users_unlock(
    State(state): State<Arc<AppState>>,
    Path(id): Path<Uuid>,
) -> impl IntoResponse {
    // Clear failure records within the lockout window so the rolling-
    // window check at login returns 0. We only clear failures, not
    // successes — success history stays intact for audit.
    let email: Result<Option<(String,)>, _> = sqlx::query_as(
        "SELECT email FROM secure_auth.users WHERE id = $1 AND deleted_at IS NULL"
    ).bind(id).fetch_optional(&state.pool).await;

    let email = match email {
        Ok(Some((e,))) => e,
        _ => return (StatusCode::NOT_FOUND, Json(serde_json::json!({"error":"not_found"}))).into_response(),
    };

    let res = sqlx::query(
        "DELETE FROM secure_auth.login_attempts
          WHERE email = $1
            AND succeeded = false
            AND attempted_at > now() - make_interval(secs => 900)"
    ).bind(&email).execute(&state.pool).await;

    match res {
        Ok(r) => (StatusCode::OK, Json(serde_json::json!({
            "unlocked": true, "cleared_failure_rows": r.rows_affected(),
        }))).into_response(),
        Err(e) => {
            tracing::error!(error = %e, "users_unlock");
            internal_error("database error")
        }
    }
}

async fn users_revoke_all_sessions(
    State(state): State<Arc<AppState>>,
    Path(id): Path<Uuid>,
) -> impl IntoResponse {
    let res = sqlx::query(
        "UPDATE secure_auth.sessions SET revoked_at = now()
          WHERE user_id = $1 AND revoked_at IS NULL"
    ).bind(id).execute(&state.pool).await;
    match res {
        Ok(r) => (StatusCode::OK, Json(serde_json::json!({
            "revoked_sessions": r.rows_affected(),
        }))).into_response(),
        Err(e) => {
            tracing::error!(error = %e, "revoke_all");
            internal_error("database error")
        }
    }
}

async fn users_force_reset(
    State(state): State<Arc<AppState>>,
    Path(id): Path<Uuid>,
) -> impl IntoResponse {
    // Sentinel hash that'll never verify against any plaintext. User
    // must complete /password-reset/request + /confirm to set a real
    // one. Revoke sessions too — the point is to lock the account
    // out of existing tokens.
    let mut tx = match state.pool.begin().await {
        Ok(t) => t,
        Err(e) => { tracing::error!(error = %e, "force_reset tx"); return internal_error("database error"); }
    };

    // Archive the current hash to password_history so the audit
    // endpoint surfaces the force-reset event. Matches the pattern
    // /change-password + /password-reset/confirm use on auth-service.
    let _ = sqlx::query(
        "INSERT INTO secure_auth.password_history (user_id, password_hash)
         SELECT id, password_hash FROM secure_auth.users
          WHERE id = $1 AND deleted_at IS NULL"
    ).bind(id).execute(&mut *tx).await;

    let hash_res = sqlx::query(
        "UPDATE secure_auth.users
            SET password_hash = '(forced-reset)', updated_at = now()
          WHERE id = $1 AND deleted_at IS NULL"
    ).bind(id).execute(&mut *tx).await;

    if let Ok(r) = &hash_res {
        if r.rows_affected() == 0 {
            return (StatusCode::NOT_FOUND, Json(serde_json::json!({"error":"not_found"}))).into_response();
        }
    } else {
        return internal_error("database error");
    }

    let _ = sqlx::query(
        "UPDATE secure_auth.sessions SET revoked_at = now()
          WHERE user_id = $1 AND revoked_at IS NULL"
    ).bind(id).execute(&mut *tx).await;

    if tx.commit().await.is_err() {
        return internal_error("database error");
    }
    (StatusCode::OK, Json(serde_json::json!({
        "forced_reset": true,
        "next_step":    "user must use /api/v1/auth/password-reset/request to regain access",
    }))).into_response()
}

// ─── Audit ────────────────────────────────────────────────────────────────

#[derive(Debug, Deserialize)]
struct AuditLoginsQuery {
    #[serde(default)] email:     Option<String>,
    #[serde(default)] succeeded: Option<bool>,
    #[serde(default = "per_page_default")] per_page: i64,
}

#[derive(Debug, Serialize, sqlx::FromRow)]
struct LoginAttemptRow {
    id:              Uuid,
    email:           String,
    ip_address:      Option<sqlx::types::ipnetwork::IpNetwork>,
    user_agent:      Option<String>,
    succeeded:       bool,
    failure_reason:  Option<String>,
    attempted_at:    chrono::DateTime<chrono::Utc>,
}

async fn audit_logins(
    State(state): State<Arc<AppState>>,
    Query(q): Query<AuditLoginsQuery>,
) -> impl IntoResponse {
    let per_page = q.per_page.clamp(1, 500);
    let email_filter = q.email.as_deref().map(|s| s.to_lowercase());

    // Compose the query with optional filters. Kept boring on purpose
    // — the audit path is read-heavy + has fixed patterns.
    let rows: Result<Vec<LoginAttemptRow>, _> = match (email_filter, q.succeeded) {
        (Some(e), Some(s)) => sqlx::query_as(
            "SELECT id, email, ip_address, user_agent, succeeded,
                    failure_reason, attempted_at
               FROM secure_auth.login_attempts
              WHERE email = $1 AND succeeded = $2
              ORDER BY attempted_at DESC LIMIT $3",
        ).bind(e).bind(s).bind(per_page).fetch_all(&state.pool).await,
        (Some(e), None) => sqlx::query_as(
            "SELECT id, email, ip_address, user_agent, succeeded,
                    failure_reason, attempted_at
               FROM secure_auth.login_attempts
              WHERE email = $1
              ORDER BY attempted_at DESC LIMIT $2",
        ).bind(e).bind(per_page).fetch_all(&state.pool).await,
        (None, Some(s)) => sqlx::query_as(
            "SELECT id, email, ip_address, user_agent, succeeded,
                    failure_reason, attempted_at
               FROM secure_auth.login_attempts
              WHERE succeeded = $1
              ORDER BY attempted_at DESC LIMIT $2",
        ).bind(s).bind(per_page).fetch_all(&state.pool).await,
        (None, None) => sqlx::query_as(
            "SELECT id, email, ip_address, user_agent, succeeded,
                    failure_reason, attempted_at
               FROM secure_auth.login_attempts
              ORDER BY attempted_at DESC LIMIT $1",
        ).bind(per_page).fetch_all(&state.pool).await,
    };
    match rows {
        Ok(v)  => (StatusCode::OK, Json(v)).into_response(),
        Err(e) => {
            tracing::error!(error = %e, "audit_logins");
            internal_error("database error")
        }
    }
}

#[derive(Debug, Deserialize)]
struct AuditSessionsQuery {
    #[serde(default)] user_id: Option<Uuid>,
    #[serde(default)] include_revoked: bool,
    #[serde(default = "per_page_default")] per_page: i64,
}

#[derive(Debug, Serialize, sqlx::FromRow)]
struct SessionAuditRow {
    id:                     Uuid,
    user_id:                Uuid,
    email:                  String,
    issued_at:              chrono::DateTime<chrono::Utc>,
    expires_at:             chrono::DateTime<chrono::Utc>,
    revoked_at:             Option<chrono::DateTime<chrono::Utc>>,
    rotated_to_session_id:  Option<Uuid>,
    user_agent:             Option<String>,
}

async fn audit_sessions(
    State(state): State<Arc<AppState>>,
    Query(q): Query<AuditSessionsQuery>,
) -> impl IntoResponse {
    let per_page = q.per_page.clamp(1, 500);

    let rows: Result<Vec<SessionAuditRow>, _> = match (q.user_id, q.include_revoked) {
        (Some(uid), true) => sqlx::query_as(
            "SELECT s.id, s.user_id, u.email, s.issued_at, s.expires_at,
                    s.revoked_at, s.rotated_to_session_id, s.user_agent
               FROM secure_auth.sessions s
               JOIN secure_auth.users u ON u.id = s.user_id
              WHERE s.user_id = $1
              ORDER BY s.issued_at DESC LIMIT $2",
        ).bind(uid).bind(per_page).fetch_all(&state.pool).await,
        (Some(uid), false) => sqlx::query_as(
            "SELECT s.id, s.user_id, u.email, s.issued_at, s.expires_at,
                    s.revoked_at, s.rotated_to_session_id, s.user_agent
               FROM secure_auth.sessions s
               JOIN secure_auth.users u ON u.id = s.user_id
              WHERE s.user_id = $1 AND s.revoked_at IS NULL
              ORDER BY s.issued_at DESC LIMIT $2",
        ).bind(uid).bind(per_page).fetch_all(&state.pool).await,
        (None, true) => sqlx::query_as(
            "SELECT s.id, s.user_id, u.email, s.issued_at, s.expires_at,
                    s.revoked_at, s.rotated_to_session_id, s.user_agent
               FROM secure_auth.sessions s
               JOIN secure_auth.users u ON u.id = s.user_id
              ORDER BY s.issued_at DESC LIMIT $1",
        ).bind(per_page).fetch_all(&state.pool).await,
        (None, false) => sqlx::query_as(
            "SELECT s.id, s.user_id, u.email, s.issued_at, s.expires_at,
                    s.revoked_at, s.rotated_to_session_id, s.user_agent
               FROM secure_auth.sessions s
               JOIN secure_auth.users u ON u.id = s.user_id
              WHERE s.revoked_at IS NULL
              ORDER BY s.issued_at DESC LIMIT $1",
        ).bind(per_page).fetch_all(&state.pool).await,
    };
    match rows {
        Ok(v)  => (StatusCode::OK, Json(v)).into_response(),
        Err(e) => {
            tracing::error!(error = %e, "audit_sessions");
            internal_error("database error")
        }
    }
}

// ─── Users: create + soft delete (Phase 4.B) ──────────────────────────────

#[derive(Debug, Deserialize)]
struct UserCreateRequest {
    email:                        String,
    #[serde(default)] password:        Option<String>,
    #[serde(default)] username:        Option<String>,
    #[serde(default)] display_name:    Option<String>,
    #[serde(default)] first_name:      Option<String>,
    #[serde(default)] last_name:       Option<String>,
    #[serde(default)] role:            Option<String>,
    #[serde(default)] is_global_admin: Option<bool>,
    #[serde(default)] is_active:       Option<bool>,
}

/// Create a new user. Password is optional — omit to create an SSO-
/// only account (sentinel `(sso-only)` hash; /login fails for them
/// until a real hash is set via /password-reset/confirm).
/// When supplied, min 12 chars matches /change-password floor.
async fn users_create(
    State(state): State<Arc<AppState>>,
    Json(req): Json<UserCreateRequest>,
) -> impl IntoResponse {
    let email = req.email.trim().to_lowercase();
    if email.is_empty() || !email.contains('@') {
        return bad_request("email required + must contain '@'");
    }

    let password_hash = match req.password.as_deref() {
        None | Some("") => "(sso-only)".to_string(),
        Some(p) if p.len() < 12 => return bad_request("password must be at least 12 characters"),
        Some(p) => match identity_core::passwords::hash_password(p) {
            Ok(h) => h,
            Err(e) => {
                tracing::error!(error = %e, "users_create: hash failed");
                return internal_error("hash failed");
            }
        },
    };

    let row: Result<UserRow, _> = sqlx::query_as(
        "INSERT INTO secure_auth.users
             (email, password_hash, username, display_name, first_name,
              last_name, role, is_global_admin, is_active)
         VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9)
         RETURNING id, email, username, display_name, role, is_active,
                   is_global_admin, mfa_enabled, duo_enabled,
                   last_login_at, created_at",
    )
    .bind(&email)
    .bind(&password_hash)
    .bind(req.username.as_deref().map(|s| s.trim()))
    .bind(req.display_name.as_deref())
    .bind(req.first_name.as_deref())
    .bind(req.last_name.as_deref())
    .bind(req.role.as_deref().unwrap_or("user"))
    .bind(req.is_global_admin.unwrap_or(false))
    .bind(req.is_active.unwrap_or(true))
    .fetch_one(&state.pool)
    .await;

    match row {
        Ok(u) => (StatusCode::CREATED, Json(u)).into_response(),
        Err(sqlx::Error::Database(db)) if db.constraint() == Some("users_email_active_idx")
                                       || db.is_unique_violation() => {
            (StatusCode::CONFLICT, Json(serde_json::json!({
                "error":  "conflict",
                "detail": "email or username already in use",
            }))).into_response()
        }
        Err(e) => {
            tracing::error!(error = %e, "users_create");
            internal_error("database error")
        }
    }
}

async fn users_delete(
    State(state): State<Arc<AppState>>,
    Path(id): Path<Uuid>,
) -> impl IntoResponse {
    // Soft delete + revoke all sessions atomically. The bridge
    // trigger (migration 116) won't fire for UPDATEs that only touch
    // deleted_at since app_users doesn't have that column — a
    // follow-up phase adds a corresponding app_users.deleted_at or
    // flips is_active instead. For Phase 4.B the user's secure_auth
    // row is the identity source of truth, which is what auth-service
    // queries; desktop queries via app_users would still see them
    // unless admin also flips is_active.
    let mut tx = match state.pool.begin().await {
        Ok(t) => t,
        Err(e) => { tracing::error!(error = %e, "delete tx"); return internal_error("database error"); }
    };

    let res = sqlx::query(
        "UPDATE secure_auth.users
            SET deleted_at = now(), is_active = false, updated_at = now()
          WHERE id = $1 AND deleted_at IS NULL"
    ).bind(id).execute(&mut *tx).await;

    match res {
        Ok(r) if r.rows_affected() == 0 =>
            return (StatusCode::NOT_FOUND, Json(serde_json::json!({"error":"not_found"}))).into_response(),
        Err(e) => { tracing::error!(error = %e, "users_delete"); return internal_error("database error"); }
        _ => {}
    }

    let _ = sqlx::query(
        "UPDATE secure_auth.sessions SET revoked_at = now()
          WHERE user_id = $1 AND revoked_at IS NULL"
    ).bind(id).execute(&mut *tx).await;

    if tx.commit().await.is_err() {
        return internal_error("database error");
    }
    StatusCode::NO_CONTENT.into_response()
}

// ─── Tenants ──────────────────────────────────────────────────────────────

#[derive(Debug, Serialize, sqlx::FromRow)]
struct TenantRow {
    id:           Uuid,
    slug:         String,
    display_name: String,
    domain:       Option<String>,
    tier:         String,
    is_active:    bool,
    created_at:   chrono::DateTime<chrono::Utc>,
}

async fn tenants_list(
    State(state): State<Arc<AppState>>,
) -> impl IntoResponse {
    let rows: Result<Vec<TenantRow>, _> = sqlx::query_as(
        "SELECT id, slug, display_name, domain, tier, is_active, created_at
           FROM central_platform.tenants
          ORDER BY slug"
    ).fetch_all(&state.pool).await;
    match rows {
        Ok(v)  => (StatusCode::OK, Json(v)).into_response(),
        Err(e) => { tracing::error!(error = %e, "tenants_list"); internal_error("database error") }
    }
}

// ─── SSO provider CRUD (Phase 4.B) ────────────────────────────────────────

#[derive(Debug, Serialize, sqlx::FromRow)]
struct SsoProviderAdminRow {
    provider_code: String,
    kind:          String,
    display_name:  String,
    enabled:       bool,
    tenant_id:     Option<Uuid>,
    config_json:   serde_json::Value,
    created_at:    chrono::DateTime<chrono::Utc>,
    updated_at:    chrono::DateTime<chrono::Utc>,
    deleted_at:    Option<chrono::DateTime<chrono::Utc>>,
}

async fn sso_providers_list_admin(
    State(state): State<Arc<AppState>>,
) -> impl IntoResponse {
    // Admin view: include disabled + soft-deleted so the UI can show
    // the full state + offer an "un-disable" toggle. Public
    // /sso/providers on auth-service filters these out.
    let rows: Result<Vec<SsoProviderAdminRow>, _> = sqlx::query_as(
        "SELECT provider_code, kind, display_name, enabled, tenant_id,
                config_json, created_at, updated_at, deleted_at
           FROM secure_auth.identity_providers
          ORDER BY provider_code"
    ).fetch_all(&state.pool).await;
    match rows {
        Ok(v)  => (StatusCode::OK, Json(v)).into_response(),
        Err(e) => { tracing::error!(error = %e, "sso_providers_list_admin"); internal_error("database error") }
    }
}

#[derive(Debug, Deserialize)]
struct SsoProviderCreate {
    provider_code: String,
    kind:          String,
    display_name:  String,
    #[serde(default)] enabled:     Option<bool>,
    #[serde(default)] tenant_id:   Option<Uuid>,
    #[serde(default)] config_json: Option<serde_json::Value>,
}

async fn sso_providers_create(
    State(state): State<Arc<AppState>>,
    Json(req): Json<SsoProviderCreate>,
) -> impl IntoResponse {
    if req.provider_code.trim().is_empty() || req.kind.trim().is_empty() || req.display_name.trim().is_empty() {
        return bad_request("provider_code + kind + display_name required");
    }
    let row: Result<SsoProviderAdminRow, _> = sqlx::query_as(
        "INSERT INTO secure_auth.identity_providers
             (provider_code, kind, display_name, enabled, tenant_id, config_json)
         VALUES ($1, $2, $3, $4, $5, $6)
         RETURNING provider_code, kind, display_name, enabled, tenant_id,
                   config_json, created_at, updated_at, deleted_at",
    )
    .bind(&req.provider_code)
    .bind(&req.kind)
    .bind(&req.display_name)
    .bind(req.enabled.unwrap_or(true))
    .bind(req.tenant_id)
    .bind(req.config_json.unwrap_or(serde_json::json!({})))
    .fetch_one(&state.pool)
    .await;
    match row {
        Ok(p) => (StatusCode::CREATED, Json(p)).into_response(),
        Err(sqlx::Error::Database(db)) if db.is_check_violation() => {
            (StatusCode::BAD_REQUEST, Json(serde_json::json!({
                "error":  "bad_request",
                "detail": "kind not allowed or provider_code format invalid",
            }))).into_response()
        }
        Err(sqlx::Error::Database(db)) if db.is_unique_violation() => {
            (StatusCode::CONFLICT, Json(serde_json::json!({
                "error":  "conflict",
                "detail": "provider_code already exists",
            }))).into_response()
        }
        Err(e) => {
            tracing::error!(error = %e, "sso_providers_create");
            internal_error("database error")
        }
    }
}

#[derive(Debug, Deserialize)]
struct SsoProviderPatch {
    #[serde(default)] display_name: Option<String>,
    #[serde(default)] enabled:      Option<bool>,
    #[serde(default)] config_json:  Option<serde_json::Value>,
}

async fn sso_providers_update(
    State(state): State<Arc<AppState>>,
    Path(code): Path<String>,
    Json(req): Json<SsoProviderPatch>,
) -> impl IntoResponse {
    let mut tx = match state.pool.begin().await {
        Ok(t) => t,
        Err(e) => { tracing::error!(error = %e, "sso patch tx"); return internal_error("database error"); }
    };

    if let Some(n) = req.display_name.as_deref() {
        let _ = sqlx::query("UPDATE secure_auth.identity_providers SET display_name = $1, updated_at = now() WHERE provider_code = $2")
            .bind(n).bind(&code).execute(&mut *tx).await;
    }
    if let Some(e) = req.enabled {
        let _ = sqlx::query("UPDATE secure_auth.identity_providers SET enabled = $1, updated_at = now() WHERE provider_code = $2")
            .bind(e).bind(&code).execute(&mut *tx).await;
    }
    if let Some(ref c) = req.config_json {
        let _ = sqlx::query("UPDATE secure_auth.identity_providers SET config_json = $1, updated_at = now() WHERE provider_code = $2")
            .bind(c).bind(&code).execute(&mut *tx).await;
    }

    if tx.commit().await.is_err() {
        return internal_error("database error");
    }

    // Return the current row.
    let row: Result<Option<SsoProviderAdminRow>, _> = sqlx::query_as(
        "SELECT provider_code, kind, display_name, enabled, tenant_id,
                config_json, created_at, updated_at, deleted_at
           FROM secure_auth.identity_providers
          WHERE provider_code = $1"
    ).bind(&code).fetch_optional(&state.pool).await;
    match row {
        Ok(Some(p)) => (StatusCode::OK, Json(p)).into_response(),
        Ok(None)    => (StatusCode::NOT_FOUND, Json(serde_json::json!({"error":"not_found"}))).into_response(),
        Err(e)      => { tracing::error!(error = %e, "sso patch refetch"); internal_error("database error") }
    }
}

async fn sso_providers_delete(
    State(state): State<Arc<AppState>>,
    Path(code): Path<String>,
) -> impl IntoResponse {
    // Soft — keep the row for audit (SSO callbacks reference it by
    // provider_code), flip enabled off + stamp deleted_at.
    let res = sqlx::query(
        "UPDATE secure_auth.identity_providers
            SET deleted_at = now(), enabled = false, updated_at = now()
          WHERE provider_code = $1 AND deleted_at IS NULL"
    ).bind(&code).execute(&state.pool).await;

    match res {
        Ok(r) if r.rows_affected() == 0 =>
            (StatusCode::NOT_FOUND, Json(serde_json::json!({"error":"not_found"}))).into_response(),
        Ok(_)  => StatusCode::NO_CONTENT.into_response(),
        Err(e) => { tracing::error!(error = %e, "sso_providers_delete"); internal_error("database error") }
    }
}

// ─── Additional audit endpoints (Phase 4.B) ───────────────────────────────

#[derive(Debug, Serialize, sqlx::FromRow)]
struct PasswordChangeRow {
    id:         Uuid,
    user_id:    Uuid,
    email:      String,
    retired_at: chrono::DateTime<chrono::Utc>,
}

/// `secure_auth.password_history` is our audit trail for password
/// changes — each row captures the OLD hash that was replaced + when.
/// This endpoint exposes timestamps + the user email; the actual
/// hash values stay in the DB, never in the response (leaking old
/// hashes is never acceptable).
async fn audit_password_changes(
    State(state): State<Arc<AppState>>,
    Query(q): Query<AuditGenericQuery>,
) -> impl IntoResponse {
    let per_page = q.per_page.clamp(1, 500);
    let rows: Result<Vec<PasswordChangeRow>, _> = match q.user_id {
        Some(uid) => sqlx::query_as(
            "SELECT ph.id, ph.user_id, u.email, ph.retired_at
               FROM secure_auth.password_history ph
               JOIN secure_auth.users u ON u.id = ph.user_id
              WHERE ph.user_id = $1
              ORDER BY ph.retired_at DESC
              LIMIT $2"
        ).bind(uid).bind(per_page).fetch_all(&state.pool).await,
        None => sqlx::query_as(
            "SELECT ph.id, ph.user_id, u.email, ph.retired_at
               FROM secure_auth.password_history ph
               JOIN secure_auth.users u ON u.id = ph.user_id
              ORDER BY ph.retired_at DESC
              LIMIT $1"
        ).bind(per_page).fetch_all(&state.pool).await,
    };
    match rows {
        Ok(v)  => (StatusCode::OK, Json(v)).into_response(),
        Err(e) => { tracing::error!(error = %e, "audit_password_changes"); internal_error("database error") }
    }
}

#[derive(Debug, Deserialize)]
struct AuditGenericQuery {
    #[serde(default)] user_id: Option<Uuid>,
    #[serde(default = "per_page_default")] per_page: i64,
}

#[derive(Debug, Serialize, sqlx::FromRow)]
struct SsoCallbackRow {
    id:              Uuid,
    email:           String,
    user_agent:      Option<String>,
    succeeded:       bool,
    failure_reason:  Option<String>,
    attempted_at:    chrono::DateTime<chrono::Utc>,
}

/// SSO callback events are logged to `secure_auth.login_attempts` with
/// failure_reason starting `sso:` (success) or `*_sso_*` (failure —
/// e.g. windows_sso_unknown_user). This endpoint returns rows where
/// either condition holds so the audit UI can filter by SSO origin.
async fn audit_sso_callbacks(
    State(state): State<Arc<AppState>>,
    Query(q): Query<AuditGenericQuery>,
) -> impl IntoResponse {
    let per_page = q.per_page.clamp(1, 500);
    let rows: Result<Vec<SsoCallbackRow>, _> = sqlx::query_as(
        "SELECT id, email, user_agent, succeeded, failure_reason, attempted_at
           FROM secure_auth.login_attempts
          WHERE failure_reason LIKE 'sso:%'
             OR failure_reason LIKE '%\\_sso\\_%' ESCAPE '\\'
          ORDER BY attempted_at DESC
          LIMIT $1"
    ).bind(per_page).fetch_all(&state.pool).await;
    let _ = q.user_id; // reserved — user-scoped filter is a refinement
    match rows {
        Ok(v)  => (StatusCode::OK, Json(v)).into_response(),
        Err(e) => { tracing::error!(error = %e, "audit_sso_callbacks"); internal_error("database error") }
    }
}

// ─── Small helpers ────────────────────────────────────────────────────────

fn bad_request(detail: &'static str) -> axum::response::Response {
    (StatusCode::BAD_REQUEST, Json(serde_json::json!({
        "error":  "bad_request",
        "detail": detail,
    }))).into_response()
}
