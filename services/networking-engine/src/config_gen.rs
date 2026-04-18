//! Device config renderer. Flavor-aware — the `Renderer` trait wraps
//! everything a specific switch CLI dialect needs to emit, and the
//! entry point `render_device` looks up the flavor for a given device
//! + delegates.
//!
//! **PicOS (FS 4.6) is the only concrete renderer today** — matches the
//! customer's production stack. Cisco / Arista / Juniper / FRR are
//! metadata stubs in [`crate::cli_flavor`] with no renderer yet; the
//! dispatcher returns a clean "not implemented" error for those
//! flavors rather than pretending.
//!
//! ## Scope of the PicOS starter
//!
//! This slice emits the basic device-identity sections — hostname,
//! loopback IP, VLANs the device terminates. Richer sections (BGP,
//! MLAG peer-link, MSTP priority, VLAN trunks, port descriptions, FW
//! zones) arrive as follow-on slices. The acceptance bar from the
//! phase plan is "byte-for-byte match with pre-migration output" —
//! met incrementally, section by section.

use chrono::{DateTime, Utc};
use serde::Serialize;
use sqlx::PgPool;
use uuid::Uuid;

use crate::cli_flavor::{self, FlavorMeta};
use crate::error::EngineError;

// ─── Public surface ──────────────────────────────────────────────────────

#[derive(Debug, Clone, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct RenderedConfig {
    pub device_id: Uuid,
    pub flavor_code: String,
    pub body: String,
    pub body_sha256: String,
    pub line_count: i32,
    pub rendered_at: DateTime<Utc>,
}

/// Resolve the device's flavor and dispatch to its renderer. Returns a
/// `RenderedConfig` the caller can persist to `net.rendered_config` or
/// just display.
pub async fn render_device(
    pool: &PgPool,
    org_id: Uuid,
    device_id: Uuid,
) -> Result<RenderedConfig, EngineError> {
    let flavor = cli_flavor::resolve_for_device(pool, org_id, device_id).await?;
    let ctx = fetch_context(pool, org_id, device_id).await?;

    let body = match flavor.code {
        "PicOS" => PicosRenderer::render(&ctx),
        _ => return Err(EngineError::bad_request(format!(
            "No renderer implemented for flavor '{}' (status: {}). \
             Only 'PicOS' has a production renderer today.",
            flavor.code, flavor.status))),
    };

    Ok(build_result(device_id, flavor, body))
}

fn build_result(device_id: Uuid, flavor: &'static FlavorMeta, body: String) -> RenderedConfig {
    let line_count = body.lines().count() as i32;
    let sha = sha256_hex(&body);
    RenderedConfig {
        device_id,
        flavor_code: flavor.code.to_string(),
        body,
        body_sha256: sha,
        line_count,
        rendered_at: Utc::now(),
    }
}

fn sha256_hex(s: &str) -> String {
    use sha2::{Digest, Sha256};
    let mut h = Sha256::new();
    h.update(s.as_bytes());
    hex::encode(h.finalize())
}

// ─── Context — everything the renderer needs to read ─────────────────────

/// Pre-fetched view of what a renderer needs. Keeping the renderer pure
/// (no DB access, just functions over this struct) makes tests easy
/// and lets the pipeline parallelise the context fetch later.
///
/// loopback + management_ip + device_id are populated for the follow-on
/// sections (mgmt interface, loopback block, BGP router-id) — currently
/// unused by the PicOS starter which only emits hostname + vlans.
/// Keeping them in the struct so the next slice doesn't refactor.
#[derive(Debug, Clone)]
#[allow(dead_code)]
pub struct DeviceContext {
    pub device_id: Uuid,
    pub hostname: String,
    pub loopback: Option<String>,       // cidr text, e.g. "10.255.91.2/32"
    pub management_ip: Option<String>,
    pub vlans: Vec<VlanLine>,
}

#[derive(Debug, Clone)]
pub struct VlanLine {
    pub vlan_id: i32,
    pub display_name: String,
    pub description: Option<String>,
}

