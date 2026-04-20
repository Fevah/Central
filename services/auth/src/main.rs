//! Central auth-service — Phase A: minimum-viable login.
//!
//! See `docs/AUTH_SERVICE_BUILDOUT.md` for the phased plan. This binary
//! ships the Phase A surface only:
//!
//! - `GET  /health`                     — 200 OK when DB reachable.
//! - `POST /api/v1/auth/login`          — email + password -> JWT.
//! - `POST /api/v1/auth/refresh`        — Phase B stub (501 Not Implemented).
//! - `POST /api/v1/auth/logout`         — Phase B stub (204 No Content).
//! - `POST /api/v1/auth/mfa/verify`     — Phase C stub (501).
//!
//! Config source: `config/auth-service.toml` resolved relative to the
//! repo root (walk up from the binary dir until a file exists), or the
//! path set by `CENTRAL_AUTH_CONFIG`. JWT secret can be overridden by
//! `AUTH_SERVICE_JWT_SECRET` env var (matches the existing pattern the
//! Central.Api uses via `CENTRAL_JWT_SECRET`).

use std::{net::SocketAddr, path::PathBuf, sync::Arc};

use anyhow::Context;
use argon2::{password_hash::PasswordHash, Argon2, PasswordVerifier};
use axum::{
    extract::State,
    http::{HeaderMap, StatusCode},
    response::IntoResponse,
    routing::{get, post},
    Json, Router,
};
use jsonwebtoken::{encode, EncodingKey, Header};
use serde::{Deserialize, Serialize};
use sqlx::postgres::{PgPool, PgPoolOptions};
use uuid::Uuid;

/// Tokio entry point. Log setup, config load, DB pool, router, listen.
#[tokio::main]
async fn main() -> anyhow::Result<()> {
    tracing_subscriber::fmt()
        .with_env_filter(tracing_subscriber::EnvFilter::from_default_env())
        .json()
        .init();

    let cfg = load_config()?;
    // AUTH_SERVICE_PORT env override so local dev can run on an
    // alternate port when the default 8081 is already bound by
    // something else (e.g. the `central-postgres` podman pod which
    // happens to expose 8081 too). K8s / prod use the config value.
    let port: u16 = std::env::var("AUTH_SERVICE_PORT")
        .ok()
        .and_then(|v| v.parse().ok())
        .unwrap_or(cfg.server.port);
    let bind: SocketAddr = format!("{}:{}", cfg.server.host, port)
        .parse()
        .context("invalid server.host/port in auth-service.toml")?;

    let pool = PgPoolOptions::new()
        .max_connections(cfg.database.pool_size as u32)
        .acquire_timeout(std::time::Duration::from_secs(cfg.database.pool_timeout_seconds))
        .connect(&cfg.db_url())
        .await
        .context("connecting to auth-service database")?;

    let state = Arc::new(AppState {
        pool,
        jwt_secret: cfg.effective_jwt_secret(),
        jwt_issuer: cfg.jwt.issuer.clone(),
        jwt_audience: cfg.jwt.audience.clone(),
        access_ttl_seconds: cfg.jwt.access_token_ttl_seconds,
    });

    let app = Router::new()
        .route("/health", get(health))
        .route("/api/v1/auth/login", post(login))
        .route("/api/v1/auth/refresh", post(refresh_not_implemented))
        .route("/api/v1/auth/logout", post(logout_stub))
        .route("/api/v1/auth/mfa/verify", post(mfa_not_implemented))
        .layer(tower_http::cors::CorsLayer::permissive())
        .layer(tower_http::trace::TraceLayer::new_for_http())
        .with_state(state);

    tracing::info!(%bind, "auth-service listening");
    let listener = tokio::net::TcpListener::bind(bind).await?;
    axum::serve(listener, app.into_make_service()).await?;
    Ok(())
}

// ─── Config ────────────────────────────────────────────────────────────────

#[derive(Debug, Deserialize)]
struct Config {
    server:   ServerCfg,
    database: DbCfg,
    jwt:      JwtCfg,
}

#[derive(Debug, Deserialize)]
struct ServerCfg { host: String, port: u16 }

#[derive(Debug, Deserialize)]
struct DbCfg {
    host: String, port: u16, username: String, password: String,
    database: String, ssl_mode: String,
    pool_size: i32, pool_timeout_seconds: u64,
}

