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
use networking_engine::scope_grants::{CreateScopeGrantBody, ScopeGrantRepo};
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

// ─── RBAC enforcement ────────────────────────────────────────────────────
//
// bulk_edit_devices enforces `write` on Device for every target in
// the batch when a user_id is passed. Service-to-service calls
// (no user_id) bypass — preserves backward compat during the RBAC
// rollout.

#[tokio::test]
#[ignore]
async fn bulk_edit_bypasses_rbac_when_no_user_id_passed() {
    // No user_id = service call; shouldn't require any grants.
    let Some(pool) = pool_or_skip("bulk_edit_bypasses_rbac_when_no_user_id_passed").await else { return; };
    let fx = TwoDeviceFixture::new(pool).await.expect("fixture");

    let req = BulkEditDevicesBody {
        device_ids: vec![fx.device_a],
        field: "status".into(),
        value: "Active".into(),
    };
    let result = bulk_edit::bulk_edit_devices(&fx.pool, fx.org_id, &req, false, None)
        .await.expect("service-call bypass");
    assert!(result.applied, "no-user-id calls must bypass RBAC entirely");
    assert_eq!(fx.status_of(fx.device_a).await, "Active");
}

#[tokio::test]
#[ignore]
async fn bulk_edit_forbidden_when_user_lacks_write_grant() {
    let Some(pool) = pool_or_skip("bulk_edit_forbidden_when_user_lacks_write_grant").await else { return; };
    let fx = TwoDeviceFixture::new(pool).await.expect("fixture");

    // user_id 42 has no grants — should be denied with a 403.
    let req = BulkEditDevicesBody {
        device_ids: vec![fx.device_a],
        field: "status".into(),
        value: "Active".into(),
    };
    let err = bulk_edit::bulk_edit_devices(&fx.pool, fx.org_id, &req, false, Some(42))
        .await.unwrap_err().to_string();
    assert!(err.contains("Forbidden"), "err should be a Forbidden variant: {err}");
    assert_eq!(fx.status_of(fx.device_a).await, "Planned",
        "denied call must not touch DB");
}

#[tokio::test]
#[ignore]
async fn bulk_edit_allowed_with_global_write_grant() {
    let Some(pool) = pool_or_skip("bulk_edit_allowed_with_global_write_grant").await else { return; };
    let fx = TwoDeviceFixture::new(pool).await.expect("fixture");

    // Seed a Global write:Device grant for user 42.
    ScopeGrantRepo::new(fx.pool.clone()).create(&CreateScopeGrantBody {
        organization_id: fx.org_id,
        user_id: 42,
        action: "write".into(),
        entity_type: "Device".into(),
        scope_type: "Global".into(),
        scope_entity_id: None,
        notes: None,
    }, Some(99)).await.expect("global grant");

    let req = BulkEditDevicesBody {
        device_ids: vec![fx.device_a, fx.device_b],
        field: "status".into(),
        value: "Active".into(),
    };
    let result = bulk_edit::bulk_edit_devices(&fx.pool, fx.org_id, &req, false, Some(42))
        .await.expect("applies with grant");
    assert!(result.applied);
    assert_eq!(fx.status_of(fx.device_a).await, "Active");
    assert_eq!(fx.status_of(fx.device_b).await, "Active");
}

