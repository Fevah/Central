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

use networking_engine::bulk_export;
use networking_engine::bulk_import::{self, ImportMode};
use networking_engine::scope_grants::{CreateScopeGrantBody, ScopeGrantRepo};
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
        &fx.pool, fx.org_id, &csv, /*dry_run=*/true, ImportMode::Create, None,
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
        &fx.pool, fx.org_id, &csv, /*dry_run=*/false, ImportMode::Create,
        // None = service-call RBAC bypass. This test is about the
        // import-apply happy path, not auth; auth tests are below.
        None,
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
        &fx.pool, fx.org_id, &csv, /*dry_run=*/false, ImportMode::Create, None,
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

// ─── VLAN + subnet import tests ──────────────────────────────────────────

/// Extra scaffolding on top of TenantFixture — seeds a VLAN pool +
/// block (needed for VLAN import) plus an IP pool (needed for
/// subnet import). Layered on top so the plain TenantFixture stays
/// minimal for the device-import tests that don't need them.
struct PoolsFixture {
    tenant: TenantFixture,
    block_code: String,
    pool_code: String,
}

impl PoolsFixture {
    async fn new(pool: PgPool) -> sqlx::Result<Self> {
        let tenant = TenantFixture::new(pool).await?;

        let vlan_pool_id: (Uuid,) = sqlx::query_as(
            "INSERT INTO net.vlan_pool (organization_id, pool_code, display_name,
                                        vlan_first, vlan_last)
             VALUES ($1, 'BI-VP', 'BI VLAN pool', 1, 4094) RETURNING id")
            .bind(tenant.org_id).fetch_one(&tenant.pool).await?;
        sqlx::query(
            "INSERT INTO net.vlan_block (organization_id, pool_id, block_code, display_name,
                                         vlan_first, vlan_last, scope_level)
             VALUES ($1, $2, 'BI-VB', 'BI VLAN block', 1, 4094, 'Free')")
            .bind(tenant.org_id).bind(vlan_pool_id.0).execute(&tenant.pool).await?;

        sqlx::query(
            "INSERT INTO net.ip_pool (organization_id, pool_code, display_name,
                                      network, address_family)
             VALUES ($1, 'BI-IP', 'BI IP pool', '10.99.0.0/16', 4)")
            .bind(tenant.org_id).execute(&tenant.pool).await?;

        Ok(Self { tenant, block_code: "BI-VB".into(), pool_code: "BI-IP".into() })
    }

