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

#[tokio::test]
#[ignore]
async fn region_scoped_grant_stores_but_does_not_yet_enforce_hierarchy() {
    // Honest test of the v1-resolver limitation: a Region-scoped
    // grant is STORED but has_permission doesn't yet expand it
    // through the hierarchy (that lands in a follow-on slice).
    // A follow-on resolver change should flip this test's assertion;
    // until then we pin the current behaviour so nobody accidentally
    // ships Region-scoped grants thinking they're enforced.
    let Some(pool) = pool_or_skip("region_scoped_grant_stores_but_does_not_yet_enforce_hierarchy").await else { return; };
    let fx = TenantFixture::new(pool).await.expect("fixture");
    let repo = ScopeGrantRepo::new(fx.pool.clone());

    let region_id = Uuid::new_v4();
    repo.create(&CreateScopeGrantBody {
        organization_id: fx.org_id,
        user_id: 42,
        action: "read".into(),
        entity_type: "Device".into(),
        scope_type: "Region".into(),
        scope_entity_id: Some(region_id),
        notes: None,
    }, Some(99)).await.expect("create region-scoped grant");

    // Asking about a device not yet linked to the region — resolver
    // returns deny because v1 doesn't expand Region grants.
    let d = scope_grants::has_permission(
        &fx.pool, fx.org_id, 42, "read", "Device", Some(Uuid::new_v4())
    ).await.expect("resolver call");
    assert!(!d.allowed,
        "v1 resolver should NOT match Region-scoped grants yet — follow-on slice adds hierarchy expansion");
}