#[derive(Debug, Deserialize)]
#[allow(dead_code)]   // Phase B wires refresh_token_ttl_seconds
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
        format!(
            "postgres://{user}:{pass}@{host}:{port}/{db}?sslmode={ssl}",
            user = self.database.username, pass = self.database.password,
            host = self.database.host, port = self.database.port,
            db = self.database.database, ssl = self.database.ssl_mode,
        )
    }

    /// Effective JWT secret: env `AUTH_SERVICE_JWT_SECRET` wins over config
    /// file (matches the pattern Central.Api uses with `CENTRAL_JWT_SECRET`).
    fn effective_jwt_secret(&self) -> String {
        if let Ok(v) = std::env::var("AUTH_SERVICE_JWT_SECRET") {
            if !v.is_empty() { return v; }
        }
        self.jwt.secret.clone().unwrap_or_else(|| {
            tracing::warn!("no JWT secret in config or env — tokens are insecure");
            "dev-only-insecure-secret".into()
        })
    }
}

/// Resolve `config/auth-service.toml` by env override or by walking up from
/// the binary's working directory.
fn load_config() -> anyhow::Result<Config> {
    let path = resolve_config_path()?;
    let text = std::fs::read_to_string(&path)
        .with_context(|| format!("reading config at {}", path.display()))?;
    let cfg: Config = toml::from_str(&text)
        .with_context(|| format!("parsing config at {}", path.display()))?;
    tracing::info!(path = %path.display(), "auth-service config loaded");
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
    anyhow::bail!("config/auth-service.toml not found above cwd; set CENTRAL_AUTH_CONFIG");
}

// ─── Shared state ──────────────────────────────────────────────────────────

struct AppState {
    pool:               PgPool,
    jwt_secret:         String,
    jwt_issuer:         String,
    jwt_audience:       String,
    access_ttl_seconds: i64,
}

// ─── Handlers ──────────────────────────────────────────────────────────────

async fn health(State(state): State<Arc<AppState>>) -> impl IntoResponse {
    // DB round-trip gate. If the DB is unreachable, return 503 so the
    // existing ServiceHealthTests probe fails deterministically rather
    // than seeing a stale "it was up earlier" 200.
    match sqlx::query_scalar::<_, i32>("SELECT 1").fetch_one(&state.pool).await {
        Ok(_)  => (StatusCode::OK, Json(serde_json::json!({"status":"ok"}))).into_response(),
        Err(e) => {
            tracing::error!(error = %e, "health DB probe failed");
            (StatusCode::SERVICE_UNAVAILABLE, Json(serde_json::json!({
                "status": "degraded",
                "detail": "database unreachable"
            }))).into_response()
        }
    }
}

#[derive(Debug, Deserialize)]
struct LoginRequest { email: String, password: String }

#[derive(Debug, Serialize)]
struct LoginResponse {
    access_token:  String,
    refresh_token: String,
    session_id:    String,
    expires_in:    i64,
    token_type:    String,
    mfa_required:  bool,
    mfa_methods:   Vec<String>,
    user:          AuthUser,
}

#[derive(Debug, Serialize)]
struct AuthUser {
    id:           String,
    email:        String,
    display_name: String,
    first_name:   Option<String>,
    last_name:    Option<String>,
    roles:        Vec<String>,
    permissions:  Vec<String>,
    mfa_enabled:  bool,
}

#[derive(Debug, Serialize)]
struct Claims {
    sub: String,   // user id
    exp: i64,      // epoch seconds
    iat: i64,
    iss: String,
    aud: String,
    email: String,
}