    async fn count_vlans(&self) -> i64 {
        let (n,): (i64,) = sqlx::query_as(
            "SELECT COUNT(*) FROM net.vlan
              WHERE organization_id = $1 AND deleted_at IS NULL")
            .bind(self.tenant.org_id).fetch_one(&self.tenant.pool).await.unwrap();
        n
    }

    async fn count_subnets(&self) -> i64 {
        let (n,): (i64,) = sqlx::query_as(
            "SELECT COUNT(*) FROM net.subnet
              WHERE organization_id = $1 AND deleted_at IS NULL")
            .bind(self.tenant.org_id).fetch_one(&self.tenant.pool).await.unwrap();
        n
    }
}

const VLAN_HEADER: &str = "vlan_id,display_name,description,scope_level,scope_entity_code,template_code,block_code,status\r\n";
const SUBNET_HEADER: &str = "subnet_code,display_name,network,vlan_id,pool_code,scope_level,scope_entity_code,status\r\n";

#[tokio::test]
#[ignore]
async fn vlan_import_dry_run_no_writes() {
    let Some(pool) = pool_or_skip("vlan_import_dry_run_no_writes").await else { return; };
    let fx = PoolsFixture::new(pool).await.expect("fixture");

    let csv = format!("{VLAN_HEADER}101,IT,,Free,,,{},Active\r\n", fx.block_code);
    let result = bulk_import::import_vlans(
        &fx.tenant.pool, fx.tenant.org_id, &csv, true, ImportMode::Create, None,
    ).await.expect("dry run");
    assert_eq!(result.valid, 1);
    assert!(!result.applied);
    assert_eq!(fx.count_vlans().await, 0);
}

#[tokio::test]
#[ignore]
async fn vlan_import_happy_path_commits() {
    let Some(pool) = pool_or_skip("vlan_import_happy_path_commits").await else { return; };
    let fx = PoolsFixture::new(pool).await.expect("fixture");

    let csv = format!(
        "{VLAN_HEADER}\
         101,IT,,Free,,,{b},Active\r\n\
         120,Servers,Servers LAN,Free,,,{b},Active\r\n",
        b = fx.block_code);
    let result = bulk_import::import_vlans(
        // None = service-call RBAC bypass (auth tests live below).
        &fx.tenant.pool, fx.tenant.org_id, &csv, false, ImportMode::Create, None,
    ).await.expect("apply");
    assert_eq!(result.valid, 2);
    assert!(result.applied);
    assert_eq!(fx.count_vlans().await, 2);
}

#[tokio::test]
#[ignore]
async fn vlan_import_rejects_duplicate_vlan_id_in_block() {
    let Some(pool) = pool_or_skip("vlan_import_rejects_duplicate_vlan_id_in_block").await else { return; };
    let fx = PoolsFixture::new(pool).await.expect("fixture");

    let csv1 = format!("{VLAN_HEADER}101,IT,,Free,,,{},Active\r\n", fx.block_code);
    bulk_import::import_vlans(&fx.tenant.pool, fx.tenant.org_id, &csv1, false, ImportMode::Create, None)
        .await.expect("seed");
    assert_eq!(fx.count_vlans().await, 1);

    let result = bulk_import::import_vlans(&fx.tenant.pool, fx.tenant.org_id, &csv1, false, ImportMode::Create, None)
        .await.expect("re-apply");
    assert_eq!(result.invalid, 1);
    assert!(!result.applied);
    assert!(result.outcomes[0].errors.iter().any(|e| e.contains("already exists")),
        "dup vlan should surface clear error: {:?}", result.outcomes[0].errors);
    assert_eq!(fx.count_vlans().await, 1);
}

#[tokio::test]
#[ignore]
async fn subnet_import_happy_path_commits() {
    let Some(pool) = pool_or_skip("subnet_import_happy_path_commits").await else { return; };
    let fx = PoolsFixture::new(pool).await.expect("fixture");

    let csv = format!(
        "{SUBNET_HEADER}\
         BI-SUB-A,Subnet A,10.99.1.0/24,,{p},Free,,Active\r\n\
         BI-SUB-B,Subnet B,10.99.2.0/24,,{p},Free,,Active\r\n",
        p = fx.pool_code);
    let result = bulk_import::import_subnets(
        &fx.tenant.pool, fx.tenant.org_id, &csv, false, ImportMode::Create, None,
    ).await.expect("apply");
    assert_eq!(result.valid, 2);
    assert!(result.applied);
    assert_eq!(fx.count_subnets().await, 2);
}

#[tokio::test]
#[ignore]
async fn subnet_import_rejects_invalid_cidr_without_writing() {
    let Some(pool) = pool_or_skip("subnet_import_rejects_invalid_cidr_without_writing").await else { return; };
    let fx = PoolsFixture::new(pool).await.expect("fixture");

    let csv = format!(
        "{SUBNET_HEADER}\
         BI-SUB-A,Subnet A,10.99.1.0/24,,{p},Free,,Active\r\n\
         BI-SUB-B,Subnet B,not-a-cidr,,{p},Free,,Active\r\n",
        p = fx.pool_code);
    let result = bulk_import::import_subnets(
        &fx.tenant.pool, fx.tenant.org_id, &csv, false, ImportMode::Create, None,
    ).await.expect("apply");
    assert_eq!(result.invalid, 1);
    assert!(!result.applied);
    assert_eq!(fx.count_subnets().await, 0,
        "invalid CIDR must prevent partial writes — atomic across all rows");
}

// ─── VLAN + subnet upsert tests ───────────────────────────────────────────
//
// VLAN + subnet CSVs don't carry a `version` column (only 7 cols vs the
// devices' 8), so upsert always applies against the current DB version —
// stale-version mismatch isn't expressible from a CSV body. The pattern
// matches device upsert in spirit: re-importing the same identifier in
// Upsert mode UPDATES instead of rejecting; new identifiers INSERT.

#[tokio::test]
#[ignore]
async fn vlan_upsert_mode_updates_existing_pair_rather_than_rejecting() {
    let Some(pool) = pool_or_skip("vlan_upsert_mode_updates_existing_pair_rather_than_rejecting").await else { return; };
    let fx = PoolsFixture::new(pool).await.expect("fixture");

    // Seed via Create.
    let csv_initial = format!("{VLAN_HEADER}101,IT,Original desc,Free,,,{},Planned\r\n", fx.block_code);
    bulk_import::import_vlans(
        &fx.tenant.pool, fx.tenant.org_id, &csv_initial, false, ImportMode::Create, None,
    ).await.expect("seed");
    assert_eq!(fx.count_vlans().await, 1);

    // Re-import same (block, vlan_id) with Upsert mode + new
    // display_name + Active status — should UPDATE, not reject.
    let csv_upsert = format!("{VLAN_HEADER}101,IT-Updated,New desc,Free,,,{},Active\r\n", fx.block_code);
    let result = bulk_import::import_vlans(
        &fx.tenant.pool, fx.tenant.org_id, &csv_upsert, false, ImportMode::Upsert, None,
    ).await.expect("upsert");
    assert!(result.applied, "upsert must succeed on existing (block, vlan_id)");
    assert_eq!(fx.count_vlans().await, 1, "upsert must NOT create a duplicate row");

    // Confirm the UPDATE actually changed the row + bumped version.
    let (display_name, status, version): (String, String, i32) = sqlx::query_as(
        "SELECT v.display_name, v.status::text, v.version
           FROM net.vlan v
           JOIN net.vlan_block b ON b.id = v.block_id
          WHERE v.organization_id = $1 AND b.block_code = $2 AND v.vlan_id = 101")
        .bind(fx.tenant.org_id).bind(&fx.block_code)
        .fetch_one(&fx.tenant.pool).await.unwrap();
    assert_eq!(display_name, "IT-Updated");
    assert_eq!(status, "Active");
    assert_eq!(version, 2, "version must bump on upsert UPDATE");
}

#[tokio::test]
#[ignore]
async fn vlan_upsert_mode_creates_new_pair_like_create() {
    let Some(pool) = pool_or_skip("vlan_upsert_mode_creates_new_pair_like_create").await else { return; };
    let fx = PoolsFixture::new(pool).await.expect("fixture");

    let csv = format!("{VLAN_HEADER}205,Servers,,Free,,,{},Active\r\n", fx.block_code);
    let result = bulk_import::import_vlans(
        &fx.tenant.pool, fx.tenant.org_id, &csv, false, ImportMode::Upsert, None,
    ).await.expect("upsert new");
    assert!(result.applied);
    assert_eq!(fx.count_vlans().await, 1,
        "upsert on a new (block, vlan_id) must INSERT, matching create behaviour");
}

#[tokio::test]
#[ignore]
async fn subnet_upsert_mode_updates_existing_code_rather_than_rejecting() {
    let Some(pool) = pool_or_skip("subnet_upsert_mode_updates_existing_code_rather_than_rejecting").await else { return; };
    let fx = PoolsFixture::new(pool).await.expect("fixture");

    // Seed via Create — subnet_code is the unique key per tenant.
    let csv_initial = format!(
        "{SUBNET_HEADER}BI-SUB-A,Subnet A,10.99.1.0/24,,{p},Free,,Planned\r\n",
        p = fx.pool_code);
    bulk_import::import_subnets(
        &fx.tenant.pool, fx.tenant.org_id, &csv_initial, false, ImportMode::Create, None,
    ).await.expect("seed");
    assert_eq!(fx.count_subnets().await, 1);

    // Re-import same subnet_code with new display_name + Active +
    // network re-resized. UPDATE path, not reject.
    let csv_upsert = format!(
        "{SUBNET_HEADER}BI-SUB-A,Subnet A Renamed,10.99.1.0/26,,{p},Free,,Active\r\n",
        p = fx.pool_code);
    let result = bulk_import::import_subnets(
        &fx.tenant.pool, fx.tenant.org_id, &csv_upsert, false, ImportMode::Upsert, None,
    ).await.expect("upsert");
    assert!(result.applied, "upsert must succeed on existing subnet_code");
    assert_eq!(fx.count_subnets().await, 1, "upsert must NOT duplicate the row");

    let (display_name, status, version): (String, String, i32) = sqlx::query_as(
        "SELECT display_name, status::text, version
           FROM net.subnet
          WHERE organization_id = $1 AND subnet_code = 'BI-SUB-A'")
        .bind(fx.tenant.org_id)
        .fetch_one(&fx.tenant.pool).await.unwrap();
    assert_eq!(display_name, "Subnet A Renamed");
    assert_eq!(status, "Active");
    assert_eq!(version, 2);
}

#[tokio::test]
#[ignore]
async fn subnet_upsert_mode_creates_new_code_like_create() {
    let Some(pool) = pool_or_skip("subnet_upsert_mode_creates_new_code_like_create").await else { return; };
    let fx = PoolsFixture::new(pool).await.expect("fixture");

    let csv = format!(
        "{SUBNET_HEADER}BI-SUB-NEW,Brand New,10.99.42.0/24,,{p},Free,,Active\r\n",
        p = fx.pool_code);
    let result = bulk_import::import_subnets(
        &fx.tenant.pool, fx.tenant.org_id, &csv, false, ImportMode::Upsert, None,
    ).await.expect("upsert new");
    assert!(result.applied);
    assert_eq!(fx.count_subnets().await, 1,
        "upsert on a new subnet_code must INSERT, matching create behaviour");
}

// ─── Subnet scope_entity_code resolution (Phase 10 Floor/Room) ───────────
//
// The scope_entity_code column on the CSV resolves to net.subnet.scope_entity_id
// per scope_level: Building → BUILDING_CODE (globally unique), Floor →
// BUILDING_CODE/FLOOR_CODE (compound because floor_code is only unique
// within its building), Room → BUILDING_CODE/FLOOR_CODE/ROOM_CODE.

/// Seeds a floor + room under the existing 'IT-B' building so
/// scope-aware subnet imports resolve. Reuses PoolsFixture so the
/// VLAN + IP pool scaffolding stays available.
async fn seed_hierarchy_on_pools_fixture(fx: &PoolsFixture) -> sqlx::Result<(String, String, String)> {
    let (building_id,): (Uuid,) = sqlx::query_as(
        "SELECT id FROM net.building
          WHERE organization_id = $1 AND building_code = 'IT-B' AND deleted_at IS NULL")
        .bind(fx.tenant.org_id).fetch_one(&fx.tenant.pool).await?;
    let (floor_id,): (Uuid,) = sqlx::query_as(
        "INSERT INTO net.floor (organization_id, building_id, floor_code, display_name,
                                floor_number, status)
         VALUES ($1, $2, 'F1', 'First Floor', 1, 'Active')
         RETURNING id")
        .bind(fx.tenant.org_id).bind(building_id).fetch_one(&fx.tenant.pool).await?;
    sqlx::query(
        "INSERT INTO net.room (organization_id, floor_id, room_code, display_name, status)
         VALUES ($1, $2, 'R1', 'Server Room 1', 'Active')")
        .bind(fx.tenant.org_id).bind(floor_id).execute(&fx.tenant.pool).await?;
    let _ = floor_id;  // consumed via the INSERT above
    Ok(("IT-B".into(), "F1".into(), "R1".into()))
}

#[tokio::test]
#[ignore]
async fn subnet_region_scope_resolves_single_token_code() {
    let Some(pool) = pool_or_skip("subnet_region_scope_resolves_single_token_code").await else { return; };
    let fx = PoolsFixture::new(pool).await.expect("fixture");

    // The parent TenantFixture seeds region 'IT-R' for every tenant,
    // so Region scope is resolvable without an extra fixture step.
    let csv = format!(
        "{SUBNET_HEADER}BI-SUB-RG,Region Subnet,10.99.10.0/24,,{p},Region,IT-R,Active\r\n",
        p = fx.pool_code);
    let result = bulk_import::import_subnets(
        &fx.tenant.pool, fx.tenant.org_id, &csv, false, ImportMode::Create, None,
    ).await.expect("apply");
    assert!(result.applied);

    let (scope_level, scope_entity_id): (String, Option<Uuid>) = sqlx::query_as(
        "SELECT scope_level, scope_entity_id FROM net.subnet
          WHERE organization_id = $1 AND subnet_code = 'BI-SUB-RG'")
        .bind(fx.tenant.org_id).fetch_one(&fx.tenant.pool).await.unwrap();
    assert_eq!(scope_level, "Region");
    let (expected_region_id,): (Uuid,) = sqlx::query_as(
        "SELECT id FROM net.region
          WHERE organization_id = $1 AND region_code = 'IT-R'")
        .bind(fx.tenant.org_id).fetch_one(&fx.tenant.pool).await.unwrap();
    assert_eq!(scope_entity_id, Some(expected_region_id));
}

#[tokio::test]
#[ignore]
async fn subnet_site_scope_resolves_compound_code() {
    let Some(pool) = pool_or_skip("subnet_site_scope_resolves_compound_code").await else { return; };
    let fx = PoolsFixture::new(pool).await.expect("fixture");

    // Parent TenantFixture seeds region 'IT-R' + site 'IT-S'.
    let csv = format!(
        "{SUBNET_HEADER}BI-SUB-ST,Site Subnet,10.99.11.0/24,,{p},Site,IT-R/IT-S,Active\r\n",
        p = fx.pool_code);
    let result = bulk_import::import_subnets(
        &fx.tenant.pool, fx.tenant.org_id, &csv, false, ImportMode::Create, None,
    ).await.expect("apply");
    assert!(result.applied);

    let (scope_entity_id,): (Option<Uuid>,) = sqlx::query_as(
        "SELECT scope_entity_id FROM net.subnet
          WHERE organization_id = $1 AND subnet_code = 'BI-SUB-ST'")
        .bind(fx.tenant.org_id).fetch_one(&fx.tenant.pool).await.unwrap();
    let (expected_site_id,): (Uuid,) = sqlx::query_as(
        "SELECT s.id FROM net.site s
           JOIN net.region r ON r.id = s.region_id
          WHERE s.organization_id = $1 AND r.region_code = 'IT-R' AND s.site_code = 'IT-S'")
        .bind(fx.tenant.org_id).fetch_one(&fx.tenant.pool).await.unwrap();
    assert_eq!(scope_entity_id, Some(expected_site_id));
}

#[tokio::test]
#[ignore]
async fn subnet_site_scope_rejects_single_token() {
    let Some(pool) = pool_or_skip("subnet_site_scope_rejects_single_token").await else { return; };
    let fx = PoolsFixture::new(pool).await.expect("fixture");

    // Site scope needs REGION_CODE/SITE_CODE — a bare site code is
    // ambiguous (same site_code can exist in multiple regions) so
    // the importer rejects rather than guessing.
    let csv = format!(
        "{SUBNET_HEADER}BI-SUB-X,Subnet X,10.99.1.0/24,,{p},Site,IT-S,Active\r\n",
        p = fx.pool_code);
    let result = bulk_import::import_subnets(
        &fx.tenant.pool, fx.tenant.org_id, &csv, false, ImportMode::Create, None,
    ).await.expect("apply");
    assert!(!result.applied);
    assert!(result.outcomes[0].errors.iter().any(|e| e.contains("REGION_CODE/SITE_CODE")),
        "compound-shape error expected for bare Site code: {:?}", result.outcomes[0].errors);
}

#[tokio::test]
#[ignore]
async fn subnet_region_scope_rejects_compound_code() {
    let Some(pool) = pool_or_skip("subnet_region_scope_rejects_compound_code").await else { return; };
    let fx = PoolsFixture::new(pool).await.expect("fixture");

    // Region scope wants a single token. Passing "IT-R/IT-S" for
    // Region is likely an operator-accidentally-picked-wrong-level
    // error and should be flagged rather than silently truncated.
    let csv = format!(
        "{SUBNET_HEADER}BI-SUB-X,Subnet X,10.99.1.0/24,,{p},Region,IT-R/IT-S,Active\r\n",
        p = fx.pool_code);
    let result = bulk_import::import_subnets(
        &fx.tenant.pool, fx.tenant.org_id, &csv, false, ImportMode::Create, None,
    ).await.expect("apply");
    assert!(!result.applied);
    assert!(result.outcomes[0].errors.iter().any(|e| e.contains("REGION_CODE (single token)")),
        "single-token error expected for compound Region code: {:?}", result.outcomes[0].errors);
}

#[tokio::test]
#[ignore]
async fn subnet_all_five_scopes_round_trip_through_export() {
    let Some(pool) = pool_or_skip("subnet_all_five_scopes_round_trip_through_export").await else { return; };
    let fx = PoolsFixture::new(pool).await.expect("fixture");
    let _ = seed_hierarchy_on_pools_fixture(&fx).await.expect("seed hierarchy");

    // Seed one subnet per non-Free scope — Region, Site, Building,
    // Floor, Room — and verify the export emits each compound code
    // in the expected shape.
    let csv = format!(
        "{SUBNET_HEADER}\
         RT-RG,Region Subnet,10.98.1.0/24,,{p},Region,IT-R,Active\r\n\
         RT-ST,Site Subnet,10.98.2.0/24,,{p},Site,IT-R/IT-S,Active\r\n\
         RT-BLD,Building Subnet,10.98.3.0/24,,{p},Building,IT-B,Active\r\n\
         RT-FLR,Floor Subnet,10.98.4.0/24,,{p},Floor,IT-B/F1,Active\r\n\
         RT-RM,Room Subnet,10.98.5.0/24,,{p},Room,IT-B/F1/R1,Active\r\n",
        p = fx.pool_code);
    bulk_import::import_subnets(
        &fx.tenant.pool, fx.tenant.org_id, &csv, false, ImportMode::Create, None,
    ).await.expect("seed");

    let exported = bulk_export::export_subnets_csv(&fx.tenant.pool, fx.tenant.org_id).await
        .expect("export");
    assert!(exported.contains("Region,IT-R,Active"),
        "Region-scope row must emit REGION_CODE as scope_entity_code; got:\n{exported}");
    assert!(exported.contains("Site,IT-R/IT-S,Active"),
        "Site-scope row must emit REGION/SITE; got:\n{exported}");
    assert!(exported.contains("Building,IT-B,Active"));
    assert!(exported.contains("Floor,IT-B/F1,Active"));
    assert!(exported.contains("Room,IT-B/F1/R1,Active"));

    // Re-import as Upsert — every scope_entity_code shape must
    // round-trip without validation errors.
    let result = bulk_import::import_subnets(
        &fx.tenant.pool, fx.tenant.org_id, &exported, false, ImportMode::Upsert, None,
    ).await.expect("re-import");
    assert!(result.applied, "all five scopes must round-trip cleanly");
}

#[tokio::test]
#[ignore]
async fn subnet_building_scope_resolves_and_writes_entity_id() {
    let Some(pool) = pool_or_skip("subnet_building_scope_resolves_and_writes_entity_id").await else { return; };
    let fx = PoolsFixture::new(pool).await.expect("fixture");
    let _ = seed_hierarchy_on_pools_fixture(&fx).await.expect("seed hierarchy");

    let csv = format!(
        "{SUBNET_HEADER}BI-SUB-BLD,Building Subnet,10.99.20.0/24,,{p},Building,IT-B,Active\r\n",
        p = fx.pool_code);
    let result = bulk_import::import_subnets(
        &fx.tenant.pool, fx.tenant.org_id, &csv, false, ImportMode::Create, None,
    ).await.expect("apply");
    assert!(result.applied, "happy path applied");
    assert_eq!(result.valid, 1);

    let (scope_level, scope_entity_id): (String, Option<Uuid>) = sqlx::query_as(
        "SELECT scope_level, scope_entity_id FROM net.subnet
          WHERE organization_id = $1 AND subnet_code = 'BI-SUB-BLD'")
        .bind(fx.tenant.org_id).fetch_one(&fx.tenant.pool).await.unwrap();
    assert_eq!(scope_level, "Building");
    assert!(scope_entity_id.is_some(), "scope_entity_id must be resolved to the building uuid");
}

#[tokio::test]
#[ignore]
async fn subnet_floor_scope_resolves_compound_code() {
    let Some(pool) = pool_or_skip("subnet_floor_scope_resolves_compound_code").await else { return; };
    let fx = PoolsFixture::new(pool).await.expect("fixture");
    let _ = seed_hierarchy_on_pools_fixture(&fx).await.expect("seed hierarchy");

    let csv = format!(
        "{SUBNET_HEADER}BI-SUB-FLR,Floor Subnet,10.99.30.0/24,,{p},Floor,IT-B/F1,Active\r\n",
        p = fx.pool_code);
    let result = bulk_import::import_subnets(
        &fx.tenant.pool, fx.tenant.org_id, &csv, false, ImportMode::Create, None,
    ).await.expect("apply");
    assert!(result.applied);

    let (scope_level, scope_entity_id): (String, Option<Uuid>) = sqlx::query_as(
        "SELECT scope_level, scope_entity_id FROM net.subnet
          WHERE organization_id = $1 AND subnet_code = 'BI-SUB-FLR'")
        .bind(fx.tenant.org_id).fetch_one(&fx.tenant.pool).await.unwrap();
    assert_eq!(scope_level, "Floor");
    // The resolved uuid should match net.floor.id for (IT-B, F1).
    let (expected_floor_id,): (Uuid,) = sqlx::query_as(
        "SELECT f.id FROM net.floor f
           JOIN net.building b ON b.id = f.building_id
          WHERE f.organization_id = $1 AND b.building_code = 'IT-B' AND f.floor_code = 'F1'")
        .bind(fx.tenant.org_id).fetch_one(&fx.tenant.pool).await.unwrap();
    assert_eq!(scope_entity_id, Some(expected_floor_id));
}

#[tokio::test]
#[ignore]
async fn subnet_room_scope_resolves_three_part_code() {
    let Some(pool) = pool_or_skip("subnet_room_scope_resolves_three_part_code").await else { return; };
    let fx = PoolsFixture::new(pool).await.expect("fixture");
    let _ = seed_hierarchy_on_pools_fixture(&fx).await.expect("seed hierarchy");

    let csv = format!(
        "{SUBNET_HEADER}BI-SUB-RM,Room Subnet,10.99.40.0/24,,{p},Room,IT-B/F1/R1,Active\r\n",
        p = fx.pool_code);
    let result = bulk_import::import_subnets(
        &fx.tenant.pool, fx.tenant.org_id, &csv, false, ImportMode::Create, None,
    ).await.expect("apply");
    assert!(result.applied);

    let (scope_level, scope_entity_id): (String, Option<Uuid>) = sqlx::query_as(
        "SELECT scope_level, scope_entity_id FROM net.subnet
          WHERE organization_id = $1 AND subnet_code = 'BI-SUB-RM'")
        .bind(fx.tenant.org_id).fetch_one(&fx.tenant.pool).await.unwrap();
    assert_eq!(scope_level, "Room");
    assert!(scope_entity_id.is_some());
}

#[tokio::test]
#[ignore]
async fn subnet_building_scope_rejects_unknown_building_code() {
    let Some(pool) = pool_or_skip("subnet_building_scope_rejects_unknown_building_code").await else { return; };
    let fx = PoolsFixture::new(pool).await.expect("fixture");

    let csv = format!(
        "{SUBNET_HEADER}BI-SUB-X,Subnet X,10.99.1.0/24,,{p},Building,NO-SUCH-BLDG,Active\r\n",
        p = fx.pool_code);
    let result = bulk_import::import_subnets(
        &fx.tenant.pool, fx.tenant.org_id, &csv, false, ImportMode::Create, None,
    ).await.expect("apply");
    assert!(!result.applied);
    assert_eq!(result.invalid, 1);
    assert!(result.outcomes[0].errors.iter().any(|e| e.contains("building catalog")),
        "unknown building_code should surface: {:?}", result.outcomes[0].errors);
    assert_eq!(fx.count_subnets().await, 0);
}

#[tokio::test]
#[ignore]
async fn subnet_floor_scope_rejects_missing_slash() {
    let Some(pool) = pool_or_skip("subnet_floor_scope_rejects_missing_slash").await else { return; };
    let fx = PoolsFixture::new(pool).await.expect("fixture");
    let _ = seed_hierarchy_on_pools_fixture(&fx).await.expect("seed hierarchy");

    // Floor scope needs BUILDING/FLOOR; giving just a floor code
    // should be rejected, not silently treated as a building code.
    let csv = format!(
        "{SUBNET_HEADER}BI-SUB-X,Subnet X,10.99.1.0/24,,{p},Floor,F1,Active\r\n",
        p = fx.pool_code);
    let result = bulk_import::import_subnets(
        &fx.tenant.pool, fx.tenant.org_id, &csv, false, ImportMode::Create, None,
    ).await.expect("apply");
    assert!(!result.applied);
    assert!(result.outcomes[0].errors.iter().any(|e| e.contains("BUILDING_CODE/FLOOR_CODE")),
        "compound-shape error expected: {:?}", result.outcomes[0].errors);
}

#[tokio::test]
#[ignore]
async fn subnet_free_scope_rejects_non_empty_scope_entity_code() {
    let Some(pool) = pool_or_skip("subnet_free_scope_rejects_non_empty_scope_entity_code").await else { return; };
    let fx = PoolsFixture::new(pool).await.expect("fixture");
    let _ = seed_hierarchy_on_pools_fixture(&fx).await.expect("seed hierarchy");

    // scope_level Free + scope_entity_code set is an operator error
    // — the importer flags the mismatch rather than silently
    // ignoring the code.
    let csv = format!(
        "{SUBNET_HEADER}BI-SUB-X,Subnet X,10.99.1.0/24,,{p},Free,IT-B,Active\r\n",
        p = fx.pool_code);
    let result = bulk_import::import_subnets(
        &fx.tenant.pool, fx.tenant.org_id, &csv, false, ImportMode::Create, None,
    ).await.expect("apply");
    assert!(!result.applied);
    assert!(result.outcomes[0].errors.iter().any(|e| e.contains("scope_entity_code 'IT-B' set but scope_level is Free")),
        "Free-with-code error expected: {:?}", result.outcomes[0].errors);
}

#[tokio::test]
#[ignore]
async fn subnet_building_scope_round_trips_through_export() {
    let Some(pool) = pool_or_skip("subnet_building_scope_round_trips_through_export").await else { return; };
    let fx = PoolsFixture::new(pool).await.expect("fixture");
    let _ = seed_hierarchy_on_pools_fixture(&fx).await.expect("seed hierarchy");

    // Seed via import.
    let csv = format!(
        "{SUBNET_HEADER}RT-BLD,Building Subnet,10.99.20.0/24,,{p},Building,IT-B,Active\r\n\
         RT-FLR,Floor Subnet,10.99.30.0/24,,{p},Floor,IT-B/F1,Active\r\n\
         RT-RM,Room Subnet,10.99.40.0/24,,{p},Room,IT-B/F1/R1,Active\r\n",
        p = fx.pool_code);
    bulk_import::import_subnets(
        &fx.tenant.pool, fx.tenant.org_id, &csv, false, ImportMode::Create, None,
    ).await.expect("seed");

    // Export and assert the compound scope_entity_code column landed
    // in the emitted CSV. This is the round-trip guarantee —
    // export/import agree on scope_entity_code shape per scope_level.
    let exported = bulk_export::export_subnets_csv(&fx.tenant.pool, fx.tenant.org_id).await
        .expect("export");
    assert!(exported.contains("Building,IT-B,Active"),
        "Building-scope row must emit BUILDING_CODE as scope_entity_code; got:\n{exported}");
    assert!(exported.contains("Floor,IT-B/F1,Active"),
        "Floor-scope row must emit BUILDING/FLOOR as scope_entity_code; got:\n{exported}");
    assert!(exported.contains("Room,IT-B/F1/R1,Active"),
        "Room-scope row must emit BUILDING/FLOOR/ROOM as scope_entity_code; got:\n{exported}");

    // Re-import the export as Upsert: should be a no-op (matching
    // rows already exist) + must not error on the compound codes.
    let result = bulk_import::import_subnets(
        &fx.tenant.pool, fx.tenant.org_id, &exported, false, ImportMode::Upsert, None,
    ).await.expect("re-import");
    assert!(result.applied, "upsert round-trip must apply cleanly");
}

// ─── VLAN scope_entity_code resolution (Phase 10 — all 5 scope levels) ──
//
// Same compound-code grammar as the subnet importer but with VLAN's
// Device scope (per migration 086 CHECK: Free/Region/Site/Building/Device).
// Device scope is single-token (hostname unique per tenant).

#[tokio::test]
#[ignore]
async fn vlan_region_scope_resolves_single_token_code() {
    let Some(pool) = pool_or_skip("vlan_region_scope_resolves_single_token_code").await else { return; };
    let fx = PoolsFixture::new(pool).await.expect("fixture");

    // TenantFixture seeds region 'IT-R' for every tenant.
    let csv = format!("{VLAN_HEADER}301,RegionVlan,,Region,IT-R,,{b},Active\r\n",
                      b = fx.block_code);
    let result = bulk_import::import_vlans(
        &fx.tenant.pool, fx.tenant.org_id, &csv, false, ImportMode::Create, None,
    ).await.expect("apply");
    assert!(result.applied);

    let (scope_level, scope_entity_id): (String, Option<Uuid>) = sqlx::query_as(
        "SELECT scope_level, scope_entity_id FROM net.vlan
          WHERE organization_id = $1 AND vlan_id = 301")
        .bind(fx.tenant.org_id).fetch_one(&fx.tenant.pool).await.unwrap();
    assert_eq!(scope_level, "Region");
    assert!(scope_entity_id.is_some(), "Region-scoped VLAN must carry scope_entity_id");
}

#[tokio::test]
#[ignore]
async fn vlan_site_scope_resolves_compound_code() {
    let Some(pool) = pool_or_skip("vlan_site_scope_resolves_compound_code").await else { return; };
    let fx = PoolsFixture::new(pool).await.expect("fixture");

    let csv = format!("{VLAN_HEADER}302,SiteVlan,,Site,IT-R/IT-S,,{b},Active\r\n",
                      b = fx.block_code);
    let result = bulk_import::import_vlans(
        &fx.tenant.pool, fx.tenant.org_id, &csv, false, ImportMode::Create, None,
    ).await.expect("apply");
    assert!(result.applied);
}

#[tokio::test]
#[ignore]
async fn vlan_building_scope_resolves() {
    let Some(pool) = pool_or_skip("vlan_building_scope_resolves").await else { return; };
    let fx = PoolsFixture::new(pool).await.expect("fixture");

    let csv = format!("{VLAN_HEADER}303,BldgVlan,,Building,IT-B,,{b},Active\r\n",
                      b = fx.block_code);
    let result = bulk_import::import_vlans(
        &fx.tenant.pool, fx.tenant.org_id, &csv, false, ImportMode::Create, None,
    ).await.expect("apply");
    assert!(result.applied);
}

#[tokio::test]
#[ignore]
async fn vlan_device_scope_resolves_by_hostname() {
    let Some(pool) = pool_or_skip("vlan_device_scope_resolves_by_hostname").await else { return; };
    let fx = PoolsFixture::new(pool).await.expect("fixture");

    // Seed a device so Device scope has a resolvable hostname.
    let (role_id,): (Uuid,) = sqlx::query_as(
        "SELECT id FROM net.device_role
          WHERE organization_id = $1 AND role_code = 'Core' AND deleted_at IS NULL")
        .bind(fx.tenant.org_id).fetch_one(&fx.tenant.pool).await.unwrap();
    let (building_id,): (Uuid,) = sqlx::query_as(
        "SELECT id FROM net.building
          WHERE organization_id = $1 AND building_code = 'IT-B' AND deleted_at IS NULL")
        .bind(fx.tenant.org_id).fetch_one(&fx.tenant.pool).await.unwrap();
    sqlx::query(
        "INSERT INTO net.device (organization_id, device_role_id, building_id,
                                 hostname, status)
         VALUES ($1, $2, $3, 'DEV-VLAN-SCOPE', 'Active')")
        .bind(fx.tenant.org_id).bind(role_id).bind(building_id)
        .execute(&fx.tenant.pool).await.unwrap();

    let csv = format!("{VLAN_HEADER}304,DevVlan,,Device,DEV-VLAN-SCOPE,,{b},Active\r\n",
                      b = fx.block_code);
    let result = bulk_import::import_vlans(
        &fx.tenant.pool, fx.tenant.org_id, &csv, false, ImportMode::Create, None,
    ).await.expect("apply");
    assert!(result.applied);

    let (scope_entity_id,): (Option<Uuid>,) = sqlx::query_as(
        "SELECT scope_entity_id FROM net.vlan
          WHERE organization_id = $1 AND vlan_id = 304")
        .bind(fx.tenant.org_id).fetch_one(&fx.tenant.pool).await.unwrap();
    let (expected,): (Uuid,) = sqlx::query_as(
        "SELECT id FROM net.device
          WHERE organization_id = $1 AND hostname = 'DEV-VLAN-SCOPE'")
        .bind(fx.tenant.org_id).fetch_one(&fx.tenant.pool).await.unwrap();
    assert_eq!(scope_entity_id, Some(expected),
        "scope_entity_id must resolve to the device's uuid");
}

#[tokio::test]
#[ignore]
async fn vlan_device_scope_rejects_unknown_hostname() {
    let Some(pool) = pool_or_skip("vlan_device_scope_rejects_unknown_hostname").await else { return; };
    let fx = PoolsFixture::new(pool).await.expect("fixture");

    let csv = format!("{VLAN_HEADER}305,Ghost,,Device,NO-SUCH-HOST,,{b},Active\r\n",
                      b = fx.block_code);
    let result = bulk_import::import_vlans(
        &fx.tenant.pool, fx.tenant.org_id, &csv, false, ImportMode::Create, None,
    ).await.expect("apply");
    assert!(!result.applied);
    assert!(result.outcomes[0].errors.iter().any(|e| e.contains("device catalog")),
        "unknown hostname should surface: {:?}", result.outcomes[0].errors);
}

#[tokio::test]
#[ignore]
async fn vlan_free_scope_rejects_non_empty_code() {
    let Some(pool) = pool_or_skip("vlan_free_scope_rejects_non_empty_code").await else { return; };
    let fx = PoolsFixture::new(pool).await.expect("fixture");

    // Free + code is operator error — flag it rather than silently
    // ignore (matches the subnet importer's behaviour).
    let csv = format!("{VLAN_HEADER}306,Mix,,Free,IT-R,,{b},Active\r\n",
                      b = fx.block_code);
    let result = bulk_import::import_vlans(
        &fx.tenant.pool, fx.tenant.org_id, &csv, false, ImportMode::Create, None,
    ).await.expect("apply");
    assert!(!result.applied);
    assert!(result.outcomes[0].errors.iter().any(|e| e.contains("scope_level is Free")),
        "Free-with-code error expected: {:?}", result.outcomes[0].errors);
}

#[tokio::test]
#[ignore]
async fn vlan_all_five_scopes_round_trip_through_export() {
    let Some(pool) = pool_or_skip("vlan_all_five_scopes_round_trip_through_export").await else { return; };
    let fx = PoolsFixture::new(pool).await.expect("fixture");

    // Seed a device for the Device-scope row to reference.
    let (role_id,): (Uuid,) = sqlx::query_as(
        "SELECT id FROM net.device_role
          WHERE organization_id = $1 AND role_code = 'Core' AND deleted_at IS NULL")
        .bind(fx.tenant.org_id).fetch_one(&fx.tenant.pool).await.unwrap();
    let (building_id,): (Uuid,) = sqlx::query_as(
        "SELECT id FROM net.building
          WHERE organization_id = $1 AND building_code = 'IT-B' AND deleted_at IS NULL")
        .bind(fx.tenant.org_id).fetch_one(&fx.tenant.pool).await.unwrap();
    sqlx::query(
        "INSERT INTO net.device (organization_id, device_role_id, building_id,
                                 hostname, status)
         VALUES ($1, $2, $3, 'DEV-RT', 'Active')")
        .bind(fx.tenant.org_id).bind(role_id).bind(building_id)
        .execute(&fx.tenant.pool).await.unwrap();

    let csv = format!("{VLAN_HEADER}\
         310,Free-RT,,Free,,,{b},Active\r\n\
         311,Reg-RT,,Region,IT-R,,{b},Active\r\n\
         312,Site-RT,,Site,IT-R/IT-S,,{b},Active\r\n\
         313,Bldg-RT,,Building,IT-B,,{b},Active\r\n\
         314,Dev-RT,,Device,DEV-RT,,{b},Active\r\n",
                      b = fx.block_code);
    bulk_import::import_vlans(
        &fx.tenant.pool, fx.tenant.org_id, &csv, false, ImportMode::Create, None,
    ).await.expect("seed");

    let exported = bulk_export::export_vlans_csv(&fx.tenant.pool, fx.tenant.org_id).await
        .expect("export");
    assert!(exported.contains("Region,IT-R,"),
        "Region scope must emit REGION_CODE; got:\n{exported}");
    assert!(exported.contains("Site,IT-R/IT-S,"),
        "Site scope must emit REGION/SITE; got:\n{exported}");
    assert!(exported.contains("Building,IT-B,"));
    assert!(exported.contains("Device,DEV-RT,"));

    // Re-import as Upsert — every compound shape must round-trip
    // without validation errors.
    let result = bulk_import::import_vlans(
        &fx.tenant.pool, fx.tenant.org_id, &exported, false, ImportMode::Upsert, None,
    ).await.expect("re-import");
    assert!(result.applied, "all five VLAN scopes must round-trip cleanly");
}

#[tokio::test]
#[ignore]
async fn apply_rejects_existing_hostname_as_row_error() {
    let Some(pool) = pool_or_skip("apply_rejects_existing_hostname_as_row_error").await else { return; };
    let fx = TenantFixture::new(pool).await.expect("fixture");

    // Pre-seed a device so the import row hits "already exists".
    let csv1 = format!("{HEADER}IT-B-CORE01,Core,IT-B,IT-S,10.99.0.1/24,65100,Active,1\r\n");
    bulk_import::import_devices(&fx.pool, fx.org_id, &csv1, false, ImportMode::Create, None)
        .await.expect("seed apply");
    assert_eq!(fx.count_devices().await, 1);

    // Re-import the same hostname — should be flagged as an existing
    // row and NOT applied.
    let result = bulk_import::import_devices(&fx.pool, fx.org_id, &csv1, false, ImportMode::Create, None)
        .await.expect("re-apply");
    assert_eq!(result.invalid, 1);
    assert!(!result.applied);
    assert!(result.outcomes[0].errors.iter().any(|e| e.contains("already exists")),
        "outcome should explain the duplicate: {:?}", result.outcomes[0].errors);
    assert_eq!(fx.count_devices().await, 1,
        "no new device should have been added");
}

// ─── RBAC enforcement ────────────────────────────────────────────────────
//
// Each bulk_import entity-point now requires `write:entity_type` when
// an X-User-Id is present. None (service calls) bypasses so internal
// seeding flows still work.

#[tokio::test]
#[ignore]
async fn device_import_forbidden_without_write_grant() {
    let Some(pool) = pool_or_skip("device_import_forbidden_without_write_grant").await else { return; };
    let fx = TenantFixture::new(pool).await.expect("fixture");

    let csv = format!("{HEADER}IT-B-CORE99,Core,IT-B,IT-S,10.99.0.9/24,65199,Active,1\r\n");
    let err = bulk_import::import_devices(
        &fx.pool, fx.org_id, &csv, false, ImportMode::Create, Some(42)
    ).await.unwrap_err().to_string();
    assert!(err.contains("Forbidden"), "expected Forbidden err: {err}");
    assert_eq!(fx.count_devices().await, 0,
        "denied import must not touch DB");
}

// ─── Upsert mode ─────────────────────────────────────────────────────────
//
// Default mode is Create — proven by the pre-existing tests.
// These exercise the Upsert branch: existing hostnames UPDATE;
// missing hostnames INSERT; version-checked updates surface stale
// CSV versions as row-level errors that roll back the whole batch.

#[tokio::test]
#[ignore]
async fn upsert_mode_updates_existing_device_rather_than_rejecting() {
    let Some(pool) = pool_or_skip("upsert_mode_updates_existing_device_rather_than_rejecting").await else { return; };
    let fx = TenantFixture::new(pool).await.expect("fixture");

    // Seed one device via Create mode.
    let csv_initial = format!("{HEADER}IT-B-UPSERT01,Core,IT-B,IT-S,10.99.9.1/24,65100,Planned,1\r\n");
    bulk_import::import_devices(&fx.pool, fx.org_id, &csv_initial, false, ImportMode::Create, None)
        .await.expect("seed");
    assert_eq!(fx.count_devices().await, 1);

    // Re-import the SAME hostname with Active status + Upsert mode —
    // should UPDATE, not reject.
    let csv_upsert = format!("{HEADER}IT-B-UPSERT01,Core,IT-B,IT-S,10.99.9.1/24,65100,Active,1\r\n");
    let result = bulk_import::import_devices(
        &fx.pool, fx.org_id, &csv_upsert, false, ImportMode::Upsert, None
    ).await.expect("upsert");
    assert!(result.applied, "upsert must succeed on existing hostname (not reject)");
    assert_eq!(fx.count_devices().await, 1,
        "upsert update must NOT create a duplicate row");

    // Verify the UPDATE actually changed status.
    let (status, version): (String, i32) = sqlx::query_as(
        "SELECT status::text, version FROM net.device
          WHERE organization_id = $1 AND hostname = 'IT-B-UPSERT01'")
        .bind(fx.org_id).fetch_one(&fx.pool).await.unwrap();
    assert_eq!(status, "Active", "status should be Active after upsert");
    assert_eq!(version, 2, "version must bump on upsert UPDATE");
}

#[tokio::test]
#[ignore]
async fn upsert_mode_creates_new_hostname_like_create() {
    let Some(pool) = pool_or_skip("upsert_mode_creates_new_hostname_like_create").await else { return; };
    let fx = TenantFixture::new(pool).await.expect("fixture");

    let csv = format!("{HEADER}IT-B-NEW42,Core,IT-B,IT-S,10.99.42.1/24,65142,Active,1\r\n");
    let result = bulk_import::import_devices(
        &fx.pool, fx.org_id, &csv, false, ImportMode::Upsert, None
    ).await.expect("upsert new");
    assert!(result.applied);
    assert_eq!(fx.count_devices().await, 1,
        "upsert on a new hostname must INSERT, matching create behaviour");
}

#[tokio::test]
#[ignore]
async fn upsert_mode_stale_csv_version_rolls_back_batch() {
    let Some(pool) = pool_or_skip("upsert_mode_stale_csv_version_rolls_back_batch").await else { return; };
    let fx = TenantFixture::new(pool).await.expect("fixture");

    // Seed
    let csv_initial = format!("{HEADER}IT-B-STALE,Core,IT-B,IT-S,10.99.9.1/24,65100,Planned,1\r\n");
    bulk_import::import_devices(&fx.pool, fx.org_id, &csv_initial, false, ImportMode::Create, None)
        .await.expect("seed");

    // Simulate a concurrent writer bumping the version via CRUD.
    sqlx::query("UPDATE net.device
                    SET status = 'Active'::net.entity_status,
                        version = version + 1
                  WHERE organization_id = $1 AND hostname = 'IT-B-STALE'")
        .bind(fx.org_id).execute(&fx.pool).await.unwrap();

    // Operator tries to upsert based on version=1 (stale) — should
    // surface a clear version-mismatch error and roll back.
    let csv_stale = format!("{HEADER}IT-B-STALE,Core,IT-B,IT-S,10.99.9.1/24,65100,Retired,1\r\n");
    let result = bulk_import::import_devices(
        &fx.pool, fx.org_id, &csv_stale, false, ImportMode::Upsert, None
    ).await.expect("upsert call succeeds but apply fails");
    assert!(!result.applied, "stale version must block apply");
    assert!(result.outcomes[0].errors.iter().any(|e| e.contains("version mismatch")),
        "stale version should surface as version mismatch: {:?}",
        result.outcomes[0].errors);

    // Operator's attempted Retired update must not have landed.
    let (status,): (String,) = sqlx::query_as(
        "SELECT status::text FROM net.device
          WHERE organization_id = $1 AND hostname = 'IT-B-STALE'")
        .bind(fx.org_id).fetch_one(&fx.pool).await.unwrap();
    assert_eq!(status, "Active",
        "concurrent writer's Active status must survive; stale upsert didn't overwrite it");
}

#[tokio::test]
#[ignore]
async fn upsert_mode_empty_version_column_skips_version_check() {
    // Operators hand-writing CSVs (rather than round-tripping an
    // export) often leave the version column blank. The validator
    // accepts blank version in create mode; upsert treats blank as
    // "I don't know — just apply to current version" rather than
    // failing with a parse error.
    let Some(pool) = pool_or_skip("upsert_mode_empty_version_column_skips_version_check").await else { return; };
    let fx = TenantFixture::new(pool).await.expect("fixture");

    let csv_initial = format!("{HEADER}IT-B-BLANK,Core,IT-B,IT-S,10.99.9.1/24,65100,Planned,1\r\n");
    bulk_import::import_devices(&fx.pool, fx.org_id, &csv_initial, false, ImportMode::Create, None)
        .await.expect("seed");

    // CSV with empty version column — note the trailing `,,\r\n` region.
    let csv_blank = "hostname,role_code,building_code,site_code,management_ip,asn,status,version\r\n\
                     IT-B-BLANK,Core,IT-B,IT-S,10.99.9.1/24,65100,Active,\r\n";
    let result = bulk_import::import_devices(
        &fx.pool, fx.org_id, csv_blank, false, ImportMode::Upsert, None
    ).await.expect("upsert with blank version");
    assert!(result.applied,
        "empty version must accept current DB version as the snapshot, not fail parse");
    let (status,): (String,) = sqlx::query_as(
        "SELECT status::text FROM net.device
          WHERE organization_id = $1 AND hostname = 'IT-B-BLANK'")
        .bind(fx.org_id).fetch_one(&fx.pool).await.unwrap();
    assert_eq!(status, "Active");
}

#[tokio::test]
#[ignore]
async fn import_mode_parse_rejects_unknown_mode_string() {
    // ImportMode::parse is public — test directly without DB.
    let err = bulk_import::ImportMode::parse(Some("upser")).unwrap_err().to_string();
    assert!(err.contains("mode"), "err: {err}");
}

#[tokio::test]
#[ignore]
async fn device_import_allowed_with_global_write_grant() {
    let Some(pool) = pool_or_skip("device_import_allowed_with_global_write_grant").await else { return; };
    let fx = TenantFixture::new(pool).await.expect("fixture");

    ScopeGrantRepo::new(fx.pool.clone()).create(&CreateScopeGrantBody {
        organization_id: fx.org_id,
        user_id: 42,
        action: "write".into(),
        entity_type: "Device".into(),
        scope_type: "Global".into(),
        scope_entity_id: None,
        notes: None,
    }, Some(99)).await.expect("seed grant");

    let csv = format!("{HEADER}IT-B-CORE50,Core,IT-B,IT-S,10.99.0.5/24,65150,Active,1\r\n");
    let result = bulk_import::import_devices(
        &fx.pool, fx.org_id, &csv, false, ImportMode::Create, Some(42)
    ).await.expect("allowed import");
    assert!(result.applied);
    assert_eq!(fx.count_devices().await, 1);
}

#[tokio::test]
#[ignore]
async fn vlan_import_forbidden_without_write_vlan_grant() {
    let Some(pool) = pool_or_skip("vlan_import_forbidden_without_write_vlan_grant").await else { return; };
    let fx = PoolsFixture::new(pool).await.expect("fixture");

    let csv = format!("{VLAN_HEADER}101,IT,,Free,,,{},Active\r\n", fx.block_code);
    let err = bulk_import::import_vlans(
        &fx.tenant.pool, fx.tenant.org_id, &csv, false, ImportMode::Create, Some(42)
    ).await.unwrap_err().to_string();
    assert!(err.contains("Forbidden"), "expected Forbidden err: {err}");
    assert_eq!(fx.count_vlans().await, 0);

    // Sanity: a write:Device grant must NOT authorise a VLAN import —
    // entity_type dimension of the grant tuple matters.
    ScopeGrantRepo::new(fx.tenant.pool.clone()).create(&CreateScopeGrantBody {
        organization_id: fx.tenant.org_id,
        user_id: 42,
        action: "write".into(),
        entity_type: "Device".into(),    // wrong entity_type for this import
        scope_type: "Global".into(),
        scope_entity_id: None,
        notes: None,
    }, Some(99)).await.expect("seed wrong-entity-type grant");

    let err = bulk_import::import_vlans(
        &fx.tenant.pool, fx.tenant.org_id, &csv, false, ImportMode::Create, Some(42)
    ).await.unwrap_err().to_string();
    assert!(err.contains("Forbidden"),
        "write:Device grant must not authorise Vlan import: {err}");
}

#[tokio::test]
#[ignore]
async fn subnet_import_forbidden_without_write_subnet_grant() {
    let Some(pool) = pool_or_skip("subnet_import_forbidden_without_write_subnet_grant").await else { return; };
    let fx = PoolsFixture::new(pool).await.expect("fixture");

    let csv = format!(
        "{SUBNET_HEADER}BI-SUB,Subnet X,10.99.1.0/24,,{p},Free,,Active\r\n",
        p = fx.pool_code);
    let err = bulk_import::import_subnets(
        &fx.tenant.pool, fx.tenant.org_id, &csv, false, ImportMode::Create, Some(42)
    ).await.unwrap_err().to_string();
    assert!(err.contains("Forbidden"), "err: {err}");
    assert_eq!(fx.count_subnets().await, 0);
}

// ─── Server + DHCP relay target import tests ──────────────────────────────
//
// The tests above already exercise the devices / vlans / subnets
// entities. These add coverage for server + dhcp-relay-target —
// the last two of the core 9 import-eligible entities.

/// Extends PoolsFixture with a server_profile + seeded VLAN so
/// server + dhcp-relay-target imports can resolve their FKs.
struct FullFixture {
    pools: PoolsFixture,
    profile_code: String,
    vlan_numeric: i32,
}

impl FullFixture {
    async fn new(pool: PgPool) -> sqlx::Result<Self> {
        let pools = PoolsFixture::new(pool).await?;

        // Server profile — profile_code must be unique per tenant.
        sqlx::query(
            "INSERT INTO net.server_profile (organization_id, profile_code,
                                             display_name, nic_count, naming_template)
             VALUES ($1, 'BI-SP', 'BI Server Profile', 4,
                     '{building_code}-SRV{instance}')")
            .bind(pools.tenant.org_id).execute(&pools.tenant.pool).await?;

        // A VLAN row (not just a block) for dhcp-relay-target tests.
        let (vlan_pool_id,): (Uuid,) = sqlx::query_as(
            "SELECT id FROM net.vlan_pool
              WHERE organization_id = $1 AND pool_code = 'BI-VP'")
            .bind(pools.tenant.org_id).fetch_one(&pools.tenant.pool).await?;
        let (vlan_block_id,): (Uuid,) = sqlx::query_as(
            "SELECT id FROM net.vlan_block
              WHERE organization_id = $1 AND block_code = 'BI-VB'")
            .bind(pools.tenant.org_id).fetch_one(&pools.tenant.pool).await?;
        sqlx::query(
            "INSERT INTO net.vlan (organization_id, pool_id, block_id,
                                   vlan_id, display_name, scope_level, status)
             VALUES ($1, $2, $3, 120, 'BI-VLAN', 'Free', 'Active')")
            .bind(pools.tenant.org_id).bind(vlan_pool_id).bind(vlan_block_id)
            .execute(&pools.tenant.pool).await?;

        Ok(Self { pools, profile_code: "BI-SP".into(), vlan_numeric: 120 })
    }

    async fn count_servers(&self) -> i64 {
        let (n,): (i64,) = sqlx::query_as(
            "SELECT COUNT(*) FROM net.server
              WHERE organization_id = $1 AND deleted_at IS NULL")
            .bind(self.pools.tenant.org_id).fetch_one(&self.pools.tenant.pool).await.unwrap();
        n
    }

    async fn count_dhcp_relay_targets(&self) -> i64 {
        let (n,): (i64,) = sqlx::query_as(
            "SELECT COUNT(*) FROM net.dhcp_relay_target
              WHERE organization_id = $1 AND deleted_at IS NULL")
            .bind(self.pools.tenant.org_id).fetch_one(&self.pools.tenant.pool).await.unwrap();
        n
    }
}

const SERVER_HEADER: &str = "hostname,profile_code,building_code,asn,loopback_ip,management_ip,nic_count,status\r\n";
const DHCP_HEADER:   &str = "vlan_id,server_ip,priority,linked_ip_address_id,notes,status\r\n";

#[tokio::test]
#[ignore]
async fn server_import_happy_path_commits() {
    let Some(pool) = pool_or_skip("server_import_happy_path_commits").await else { return; };
    let fx = FullFixture::new(pool).await.expect("fixture");

    let csv = format!(
        "{SERVER_HEADER}\
         SRV-A,{p},IT-B,,,10.88.0.1,4,Active\r\n\
         SRV-B,{p},IT-B,,,10.88.0.2,4,Active\r\n",
        p = fx.profile_code);
    let result = bulk_import::import_servers(
        &fx.pools.tenant.pool, fx.pools.tenant.org_id, &csv, false, ImportMode::Create, None,
    ).await.expect("apply");
    assert_eq!(result.valid, 2);
    assert!(result.applied);
    assert_eq!(fx.count_servers().await, 2);
}

#[tokio::test]
#[ignore]
async fn server_import_rejects_duplicate_hostname() {
    let Some(pool) = pool_or_skip("server_import_rejects_duplicate_hostname").await else { return; };
    let fx = FullFixture::new(pool).await.expect("fixture");

    let csv = format!("{SERVER_HEADER}SRV-X,{p},IT-B,,,,4,Active\r\n", p = fx.profile_code);
    bulk_import::import_servers(&fx.pools.tenant.pool, fx.pools.tenant.org_id, &csv, false, ImportMode::Create, None)
        .await.expect("seed apply");
    let result = bulk_import::import_servers(&fx.pools.tenant.pool, fx.pools.tenant.org_id, &csv, false, ImportMode::Create, None)
        .await.expect("re-apply");
    assert_eq!(result.invalid, 1);
    assert!(!result.applied);
    assert!(result.outcomes[0].errors.iter().any(|e| e.contains("already exists")),
        "duplicate hostname must surface: {:?}", result.outcomes[0].errors);
    assert_eq!(fx.count_servers().await, 1);
}

#[tokio::test]
#[ignore]
async fn server_import_forbidden_without_grant() {
    let Some(pool) = pool_or_skip("server_import_forbidden_without_grant").await else { return; };
    let fx = FullFixture::new(pool).await.expect("fixture");

    let csv = format!("{SERVER_HEADER}SRV-Z,{p},IT-B,,,,4,Active\r\n", p = fx.profile_code);
    let err = bulk_import::import_servers(
        &fx.pools.tenant.pool, fx.pools.tenant.org_id, &csv, false, ImportMode::Create, Some(42),
    ).await.unwrap_err().to_string();
    assert!(err.contains("Forbidden"), "err: {err}");
    assert_eq!(fx.count_servers().await, 0);
}

#[tokio::test]
#[ignore]
async fn dhcp_relay_import_happy_path_commits() {
    let Some(pool) = pool_or_skip("dhcp_relay_import_happy_path_commits").await else { return; };
    let fx = FullFixture::new(pool).await.expect("fixture");

    let csv = format!(
        "{DHCP_HEADER}\
         {v},10.11.120.10,10,,,Active\r\n\
         {v},10.11.120.11,20,,,Active\r\n",
        v = fx.vlan_numeric);
    let result = bulk_import::import_dhcp_relay_targets(
        &fx.pools.tenant.pool, fx.pools.tenant.org_id, &csv, false, ImportMode::Create, None,
    ).await.expect("apply");
    assert_eq!(result.valid, 2);
    assert!(result.applied);
    assert_eq!(fx.count_dhcp_relay_targets().await, 2);
}

#[tokio::test]
#[ignore]
async fn dhcp_relay_import_rejects_unknown_vlan_id() {
    let Some(pool) = pool_or_skip("dhcp_relay_import_rejects_unknown_vlan_id").await else { return; };
    let fx = FullFixture::new(pool).await.expect("fixture");

    // VLAN 999 isn't in this tenant — rejected with a clear message.
    let csv = format!("{DHCP_HEADER}999,10.11.99.10,10,,,Active\r\n");
    let result = bulk_import::import_dhcp_relay_targets(
        &fx.pools.tenant.pool, fx.pools.tenant.org_id, &csv, false, ImportMode::Create, None,
    ).await.expect("apply");
    assert_eq!(result.invalid, 1);
    assert!(!result.applied);
    assert!(result.outcomes[0].errors.iter().any(|e| e.contains("vlan catalog")),
        "unknown vlan should surface: {:?}", result.outcomes[0].errors);
    assert_eq!(fx.count_dhcp_relay_targets().await, 0);
}

#[tokio::test]
#[ignore]
async fn dhcp_relay_import_forbidden_without_grant() {
    let Some(pool) = pool_or_skip("dhcp_relay_import_forbidden_without_grant").await else { return; };
    let fx = FullFixture::new(pool).await.expect("fixture");

    let csv = format!("{DHCP_HEADER}{},10.11.120.99,10,,,Active\r\n", fx.vlan_numeric);
    let err = bulk_import::import_dhcp_relay_targets(
        &fx.pools.tenant.pool, fx.pools.tenant.org_id, &csv, false, ImportMode::Create, Some(42),
    ).await.unwrap_err().to_string();
    assert!(err.contains("Forbidden"), "err: {err}");
    assert_eq!(fx.count_dhcp_relay_targets().await, 0);
}

// ─── Link import tests ───────────────────────────────────────────────────
//
// Link import is the most structurally complex of the bulk importers —
// one CSV row materialises three DB rows (1 link + 2 endpoints) in a
// single transaction. These tests pin the decomposition invariants.

/// Extends FullFixture with 2 devices (DEV-A, DEV-B) that link
/// imports can reference as endpoint hostnames. Also seeds a
/// 'P2P' link_type since a fresh tenant doesn't have any by default.
struct LinkFixture {
    full: FullFixture,
    device_a_hostname: String,
    device_b_hostname: String,
}

impl LinkFixture {
    async fn new(pool: PgPool) -> sqlx::Result<Self> {
        let full = FullFixture::new(pool).await?;
        let org = full.pools.tenant.org_id;
        let pool = full.pools.tenant.pool.clone();

        // Seed a device_role + two devices in the tenant so link
        // endpoints have something to reference.
        let (role_id,): (Uuid,) = sqlx::query_as(
            "SELECT id FROM net.device_role
              WHERE organization_id = $1 AND role_code = 'Core'
              AND deleted_at IS NULL")
            .bind(org).fetch_one(&pool).await?;
        let (building_id,): (Uuid,) = sqlx::query_as(
            "SELECT id FROM net.building
              WHERE organization_id = $1 AND building_code = 'IT-B'
              AND deleted_at IS NULL")
            .bind(org).fetch_one(&pool).await?;
        sqlx::query(
            "INSERT INTO net.device (organization_id, device_role_id, building_id,
                                     hostname, status)
             VALUES ($1, $2, $3, 'DEV-A', 'Active'),
                    ($1, $2, $3, 'DEV-B', 'Active')")
            .bind(org).bind(role_id).bind(building_id).execute(&pool).await?;

        // Seed a 'P2P' link_type.
        sqlx::query(
            "INSERT INTO net.link_type (organization_id, type_code, display_name,
                                        naming_template, required_endpoints, status)
             VALUES ($1, 'P2P', 'Point-to-Point', '{device_a}-to-{device_b}', 2, 'Active')")
            .bind(org).execute(&pool).await?;

        Ok(Self { full, device_a_hostname: "DEV-A".into(), device_b_hostname: "DEV-B".into() })
    }

    async fn count_links(&self) -> i64 {
        let (n,): (i64,) = sqlx::query_as(
            "SELECT COUNT(*) FROM net.link
              WHERE organization_id = $1 AND deleted_at IS NULL")
            .bind(self.full.pools.tenant.org_id)
            .fetch_one(&self.full.pools.tenant.pool).await.unwrap();
        n
    }

    async fn count_endpoints(&self) -> i64 {
        let (n,): (i64,) = sqlx::query_as(
            "SELECT COUNT(*) FROM net.link_endpoint
              WHERE organization_id = $1 AND deleted_at IS NULL")
            .bind(self.full.pools.tenant.org_id)
            .fetch_one(&self.full.pools.tenant.pool).await.unwrap();
        n
    }
}

const LINK_HEADER: &str = "link_code,link_type,vlan_id,subnet_code,device_a,port_a,ip_a,device_b,port_b,ip_b,status\r\n";

#[tokio::test]
#[ignore]
async fn link_import_happy_path_writes_one_link_plus_two_endpoints() {
    let Some(pool) = pool_or_skip("link_import_happy_path_writes_one_link_plus_two_endpoints").await else { return; };
    let fx = LinkFixture::new(pool).await.expect("fixture");

    let csv = format!(
        "{LINK_HEADER}\
         LINK-1,P2P,,,{a},xe-1/1/1,10.0.0.1,{b},xe-1/1/1,10.0.0.2,Active\r\n",
        a = fx.device_a_hostname, b = fx.device_b_hostname);
    let result = bulk_import::import_links(
        &fx.full.pools.tenant.pool, fx.full.pools.tenant.org_id, &csv, false, ImportMode::Create, None,
    ).await.expect("apply");
    assert_eq!(result.valid, 1);
    assert!(result.applied);
    assert_eq!(fx.count_links().await, 1, "exactly one link row");
    assert_eq!(fx.count_endpoints().await, 2,
        "exactly TWO endpoint rows (A + B) from one CSV row");
}

#[tokio::test]
#[ignore]
async fn link_import_rejects_unknown_device_hostname() {
    let Some(pool) = pool_or_skip("link_import_rejects_unknown_device_hostname").await else { return; };
    let fx = LinkFixture::new(pool).await.expect("fixture");

    // device_b doesn't exist in the tenant — validation fails.
    let csv = format!(
        "{LINK_HEADER}\
         LINK-2,P2P,,,{a},xe-1/1/1,,DEV-GHOST,xe-1/1/1,,Active\r\n",
        a = fx.device_a_hostname);
    let result = bulk_import::import_links(
        &fx.full.pools.tenant.pool, fx.full.pools.tenant.org_id, &csv, false, ImportMode::Create, None,
    ).await.expect("apply");
    assert_eq!(result.invalid, 1);
    assert!(!result.applied);
    assert!(result.outcomes[0].errors.iter().any(|e| e.contains("DEV-GHOST")),
        "unknown device_b should be flagged: {:?}", result.outcomes[0].errors);
    assert_eq!(fx.count_links().await, 0);
    assert_eq!(fx.count_endpoints().await, 0,
        "no partial writes — link + endpoints stay zero");
}

#[tokio::test]
#[ignore]
async fn link_import_rejects_unknown_link_type() {
    let Some(pool) = pool_or_skip("link_import_rejects_unknown_link_type").await else { return; };
    let fx = LinkFixture::new(pool).await.expect("fixture");

    let csv = format!(
        "{LINK_HEADER}\
         LINK-X,Bogus,,,{a},,,{b},,,Active\r\n",
        a = fx.device_a_hostname, b = fx.device_b_hostname);
    let result = bulk_import::import_links(
        &fx.full.pools.tenant.pool, fx.full.pools.tenant.org_id, &csv, false, ImportMode::Create, None,
    ).await.expect("apply");
    assert_eq!(result.invalid, 1);
    assert!(!result.applied);
    assert!(result.outcomes[0].errors.iter().any(|e| e.contains("Bogus")),
        "unknown link_type should be flagged: {:?}", result.outcomes[0].errors);
}

#[tokio::test]
#[ignore]
async fn link_import_rejects_duplicate_link_code() {
    let Some(pool) = pool_or_skip("link_import_rejects_duplicate_link_code").await else { return; };
    let fx = LinkFixture::new(pool).await.expect("fixture");

    let csv = format!(
        "{LINK_HEADER}\
         LINK-DUP,P2P,,,{a},,,{b},,,Active\r\n",
        a = fx.device_a_hostname, b = fx.device_b_hostname);
    bulk_import::import_links(&fx.full.pools.tenant.pool, fx.full.pools.tenant.org_id, &csv, false, ImportMode::Create, None)
        .await.expect("seed apply");
    assert_eq!(fx.count_links().await, 1);

    let result = bulk_import::import_links(
        &fx.full.pools.tenant.pool, fx.full.pools.tenant.org_id, &csv, false, ImportMode::Create, None,
    ).await.expect("re-apply");
    assert_eq!(result.invalid, 1);
    assert!(!result.applied);
    assert!(result.outcomes[0].errors.iter().any(|e| e.contains("already exists")),
        "dup link_code should surface: {:?}", result.outcomes[0].errors);
    assert_eq!(fx.count_links().await, 1, "no second link row created");
    assert_eq!(fx.count_endpoints().await, 2,
        "endpoint count unchanged — dup-rejected link doesn't smuggle endpoints in");
}

#[tokio::test]
#[ignore]
async fn link_import_forbidden_without_write_link_grant() {
    let Some(pool) = pool_or_skip("link_import_forbidden_without_write_link_grant").await else { return; };
    let fx = LinkFixture::new(pool).await.expect("fixture");

    let csv = format!(
        "{LINK_HEADER}\
         LINK-Z,P2P,,,{a},,,{b},,,Active\r\n",
        a = fx.device_a_hostname, b = fx.device_b_hostname);
    let err = bulk_import::import_links(
        &fx.full.pools.tenant.pool, fx.full.pools.tenant.org_id, &csv, false, ImportMode::Create, Some(42),
    ).await.unwrap_err().to_string();
    assert!(err.contains("Forbidden"), "err: {err}");
    assert_eq!(fx.count_links().await, 0);
}

// ─── Server / DHCP-relay / Link upsert tests ──────────────────────────────
//
// Same shape as the VLAN + subnet upsert tests above. None of these
// CSVs carry a version column today, so upsert applies against current
// DB version (no client-side concurrency snapshot to validate). Link
// upsert additionally exercises the delete-then-insert endpoint
// rewrite — a CSV row's port_a/port_b is the source of truth on
// re-import.

#[tokio::test]
#[ignore]
async fn server_upsert_mode_updates_existing_hostname_rather_than_rejecting() {
    let Some(pool) = pool_or_skip("server_upsert_mode_updates_existing_hostname_rather_than_rejecting").await else { return; };
    let fx = FullFixture::new(pool).await.expect("fixture");

    // Seed via Create mode.
    let csv_initial = format!(
        "{SERVER_HEADER}SRV-UP,{p},IT-B,,,10.88.0.5,4,Planned\r\n",
        p = fx.profile_code);
    bulk_import::import_servers(
        &fx.pools.tenant.pool, fx.pools.tenant.org_id, &csv_initial, false, ImportMode::Create, None,
    ).await.expect("seed");
    assert_eq!(fx.count_servers().await, 1);

    // Re-import same hostname with Active status + new mgmt IP.
    let csv_upsert = format!(
        "{SERVER_HEADER}SRV-UP,{p},IT-B,,,10.88.0.99,4,Active\r\n",
        p = fx.profile_code);
    let result = bulk_import::import_servers(
        &fx.pools.tenant.pool, fx.pools.tenant.org_id, &csv_upsert, false, ImportMode::Upsert, None,
    ).await.expect("upsert");
    assert!(result.applied, "upsert must succeed on existing hostname");
    assert_eq!(fx.count_servers().await, 1, "no duplicate row");

    let (mgmt_ip, status, version): (String, String, i32) = sqlx::query_as(
        "SELECT host(management_ip), status::text, version
           FROM net.server
          WHERE organization_id = $1 AND hostname = 'SRV-UP'")
        .bind(fx.pools.tenant.org_id)
        .fetch_one(&fx.pools.tenant.pool).await.unwrap();
    assert_eq!(mgmt_ip, "10.88.0.99");
    assert_eq!(status, "Active");
    assert_eq!(version, 2);
}

#[tokio::test]
#[ignore]
async fn server_upsert_mode_creates_new_hostname_like_create() {
    let Some(pool) = pool_or_skip("server_upsert_mode_creates_new_hostname_like_create").await else { return; };
    let fx = FullFixture::new(pool).await.expect("fixture");

    let csv = format!(
        "{SERVER_HEADER}SRV-FRESH,{p},IT-B,,,10.88.42.1,4,Active\r\n",
        p = fx.profile_code);
    let result = bulk_import::import_servers(
        &fx.pools.tenant.pool, fx.pools.tenant.org_id, &csv, false, ImportMode::Upsert, None,
    ).await.expect("upsert new");
    assert!(result.applied);
    assert_eq!(fx.count_servers().await, 1);
}

#[tokio::test]
#[ignore]
async fn dhcp_relay_upsert_mode_updates_existing_pair_rather_than_rejecting() {
    let Some(pool) = pool_or_skip("dhcp_relay_upsert_mode_updates_existing_pair_rather_than_rejecting").await else { return; };
    let fx = FullFixture::new(pool).await.expect("fixture");

    let csv_initial = format!(
        "{DHCP_HEADER}{v},10.11.120.10,10,,initial,Active\r\n",
        v = fx.vlan_numeric);
    bulk_import::import_dhcp_relay_targets(
        &fx.pools.tenant.pool, fx.pools.tenant.org_id, &csv_initial, false, ImportMode::Create, None,
    ).await.expect("seed");
    assert_eq!(fx.count_dhcp_relay_targets().await, 1);

    // Re-import same (vlan_id, server_ip) pair with bumped priority + notes.
    let csv_upsert = format!(
        "{DHCP_HEADER}{v},10.11.120.10,99,,upgraded,Active\r\n",
        v = fx.vlan_numeric);
    let result = bulk_import::import_dhcp_relay_targets(
        &fx.pools.tenant.pool, fx.pools.tenant.org_id, &csv_upsert, false, ImportMode::Upsert, None,
    ).await.expect("upsert");
    assert!(result.applied, "upsert must succeed on existing (vlan_id, server_ip)");
    assert_eq!(fx.count_dhcp_relay_targets().await, 1);

    let (priority, notes, version): (i32, Option<String>, i32) = sqlx::query_as(
        "SELECT priority, notes, version
           FROM net.dhcp_relay_target
          WHERE organization_id = $1 AND host(server_ip) = '10.11.120.10'")
        .bind(fx.pools.tenant.org_id)
        .fetch_one(&fx.pools.tenant.pool).await.unwrap();
    assert_eq!(priority, 99);
    assert_eq!(notes.as_deref(), Some("upgraded"));
    assert_eq!(version, 2);
}

#[tokio::test]
#[ignore]
async fn dhcp_relay_upsert_mode_creates_new_pair_like_create() {
    let Some(pool) = pool_or_skip("dhcp_relay_upsert_mode_creates_new_pair_like_create").await else { return; };
    let fx = FullFixture::new(pool).await.expect("fixture");

    let csv = format!(
        "{DHCP_HEADER}{v},10.11.120.42,5,,,Active\r\n",
        v = fx.vlan_numeric);
    let result = bulk_import::import_dhcp_relay_targets(
        &fx.pools.tenant.pool, fx.pools.tenant.org_id, &csv, false, ImportMode::Upsert, None,
    ).await.expect("upsert new");
    assert!(result.applied);
    assert_eq!(fx.count_dhcp_relay_targets().await, 1);
}

#[tokio::test]
#[ignore]
async fn link_upsert_mode_updates_link_and_rewrites_endpoints() {
    let Some(pool) = pool_or_skip("link_upsert_mode_updates_link_and_rewrites_endpoints").await else { return; };
    let fx = LinkFixture::new(pool).await.expect("fixture");

    // Seed: LINK-UP with port xe-1/1/1 on both sides.
    let csv_initial = format!(
        "{LINK_HEADER}LINK-UP,P2P,,,{a},xe-1/1/1,,{b},xe-1/1/1,,Planned\r\n",
        a = fx.device_a_hostname, b = fx.device_b_hostname);
    bulk_import::import_links(
        &fx.full.pools.tenant.pool, fx.full.pools.tenant.org_id, &csv_initial, false, ImportMode::Create, None,
    ).await.expect("seed");
    assert_eq!(fx.count_links().await, 1);
    assert_eq!(fx.count_endpoints().await, 2);

    // Re-import LINK-UP with new ports + Active status. Endpoints
    // should be rewritten (delete+insert in same tx) — the count
    // stays at 2 but interface_name on each side flips.
    let csv_upsert = format!(
        "{LINK_HEADER}LINK-UP,P2P,,,{a},xe-1/1/9,,{b},xe-1/1/9,,Active\r\n",
        a = fx.device_a_hostname, b = fx.device_b_hostname);
    let result = bulk_import::import_links(
        &fx.full.pools.tenant.pool, fx.full.pools.tenant.org_id, &csv_upsert, false, ImportMode::Upsert, None,
    ).await.expect("upsert");
    assert!(result.applied, "upsert must succeed on existing link_code");
    assert_eq!(fx.count_links().await, 1, "no duplicate link");
    assert_eq!(fx.count_endpoints().await, 2,
        "endpoints rewritten — count stays at 2, not doubled");

    // Verify link row updated.
    let (status, version): (String, i32) = sqlx::query_as(
        "SELECT status::text, version FROM net.link
          WHERE organization_id = $1 AND link_code = 'LINK-UP'")
        .bind(fx.full.pools.tenant.org_id)
        .fetch_one(&fx.full.pools.tenant.pool).await.unwrap();
    assert_eq!(status, "Active");
    assert_eq!(version, 2);

    // Verify endpoints carry the new port names.
    let port_rows: Vec<(i32, Option<String>)> = sqlx::query_as(
        "SELECT endpoint_order, interface_name FROM net.link_endpoint
           JOIN net.link l ON l.id = link_endpoint.link_id
          WHERE link_endpoint.organization_id = $1 AND l.link_code = 'LINK-UP'
          ORDER BY endpoint_order")
        .bind(fx.full.pools.tenant.org_id)
        .fetch_all(&fx.full.pools.tenant.pool).await.unwrap();
    assert_eq!(port_rows.len(), 2);
    assert_eq!(port_rows[0].1.as_deref(), Some("xe-1/1/9"),
        "endpoint A interface_name must reflect upsert CSV value");
    assert_eq!(port_rows[1].1.as_deref(), Some("xe-1/1/9"),
        "endpoint B interface_name must reflect upsert CSV value");
}

#[tokio::test]
#[ignore]
async fn link_upsert_mode_creates_new_link_code_like_create() {
    let Some(pool) = pool_or_skip("link_upsert_mode_creates_new_link_code_like_create").await else { return; };
    let fx = LinkFixture::new(pool).await.expect("fixture");

    let csv = format!(
        "{LINK_HEADER}LINK-NEW,P2P,,,{a},,,{b},,,Active\r\n",
        a = fx.device_a_hostname, b = fx.device_b_hostname);
    let result = bulk_import::import_links(
        &fx.full.pools.tenant.pool, fx.full.pools.tenant.org_id, &csv, false, ImportMode::Upsert, None,
    ).await.expect("upsert new");
    assert!(result.applied);
    assert_eq!(fx.count_links().await, 1);
    assert_eq!(fx.count_endpoints().await, 2);
}
