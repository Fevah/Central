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
use base64::{engine::general_purpose::URL_SAFE_NO_PAD, Engine};
use jsonwebtoken::{decode, encode, DecodingKey, EncodingKey, Header, Validation};
use serde::{Deserialize, Serialize};
use sha2::{Digest, Sha256};
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
        refresh_ttl_seconds: if cfg.jwt.refresh_token_ttl_seconds > 0 {
            cfg.jwt.refresh_token_ttl_seconds
        } else {
            // Phase-B default when the config file predates refresh-token
            // rotation: 7 days, matching the jwt[refresh_token_ttl_seconds]
            // example in config/auth-service.toml.
            7 * 24 * 3600
        },
    });

    let app = Router::new()
        .route("/health", get(health))
        .route("/api/v1/auth/login", post(login))
        .route("/api/v1/auth/refresh", post(refresh))
        .route("/api/v1/auth/logout", post(logout))
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
    pool:                 PgPool,
    jwt_secret:           String,
    jwt_issuer:           String,
    jwt_audience:         String,
    access_ttl_seconds:   i64,
    refresh_ttl_seconds:  i64,
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

#[derive(Debug, Serialize, Deserialize)]
struct Claims {
    sub:   String,   // user id
    exp:   i64,      // epoch seconds
    iat:   i64,
    iss:   String,
    aud:   String,
    email: String,
    /// Session id — matches the row in `secure_auth.sessions`. Logout
    /// revokes by this id. New in Phase B; older tokens without `sid`
    /// are rejected on logout + refresh, so any session issued by
    /// Phase A automatically invalidates on next refresh attempt.
    #[serde(default)]
    sid: String,
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

    let user_agent = headers
        .get("user-agent")
        .and_then(|v| v.to_str().ok())
        .map(|s| s.to_owned());

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
            return internal_error("database error");
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

