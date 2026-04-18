//! Live-DB integration tests for the config-gen SQL paths.
//!
//! These complement the unit tests in `src/config_gen.rs` (which
//! exercise the pure helpers over in-memory `DeviceContext` fixtures)
//! by driving the actual INSERT / SELECT / UPDATE paths against a
//! Postgres with migration 102 + 103 applied.
//!
//! ## Running
//!
//! The tests are `#[ignore]` so `cargo test` stays fast and
//! DB-independent. Opt in with:
//!
//! ```sh
//! export TEST_DATABASE_URL="postgresql://central:central@192.168.56.201:5432/central_test"
//! cargo test --test config_gen_integration -- --ignored --test-threads=1
//! ```
//!
//! `--test-threads=1` keeps multiple integration suites from stomping
//! each other's schema state. Within one suite we still isolate via
//! per-test tenant UUIDs so individual tests can share a DB.
//!
//! ## Skip-on-missing-env behaviour
//!
//! If `TEST_DATABASE_URL` is unset even when the suite is explicitly
//! opted into via `--ignored`, each test prints a clear skip message
//! and returns `Ok(())` rather than failing. This means CI without a
//! DB attached won't flake on these tests when someone accidentally
//! passes `--ignored` globally.

use networking_engine::{
    cli_flavor,
    config_gen::{self, RenderedConfig},
    dhcp_relay::{CreateDhcpRelayBody, DhcpRelayRepo, ListDhcpRelayQuery, UpdateDhcpRelayBody},
};
use sqlx::{PgPool, postgres::PgPoolOptions};
use std::env;
use uuid::Uuid;

// ─── Harness ─────────────────────────────────────────────────────────────

/// Connect to `TEST_DATABASE_URL` or return `None` so the caller can
/// skip cleanly. Deliberately does NOT panic — missing env is a valid
/// "not running in a DB-enabled CI" signal, not a test failure.
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

/// Minimal "tenant + hierarchy + device" setup — enough rows to satisfy
/// the FKs every `config_gen::fetch_context` query walks, without
/// pulling in the full Immunocore seed. Returns the key ids the tests
/// need.
///
/// Rolls back (via soft-delete on the universal `deleted_at` columns)
/// at the end of each test by way of the caller's `Drop`, so tests
/// are re-runnable against the same DB without piling up garbage.
struct TenantFixture {
    pool: PgPool,
    org_id: Uuid,
    device_id: Uuid,
    vlan_id: Uuid,
}

