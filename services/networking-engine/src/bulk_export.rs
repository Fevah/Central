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