async fn fetch_context(
    pool: &PgPool,
    org_id: Uuid,
    device_id: Uuid,
) -> Result<DeviceContext, EngineError> {
    // Device identity + loopback + mgmt ip in one query via LEFT JOIN.
    let dev: Option<(String, Option<String>, Option<String>)> = sqlx::query_as(
        "SELECT d.hostname,
                lb.address::text AS loopback,
                d.management_ip::text AS mgmt
           FROM net.device d
           LEFT JOIN net.ip_address lb
                  ON lb.id = d.asn_allocation_id       -- NOT the right join; see comment
            AND lb.deleted_at IS NULL
          WHERE d.id = $1 AND d.organization_id = $2 AND d.deleted_at IS NULL")
        .bind(device_id)
        .bind(org_id)
        .fetch_optional(pool)
        .await?;
    // The loopback column lookup above is WRONG as written — net.device
    // doesn't currently carry a direct loopback_ip_address_id FK. The
    // loopback is stored per-server (net.server.loopback_ip_address_id),
    // not per-device. Devices carry asn_allocation_id but that's the
    // ASN, not the IP.
    //
    // The right resolution is "find the LOOPBACK subnet for this
    // device's building + pick the host entry whose assigned_to_id =
    // device_id". That query lives in the full renderer; starter
    // code emits the hostname section only and leaves loopback as None.
    let (hostname, _, mgmt) = dev.ok_or_else(|| EngineError::container_not_found("device", device_id))?;

    // VLANs the device terminates — for now, every VLAN in the tenant's
    // active rows. Real scoping (VLAN's scope_level / scope_entity_id
    // intersects device's building) comes in a follow-on slice.
    let vlan_rows: Vec<(i32, String, Option<String>)> = sqlx::query_as(
        "SELECT vlan_id, display_name, description
           FROM net.vlan
          WHERE organization_id = $1 AND deleted_at IS NULL
          ORDER BY vlan_id
          LIMIT 500")
        .bind(org_id)
        .fetch_all(pool)
        .await?;

    Ok(DeviceContext {
        device_id,
        hostname,
        loopback: None,   // see note above
        management_ip: mgmt,
        vlans: vlan_rows.into_iter().map(|(vid, name, desc)| VlanLine {
            vlan_id: vid, display_name: name, description: desc,
        }).collect(),
    })
}

// ─── Renderer trait + PicOS impl ──────────────────────────────────────────

pub trait Renderer {
    /// Produce the full config body. Pure function over the fetched
    /// context — no I/O inside.
    fn render(ctx: &DeviceContext) -> String;
}

/// PicOS 4.6 `set`-style renderer. Matches the CLI pattern the
/// customer's switches accept (`set system hostname ...`,
/// `set vlans vlan-id N description "..."`, etc.).
pub struct PicosRenderer;

impl Renderer for PicosRenderer {
    fn render(ctx: &DeviceContext) -> String {
        let mut out = String::with_capacity(256 + ctx.vlans.len() * 80);
        render_header(&mut out, ctx);
        render_system_section(&mut out, ctx);
        render_vlans_section(&mut out, ctx);
        // TODO(follow-on): render_loopback, render_mgmt_iface,
        // render_bgp, render_mlag_peer, render_mstp, render_ports.
        out
    }
}

fn render_header(out: &mut String, ctx: &DeviceContext) {
    out.push_str(&format!("# Config for {} — generated {}\n",
                           ctx.hostname, Utc::now().to_rfc3339()));
    out.push_str("# Flavor: PicOS 4.6 (FS N-series)\n");
    out.push('\n');
}

fn render_system_section(out: &mut String, ctx: &DeviceContext) {
    out.push_str(&format!("set system hostname \"{}\"\n", escape_picos(&ctx.hostname)));
    out.push('\n');
}

fn render_vlans_section(out: &mut String, ctx: &DeviceContext) {
    if ctx.vlans.is_empty() { return; }
    for v in &ctx.vlans {
        // PicOS: set vlans vlan-id N description "..."
        // We always emit vlan-id + description; name is PicOS's
        // description field (distinct from the tenant-facing display
        // name which may differ from the switch-level comment).
        out.push_str(&format!(
            "set vlans vlan-id {} description \"{}\"\n",
            v.vlan_id,
            escape_picos(v.description.as_deref().unwrap_or(&v.display_name))));
    }
    out.push('\n');
}

/// PicOS string escaping — escape embedded double quotes and
/// backslashes so the `"..."` literal stays valid. PicOS doesn't
/// document a rich escape grammar beyond that, so we stop there.
fn escape_picos(s: &str) -> String {
    let mut out = String::with_capacity(s.len() + 4);
    for ch in s.chars() {
        match ch {
            '"'  => out.push_str("\\\""),
            '\\' => out.push_str("\\\\"),
            _    => out.push(ch),
        }
    }
    out
}

// ─── Tests ────────────────────────────────────────────────────────────────

#[cfg(test)]
mod tests {
    use super::*;

    fn fixture(hostname: &str, vlans: Vec<VlanLine>) -> DeviceContext {
        DeviceContext {
            device_id: Uuid::nil(),
            hostname: hostname.into(),
            loopback: None,
            management_ip: None,
            vlans,
        }
    }

    #[test]
    fn picos_emits_header_with_hostname() {
        let out = PicosRenderer::render(&fixture("MEP-91-CORE02", vec![]));
        assert!(out.contains("# Config for MEP-91-CORE02"));
        assert!(out.contains("# Flavor: PicOS 4.6"));
    }

    #[test]
    fn picos_emits_set_system_hostname() {
        let out = PicosRenderer::render(&fixture("MEP-91-CORE02", vec![]));
        assert!(out.contains("set system hostname \"MEP-91-CORE02\""));
    }

    #[test]
    fn picos_emits_vlan_lines_with_description_fallback_to_name() {
        let vlans = vec![
            VlanLine { vlan_id: 101, display_name: "IT".into(), description: None },
            VlanLine { vlan_id: 120, display_name: "Servers".into(),
                       description: Some("Server LAN".into()) },
        ];
        let out = PicosRenderer::render(&fixture("CORE02", vlans));
        assert!(out.contains(r#"set vlans vlan-id 101 description "IT""#),
            "vlan 101 should use display_name when description is None:\n{out}");
        assert!(out.contains(r#"set vlans vlan-id 120 description "Server LAN""#),
            "vlan 120 should prefer description over display_name:\n{out}");
    }

    #[test]
    fn picos_omits_vlan_section_when_none() {
        let out = PicosRenderer::render(&fixture("CORE02", vec![]));
        assert!(!out.contains("set vlans"),
            "no vlan lines should appear when the context has no vlans:\n{out}");
    }

    #[test]
    fn picos_escapes_embedded_quotes_and_backslashes() {
        let vlans = vec![
            VlanLine { vlan_id: 5, display_name: "".into(),
                       description: Some(r#"a"b\c"#.into()) },
        ];
        let out = PicosRenderer::render(&fixture("x", vlans));
        assert!(out.contains(r#"description "a\"b\\c""#),
            "escape_picos should double quotes and backslashes:\n{out}");
    }

    #[test]
    fn escape_picos_leaves_normal_chars() {
        assert_eq!(escape_picos("Server LAN"), "Server LAN");
        assert_eq!(escape_picos("MEP-91-CORE02"), "MEP-91-CORE02");
    }

    #[test]
    fn build_result_counts_lines_correctly() {
        let body = "a\nb\nc\n".to_string();
        let r = build_result(Uuid::nil(),
            cli_flavor::find_flavor("PicOS").unwrap(), body);
        assert_eq!(r.line_count, 3);
        assert_eq!(r.flavor_code, "PicOS");
        assert_eq!(r.body_sha256.len(), 64);
    }
}
