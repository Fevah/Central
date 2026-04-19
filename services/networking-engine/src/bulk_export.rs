//! Bulk export — CSV / NDJSON flat dumps of `net.*` entities for
//! spreadsheet import, BI ingestion, or emergency "just give me the
//! table" operator workflows. Counterpart to the future bulk import
//! path (Phase 10 deliverable).
//!
//! Design: pure read paths, in-memory body. For the expected tenant
//! sizes (Immunocore = 17 switches, largest forecasted customer ~200)
//! streaming is overkill. When we see tenants with 10k+ rows per
//! entity we'll revisit with `axum::body::StreamBody` + per-row
//! serialisation — until then "fetch all + format" is simpler and
//! doesn't need complex backpressure handling.

use sqlx::PgPool;
use uuid::Uuid;

use crate::error::EngineError;

// ─── CSV escaping (RFC 4180) ─────────────────────────────────────────────

/// Escape one field value for RFC 4180 CSV output. Fields that contain
/// `,`, `"`, `\r`, or `\n` are wrapped in double quotes; embedded
/// double quotes within a wrapped field are doubled (`"` → `""`).
/// Plain fields pass through unchanged.
///
/// This mirrors what Excel, LibreOffice Calc, `csv` crate, pandas
/// `to_csv`, and every other mainstream CSV toolchain produce — so
/// the output round-trips cleanly through any of them.
pub fn csv_escape(field: &str) -> String {
    let needs_quote = field.contains(',') || field.contains('"')
                   || field.contains('\n') || field.contains('\r');
    if !needs_quote { return field.to_string(); }
    let mut out = String::with_capacity(field.len() + 2);
    out.push('"');
    for ch in field.chars() {
        if ch == '"' { out.push('"'); out.push('"'); }
        else { out.push(ch); }
    }
    out.push('"');
    out
}

/// Join one row of already-escaped fields with `,` separators + RFC
/// 4180 CRLF line terminator. Kept separate from `csv_escape` so the
/// two invariants (per-field escaping + per-row line terminator)
/// can be tested independently.
pub fn csv_row(fields: &[String]) -> String {
    let mut out = fields.join(",");
    out.push_str("\r\n");
    out
}

// ─── Device export ───────────────────────────────────────────────────────

