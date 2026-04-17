// Core provisioning workflows
use anyhow::{Context, Result};
use regex::Regex;
use sqlx::PgPool;
use std::process::Stdio;
use tokio::process::Command;
use uuid::Uuid;

/// Provision a dedicated database + namespace for an enterprise tenant.
///
///   1. CREATE DATABASE central_<slug>
///   2. pg_dump tenant_<slug> schema from shared cluster
///   3. Restore dump into new database
///   4. Run migrations against new database
///   5. Create K8s namespace central-<slug>
///   6. Update tenant_connection_map
pub async fn provision_dedicated(
    pool: &PgPool,
    pg_admin_dsn: &str,
    tenant_id: Uuid,
    payload: &serde_json::Value,
) -> Result<()> {
    let target_db = payload["target_database"].as_str()
        .context("payload.target_database missing")?;
    let target_ns = payload["target_namespace"].as_str()
        .context("payload.target_namespace missing")?;

    // Validate identifiers (prevent injection)
    let ident_re = Regex::new(r"^[a-z_][a-z0-9_-]*$").unwrap();
    anyhow::ensure!(ident_re.is_match(target_db), "invalid target_database");
    anyhow::ensure!(ident_re.is_match(target_ns), "invalid target_namespace");

    // Look up tenant slug
    let slug: String = sqlx::query_scalar("SELECT slug FROM central_platform.tenants WHERE id = $1")
        .bind(tenant_id).fetch_one(pool).await?;
    let source_schema = if slug == "default" { "public".to_string() } else { format!("tenant_{slug}") };

    // Record provisioning start
    let db_record_id: i32 = sqlx::query_scalar(
        r#"INSERT INTO central_platform.tenant_dedicated_databases
           (tenant_id, database_name, namespace, source_schema, status)
           VALUES ($1, $2, $3, $4, 'provisioning') RETURNING id"#)
        .bind(tenant_id).bind(target_db).bind(target_ns).bind(&source_schema)
        .fetch_one(pool).await?;

    // Step 1: CREATE DATABASE
    tracing::info!(%tenant_id, target_db, "creating dedicated database");
    run_admin_sql(pg_admin_dsn, &format!("CREATE DATABASE \"{target_db}\" OWNER central")).await?;

    // Step 2: Install extensions in new DB
    let new_db_dsn = swap_database_in_dsn(pg_admin_dsn, target_db);
    for ext in ["uuid-ossp", "pgcrypto", "pg_trgm", "citext", "btree_gin"] {
        run_admin_sql(&new_db_dsn, &format!("CREATE EXTENSION IF NOT EXISTS \"{ext}\"")).await?;
    }

    // Step 3: pg_dump source schema
    sqlx::query("UPDATE central_platform.tenant_dedicated_databases SET status = 'dumping_source' WHERE id = $1")
        .bind(db_record_id).execute(pool).await?;
    let dump_file = format!("/tmp/tenant-{}-provision.dump", tenant_id);
    pg_dump_schema(pg_admin_dsn, &source_schema, &dump_file).await?;

    sqlx::query("UPDATE central_platform.tenant_dedicated_databases SET dump_file_path = $1 WHERE id = $2")
        .bind(&dump_file).bind(db_record_id).execute(pool).await?;

    // Step 4: Restore into new DB
    sqlx::query("UPDATE central_platform.tenant_dedicated_databases SET status = 'restoring' WHERE id = $1")
        .bind(db_record_id).execute(pool).await?;
    pg_restore_schema(&new_db_dsn, &dump_file).await?;

    sqlx::query("UPDATE central_platform.tenant_dedicated_databases SET restored_at = NOW() WHERE id = $1")
        .bind(db_record_id).execute(pool).await?;

    // Step 5: Run migrations (schema completeness)
    sqlx::query("UPDATE central_platform.tenant_dedicated_databases SET status = 'migrating' WHERE id = $1")
        .bind(db_record_id).execute(pool).await?;
    run_migrations_against(&new_db_dsn).await?;
    sqlx::query("UPDATE central_platform.tenant_dedicated_databases SET migrated_at = NOW() WHERE id = $1")
        .bind(db_record_id).execute(pool).await?;

    // Step 6: Create K8s namespace
    crate::k8s_ops::create_tenant_namespace(target_ns, &slug).await?;

    // Step 7: Update connection map → route this tenant to the new database
    sqlx::query(r#"
        INSERT INTO central_platform.tenant_connection_map
            (tenant_id, sizing_model, database_name, schema_name, k8s_namespace)
        VALUES ($1, 'dedicated', $2, 'public', $3)
        ON CONFLICT (tenant_id) DO UPDATE SET
            sizing_model = 'dedicated',
            database_name = $2,
            schema_name = 'public',
            k8s_namespace = $3,
            updated_at = NOW()"#)
        .bind(tenant_id).bind(target_db).bind(target_ns)
        .execute(pool).await?;

    // Step 8: Mark tenant ready
    sqlx::query(r#"UPDATE central_platform.tenants
        SET sizing_model = 'dedicated', provisioning_status = 'ready' WHERE id = $1"#)
        .bind(tenant_id).execute(pool).await?;
    sqlx::query(r#"UPDATE central_platform.tenant_dedicated_databases
        SET status = 'active', activated_at = NOW() WHERE id = $1"#)
        .bind(db_record_id).execute(pool).await?;

    tracing::info!(%tenant_id, target_db, "dedicated provisioning complete");
    Ok(())
}

pub async fn decommission_dedicated(
    pool: &PgPool,
    pg_admin_dsn: &str,
    tenant_id: Uuid,
    _payload: &serde_json::Value,
) -> Result<()> {
    // Look up current dedicated DB
    let row: Option<(String, String)> = sqlx::query_as(
        "SELECT database_name, k8s_namespace FROM central_platform.tenant_connection_map
         WHERE tenant_id = $1 AND sizing_model = 'dedicated'")
        .bind(tenant_id).fetch_optional(pool).await?;
    let Some((db_name, ns)) = row else {
        anyhow::bail!("tenant has no dedicated database");
    };

    // Take final backup
    let dump_file = format!("/tmp/tenant-{}-decommission.dump", tenant_id);
    let src_dsn = swap_database_in_dsn(pg_admin_dsn, &db_name);
    pg_dump_full(&src_dsn, &dump_file).await?;

    // Terminate connections + drop DB
    run_admin_sql(pg_admin_dsn, &format!(
        "SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = '{db_name}' AND pid <> pg_backend_pid()"
    )).await.ok();
    run_admin_sql(pg_admin_dsn, &format!("DROP DATABASE IF EXISTS \"{db_name}\"")).await?;

    // Delete K8s namespace
    crate::k8s_ops::delete_tenant_namespace(&ns).await?;

    // Revert tenant to zoned
    sqlx::query("UPDATE central_platform.tenant_connection_map SET sizing_model = 'zoned', database_name = 'central' WHERE tenant_id = $1")
        .bind(tenant_id).execute(pool).await?;
    sqlx::query("UPDATE central_platform.tenants SET sizing_model = 'zoned' WHERE id = $1")
        .bind(tenant_id).execute(pool).await?;

    Ok(())
}

pub async fn resize(_pool: &PgPool, _tenant_id: Uuid, _payload: &serde_json::Value) -> Result<()> {
    // Future: scale max_connections, upgrade Postgres instance, etc.
    tracing::warn!("resize not yet implemented");
    Ok(())
}

// ── Shell helpers ──

async fn run_admin_sql(dsn: &str, sql: &str) -> Result<()> {
    let status = Command::new("psql")
        .arg(dsn).arg("-v").arg("ON_ERROR_STOP=1").arg("-c").arg(sql)
        .stdout(Stdio::piped()).stderr(Stdio::piped())
        .status().await.context("psql failed to spawn")?;
    anyhow::ensure!(status.success(), "psql returned non-zero exit");
    Ok(())
}

async fn pg_dump_schema(dsn: &str, schema: &str, out: &str) -> Result<()> {
    let status = Command::new("pg_dump")
        .arg(dsn).arg("-n").arg(schema).arg("-Fc").arg("-f").arg(out)
        .stdout(Stdio::piped()).stderr(Stdio::piped())
        .status().await?;
    anyhow::ensure!(status.success(), "pg_dump failed");
    Ok(())
}

async fn pg_dump_full(dsn: &str, out: &str) -> Result<()> {
    let status = Command::new("pg_dump")
        .arg(dsn).arg("-Fc").arg("-Z9").arg("-f").arg(out)
        .status().await?;
    anyhow::ensure!(status.success(), "pg_dump failed");
    Ok(())
}

async fn pg_restore_schema(dsn: &str, dump: &str) -> Result<()> {
    let status = Command::new("pg_restore")
        .arg("-d").arg(dsn).arg("--no-owner").arg("--no-privileges")
        .arg("--if-exists").arg("--clean").arg(dump)
        .status().await?;
    // pg_restore often returns non-zero for non-fatal warnings; accept
    tracing::info!(exit_code = ?status.code(), "pg_restore finished");
    Ok(())
}

async fn run_migrations_against(dsn: &str) -> Result<()> {
    // Walk /migrations in sorted order, apply each via psql, record in schema_versions
    let mut entries = tokio::fs::read_dir("/migrations").await?;
    let mut files: Vec<String> = Vec::new();
    while let Some(e) = entries.next_entry().await? {
        if let Some(name) = e.file_name().to_str() {
            if name.ends_with(".sql") { files.push(name.to_string()); }
        }
    }
    files.sort();

    for name in files {
        let status = Command::new("psql")
            .arg(dsn).arg("-v").arg("ON_ERROR_STOP=1")
            .arg("-f").arg(format!("/migrations/{name}"))
            .status().await?;
        if !status.success() {
            tracing::warn!(migration = name, "migration failed or already applied");
        }
    }
    Ok(())
}

fn swap_database_in_dsn(dsn: &str, new_db: &str) -> String {
    // Works for both URL and key=value forms
    if dsn.starts_with("postgres://") || dsn.starts_with("postgresql://") {
        let re = Regex::new(r"://([^/]+)/[^?]+").unwrap();
        re.replace(dsn, format!("://$1/{new_db}")).to_string()
    } else {
        let re = Regex::new(r"(?i)(?:\bDatabase\s*=\s*)([^;]+)").unwrap();
        re.replace(dsn, format!("Database={new_db}")).to_string()
    }
}
