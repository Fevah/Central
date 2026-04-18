//! Engine error types. Port of `libs/persistence/Net/AllocationExceptions.cs` plus a few
//! infrastructural errors (DB, bad input). All map to HTTP status via [`axum::response::IntoResponse`].

use axum::{http::StatusCode, response::{IntoResponse, Response}, Json};
use serde::Serialize;
use thiserror::Error;
use uuid::Uuid;

#[derive(Debug, Error)]
pub enum EngineError {
    #[error("Pool exhausted: no free {resource} available in container {container_id}. \
             Either the pool is fully used, or all unused values are still within their cool-down window.")]
    PoolExhausted { resource: String, container_id: Uuid },

    #[error("Allocation container not found: {resource} {container_id} is missing, deleted, \
             or belongs to a different tenant.")]
    ContainerNotFound { resource: String, container_id: Uuid },

    #[error("Allocation out of range: {resource} {value} is outside the container range [{first}, {last}].")]
    RangeViolation { resource: String, value: i64, first: i64, last: i64 },

    #[error("Invalid CIDR: '{0}'")]
    BadCidr(String),

    #[error("Server profile {0} not found or not in caller's tenant.")]
    ServerProfileNotFound(Uuid),

    #[error("Bad request: {0}")]
    BadRequest(String),

    /// Lock-state trigger rejected the write. `message` carries the
    /// trigger's RAISE EXCEPTION text so the UI can surface exactly
    /// what the DB said.
    #[error("Lock violation: {message}")]
    LockViolation { message: String },

    #[error("Database error: {0}")]
    Db(sqlx::Error),
}

impl From<sqlx::Error> for EngineError {
    /// Promote specific Postgres error codes to typed variants before
    /// falling through to the generic `Db` bucket. Today the only code we
    /// recognise is `check_violation` (23514) emitted by the lock-state
    /// trigger in migration 100 — we match on that plus a substring of
    /// the trigger's message text so we don't hijack unrelated CHECK
    /// constraints from schema `CHECK (...)` clauses.
    fn from(e: sqlx::Error) -> Self {
        if let sqlx::Error::Database(ref db) = e {
            if db.code().as_deref() == Some("23514") {
                let msg = db.message();
                if msg.contains("lock_state")
                    || msg.contains("Immutable")
                    || msg.contains("HardLock")
                {
                    return EngineError::LockViolation { message: msg.to_string() };
                }
            }
        }
        EngineError::Db(e)
    }
}

impl EngineError {
    pub fn pool_exhausted(resource: impl Into<String>, container_id: Uuid) -> Self {
        Self::PoolExhausted { resource: resource.into(), container_id }
    }

    pub fn container_not_found(resource: impl Into<String>, container_id: Uuid) -> Self {
        Self::ContainerNotFound { resource: resource.into(), container_id }
    }

    pub fn range_violation(resource: impl Into<String>, value: i64, first: i64, last: i64) -> Self {
        Self::RangeViolation { resource: resource.into(), value, first, last }
    }

    pub fn bad_cidr(s: impl Into<String>) -> Self {
        Self::BadCidr(s.into())
    }

    pub fn bad_request(s: impl Into<String>) -> Self {
        Self::BadRequest(s.into())
    }

    fn status(&self) -> StatusCode {
        match self {
            Self::PoolExhausted { .. } => StatusCode::CONFLICT,
            Self::ContainerNotFound { .. } => StatusCode::NOT_FOUND,
            Self::ServerProfileNotFound(_) => StatusCode::NOT_FOUND,
            Self::RangeViolation { .. } => StatusCode::UNPROCESSABLE_ENTITY,
            Self::BadCidr(_) => StatusCode::UNPROCESSABLE_ENTITY,
            Self::BadRequest(_) => StatusCode::BAD_REQUEST,
            Self::LockViolation { .. } => StatusCode::CONFLICT,
            Self::Db(_) => StatusCode::INTERNAL_SERVER_ERROR,
        }
    }

    fn code(&self) -> &'static str {
        match self {
            Self::PoolExhausted { .. } => "pool_exhausted",
            Self::ContainerNotFound { .. } => "container_not_found",
            Self::ServerProfileNotFound(_) => "server_profile_not_found",
            Self::RangeViolation { .. } => "range_violation",
            Self::BadCidr(_) => "bad_cidr",
            Self::BadRequest(_) => "bad_request",
            Self::LockViolation { .. } => "lock_violation",
            Self::Db(_) => "db_error",
        }
    }
}

#[derive(Serialize)]
struct ErrorBody<'a> {
    error: &'a str,
    message: String,
}

impl IntoResponse for EngineError {
    fn into_response(self) -> Response {
        let status = self.status();
        let body = ErrorBody { error: self.code(), message: self.to_string() };
        (status, Json(body)).into_response()
    }
}