async fn login(
    State(state): State<Arc<AppState>>,
    headers: HeaderMap,
    Json(req): Json<LoginRequest>,
) -> impl IntoResponse {
    let _tenant = headers
        .get("x-tenant-id")
        .and_then(|v| v.to_str().ok())
        .unwrap_or("default");   // tenant-scoped login lands in Phase E

    // Fetch the row. Single query — SELECT returns None on unknown email
    // (treat identically to wrong password to avoid enumeration).
    let row = sqlx::query_as::<_, UserRow>(
        "SELECT id, email, password_hash, display_name, first_name, last_name,
                is_global_admin, mfa_enabled
           FROM secure_auth.users
          WHERE email = $1 AND deleted_at IS NULL",
    )
    .bind(req.email.to_lowercase())
    .fetch_optional(&state.pool)
    .await;

    let row = match row {
        Ok(Some(r)) => r,
        Ok(None) => return unauthorized(),
        Err(e) => {
            tracing::error!(error = %e, "login db query failed");
            return (
                StatusCode::INTERNAL_SERVER_ERROR,
                Json(serde_json::json!({"error":"internal","detail":"database error"})),
            ).into_response();
        }
    };

    // Argon2 verify. Wrong password, unknown email, or garbled hash all
    // collapse to 401 — callers never learn which.
    let parsed = match PasswordHash::new(&row.password_hash) {
        Ok(p) => p,
        Err(e) => {
            tracing::error!(user_id = %row.id, error = %e, "stored password_hash is not valid Argon2");
            return unauthorized();
        }
    };
    if Argon2::default().verify_password(req.password.as_bytes(), &parsed).is_err() {
        return unauthorized();
    }

    // Roles + permissions are minimal in Phase A — is_global_admin maps to
    // role "global_admin". Full per-tenant role/permission resolution lands
    // in Phase E (IDP claim mapping).
    let roles: Vec<String> = if row.is_global_admin {
        vec!["global_admin".into()]
    } else {
        vec!["user".into()]
    };

    let now = chrono::Utc::now().timestamp();
    let claims = Claims {
        sub: row.id.to_string(),
        exp: now + state.access_ttl_seconds,
        iat: now,
        iss: state.jwt_issuer.clone(),
        aud: state.jwt_audience.clone(),
        email: row.email.clone(),
    };
    let token = match encode(&Header::default(), &claims, &EncodingKey::from_secret(state.jwt_secret.as_bytes())) {
        Ok(t) => t,
        Err(e) => {
            tracing::error!(error = %e, "jwt encode failed");
            return (
                StatusCode::INTERNAL_SERVER_ERROR,
                Json(serde_json::json!({"error":"internal","detail":"token encode failed"})),
            ).into_response();
        }
    };

    // Last-login stamp. Failure is non-fatal — telemetry, not auth.
    let _ = sqlx::query("UPDATE secure_auth.users SET last_login_at = now() WHERE id = $1")
        .bind(row.id).execute(&state.pool).await;

    let resp = LoginResponse {
        access_token:  token.clone(),
        // Phase A placeholder — the Angular client stores a refresh_token
        // field; Phase B replaces with a real rotating refresh token.
        refresh_token: token,
        session_id:    Uuid::new_v4().to_string(),
        expires_in:    state.access_ttl_seconds,
        token_type:    "Bearer".into(),
        mfa_required:  false,   // Phase C flips this when user.mfa_enabled
        mfa_methods:   Vec::new(),
        user: AuthUser {
            id:           row.id.to_string(),
            email:        row.email,
            display_name: row.display_name.unwrap_or_default(),
            first_name:   row.first_name,
            last_name:    row.last_name,
            roles,
            permissions:  Vec::new(),   // Phase E
            mfa_enabled:  row.mfa_enabled,
        },
    };
    (StatusCode::OK, Json(resp)).into_response()
}

fn unauthorized() -> axum::response::Response {
    (StatusCode::UNAUTHORIZED, Json(serde_json::json!({
        "error":  "unauthorized",
        "detail": "invalid email or password"
    }))).into_response()
}

async fn refresh_not_implemented() -> impl IntoResponse {
    (StatusCode::NOT_IMPLEMENTED, Json(serde_json::json!({
        "error":"not_implemented",
        "detail":"refresh lands in Phase B of the auth-service buildout"
    })))
}

async fn logout_stub() -> impl IntoResponse {
    // No session tracking yet (Phase B), so there's nothing to revoke.
    // Responding 204 lets the Angular client's logout button behave as
    // expected + avoids surfacing a confusing error on sign-out.
    StatusCode::NO_CONTENT
}

async fn mfa_not_implemented() -> impl IntoResponse {
    (StatusCode::NOT_IMPLEMENTED, Json(serde_json::json!({
        "error":"not_implemented",
        "detail":"mfa lands in Phase C of the auth-service buildout"
    })))
}

// ─── DB row type ───────────────────────────────────────────────────────────

#[derive(Debug, sqlx::FromRow)]
struct UserRow {
    id:              Uuid,
    email:           String,
    password_hash:   String,
    display_name:    Option<String>,
    first_name:      Option<String>,
    last_name:       Option<String>,
    is_global_admin: bool,
    mfa_enabled:     bool,
}
