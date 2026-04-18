//! Live-DB integration tests for `bulk_edit::bulk_edit_devices`.
//!
//! Same `#[ignore]` opt-in + skip-when-no-env shape as the other
//! integration suites. Run with:
//!
//! ```sh
//! export TEST_DATABASE_URL="postgresql://central:central@192.168.56.201:5432/central_test"
//! cargo test --test bulk_edit_integration -- --ignored --test-threads=1
//! ```

use networking_engine::bulk_edit::{self, BulkEditDevicesBody};
use sqlx::{PgPool, postgres::PgPoolOptions};
use std::env;
use uuid::Uuid;

async fn pool_or_skip(test_name: &str) -> Option<PgPool> {
    let Ok(dsn) = env::var("TEST_DATABASE_URL") else {
        eprintln!("[{test_name}] skipped: TEST_DATABASE_URL not set");
        return None;
    };
    PgPoolOptions::new().max_connections(4).connect(&dsn).await
        .map_err(|e| { eprintln!("[{test_name}] skipped: {e}"); }).ok()
}

/// Fixture — tenant + one building + two devices so a bulk edit
/// across both exercises the happy path + the per-row audit emission.
struct TwoDeviceFixture {
    pool: PgPool,
    org_id: Uuid,
    device_a: Uuid,
    device_b: Uuid,
}