#[tokio::test]
#[ignore]
async fn bulk_edit_forbidden_when_one_target_outside_scoped_grant() {
    // EntityId grant on device_a only; batch tries to touch both
    // devices → whole batch forbidden (all-or-nothing).
    let Some(pool) = pool_or_skip("bulk_edit_forbidden_when_one_target_outside_scoped_grant").await else { return; };
    let fx = TwoDeviceFixture::new(pool).await.expect("fixture");

    ScopeGrantRepo::new(fx.pool.clone()).create(&CreateScopeGrantBody {
        organization_id: fx.org_id,
        user_id: 42,
        action: "write".into(),
        entity_type: "Device".into(),
        scope_type: "EntityId".into(),
        scope_entity_id: Some(fx.device_a),
        notes: None,
    }, Some(99)).await.expect("partial grant");

    let req = BulkEditDevicesBody {
        device_ids: vec![fx.device_a, fx.device_b],
        field: "status".into(),
        value: "Active".into(),
    };
    let err = bulk_edit::bulk_edit_devices(&fx.pool, fx.org_id, &req, false, Some(42))
        .await.unwrap_err().to_string();
    assert!(err.contains("Forbidden"));
    // Neither device must be written — grant covered one, but the
    // batch is atomic so the whole thing fails.
    assert_eq!(fx.status_of(fx.device_a).await, "Planned");
    assert_eq!(fx.status_of(fx.device_b).await, "Planned");
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

// ─── VLAN + subnet bulk edit tests ───────────────────────────────────────
//
// Extend the device bulk-edit invariants (whitelist / happy-path /
// missing-id / forbidden / empty-value handling) to VLANs + subnets.

use networking_engine::bulk_edit::{BulkEditVlansBody, BulkEditSubnetsBody};

/// Fixture with 1 tenant + 1 vlan_pool/block + 2 VLANs + 1 ip_pool
/// + 2 subnets. Enough rows to drive bulk-edit happy paths +
/// rollback-on-missing tests for both entity types.
struct VlanSubnetFixture {
    pool: PgPool,
    org_id: Uuid,
    vlan_a: Uuid,
    vlan_b: Uuid,
    subnet_a: Uuid,
    subnet_b: Uuid,
}

impl VlanSubnetFixture {
    async fn new(pool: PgPool) -> sqlx::Result<Self> {
        let org_id = Uuid::new_v4();
        sqlx::query(
            "INSERT INTO central_platform.tenants (id, slug, display_name, status)
             VALUES ($1, $2, $2, 'Active') ON CONFLICT (id) DO NOTHING")
            .bind(org_id).bind(format!("vs-itest-{org_id}"))
            .execute(&pool).await?;

        let vp_id: (Uuid,) = sqlx::query_as(
            "INSERT INTO net.vlan_pool (organization_id, pool_code, display_name,
                                        vlan_first, vlan_last)
             VALUES ($1, 'VS-VP', 'VS pool', 1, 4094) RETURNING id")
            .bind(org_id).fetch_one(&pool).await?;
        let vb_id: (Uuid,) = sqlx::query_as(
            "INSERT INTO net.vlan_block (organization_id, pool_id, block_code, display_name,
                                         vlan_first, vlan_last, scope_level)
             VALUES ($1, $2, 'VS-VB', 'VS block', 1, 4094, 'Free') RETURNING id")
            .bind(org_id).bind(vp_id.0).fetch_one(&pool).await?;
        let vlan_a: (Uuid,) = sqlx::query_as(
            "INSERT INTO net.vlan (organization_id, pool_id, block_id,
                                   vlan_id, display_name, scope_level, status)
             VALUES ($1, $2, $3, 100, 'VLAN-A', 'Free', 'Active') RETURNING id")
            .bind(org_id).bind(vp_id.0).bind(vb_id.0).fetch_one(&pool).await?;
        let vlan_b: (Uuid,) = sqlx::query_as(
            "INSERT INTO net.vlan (organization_id, pool_id, block_id,
                                   vlan_id, display_name, scope_level, status)
             VALUES ($1, $2, $3, 101, 'VLAN-B', 'Free', 'Active') RETURNING id")
            .bind(org_id).bind(vp_id.0).bind(vb_id.0).fetch_one(&pool).await?;

        let ip_id: (Uuid,) = sqlx::query_as(
            "INSERT INTO net.ip_pool (organization_id, pool_code, display_name,
                                      network, address_family)
             VALUES ($1, 'VS-IP', 'VS IP pool', '10.77.0.0/16', 4) RETURNING id")
            .bind(org_id).fetch_one(&pool).await?;
        let subnet_a: (Uuid,) = sqlx::query_as(
            "INSERT INTO net.subnet (organization_id, pool_id, subnet_code, display_name,
                                     network, scope_level, status)
             VALUES ($1, $2, 'VS-SUB-A', 'SA', '10.77.1.0/24', 'Free', 'Active')
             RETURNING id")
            .bind(org_id).bind(ip_id.0).fetch_one(&pool).await?;
        let subnet_b: (Uuid,) = sqlx::query_as(
            "INSERT INTO net.subnet (organization_id, pool_id, subnet_code, display_name,
                                     network, scope_level, status)
             VALUES ($1, $2, 'VS-SUB-B', 'SB', '10.77.2.0/24', 'Free', 'Active')
             RETURNING id")
            .bind(org_id).bind(ip_id.0).fetch_one(&pool).await?;

        Ok(Self { pool, org_id,
                   vlan_a: vlan_a.0, vlan_b: vlan_b.0,
                   subnet_a: subnet_a.0, subnet_b: subnet_b.0 })
    }

    async fn vlan_status(&self, id: Uuid) -> String {
        let (s,): (String,) = sqlx::query_as(
            "SELECT status::text FROM net.vlan WHERE id = $1")
            .bind(id).fetch_one(&self.pool).await.unwrap();
        s
    }

    async fn subnet_display(&self, id: Uuid) -> String {
        let (s,): (String,) = sqlx::query_as(
            "SELECT display_name FROM net.subnet WHERE id = $1")
            .bind(id).fetch_one(&self.pool).await.unwrap();
        s
    }
}

impl Drop for VlanSubnetFixture {
    fn drop(&mut self) {
        let pool = self.pool.clone();
        let org_id = self.org_id;
        tokio::spawn(async move {
            let _ = sqlx::query("DELETE FROM central_platform.tenants WHERE id = $1")
                .bind(org_id).execute(&pool).await;
        });
    }
}

#[tokio::test]
#[ignore]
async fn vlan_bulk_edit_status_updates_every_selected() {
    let Some(pool) = pool_or_skip("vlan_bulk_edit_status_updates_every_selected").await else { return; };
    let fx = VlanSubnetFixture::new(pool).await.expect("fixture");
    assert_eq!(fx.vlan_status(fx.vlan_a).await, "Active");

    let req = BulkEditVlansBody {
        vlan_ids: vec![fx.vlan_a, fx.vlan_b],
        field: "status".into(),
        value: "Retired".into(),
    };
    let result = bulk_edit::bulk_edit_vlans(&fx.pool, fx.org_id, &req, false, None)
        .await.expect("apply");
    assert!(result.applied);
    assert_eq!(fx.vlan_status(fx.vlan_a).await, "Retired");
    assert_eq!(fx.vlan_status(fx.vlan_b).await, "Retired");
}

#[tokio::test]
#[ignore]
async fn vlan_bulk_edit_rejects_non_whitelisted_field() {
    let Some(pool) = pool_or_skip("vlan_bulk_edit_rejects_non_whitelisted_field").await else { return; };
    let fx = VlanSubnetFixture::new(pool).await.expect("fixture");

    let req = BulkEditVlansBody {
        vlan_ids: vec![fx.vlan_a],
        field: "vlan_id".into(),
        value: "999".into(),
    };
    let err = bulk_edit::bulk_edit_vlans(&fx.pool, fx.org_id, &req, false, None)
        .await.unwrap_err().to_string();
    assert!(err.contains("not editable"), "err: {err}");
}

#[tokio::test]
#[ignore]
async fn vlan_bulk_edit_forbidden_without_grant() {
    let Some(pool) = pool_or_skip("vlan_bulk_edit_forbidden_without_grant").await else { return; };
    let fx = VlanSubnetFixture::new(pool).await.expect("fixture");

    let req = BulkEditVlansBody {
        vlan_ids: vec![fx.vlan_a],
        field: "status".into(),
        value: "Retired".into(),
    };
    let err = bulk_edit::bulk_edit_vlans(&fx.pool, fx.org_id, &req, false, Some(42))
        .await.unwrap_err().to_string();
    assert!(err.contains("Forbidden"), "err: {err}");
    assert_eq!(fx.vlan_status(fx.vlan_a).await, "Active");
}

#[tokio::test]
#[ignore]
async fn subnet_bulk_edit_display_name_updates_every_selected() {
    let Some(pool) = pool_or_skip("subnet_bulk_edit_display_name_updates_every_selected").await else { return; };
    let fx = VlanSubnetFixture::new(pool).await.expect("fixture");

    let req = BulkEditSubnetsBody {
        subnet_ids: vec![fx.subnet_a, fx.subnet_b],
        field: "display_name".into(),
        value: "Renamed in bulk".into(),
    };
    let result = bulk_edit::bulk_edit_subnets(&fx.pool, fx.org_id, &req, false, None)
        .await.expect("apply");
    assert!(result.applied);
    assert_eq!(fx.subnet_display(fx.subnet_a).await, "Renamed in bulk");
    assert_eq!(fx.subnet_display(fx.subnet_b).await, "Renamed in bulk");
}

#[tokio::test]
#[ignore]
async fn subnet_bulk_edit_rejects_empty_display_name() {
    let Some(pool) = pool_or_skip("subnet_bulk_edit_rejects_empty_display_name").await else { return; };
    let fx = VlanSubnetFixture::new(pool).await.expect("fixture");

    let req = BulkEditSubnetsBody {
        subnet_ids: vec![fx.subnet_a],
        field: "display_name".into(),
        value: "".into(),
    };
    let err = bulk_edit::bulk_edit_subnets(&fx.pool, fx.org_id, &req, false, None)
        .await.unwrap_err().to_string();
    assert!(err.contains("display_name cannot be empty"), "err: {err}");
    assert_eq!(fx.subnet_display(fx.subnet_a).await, "SA");
}

#[tokio::test]
#[ignore]
async fn subnet_bulk_edit_forbidden_without_grant() {
    let Some(pool) = pool_or_skip("subnet_bulk_edit_forbidden_without_grant").await else { return; };
    let fx = VlanSubnetFixture::new(pool).await.expect("fixture");

    let req = BulkEditSubnetsBody {
        subnet_ids: vec![fx.subnet_a],
        field: "status".into(),
        value: "Deprecated".into(),
    };
    let err = bulk_edit::bulk_edit_subnets(&fx.pool, fx.org_id, &req, false, Some(42))
        .await.unwrap_err().to_string();
    assert!(err.contains("Forbidden"), "err: {err}");
}
