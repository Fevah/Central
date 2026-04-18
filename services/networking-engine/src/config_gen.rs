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
//! loopback IP (`lo0`), management SVI (`vlan-152`), and the VLANs the
//! device terminates. Richer sections (BGP, MLAG peer-link, MSTP
//! priority, VLAN trunks, port descriptions, FW zones) arrive as
//! follow-on slices. The acceptance bar from the phase plan is
//! "byte-for-byte match with pre-migration output" — met incrementally,
//! section by section.

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
/// `loopback` / `management_ip` come back as Postgres inet text
/// (e.g. `"10.255.91.2/32"`, `"10.11.152.2/24"`). The renderer splits
/// them into address + prefix for the PicOS `set l3-interface` syntax.
/// `device_id` is passed through so follow-on sections that need the
/// id (BGP router-id derivation, port lookups) don't re-fetch.
#[derive(Debug, Clone)]
#[allow(dead_code)]
pub struct DeviceContext {
    pub device_id: Uuid,
    pub hostname: String,
    pub loopback: Option<String>,
    pub management_ip: Option<String>,
    pub vlans: Vec<VlanLine>,
    pub bgp: Option<BgpContext>,
    pub bgp_neighbors: Vec<BgpNeighborLine>,
}

/// BGP scalar context — local AS + router-id source. Neighbors live
/// in `DeviceContext::bgp_neighbors`, derived from `net.link_endpoint`
/// rather than the SSH-synced read-back `public.bgp_neighbors`.
#[derive(Debug, Clone)]
pub struct BgpContext {
    pub local_as: i64,
    /// Defaults to 4 (customer standard across every PicOS core today —
    /// see `CLAUDE.md` "BGP ECMP max-paths 4 on all core switches").
    pub max_paths: i32,
}

