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
        .route("/api/v1/auth/mfa/verify", post(mfa_verify))
        .route("/api/v1/auth/mfa/setup", post(mfa_setup))
        .route("/api/v1/auth/mfa/setup/confirm", post(mfa_setup_confirm))
        .route("/api/v1/auth/mfa/duo/start", post(mfa_duo_start))
        .route("/api/v1/auth/mfa/duo/callback", post(mfa_duo_callback))
        .route("/api/v1/auth/change-password", post(change_password))
        .route("/api/v1/auth/password-reset/request", post(password_reset_request))
        .route("/api/v1/auth/password-reset/confirm", post(password_reset_confirm))
        .route("/api/v1/auth/sso/providers", get(sso_providers_list))
        .route("/api/v1/auth/sso/:provider/start", post(sso_start))
        .route("/api/v1/auth/sso/:provider/callback", post(sso_callback))
        // Dedicated Windows SSO bridge for the desktop shell — no
        // browser redirect flow needed, so no /start. Skips the
        // state-nonce dance the generic /sso/:provider/callback
        // requires because there's no browser hop to guard against
        // CSRF from.
        .route("/api/v1/auth/sso/windows/callback", post(sso_windows_callback))
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
    let email_lower = req.email.to_lowercase();

    // Phase D — lockout check. Count failed attempts in the rolling
    // window; over threshold -> 429. Log the locked-out attempt too
    // so admins can see the attacker's user-agent / IP without having
    // to unlock them first.
    if let Ok(true) = is_locked_out(&state.pool, &email_lower).await {
        log_login_attempt(&state.pool, &email_lower, user_agent.as_deref(),
                          false, Some("locked_out")).await;
        return (
            StatusCode::TOO_MANY_REQUESTS,
            [("retry-after", LOCKOUT_WINDOW_SECONDS.to_string())],
            Json(serde_json::json!({
                "error":  "locked_out",
                "detail": "too many failed login attempts; try again later",
            })),
        ).into_response();
    }

    // Fetch the row. Single query — SELECT returns None on unknown email
    // (treat identically to wrong password to avoid enumeration).
    let row = sqlx::query_as::<_, UserRow>(
        "SELECT id, email, password_hash, display_name, first_name, last_name,
                is_global_admin, mfa_enabled
           FROM secure_auth.users
          WHERE email = $1 AND deleted_at IS NULL",
    )
    .bind(&email_lower)
    .fetch_optional(&state.pool)
    .await;

    let row = match row {
        Ok(Some(r)) => r,
        Ok(None) => {
            log_login_attempt(&state.pool, &email_lower, user_agent.as_deref(),
                              false, Some("unknown_email")).await;
            return unauthorized();
        }
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
            log_login_attempt(&state.pool, &email_lower, user_agent.as_deref(),
                              false, Some("bad_hash_format")).await;
            return unauthorized();
        }
    };
    if Argon2::default().verify_password(req.password.as_bytes(), &parsed).is_err() {
        log_login_attempt(&state.pool, &email_lower, user_agent.as_deref(),
                          false, Some("wrong_password")).await;
        return unauthorized();
    }

    // Phase C: if MFA is enabled for this user, don't issue tokens
    // yet. Instead create a short-lived challenge row + return the
    // "mfa_required" response shape the Angular client recognises
    // (routes to the MFA prompt page instead of setting the session).
    // Phase D: log a success here too — password was right; the
    // second-factor step is in /mfa/verify.
    if row.mfa_enabled {
        log_login_attempt(&state.pool, &email_lower, user_agent.as_deref(),
                          true, None).await;
        return issue_mfa_challenge(&state, &row, user_agent.as_deref()).await;
    }

    // Issue tokens + persist session. Any failure here fails the whole
    // login so the client never gets an access_token with no backing
    // session row.
    match issue_tokens(&state, row.id, &row.email, user_agent.as_deref()).await {
        Ok((access_token, refresh_token, session_id)) => {
            // Phase D — log the successful attempt so the rolling-
            // window lockout counter resets implicitly (the handler
            // queries failures-since-last-success elsewhere).
            log_login_attempt(&state.pool, &email_lower, user_agent.as_deref(),
                              true, None).await;
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


// ─── Phase C — MFA ─────────────────────────────────────────────────────────

const MFA_CHALLENGE_TTL_SECONDS: i64 = 5 * 60;      // 5 minutes
const MFA_MAX_ATTEMPTS_PER_CHALLENGE: i32 = 5;
const MFA_RECOVERY_CODE_COUNT: usize = 10;

/// Insert an `mfa_login_challenges` row + return the "mfa_required"
/// LoginResponse the Angular client recognises. Used by the login
/// handler when the user has MFA enabled.
async fn issue_mfa_challenge(
    state: &AppState,
    user: &UserRow,
    user_agent: Option<&str>,
) -> axum::response::Response {
    let challenge_id: Result<Uuid, _> = sqlx::query_scalar(
        "INSERT INTO secure_auth.mfa_login_challenges
             (user_id, expires_at, user_agent)
         VALUES ($1, now() + make_interval(secs => $2), $3)
         RETURNING id",
    )
    .bind(user.id)
    .bind(MFA_CHALLENGE_TTL_SECONDS as f64)
    .bind(user_agent)
    .fetch_one(&state.pool)
    .await;

    let challenge_id = match challenge_id {
        Ok(id) => id,
        Err(e) => {
            tracing::error!(error = %e, "issue_mfa_challenge insert failed");
            return internal_error("could not create mfa challenge");
        }
    };

    // Check if any recovery codes remain — lets the client show/hide
    // the "use a recovery code instead" link.
    let methods = mfa_methods_for(user.id, &state.pool).await;

    let resp = LoginResponse {
        access_token:  String::new(),           // gated by mfa/verify
        refresh_token: String::new(),
        session_id:    challenge_id.to_string(),
        expires_in:    0,
        token_type:    String::new(),
        mfa_required:  true,
        mfa_methods:   methods,
        user: AuthUser {
            // Phase C: return minimal user info on the challenge
            // response. Full profile lands after verify.
            id:           user.id.to_string(),
            email:        user.email.clone(),
            display_name: user.display_name.clone().unwrap_or_default(),
            first_name:   user.first_name.clone(),
            last_name:    user.last_name.clone(),
            roles:        Vec::new(),
            permissions:  Vec::new(),
            mfa_enabled:  true,
        },
    };
    (StatusCode::OK, Json(resp)).into_response()
}

/// Return the list of MFA methods this user has. Always includes
/// "totp" when mfa_enabled is true + a secret exists; adds
/// "recovery" when at least one unconsumed recovery code exists;
/// adds "duo" when the user has duo_enabled + the platform's
/// duo_config row is active.
async fn mfa_methods_for(user_id: Uuid, pool: &PgPool) -> Vec<String> {
    let mut methods = vec!["totp".to_string()];
    let has_recovery: Option<i64> = sqlx::query_scalar(
        "SELECT COUNT(*) FROM secure_auth.mfa_recovery_codes
          WHERE user_id = $1 AND consumed_at IS NULL",
    )
    .bind(user_id)
    .fetch_one(pool)
    .await
    .ok();
    if has_recovery.unwrap_or(0) > 0 {
        methods.push("recovery".to_string());
    }

    // Duo — add when the user is enrolled AND the platform's duo_config
    // row exists. Phase C.1 doesn't distinguish mock vs live here; the
    // client sees "duo" as a method + /mfa/duo/start reports the mode.
    let duo_available: Option<bool> = sqlx::query_scalar(
        "SELECT COUNT(*) > 0 FROM secure_auth.users u
          WHERE u.id = $1
            AND u.duo_enabled = true
            AND EXISTS (SELECT 1 FROM secure_auth.duo_config)",
    )
    .bind(user_id)
    .fetch_one(pool)
    .await
    .ok();
    if duo_available.unwrap_or(false) {
        methods.push("duo".to_string());
    }

    methods
}

#[derive(Debug, Deserialize)]
struct MfaVerifyRequest {
    session_id: String,                      // == mfa_login_challenges.id
    code:       String,
    #[serde(default = "default_method")]
    method:     String,                      // "totp" | "recovery"
}
fn default_method() -> String { "totp".into() }

async fn mfa_verify(
    State(state): State<Arc<AppState>>,
    headers: HeaderMap,
    Json(req): Json<MfaVerifyRequest>,
) -> impl IntoResponse {
    let challenge_id = match Uuid::parse_str(&req.session_id) {
        Ok(u) => u,
        Err(_) => return unauthorized(),
    };
    let user_agent = headers
        .get("user-agent")
        .and_then(|v| v.to_str().ok())
        .map(|s| s.to_owned());

    // Transaction — challenge consumption + recovery-code consumption
    // + session issue all land together or not at all. Fail-closed:
    // any partial failure leaves the challenge still alive so the
    // user can retry, rather than a half-consumed state.
    let mut tx = match state.pool.begin().await {
        Ok(t) => t,
        Err(e) => {
            tracing::error!(error = %e, "mfa_verify: begin tx failed");
            return internal_error("database error");
        }
    };

    let challenge = sqlx::query_as::<_, ChallengeRow>(
        "SELECT id, user_id, expires_at, consumed_at, failed_attempts
           FROM secure_auth.mfa_login_challenges
          WHERE id = $1
          FOR UPDATE",
    )
    .bind(challenge_id)
    .fetch_optional(&mut *tx)
    .await;

    let challenge = match challenge {
        Ok(Some(c)) => c,
        Ok(None) => return unauthorized(),
        Err(e) => {
            tracing::error!(error = %e, "mfa_verify: challenge lookup");
            return internal_error("database error");
        }
    };

    if challenge.consumed_at.is_some()
        || challenge.expires_at < chrono::Utc::now()
        || challenge.failed_attempts >= MFA_MAX_ATTEMPTS_PER_CHALLENGE
    {
        return unauthorized();
    }

    // Verify the code — method-specific paths. `consumed_recovery_id`
    // tracks which recovery code the match hit so we can mark it
    // consumed after the challenge succeeds.
    let (ok, consumed_recovery_id) = match req.method.as_str() {
        "totp"     => (verify_totp(&mut tx, challenge.user_id, &req.code).await, None),
        "recovery" => verify_recovery_code(&mut tx, challenge.user_id, &req.code).await,
        other => {
            tracing::warn!(method = %other, "mfa_verify: unknown method");
            (false, None)
        }
    };

    if !ok {
        let _ = sqlx::query(
            "UPDATE secure_auth.mfa_login_challenges
                SET failed_attempts = failed_attempts + 1
              WHERE id = $1",
        )
        .bind(challenge_id)
        .execute(&mut *tx)
        .await;
        let _ = tx.commit().await;
        return unauthorized();
    }

    // Code verified. Mark challenge consumed + recovery code (if used)
    // + issue tokens in the same transaction.
    if let Err(e) = sqlx::query(
        "UPDATE secure_auth.mfa_login_challenges
            SET consumed_at = now()
          WHERE id = $1",
    )
    .bind(challenge_id)
    .execute(&mut *tx)
    .await
    {
        tracing::error!(error = %e, "mfa_verify: mark challenge consumed failed");
        return internal_error("database error");
    }

    if let Some(rid) = consumed_recovery_id {
        let _ = sqlx::query(
            "UPDATE secure_auth.mfa_recovery_codes
                SET consumed_at = now()
              WHERE id = $1",
        )
        .bind(rid)
        .execute(&mut *tx)
        .await;
    }

    // Fetch user for the response payload.
    let user = sqlx::query_as::<_, UserRow>(
        "SELECT id, email, password_hash, display_name, first_name, last_name,
                is_global_admin, mfa_enabled
           FROM secure_auth.users
          WHERE id = $1 AND deleted_at IS NULL",
    )
    .bind(challenge.user_id)
    .fetch_optional(&mut *tx)
    .await;

    let user = match user {
        Ok(Some(u)) => u,
        Ok(None) => return unauthorized(),
        Err(e) => {
            tracing::error!(error = %e, "mfa_verify: user lookup");
            return internal_error("database error");
        }
    };

    let issued = match insert_session_row(&mut *tx, user.id, user_agent.as_deref(),
                                          state.refresh_ttl_seconds).await {
        Ok(v) => v,
        Err(e) => {
            tracing::error!(error = %e, "mfa_verify: session insert");
            return internal_error("session store failed");
        }
    };
    let access_token = match sign_access_token(&state, user.id, &user.email, issued.session_id) {
        Ok(t) => t,
        Err(e) => {
            tracing::error!(error = %e, "mfa_verify: jwt sign");
            return internal_error("token encode failed");
        }
    };

    if let Err(e) = tx.commit().await {
        tracing::error!(error = %e, "mfa_verify: commit failed");
        return internal_error("database error");
    }
    let _ = sqlx::query("UPDATE secure_auth.users SET last_login_at = now() WHERE id = $1")
        .bind(user.id).execute(&state.pool).await;

    let roles = if user.is_global_admin { vec!["global_admin".into()] } else { vec!["user".into()] };
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

/// TOTP verify against `secure_auth.mfa_secrets`. The totp-rs crate's
/// `check_current` accepts the previous + current + next period
/// (±30s) automatically, which is what we want for clock skew.
async fn verify_totp(
    tx: &mut sqlx::PgConnection,
    user_id: Uuid,
    code: &str,
) -> bool {
    use totp_rs::{Algorithm, TOTP};

    let row: Result<Option<(String, String, i32, i32)>, _> = sqlx::query_as(
        "SELECT secret_base32, algorithm, digits, period_seconds
           FROM secure_auth.mfa_secrets
          WHERE user_id = $1",
    )
    .bind(user_id)
    .fetch_optional(&mut *tx)
    .await;

    let Ok(Some((secret_b32, algo, digits, period))) = row else {
        return false;
    };

    let alg = match algo.as_str() {
        "SHA256" => Algorithm::SHA256,
        "SHA512" => Algorithm::SHA512,
        _        => Algorithm::SHA1,
    };
    let secret = match totp_rs::Secret::Encoded(secret_b32).to_bytes() {
        Ok(b) => b,
        Err(_) => return false,
    };
    // totp-rs 5.x with the `otpauth` feature takes 7 args: algorithm,
    // digits, skew window, step, secret, issuer (Option), account_name.
    // We only use check_current here so the issuer/account are
    // placeholders — /mfa/setup builds the user-facing otpauth URI
    // separately.
    let totp = match TOTP::new(alg, digits as usize, 1, period as u64,
                               secret, None, String::new()) {
        Ok(t) => t,
        Err(_) => return false,
    };
    totp.check_current(code).unwrap_or(false)
}

/// Iterate active recovery codes + Argon2-verify each. Returns
/// `(matched, id_of_consumed_row)`. Scan is O(N) where N ≤ 10
/// per user.
async fn verify_recovery_code(
    tx: &mut sqlx::PgConnection,
    user_id: Uuid,
    code: &str,
) -> (bool, Option<Uuid>) {
    // Normalise user input — accept "abcd-efgh" or "abcdefgh".
    let normalised = code.replace('-', "").to_lowercase();

    let rows: Result<Vec<(Uuid, String)>, _> = sqlx::query_as(
        "SELECT id, code_hash FROM secure_auth.mfa_recovery_codes
          WHERE user_id = $1 AND consumed_at IS NULL",
    )
    .bind(user_id)
    .fetch_all(&mut *tx)
    .await;

    let Ok(rows) = rows else { return (false, None); };

    let argon = Argon2::default();
    for (id, hash) in rows {
        let Ok(parsed) = PasswordHash::new(&hash) else { continue; };
        if argon.verify_password(normalised.as_bytes(), &parsed).is_ok() {
            return (true, Some(id));
        }
    }
    (false, None)
}

// ─── /mfa/setup + /mfa/setup/confirm ───────────────────────────────────────

#[derive(Debug, Serialize)]
struct MfaSetupResponse {
    otpauth_uri:    String,
    secret_base32:  String,
    recovery_codes: Vec<String>,   // raw; shown once, client must store
}

/// Generate a fresh TOTP secret + 10 recovery codes. Overwrites any
/// previous setup (upsert on user_id). Does NOT flip
/// users.mfa_enabled — the user must prove they have the
/// authenticator by calling /mfa/setup/confirm with a code first.
///
/// Requires a valid access token (Bearer auth).
async fn mfa_setup(
    State(state): State<Arc<AppState>>,
    headers: HeaderMap,
) -> impl IntoResponse {
    let user = match authed_user(&state, &headers).await {
        Some(u) => u,
        None    => return unauthorized(),
    };

    // Generate secret (160-bit, 32 base32 chars) + 10 recovery codes.
    use argon2::password_hash::rand_core::{OsRng, RngCore};
    let mut secret_raw = [0u8; 20];
    OsRng.fill_bytes(&mut secret_raw);
    let secret_base32 = totp_rs::Secret::Raw(secret_raw.to_vec()).to_encoded().to_string();

    let mut recovery_raw = Vec::with_capacity(MFA_RECOVERY_CODE_COUNT);
    for _ in 0..MFA_RECOVERY_CODE_COUNT {
        let mut b = [0u8; 5];   // 40 bits -> 8 hex chars "xxxxyyyy"
        OsRng.fill_bytes(&mut b);
        let hex = hex::encode(b);
        recovery_raw.push(format!("{}-{}", &hex[..4], &hex[4..]));
    }

    // Argon2-hash each recovery code in memory before the DB round-
    // trip. Salts are per-code (SaltString::generate).
    use argon2::password_hash::{PasswordHasher, SaltString};
    let argon = Argon2::default();
    let mut recovery_hashes = Vec::with_capacity(MFA_RECOVERY_CODE_COUNT);
    for raw in &recovery_raw {
        let salt = SaltString::generate(&mut OsRng);
        let normalised = raw.replace('-', "").to_lowercase();
        let hash = match argon.hash_password(normalised.as_bytes(), &salt) {
            Ok(h) => h.to_string(),
            Err(e) => {
                tracing::error!(error = %e, "mfa_setup: recovery hash failed");
                return internal_error("hash failed");
            }
        };
        recovery_hashes.push(hash);
    }

    // Persist. Transaction so we never leave a half-setup state
    // (secret but no recovery codes, or vice versa).
    let mut tx = match state.pool.begin().await {
        Ok(t) => t,
        Err(e) => {
            tracing::error!(error = %e, "mfa_setup: begin tx");
            return internal_error("database error");
        }
    };

    // Upsert — re-enrolling overwrites the previous secret. Old
    // recovery codes are deleted so they can't be used to sidestep
    // the new TOTP.
    if let Err(e) = sqlx::query(
        "INSERT INTO secure_auth.mfa_secrets (user_id, secret_base32)
         VALUES ($1, $2)
         ON CONFLICT (user_id) DO UPDATE
         SET secret_base32 = EXCLUDED.secret_base32,
             verified_at   = NULL,
             created_at    = now()",
    )
    .bind(user.id)
    .bind(&secret_base32)
    .execute(&mut *tx)
    .await
    {
        tracing::error!(error = %e, "mfa_setup: insert secret");
        return internal_error("database error");
    }

    let _ = sqlx::query("DELETE FROM secure_auth.mfa_recovery_codes WHERE user_id = $1")
        .bind(user.id).execute(&mut *tx).await;

    for hash in &recovery_hashes {
        if let Err(e) = sqlx::query(
            "INSERT INTO secure_auth.mfa_recovery_codes (user_id, code_hash) VALUES ($1, $2)",
        )
        .bind(user.id)
        .bind(hash)
        .execute(&mut *tx)
        .await
        {
            tracing::error!(error = %e, "mfa_setup: insert recovery code");
            return internal_error("database error");
        }
    }

    if let Err(e) = tx.commit().await {
        tracing::error!(error = %e, "mfa_setup: commit");
        return internal_error("database error");
    }

    let otpauth_uri = format!(
        "otpauth://totp/Central:{email}?secret={secret}&issuer=Central&digits=6&period=30",
        email  = urlencode(&user.email),
        secret = secret_base32,
    );

    let resp = MfaSetupResponse {
        otpauth_uri,
        secret_base32,
        recovery_codes: recovery_raw,
    };
    (StatusCode::OK, Json(resp)).into_response()
}

#[derive(Debug, Deserialize)]
struct MfaSetupConfirmRequest { code: String }

/// Confirms /mfa/setup by verifying a TOTP code. On success flips
/// users.mfa_enabled=true + mfa_secrets.verified_at=now. Idempotent
/// if already enabled (returns 204 without re-verifying).
async fn mfa_setup_confirm(
    State(state): State<Arc<AppState>>,
    headers: HeaderMap,
    Json(req): Json<MfaSetupConfirmRequest>,
) -> impl IntoResponse {
    let user = match authed_user(&state, &headers).await {
        Some(u) => u,
        None    => return unauthorized(),
    };

    let mut tx = match state.pool.begin().await {
        Ok(t) => t,
        Err(e) => { tracing::error!(error = %e, "setup_confirm: begin tx"); return internal_error("database error"); }
    };

    if !verify_totp(&mut *tx, user.id, &req.code).await {
        return unauthorized();
    }

    let _ = sqlx::query("UPDATE secure_auth.mfa_secrets SET verified_at = now() WHERE user_id = $1")
        .bind(user.id).execute(&mut *tx).await;
    let _ = sqlx::query("UPDATE secure_auth.users SET mfa_enabled = true WHERE id = $1")
        .bind(user.id).execute(&mut *tx).await;

    if let Err(e) = tx.commit().await {
        tracing::error!(error = %e, "setup_confirm: commit");
        return internal_error("database error");
    }
    StatusCode::NO_CONTENT.into_response()
}

/// Decode + verify a Bearer JWT from the Authorization header, then
/// look up the user. Returns None on any failure (expired, bad
/// signature, unknown sid, deleted user). Callers render 401.
async fn authed_user(state: &AppState, headers: &HeaderMap) -> Option<UserRow> {
    let token = headers.get("authorization")?
        .to_str().ok()?
        .strip_prefix("Bearer ")?;

    let mut v = Validation::default();
    v.validate_aud = false;
    v.leeway = 30;
    let data = decode::<Claims>(token,
        &DecodingKey::from_secret(state.jwt_secret.as_bytes()), &v).ok()?;
    let user_id = Uuid::parse_str(&data.claims.sub).ok()?;

    sqlx::query_as::<_, UserRow>(
        "SELECT id, email, password_hash, display_name, first_name, last_name,
                is_global_admin, mfa_enabled
           FROM secure_auth.users
          WHERE id = $1 AND deleted_at IS NULL",
    )
    .bind(user_id)
    .fetch_optional(&state.pool)
    .await
    .ok()
    .flatten()
}

/// Minimal URL-percent-encoder — `totp-rs` provides otpauth builders,
/// but we need the email to go in the path segment safely. Only the
/// handful of chars that matter for an email in an otpauth URI.
fn urlencode(s: &str) -> String {
    let mut out = String::with_capacity(s.len());
    for c in s.chars() {
        match c {
            'A'..='Z' | 'a'..='z' | '0'..='9' | '-' | '_' | '.' | '~' => out.push(c),
            '@' => out.push_str("%40"),
            _   => out.push_str(&format!("%{:02X}", c as u32)),
        }
    }
    out
}

// ─── Phase D — password management + lockout ──────────────────────────────

const LOCKOUT_THRESHOLD:       i64 = 5;
const LOCKOUT_WINDOW_SECONDS:  i64 = 15 * 60;
const PASSWORD_HISTORY_MAX:    i64 = 5;
const PASSWORD_RESET_TTL_SECS: i64 = 60 * 60;    // 1 hour

/// True when the user has ≥ LOCKOUT_THRESHOLD failed login attempts
/// in the last LOCKOUT_WINDOW_SECONDS. A successful login since the
/// first failure resets the counter implicitly — the query only
/// counts failures after the most recent success.
async fn is_locked_out(pool: &PgPool, email_lower: &str) -> sqlx::Result<bool> {
    let fails: i64 = sqlx::query_scalar(
        "SELECT COUNT(*) FROM secure_auth.login_attempts
          WHERE email = $1
            AND succeeded = false
            AND attempted_at > now() - make_interval(secs => $2)
            AND attempted_at > COALESCE(
                  (SELECT MAX(attempted_at) FROM secure_auth.login_attempts
                    WHERE email = $1 AND succeeded = true),
                  'epoch'::timestamptz
                )",
    )
    .bind(email_lower)
    .bind(LOCKOUT_WINDOW_SECONDS as f64)
    .fetch_one(pool)
    .await?;
    Ok(fails >= LOCKOUT_THRESHOLD)
}

/// Fire-and-forget audit log. Writes failure/success to
/// login_attempts; a failure here shouldn't block the login path.
async fn log_login_attempt(
    pool: &PgPool,
    email_lower: &str,
    user_agent: Option<&str>,
    succeeded: bool,
    reason: Option<&str>,
) {
    let _ = sqlx::query(
        "INSERT INTO secure_auth.login_attempts
             (email, user_agent, succeeded, failure_reason)
         VALUES ($1, $2, $3, $4)",
    )
    .bind(email_lower)
    .bind(user_agent)
    .bind(succeeded)
    .bind(reason)
    .execute(pool)
    .await;
}

#[derive(Debug, Deserialize)]
struct ChangePasswordRequest {
    old_password: String,
    new_password: String,
}

/// Authed — verifies the old password, checks the new against
/// PASSWORD_HISTORY_MAX most-recent hashes, writes the new,
/// appends the OLD hash to password_history, trims history to N.
/// Also revokes every live session for the user — the point of
/// changing the password is to invalidate a stolen one.
async fn change_password(
    State(state): State<Arc<AppState>>,
    headers: HeaderMap,
    Json(req): Json<ChangePasswordRequest>,
) -> impl IntoResponse {
    let user = match authed_user(&state, &headers).await {
        Some(u) => u,
        None    => return unauthorized(),
    };

    // Old password check. Same enumeration-resistance rules as login
    // (returns 401 without saying which part failed).
    let parsed = match PasswordHash::new(&user.password_hash) {
        Ok(p) => p,
        Err(_) => return unauthorized(),
    };
    if Argon2::default().verify_password(req.old_password.as_bytes(), &parsed).is_err() {
        return unauthorized();
    }

    // Minimal policy check. Full policy (uppercase/digit/symbol) lands
    // alongside Phase E or G; Phase D just refuses obvious weaknesses.
    if req.new_password.len() < 12 {
        return (
            StatusCode::BAD_REQUEST,
            Json(serde_json::json!({
                "error":  "weak_password",
                "detail": "new password must be at least 12 characters",
            })),
        ).into_response();
    }
    if req.new_password == req.old_password {
        return (
            StatusCode::BAD_REQUEST,
            Json(serde_json::json!({
                "error":  "weak_password",
                "detail": "new password must differ from old password",
            })),
        ).into_response();
    }

    // Reject if the new password matches any of the last N hashes.
    let recent_hashes: Result<Vec<(String,)>, _> = sqlx::query_as(
        "SELECT password_hash FROM secure_auth.password_history
          WHERE user_id = $1 ORDER BY retired_at DESC LIMIT $2",
    )
    .bind(user.id)
    .bind(PASSWORD_HISTORY_MAX)
    .fetch_all(&state.pool)
    .await;
    if let Ok(rows) = recent_hashes {
        for (h,) in &rows {
            if let Ok(p) = PasswordHash::new(h) {
                if Argon2::default().verify_password(req.new_password.as_bytes(), &p).is_ok() {
                    return (
                        StatusCode::BAD_REQUEST,
                        Json(serde_json::json!({
                            "error":  "password_reused",
                            "detail": format!("password matches one of the last {PASSWORD_HISTORY_MAX} hashes"),
                        })),
                    ).into_response();
                }
            }
        }
    }

    // Hash + persist.
    let new_hash = match hash_password(&req.new_password) {
        Ok(h) => h,
        Err(e) => {
            tracing::error!(error = %e, "change_password: hash failed");
            return internal_error("hash failed");
        }
    };

    let mut tx = match state.pool.begin().await {
        Ok(t) => t,
        Err(e) => {
            tracing::error!(error = %e, "change_password: tx begin");
            return internal_error("database error");
        }
    };

    if sqlx::query(
        "INSERT INTO secure_auth.password_history (user_id, password_hash) VALUES ($1, $2)",
    )
    .bind(user.id)
    .bind(&user.password_hash)
    .execute(&mut *tx)
    .await.is_err()
    {
        return internal_error("database error");
    }

    // Trim history to the newest N. Keep the audit trail bounded but
    // preserve the rows we check against.
    let _ = sqlx::query(
        "DELETE FROM secure_auth.password_history
          WHERE user_id = $1 AND id NOT IN (
              SELECT id FROM secure_auth.password_history
               WHERE user_id = $1 ORDER BY retired_at DESC LIMIT $2
          )",
    )
    .bind(user.id)
    .bind(PASSWORD_HISTORY_MAX)
    .execute(&mut *tx)
    .await;

    if sqlx::query(
        "UPDATE secure_auth.users SET password_hash = $1 WHERE id = $2",
    )
    .bind(&new_hash)
    .bind(user.id)
    .execute(&mut *tx)
    .await.is_err()
    {
        return internal_error("database error");
    }

    // Revoke all live sessions — new password invalidates anyone else
    // holding a refresh token.
    let _ = sqlx::query(
        "UPDATE secure_auth.sessions SET revoked_at = now()
          WHERE user_id = $1 AND revoked_at IS NULL",
    )
    .bind(user.id)
    .execute(&mut *tx)
    .await;

    if tx.commit().await.is_err() {
        return internal_error("database error");
    }
    StatusCode::NO_CONTENT.into_response()
}

fn hash_password(pwd: &str) -> Result<String, argon2::password_hash::Error> {
    use argon2::password_hash::{rand_core::OsRng, PasswordHasher, SaltString};
    let salt = SaltString::generate(&mut OsRng);
    Argon2::default()
        .hash_password(pwd.as_bytes(), &salt)
        .map(|h| h.to_string())
}

#[derive(Debug, Deserialize)]
struct PasswordResetRequestRequest { email: String }

#[derive(Debug, Serialize)]
struct PasswordResetRequestResponse {
    // Phase D returns the raw token in the response so dev can
    // exercise the flow without an email provider. Phase F swaps this
    // for an empty response + the token goes through Central's
    // notifications pipeline.
    reset_token: String,
    expires_in:  i64,
}

async fn password_reset_request(
    State(state): State<Arc<AppState>>,
    Json(req): Json<PasswordResetRequestRequest>,
) -> impl IntoResponse {
    let email_lower = req.email.to_lowercase();

    // Look up user. Enumeration-resistance: we always return 200 with
    // a token shape even for unknown emails. For unknown we just
    // don't persist anything — the token we return is random + will
    // fail /password-reset/confirm.
    let user_id_opt: Option<(Uuid,)> = sqlx::query_as(
        "SELECT id FROM secure_auth.users
          WHERE email = $1 AND deleted_at IS NULL",
    )
    .bind(&email_lower)
    .fetch_optional(&state.pool)
    .await
    .ok()
    .flatten();

    let raw = generate_refresh_token();   // reusing — same shape, different purpose
    if let Some((user_id,)) = user_id_opt {
        let hash = sha256_hex(&raw);
        let _ = sqlx::query(
            "INSERT INTO secure_auth.password_reset_tokens
                 (user_id, token_hash, expires_at)
             VALUES ($1, $2, now() + make_interval(secs => $3))",
        )
        .bind(user_id)
        .bind(&hash)
        .bind(PASSWORD_RESET_TTL_SECS as f64)
        .execute(&state.pool)
        .await;
    }

    (StatusCode::OK, Json(PasswordResetRequestResponse {
        reset_token: raw,
        expires_in:  PASSWORD_RESET_TTL_SECS,
    })).into_response()
}

#[derive(Debug, Deserialize)]
struct PasswordResetConfirmRequest {
    reset_token:  String,
    new_password: String,
}

async fn password_reset_confirm(
    State(state): State<Arc<AppState>>,
    Json(req): Json<PasswordResetConfirmRequest>,
) -> impl IntoResponse {
    if req.new_password.len() < 12 {
        return (
            StatusCode::BAD_REQUEST,
            Json(serde_json::json!({
                "error":  "weak_password",
                "detail": "new password must be at least 12 characters",
            })),
        ).into_response();
    }

    let hash = sha256_hex(&req.reset_token);

    let mut tx = match state.pool.begin().await {
        Ok(t) => t,
        Err(e) => { tracing::error!(error = %e, "reset_confirm: tx"); return internal_error("database error"); }
    };

    let row: Result<Option<(Uuid, Uuid)>, _> = sqlx::query_as(
        "SELECT id, user_id FROM secure_auth.password_reset_tokens
          WHERE token_hash = $1 AND consumed_at IS NULL AND expires_at > now()
          FOR UPDATE",
    )
    .bind(&hash)
    .fetch_optional(&mut *tx)
    .await;

    let (token_id, user_id) = match row {
        Ok(Some(r)) => r,
        _ => return unauthorized(),
    };

    let new_hash = match hash_password(&req.new_password) {
        Ok(h) => h,
        Err(e) => { tracing::error!(error = %e, "reset_confirm: hash"); return internal_error("hash failed"); }
    };

    // Update password, mark token consumed, push old hash to history,
    // revoke all sessions. All in one transaction.
    let old_hash: Result<String, _> = sqlx::query_scalar(
        "SELECT password_hash FROM secure_auth.users WHERE id = $1",
    )
    .bind(user_id)
    .fetch_one(&mut *tx)
    .await;
    if let Ok(old_hash) = old_hash {
        let _ = sqlx::query(
            "INSERT INTO secure_auth.password_history (user_id, password_hash) VALUES ($1, $2)",
        )
        .bind(user_id).bind(&old_hash).execute(&mut *tx).await;
    }

    let _ = sqlx::query("UPDATE secure_auth.users SET password_hash = $1 WHERE id = $2")
        .bind(&new_hash).bind(user_id).execute(&mut *tx).await;

    let _ = sqlx::query(
        "UPDATE secure_auth.password_reset_tokens
            SET consumed_at = now()
          WHERE id = $1",
    )
    .bind(token_id).execute(&mut *tx).await;

    let _ = sqlx::query(
        "UPDATE secure_auth.sessions SET revoked_at = now()
          WHERE user_id = $1 AND revoked_at IS NULL",
    )
    .bind(user_id).execute(&mut *tx).await;

    if tx.commit().await.is_err() {
        return internal_error("database error");
    }
    StatusCode::NO_CONTENT.into_response()
}

// ─── Phase E.1 — SSO / federation scaffold ────────────────────────────────

const SSO_SESSION_TTL_SECS: i64 = 5 * 60;       // 5 minutes /start → /callback

#[derive(Debug, Serialize, sqlx::FromRow)]
struct ProviderRow {
    provider_code: String,
    kind:          String,
    display_name:  String,
}

/// GET /api/v1/auth/sso/providers — visible, enabled, non-deleted
/// providers the caller can start an SSO flow against. Tenant scope
/// is honoured in Phase E.2 when we wire the X-Tenant-ID header into
/// the filter; Phase E.1 returns platform-wide providers (tenant_id
/// IS NULL) + ignores tenant-scoped rows.
async fn sso_providers_list(
    State(state): State<Arc<AppState>>,
) -> impl IntoResponse {
    let rows: Result<Vec<ProviderRow>, _> = sqlx::query_as(
        "SELECT provider_code, kind, display_name
           FROM secure_auth.identity_providers
          WHERE enabled = true
            AND deleted_at IS NULL
            AND tenant_id IS NULL
          ORDER BY display_name",
    )
    .fetch_all(&state.pool)
    .await;
    match rows {
        Ok(v)  => (StatusCode::OK, Json(v)).into_response(),
        Err(e) => {
            tracing::error!(error = %e, "sso_providers_list failed");
            internal_error("database error")
        }
    }
}

#[derive(Debug, Deserialize)]
struct SsoStartRequest {
    #[serde(default)]
    redirect_uri: Option<String>,
    /// When present, this is an account-linking flow — the caller is
    /// already logged in + wants to attach the new identity to their
    /// existing user row. Phase E.1 doesn't use it yet (mock provider
    /// is always create-or-login) but the state carries through to
    /// Phase E.2 without a schema change.
    #[serde(default)]
    link_user_id: Option<String>,
}

#[derive(Debug, Serialize)]
struct SsoStartResponse {
    state:        String,
    redirect_url: String,
    /// For `kind=mock` the callback is called directly by tests with
    /// the state nonce — no real IdP round-trip. For real providers
    /// this is where the browser navigates.
    kind:         String,
}

async fn sso_start(
    State(state): State<Arc<AppState>>,
    axum::extract::Path(provider): axum::extract::Path<String>,
    Json(req): Json<SsoStartRequest>,
) -> impl IntoResponse {
    let prov = match fetch_provider(&state.pool, &provider).await {
        Some(p) => p,
        None    => return (
            StatusCode::NOT_FOUND,
            Json(serde_json::json!({
                "error":  "unknown_provider",
                "detail": format!("no enabled provider with code '{provider}'"),
            })),
        ).into_response(),
    };

    let link_uuid = req.link_user_id.as_deref().and_then(|s| Uuid::parse_str(s).ok());

    // State nonce = 32 crypto-random bytes, URL-safe base64. Stored
    // raw in secure_auth.sso_sessions — deterministic lookup on
    // callback + raw value never leaves the server except in the
    // redirect URL.
    let state_nonce = generate_refresh_token();   // shape reusable; prefix is "rt_" but that's fine
    let _ = sqlx::query(
        "INSERT INTO secure_auth.sso_sessions
             (state_nonce, provider_code, redirect_uri, link_user_id,
              expires_at)
         VALUES ($1, $2, $3, $4, now() + make_interval(secs => $5))",
    )
    .bind(&state_nonce)
    .bind(&prov.provider_code)
    .bind(req.redirect_uri.as_deref())
    .bind(link_uuid)
    .bind(SSO_SESSION_TTL_SECS as f64)
    .execute(&state.pool)
    .await;

    let redirect_url = match prov.kind.as_str() {
        "mock" => {
            // For the mock provider, the "redirect" is just a pointer
            // at our own callback. Dev tools POST to
            // /sso/mock/callback with { state, email } directly.
            format!("about:blank?sso_state={state_nonce}")
        }
        // E.2 / E.3: build the real redirect URL from config_json
        // (issuer, client_id, scopes, etc.). Until then, we tell the
        // client the provider isn't wired yet.
        _ => return not_implemented(
            "provider kind not yet supported — see docs/AUTH_SERVICE_BUILDOUT.md Phase E.2/E.3",
        ),
    };

    (StatusCode::OK, Json(SsoStartResponse {
        state:        state_nonce,
        redirect_url,
        kind:         prov.kind,
    })).into_response()
}

#[derive(Debug, Deserialize)]
#[allow(dead_code)]   // `code` wires to the IdP exchange in Phase E.2
struct SsoCallbackRequest {
    state: String,
    /// Real providers send { code } which the callback exchanges with
    /// the IdP for an id_token / userinfo. Phase E.1's mock provider
    /// takes { email } directly — tests supply the email they want
    /// the issued user to resolve to.
    #[serde(default)]
    code:  Option<String>,
    #[serde(default)]
    email: Option<String>,
}

async fn sso_callback(
    State(state): State<Arc<AppState>>,
    headers: HeaderMap,
    axum::extract::Path(provider): axum::extract::Path<String>,
    Json(req): Json<SsoCallbackRequest>,
) -> impl IntoResponse {
    let user_agent = headers
        .get("user-agent")
        .and_then(|v| v.to_str().ok())
        .map(|s| s.to_owned());

    let mut tx = match state.pool.begin().await {
        Ok(t) => t,
        Err(e) => {
            tracing::error!(error = %e, "sso_callback: begin tx");
            return internal_error("database error");
        }
    };

    // Look up + lock the sso_sessions row. Checks: unconsumed + not
    // expired + matches the provider slug in the URL (guards against
    // a state nonce from one provider being replayed at another's
    // callback).
    let row: Result<Option<(Uuid, String, Option<Uuid>)>, _> = sqlx::query_as(
        "SELECT id, provider_code, link_user_id
           FROM secure_auth.sso_sessions
          WHERE state_nonce  = $1
            AND provider_code = $2
            AND consumed_at IS NULL
            AND expires_at  > now()
          FOR UPDATE",
    )
    .bind(&req.state)
    .bind(&provider)
    .fetch_optional(&mut *tx)
    .await;

    let (sso_id, _prov, link_user_id) = match row {
        Ok(Some(r)) => r,
        _ => return unauthorized(),
    };

    let prov = match fetch_provider_tx(&mut tx, &provider).await {
        Some(p) => p,
        None    => return unauthorized(),
    };

    // Handler dispatch per provider kind. E.1 only implements `mock`;
    // the others return a 501 so the client gets an honest "not
    // wired yet" + the state row stays unconsumed for retry.
    let (email, external_id, claims_json) = match prov.kind.as_str() {
        "mock" => {
            let email = match req.email.as_deref() {
                Some(e) if !e.is_empty() => e.to_lowercase(),
                _ => return (
                    StatusCode::BAD_REQUEST,
                    Json(serde_json::json!({
                        "error":  "bad_request",
                        "detail": "mock provider requires { email } in body",
                    })),
                ).into_response(),
            };
            (email.clone(), email.clone(),
             serde_json::json!({"via": "mock", "email": email}))
        }
        _ => return not_implemented(
            "provider kind not yet supported — see docs/AUTH_SERVICE_BUILDOUT.md Phase E.2/E.3",
        ),
    };

    // Consume the sso_session row regardless of what happens next —
    // a consumed state nonce is dead for replay purposes whether the
    // user successfully binds or the handler fails.
    let _ = sqlx::query(
        "UPDATE secure_auth.sso_sessions SET consumed_at = now() WHERE id = $1",
    )
    .bind(sso_id)
    .execute(&mut *tx)
    .await;

    // Resolve the user: (a) if this is a link-flow + we already know
    // who the user is, link the external identity to them; (b) else
    // find an existing user by email; (c) else create a new user.
    let user_id: Uuid = if let Some(uid) = link_user_id {
        uid
    } else {
        let existing: Result<Option<(Uuid,)>, _> = sqlx::query_as(
            "SELECT id FROM secure_auth.users
              WHERE email = $1 AND deleted_at IS NULL",
        )
        .bind(&email)
        .fetch_optional(&mut *tx)
        .await;
        match existing {
            Ok(Some((uid,))) => uid,
            Ok(None) => {
                // Create a new user with no password hash (SSO-only).
                // Phase E places a sentinel hash that can't be Argon2-
                // verified by anything, so the /login path stays
                // closed for this account until /change-password or
                // /password-reset/confirm sets a real one.
                let new_id: Uuid = match sqlx::query_scalar(
                    "INSERT INTO secure_auth.users
                         (email, password_hash, display_name)
                     VALUES ($1, '(sso-only)', $2)
                     RETURNING id",
                )
                .bind(&email)
                .bind(&email)
                .fetch_one(&mut *tx)
                .await {
                    Ok(id) => id,
                    Err(e) => {
                        tracing::error!(error = %e, "sso_callback: user create");
                        return internal_error("database error");
                    }
                };
                new_id
            }
            Err(e) => {
                tracing::error!(error = %e, "sso_callback: user lookup");
                return internal_error("database error");
            }
        }
    };

    // Link the external identity (upsert). Updates last_seen_at +
    // raw_claims on each login.
    let _ = sqlx::query(
        "INSERT INTO secure_auth.user_external_identities
             (user_id, provider_code, external_id, raw_claims)
         VALUES ($1, $2, $3, $4)
         ON CONFLICT (provider_code, external_id) DO UPDATE
           SET last_seen_at = now(),
               raw_claims   = EXCLUDED.raw_claims",
    )
    .bind(user_id)
    .bind(&provider)
    .bind(&external_id)
    .bind(&claims_json)
    .execute(&mut *tx)
    .await;

    // Issue tokens + session row (same shape as the password + MFA
    // login paths — Phase B's rotation chain + Phase D's audit log
    // both benefit for free).
    let issued = match insert_session_row(&mut *tx, user_id, user_agent.as_deref(),
                                          state.refresh_ttl_seconds).await {
        Ok(v) => v,
        Err(e) => {
            tracing::error!(error = %e, "sso_callback: session insert");
            return internal_error("session store failed");
        }
    };

    // Fetch user for the response.
    let user = match sqlx::query_as::<_, UserRow>(
        "SELECT id, email, password_hash, display_name, first_name, last_name,
                is_global_admin, mfa_enabled
           FROM secure_auth.users
          WHERE id = $1",
    )
    .bind(user_id)
    .fetch_one(&mut *tx)
    .await
    {
        Ok(u) => u,
        Err(e) => {
            tracing::error!(error = %e, "sso_callback: user refetch");
            return internal_error("database error");
        }
    };

    let access_token = match sign_access_token(&state, user.id, &user.email, issued.session_id) {
        Ok(t) => t,
        Err(e) => {
            tracing::error!(error = %e, "sso_callback: jwt sign");
            return internal_error("token encode failed");
        }
    };

    if tx.commit().await.is_err() {
        return internal_error("database error");
    }
    let _ = sqlx::query("UPDATE secure_auth.users SET last_login_at = now() WHERE id = $1")
        .bind(user_id).execute(&state.pool).await;
    // Log the SSO success through the same ledger as password logins
    // so the audit view stays unified.
    log_login_attempt(&state.pool, &user.email, user_agent.as_deref(),
                      true, Some(&format!("sso:{}", provider))).await;

    let roles = if user.is_global_admin { vec!["global_admin".into()] } else { vec!["user".into()] };
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

/// Phase C.1 Duo scaffold — start endpoint. Real Duo Universal Prompt
/// wiring lands in Phase 5.A of the IDP buildout (see
/// docs/IDP_BUILDOUT.md) — until then returns a 501 so the route is
/// visible + clients can render an accurate "Duo not configured"
/// error instead of a 404.
async fn mfa_duo_start() -> impl IntoResponse {
    not_implemented(
        "Duo Universal Prompt wiring lands in Phase 5.A of the IDP buildout \
         (see docs/IDP_BUILDOUT.md). Migration 115 stages the schema.",
    )
}

/// Phase C.1 Duo scaffold — callback endpoint. Paired with
/// `mfa_duo_start` above; same status until Phase 5.A.
async fn mfa_duo_callback() -> impl IntoResponse {
    not_implemented(
        "Duo Universal Prompt wiring lands in Phase 5.A of the IDP buildout \
         (see docs/IDP_BUILDOUT.md). Migration 115 stages the schema.",
    )
}

#[derive(Debug, Deserialize)]
struct SsoWindowsCallback {
    /// Domain username in either `DOMAIN\user` or just `user` form.
    /// Case-insensitive; we lowercase before lookup.
    domain_username: String,

    /// Optional diagnostic. Included in the user_external_identities
    /// last-seen claim blob + the login_attempts audit row.
    #[serde(default)]
    machine_name: Option<String>,
}

/// Dedicated Windows-SSO bridge for the desktop shell (Phase 2 of
/// the IDP buildout — see docs/IDP_BUILDOUT.md). Desktop's
/// App.xaml.cs posts { domain_username } after resolving
/// Environment.UserName; we look up the corresponding
/// user_external_identities row (provider_code='windows') + issue a
/// JWT the client uses for every Central.Api + auth-service call
/// from then on.
///
/// No state nonce, no code-exchange dance: the desktop isn't doing
/// a browser redirect, so there's no CSRF surface to mitigate.
/// Instead this endpoint MUST be IP-restricted in production
/// (Phase 10 deployment work) to known desktop subnets — anyone who
/// can POST here can log in as any AD-linked user. For local dev +
/// single-machine deployments that's acceptable.
///
/// Returns 401 when:
///   - No user_external_identities row matches the username.
///   - The matched user has deleted_at set.
///   - The user has is_active=false on app_users / secure_auth.users.
async fn sso_windows_callback(
    State(state): State<Arc<AppState>>,
    headers: HeaderMap,
    Json(req): Json<SsoWindowsCallback>,
) -> impl IntoResponse {
    let user_agent = headers
        .get("user-agent")
        .and_then(|v| v.to_str().ok())
        .map(|s| s.to_owned());

    // Strip the DOMAIN\ prefix if present. Desktop may send either
    // "CORP\corys" or just "corys"; the user_external_identities
    // external_id is just the lowered username.
    let raw  = req.domain_username.trim();
    let bare = raw.rsplit('\\').next().unwrap_or(raw);
    let normalised = bare.to_lowercase();

    if normalised.is_empty() {
        return (
            StatusCode::BAD_REQUEST,
            Json(serde_json::json!({
                "error":  "bad_request",
                "detail": "domain_username required",
            })),
        ).into_response();
    }

    // Transaction so the user_external_identities upsert + session
    // insert land atomically.
    let mut tx = match state.pool.begin().await {
        Ok(t) => t,
        Err(e) => {
            tracing::error!(error = %e, "windows_callback: tx begin");
            return internal_error("database error");
        }
    };

    let user: Option<UserRow> = sqlx::query_as::<_, UserRow>(
        "SELECT u.id, u.email, u.password_hash, u.display_name, u.first_name,
                u.last_name, u.is_global_admin, u.mfa_enabled
           FROM secure_auth.users u
           JOIN secure_auth.user_external_identities uei
             ON uei.user_id = u.id
          WHERE uei.provider_code = 'windows'
            AND uei.external_id  = $1
            AND u.deleted_at IS NULL
            AND u.is_active = true",
    )
    .bind(&normalised)
    .fetch_optional(&mut *tx)
    .await
    .unwrap_or(None);

    let Some(user) = user else {
        // Log the failure to login_attempts so the lockout + audit
        // trail covers Windows-SSO paths too. email column gets the
        // resolved username since there's no email at this stage.
        let _ = sqlx::query(
            "INSERT INTO secure_auth.login_attempts
                 (email, user_agent, succeeded, failure_reason)
             VALUES ($1, $2, false, 'windows_sso_unknown_user')",
        )
        .bind(&normalised)
        .bind(user_agent.as_deref())
        .execute(&mut *tx)
        .await;
        let _ = tx.commit().await;
        return unauthorized();
    };

    // Update last_seen_at + raw_claims on the external identity.
    let _ = sqlx::query(
        "UPDATE secure_auth.user_external_identities
            SET last_seen_at = now(),
                raw_claims   = raw_claims || $3
          WHERE provider_code = 'windows' AND external_id = $1 AND user_id = $2",
    )
    .bind(&normalised)
    .bind(user.id)
    .bind(serde_json::json!({
        "last_machine_name": req.machine_name,
        "last_domain_username_raw": raw,
    }))
    .execute(&mut *tx)
    .await;

    // Issue tokens (reuses the Phase B / E.1 path).
    let issued = match insert_session_row(&mut *tx, user.id, user_agent.as_deref(),
                                          state.refresh_ttl_seconds).await {
        Ok(v) => v,
        Err(e) => {
            tracing::error!(error = %e, "windows_callback: session insert");
            return internal_error("session store failed");
        }
    };
    let access_token = match sign_access_token(&state, user.id, &user.email, issued.session_id) {
        Ok(t) => t,
        Err(e) => {
            tracing::error!(error = %e, "windows_callback: jwt sign");
            return internal_error("token encode failed");
        }
    };

    if tx.commit().await.is_err() {
        return internal_error("database error");
    }

    // last_login_at stamp + login_attempts audit entry.
    let _ = sqlx::query("UPDATE secure_auth.users SET last_login_at = now() WHERE id = $1")
        .bind(user.id).execute(&state.pool).await;
    log_login_attempt(&state.pool, &user.email, user_agent.as_deref(),
                      true, Some("windows_sso")).await;

    let roles = if user.is_global_admin { vec!["global_admin".into()] } else { vec!["user".into()] };
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

async fn fetch_provider(pool: &PgPool, code: &str) -> Option<ProviderRow> {
    sqlx::query_as::<_, ProviderRow>(
        "SELECT provider_code, kind, display_name
           FROM secure_auth.identity_providers
          WHERE provider_code = $1
            AND enabled = true
            AND deleted_at IS NULL",
    )
    .bind(code)
    .fetch_optional(pool)
    .await
    .ok()
    .flatten()
}

async fn fetch_provider_tx(
    tx: &mut sqlx::PgConnection,
    code: &str,
) -> Option<ProviderRow> {
    sqlx::query_as::<_, ProviderRow>(
        "SELECT provider_code, kind, display_name
           FROM secure_auth.identity_providers
          WHERE provider_code = $1
            AND enabled = true
            AND deleted_at IS NULL",
    )
    .bind(code)
    .fetch_optional(tx)
    .await
    .ok()
    .flatten()
}

fn not_implemented(detail: &'static str) -> axum::response::Response {
    (
        StatusCode::NOT_IMPLEMENTED,
        Json(serde_json::json!({
            "error":  "not_implemented",
            "detail": detail,
        })),
    ).into_response()
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

#[derive(Debug, sqlx::FromRow)]
struct ChallengeRow {
    #[allow(dead_code)]   // id is echoed into structured logs in Phase G
    id:               Uuid,
    user_id:          Uuid,
    expires_at:       chrono::DateTime<chrono::Utc>,
    consumed_at:      Option<chrono::DateTime<chrono::Utc>>,
    failed_attempts:  i32,
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
