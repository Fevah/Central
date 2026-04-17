// HTTP API for manual triggers + status
use axum::{Router, routing::{get, post}, extract::{State, Path}, Json, http::StatusCode};
use serde::{Deserialize, Serialize};
use sqlx::PgPool;
use uuid::Uuid;

pub fn build_router(pool: PgPool) -> Router {
    Router::new()
        .route("/health", get(|| async { "ok" }))
        .route("/provision/:tenant_id", post(provision_handler))
        .route("/decommission/:tenant_id", post(decommission_handler))
        .route("/status/:tenant_id", get(status_handler))
        .route("/jobs", get(list_jobs))
        .with_state(pool)
}

#[derive(Serialize, Deserialize)]
struct JobResponse { job_id: i64, status: String }

#[derive(Serialize)]
struct TenantStatus {
    tenant_id: Uuid,
    sizing_model: String,
    provisioning_status: String,
    database_name: Option<String>,
    namespace: Option<String>,
    recent_jobs: Vec<JobSummary>,
}

#[derive(Serialize, sqlx::FromRow)]
struct JobSummary {
    id: i64,
    job_type: String,
    status: String,
    started_at: Option<chrono::DateTime<chrono::Utc>>,
    completed_at: Option<chrono::DateTime<chrono::Utc>>,
    error_message: Option<String>,
}

async fn provision_handler(
    State(pool): State<PgPool>,
    Path(tenant_id): Path<Uuid>,
) -> Result<Json<JobResponse>, (StatusCode, String)> {
    let slug: Option<String> = sqlx::query_scalar("SELECT slug FROM central_platform.tenants WHERE id = $1")
        .bind(tenant_id).fetch_optional(&pool).await
        .map_err(|e| (StatusCode::INTERNAL_SERVER_ERROR, e.to_string()))?;
    let Some(slug) = slug else { return Err((StatusCode::NOT_FOUND, "tenant not found".into())); };

    let slug_safe = regex::Regex::new(r"[^a-zA-Z0-9_]").unwrap().replace_all(&slug, "_");
    let slug_dash = regex::Regex::new(r"[^a-zA-Z0-9-]").unwrap().replace_all(&slug, "-");
    let payload = serde_json::json!({
        "target_database": format!("central_{slug_safe}"),
        "target_namespace": format!("central-{slug_dash}"),
    });

    let job_id: i64 = sqlx::query_scalar(
        "INSERT INTO central_platform.provisioning_jobs (tenant_id, job_type, payload) VALUES ($1, 'provision_dedicated', $2) RETURNING id")
        .bind(tenant_id).bind(payload).fetch_one(&pool).await
        .map_err(|e| (StatusCode::INTERNAL_SERVER_ERROR, e.to_string()))?;

    Ok(Json(JobResponse { job_id, status: "queued".into() }))
}

async fn decommission_handler(
    State(pool): State<PgPool>,
    Path(tenant_id): Path<Uuid>,
) -> Result<Json<JobResponse>, (StatusCode, String)> {
    let job_id: i64 = sqlx::query_scalar(
        "INSERT INTO central_platform.provisioning_jobs (tenant_id, job_type) VALUES ($1, 'decommission_dedicated') RETURNING id")
        .bind(tenant_id).fetch_one(&pool).await
        .map_err(|e| (StatusCode::INTERNAL_SERVER_ERROR, e.to_string()))?;
    Ok(Json(JobResponse { job_id, status: "queued".into() }))
}

async fn status_handler(
    State(pool): State<PgPool>,
    Path(tenant_id): Path<Uuid>,
) -> Result<Json<TenantStatus>, (StatusCode, String)> {
    let row: Option<(String, String, Option<String>, Option<String>)> = sqlx::query_as(
        r#"SELECT t.sizing_model, t.provisioning_status, m.database_name, m.k8s_namespace
           FROM central_platform.tenants t
           LEFT JOIN central_platform.tenant_connection_map m ON m.tenant_id = t.id
           WHERE t.id = $1"#)
        .bind(tenant_id).fetch_optional(&pool).await
        .map_err(|e| (StatusCode::INTERNAL_SERVER_ERROR, e.to_string()))?;

    let Some((sizing_model, provisioning_status, database_name, namespace)) = row
        else { return Err((StatusCode::NOT_FOUND, "tenant not found".into())); };

    let jobs: Vec<JobSummary> = sqlx::query_as(
        "SELECT id, job_type, status, started_at, completed_at, error_message
         FROM central_platform.provisioning_jobs
         WHERE tenant_id = $1 ORDER BY created_at DESC LIMIT 10")
        .bind(tenant_id).fetch_all(&pool).await.unwrap_or_default();

    Ok(Json(TenantStatus {
        tenant_id, sizing_model, provisioning_status,
        database_name, namespace, recent_jobs: jobs,
    }))
}

async fn list_jobs(State(pool): State<PgPool>) -> Result<Json<Vec<JobSummary>>, (StatusCode, String)> {
    let jobs: Vec<JobSummary> = sqlx::query_as(
        "SELECT id, job_type, status, started_at, completed_at, error_message
         FROM central_platform.provisioning_jobs ORDER BY created_at DESC LIMIT 100")
        .fetch_all(&pool).await
        .map_err(|e| (StatusCode::INTERNAL_SERVER_ERROR, e.to_string()))?;
    Ok(Json(jobs))
}