/// One peer from the device's point of view. Derived from P2P / B2B
/// links by looking at the other endpoint of every link this device
/// terminates. `remote_as = None` means the peer's ASN isn't allocated
/// in `net.asn_allocation` yet — the renderer emits `"?"` to match
/// legacy behaviour for cross-building B2B peers whose far end isn't
/// modelled in our tenant DB.
#[derive(Debug, Clone)]
pub struct BgpNeighborLine {
    pub peer_ip: String,
    pub remote_as: Option<i64>,
    /// Description prefix — "P2P" or "B2B". Kept abstract so the
    /// renderer can format `"P2P-{peer_name}"` or `"B2B-{building}"`
    /// without re-deriving the link type.
    pub kind: BgpNeighborKind,
    /// For P2P, the peer device's hostname. For B2B, the peer
    /// building's `building_code`. Either may be empty if the peer
    /// endpoint is partially modelled.
    pub peer_label: String,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum BgpNeighborKind {
    P2P,
    B2B,
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
    let dev: Option<(String, Option<String>)> = sqlx::query_as(
        "SELECT d.hostname, d.management_ip::text AS mgmt
           FROM net.device d
          WHERE d.id = $1 AND d.organization_id = $2 AND d.deleted_at IS NULL")
        .bind(device_id)
        .bind(org_id)
        .fetch_optional(pool)
        .await?;
    let (hostname, mgmt) = dev.ok_or_else(|| EngineError::container_not_found("device", device_id))?;

    // Loopback: `net.device` has no direct FK to its loopback IP. Devices
    // live in a LOOPBACK-coded subnet via net.ip_address.assigned_to_id
    // (same convention ServerCreationService uses for server loopbacks).
    // A device should only ever have one active loopback entry; LIMIT 1
    // protects the renderer if the invariant is ever broken.
    let loopback: Option<(String,)> = sqlx::query_as(
        "SELECT ip.address::text
           FROM net.ip_address ip
           JOIN net.subnet s ON s.id = ip.subnet_id AND s.deleted_at IS NULL
          WHERE ip.organization_id = $1
            AND ip.assigned_to_id = $2
            AND ip.assigned_to_type = 'Device'
            AND s.subnet_code ILIKE 'LOOPBACK%'
            AND ip.deleted_at IS NULL
          ORDER BY ip.assigned_at ASC
          LIMIT 1")
        .bind(org_id)
        .bind(device_id)
        .fetch_optional(pool)
        .await?;

    // BGP local-as: resolved via the device's asn_allocation_id FK,
    // NOT from public.bgp_config (which is the SSH-synced read-back of
    // live state, not the target-state source of truth).
    let asn: Option<(i64,)> = sqlx::query_as(
        "SELECT aa.asn
           FROM net.device d
           JOIN net.asn_allocation aa
             ON aa.id = d.asn_allocation_id AND aa.deleted_at IS NULL
          WHERE d.id = $1 AND d.organization_id = $2 AND d.deleted_at IS NULL")
        .bind(device_id)
        .bind(org_id)
        .fetch_optional(pool)
        .await?;

    // BGP neighbors — derived from link endpoints the device
    // terminates on P2P or B2B links. For each link where this device
    // is endpoint X, the OTHER endpoint gives us peer IP + (possibly)
    // peer device + peer's ASN + peer's building. Cross-building B2B
    // peers whose far end isn't in net.asn_allocation come back with
    // NULL peer_asn; the renderer emits "?" to match legacy output.
    let neighbor_rows: Vec<(String, String, Option<i64>, Option<String>, Option<String>)> = sqlx::query_as(
        r#"SELECT lt.type_code,
                  ip.address::text            AS peer_ip,
                  aa.asn                      AS peer_asn,
                  dev_peer.hostname           AS peer_hostname,
                  b_peer.building_code        AS peer_building
             FROM net.link_endpoint le_self
             JOIN net.link l
               ON l.id = le_self.link_id AND l.deleted_at IS NULL
             JOIN net.link_type lt
               ON lt.id = l.link_type_id
             JOIN net.link_endpoint le_peer
               ON le_peer.link_id = le_self.link_id
              AND le_peer.id <> le_self.id
              AND le_peer.deleted_at IS NULL
             JOIN net.ip_address ip
               ON ip.id = le_peer.ip_address_id AND ip.deleted_at IS NULL
             LEFT JOIN net.device dev_peer
               ON dev_peer.id = le_peer.device_id AND dev_peer.deleted_at IS NULL
             LEFT JOIN net.asn_allocation aa
               ON aa.id = dev_peer.asn_allocation_id AND aa.deleted_at IS NULL
             LEFT JOIN net.building b_peer
               ON b_peer.id = dev_peer.building_id AND b_peer.deleted_at IS NULL
            WHERE le_self.organization_id = $1
              AND le_self.device_id       = $2
              AND le_self.deleted_at IS NULL
              AND lt.type_code IN ('P2P','B2B')
            ORDER BY ip.address"#)
        .bind(org_id)
        .bind(device_id)
        .fetch_all(pool)
        .await?;

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
        loopback: loopback.map(|(addr,)| addr),
        management_ip: mgmt,
        vlans: vlan_rows.into_iter().map(|(vid, name, desc)| VlanLine {
            vlan_id: vid, display_name: name, description: desc,
        }).collect(),
        bgp: asn.map(|(n,)| BgpContext { local_as: n, max_paths: 4 }),
        bgp_neighbors: neighbor_rows.into_iter().map(|(type_code, peer_ip, peer_asn, peer_host, peer_bldg)| {
            let (kind, peer_label) = match type_code.as_str() {
                "P2P" => (BgpNeighborKind::P2P, peer_host.unwrap_or_default()),
                // B2B falls back to building code; legacy emits "B2B-{building}"
                _     => (BgpNeighborKind::B2B, peer_bldg.unwrap_or_default()),
            };
            // Peer IP comes back as inet text "a.b.c.d/NN" — strip the
            // prefix so neighbor lines use a bare host address.
            let peer_ip_host = split_inet_text(&peer_ip)
                .map(|(h, _)| h.to_string())
                .unwrap_or(peer_ip);
            BgpNeighborLine { peer_ip: peer_ip_host, remote_as: peer_asn, kind, peer_label }
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
        render_loopback_section(&mut out, ctx);
        render_management_interface_section(&mut out, ctx);
        render_vlans_section(&mut out, ctx);
        render_bgp_scalar_section(&mut out, ctx);
        // TODO(follow-on): render_bgp_neighbors (derived from
        // net.link_endpoint), render_mlag_peer, render_mstp, render_ports.
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

/// Split a Postgres `inet` text value like `"10.255.91.2/32"` or
/// `"10.11.152.2/24"` into (host, prefix). Returns `None` if the input
/// can't be parsed — the renderer then skips the section rather than
/// emitting a half-formed line.
fn split_inet_text(s: &str) -> Option<(&str, u8)> {
    let (host, rest) = s.split_once('/')?;
    let prefix: u8 = rest.parse().ok()?;
    if host.is_empty() { return None; }
    Some((host, prefix))
}

fn render_loopback_section(out: &mut String, ctx: &DeviceContext) {
    let Some(lb) = ctx.loopback.as_deref() else { return; };
    let Some((host, prefix)) = split_inet_text(lb) else { return; };
    // PicOS loopback uses the "lo0" alias under l3-interface.
    out.push_str(&format!(
        "set l3-interface loopback lo0 address {} prefix-length {}\n",
        host, prefix));
    out.push('\n');
}

fn render_management_interface_section(out: &mut String, ctx: &DeviceContext) {
    let Some(mgmt) = ctx.management_ip.as_deref() else { return; };
    let Some((host, prefix)) = split_inet_text(mgmt) else { return; };
    // Management lives on the VLAN-152 SVI across every site in the
    // Immunocore footprint (see CLAUDE.md — "VLAN 152: Devices").
    // Richer sections (management-instance default-route, ssh allow-list)
    // come in a follow-on slice.
    out.push_str(&format!(
        "set l3-interface vlan-interface vlan-152 address {} prefix-length {}\n",
        host, prefix));
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

/// Full BGP section — scalar lines (local-as, ebgp-requires-policy,
/// router-id), then one pair of lines per neighbor (remote-as +
/// description), then the two fixed `ipv4-unicast` lines. Order
/// matches the legacy `ConfigBuilderService` output byte-for-byte.
fn render_bgp_scalar_section(out: &mut String, ctx: &DeviceContext) {
    let Some(bgp) = ctx.bgp.as_ref() else { return; };
    out.push_str(&format!("set protocols bgp local-as \"{}\"\n", bgp.local_as));
    out.push_str("set protocols bgp ebgp-requires-policy false\n");
    if let Some(lb) = ctx.loopback.as_deref() {
        if let Some((host, _)) = split_inet_text(lb) {
            out.push_str(&format!("set protocols bgp router-id {}\n", host));
        }
    }
    for n in &ctx.bgp_neighbors {
        let remote_as = n.remote_as.map(|a| a.to_string()).unwrap_or_else(|| "?".into());
        out.push_str(&format!(
            "set protocols bgp neighbor {} remote-as \"{}\"\n",
            n.peer_ip, remote_as));
        let desc_prefix = match n.kind {
            BgpNeighborKind::P2P => "P2P",
            BgpNeighborKind::B2B => "B2B",
        };
        out.push_str(&format!(
            "set protocols bgp neighbor {} description \"{}-{}\"\n",
            n.peer_ip, desc_prefix, escape_picos(&n.peer_label)));
    }
    out.push_str("set protocols bgp ipv4-unicast redistribute connected\n");
    out.push_str(&format!(
        "set protocols bgp ipv4-unicast multipath ebgp maximum-paths {}\n",
        bgp.max_paths));
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
            bgp: None,
            bgp_neighbors: vec![],
        }
    }

    fn fixture_with_addrs(
        hostname: &str,
        loopback: Option<&str>,
        mgmt: Option<&str>,
    ) -> DeviceContext {
        DeviceContext {
            device_id: Uuid::nil(),
            hostname: hostname.into(),
            loopback: loopback.map(Into::into),
            management_ip: mgmt.map(Into::into),
            vlans: vec![],
            bgp: None,
            bgp_neighbors: vec![],
        }
    }

    fn fixture_with_bgp(
        hostname: &str,
        loopback: Option<&str>,
        bgp: Option<BgpContext>,
    ) -> DeviceContext {
        fixture_with_bgp_and_neighbors(hostname, loopback, bgp, vec![])
    }

    fn fixture_with_bgp_and_neighbors(
        hostname: &str,
        loopback: Option<&str>,
        bgp: Option<BgpContext>,
        bgp_neighbors: Vec<BgpNeighborLine>,
    ) -> DeviceContext {
        DeviceContext {
            device_id: Uuid::nil(),
            hostname: hostname.into(),
            loopback: loopback.map(Into::into),
            management_ip: None,
            vlans: vec![],
            bgp,
            bgp_neighbors,
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
    fn picos_emits_loopback_block_when_present() {
        let ctx = fixture_with_addrs("CORE02", Some("10.255.91.2/32"), None);
        let out = PicosRenderer::render(&ctx);
        assert!(out.contains("set l3-interface loopback lo0 address 10.255.91.2 prefix-length 32"),
            "loopback line missing:\n{out}");
    }

    #[test]
    fn picos_omits_loopback_block_when_absent() {
        let out = PicosRenderer::render(&fixture_with_addrs("CORE02", None, None));
        assert!(!out.contains("set l3-interface loopback"),
            "no loopback IP in context → no loopback line:\n{out}");
    }

    #[test]
    fn picos_emits_management_interface_when_present() {
        let ctx = fixture_with_addrs("CORE02", None, Some("10.11.152.2/24"));
        let out = PicosRenderer::render(&ctx);
        assert!(out.contains("set l3-interface vlan-interface vlan-152 address 10.11.152.2 prefix-length 24"),
            "mgmt SVI line missing:\n{out}");
    }

    #[test]
    fn picos_omits_management_interface_when_absent() {
        let out = PicosRenderer::render(&fixture_with_addrs("CORE02", None, None));
        assert!(!out.contains("vlan-interface vlan-152"),
            "no mgmt IP in context → no VLAN-152 SVI line:\n{out}");
    }

    #[test]
    fn picos_skips_loopback_if_inet_text_malformed() {
        // Bare host with no "/prefix" — can't render a valid PicOS line,
        // so the section should be skipped rather than emitting garbage.
        let ctx = fixture_with_addrs("CORE02", Some("10.255.91.2"), None);
        let out = PicosRenderer::render(&ctx);
        assert!(!out.contains("set l3-interface loopback"),
            "malformed loopback text should skip the section:\n{out}");
    }

    #[test]
    fn split_inet_text_parses_standard_forms() {
        assert_eq!(split_inet_text("10.255.91.2/32"), Some(("10.255.91.2", 32)));
        assert_eq!(split_inet_text("10.11.152.2/24"), Some(("10.11.152.2", 24)));
        assert_eq!(split_inet_text("192.168.0.1/8"),  Some(("192.168.0.1", 8)));
    }

    #[test]
    fn split_inet_text_rejects_malformed() {
        assert_eq!(split_inet_text("10.255.91.2"),      None); // no slash
        assert_eq!(split_inet_text("10.255.91.2/abc"),  None); // non-numeric prefix
        assert_eq!(split_inet_text("/32"),              None); // empty host
        assert_eq!(split_inet_text(""),                 None);
    }

    #[test]
    fn picos_emits_bgp_scalar_block_with_router_id_when_loopback_present() {
        let ctx = fixture_with_bgp(
            "CORE02",
            Some("10.255.91.2/32"),
            Some(BgpContext { local_as: 65112, max_paths: 4 }),
        );
        let out = PicosRenderer::render(&ctx);
        assert!(out.contains("set protocols bgp local-as \"65112\""),
            "local-as line missing:\n{out}");
        assert!(out.contains("set protocols bgp ebgp-requires-policy false"),
            "ebgp-requires-policy line missing:\n{out}");
        assert!(out.contains("set protocols bgp router-id 10.255.91.2"),
            "router-id line missing (should be bare host, no prefix):\n{out}");
        assert!(out.contains("set protocols bgp ipv4-unicast redistribute connected"),
            "redistribute connected line missing:\n{out}");
        assert!(out.contains("set protocols bgp ipv4-unicast multipath ebgp maximum-paths 4"),
            "multipath line missing:\n{out}");
    }

    #[test]
    fn picos_omits_router_id_when_loopback_absent_but_keeps_rest_of_bgp() {
        let ctx = fixture_with_bgp(
            "CORE02",
            None,
            Some(BgpContext { local_as: 65112, max_paths: 4 }),
        );
        let out = PicosRenderer::render(&ctx);
        assert!(out.contains("set protocols bgp local-as \"65112\""),
            "local-as line should still render without loopback:\n{out}");
        assert!(!out.contains("router-id"),
            "no loopback → skip router-id line:\n{out}");
    }

    #[test]
    fn picos_omits_bgp_section_when_asn_absent() {
        let ctx = fixture_with_bgp("CORE02", Some("10.255.91.2/32"), None);
        let out = PicosRenderer::render(&ctx);
        assert!(!out.contains("protocols bgp"),
            "no ASN → skip entire BGP section:\n{out}");
    }

    #[test]
    fn picos_respects_custom_max_paths() {
        let ctx = fixture_with_bgp(
            "CORE02",
            None,
            Some(BgpContext { local_as: 65112, max_paths: 8 }),
        );
        let out = PicosRenderer::render(&ctx);
        assert!(out.contains("multipath ebgp maximum-paths 8"),
            "max_paths 8 should pass through:\n{out}");
    }

    #[test]
    fn picos_emits_p2p_bgp_neighbor_with_peer_hostname() {
        let ctx = fixture_with_bgp_and_neighbors(
            "MEP-91-CORE02",
            Some("10.255.91.2/32"),
            Some(BgpContext { local_as: 65112, max_paths: 4 }),
            vec![BgpNeighborLine {
                peer_ip: "10.5.17.2".into(),
                remote_as: Some(65121),
                kind: BgpNeighborKind::P2P,
                peer_label: "MEP-92-CORE01".into(),
            }],
        );
        let out = PicosRenderer::render(&ctx);
        assert!(out.contains(r#"set protocols bgp neighbor 10.5.17.2 remote-as "65121""#),
            "P2P remote-as missing:\n{out}");
        assert!(out.contains(r#"set protocols bgp neighbor 10.5.17.2 description "P2P-MEP-92-CORE01""#),
            "P2P description missing:\n{out}");
    }

    #[test]
    fn picos_emits_b2b_bgp_neighbor_with_building_code_and_question_mark_when_asn_missing() {
        let ctx = fixture_with_bgp_and_neighbors(
            "MEP-91-CORE02",
            None,
            Some(BgpContext { local_as: 65112, max_paths: 4 }),
            vec![BgpNeighborLine {
                peer_ip: "10.11.262.1".into(),
                remote_as: None,
                kind: BgpNeighborKind::B2B,
                peer_label: "MEP-92".into(),
            }],
        );
        let out = PicosRenderer::render(&ctx);
        assert!(out.contains(r#"set protocols bgp neighbor 10.11.262.1 remote-as "?""#),
            "B2B missing-ASN should emit remote-as \"?\":\n{out}");
        assert!(out.contains(r#"set protocols bgp neighbor 10.11.262.1 description "B2B-MEP-92""#),
            "B2B description should use building code:\n{out}");
    }

    #[test]
    fn picos_neighbor_block_lands_between_router_id_and_redistribute() {
        // Ordering matters for byte-for-byte parity: scalar lines first,
        // then neighbor pairs, then the two ipv4-unicast lines.
        let ctx = fixture_with_bgp_and_neighbors(
            "CORE02",
            Some("10.255.91.2/32"),
            Some(BgpContext { local_as: 65112, max_paths: 4 }),
            vec![BgpNeighborLine {
                peer_ip: "10.5.17.2".into(),
                remote_as: Some(65121),
                kind: BgpNeighborKind::P2P,
                peer_label: "CORE01".into(),
            }],
        );
        let out = PicosRenderer::render(&ctx);
        let router_id = out.find("router-id").expect("router-id present");
        let neighbor = out.find("neighbor 10.5.17.2 remote-as").expect("neighbor present");
        let redistribute = out.find("redistribute connected").expect("redistribute present");
        assert!(router_id < neighbor && neighbor < redistribute,
            "expected order: router-id < neighbor < redistribute in:\n{out}");
    }

    #[test]
    fn picos_neighbor_description_escapes_special_chars_in_peer_label() {
        let ctx = fixture_with_bgp_and_neighbors(
            "CORE02",
            None,
            Some(BgpContext { local_as: 65112, max_paths: 4 }),
            vec![BgpNeighborLine {
                peer_ip: "10.0.0.1".into(),
                remote_as: Some(65200),
                kind: BgpNeighborKind::P2P,
                peer_label: r#"weird"name"#.into(),
            }],
        );
        let out = PicosRenderer::render(&ctx);
        assert!(out.contains(r#"description "P2P-weird\"name""#),
            "peer_label quotes must be escaped:\n{out}");
    }

    #[test]
    fn picos_emits_no_neighbor_lines_when_vec_empty() {
        let ctx = fixture_with_bgp(
            "CORE02",
            Some("10.255.91.2/32"),
            Some(BgpContext { local_as: 65112, max_paths: 4 }),
        );
        let out = PicosRenderer::render(&ctx);
        assert!(!out.contains("bgp neighbor"),
            "no neighbor rows → no neighbor lines:\n{out}");
        // But the rest of the BGP section must still render.
        assert!(out.contains("local-as \"65112\""));
        assert!(out.contains("redistribute connected"));
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
