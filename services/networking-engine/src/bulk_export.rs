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
///   vlan_id, display_name, description, scope_level, template_code, status
///
/// `template_code` joins through `net.vlan_template` — lets the
/// spreadsheet show "IT / Servers / DMZ" rather than a UUID that
/// means nothing outside the DB. VLANs ordered by `vlan_id` so the
/// export reads bottom-up the way operators think about it.
pub async fn export_vlans_csv(
    pool: &PgPool,
    org_id: Uuid,
) -> Result<String, EngineError> {
    type Row = (i32, String, Option<String>, String, Option<String>, String);
    let rows: Vec<Row> = sqlx::query_as(
        r#"SELECT v.vlan_id,
                  v.display_name,
                  v.description,
                  v.scope_level,
                  t.template_code,
                  v.status::text AS status
             FROM net.vlan v
             LEFT JOIN net.vlan_template t ON t.id = v.template_id AND t.deleted_at IS NULL
            WHERE v.organization_id = $1 AND v.deleted_at IS NULL
            ORDER BY v.vlan_id"#)
        .bind(org_id)
        .fetch_all(pool)
        .await?;

    let mut out = String::with_capacity(128 + rows.len() * 96);
    out.push_str(&csv_row(&[
        "vlan_id".into(), "display_name".into(), "description".into(),
        "scope_level".into(), "template_code".into(), "status".into(),
    ]));
    for (vlan_id, name, desc, scope, tpl, status) in rows {
        out.push_str(&csv_row(&[
            vlan_id.to_string(),
            csv_escape(&name),
            csv_escape(&desc.unwrap_or_default()),
            csv_escape(&scope),
            csv_escape(&tpl.unwrap_or_default()),
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