    // Issue tokens + persist session. Any failure here fails the whole
    // login so the client never gets an access_token with no backing
    // session row.
    match issue_tokens(&state, row.id, &row.email, user_agent.as_deref()).await {
        Ok((access_token, refresh_token, session_id)) => {
            // Last-login stamp. Failure is non-fatal — telemetry, not auth.
            let _ = sqlx::query("UPDATE secure_auth.users SET last_login_at = now() WHERE id = $1")
                .bind(row.id).execute(&state.pool).await;

            let roles: Vec<String> = if row.is_global_admin {
                vec!["global_admin".into()]
            } else {
                vec!["user".into()]
            };
            let resp = LoginResponse {
                access_token,
                refresh_token,
                session_id: session_id.to_string(),
                expires_in: state.access_ttl_seconds,
                token_type: "Bearer".into(),
                mfa_required: false,   // Phase C flips on user.mfa_enabled
                mfa_methods:  Vec::new(),
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
        Err(e) => {
            tracing::error!(error = %e, "issue_tokens failed during login");
            internal_error("could not issue tokens")
        }
    }
}

#[derive(Debug, Deserialize)]
struct RefreshRequest { refresh_token: String }

/// Phase B — rotating refresh. Validates the incoming token, revokes
/// the old session, issues + stores a new one in the same transaction
/// so a crash mid-rotation either fully succeeds or leaves the
/// original session intact.
async fn refresh(
    State(state): State<Arc<AppState>>,
    headers: HeaderMap,
    Json(req): Json<RefreshRequest>,
) -> impl IntoResponse {
    if req.refresh_token.is_empty() {
        return (
            StatusCode::BAD_REQUEST,
            Json(serde_json::json!({"error":"bad_request","detail":"refresh_token required"})),
        ).into_response();
    }

    let user_agent = headers
        .get("user-agent")
        .and_then(|v| v.to_str().ok())
        .map(|s| s.to_owned());

    let token_hash = sha256_hex(&req.refresh_token);

    // Transaction ensures the revoke + issue are atomic. If anything
    // fails after we've revoked the old session, we roll back + leave
    // the client with a working old token rather than locked out.
    let mut tx = match state.pool.begin().await {
        Ok(t) => t,
        Err(e) => {
            tracing::error!(error = %e, "refresh: begin tx failed");
            return internal_error("database error");
        }
    };

    // Look up the active session matching this hash. Partial index
    // (sessions_active_hash_idx) makes this cheap.
    let session = sqlx::query_as::<_, SessionRow>(
        "SELECT id, user_id, expires_at FROM secure_auth.sessions
          WHERE refresh_token_hash = $1
            AND revoked_at IS NULL
            AND expires_at > now()",
    )
    .bind(&token_hash)
    .fetch_optional(&mut *tx)
    .await;

    let session = match session {
        Ok(Some(s)) => s,
        Ok(None) => return unauthorized(),   // unknown / expired / revoked
        Err(e) => {
            tracing::error!(error = %e, "refresh: lookup failed");
            return internal_error("database error");
        }
    };

    // Fetch user for response payload. If the user was deleted after
    // login, reject — clients shouldn't keep rotating against a row
    // that no longer exists.
    let user = sqlx::query_as::<_, UserRow>(
        "SELECT id, email, password_hash, display_name, first_name, last_name,
                is_global_admin, mfa_enabled
           FROM secure_auth.users
          WHERE id = $1 AND deleted_at IS NULL",
    )
    .bind(session.user_id)
    .fetch_optional(&mut *tx)
    .await;

    let user = match user {
        Ok(Some(u)) => u,
        Ok(None)    => return unauthorized(),
        Err(e) => {
            tracing::error!(error = %e, "refresh: user lookup failed");
            return internal_error("database error");
        }
    };

    // Issue new tokens + new session row.
    let issued = match insert_session_row(&mut tx, user.id, user_agent.as_deref(),
                                          state.refresh_ttl_seconds).await {
        Ok(v) => v,
        Err(e) => {
            tracing::error!(error = %e, "refresh: insert new session failed");
            return internal_error("session store failed");
        }
    };

    // Revoke the old session, link to the new one for audit.
    if let Err(e) = sqlx::query(
        "UPDATE secure_auth.sessions
            SET revoked_at = now(),
                rotated_to_session_id = $2
          WHERE id = $1 AND revoked_at IS NULL",
    )
    .bind(session.id)
    .bind(issued.session_id)
    .execute(&mut *tx)
    .await
    {
        tracing::error!(error = %e, "refresh: revoke old session failed");
        return internal_error("session store failed");
    }

    // Sign the new access token. Failure here rolls back the whole tx
    // — the client keeps using the old refresh token until the next
    // try.
    let access_token = match sign_access_token(&state, user.id, &user.email, issued.session_id) {
        Ok(t) => t,
        Err(e) => {
            tracing::error!(error = %e, "refresh: jwt sign failed");
            return internal_error("token encode failed");
        }
    };

    if let Err(e) = tx.commit().await {
        tracing::error!(error = %e, "refresh: commit failed");
        return internal_error("database error");
    }

    let roles: Vec<String> = if user.is_global_admin {
        vec!["global_admin".into()]
    } else {
        vec!["user".into()]
    };
    let resp = LoginResponse {
        access_token,
        refresh_token: issued.raw_refresh_token,
        session_id:    issued.session_id.to_string(),
        expires_in:    state.access_ttl_seconds,
        token_type:    "Bearer".into(),
        mfa_required:  false,
        mfa_methods:   Vec::new(),
        user: AuthUser {
            id:           user.id.to_string(),
            email:        user.email,
            display_name: user.display_name.unwrap_or_default(),
            first_name:   user.first_name,
            last_name:    user.last_name,
            roles,
            permissions:  Vec::new(),
            mfa_enabled:  user.mfa_enabled,
        },
    };
    (StatusCode::OK, Json(resp)).into_response()
}

/// Phase B logout — extracts the session id from the access token
/// (Authorization: Bearer ...) and revokes that specific session.
/// Other sessions for the same user stay live. Always returns 204 so
/// the client can safely call it even when its token is already
/// expired or malformed.
async fn logout(
    State(state): State<Arc<AppState>>,
    headers: HeaderMap,
) -> impl IntoResponse {
    let bearer = headers
        .get("authorization")
        .and_then(|v| v.to_str().ok())
        .and_then(|s| s.strip_prefix("Bearer "))
        .map(|s| s.to_owned());

    let Some(token) = bearer else { return StatusCode::NO_CONTENT; };

    // Decode WITHOUT requiring current validity — a user logging out of
    // an already-expired session still deserves the server-side
    // revoke. Disabling exp + aud enforcement is deliberate; forged
    // tokens still fail the signature check.
    let mut v = Validation::default();
    v.validate_exp = false;
    v.validate_aud = false;
    v.leeway = 30;

    let claims = match decode::<Claims>(&token,
        &DecodingKey::from_secret(state.jwt_secret.as_bytes()), &v)
    {
        Ok(data) => data.claims,
        Err(e) => {
            tracing::debug!(error = %e, "logout: token decode failed; no-op");
            return StatusCode::NO_CONTENT;
        }
    };

    if claims.sid.is_empty() {
        // Token predates Phase B (no sid claim). Nothing session-
        // specific to revoke — client already cleared its state.
        return StatusCode::NO_CONTENT;
    }

    let sid = match Uuid::parse_str(&claims.sid) {
        Ok(u) => u,
        Err(_) => return StatusCode::NO_CONTENT,
    };

    let _ = sqlx::query(
        "UPDATE secure_auth.sessions
            SET revoked_at = now()
          WHERE id = $1 AND revoked_at IS NULL",
    )
    .bind(sid)
    .execute(&state.pool)
    .await;

    StatusCode::NO_CONTENT
}

fn unauthorized() -> axum::response::Response {
    (StatusCode::UNAUTHORIZED, Json(serde_json::json!({
        "error":  "unauthorized",
        "detail": "invalid email or password"
    }))).into_response()
}

fn internal_error(detail: &'static str) -> axum::response::Response {
    (StatusCode::INTERNAL_SERVER_ERROR, Json(serde_json::json!({
        "error":  "internal",
        "detail": detail,
    }))).into_response()
}

async fn mfa_not_implemented() -> impl IntoResponse {
    (StatusCode::NOT_IMPLEMENTED, Json(serde_json::json!({
        "error":"not_implemented",
        "detail":"mfa lands in Phase C of the auth-service buildout"
    })))
}

// ─── DB row types ──────────────────────────────────────────────────────────

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

#[derive(Debug, sqlx::FromRow)]
struct SessionRow {
    id:         Uuid,
    user_id:    Uuid,
    #[allow(dead_code)]   // we check expiry in-SQL; kept for logs in Phase G
    expires_at: chrono::DateTime<chrono::Utc>,
}

// ─── Phase B token + session helpers ───────────────────────────────────────

/// Return a fresh 32-byte refresh token, URL-safe base64 with an
/// `rt_` prefix so it's identifiable in logs. Raw value never
/// persists — only its SHA-256 hash ends up in
/// `secure_auth.sessions.refresh_token_hash`.
fn generate_refresh_token() -> String {
    use argon2::password_hash::rand_core::{OsRng, RngCore};
    let mut bytes = [0u8; 32];
    OsRng.fill_bytes(&mut bytes);
    format!("rt_{}", URL_SAFE_NO_PAD.encode(bytes))
}

/// SHA-256 hex (lowercase, 64 chars). Deterministic — same input ->
/// same hash — so the refresh-handler's lookup is a direct b-tree
/// probe on `refresh_token_hash`.
fn sha256_hex(input: &str) -> String {
    let mut h = Sha256::new();
    h.update(input.as_bytes());
    hex::encode(h.finalize())
}

/// Result of `issue_tokens` / `insert_session_row`: the raw refresh
/// token the client receives once, the session id it maps to, and
/// the access_token (filled in by the caller after it signs the JWT).
struct IssuedSession {
    session_id:         Uuid,
    raw_refresh_token:  String,
}

/// Insert a session row + return (session_id, raw_refresh_token).
/// Takes a `&mut PgConnection` — callers pass `&mut *tx` inside a
/// transaction, or acquire a connection from the pool explicitly on
/// the login fast path.
async fn insert_session_row(
    conn: &mut sqlx::PgConnection,
    user_id: Uuid,
    user_agent: Option<&str>,
    ttl_seconds: i64,
) -> sqlx::Result<IssuedSession> {
    let raw = generate_refresh_token();
    let hash = sha256_hex(&raw);
    let session_id: Uuid = sqlx::query_scalar(
        "INSERT INTO secure_auth.sessions
             (user_id, refresh_token_hash, expires_at, user_agent)
         VALUES ($1, $2, now() + make_interval(secs => $3), $4)
         RETURNING id",
    )
    .bind(user_id)
    .bind(&hash)
    .bind(ttl_seconds as f64)
    .bind(user_agent)
    .fetch_one(conn)
    .await?;
    Ok(IssuedSession { session_id, raw_refresh_token: raw })
}

/// Sign an access token JWT with the session id baked into `sid`.
fn sign_access_token(
    state: &AppState,
    user_id: Uuid,
    email: &str,
    session_id: Uuid,
) -> jsonwebtoken::errors::Result<String> {
    let now = chrono::Utc::now().timestamp();
    let claims = Claims {
        sub:   user_id.to_string(),
        exp:   now + state.access_ttl_seconds,
        iat:   now,
        iss:   state.jwt_issuer.clone(),
        aud:   state.jwt_audience.clone(),
        email: email.into(),
        sid:   session_id.to_string(),
    };
    encode(&Header::default(), &claims, &EncodingKey::from_secret(state.jwt_secret.as_bytes()))
}

/// Compose: insert a session + sign an access token + return both
/// with the raw refresh token. Used on login (fresh pool connection);
/// refresh has a slightly different shape because it runs inside a
/// transaction.
async fn issue_tokens(
    state: &AppState,
    user_id: Uuid,
    email: &str,
    user_agent: Option<&str>,
) -> anyhow::Result<(String, String, Uuid)> {
    let mut conn = state.pool.acquire().await?;
    let issued = insert_session_row(&mut *conn, user_id, user_agent,
                                    state.refresh_ttl_seconds).await?;
    let access = sign_access_token(state, user_id, email, issued.session_id)?;
    Ok((access, issued.raw_refresh_token, issued.session_id))
}
