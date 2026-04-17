// Job queue worker — polls central_platform.provisioning_jobs
use anyhow::Result;
use sqlx::PgPool;
use std::time::Duration;

pub async fn run_worker(pool: PgPool, pg_admin_dsn: String) {
    tracing::info!("queue worker starting");
    loop {
        match pick_and_run_one(&pool, &pg_admin_dsn).await {
            Ok(true)  => continue,                                 // Picked a job, try next immediately
            Ok(false) => tokio::time::sleep(Duration::from_secs(5)).await,  // No work
            Err(e)    => {
                tracing::error!(?e, "worker iteration failed");
                tokio::time::sleep(Duration::from_secs(10)).await;
            }
        }
    }
}

async fn pick_and_run_one(pool: &PgPool, pg_admin_dsn: &str) -> Result<bool> {
    // Atomic pick-next using UPDATE ... RETURNING
    let row = sqlx::query_as::<_, (i64, uuid::Uuid, String, serde_json::Value)>(
        r#"UPDATE central_platform.provisioning_jobs
           SET status = 'running', started_at = NOW()
           WHERE id = (
             SELECT id FROM central_platform.provisioning_jobs
             WHERE status = 'queued'
               AND (next_retry_at IS NULL OR next_retry_at <= NOW())
             ORDER BY created_at
             FOR UPDATE SKIP LOCKED
             LIMIT 1
           )
           RETURNING id, tenant_id, job_type, payload"#,
    )
    .fetch_optional(pool)
    .await?;

    let Some((id, tenant_id, job_type, payload)) = row else { return Ok(false); };
    tracing::info!(job_id = id, %tenant_id, job_type = %job_type, "job picked up");

    let result = match job_type.as_str() {
        "provision_dedicated" => crate::provisioner::provision_dedicated(pool, pg_admin_dsn, tenant_id, &payload).await,
        "decommission_dedicated" => crate::provisioner::decommission_dedicated(pool, pg_admin_dsn, tenant_id, &payload).await,
        "resize" => crate::provisioner::resize(pool, tenant_id, &payload).await,
        other => Err(anyhow::anyhow!("unknown job type: {other}")),
    };

    match result {
        Ok(()) => {
            sqlx::query("UPDATE central_platform.provisioning_jobs SET status = 'completed', completed_at = NOW() WHERE id = $1")
                .bind(id).execute(pool).await?;
            tracing::info!(job_id = id, "job completed");
        }
        Err(e) => {
            let retry_count: i32 = sqlx::query_scalar("SELECT retry_count FROM central_platform.provisioning_jobs WHERE id = $1")
                .bind(id).fetch_one(pool).await.unwrap_or(0);
            let new_status = if retry_count >= 3 { "failed" } else { "queued" };
            let next_retry = if retry_count >= 3 { None } else {
                Some(chrono::Utc::now() + chrono::Duration::seconds(30i64 * (2i64.pow(retry_count as u32))))
            };
            sqlx::query(r#"UPDATE central_platform.provisioning_jobs
                SET status = $2, retry_count = retry_count + 1, next_retry_at = $3, error_message = $4
                WHERE id = $1"#)
                .bind(id).bind(new_status).bind(next_retry).bind(format!("{e}"))
                .execute(pool).await?;
            tracing::error!(job_id = id, ?e, "job failed");
        }
    }
    Ok(true)
}