/// CSV dump of `net.device` for a tenant. Columns — in this order:
///
///   hostname, role_code, building_code, site_code, management_ip,
///   asn, status, version
///
/// Header row emitted first. Ordered by hostname so the output is
/// deterministic for diff tools.
pub async fn export_devices_csv(
    pool: &PgPool,
    org_id: Uuid,
) -> Result<String, EngineError> {
    type Row = (
        String,          // hostname
        Option<String>,  // role_code
        Option<String>,  // building_code
        Option<String>,  // site_code
        Option<String>,  // management_ip (inet text)
        Option<i64>,     // asn
        String,          // status
        i32,             // version
    );
    let rows: Vec<Row> = sqlx::query_as(
        r#"SELECT d.hostname,
                  r.role_code,
                  b.building_code,
                  s.site_code,
                  d.management_ip::text         AS management_ip,
                  aa.asn                        AS asn,
                  d.status::text                AS status,
                  d.version
             FROM net.device d
             LEFT JOIN net.device_role    r  ON r.id  = d.device_role_id
             LEFT JOIN net.building       b  ON b.id  = d.building_id
             LEFT JOIN net.site           s  ON s.id  = b.site_id
             LEFT JOIN net.asn_allocation aa ON aa.id = d.asn_allocation_id AND aa.deleted_at IS NULL
            WHERE d.organization_id = $1 AND d.deleted_at IS NULL
            ORDER BY d.hostname"#)
        .bind(org_id)
        .fetch_all(pool)
        .await?;

    let mut out = String::with_capacity(128 + rows.len() * 96);
    out.push_str(&csv_row(&[
        "hostname".into(), "role_code".into(), "building_code".into(),
        "site_code".into(), "management_ip".into(), "asn".into(),
        "status".into(), "version".into(),
    ]));
    for (hostname, role_code, building_code, site_code, mgmt, asn, status, version) in rows {
        out.push_str(&csv_row(&[
            csv_escape(&hostname),
            csv_escape(&role_code.unwrap_or_default()),
            csv_escape(&building_code.unwrap_or_default()),
            csv_escape(&site_code.unwrap_or_default()),
            // inet text comes back as "a.b.c.d/NN"; strip the /NN so
            // downstream spreadsheet formulas that parse as a dotted
            // quad don't choke. Fine to keep the prefix for ops who
            // want it via a separate export once there's demand.
            csv_escape(&mgmt
                .as_deref()
                .and_then(|m| m.split_once('/').map(|(h,_)| h.to_string()).or_else(|| Some(m.to_string())))
                .unwrap_or_default()),
            asn.map(|a| a.to_string()).unwrap_or_default(),
            csv_escape(&status),
            version.to_string(),
        ]));
    }
    Ok(out)
}

// ─── VLAN export ─────────────────────────────────────────────────────────

/// CSV dump of `net.vlan` for a tenant. Columns — in this order:
///
///   vlan_id, display_name, description, scope_level,
///   template_code, block_code, status
///
/// `block_code` joins through `net.vlan_block` — carried for the
/// round-trip with bulk import, which needs to resolve a VLAN's
/// parent block FK. `template_code` joins through `net.vlan_template`
/// for the same reason plus the human-recognisable display.
///
/// VLANs ordered by `(block_code, vlan_id)` so the export reads
/// naturally when multiple blocks partition the 1..4094 space
/// (e.g. infrastructure vs tenant ranges).
pub async fn export_vlans_csv(
    pool: &PgPool,
    org_id: Uuid,
) -> Result<String, EngineError> {
    type Row = (i32, String, Option<String>, String, Option<String>, Option<String>, String);
    let rows: Vec<Row> = sqlx::query_as(
        r#"SELECT v.vlan_id,
                  v.display_name,
                  v.description,
                  v.scope_level,
                  t.template_code,
                  b.block_code,
                  v.status::text AS status
             FROM net.vlan v
             LEFT JOIN net.vlan_template t ON t.id = v.template_id AND t.deleted_at IS NULL
             LEFT JOIN net.vlan_block    b ON b.id = v.block_id    AND b.deleted_at IS NULL
            WHERE v.organization_id = $1 AND v.deleted_at IS NULL
            ORDER BY b.block_code NULLS LAST, v.vlan_id"#)
        .bind(org_id)
        .fetch_all(pool)
        .await?;

    let mut out = String::with_capacity(128 + rows.len() * 96);
    out.push_str(&csv_row(&[
        "vlan_id".into(), "display_name".into(), "description".into(),
        "scope_level".into(), "template_code".into(), "block_code".into(),
        "status".into(),
    ]));
    for (vlan_id, name, desc, scope, tpl, block, status) in rows {
        out.push_str(&csv_row(&[
            vlan_id.to_string(),
            csv_escape(&name),
            csv_escape(&desc.unwrap_or_default()),
            csv_escape(&scope),
            csv_escape(&tpl.unwrap_or_default()),
            csv_escape(&block.unwrap_or_default()),
            csv_escape(&status),
        ]));
    }
    Ok(out)
}

// ─── IP address export ───────────────────────────────────────────────────

/// CSV dump of `net.ip_address` for a tenant. Columns — in this order:
///
///   address, subnet_code, assigned_to_type, assigned_to_id,
///   is_reserved, status
///
/// Address is emitted as a bare host (CIDR prefix stripped) matching
/// the device-export convention. `subnet_code` joins through
/// `net.subnet` so the output names the subnet operators recognise
/// ("MEP-91-LOOPBACK") rather than a UUID.
///
/// Ordered by subnet_code then address so IPs cluster by their subnet
/// for human scanning — easier to spot gaps and conflicts than a
/// flat sort by address alone.
pub async fn export_ip_addresses_csv(
    pool: &PgPool,
    org_id: Uuid,
) -> Result<String, EngineError> {
    type Row = (String, Option<String>, Option<String>, Option<Uuid>, bool, String);
    let rows: Vec<Row> = sqlx::query_as(
        r#"SELECT host(ip.address),
                  s.subnet_code,
                  ip.assigned_to_type,
                  ip.assigned_to_id,
                  ip.is_reserved,
                  ip.status::text AS status
             FROM net.ip_address ip
             LEFT JOIN net.subnet s ON s.id = ip.subnet_id AND s.deleted_at IS NULL
            WHERE ip.organization_id = $1 AND ip.deleted_at IS NULL
            ORDER BY s.subnet_code NULLS LAST, ip.address"#)
        .bind(org_id)
        .fetch_all(pool)
        .await?;

    let mut out = String::with_capacity(128 + rows.len() * 96);
    out.push_str(&csv_row(&[
        "address".into(), "subnet_code".into(), "assigned_to_type".into(),
        "assigned_to_id".into(), "is_reserved".into(), "status".into(),
    ]));
    for (addr, subnet_code, assigned_type, assigned_id, is_reserved, status) in rows {
        out.push_str(&csv_row(&[
            csv_escape(&addr),
            csv_escape(&subnet_code.unwrap_or_default()),
            csv_escape(&assigned_type.unwrap_or_default()),
            assigned_id.map(|id| id.to_string()).unwrap_or_default(),
            if is_reserved { "true".into() } else { "false".into() },
            csv_escape(&status),
        ]));
    }
    Ok(out)
}

// ─── Link export ─────────────────────────────────────────────────────────

/// CSV dump of `net.link` with endpoints flattened A/B side-by-side.
/// Matches the shape of the legacy P2P / B2B / FW link exports so
/// operators used to that format see the same columns.
///
/// Columns (in order):
///
///   link_code, link_type, vlan_id, subnet_code,
///   device_a, port_a, ip_a,
///   device_b, port_b, ip_b,
///   status
///
/// The cross-tab join goes through `net.link_endpoint` twice — once
/// for `endpoint_order = 0` (the A side) and once for `= 1` (the B
/// side). Endpoints beyond order 1 (hub + spoke links, if they ever
/// land) won't show up in this export; they warrant their own format
/// when the requirement actually exists.
///
/// Ordered `(link_type, link_code)` so the export groups by type
/// (P2P / B2B / FW / ...) and reads naturally within each group.
pub async fn export_links_csv(
    pool: &PgPool,
    org_id: Uuid,
) -> Result<String, EngineError> {
    type Row = (
        String,          // link_code
        Option<String>,  // link_type
        Option<i32>,     // vlan_id
        Option<String>,  // subnet_code
        Option<String>,  // device_a
        Option<String>,  // port_a
        Option<String>,  // ip_a (bare host)
        Option<String>,  // device_b
        Option<String>,  // port_b
        Option<String>,  // ip_b (bare host)
        String,          // status
    );
    let rows: Vec<Row> = sqlx::query_as(
        r#"SELECT l.link_code,
                  lt.type_code                    AS link_type,
                  v.vlan_id                       AS vlan_id,
                  s.subnet_code                   AS subnet_code,
                  da.hostname                     AS device_a,
                  ea.interface_name               AS port_a,
                  host(ipa.address)               AS ip_a,
                  db.hostname                     AS device_b,
                  eb.interface_name               AS port_b,
                  host(ipb.address)               AS ip_b,
                  l.status::text                  AS status
             FROM net.link l
             LEFT JOIN net.link_type lt ON lt.id = l.link_type_id
             LEFT JOIN net.vlan      v  ON v.id  = l.vlan_id   AND v.deleted_at IS NULL
             LEFT JOIN net.subnet    s  ON s.id  = l.subnet_id AND s.deleted_at IS NULL
             LEFT JOIN net.link_endpoint ea
                    ON ea.link_id = l.id AND ea.endpoint_order = 0 AND ea.deleted_at IS NULL
             LEFT JOIN net.device        da ON da.id = ea.device_id     AND da.deleted_at IS NULL
             LEFT JOIN net.ip_address    ipa ON ipa.id = ea.ip_address_id AND ipa.deleted_at IS NULL
             LEFT JOIN net.link_endpoint eb
                    ON eb.link_id = l.id AND eb.endpoint_order = 1 AND eb.deleted_at IS NULL
             LEFT JOIN net.device        db ON db.id = eb.device_id     AND db.deleted_at IS NULL
             LEFT JOIN net.ip_address    ipb ON ipb.id = eb.ip_address_id AND ipb.deleted_at IS NULL
            WHERE l.organization_id = $1 AND l.deleted_at IS NULL
            ORDER BY lt.type_code NULLS LAST, l.link_code"#)
        .bind(org_id)
        .fetch_all(pool)
        .await?;

    let mut out = String::with_capacity(256 + rows.len() * 160);
    out.push_str(&csv_row(&[
        "link_code".into(), "link_type".into(), "vlan_id".into(),
        "subnet_code".into(),
        "device_a".into(), "port_a".into(), "ip_a".into(),
        "device_b".into(), "port_b".into(), "ip_b".into(),
        "status".into(),
    ]));
    for (code, ty, vid, subnet, da, pa, ipa, db, pb, ipb, status) in rows {
        out.push_str(&csv_row(&[
            csv_escape(&code),
            csv_escape(&ty.unwrap_or_default()),
            vid.map(|n| n.to_string()).unwrap_or_default(),
            csv_escape(&subnet.unwrap_or_default()),
            csv_escape(&da.unwrap_or_default()),
            csv_escape(&pa.unwrap_or_default()),
            csv_escape(&ipa.unwrap_or_default()),
            csv_escape(&db.unwrap_or_default()),
            csv_escape(&pb.unwrap_or_default()),
            csv_escape(&ipb.unwrap_or_default()),
            csv_escape(&status),
        ]));
    }
    Ok(out)
}

// ─── Server export ───────────────────────────────────────────────────────

/// CSV dump of `net.server`. Columns (in order):
///
///   hostname, profile_code, building_code, asn, loopback_ip,
///   management_ip, nic_count, status
///
/// `nic_count` is the COUNT(*) of live `net.server_nic` rows for the
/// server — gives ops a single-glance sanity check against
/// `server_profile.nic_count` without shipping per-NIC rows. When
/// per-NIC detail is needed it belongs in its own `server-nics` export
/// (separate endpoint once demand shows up).
///
/// Ordered by hostname for deterministic diffing.
pub async fn export_servers_csv(
    pool: &PgPool,
    org_id: Uuid,
) -> Result<String, EngineError> {
    type Row = (
        String,          // hostname
        Option<String>,  // profile_code
        Option<String>,  // building_code
        Option<i64>,     // asn
        Option<String>,  // loopback_ip (host, no prefix)
        Option<String>,  // management_ip (host, no prefix)
        i64,             // nic_count
        String,          // status
    );
    let rows: Vec<Row> = sqlx::query_as(
        r#"SELECT s.hostname,
                  sp.profile_code,
                  b.building_code,
                  aa.asn                               AS asn,
                  host(lb.address)                     AS loopback_ip,
                  host(s.management_ip)                AS management_ip,
                  COALESCE(nc.n, 0)                    AS nic_count,
                  s.status::text                       AS status
             FROM net.server s
             LEFT JOIN net.server_profile    sp ON sp.id = s.server_profile_id    AND sp.deleted_at IS NULL
             LEFT JOIN net.building          b  ON b.id  = s.building_id          AND b.deleted_at IS NULL
             LEFT JOIN net.asn_allocation    aa ON aa.id = s.asn_allocation_id    AND aa.deleted_at IS NULL
             LEFT JOIN net.ip_address        lb ON lb.id = s.loopback_ip_address_id AND lb.deleted_at IS NULL
             LEFT JOIN LATERAL (
                 SELECT COUNT(*) AS n
                   FROM net.server_nic sn
                  WHERE sn.server_id = s.id AND sn.deleted_at IS NULL
             ) nc ON true
            WHERE s.organization_id = $1 AND s.deleted_at IS NULL
            ORDER BY s.hostname"#)
        .bind(org_id)
        .fetch_all(pool)
        .await?;

    let mut out = String::with_capacity(128 + rows.len() * 112);
    out.push_str(&csv_row(&[
        "hostname".into(), "profile_code".into(), "building_code".into(),
        "asn".into(), "loopback_ip".into(), "management_ip".into(),
        "nic_count".into(), "status".into(),
    ]));
    for (hostname, profile, building, asn, loopback, mgmt, nic_count, status) in rows {
        out.push_str(&csv_row(&[
            csv_escape(&hostname),
            csv_escape(&profile.unwrap_or_default()),
            csv_escape(&building.unwrap_or_default()),
            asn.map(|a| a.to_string()).unwrap_or_default(),
            csv_escape(&loopback.unwrap_or_default()),
            csv_escape(&mgmt.unwrap_or_default()),
            nic_count.to_string(),
            csv_escape(&status),
        ]));
    }
    Ok(out)
}

// ─── Subnet export ───────────────────────────────────────────────────────

/// CSV dump of `net.subnet`. Columns:
///
///   subnet_code, display_name, network, vlan_id, pool_code,
///   scope_level, status
///
/// `vlan_id` is the numeric VLAN tag from the linked `net.vlan`
/// (NULL when the subnet isn't VLAN-bound). `pool_code` identifies
/// the parent IP pool. Ordered by `subnet_code` so diffing against
/// prior exports stays stable.
pub async fn export_subnets_csv(
    pool: &PgPool,
    org_id: Uuid,
) -> Result<String, EngineError> {
    type Row = (
        String,          // subnet_code
        String,          // display_name
        String,          // network (cidr text)
        Option<i32>,     // vlan_id
        Option<String>,  // pool_code
        String,          // scope_level
        Option<String>,  // scope_entity_code (compound per scope_level)
        String,          // status
    );
    // scope_entity_code is a compound expression keyed off scope_level
    // — Region emits REGION_CODE alone (globally unique per tenant),
    // Site emits REGION_CODE/SITE_CODE (site_code only unique within
    // region), Building emits BUILDING_CODE (globally unique per
    // tenant), Floor/Room add FLOOR_CODE/ROOM_CODE segments. The join
    // chain is verbose but keeps bulk export in lockstep with bulk
    // import without a second query.
    let rows: Vec<Row> = sqlx::query_as(
        r#"SELECT s.subnet_code,
                  s.display_name,
                  s.network::text                  AS network,
                  v.vlan_id                        AS vlan_id,
                  p.pool_code                      AS pool_code,
                  s.scope_level,
                  CASE s.scope_level
                      WHEN 'Region'   THEN rg.region_code
                      WHEN 'Site'     THEN sr.region_code || '/' || st.site_code
                      WHEN 'Building' THEN sb.building_code
                      WHEN 'Floor'    THEN fb.building_code || '/' || f.floor_code
                      WHEN 'Room'     THEN rb.building_code || '/' || rf.floor_code || '/' || r.room_code
                      ELSE NULL
                  END                              AS scope_entity_code,
                  s.status::text                   AS status
             FROM net.subnet s
             LEFT JOIN net.vlan     v  ON v.id = s.vlan_id AND v.deleted_at IS NULL
             LEFT JOIN net.ip_pool  p  ON p.id = s.pool_id AND p.deleted_at IS NULL
             -- Region scope: single-hop to net.region.
             LEFT JOIN net.region   rg ON rg.id = s.scope_entity_id
                                      AND s.scope_level = 'Region'
             -- Site scope: net.site + its parent region.
             LEFT JOIN net.site     st ON st.id = s.scope_entity_id
                                      AND s.scope_level = 'Site'
             LEFT JOIN net.region   sr ON sr.id = st.region_id
             -- Building scope: single-hop to net.building.
             LEFT JOIN net.building sb ON sb.id = s.scope_entity_id
                                      AND s.scope_level = 'Building'
             -- Floor scope: net.floor + its parent building.
             LEFT JOIN net.floor    f  ON f.id  = s.scope_entity_id
                                      AND s.scope_level = 'Floor'
             LEFT JOIN net.building fb ON fb.id = f.building_id
             -- Room scope: net.room + floor + building.
             LEFT JOIN net.room     r  ON r.id  = s.scope_entity_id
                                      AND s.scope_level = 'Room'
             LEFT JOIN net.floor    rf ON rf.id = r.floor_id
             LEFT JOIN net.building rb ON rb.id = rf.building_id
            WHERE s.organization_id = $1 AND s.deleted_at IS NULL
            ORDER BY s.subnet_code"#)
        .bind(org_id)
        .fetch_all(pool)
        .await?;

    let mut out = String::with_capacity(128 + rows.len() * 96);
    out.push_str(&csv_row(&[
        "subnet_code".into(), "display_name".into(), "network".into(),
        "vlan_id".into(), "pool_code".into(),
        "scope_level".into(), "scope_entity_code".into(), "status".into(),
    ]));
    for (code, name, network, vlan, pool_code, scope, scope_code, status) in rows {
        out.push_str(&csv_row(&[
            csv_escape(&code),
            csv_escape(&name),
            csv_escape(&network),
            vlan.map(|n| n.to_string()).unwrap_or_default(),
            csv_escape(&pool_code.unwrap_or_default()),
            csv_escape(&scope),
            csv_escape(&scope_code.unwrap_or_default()),
            csv_escape(&status),
        ]));
    }
    Ok(out)
}

// ─── ASN allocation export ───────────────────────────────────────────────

/// CSV dump of `net.asn_allocation` — one row per allocated ASN.
/// Columns:
///
///   asn, allocated_to_type, allocated_to_hostname, block_code,
///   allocated_at, status
///
/// `allocated_to_hostname` resolves the opaque `allocated_to_id` via
/// the type-specific table (device or server) so operators reading
/// the export see "MEP-91-CORE02" rather than a UUID. Allocations
/// pointing at entities outside those two types (future: building-
/// level allocations) render an empty hostname rather than NULL —
/// the CSV consumer then needs no branch on type.
///
/// Ordered by `asn` so the ASN space reads bottom-up.
pub async fn export_asn_allocations_csv(
    pool: &PgPool,
    org_id: Uuid,
) -> Result<String, EngineError> {
    type Row = (
        i64,             // asn
        String,          // allocated_to_type
        Option<String>,  // allocated_to_hostname (from device or server)
        Option<String>,  // block_code
        chrono::DateTime<chrono::Utc>,  // allocated_at
        String,          // status
    );
    let rows: Vec<Row> = sqlx::query_as(
        r#"SELECT aa.asn,
                  aa.allocated_to_type,
                  COALESCE(d.hostname, s.hostname) AS allocated_to_hostname,
                  ab.block_code                    AS block_code,
                  aa.allocated_at,
                  aa.status::text                  AS status
             FROM net.asn_allocation aa
             LEFT JOIN net.asn_block ab ON ab.id = aa.block_id       AND ab.deleted_at IS NULL
             LEFT JOIN net.device    d  ON aa.allocated_to_type = 'Device'
                                       AND d.id = aa.allocated_to_id
                                       AND d.deleted_at IS NULL
             LEFT JOIN net.server    s  ON aa.allocated_to_type = 'Server'
                                       AND s.id = aa.allocated_to_id
                                       AND s.deleted_at IS NULL
            WHERE aa.organization_id = $1 AND aa.deleted_at IS NULL
            ORDER BY aa.asn"#)
        .bind(org_id)
        .fetch_all(pool)
        .await?;

    let mut out = String::with_capacity(128 + rows.len() * 96);
    out.push_str(&csv_row(&[
        "asn".into(), "allocated_to_type".into(), "allocated_to_hostname".into(),
        "block_code".into(), "allocated_at".into(), "status".into(),
    ]));
    for (asn, ty, hostname, block, allocated_at, status) in rows {
        out.push_str(&csv_row(&[
            asn.to_string(),
            csv_escape(&ty),
            csv_escape(&hostname.unwrap_or_default()),
            csv_escape(&block.unwrap_or_default()),
            csv_escape(&allocated_at.to_rfc3339()),
            csv_escape(&status),
        ]));
    }
    Ok(out)
}

// ─── MLAG domain export ──────────────────────────────────────────────────

/// CSV dump of `net.mlag_domain`. Columns:
///
///   domain_id, display_name, pool_code, scope_level,
///   scope_entity_id, status
///
/// `scope_entity_id` is a UUID — the *human* display (building code
/// / site code / region code) depends on `scope_level` and would
/// require a polymorphic join the export layer intentionally skips.
/// Operators who want the building-code column can correlate via
/// the subnet / device exports.
///
/// Ordered by `domain_id` so the MLAG ID space reads bottom-up.
pub async fn export_mlag_domains_csv(
    pool: &PgPool,
    org_id: Uuid,
) -> Result<String, EngineError> {
    type Row = (
        i32,             // domain_id
        String,          // display_name
        Option<String>,  // pool_code
        String,          // scope_level
        Option<Uuid>,    // scope_entity_id
        String,          // status
    );
    let rows: Vec<Row> = sqlx::query_as(
        r#"SELECT m.domain_id,
                  m.display_name,
                  p.pool_code        AS pool_code,
                  m.scope_level,
                  m.scope_entity_id,
                  m.status::text     AS status
             FROM net.mlag_domain m
             LEFT JOIN net.mlag_domain_pool p ON p.id = m.pool_id AND p.deleted_at IS NULL
            WHERE m.organization_id = $1 AND m.deleted_at IS NULL
            ORDER BY m.domain_id"#)
        .bind(org_id)
        .fetch_all(pool)
        .await?;

    let mut out = String::with_capacity(128 + rows.len() * 96);
    out.push_str(&csv_row(&[
        "domain_id".into(), "display_name".into(), "pool_code".into(),
        "scope_level".into(), "scope_entity_id".into(), "status".into(),
    ]));
    for (domain_id, name, pool_code, scope, scope_entity, status) in rows {
        out.push_str(&csv_row(&[
            domain_id.to_string(),
            csv_escape(&name),
            csv_escape(&pool_code.unwrap_or_default()),
            csv_escape(&scope),
            scope_entity.map(|id| id.to_string()).unwrap_or_default(),
            csv_escape(&status),
        ]));
    }
    Ok(out)
}

// ─── DHCP relay target export ────────────────────────────────────────────

/// CSV dump of `net.dhcp_relay_target` — symmetric with the DHCP relay
/// CRUD that's shipped on `/api/net/dhcp-relay-targets`. Columns:
///
///   vlan_id, server_ip, priority, linked_ip_address_id, notes, status
///
/// `vlan_id` is the human-readable numeric tag (joined through
/// `net.vlan`), not the internal UUID; that way operators pasting the
/// export into a ticket see "120" rather than a UUID nobody can
/// correlate.
///
/// `linked_ip_address_id` remains a UUID when set — there's no useful
/// "code" to project for a single IP, and the `address` column on
/// `net.ip_address` is already available via the
/// `/api/net/ip-addresses/export` dump if operators want to correlate.
///
/// Ordered `(vlan_id, priority ASC)` so rows group by VLAN with the
/// primary server first within each group — same ordering the PicOS
/// renderer uses when emitting `set protocols dhcp relay` lines.
pub async fn export_dhcp_relay_targets_csv(
    pool: &PgPool,
    org_id: Uuid,
) -> Result<String, EngineError> {
    type Row = (
        i32,             // vlan_id (numeric)
        String,          // server_ip (bare host)
        i32,             // priority
        Option<Uuid>,    // ip_address_id
        Option<String>,  // notes
        String,          // status
    );
    let rows: Vec<Row> = sqlx::query_as(
        r#"SELECT v.vlan_id,
                  host(drt.server_ip)     AS server_ip,
                  drt.priority,
                  drt.ip_address_id,
                  drt.notes,
                  drt.status::text        AS status
             FROM net.dhcp_relay_target drt
             JOIN net.vlan v ON v.id = drt.vlan_id AND v.deleted_at IS NULL
            WHERE drt.organization_id = $1 AND drt.deleted_at IS NULL
            ORDER BY v.vlan_id, drt.priority ASC, drt.server_ip"#)
        .bind(org_id)
        .fetch_all(pool)
        .await?;

    let mut out = String::with_capacity(128 + rows.len() * 96);
    out.push_str(&csv_row(&[
        "vlan_id".into(), "server_ip".into(), "priority".into(),
        "linked_ip_address_id".into(), "notes".into(), "status".into(),
    ]));
    for (vlan_id, server_ip, priority, ip_address_id, notes, status) in rows {
        out.push_str(&csv_row(&[
            vlan_id.to_string(),
            csv_escape(&server_ip),
            priority.to_string(),
            ip_address_id.map(|id| id.to_string()).unwrap_or_default(),
            csv_escape(&notes.unwrap_or_default()),
            csv_escape(&status),
        ]));
    }
    Ok(out)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn csv_escape_passes_through_plain_fields_unchanged() {
        assert_eq!(csv_escape("hostname"),      "hostname");
        assert_eq!(csv_escape("MEP-91-CORE02"), "MEP-91-CORE02");
        assert_eq!(csv_escape(""),              "");
        assert_eq!(csv_escape("10.11.152.2"),   "10.11.152.2");
    }

    #[test]
    fn csv_escape_wraps_and_doubles_internal_quotes() {
        // Field containing a " must be wrapped AND have the " doubled.
        assert_eq!(csv_escape(r#"a"b"#), r#""a""b""#);
        // Lone quote by itself still wraps.
        assert_eq!(csv_escape(r#"""#),   r#""""""#);
    }

    #[test]
    fn csv_escape_wraps_on_comma() {
        // Field containing a comma wraps but no double-escape needed.
        assert_eq!(csv_escape("a,b"), r#""a,b""#);
        assert_eq!(csv_escape("trailing,"), r#""trailing,""#);
    }

    #[test]
    fn csv_escape_wraps_on_newline_or_crlf() {
        // Embedded line breaks within a field are legal in RFC 4180
        // as long as the field is wrapped in double quotes.
        assert_eq!(csv_escape("a\nb"),   "\"a\nb\"");
        assert_eq!(csv_escape("a\r\nb"), "\"a\r\nb\"");
        assert_eq!(csv_escape("a\rb"),   "\"a\rb\"");
    }

    #[test]
    fn csv_escape_handles_combined_metacharacters() {
        // Comma + quote + newline in one field. All three triggers
        // apply; the quote gets doubled, the whole field wrapped.
        let input = "desc: \"foo, bar\"\nline2";
        let out   = csv_escape(input);
        assert_eq!(out, "\"desc: \"\"foo, bar\"\"\nline2\"");
    }

    #[test]
    fn csv_row_terminates_with_crlf() {
        let r = csv_row(&["a".into(), "b".into(), "c".into()]);
        assert_eq!(r, "a,b,c\r\n");
    }

    #[test]
    fn csv_row_joins_prepared_escaped_fields_unchanged() {
        // csv_row does NOT re-escape — caller already ran csv_escape.
        // Passing raw untrusted data to csv_row is a bug; keep the
        // two responsibilities separated.
        let r = csv_row(&[csv_escape("a,b"), csv_escape("c")]);
        assert_eq!(r, "\"a,b\",c\r\n");
    }

    #[test]
    fn csv_row_handles_empty_field_list() {
        // Degenerate but shouldn't panic — empty row has just the
        // terminator.
        let r = csv_row(&[]);
        assert_eq!(r, "\r\n");
    }
}
