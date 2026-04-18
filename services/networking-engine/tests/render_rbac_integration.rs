//! Live-DB integration tests for RBAC enforcement on the config-gen
//! surfaces. Follows the same `#[ignore]` opt-in + skip-when-no-env
//! pattern as the other integration suites.
//!
//! Covers the render path specifically — the other bulk surfaces
//! (bulk_edit / bulk_import) have their own suites. Split rather
//! than collocated because the fixture shape is noticeably different
//! (needs CLI flavor + device_role with template + actual device
//! rows to pass render_device_persisted's fetch_context).

use networking_engine::{
    cli_flavor,
    config_gen,
    scope_grants::{CreateScopeGrantBody, ScopeGrantRepo},
};
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

struct RenderFixture {
    pool: PgPool,
    org_id: Uuid,
    building_id: Uuid,
    device_id: Uuid,
}

impl RenderFixture {
    async fn new(pool: PgPool) -> sqlx::Result<Self> {
        let org_id = Uuid::new_v4();
        sqlx::query(
            "INSERT INTO central_platform.tenants (id, slug, display_name, status)
             VALUES ($1, $2, $2, 'Active') ON CONFLICT (id) DO NOTHING")
            .bind(org_id).bind(format!("rr-itest-{org_id}"))
            .execute(&pool).await?;

        let region_id: (Uuid,) = sqlx::query_as(
            "INSERT INTO net.region (organization_id, region_code, display_name, status)
             VALUES ($1, 'RR-R', 'RR Region', 'Active') RETURNING id")
            .bind(org_id).fetch_one(&pool).await?;
        let site_id: (Uuid,) = sqlx::query_as(
            "INSERT INTO net.site (organization_id, region_id, site_code, display_name,
                                   city, country, timezone, site_number, status)
             VALUES ($1, $2, 'RR-S', 'RR Site', 'C', 'UK', 'UTC', 1, 'Active') RETURNING id")
            .bind(org_id).bind(region_id.0).fetch_one(&pool).await?;
        let building_id: (Uuid,) = sqlx::query_as(
            "INSERT INTO net.building (organization_id, site_id, building_code,
                                       display_name, building_number, status)
             VALUES ($1, $2, 'RR-B', 'RR Building', '1', 'Active') RETURNING id")
            .bind(org_id).bind(site_id.0).fetch_one(&pool).await?;
        let role_id: (Uuid,) = sqlx::query_as(
            "INSERT INTO net.device_role (organization_id, role_code, display_name,
                                          naming_template, status)
             VALUES ($1, 'Core', 'RR Core', '{building_code}-CORE{instance}', 'Active')
             RETURNING id")
            .bind(org_id).fetch_one(&pool).await?;
        let device_id: (Uuid,) = sqlx::query_as(
            "INSERT INTO net.device (organization_id, device_role_id, building_id,
                                     hostname, device_code, status)
             VALUES ($1, $2, $3, 'RR-CORE01', '01', 'Active') RETURNING id")
            .bind(org_id).bind(role_id.0).bind(building_id.0).fetch_one(&pool).await?;

        let flavor_body = cli_flavor::SetFlavorConfigBody {
            enabled: Some(true), is_default: Some(true), notes: None,
        };
        cli_flavor::set_flavor_config(&pool, org_id, "PicOS", &flavor_body, None).await
            .expect("seed PicOS flavor");

        Ok(Self { pool, org_id, building_id: building_id.0, device_id: device_id.0 })
    }
}

impl Drop for RenderFixture {
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
async fn render_device_bypasses_rbac_on_service_call() {
    // No X-User-Id → service bypass. Backward compat during rollout.
    // Note: this test calls render_device_persisted directly (not
    // through the HTTP handler) — it proves the library side
    // doesn't enforce; the HTTP handler adds the require_permission
    // gate which the tests below exercise through scope_grants.
    let Some(pool) = pool_or_skip("render_device_bypasses_rbac_on_service_call").await else { return; };
    let fx = RenderFixture::new(pool).await.expect("fixture");

    let rc = config_gen::render_device_persisted(
        &fx.pool, fx.org_id, fx.device_id, None,
    ).await.expect("render");
    assert!(rc.id.is_some(), "service-bypass path should still persist");
}

#[tokio::test]
#[ignore]
async fn render_device_gated_via_scope_grants_resolver() {
    // The HTTP handler wraps render_device_persisted with
    // require_permission(write, Device). Here we exercise that
    // resolver directly — same rejection the handler would emit.
    let Some(pool) = pool_or_skip("render_device_gated_via_scope_grants_resolver").await else { return; };
    let fx = RenderFixture::new(pool).await.expect("fixture");

    // User 42, no grant → denied.
    let d = networking_engine::scope_grants::has_permission(
        &fx.pool, fx.org_id, 42, "write", "Device", Some(fx.device_id)
    ).await.expect("decision");
    assert!(!d.allowed);

    // Seed a Building-scoped write:Device grant. Hierarchy expansion
    // should let the Device-level check pass because the device is
    // in that building.
    ScopeGrantRepo::new(fx.pool.clone()).create(&CreateScopeGrantBody {
        organization_id: fx.org_id,
        user_id: 42,
        action: "write".into(),
        entity_type: "Device".into(),
        scope_type: "Building".into(),
        scope_entity_id: Some(fx.building_id),
        notes: None,
    }, Some(99)).await.expect("grant");

    let d = networking_engine::scope_grants::has_permission(
        &fx.pool, fx.org_id, 42, "write", "Device", Some(fx.device_id)
    ).await.expect("after grant");
    assert!(d.allowed,
        "Building-scoped write:Device must match the device via hierarchy");
}

#[tokio::test]
#[ignore]
async fn render_history_read_gated_by_read_device() {
    // The GET list / get / diff handlers check read:Device on the
    // target. Exercise the resolver path with write grant only —
    // should deny for 'read'.
    let Some(pool) = pool_or_skip("render_history_read_gated_by_read_device").await else { return; };
    let fx = RenderFixture::new(pool).await.expect("fixture");

    // write:Device at Global doesn't authorise read without a
    // separate read grant — confirms the action dimension of the
    // tuple matters.
    ScopeGrantRepo::new(fx.pool.clone()).create(&CreateScopeGrantBody {
        organization_id: fx.org_id,
        user_id: 42,
        action: "write".into(),
        entity_type: "Device".into(),
        scope_type: "Global".into(),
        scope_entity_id: None,
        notes: None,
    }, Some(99)).await.expect("write grant");

    let read_check = networking_engine::scope_grants::has_permission(
        &fx.pool, fx.org_id, 42, "read", "Device", Some(fx.device_id)
    ).await.expect("read check");
    assert!(!read_check.allowed,
        "write:Device should NOT authorise a read:Device check");
}

#[tokio::test]
#[ignore]
async fn render_building_gated_by_write_building_grant() {
    // Fan-out renders gate on the container entity_type, not per-
    // device. Here: write:Building at EntityId scope matches the
    // building in question; resolver returns allowed.
    let Some(pool) = pool_or_skip("render_building_gated_by_write_building_grant").await else { return; };
    let fx = RenderFixture::new(pool).await.expect("fixture");

    let d = networking_engine::scope_grants::has_permission(
        &fx.pool, fx.org_id, 42, "write", "Building", Some(fx.building_id)
    ).await.expect("decision");
    assert!(!d.allowed, "no grant → denied");

    ScopeGrantRepo::new(fx.pool.clone()).create(&CreateScopeGrantBody {
        organization_id: fx.org_id,
        user_id: 42,
        action: "write".into(),
        entity_type: "Building".into(),
        scope_type: "EntityId".into(),
        scope_entity_id: Some(fx.building_id),
        notes: None,
    }, Some(99)).await.expect("building grant");

    let d = networking_engine::scope_grants::has_permission(
        &fx.pool, fx.org_id, 42, "write", "Building", Some(fx.building_id)
    ).await.expect("after grant");
    assert!(d.allowed);
}