impl TwoDeviceFixture {
    async fn new(pool: PgPool) -> sqlx::Result<Self> {
        let org_id = Uuid::new_v4();
        sqlx::query(
            "INSERT INTO central_platform.tenants (id, slug, display_name, status)
             VALUES ($1, $2, $2, 'Active')
             ON CONFLICT (id) DO NOTHING")
            .bind(org_id).bind(format!("be-itest-{org_id}"))
            .execute(&pool).await?;

        let region_id: (Uuid,) = sqlx::query_as(
            "INSERT INTO net.region (organization_id, region_code, display_name, status)
             VALUES ($1, 'IT-R', 'BE Region', 'Active') RETURNING id")
            .bind(org_id).fetch_one(&pool).await?;
        let site_id: (Uuid,) = sqlx::query_as(
            "INSERT INTO net.site (organization_id, region_id, site_code, display_name,
                                   city, country, timezone, site_number, status)
             VALUES ($1, $2, 'IT-S', 'BE Site', 'IT', 'UK', 'UTC', 1, 'Active')
             RETURNING id")
            .bind(org_id).bind(region_id.0).fetch_one(&pool).await?;
        let building_id: (Uuid,) = sqlx::query_as(
            "INSERT INTO net.building (organization_id, site_id, building_code,
                                       display_name, building_number, status)
             VALUES ($1, $2, 'IT-B', 'BE Building', '1', 'Active')
             RETURNING id")
            .bind(org_id).bind(site_id.0).fetch_one(&pool).await?;

        let role_id: (Uuid,) = sqlx::query_as(
            "INSERT INTO net.device_role (organization_id, role_code, display_name,
                                          naming_template, status)
             VALUES ($1, 'Core', 'BE Core', '{building_code}-CORE{instance}', 'Active')
             RETURNING id")
            .bind(org_id).fetch_one(&pool).await?;

        let device_a: (Uuid,) = sqlx::query_as(
            "INSERT INTO net.device (organization_id, device_role_id, building_id,
                                     hostname, status)
             VALUES ($1, $2, $3, 'IT-B-A', 'Planned') RETURNING id")
            .bind(org_id).bind(role_id.0).bind(building_id.0).fetch_one(&pool).await?;
        let device_b: (Uuid,) = sqlx::query_as(
            "INSERT INTO net.device (organization_id, device_role_id, building_id,
                                     hostname, status)
             VALUES ($1, $2, $3, 'IT-B-B', 'Planned') RETURNING id")
            .bind(org_id).bind(role_id.0).bind(building_id.0).fetch_one(&pool).await?;

        Ok(Self { pool, org_id, device_a: device_a.0, device_b: device_b.0 })
    }

    async fn status_of(&self, id: Uuid) -> String {
        let (s,): (String,) = sqlx::query_as(
            "SELECT status::text FROM net.device WHERE id = $1")
            .bind(id).fetch_one(&self.pool).await.unwrap();
        s
    }
}

impl Drop for TwoDeviceFixture {
    fn drop(&mut self) {
        let pool = self.pool.clone();
        let org_id = self.org_id;
        tokio::spawn(async move {
            let _ = sqlx::query("DELETE FROM central_platform.tenants WHERE id = $1")
                .bind(org_id).execute(&pool).await;
        });
    }
}

// ─── Tests ───────────────────────────────────────────────────────────────

#[tokio::test]
#[ignore]
async fn bulk_edit_dry_run_does_not_mutate() {
    let Some(pool) = pool_or_skip("bulk_edit_dry_run_does_not_mutate").await else { return; };
    let fx = TwoDeviceFixture::new(pool).await.expect("fixture");
    assert_eq!(fx.status_of(fx.device_a).await, "Planned");

    let req = BulkEditDevicesBody {
        device_ids: vec![fx.device_a, fx.device_b],
        field: "status".into(),
        value: "Active".into(),
    };
    let result = bulk_edit::bulk_edit_devices(
        &fx.pool, fx.org_id, &req, /*dry_run=*/true, None,
    ).await.expect("dry run");
    assert_eq!(result.total, 2);
    assert_eq!(result.succeeded, 2);
    assert_eq!(result.failed, 0);
    assert!(!result.applied, "dry-run must not set applied=true");
    assert_eq!(fx.status_of(fx.device_a).await, "Planned",
        "dry-run must not touch DB state");
    assert_eq!(fx.status_of(fx.device_b).await, "Planned");
}

#[tokio::test]
#[ignore]
async fn bulk_edit_happy_path_updates_every_selected_device() {
    let Some(pool) = pool_or_skip("bulk_edit_happy_path_updates_every_selected_device").await else { return; };
    let fx = TwoDeviceFixture::new(pool).await.expect("fixture");

    let req = BulkEditDevicesBody {
        device_ids: vec![fx.device_a, fx.device_b],
        field: "status".into(),
        value: "Active".into(),
    };
    let result = bulk_edit::bulk_edit_devices(
        &fx.pool, fx.org_id, &req, /*dry_run=*/false, Some(7),
    ).await.expect("apply");
    assert_eq!(result.succeeded, 2);
    assert_eq!(result.failed, 0);
    assert!(result.applied);
    assert_eq!(fx.status_of(fx.device_a).await, "Active");
    assert_eq!(fx.status_of(fx.device_b).await, "Active");
}

#[tokio::test]
#[ignore]
async fn bulk_edit_rejects_non_whitelisted_field() {
    let Some(pool) = pool_or_skip("bulk_edit_rejects_non_whitelisted_field").await else { return; };
    let fx = TwoDeviceFixture::new(pool).await.expect("fixture");

    // hostname isn't editable via bulk-edit — gated single-row CRUD
    // by design (a typo at 50-row scale is a Sev-1).
    let req = BulkEditDevicesBody {
        device_ids: vec![fx.device_a],
        field: "hostname".into(),
        value: "IT-B-NEW".into(),
    };
    let err = bulk_edit::bulk_edit_devices(&fx.pool, fx.org_id, &req, false, None)
        .await.unwrap_err().to_string();
    assert!(err.contains("not editable"), "err should explain whitelist: {err}");
    assert_eq!(fx.status_of(fx.device_a).await, "Planned",
        "rejected request must not touch DB state");
}

#[tokio::test]
#[ignore]
async fn bulk_edit_reports_missing_ids_and_rolls_back() {
    let Some(pool) = pool_or_skip("bulk_edit_reports_missing_ids_and_rolls_back").await else { return; };
    let fx = TwoDeviceFixture::new(pool).await.expect("fixture");

    // Real id + bogus id — validation marks the bogus row failed and
    // the whole apply aborts before any row is mutated.
    let bogus = Uuid::new_v4();
    let req = BulkEditDevicesBody {
        device_ids: vec![fx.device_a, bogus],
        field: "status".into(),
        value: "Retired".into(),
    };
    let result = bulk_edit::bulk_edit_devices(&fx.pool, fx.org_id, &req, false, None)
        .await.expect("apply call returns result even with invalid id");
    assert_eq!(result.failed, 1);
    assert!(!result.applied, "any-invalid must set applied=false");
    assert_eq!(fx.status_of(fx.device_a).await, "Planned",
        "device_a must NOT have been updated because the batch failed as a whole");
    let bogus_outcome = result.outcomes.iter().find(|o| o.id == bogus).unwrap();
    assert!(bogus_outcome.error.as_deref().unwrap_or("").contains("not found"),
        "bogus id error should mention 'not found': {bogus_outcome:?}");
}

#[tokio::test]
#[ignore]
async fn bulk_edit_validates_invalid_status_value_before_write() {
    let Some(pool) = pool_or_skip("bulk_edit_validates_invalid_status_value_before_write").await else { return; };
    let fx = TwoDeviceFixture::new(pool).await.expect("fixture");

    let req = BulkEditDevicesBody {
        device_ids: vec![fx.device_a],
        field: "status".into(),
        value: "Decommissioned".into(), // not in the enum
    };
    let err = bulk_edit::bulk_edit_devices(&fx.pool, fx.org_id, &req, false, None)
        .await.unwrap_err().to_string();
    assert!(err.contains("Decommissioned"), "err should quote the bad value: {err}");
    assert_eq!(fx.status_of(fx.device_a).await, "Planned");
}

#[tokio::test]
#[ignore]
async fn bulk_edit_notes_clears_to_null_on_empty_value() {
    let Some(pool) = pool_or_skip("bulk_edit_notes_clears_to_null_on_empty_value").await else { return; };
    let fx = TwoDeviceFixture::new(pool).await.expect("fixture");

    // Seed a notes value
    sqlx::query("UPDATE net.device SET notes = 'initial' WHERE id = $1")
        .bind(fx.device_a).execute(&fx.pool).await.unwrap();

    let req = BulkEditDevicesBody {
        device_ids: vec![fx.device_a],
        field: "notes".into(),
        value: "".into(),     // empty → NULL
    };
    let result = bulk_edit::bulk_edit_devices(&fx.pool, fx.org_id, &req, false, None)
        .await.expect("apply");
    assert!(result.applied);

    let (notes,): (Option<String>,) = sqlx::query_as(
        "SELECT notes FROM net.device WHERE id = $1")
        .bind(fx.device_a).fetch_one(&fx.pool).await.unwrap();
    assert_eq!(notes, None, "empty value must clear notes to NULL");
}
