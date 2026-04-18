//! Live-DB integration tests for `scope_grants`.
//!
//! Same `#[ignore]` + skip-when-no-env pattern as the other suites.
//! Run with:
//!
//! ```sh
//! export TEST_DATABASE_URL="postgresql://central:central@192.168.56.201:5432/central_test"
//! cargo test --test scope_grants_integration -- --ignored --test-threads=1
//! ```

use networking_engine::scope_grants::{self, CreateScopeGrantBody, ListScopeGrantsQuery, ScopeGrantRepo};
use sqlx::{PgPool, postgres::PgPoolOptions};
use std::env;
use uuid::Uuid;

async fn pool_or_skip(test_name: &str) -> Option<PgPool> {
    let Ok(dsn) = env::var("TEST_DATABASE_URL") else {
        eprintln!("[{test_name}] skipped: TEST_DATABASE_URL not set");
        return None;
    };
    PgPoolOptions::new().max_connections(4).connect(&dsn).await
        .map_err(|e| eprintln!("[{test_name}] skipped: {e}")).ok()
}

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
            .bind(org_id).bind(format!("sg-itest-{org_id}"))
            .execute(&pool).await?;
        Ok(Self { pool, org_id })
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

// ─── Tests ───────────────────────────────────────────────────────────────

#[tokio::test]
#[ignore]
async fn create_list_delete_round_trip() {
    let Some(pool) = pool_or_skip("create_list_delete_round_trip").await else { return; };
    let fx = TenantFixture::new(pool).await.expect("fixture");
    let repo = ScopeGrantRepo::new(fx.pool.clone());

    // Create — Global grant.
    let grant = repo.create(&CreateScopeGrantBody {
        organization_id: fx.org_id,
        user_id: 42,
        action: "read".into(),
        entity_type: "Device".into(),
        scope_type: "Global".into(),
        scope_entity_id: None,
        notes: Some("created in integration test".into()),
    }, Some(99)).await.expect("create");
    assert_eq!(grant.action, "read");
    assert_eq!(grant.scope_type, "Global");

    // List — should find the grant we just created.
    let rows = repo.list(&ListScopeGrantsQuery {
        organization_id: fx.org_id,
        user_id: Some(42),
        action: None, entity_type: None,
    }).await.expect("list");
    assert_eq!(rows.len(), 1);
    assert_eq!(rows[0].id, grant.id);

    // Delete — soft-delete, should disappear from the list.
    repo.soft_delete(grant.id, fx.org_id, Some(99)).await.expect("delete");
    let after = repo.list(&ListScopeGrantsQuery {
        organization_id: fx.org_id,
        user_id: Some(42),
        action: None, entity_type: None,
    }).await.expect("list after delete");
    assert!(after.is_empty(), "soft-deleted grant must be filtered out");
}

#[tokio::test]
#[ignore]
async fn has_permission_matches_global_grant() {
    let Some(pool) = pool_or_skip("has_permission_matches_global_grant").await else { return; };
    let fx = TenantFixture::new(pool).await.expect("fixture");
    let repo = ScopeGrantRepo::new(fx.pool.clone());

    repo.create(&CreateScopeGrantBody {
        organization_id: fx.org_id,
        user_id: 42,
        action: "read".into(),
        entity_type: "Device".into(),
        scope_type: "Global".into(),
        scope_entity_id: None,
        notes: None,
    }, Some(99)).await.expect("create");

    // Any entity_id passes because the grant is Global.
    let d = scope_grants::has_permission(
        &fx.pool, fx.org_id, 42, "read", "Device", Some(Uuid::new_v4())
    ).await.expect("has_permission");
    assert!(d.allowed);
    assert!(d.matched_grant_id.is_some(),
        "allowed decision must carry the matching grant id");
}

#[tokio::test]
#[ignore]
async fn has_permission_matches_entity_id_grant_only_for_that_id() {
    let Some(pool) = pool_or_skip("has_permission_matches_entity_id_grant_only_for_that_id").await else { return; };
    let fx = TenantFixture::new(pool).await.expect("fixture");
    let repo = ScopeGrantRepo::new(fx.pool.clone());

    let target_id = Uuid::new_v4();
    repo.create(&CreateScopeGrantBody {
        organization_id: fx.org_id,
        user_id: 42,
        action: "write".into(),
        entity_type: "Device".into(),
        scope_type: "EntityId".into(),
        scope_entity_id: Some(target_id),
        notes: None,
    }, Some(99)).await.expect("create");

    // The targeted id — allowed.
    let d = scope_grants::has_permission(
        &fx.pool, fx.org_id, 42, "write", "Device", Some(target_id)
    ).await.expect("target");
    assert!(d.allowed, "grant for target_id should allow that exact id");

    // A different id — denied.
    let d = scope_grants::has_permission(
        &fx.pool, fx.org_id, 42, "write", "Device", Some(Uuid::new_v4())
    ).await.expect("other");
    assert!(!d.allowed, "grant scoped to one id must not leak to others");
    assert!(d.matched_grant_id.is_none(),
        "denied decision must carry no grant id");
}

