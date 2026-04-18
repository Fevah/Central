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

    #[error("Database error: {0}")]
    Db(#[from] sqlx::Error),
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
