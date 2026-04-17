// Tenant Provisioner Service
//
// Watches the `central_platform.provisioning_jobs` queue and provisions/decommissions
// dedicated databases + K8s namespaces for enterprise tenants.
//
// Flow for provision_dedicated:
//   1. Pick up queued job
//   2. CREATE DATABASE central_<slug> on the shared cluster
//   3. pg_dump the tenant's zoned schema to a file
//   4. Restore the dump into the new database
//   5. Run migration job against the new database (schema completeness)
//   6. Create K8s namespace `central-<slug>` with NetworkPolicy + RBAC
//   7. Update central_platform.tenant_connection_map to point to new DB
//   8. Mark job completed, tenant.provisioning_status = 'ready'
//
// Flow for decommission_dedicated:
//   1. pg_dump final backup of the dedicated DB
//   2. Copy data back into the zoned schema (or archive and delete)
//   3. Drop database
//   4. Delete K8s namespace
//   5. Update tenant_connection_map

mod api;
mod provisioner;
mod queue;
mod db;
mod k8s_ops;

use anyhow::Result;
use std::env;
use tracing_subscriber::{EnvFilter, fmt};

#[tokio::main]
async fn main() -> Result<()> {
    fmt()
        .with_env_filter(EnvFilter::try_from_default_env()
            .unwrap_or_else(|_| EnvFilter::new("info")))
        .json()
        .init();

    let dsn = env::var("DATABASE_URL")
        .expect("DATABASE_URL required (platform database connection)");
    let pg_admin_dsn = env::var("PG_ADMIN_URL")
        .expect("PG_ADMIN_URL required (superuser connection for CREATE DATABASE)");
    let bind = env::var("BIND_ADDR").unwrap_or_else(|_| "0.0.0.0:8090".into());

    tracing::info!("tenant-provisioner starting on {bind}");

    // Connect to platform DB for job queue
    let pool = sqlx::postgres::PgPoolOptions::new()
        .max_connections(10)
        .connect(&dsn)
        .await?;

    // Spawn queue worker
    let queue_pool = pool.clone();
    let queue_admin = pg_admin_dsn.clone();
    tokio::spawn(async move {
        queue::run_worker(queue_pool, queue_admin).await;
    });

    // HTTP API for manual triggers + status
    let app = api::build_router(pool);
    let listener = tokio::net::TcpListener::bind(&bind).await?;
    axum::serve(listener, app).await?;
    Ok(())
}
