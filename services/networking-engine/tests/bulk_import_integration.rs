//! Live-DB integration tests for the bulk import apply path.
//!
//! Same opt-in harness as `config_gen_integration.rs`:
//!
//! ```sh
//! export TEST_DATABASE_URL="postgresql://central:central@192.168.56.201:5432/central_test"
//! cargo test --test bulk_import_integration -- --ignored --test-threads=1
//! ```
//!
//! When `TEST_DATABASE_URL` is unset, every test logs a clear skip
//! message and returns `Ok(())` — keeps `cargo test --ignored` safe
//! on machines without a live DB.

use networking_engine::bulk_import;
use sqlx::{PgPool, postgres::PgPoolOptions};
use std::env;
use uuid::Uuid;

async fn pool_or_skip(test_name: &str) -> Option<PgPool> {
    let Ok(dsn) = env::var("TEST_DATABASE_URL") else {
        eprintln!("[{test_name}] skipped: TEST_DATABASE_URL not set");
        return None;
    };
    match PgPoolOptions::new().max_connections(4).connect(&dsn).await {
        Ok(p) => Some(p),
        Err(e) => {
            eprintln!("[{test_name}] skipped: couldn't connect to TEST_DATABASE_URL: {e}");
            None
        }
    }
}

/// Per-test tenant fixture — enough rows to satisfy the FKs that bulk
/// import resolves (device_role_id → net.device_role, building_id →
/// net.building). Tenant UUID is fresh per test so parallel runs
/// don't stomp each other's data.
struct TenantFixture {
    pool: PgPool,
    org_id: Uuid,
}