#[tokio::test]
#[ignore]
async fn has_permission_does_not_match_when_action_or_entity_type_differs() {
    let Some(pool) = pool_or_skip("has_permission_does_not_match_when_action_or_entity_type_differs").await else { return; };
    let fx = TenantFixture::new(pool).await.expect("fixture");
    let repo = ScopeGrantRepo::new(fx.pool.clone());

    // Grant is specifically read:Device — other tuples must deny.
    repo.create(&CreateScopeGrantBody {
        organization_id: fx.org_id,
        user_id: 42,
        action: "read".into(),
        entity_type: "Device".into(),
        scope_type: "Global".into(),
        scope_entity_id: None,
        notes: None,
    }, Some(99)).await.expect("create");

    // Right entity, wrong action.
    let d = scope_grants::has_permission(
        &fx.pool, fx.org_id, 42, "write", "Device", None
    ).await.expect("wrong action");
    assert!(!d.allowed, "write isn't granted by a read grant");

    // Right action, wrong entity_type.
    let d = scope_grants::has_permission(
        &fx.pool, fx.org_id, 42, "read", "Vlan", None
    ).await.expect("wrong entity");
    assert!(!d.allowed, "Vlan isn't granted by a Device grant");

    // Wrong user entirely.
    let d = scope_grants::has_permission(
        &fx.pool, fx.org_id, 43, "read", "Device", None
    ).await.expect("wrong user");
    assert!(!d.allowed, "grants are per-user — different user must deny");
}

// ─── Hierarchy expansion (v2 resolver) ───────────────────────────────────
//
// The v1 resolver matched Global + EntityId only. v2 adds
// Region / Site / Building expansion for entity types with a
// modelled hierarchy (Device + Server + Building + Site today).
// These tests pin the current behaviour so the "which entity types
// are hierarchy-expanded" answer lives in a test, not just a doc
// comment that drifts.

