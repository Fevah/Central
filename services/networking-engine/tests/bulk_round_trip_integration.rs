//! Live-DB integration tests for the export→edit→re-import round
//! trip. The goal is operator-facing: an admin downloads a CSV (or
//! XLSX), opens it in Excel, edits a few cells, uploads it again with
//! `?mode=upsert`, and the only differences between the pre-export
//! and post-import DB state are the cells they actually edited.
//!
//! This is the test the bulk surface needed all along — every prior
//! integration test exercises one half of the round trip (export OR
//! import OR upsert) but none of them prove the two halves agree on
//! column order, encoding, or escaping. A single round-trip test
//! catches header-renamed-but-importer-not-updated bugs that no unit
//! test can.
//!
//! Same opt-in harness as the other integration suites:
//!
//! ```sh
//! export TEST_DATABASE_URL="postgresql://central:central@192.168.56.201:5432/central_test"
//! cargo test --test bulk_round_trip_integration -- --ignored --test-threads=1
//! ```

use networking_engine::{bulk_export, bulk_import};
use networking_engine::bulk_import::ImportMode;
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

/// Per-test tenant. Mirrors the shape of TenantFixture in
/// bulk_import_integration.rs but kept self-contained — sharing the
/// fixture across test files would require pub-ifying the test module
/// which is awkward and fragile.
struct RoundTripFixture {
    pool: PgPool,
    org_id: Uuid,
    block_code: String,
    pool_code: String,
}