impl TenantFixture {
    async fn new(pool: PgPool) -> sqlx::Result<Self> {
        // Unique tenant id per test — lets tests run in parallel and
        // means one test's cleanup can't blow away another's data.
        let org_id = Uuid::new_v4();

        // The `central_platform.tenants` FK requires a tenant row. In
        // the real system this is bootstrapped by platform-admin; for
        // the test harness we insert a minimal stub.
        sqlx::query(
            "INSERT INTO central_platform.tenants (id, slug, display_name, status)
             VALUES ($1, $2, $2, 'Active')
             ON CONFLICT (id) DO NOTHING")
            .bind(org_id)
            .bind(format!("cg-itest-{org_id}"))
            .execute(&pool).await?;

        // Minimal hierarchy — region → site → building. Codes are
        // test-scoped so they're unique per org (the UNIQUE constraints
        // on (org, code) in each hierarchy table allow reuse across
        // tenants).
        let region_id: (Uuid,) = sqlx::query_as(
            "INSERT INTO net.region (organization_id, region_code, display_name, status)
             VALUES ($1, 'IT-R', 'Integration Test Region', 'Active')
             RETURNING id")
            .bind(org_id).fetch_one(&pool).await?;
        let site_id: (Uuid,) = sqlx::query_as(
            "INSERT INTO net.site (organization_id, region_id, site_code, display_name,
                                   city, country, timezone, site_number, status)
             VALUES ($1, $2, 'IT-S', 'ITest Site', 'ITest', 'UK', 'UTC', 1, 'Active')
             RETURNING id")
            .bind(org_id).bind(region_id.0).fetch_one(&pool).await?;
        let building_id: (Uuid,) = sqlx::query_as(
            "INSERT INTO net.building (organization_id, site_id, building_code,
                                       display_name, building_number, status)
             VALUES ($1, $2, 'IT-B', 'ITest Building', '1', 'Active')
             RETURNING id")
            .bind(org_id).bind(site_id.0).fetch_one(&pool).await?;

        // Device role — name matches nothing fancy, template uses the
        // same `{building_code}-CORE{instance}` shape the Immunocore
        // seed uses so resolve_device_hostname has something to expand.
        let role_id: (Uuid,) = sqlx::query_as(
            "INSERT INTO net.device_role (organization_id, role_code, display_name,
                                          naming_template, status)
             VALUES ($1, 'Core', 'ITest Core', '{building_code}-CORE{instance}', 'Active')
             RETURNING id")
            .bind(org_id).fetch_one(&pool).await?;

        // Device — device_code '01' drives `{instance}` → '01'.
        let device_id: (Uuid,) = sqlx::query_as(
            "INSERT INTO net.device (organization_id, device_role_id, building_id,
                                     hostname, device_code, status)
             VALUES ($1, $2, $3, 'IT-B-CORE01', '01', 'Active')
             RETURNING id")
            .bind(org_id).bind(role_id.0).bind(building_id.0).fetch_one(&pool).await?;

        // VLAN — needed for DhcpRelay CRUD test; trivial definition.
        let vlan_pool_id: (Uuid,) = sqlx::query_as(
            "INSERT INTO net.vlan_pool (organization_id, pool_code, display_name,
                                        vlan_first, vlan_last)
             VALUES ($1, 'IT-VP', 'ITest VLAN pool', 1, 4094)
             RETURNING id")
            .bind(org_id).fetch_one(&pool).await?;
        let vlan_block_id: (Uuid,) = sqlx::query_as(
            "INSERT INTO net.vlan_block (organization_id, pool_id, block_code, display_name,
                                         vlan_first, vlan_last, scope_level)
             VALUES ($1, $2, 'IT-VB', 'ITest VLAN block', 1, 4094, 'Free')
             RETURNING id")
            .bind(org_id).bind(vlan_pool_id.0).fetch_one(&pool).await?;
        let vlan_id: (Uuid,) = sqlx::query_as(
            "INSERT INTO net.vlan (organization_id, pool_id, block_id, vlan_id,
                                   display_name, scope_level, status)
             VALUES ($1, $2, $3, 120, 'ITest Servers', 'Free', 'Active')
             RETURNING id")
            .bind(org_id).bind(vlan_pool_id.0).bind(vlan_block_id.0).fetch_one(&pool).await?;

        // Default CLI flavor: the tenant needs a PicOS enable for
        // resolve_for_device to succeed.
        let flavor_body = cli_flavor::SetFlavorConfigBody {
            enabled:    Some(true),
            is_default: Some(true),
            notes:      None,
        };
        cli_flavor::set_flavor_config(&pool, org_id, "PicOS", &flavor_body, None).await
            .expect("seed PicOS flavor");

        Ok(Self { pool, org_id, device_id: device_id.0, vlan_id: vlan_id.0 })
    }
}

impl Drop for TenantFixture {
    fn drop(&mut self) {
        // Best-effort cleanup — soft-delete everything we inserted by
        // org. ON DELETE CASCADE on the tenants row handles the rest.
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
async fn render_persist_populates_id_and_chain() {
    let Some(pool) = pool_or_skip("render_persist_populates_id_and_chain").await else { return; };
    let fx = TenantFixture::new(pool).await.expect("fixture");

    // First render: no previous chain. id should come back Some,
    // previous_render_id should be None.
    let first: RenderedConfig = config_gen::render_device_persisted(
        &fx.pool, fx.org_id, fx.device_id, Some(42)
    ).await.expect("first render");
    assert!(first.id.is_some(), "first render id must be populated after persist");
    assert!(first.previous_render_id.is_none(),
        "first render has no previous chain entry");

    // Second render: chains to the first.
    let second = config_gen::render_device_persisted(
        &fx.pool, fx.org_id, fx.device_id, Some(42)
    ).await.expect("second render");
    assert_eq!(second.previous_render_id, first.id,
        "second render's previous_render_id must point at the first");
}

#[tokio::test]
#[ignore]
async fn list_and_get_render_round_trip() {
    let Some(pool) = pool_or_skip("list_and_get_render_round_trip").await else { return; };
    let fx = TenantFixture::new(pool).await.expect("fixture");

    let persisted = config_gen::render_device_persisted(
        &fx.pool, fx.org_id, fx.device_id, None
    ).await.expect("render");
    let render_id = persisted.id.expect("id populated");

    let list = config_gen::list_renders(&fx.pool, fx.org_id, fx.device_id, 10)
        .await.expect("list");
    assert_eq!(list.len(), 1, "exactly one render for this device");
    assert_eq!(list[0].id, render_id);

    let record = config_gen::get_render(&fx.pool, fx.org_id, render_id)
        .await.expect("get");
    assert_eq!(record.body, persisted.body,
        "body round-trips through persistence unchanged");
    assert_eq!(record.body_sha256, persisted.body_sha256);
}

#[tokio::test]
#[ignore]
async fn diff_render_returns_whole_body_on_first_ever() {
    let Some(pool) = pool_or_skip("diff_render_returns_whole_body_on_first_ever").await else { return; };
    let fx = TenantFixture::new(pool).await.expect("fixture");

    let persisted = config_gen::render_device_persisted(
        &fx.pool, fx.org_id, fx.device_id, None
    ).await.expect("render");

    let diff = config_gen::diff_render(&fx.pool, fx.org_id, persisted.id.unwrap())
        .await.expect("diff");
    assert_eq!(diff.previous_render_id, None,
        "first render has no previous to diff against");
    assert!(!diff.added.is_empty(), "added lines must carry the full body");
    assert!(diff.removed.is_empty());
    assert_eq!(diff.unchanged_count, 0);
}

#[tokio::test]
#[ignore]
async fn dhcp_relay_crud_round_trip() {
    let Some(pool) = pool_or_skip("dhcp_relay_crud_round_trip").await else { return; };
    let fx = TenantFixture::new(pool).await.expect("fixture");

    let repo = DhcpRelayRepo::new(fx.pool.clone());

    let created = repo.create(&CreateDhcpRelayBody {
        organization_id: fx.org_id,
        vlan_id:         fx.vlan_id,
        server_ip:       "10.99.99.10/32".parse().unwrap(),
        ip_address_id:   None,
        priority:        10,
        notes:           Some("integration test".into()),
    }, Some(7)).await.expect("create");
    assert_eq!(created.priority, 10);

    let listed = repo.list(&ListDhcpRelayQuery {
        organization_id: fx.org_id,
        vlan_id:         Some(fx.vlan_id),
    }).await.expect("list");
    assert_eq!(listed.len(), 1);

    let updated = repo.update(created.id, fx.org_id, &UpdateDhcpRelayBody {
        priority:      20,
        ip_address_id: None,
        notes:         Some("bumped".into()),
        version:       created.version,
    }, Some(7)).await.expect("update");
    assert_eq!(updated.priority, 20);
    assert_eq!(updated.version, created.version + 1,
        "version must bump on update");

    // Optimistic-lock failure when we pass the stale version.
    let stale = repo.update(created.id, fx.org_id, &UpdateDhcpRelayBody {
        priority:      30,
        ip_address_id: None,
        notes:         None,
        version:       created.version,   // stale
    }, Some(7)).await;
    assert!(stale.is_err(), "stale version must fail the update");

    repo.soft_delete(created.id, fx.org_id, Some(7)).await.expect("delete");
    let after_delete = repo.list(&ListDhcpRelayQuery {
        organization_id: fx.org_id, vlan_id: Some(fx.vlan_id),
    }).await.expect("list after delete");
    assert!(after_delete.is_empty(), "soft-deleted row must be filtered out");
}