/// Seeds region → site → building → device for hierarchy tests.
async fn seed_device_hierarchy(pool: &PgPool, org_id: Uuid) -> (Uuid, Uuid, Uuid, Uuid) {
    let region_id: (Uuid,) = sqlx::query_as(
        "INSERT INTO net.region (organization_id, region_code, display_name, status)
         VALUES ($1, 'H-R', 'H Region', 'Active') RETURNING id")
        .bind(org_id).fetch_one(pool).await.unwrap();
    let site_id: (Uuid,) = sqlx::query_as(
        "INSERT INTO net.site (organization_id, region_id, site_code, display_name,
                               city, country, timezone, site_number, status)
         VALUES ($1, $2, 'H-S', 'H Site', 'C', 'UK', 'UTC', 1, 'Active') RETURNING id")
        .bind(org_id).bind(region_id.0).fetch_one(pool).await.unwrap();
    let building_id: (Uuid,) = sqlx::query_as(
        "INSERT INTO net.building (organization_id, site_id, building_code,
                                   display_name, building_number, status)
         VALUES ($1, $2, 'H-B', 'H Building', '1', 'Active') RETURNING id")
        .bind(org_id).bind(site_id.0).fetch_one(pool).await.unwrap();
    let device_id: (Uuid,) = sqlx::query_as(
        "INSERT INTO net.device (organization_id, building_id, hostname, status)
         VALUES ($1, $2, 'H-CORE01', 'Active') RETURNING id")
        .bind(org_id).bind(building_id.0).fetch_one(pool).await.unwrap();
    (region_id.0, site_id.0, building_id.0, device_id.0)
}

#[tokio::test]
#[ignore]
async fn region_scoped_grant_allows_device_in_that_region() {
    let Some(pool) = pool_or_skip("region_scoped_grant_allows_device_in_that_region").await else { return; };
    let fx = TenantFixture::new(pool).await.expect("fixture");
    let (region_id, _, _, device_id) = seed_device_hierarchy(&fx.pool, fx.org_id).await;
    let repo = ScopeGrantRepo::new(fx.pool.clone());

    repo.create(&CreateScopeGrantBody {
        organization_id: fx.org_id,
        user_id: 42,
        action: "read".into(),
        entity_type: "Device".into(),
        scope_type: "Region".into(),
        scope_entity_id: Some(region_id),
        notes: None,
    }, Some(99)).await.expect("create region-scoped grant");

    let d = scope_grants::has_permission(
        &fx.pool, fx.org_id, 42, "read", "Device", Some(device_id)
    ).await.expect("hierarchy resolve");
    assert!(d.allowed,
        "Region grant must expand to devices via building→site→region chain");
}

#[tokio::test]
#[ignore]
async fn region_scoped_grant_denies_device_in_a_different_region() {
    let Some(pool) = pool_or_skip("region_scoped_grant_denies_device_in_a_different_region").await else { return; };
    let fx = TenantFixture::new(pool).await.expect("fixture");
    let (_region_id_a, _, _, _) = seed_device_hierarchy(&fx.pool, fx.org_id).await;
    let repo = ScopeGrantRepo::new(fx.pool.clone());

    // Grant on a DIFFERENT region id — resolver must not leak.
    let other_region = Uuid::new_v4();
    repo.create(&CreateScopeGrantBody {
        organization_id: fx.org_id,
        user_id: 42,
        action: "read".into(),
        entity_type: "Device".into(),
        scope_type: "Region".into(),
        scope_entity_id: Some(other_region),
        notes: None,
    }, Some(99)).await.expect("create grant for a different region");

    // Device is in region_a, not other_region, so grant doesn't apply.
    let (_, _, _, device_id) = seed_device_hierarchy_b(&fx.pool, fx.org_id).await;
    let d = scope_grants::has_permission(
        &fx.pool, fx.org_id, 42, "read", "Device", Some(device_id)
    ).await.expect("hierarchy resolve");
    assert!(!d.allowed,
        "Region grant on region_b must NOT leak to a device in region_a");
}

async fn seed_device_hierarchy_b(pool: &PgPool, org_id: Uuid) -> (Uuid, Uuid, Uuid, Uuid) {
    // Distinct codes so the test tenant holds two parallel hierarchies.
    let region_id: (Uuid,) = sqlx::query_as(
        "INSERT INTO net.region (organization_id, region_code, display_name, status)
         VALUES ($1, 'H-R-B', 'Other region', 'Active') RETURNING id")
        .bind(org_id).fetch_one(pool).await.unwrap();
    let site_id: (Uuid,) = sqlx::query_as(
        "INSERT INTO net.site (organization_id, region_id, site_code, display_name,
                               city, country, timezone, site_number, status)
         VALUES ($1, $2, 'H-S-B', 'Other site', 'C', 'UK', 'UTC', 2, 'Active') RETURNING id")
        .bind(org_id).bind(region_id.0).fetch_one(pool).await.unwrap();
    let building_id: (Uuid,) = sqlx::query_as(
        "INSERT INTO net.building (organization_id, site_id, building_code,
                                   display_name, building_number, status)
         VALUES ($1, $2, 'H-B-B', 'Other building', '2', 'Active') RETURNING id")
        .bind(org_id).bind(site_id.0).fetch_one(pool).await.unwrap();
    let device_id: (Uuid,) = sqlx::query_as(
        "INSERT INTO net.device (organization_id, building_id, hostname, status)
         VALUES ($1, $2, 'H-CORE-B', 'Active') RETURNING id")
        .bind(org_id).bind(building_id.0).fetch_one(pool).await.unwrap();
    (region_id.0, site_id.0, building_id.0, device_id.0)
}

#[tokio::test]
#[ignore]
async fn building_scoped_grant_allows_devices_in_that_building_only() {
    let Some(pool) = pool_or_skip("building_scoped_grant_allows_devices_in_that_building_only").await else { return; };
    let fx = TenantFixture::new(pool).await.expect("fixture");
    let (_, _, building_a, device_a) = seed_device_hierarchy(&fx.pool, fx.org_id).await;
    let (_, _, _, device_b) = seed_device_hierarchy_b(&fx.pool, fx.org_id).await;
    let repo = ScopeGrantRepo::new(fx.pool.clone());

    repo.create(&CreateScopeGrantBody {
        organization_id: fx.org_id,
        user_id: 42,
        action: "write".into(),
        entity_type: "Device".into(),
        scope_type: "Building".into(),
        scope_entity_id: Some(building_a),
        notes: None,
    }, Some(99)).await.expect("building-scoped grant");

    // device_a is in building_a — allowed.
    let d = scope_grants::has_permission(
        &fx.pool, fx.org_id, 42, "write", "Device", Some(device_a)
    ).await.expect("device_a");
    assert!(d.allowed, "device_a in building_a must be allowed by a Building=a grant");

    // device_b is in building_b — denied.
    let d = scope_grants::has_permission(
        &fx.pool, fx.org_id, 42, "write", "Device", Some(device_b)
    ).await.expect("device_b");
    assert!(!d.allowed, "device_b in building_b must NOT be allowed by a Building=a grant");
}