impl RoundTripFixture {
    async fn new(pool: PgPool) -> sqlx::Result<Self> {
        let org_id = Uuid::new_v4();
        sqlx::query(
            "INSERT INTO central_platform.tenants (id, slug, display_name, status)
             VALUES ($1, $2, $2, 'Active') ON CONFLICT (id) DO NOTHING")
            .bind(org_id).bind(format!("rt-itest-{org_id}"))
            .execute(&pool).await?;

        let region_id: (Uuid,) = sqlx::query_as(
            "INSERT INTO net.region (organization_id, region_code, display_name, status)
             VALUES ($1, 'RT-R', 'RT Region', 'Active') RETURNING id")
            .bind(org_id).fetch_one(&pool).await?;
        let site_id: (Uuid,) = sqlx::query_as(
            "INSERT INTO net.site (organization_id, region_id, site_code, display_name,
                                   city, country, timezone, site_number, status)
             VALUES ($1, $2, 'RT-S', 'RT Site', 'C', 'UK', 'UTC', 1, 'Active')
             RETURNING id")
            .bind(org_id).bind(region_id.0).fetch_one(&pool).await?;
        sqlx::query(
            "INSERT INTO net.building (organization_id, site_id, building_code,
                                       display_name, building_number, status)
             VALUES ($1, $2, 'RT-B', 'RT Building', '1', 'Active')")
            .bind(org_id).bind(site_id.0).execute(&pool).await?;

        sqlx::query(
            "INSERT INTO net.device_role (organization_id, role_code, display_name,
                                          naming_template, status)
             VALUES ($1, 'Core', 'RT Core', '{building_code}-CORE{instance}', 'Active')")
            .bind(org_id).execute(&pool).await?;

        let vlan_pool_id: (Uuid,) = sqlx::query_as(
            "INSERT INTO net.vlan_pool (organization_id, pool_code, display_name,
                                        vlan_first, vlan_last)
             VALUES ($1, 'RT-VP', 'RT VLAN pool', 1, 4094) RETURNING id")
            .bind(org_id).fetch_one(&pool).await?;
        sqlx::query(
            "INSERT INTO net.vlan_block (organization_id, pool_id, block_code, display_name,
                                          vlan_start, vlan_end)
             VALUES ($1, $2, 'RT-VB', 'RT VLAN block', 1, 4094)")
            .bind(org_id).bind(vlan_pool_id.0).execute(&pool).await?;

        sqlx::query(
            "INSERT INTO net.ip_pool (organization_id, pool_code, display_name,
                                      pool_cidr, status)
             VALUES ($1, 'RT-IP', 'RT IP pool', '10.0.0.0/8', 'Active')")
            .bind(org_id).execute(&pool).await?;

        Ok(Self { pool, org_id, block_code: "RT-VB".into(), pool_code: "RT-IP".into() })
    }
}

impl Drop for RoundTripFixture {
    fn drop(&mut self) {
        let pool = self.pool.clone();
        let org_id = self.org_id;
        tokio::spawn(async move {
            let _ = sqlx::query("DELETE FROM central_platform.tenants WHERE id = $1")
                .bind(org_id).execute(&pool).await;
        });
    }
}

/// Replace the value of a single CSV cell, identified by header name +
/// row identifier (the value in the first column). Used to simulate
/// an operator opening the export in Excel and changing one cell.
///
/// Returns the rebuilt CSV. Panics if header / row / column not found
/// — these are test fixtures, not user input.
fn edit_csv_cell(csv: &str, row_identifier: &str, column: &str, new_value: &str) -> String {
    let mut lines: Vec<Vec<String>> = csv.lines()
        .map(|line| line.split(',').map(|c| c.to_string()).collect())
        .collect();
    let header = &lines[0];
    let col_idx = header.iter().position(|c| c == column)
        .unwrap_or_else(|| panic!("column '{column}' not found in header: {header:?}"));
    let row_idx = lines.iter().position(|row| row.first().map(|s| s.as_str()) == Some(row_identifier))
        .unwrap_or_else(|| panic!("row identifier '{row_identifier}' not found in CSV"));
    lines[row_idx][col_idx] = new_value.to_string();
    let mut out = lines.iter()
        .map(|r| r.join(","))
        .collect::<Vec<_>>()
        .join("\r\n");
    // CSV exports use trailing CRLF; preserve that so the importer's
    // header check doesn't see an extra/missing line.
    out.push_str("\r\n");
    out
}

// ─── Devices round trip ─────────────────────────────────────────────────

#[tokio::test]
#[ignore]
async fn devices_export_edit_reimport_only_changes_edited_cell() {
    let Some(pool) = pool_or_skip("devices_export_edit_reimport_only_changes_edited_cell").await else { return; };
    let fx = RoundTripFixture::new(pool).await.expect("fixture");

    // Seed two devices.
    let seed_csv = "hostname,role_code,building_code,site_code,management_ip,asn,status,version\r\n\
                    RT-B-CORE01,Core,RT-B,RT-S,10.99.0.1/24,65100,Planned,1\r\n\
                    RT-B-CORE02,Core,RT-B,RT-S,10.99.0.2/24,65101,Planned,1\r\n";
    bulk_import::import_devices(
        &fx.pool, fx.org_id, seed_csv, false, ImportMode::Create, None,
    ).await.expect("seed");

    // Snapshot pre-export state — capture every device row so we can
    // diff after the round trip and assert nothing else moved.
    let pre: Vec<(String, String)> = sqlx::query_as(
        "SELECT hostname, status::text FROM net.device
          WHERE organization_id = $1 AND deleted_at IS NULL
          ORDER BY hostname")
        .bind(fx.org_id).fetch_all(&fx.pool).await.unwrap();
    assert_eq!(pre.len(), 2);

    // Round-trip step 1: export. The importer will round-trip through
    // this body — if export writes a different column order than
    // import expects, the next step blows up at the header check.
    let exported_csv = bulk_export::export_devices_csv(&fx.pool, fx.org_id).await
        .expect("export");
    assert!(exported_csv.contains("RT-B-CORE01"));
    assert!(exported_csv.contains("RT-B-CORE02"));

    // Round-trip step 2: operator edits one cell — flip CORE01 from
    // Planned to Active. Everything else stays the same.
    let edited_csv = edit_csv_cell(&exported_csv, "RT-B-CORE01", "status", "Active");

    // Round-trip step 3: re-import in upsert mode. New rows MUST NOT
    // appear; only the edited cell should change.
    let result = bulk_import::import_devices(
        &fx.pool, fx.org_id, &edited_csv, false, ImportMode::Upsert, None,
    ).await.expect("re-import");
    assert!(result.applied, "round-trip upsert must apply");

    // Post-import diff. Same row count (no duplicates from the
    // upsert), CORE01 is now Active, CORE02 is unchanged.
    let post: Vec<(String, String)> = sqlx::query_as(
        "SELECT hostname, status::text FROM net.device
          WHERE organization_id = $1 AND deleted_at IS NULL
          ORDER BY hostname")
        .bind(fx.org_id).fetch_all(&fx.pool).await.unwrap();
    assert_eq!(post.len(), 2,
        "round-trip must preserve row count — got {} after upsert", post.len());
    assert_eq!(post[0].0, "RT-B-CORE01");
    assert_eq!(post[0].1, "Active",
        "edited cell must reflect operator change post-round-trip");
    assert_eq!(post[1].0, "RT-B-CORE02");
    assert_eq!(post[1].1, pre[1].1,
        "untouched row must not change status across the round trip");
}

// ─── VLANs round trip ──────────────────────────────────────────────────

#[tokio::test]
#[ignore]
async fn vlans_export_edit_reimport_only_changes_edited_cell() {
    let Some(pool) = pool_or_skip("vlans_export_edit_reimport_only_changes_edited_cell").await else { return; };
    let fx = RoundTripFixture::new(pool).await.expect("fixture");

    let seed_csv = format!(
        "vlan_id,display_name,description,scope_level,template_code,block_code,status\r\n\
         101,IT,initial desc,Free,,{b},Planned\r\n\
         120,Servers,server VLAN,Free,,{b},Planned\r\n",
        b = fx.block_code);
    bulk_import::import_vlans(
        &fx.pool, fx.org_id, &seed_csv, false, ImportMode::Create, None,
    ).await.expect("seed");

    let pre: Vec<(i32, String, String)> = sqlx::query_as(
        "SELECT v.vlan_id, v.display_name, v.status::text
           FROM net.vlan v
          WHERE v.organization_id = $1 AND v.deleted_at IS NULL
          ORDER BY v.vlan_id")
        .bind(fx.org_id).fetch_all(&fx.pool).await.unwrap();
    assert_eq!(pre.len(), 2);

    let exported_csv = bulk_export::export_vlans_csv(&fx.pool, fx.org_id).await
        .expect("export");
    assert!(exported_csv.contains("101,"));
    assert!(exported_csv.contains("120,"));

    // Edit VLAN 101's display_name from "IT" → "IT-Renamed".
    let edited_csv = edit_csv_cell(&exported_csv, "101", "display_name", "IT-Renamed");
    let result = bulk_import::import_vlans(
        &fx.pool, fx.org_id, &edited_csv, false, ImportMode::Upsert, None,
    ).await.expect("re-import");
    assert!(result.applied);

    let post: Vec<(i32, String, String)> = sqlx::query_as(
        "SELECT v.vlan_id, v.display_name, v.status::text
           FROM net.vlan v
          WHERE v.organization_id = $1 AND v.deleted_at IS NULL
          ORDER BY v.vlan_id")
        .bind(fx.org_id).fetch_all(&fx.pool).await.unwrap();
    assert_eq!(post.len(), 2, "no duplicate VLAN created");
    assert_eq!(post[0].0, 101);
    assert_eq!(post[0].1, "IT-Renamed",
        "edited cell must reflect operator change");
    assert_eq!(post[1], pre[1],
        "untouched VLAN 120 must round-trip unchanged");
}

// ─── Subnets round trip ────────────────────────────────────────────────

#[tokio::test]
#[ignore]
async fn subnets_export_edit_reimport_only_changes_edited_cell() {
    let Some(pool) = pool_or_skip("subnets_export_edit_reimport_only_changes_edited_cell").await else { return; };
    let fx = RoundTripFixture::new(pool).await.expect("fixture");

    let seed_csv = format!(
        "subnet_code,display_name,network,vlan_id,pool_code,scope_level,status\r\n\
         RT-SUB-A,Subnet A,10.99.1.0/24,,{p},Free,Planned\r\n\
         RT-SUB-B,Subnet B,10.99.2.0/24,,{p},Free,Planned\r\n",
        p = fx.pool_code);
    bulk_import::import_subnets(
        &fx.pool, fx.org_id, &seed_csv, false, ImportMode::Create, None,
    ).await.expect("seed");

    let pre: Vec<(String, String, String)> = sqlx::query_as(
        "SELECT subnet_code, display_name, status::text
           FROM net.subnet
          WHERE organization_id = $1 AND deleted_at IS NULL
          ORDER BY subnet_code")
        .bind(fx.org_id).fetch_all(&fx.pool).await.unwrap();
    assert_eq!(pre.len(), 2);

    let exported_csv = bulk_export::export_subnets_csv(&fx.pool, fx.org_id).await
        .expect("export");
    assert!(exported_csv.contains("RT-SUB-A"));
    assert!(exported_csv.contains("RT-SUB-B"));

    let edited_csv = edit_csv_cell(&exported_csv, "RT-SUB-A", "status", "Active");
    let result = bulk_import::import_subnets(
        &fx.pool, fx.org_id, &edited_csv, false, ImportMode::Upsert, None,
    ).await.expect("re-import");
    assert!(result.applied);

    let post: Vec<(String, String, String)> = sqlx::query_as(
        "SELECT subnet_code, display_name, status::text
           FROM net.subnet
          WHERE organization_id = $1 AND deleted_at IS NULL
          ORDER BY subnet_code")
        .bind(fx.org_id).fetch_all(&fx.pool).await.unwrap();
    assert_eq!(post.len(), 2, "no duplicate subnet created");
    assert_eq!(post[0].0, "RT-SUB-A");
    assert_eq!(post[0].2, "Active");
    assert_eq!(post[1], pre[1], "untouched subnet B unchanged");
}

// ─── Header round-trip parity ──────────────────────────────────────────
//
// These don't need DB writes — they just confirm the importer accepts
// every header the exporter emits. If a future PR adds a column to the
// export but forgets the matching importer update, these catch it
// before any operator round-trips a real CSV.

#[tokio::test]
#[ignore]
async fn export_devices_header_matches_import_devices_expectation() {
    let Some(pool) = pool_or_skip("export_devices_header_matches_import_devices_expectation").await else { return; };
    let fx = RoundTripFixture::new(pool).await.expect("fixture");

    // Empty tenant export still emits the header row.
    let exported = bulk_export::export_devices_csv(&fx.pool, fx.org_id).await.expect("export");
    let header_line = exported.lines().next().expect("export must include a header");

    // Re-import the empty (header-only) body: importer must accept
    // the header without complaint. dry_run + upsert mode = max
    // permissive — no row writes, no duplicate checks.
    let result = bulk_import::import_devices(
        &fx.pool, fx.org_id, &format!("{header_line}\r\n"),
        true, ImportMode::Upsert, None,
    ).await;
    assert!(result.is_ok(),
        "import_devices must accept the export's own header — got: {result:?}");
}

#[tokio::test]
#[ignore]
async fn export_vlans_header_matches_import_vlans_expectation() {
    let Some(pool) = pool_or_skip("export_vlans_header_matches_import_vlans_expectation").await else { return; };
    let fx = RoundTripFixture::new(pool).await.expect("fixture");

    let exported = bulk_export::export_vlans_csv(&fx.pool, fx.org_id).await.expect("export");
    let header_line = exported.lines().next().expect("export must include a header");
    let result = bulk_import::import_vlans(
        &fx.pool, fx.org_id, &format!("{header_line}\r\n"),
        true, ImportMode::Upsert, None,
    ).await;
    assert!(result.is_ok(),
        "import_vlans must accept the export's own header — got: {result:?}");
}

#[tokio::test]
#[ignore]
async fn export_subnets_header_matches_import_subnets_expectation() {
    let Some(pool) = pool_or_skip("export_subnets_header_matches_import_subnets_expectation").await else { return; };
    let fx = RoundTripFixture::new(pool).await.expect("fixture");

    let exported = bulk_export::export_subnets_csv(&fx.pool, fx.org_id).await.expect("export");
    let header_line = exported.lines().next().expect("export must include a header");
    let result = bulk_import::import_subnets(
        &fx.pool, fx.org_id, &format!("{header_line}\r\n"),
        true, ImportMode::Upsert, None,
    ).await;
    assert!(result.is_ok(),
        "import_subnets must accept the export's own header — got: {result:?}");
}

#[tokio::test]
#[ignore]
async fn export_servers_header_matches_import_servers_expectation() {
    let Some(pool) = pool_or_skip("export_servers_header_matches_import_servers_expectation").await else { return; };
    let fx = RoundTripFixture::new(pool).await.expect("fixture");

    let exported = bulk_export::export_servers_csv(&fx.pool, fx.org_id).await.expect("export");
    let header_line = exported.lines().next().expect("export must include a header");
    let result = bulk_import::import_servers(
        &fx.pool, fx.org_id, &format!("{header_line}\r\n"),
        true, ImportMode::Upsert, None,
    ).await;
    assert!(result.is_ok(),
        "import_servers must accept the export's own header — got: {result:?}");
}

#[tokio::test]
#[ignore]
async fn export_links_header_matches_import_links_expectation() {
    let Some(pool) = pool_or_skip("export_links_header_matches_import_links_expectation").await else { return; };
    let fx = RoundTripFixture::new(pool).await.expect("fixture");

    let exported = bulk_export::export_links_csv(&fx.pool, fx.org_id).await.expect("export");
    let header_line = exported.lines().next().expect("export must include a header");
    let result = bulk_import::import_links(
        &fx.pool, fx.org_id, &format!("{header_line}\r\n"),
        true, ImportMode::Upsert, None,
    ).await;
    assert!(result.is_ok(),
        "import_links must accept the export's own header — got: {result:?}");
}

#[tokio::test]
#[ignore]
async fn export_dhcp_relay_targets_header_matches_import_expectation() {
    let Some(pool) = pool_or_skip("export_dhcp_relay_targets_header_matches_import_expectation").await else { return; };
    let fx = RoundTripFixture::new(pool).await.expect("fixture");

    let exported = bulk_export::export_dhcp_relay_targets_csv(&fx.pool, fx.org_id).await.expect("export");
    let header_line = exported.lines().next().expect("export must include a header");
    let result = bulk_import::import_dhcp_relay_targets(
        &fx.pool, fx.org_id, &format!("{header_line}\r\n"),
        true, ImportMode::Upsert, None,
    ).await;
    assert!(result.is_ok(),
        "import_dhcp_relay_targets must accept the export's own header — got: {result:?}");
}

// ─── edit_csv_cell self-test ──────────────────────────────────────────
//
// Tiny utility, but the round-trip tests above rely on it doing the
// right thing — pin the contract.

#[test]
fn edit_csv_cell_replaces_only_the_targeted_cell() {
    let csv = "id,name,status\r\nA,Alpha,Planned\r\nB,Bravo,Active\r\n";
    let out = edit_csv_cell(csv, "A", "status", "Active");
    assert!(out.contains("A,Alpha,Active"),
        "edited row must carry new value: {out}");
    assert!(out.contains("B,Bravo,Active"),
        "untouched row must survive verbatim: {out}");
    assert!(out.starts_with("id,name,status\r\n"),
        "header must be first + unchanged");
    assert!(out.ends_with("\r\n"),
        "trailing CRLF must be preserved (matches export format)");
}
