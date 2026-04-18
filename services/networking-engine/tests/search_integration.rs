//! Live-DB integration tests for `search::global_search`.
//!
//! `#[ignore]` opt-in + skip-when-no-env pattern, same as the other
//! integration suites. These tests exercise the query-time UNION
//! search against a minimal seeded tenant.

use networking_engine::search::{self, global_search};
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

struct SearchFixture {
    pool: PgPool,
    org_id: Uuid,
}

impl SearchFixture {
    async fn new(pool: PgPool) -> sqlx::Result<Self> {
        let org_id = Uuid::new_v4();
        sqlx::query(
            "INSERT INTO central_platform.tenants (id, slug, display_name, status)
             VALUES ($1, $2, $2, 'Active') ON CONFLICT (id) DO NOTHING")
            .bind(org_id).bind(format!("search-itest-{org_id}"))
            .execute(&pool).await?;

        // Minimum hierarchy for devices + vlans.
        let region_id: (Uuid,) = sqlx::query_as(
            "INSERT INTO net.region (organization_id, region_code, display_name, status)
             VALUES ($1, 'SE-R', 'Search Region', 'Active') RETURNING id")
            .bind(org_id).fetch_one(&pool).await?;
        let site_id: (Uuid,) = sqlx::query_as(
            "INSERT INTO net.site (organization_id, region_id, site_code, display_name,
                                   city, country, timezone, site_number, status)
             VALUES ($1, $2, 'SE-S', 'Search Site', 'C', 'UK', 'UTC', 1, 'Active')
             RETURNING id")
            .bind(org_id).bind(region_id.0).fetch_one(&pool).await?;
        let building_id: (Uuid,) = sqlx::query_as(
            "INSERT INTO net.building (organization_id, site_id, building_code,
                                       display_name, building_number, status)
             VALUES ($1, $2, 'SE-B', 'Search Building', '1', 'Active')
             RETURNING id")
            .bind(org_id).bind(site_id.0).fetch_one(&pool).await?;
        let role_id: (Uuid,) = sqlx::query_as(
            "INSERT INTO net.device_role (organization_id, role_code, display_name,
                                          naming_template, status)
             VALUES ($1, 'Core', 'Search Core', '{building_code}-CORE{instance}', 'Active')
             RETURNING id")
            .bind(org_id).fetch_one(&pool).await?;
        sqlx::query(
            "INSERT INTO net.device (organization_id, device_role_id, building_id,
                                     hostname, status)
             VALUES ($1, $2, $3, 'MEP-91-CORE02', 'Active'),
                    ($1, $2, $3, 'MEP-93-L1-CORE02', 'Active')")
            .bind(org_id).bind(role_id.0).bind(building_id.0)
            .execute(&pool).await?;

        // VLANs (+ pool + block).
        let vp_id: (Uuid,) = sqlx::query_as(
            "INSERT INTO net.vlan_pool (organization_id, pool_code, display_name,
                                        vlan_first, vlan_last)
             VALUES ($1, 'SE-VP', 'SE pool', 1, 4094) RETURNING id")
            .bind(org_id).fetch_one(&pool).await?;
        let vb_id: (Uuid,) = sqlx::query_as(
            "INSERT INTO net.vlan_block (organization_id, pool_id, block_code, display_name,
                                         vlan_first, vlan_last, scope_level)
             VALUES ($1, $2, 'SE-VB', 'SE block', 1, 4094, 'Free') RETURNING id")
            .bind(org_id).bind(vp_id.0).fetch_one(&pool).await?;
        sqlx::query(
            "INSERT INTO net.vlan (organization_id, pool_id, block_id,
                                   vlan_id, display_name, description, scope_level, status)
             VALUES
                ($1, $2, $3, 101, 'IT',      'IT staff network', 'Free', 'Active'),
                ($1, $2, $3, 120, 'Servers', 'Primary server LAN', 'Free', 'Active')")
            .bind(org_id).bind(vp_id.0).bind(vb_id.0).execute(&pool).await?;

        Ok(Self { pool, org_id })
    }
}

impl Drop for SearchFixture {
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
async fn empty_query_returns_empty_results() {
    let Some(pool) = pool_or_skip("empty_query_returns_empty_results").await else { return; };
    let fx = SearchFixture::new(pool).await.expect("fixture");

    let results = global_search(&fx.pool, fx.org_id, "", None, 50)
        .await.expect("empty");
    assert!(results.is_empty(),
        "empty query must short-circuit to empty results (not full table scan)");
}

#[tokio::test]
#[ignore]
async fn search_finds_device_by_hostname_token() {
    let Some(pool) = pool_or_skip("search_finds_device_by_hostname_token").await else { return; };
    let fx = SearchFixture::new(pool).await.expect("fixture");

    // "MEP 91 CORE" should tokenise and match MEP-91-CORE02 (not the
    // MEP-93-L1-CORE02 row since it lacks "91").
    let results = global_search(&fx.pool, fx.org_id, "MEP-91-CORE02", None, 50)
        .await.expect("search");
    assert!(!results.is_empty(), "should find the device");
    assert!(results.iter().any(|r| r.entity_type == "Device" && r.label == "MEP-91-CORE02"),
        "expected Device hit for MEP-91-CORE02: {results:?}");
}

#[tokio::test]
#[ignore]
async fn search_finds_vlan_by_description_text() {
    let Some(pool) = pool_or_skip("search_finds_vlan_by_description_text").await else { return; };
    let fx = SearchFixture::new(pool).await.expect("fixture");

    // VLAN 120's description is "Primary server LAN" — query
    // "server LAN" should match via stemming.
    let results = global_search(&fx.pool, fx.org_id, "server LAN", None, 50)
        .await.expect("search");
    assert!(results.iter().any(|r| r.entity_type == "Vlan"),
        "description text should match VLAN rows: {results:?}");
}

#[tokio::test]
#[ignore]
async fn search_entity_types_filter_narrows_to_selected() {
    let Some(pool) = pool_or_skip("search_entity_types_filter_narrows_to_selected").await else { return; };
    let fx = SearchFixture::new(pool).await.expect("fixture");

    // "core" matches Device hostnames + role — entity_types filter
    // to Device-only should skip anything else.
    let entity_types = Some(
        ["Device"].iter().map(|s| s.to_string()).collect::<std::collections::HashSet<_>>()
    );
    let results = global_search(
        &fx.pool, fx.org_id, "CORE02", entity_types.as_ref(), 50
    ).await.expect("search");
    assert!(!results.is_empty());
    for r in &results {
        assert_eq!(r.entity_type, "Device",
            "entity_types filter must drop non-Device results: {r:?}");
    }
}

#[tokio::test]
#[ignore]
async fn search_respects_organization_id_scoping() {
    let Some(pool) = pool_or_skip("search_respects_organization_id_scoping").await else { return; };
    let fx = SearchFixture::new(pool).await.expect("fixture");

    // Search under a DIFFERENT org — must not leak into fx's data.
    let other_org = Uuid::new_v4();
    let results = global_search(&fx.pool, other_org, "CORE02", None, 50)
        .await.expect("search");
    assert!(results.is_empty(),
        "organization_id is a hard wall — other-org search must not match");
}

#[tokio::test]
#[ignore]
async fn search_results_ranked_highest_first() {
    let Some(pool) = pool_or_skip("search_results_ranked_highest_first").await else { return; };
    let fx = SearchFixture::new(pool).await.expect("fixture");

    let results = global_search(&fx.pool, fx.org_id, "Servers", None, 50)
        .await.expect("search");
    for pair in results.windows(2) {
        assert!(pair[0].rank >= pair[1].rank,
            "results must be rank-descending: {:?}", results);
    }
}

#[tokio::test]
#[ignore]
async fn search_limit_clamps_result_count() {
    let Some(pool) = pool_or_skip("search_limit_clamps_result_count").await else { return; };
    let fx = SearchFixture::new(pool).await.expect("fixture");

    // Query matches multiple entities; limit=1 must return exactly one.
    let results = global_search(&fx.pool, fx.org_id, "MEP", None, 1)
        .await.expect("search");
    assert!(results.len() <= 1,
        "limit=1 must cap at one result: {results:?}");

    // clamp_search_limit also guards: -5 → 1
    assert_eq!(search::clamp_search_limit(Some(-5)), 1);
}