impl TenantFixture {
    async fn new(pool: PgPool) -> sqlx::Result<Self> {
        let org_id = Uuid::new_v4();
        sqlx::query(
            "INSERT INTO central_platform.tenants (id, slug, display_name, status)
             VALUES ($1, $2, $2, 'Active')
             ON CONFLICT (id) DO NOTHING")
            .bind(org_id).bind(format!("bi-itest-{org_id}"))
            .execute(&pool).await?;

        // Minimal hierarchy — one region, one site, one building.
        let region_id: (Uuid,) = sqlx::query_as(
            "INSERT INTO net.region (organization_id, region_code, display_name, status)
             VALUES ($1, 'IT-R', 'BI Region', 'Active') RETURNING id")
            .bind(org_id).fetch_one(&pool).await?;
        let site_id: (Uuid,) = sqlx::query_as(
            "INSERT INTO net.site (organization_id, region_id, site_code, display_name,
                                   city, country, timezone, site_number, status)
             VALUES ($1, $2, 'IT-S', 'BI Site', 'ITest', 'UK', 'UTC', 1, 'Active')
             RETURNING id")
            .bind(org_id).bind(region_id.0).fetch_one(&pool).await?;
        sqlx::query(
            "INSERT INTO net.building (organization_id, site_id, building_code,
                                       display_name, building_number, status)
             VALUES ($1, $2, 'IT-B', 'BI Building', '1', 'Active')")
            .bind(org_id).bind(site_id.0).execute(&pool).await?;

        // One device_role so role_code='Core' resolves on apply.
        sqlx::query(
            "INSERT INTO net.device_role (organization_id, role_code, display_name,
                                          naming_template, status)
             VALUES ($1, 'Core', 'BI Core', '{building_code}-CORE{instance}', 'Active')")
            .bind(org_id).execute(&pool).await?;

        Ok(Self { pool, org_id })
    }

    async fn count_devices(&self) -> i64 {
        let (n,): (i64,) = sqlx::query_as(
            "SELECT COUNT(*) FROM net.device
              WHERE organization_id = $1 AND deleted_at IS NULL")
            .bind(self.org_id).fetch_one(&self.pool).await.unwrap();
        n
    }
}

impl Drop for TenantFixture {
    fn drop(&mut self) {
        let pool = self.pool.clone();
        let org_id = self.org_id;
        tokio::spawn(async move {
            let _ = sqlx::query("DELETE FROM central_platform.tenants WHERE id = $1")
                .bind(org_id).execute(&pool).await;
        });
    }
}

const HEADER: &str = "hostname,role_code,building_code,site_code,management_ip,asn,status,version\r\n";

// ─── Tests ───────────────────────────────────────────────────────────────

#[tokio::test]
#[ignore]
async fn dry_run_returns_outcomes_without_writing() {
    let Some(pool) = pool_or_skip("dry_run_returns_outcomes_without_writing").await else { return; };
    let fx = TenantFixture::new(pool).await.expect("fixture");
    assert_eq!(fx.count_devices().await, 0, "precondition: no devices");

    let csv = format!("{HEADER}IT-B-NEW01,Core,IT-B,IT-S,10.99.0.1/24,65100,Active,1\r\n");
    let result = bulk_import::import_devices(
        &fx.pool, fx.org_id, &csv, /*dry_run=*/true, None,
    ).await.expect("dry run");

    assert_eq!(result.total_rows, 1);
    assert_eq!(result.valid, 1);
    assert_eq!(result.invalid, 0);
    assert!(result.dry_run);
    assert!(!result.applied, "dry-run must not set applied=true");
    assert_eq!(fx.count_devices().await, 0,
        "dry-run must not touch the DB: got {} devices after dry-run",
        fx.count_devices().await);
}

#[tokio::test]
#[ignore]
async fn apply_happy_path_inserts_all_rows_and_commits() {
    let Some(pool) = pool_or_skip("apply_happy_path_inserts_all_rows_and_commits").await else { return; };
    let fx = TenantFixture::new(pool).await.expect("fixture");

    let csv = format!(
        "{HEADER}\
         IT-B-CORE01,Core,IT-B,IT-S,10.99.0.1/24,65100,Active,1\r\n\
         IT-B-CORE02,Core,IT-B,IT-S,10.99.0.2/24,65101,Active,1\r\n");
    let result = bulk_import::import_devices(
        &fx.pool, fx.org_id, &csv, /*dry_run=*/false, Some(7),
    ).await.expect("apply");

    assert_eq!(result.total_rows, 2);
    assert_eq!(result.valid, 2);
    assert_eq!(result.invalid, 0);
    assert!(!result.dry_run);
    assert!(result.applied, "happy-path apply must set applied=true");
    assert_eq!(fx.count_devices().await, 2,
        "both devices must be persisted on happy-path apply");
}

#[tokio::test]
#[ignore]
async fn apply_rolls_back_on_any_invalid_row() {
    let Some(pool) = pool_or_skip("apply_rolls_back_on_any_invalid_row").await else { return; };
    let fx = TenantFixture::new(pool).await.expect("fixture");

    // Row 1 is valid; row 2 has a bad role — validation fails, apply
    // MUST NOT write row 1.
    let csv = format!(
        "{HEADER}\
         IT-B-CORE01,Core,IT-B,IT-S,10.99.0.1/24,65100,Active,1\r\n\
         IT-B-BAD01,UnknownRole,IT-B,IT-S,10.99.0.2/24,65101,Active,1\r\n");
    let result = bulk_import::import_devices(
        &fx.pool, fx.org_id, &csv, /*dry_run=*/false, None,
    ).await.expect("apply call succeeds even though validation fails");

    assert_eq!(result.total_rows, 2);
    assert_eq!(result.valid, 1);
    assert_eq!(result.invalid, 1);
    assert!(!result.applied,
        "any-invalid must result in applied=false");
    assert_eq!(fx.count_devices().await, 0,
        "no rows must be persisted when any row is invalid: got {}",
        fx.count_devices().await);
}

#[tokio::test]
#[ignore]
async fn apply_rejects_existing_hostname_as_row_error() {
    let Some(pool) = pool_or_skip("apply_rejects_existing_hostname_as_row_error").await else { return; };
    let fx = TenantFixture::new(pool).await.expect("fixture");

    // Pre-seed a device so the import row hits "already exists".
    let csv1 = format!("{HEADER}IT-B-CORE01,Core,IT-B,IT-S,10.99.0.1/24,65100,Active,1\r\n");
    bulk_import::import_devices(&fx.pool, fx.org_id, &csv1, false, None)
        .await.expect("seed apply");
    assert_eq!(fx.count_devices().await, 1);

    // Re-import the same hostname — should be flagged as an existing
    // row and NOT applied.
    let result = bulk_import::import_devices(&fx.pool, fx.org_id, &csv1, false, None)
        .await.expect("re-apply");
    assert_eq!(result.invalid, 1);
    assert!(!result.applied);
    assert!(result.outcomes[0].errors.iter().any(|e| e.contains("already exists")),
        "outcome should explain the duplicate: {:?}", result.outcomes[0].errors);
    assert_eq!(fx.count_devices().await, 1,
        "no new device should have been added");
}
