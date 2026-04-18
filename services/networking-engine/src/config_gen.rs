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
use crate::naming::{self, DeviceNamingContext};

// ─── Public surface ──────────────────────────────────────────────────────

/// Lightweight row for the render-history list endpoint — no `body`,
/// so a tenant with 10k renders doesn't need to ship MBs over the
/// wire to populate a list panel. Callers fetch the full body by id
/// via `RenderedConfigRecord` when they actually need to diff.
#[derive(Debug, Clone, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct RenderedConfigSummary {
    pub id: Uuid,
    pub device_id: Uuid,
    pub flavor_code: String,
    pub body_sha256: String,
    pub line_count: i32,
    pub render_duration_ms: Option<i32>,
    pub previous_render_id: Option<Uuid>,
    pub rendered_at: DateTime<Utc>,
    pub rendered_by: Option<i32>,
}

/// Full record including body — for the diff / view-one-render flow.
#[derive(Debug, Clone, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct RenderedConfigRecord {
    pub id: Uuid,
    pub device_id: Uuid,
    pub flavor_code: String,
    pub body: String,
    pub body_sha256: String,
    pub line_count: i32,
    pub render_duration_ms: Option<i32>,
    pub previous_render_id: Option<Uuid>,
    pub rendered_at: DateTime<Utc>,
    pub rendered_by: Option<i32>,
}

#[derive(Debug, Clone, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct RenderedConfig {
    pub device_id: Uuid,
    pub flavor_code: String,
    pub body: String,
    pub body_sha256: String,
    pub line_count: i32,
    pub rendered_at: DateTime<Utc>,
    /// Row id in `net.rendered_config` once persisted; `None` for
    /// in-memory / dry-run renders that skip the write.
    #[serde(skip_serializing_if = "Option::is_none")]
    pub id: Option<Uuid>,
    /// Previous render's id for this (org, device, flavor) — points at
    /// the immediately-prior row so "what changed since last render"
    /// is a two-row join rather than a full-text diff every time.
    #[serde(skip_serializing_if = "Option::is_none")]
    pub previous_render_id: Option<Uuid>,
    /// Wall-clock milliseconds from start of `render_device*` to
    /// completion (fetch + render combined). `None` on the in-memory
    /// path where nobody's watching the clock.
    #[serde(skip_serializing_if = "Option::is_none")]
    pub render_duration_ms: Option<i32>,
}

/// Resolve the device's flavor and dispatch to its renderer. Returns a
/// `RenderedConfig` in memory **without** writing to
/// `net.rendered_config` — use this for dry-run / preview flows where
/// the caller doesn't want to pollute render history. `id` /
/// `previous_render_id` / `render_duration_ms` on the returned struct
/// stay `None`. For the production "render + persist + chain" path
/// call `render_device_persisted`.
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

/// Render **and** persist to `net.rendered_config`, chaining to the
/// previous render for this (org, device, flavor). Returns the
/// persisted row's id + previous-id + measured duration populated on
/// the returned struct. Use this from the REST `POST` handler and
/// other places where render history is part of the audit trail.
pub async fn render_device_persisted(
    pool: &PgPool,
    org_id: Uuid,
    device_id: Uuid,
    rendered_by: Option<i32>,
) -> Result<RenderedConfig, EngineError> {
    let started = std::time::Instant::now();
    let mut rc = render_device(pool, org_id, device_id).await?;
    rc.render_duration_ms = Some(started.elapsed().as_millis().min(i32::MAX as u128) as i32);
    persist_render(pool, org_id, rendered_by, &mut rc).await?;
    Ok(rc)
}

/// Insert a row into `net.rendered_config` and fill
/// `rc.id` + `rc.previous_render_id` from the result. Looks up the
/// previous render for this (org, device, flavor) via the
/// `ix_rendered_config_device` index so the chain stays cheap.
async fn persist_render(
    pool: &PgPool,
    org_id: Uuid,
    rendered_by: Option<i32>,
    rc: &mut RenderedConfig,
) -> Result<(), EngineError> {
    let prev: Option<(Uuid,)> = sqlx::query_as(
        "SELECT id FROM net.rendered_config
          WHERE organization_id = $1 AND device_id = $2 AND flavor_code = $3
            AND deleted_at IS NULL
          ORDER BY rendered_at DESC
          LIMIT 1")
        .bind(org_id)
        .bind(rc.device_id)
        .bind(&rc.flavor_code)
        .fetch_optional(pool)
        .await?;
    let previous_render_id = prev.map(|(id,)| id);

    let row: (Uuid,) = sqlx::query_as(
        "INSERT INTO net.rendered_config
            (organization_id, device_id, flavor_code, body, body_sha256,
             line_count, render_duration_ms, previous_render_id, rendered_by)
          VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9)
          RETURNING id")
        .bind(org_id)
        .bind(rc.device_id)
        .bind(&rc.flavor_code)
        .bind(&rc.body)
        .bind(&rc.body_sha256)
        .bind(rc.line_count)
        .bind(rc.render_duration_ms)
        .bind(previous_render_id)
        .bind(rendered_by)
        .fetch_one(pool)
        .await?;

    rc.id = Some(row.0);
    rc.previous_render_id = previous_render_id;
    Ok(())
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
        id: None,
        previous_render_id: None,
        render_duration_ms: None,
    }
}

/// Lightweight default + safety-cap for the list endpoint. Callers
/// can pass their own `limit` but we clamp to `RENDER_LIST_MAX` to
/// stop a single request from pulling a tenant's entire history.
const RENDER_LIST_DEFAULT: i64 = 50;
const RENDER_LIST_MAX: i64 = 500;

pub fn clamp_render_list_limit(requested: Option<i64>) -> i64 {
    let n = requested.unwrap_or(RENDER_LIST_DEFAULT);
    n.clamp(1, RENDER_LIST_MAX)
}

/// Recent renders for a device, most recent first. Limit clamps to
/// `[1, RENDER_LIST_MAX]` via `clamp_render_list_limit` so caller
/// typos can't accidentally fetch the whole table.
pub async fn list_renders(
    pool: &PgPool,
    org_id: Uuid,
    device_id: Uuid,
    limit: i64,
) -> Result<Vec<RenderedConfigSummary>, EngineError> {
    let rows: Vec<(Uuid, Uuid, String, String, i32, Option<i32>, Option<Uuid>, DateTime<Utc>, Option<i32>)> =
        sqlx::query_as(
            "SELECT id, device_id, flavor_code, body_sha256, line_count,
                    render_duration_ms, previous_render_id, rendered_at, rendered_by
               FROM net.rendered_config
              WHERE organization_id = $1
                AND device_id       = $2
                AND deleted_at      IS NULL
              ORDER BY rendered_at DESC
              LIMIT $3")
            .bind(org_id)
            .bind(device_id)
            .bind(limit)
            .fetch_all(pool)
            .await?;
    Ok(rows.into_iter().map(|(id, dev, flavor, sha, lines, dur, prev, at, by)| RenderedConfigSummary {
        id, device_id: dev, flavor_code: flavor, body_sha256: sha, line_count: lines,
        render_duration_ms: dur, previous_render_id: prev, rendered_at: at, rendered_by: by,
    }).collect())
}

/// Fetch one render by id, scoped by tenant so cross-tenant reads
/// return `container_not_found` rather than leaking.
pub async fn get_render(
    pool: &PgPool,
    org_id: Uuid,
    render_id: Uuid,
) -> Result<RenderedConfigRecord, EngineError> {
    let row: Option<(Uuid, Uuid, String, String, String, i32, Option<i32>, Option<Uuid>, DateTime<Utc>, Option<i32>)> =
        sqlx::query_as(
            "SELECT id, device_id, flavor_code, body, body_sha256, line_count,
                    render_duration_ms, previous_render_id, rendered_at, rendered_by
               FROM net.rendered_config
              WHERE organization_id = $1
                AND id              = $2
                AND deleted_at      IS NULL")
            .bind(org_id)
            .bind(render_id)
            .fetch_optional(pool)
            .await?;
    row.map(|(id, dev, flavor, body, sha, lines, dur, prev, at, by)| RenderedConfigRecord {
        id, device_id: dev, flavor_code: flavor, body, body_sha256: sha, line_count: lines,
        render_duration_ms: dur, previous_render_id: prev, rendered_at: at, rendered_by: by,
    })
    .ok_or_else(|| EngineError::container_not_found("rendered_config", render_id))
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
    /// MSTP bridge-priority for this device. `net.mstp_priority_allocation`
    /// is unique per (org, device) so this is one row or none. Standard
    /// values are multiples of 4096 (0, 4096, 8192, 12288, 16384) — the
    /// renderer emits whatever is stored without re-validating.
    pub mstp_priority: Option<i32>,
    pub mlag: Option<MlagContext>,
    /// L3 VLAN interfaces (SVIs) terminated on this device. Each entry
    /// is one `net.ip_address` row whose subnet is linked to a VLAN.
    /// LOOPBACK-coded subnets are filtered out at fetch time because
    /// `render_loopback_section` already handles them.
    pub l3_svis: Vec<L3SviLine>,
    /// Per-port description lines. Sourced from `net.link_endpoint`
    /// rows where this device is the endpoint and `interface_name` is
    /// populated — that's where the link-naming service writes the
    /// auto-generated "P2P-peer" / "B2B-building" description when a
    /// link is wired. Unused ports (no link) don't appear here; they
    /// don't need a description line.
    pub port_descriptions: Vec<PortDescriptionLine>,
    /// L2 posture per port — port-mode + optional native-vlan-id.
    /// Sourced from `net.port` for ports with `port_mode` set to
    /// `access` or `trunk` (routed ports don't emit `family
    /// ethernet-switching` lines; unset ports are skipped). Separate
    /// from `port_descriptions` today — interleaving the two by
    /// interface name is a byte-parity future concern.
    pub port_l2_rules: Vec<PortL2Line>,
    /// Interface names that should get the QoS classifier +
    /// scheduler-profile binding — every physical port on the device.
    /// Breakout sub-interfaces are filtered out at fetch (they inherit
    /// from their parent). Port_mode is NOT filtered — QoS applies
    /// regardless of L2/L3 role.
    pub port_qos_bindings: Vec<String>,
}

#[derive(Debug, Clone)]
pub struct PortL2Line {
    pub interface_name: String,
    /// Always either `"access"` or `"trunk"`; SQL filters the rest.
    pub port_mode: String,
    pub native_vlan_id: Option<i32>,
}

#[derive(Debug, Clone)]
pub struct PortDescriptionLine {
    pub interface_name: String,
    pub description: String,
}

#[derive(Debug, Clone)]
pub struct L3SviLine {
    pub vlan_id: i32,
    /// Postgres inet text, e.g. `"10.11.101.2/24"`.
    pub address: String,
}

/// MLAG peer-link state derived from:
///  - `net.link` of type `MLAG-Peer` where this device is one endpoint
///    (gives us the peer-link interface name)
///  - `net.mlag_domain` scoped to the device's building (gives us the
///    domain id)
/// Either field can be missing (e.g. link modelled but domain not yet
/// allocated, or vice versa) — the renderer emits whichever lines are
/// resolvable and skips the rest.
#[derive(Debug, Clone)]
pub struct MlagContext {
    pub domain_id: Option<i32>,
    pub peer_link_interface: Option<String>,
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
    // Pull everything needed to *derive* the hostname from the
    // device_role naming_template (migration 093). Falls back to the
    // stored hostname when the template is empty or unresolvable so
    // operators can always force-override by clearing the template.
    type DeviceRow = (
        String,          // stored hostname
        Option<String>,  // device_code (used as {instance})
        Option<String>,  // management_ip text
        Option<String>,  // role_code
        Option<String>,  // naming_template
        Option<String>,  // region_code
        Option<String>,  // site_code
        Option<String>,  // building_code
        Option<String>,  // rack_code
    );
    let dev: Option<DeviceRow> = sqlx::query_as(
        r#"SELECT d.hostname,
                  d.device_code,
                  d.management_ip::text                   AS mgmt,
                  dr.role_code,
                  NULLIF(dr.naming_template, '')          AS naming_template,
                  r.region_code,
                  s.site_code,
                  b.building_code,
                  rk.rack_code
             FROM net.device d
             LEFT JOIN net.device_role dr ON dr.id = d.device_role_id AND dr.deleted_at IS NULL
             LEFT JOIN net.building    b  ON b.id  = d.building_id    AND b.deleted_at IS NULL
             LEFT JOIN net.site        s  ON s.id  = b.site_id        AND s.deleted_at IS NULL
             LEFT JOIN net.region      r  ON r.id  = s.region_id      AND r.deleted_at IS NULL
             LEFT JOIN net.rack        rk ON rk.id = d.rack_id        AND rk.deleted_at IS NULL
            WHERE d.id = $1 AND d.organization_id = $2 AND d.deleted_at IS NULL"#)
        .bind(device_id)
        .bind(org_id)
        .fetch_optional(pool)
        .await?;
    let (stored_hostname, device_code, mgmt, role_code, naming_template,
         region_code, site_code, building_code, rack_code)
        = dev.ok_or_else(|| EngineError::container_not_found("device", device_id))?;
    let hostname = resolve_device_hostname(
        &stored_hostname,
        naming_template.as_deref(),
        role_code.as_deref(),
        region_code.as_deref(),
        site_code.as_deref(),
        building_code.as_deref(),
        rack_code.as_deref(),
        device_code.as_deref(),
    );

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

    // MSTP priority — unique per (org, device) so one row or none.
    let mstp: Option<(i32,)> = sqlx::query_as(
        "SELECT priority
           FROM net.mstp_priority_allocation
          WHERE organization_id = $1 AND device_id = $2 AND deleted_at IS NULL
          LIMIT 1")
        .bind(org_id)
        .bind(device_id)
        .fetch_optional(pool)
        .await?;

    // MLAG: if the device terminates an MLAG-Peer link, we want:
    //   - the local endpoint's interface_name (peer-link port)
    //   - the mlag_domain allocated to the device's building
    // Either half may be missing; both are Option.
    let mlag_row: Option<(Option<i32>, Option<String>)> = sqlx::query_as(
        r#"SELECT mg.domain_id, le_self.interface_name
             FROM net.link l
             JOIN net.link_type lt ON lt.id = l.link_type_id
             JOIN net.link_endpoint le_self
               ON le_self.link_id   = l.id
              AND le_self.device_id = $2
              AND le_self.deleted_at IS NULL
             JOIN net.device d ON d.id = le_self.device_id AND d.deleted_at IS NULL
             LEFT JOIN net.mlag_domain mg
               ON mg.organization_id = $1
              AND mg.scope_level     = 'Building'
              AND mg.scope_entity_id = d.building_id
              AND mg.deleted_at IS NULL
            WHERE l.organization_id = $1
              AND l.deleted_at      IS NULL
              AND lt.type_code      = 'MLAG-Peer'
            LIMIT 1"#)
        .bind(org_id)
        .bind(device_id)
        .fetch_optional(pool)
        .await?;

    // Port QoS bindings: classifier + scheduler-profile apply to
    // every physical port regardless of L2/L3 role. Breakout children
    // inherit from their parent so we exclude them via
    // breakout_parent_id IS NULL — this also matches the legacy
    // ConfigBuilderService enumeration which only touched parent
    // interface numbers, not .1..4 sub-interfaces.
    let port_qos_rows: Vec<(String,)> = sqlx::query_as(
        r#"SELECT interface_name
             FROM net.port
            WHERE organization_id   = $1
              AND device_id         = $2
              AND deleted_at        IS NULL
              AND breakout_parent_id IS NULL
            ORDER BY interface_name"#)
        .bind(org_id)
        .bind(device_id)
        .fetch_all(pool)
        .await?;

    // Port L2 rules: port-mode + native-vlan for every port set to
    // access/trunk. Routed ports are intentionally excluded — they
    // don't emit family ethernet-switching lines; their L3 config
    // lands via SVIs / BGP / loopback sections.
    let port_l2_rows: Vec<(String, String, Option<i32>)> = sqlx::query_as(
        r#"SELECT interface_name, port_mode, native_vlan_id
             FROM net.port
            WHERE organization_id = $1
              AND device_id       = $2
              AND deleted_at      IS NULL
              AND port_mode       IN ('access','trunk')
            ORDER BY interface_name"#)
        .bind(org_id)
        .bind(device_id)
        .fetch_all(pool)
        .await?;

    // Port descriptions: one row per link endpoint this device
    // terminates that names an interface. Description comes from the
    // link-naming service (see net.link_type.naming_template). Rows
    // with empty descriptions are filtered in Rust after fetch so the
    // catalog decision "should this port have a description?" stays
    // in one place rather than duplicated in SQL.
    let port_desc_rows: Vec<(String, Option<String>)> = sqlx::query_as(
        r#"SELECT le.interface_name, le.description
             FROM net.link_endpoint le
             JOIN net.link l ON l.id = le.link_id AND l.deleted_at IS NULL
            WHERE le.organization_id = $1
              AND le.device_id       = $2
              AND le.deleted_at      IS NULL
              AND le.interface_name  IS NOT NULL
              AND le.interface_name <> ''
            ORDER BY le.interface_name"#)
        .bind(org_id)
        .bind(device_id)
        .fetch_all(pool)
        .await?;

    // L3 SVI lines: every ip_address assigned to this device whose
    // subnet is wired to a VLAN, minus the loopback (rendered
    // separately). The management IP currently emits via its own
    // section sourced from net.device.management_ip; if the same
    // address also lives in net.ip_address for the mgmt VLAN, PicOS
    // accepts the duplicate (last-write-wins on the identical line).
    // Once mgmt migrates fully to net.ip_address, the dedicated
    // render_management_interface_section can be dropped.
    let svi_rows: Vec<(i32, String)> = sqlx::query_as(
        r#"SELECT v.vlan_id, ip.address::text
             FROM net.ip_address ip
             JOIN net.subnet s ON s.id = ip.subnet_id AND s.deleted_at IS NULL
             JOIN net.vlan   v ON v.id = s.vlan_id     AND v.deleted_at IS NULL
            WHERE ip.organization_id   = $1
              AND ip.assigned_to_id    = $2
              AND ip.assigned_to_type  = 'Device'
              AND ip.deleted_at        IS NULL
              AND s.subnet_code NOT ILIKE 'LOOPBACK%'
            ORDER BY v.vlan_id, ip.address"#)
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
        mstp_priority: mstp.map(|(p,)| p),
        mlag: mlag_row.map(|(dom, iface)| MlagContext {
            domain_id: dom,
            peer_link_interface: iface,
        }),
        l3_svis: svi_rows.into_iter()
            .map(|(vid, addr)| L3SviLine { vlan_id: vid, address: addr })
            .collect(),
        port_descriptions: port_desc_rows.into_iter()
            .filter_map(|(iface, desc)| {
                let desc = desc.unwrap_or_default();
                if desc.is_empty() { return None; }
                Some(PortDescriptionLine { interface_name: iface, description: desc })
            })
            .collect(),
        port_l2_rules: port_l2_rows.into_iter()
            .map(|(iface, mode, native)| PortL2Line {
                interface_name: iface, port_mode: mode, native_vlan_id: native,
            })
            .collect(),
        port_qos_bindings: port_qos_rows.into_iter().map(|(n,)| n).collect(),
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
        render_ip_routing_section(&mut out, ctx);
        render_qos_preset_section(&mut out, ctx);
        render_port_qos_bindings_section(&mut out, ctx);
        render_voice_vlan_preset_section(&mut out, ctx);
        render_loopback_section(&mut out, ctx);
        render_management_interface_section(&mut out, ctx);
        render_vlans_section(&mut out, ctx);
        render_l3_svi_section(&mut out, ctx);
        render_bgp_scalar_section(&mut out, ctx);
        render_mstp_section(&mut out, ctx);
        render_mlag_section(&mut out, ctx);
        render_ports_section(&mut out, ctx);
        render_lldp_section(&mut out, ctx);
        // TODO(follow-on): render_vrrp (needs net.vrrp_*),
        // render_dhcp_relay (needs DHCP-role server discovery),
        // render_static_routes, render_qos_preset, render_voice_vlan.
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

/// IP routing enable — emitted unconditionally today. Every production
/// PicOS switch in the Immunocore footprint needs it on. When a tenant
/// ever wants to ship pure-L2 switches we'll gate this on a tenant
/// toggle; until then the slice stays universal to keep parity with
/// the legacy `ConfigBuilderService` step 2.
fn render_ip_routing_section(out: &mut String, _ctx: &DeviceContext) {
    out.push_str("set ip routing enable true\n");
    out.push('\n');
}

/// QoS / Class-of-Service preset — the fixed customer-wide DSCP
/// mapping + scheduler topology Immunocore runs on every PicOS
/// switch today. Forwarding classes, schedulers, classifier, and
/// scheduler-profile are all defined here. Per-interface bindings
/// (classifier + scheduler-profile per port) are a separate slice
/// because they're device-data-driven (one pair per `net.port` row)
/// rather than fixed. When a tenant ever ships a different QoS
/// policy we'll lift this into a per-tenant config table; for now
/// the block is a static const to match legacy byte-for-byte.
fn render_qos_preset_section(out: &mut String, _ctx: &DeviceContext) {
    for line in QOS_PRESET_LINES {
        out.push_str(line);
        out.push('\n');
    }
    out.push('\n');
}

const QOS_PRESET_LINES: &[&str] = &[
    // Forwarding classes — 1-to-1 with the customer's application
    // taxonomy (voice > network-control > video > realtime >
    // signaling > transactional > bulk > best-effort).
    "set class-of-service forwarding-class fc-best-effort",
    "set class-of-service forwarding-class fc-bulk local-priority 1",
    "set class-of-service forwarding-class fc-network-control local-priority 7",
    "set class-of-service forwarding-class fc-realtime local-priority 4",
    "set class-of-service forwarding-class fc-signaling local-priority 3",
    "set class-of-service forwarding-class fc-transactional local-priority 2",
    "set class-of-service forwarding-class fc-video local-priority 5",
    "set class-of-service forwarding-class fc-voice-ef local-priority 6",
    // Schedulers — Strict-Priority for network-control and voice;
    // Weighted-Fair-Queueing for the rest, weight tuned to match
    // the bandwidth split the customer's ISP contracts allow.
    "set class-of-service scheduler sched-network-control mode \"SP\"",
    "set class-of-service scheduler sched-voice mode \"SP\"",
    "set class-of-service scheduler sched-video mode \"WFQ\"",
    "set class-of-service scheduler sched-video weight 6",
    "set class-of-service scheduler sched-realtime mode \"WFQ\"",
    "set class-of-service scheduler sched-realtime weight 4",
    "set class-of-service scheduler sched-signaling mode \"WFQ\"",
    "set class-of-service scheduler sched-signaling weight 2",
    "set class-of-service scheduler sched-transactional mode \"WFQ\"",
    "set class-of-service scheduler sched-transactional weight 6",
    "set class-of-service scheduler sched-bulk mode \"WFQ\"",
    "set class-of-service scheduler sched-bulk weight 2",
    "set class-of-service scheduler sched-best-effort mode \"WFQ\"",
    "set class-of-service scheduler sched-best-effort weight 10",
    // Classifier — DSCP code-point → forwarding-class mapping.
    "set class-of-service classifier qos-dscp-classifier trust-mode \"dscp\"",
    "set class-of-service classifier qos-dscp-classifier forwarding-class fc-network-control code-point 48",
    "set class-of-service classifier qos-dscp-classifier forwarding-class fc-network-control code-point 56",
    "set class-of-service classifier qos-dscp-classifier forwarding-class fc-voice-ef code-point 44",
    "set class-of-service classifier qos-dscp-classifier forwarding-class fc-voice-ef code-point 46",
    "set class-of-service classifier qos-dscp-classifier forwarding-class fc-video code-point 34",
    "set class-of-service classifier qos-dscp-classifier forwarding-class fc-video code-point 36",
    "set class-of-service classifier qos-dscp-classifier forwarding-class fc-video code-point 38",
    "set class-of-service classifier qos-dscp-classifier forwarding-class fc-video code-point 40",
    "set class-of-service classifier qos-dscp-classifier forwarding-class fc-realtime code-point 26",
    "set class-of-service classifier qos-dscp-classifier forwarding-class fc-realtime code-point 28",
    "set class-of-service classifier qos-dscp-classifier forwarding-class fc-realtime code-point 30",
    "set class-of-service classifier qos-dscp-classifier forwarding-class fc-realtime code-point 32",
    "set class-of-service classifier qos-dscp-classifier forwarding-class fc-signaling code-point 18",
    "set class-of-service classifier qos-dscp-classifier forwarding-class fc-signaling code-point 20",
    "set class-of-service classifier qos-dscp-classifier forwarding-class fc-signaling code-point 22",
    "set class-of-service classifier qos-dscp-classifier forwarding-class fc-signaling code-point 24",
    "set class-of-service classifier qos-dscp-classifier forwarding-class fc-transactional code-point 10",
    "set class-of-service classifier qos-dscp-classifier forwarding-class fc-transactional code-point 12",
    "set class-of-service classifier qos-dscp-classifier forwarding-class fc-transactional code-point 14",
    "set class-of-service classifier qos-dscp-classifier forwarding-class fc-transactional code-point 16",
    "set class-of-service classifier qos-dscp-classifier forwarding-class fc-bulk code-point 1",
    "set class-of-service classifier qos-dscp-classifier forwarding-class fc-bulk code-point 8",
    "set class-of-service classifier qos-dscp-classifier forwarding-class fc-best-effort code-point 0",
    // Scheduler-profile — ties each forwarding-class to its scheduler.
    "set class-of-service scheduler-profile qos-flex-profile forwarding-class fc-network-control scheduler \"sched-network-control\"",
    "set class-of-service scheduler-profile qos-flex-profile forwarding-class fc-voice-ef scheduler \"sched-voice\"",
    "set class-of-service scheduler-profile qos-flex-profile forwarding-class fc-video scheduler \"sched-video\"",
    "set class-of-service scheduler-profile qos-flex-profile forwarding-class fc-realtime scheduler \"sched-realtime\"",
    "set class-of-service scheduler-profile qos-flex-profile forwarding-class fc-signaling scheduler \"sched-signaling\"",
    "set class-of-service scheduler-profile qos-flex-profile forwarding-class fc-transactional scheduler \"sched-transactional\"",
    "set class-of-service scheduler-profile qos-flex-profile forwarding-class fc-bulk scheduler \"sched-bulk\"",
    "set class-of-service scheduler-profile qos-flex-profile forwarding-class fc-best-effort scheduler \"sched-best-effort\"",
];

/// Voice-VLAN preset — 4 fixed lines matching the customer's Avaya
/// deployment (OUI `c8:1f:ea:XX:XX:XX`, local-priority 6, DSCP 46).
/// Sits at legacy step 4 so it lands after the QoS block (whose
/// forwarding classes it relies on) and before the VLAN catalog.
/// When a different tenant brings a non-Avaya voice vendor we'll
/// move this to a per-tenant settings table; today it matches the
/// only production deployment, so const is honest.
fn render_voice_vlan_preset_section(out: &mut String, _ctx: &DeviceContext) {
    for line in VOICE_VLAN_PRESET_LINES {
        out.push_str(line);
        out.push('\n');
    }
    out.push('\n');
}

const VOICE_VLAN_PRESET_LINES: &[&str] = &[
    "set vlans voice-vlan mac-address c8:1f:ea:66:72:b6 mask ff:ff:ff:00:00:00",
    "set vlans voice-vlan mac-address c8:1f:ea:66:72:b6 description \"Avaya\"",
    "set vlans voice-vlan local-priority 6",
    "set vlans voice-vlan dscp 46",
];

/// Per-port QoS bindings — one classifier + one scheduler-profile
/// line per physical port on the device. Positioned right after the
/// fixed QoS preset so the classifier / scheduler-profile names
/// referenced here are already defined earlier in the file.
fn render_port_qos_bindings_section(out: &mut String, ctx: &DeviceContext) {
    if ctx.port_qos_bindings.is_empty() { return; }
    for iface in &ctx.port_qos_bindings {
        out.push_str(&format!(
            "set class-of-service interface {} classifier \"qos-dscp-classifier\"\n",
            iface));
        out.push_str(&format!(
            "set class-of-service interface {} scheduler-profile \"qos-flex-profile\"\n",
            iface));
    }
    out.push('\n');
}

/// LLDP enable — same universal-today, toggle-tomorrow stance as
/// `render_ip_routing_section`. Legacy emits this as the last
/// configurable section before the management-IP comment, so we
/// match that position in the file.
fn render_lldp_section(out: &mut String, _ctx: &DeviceContext) {
    out.push_str("set protocols lldp enable true\n");
    out.push('\n');
}

/// Resolve the emitted hostname from the device_role `naming_template`
/// plus the hierarchy codes (region / site / building / rack / role)
/// and the device's `device_code` (used as the `{instance}` token).
/// Returns the stored hostname verbatim if the template is missing /
/// unresolvable — that's the operator's escape hatch for devices
/// whose name shouldn't come from a convention.
///
/// `device_code` is parsed for a trailing numeric instance (e.g.
/// `"02"` → `Some(2)`). Non-numeric codes fall through and the
/// `{instance}` token substitutes to empty.
#[allow(clippy::too_many_arguments)]
fn resolve_device_hostname(
    stored: &str,
    naming_template: Option<&str>,
    role_code: Option<&str>,
    region_code: Option<&str>,
    site_code: Option<&str>,
    building_code: Option<&str>,
    rack_code: Option<&str>,
    device_code: Option<&str>,
) -> String {
    let Some(tpl) = naming_template.filter(|t| !t.is_empty()) else {
        return stored.to_string();
    };
    let instance = device_code.and_then(|c| c.trim().parse::<i32>().ok());
    let ctx = DeviceNamingContext {
        region_code:      region_code.map(str::to_string),
        site_code:        site_code.map(str::to_string),
        building_code:    building_code.map(str::to_string),
        rack_code:        rack_code.map(str::to_string),
        role_code:        role_code.map(str::to_string),
        instance,
        instance_padding: 2,
    };
    let resolved = naming::expand_device(tpl, &ctx);
    // If expansion produces nothing useful (e.g. tokens all empty,
    // separators left hanging), fall back to stored. This protects
    // against half-modelled devices producing `"--"` etc.
    if resolved.trim().is_empty() { stored.to_string() } else { resolved }
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
    // VLANs bound to an SVI emit an extra `l3-interface "vlan-N"` line
    // right after their description — this is the PicOS declaration
    // that binds the L2 VLAN to the L3 interface of the same number.
    // Cross the two lists by vlan_id so VLANs without an SVI (pure L2
    // trunks) don't get a dangling binding.
    let svi_vlans: std::collections::HashSet<i32> =
        ctx.l3_svis.iter().map(|s| s.vlan_id).collect();
    for v in &ctx.vlans {
        // PicOS: set vlans vlan-id N description "..."
        // We always emit vlan-id + description; name is PicOS's
        // description field (distinct from the tenant-facing display
        // name which may differ from the switch-level comment).
        out.push_str(&format!(
            "set vlans vlan-id {} description \"{}\"\n",
            v.vlan_id,
            escape_picos(v.description.as_deref().unwrap_or(&v.display_name))));
        if svi_vlans.contains(&v.vlan_id) {
            out.push_str(&format!(
                "set vlans vlan-id {} l3-interface \"vlan-{}\"\n",
                v.vlan_id, v.vlan_id));
        }
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

/// Merged per-interface config section — description + L2 rules
/// emitted adjacently for each port that has either. Matches the
/// legacy `ConfigBuilderService` per-link block layout where all
/// lines for one interface cluster together, which keeps regen-vs-
/// shipped diffs readable.
///
/// PicOS uses the fixed keyword `gigabit-ethernet` regardless of the
/// port's actual speed — the interface name prefix (`xe-` / `ge-` /
/// `et-`) carries the speed semantics. Description text is
/// `escape_picos`'d so link-naming output that happens to contain a
/// quote can't break the config block.
///
/// QoS bindings stay in their own section near the QoS preset — they
/// reference names (`qos-dscp-classifier`, `qos-flex-profile`) that
/// need to be declared earlier in the file, so interleaving them
/// here would break that locality.
fn render_ports_section(out: &mut String, ctx: &DeviceContext) {
    if ctx.port_descriptions.is_empty() && ctx.port_l2_rules.is_empty() { return; }

    use std::collections::BTreeMap;
    let mut by_iface: BTreeMap<&str, (Option<&PortDescriptionLine>, Option<&PortL2Line>)> = BTreeMap::new();
    for d in &ctx.port_descriptions {
        by_iface.entry(d.interface_name.as_str()).or_default().0 = Some(d);
    }
    for r in &ctx.port_l2_rules {
        by_iface.entry(r.interface_name.as_str()).or_default().1 = Some(r);
    }

    for (iface, (desc, l2)) in by_iface {
        if let Some(d) = desc {
            out.push_str(&format!(
                "set interface gigabit-ethernet {} description \"{}\"\n",
                iface, escape_picos(&d.description)));
        }
        if let Some(r) = l2 {
            if let Some(vid) = r.native_vlan_id {
                out.push_str(&format!(
                    "set interface gigabit-ethernet {} family ethernet-switching native-vlan-id {}\n",
                    iface, vid));
            }
            out.push_str(&format!(
                "set interface gigabit-ethernet {} family ethernet-switching port-mode \"{}\"\n",
                iface, r.port_mode));
        }
    }
    out.push('\n');
}

fn render_l3_svi_section(out: &mut String, ctx: &DeviceContext) {
    if ctx.l3_svis.is_empty() { return; }
    for svi in &ctx.l3_svis {
        let Some((host, prefix)) = split_inet_text(&svi.address) else { continue; };
        out.push_str(&format!(
            "set l3-interface vlan-interface vlan-{} address {} prefix-length {}\n",
            svi.vlan_id, host, prefix));
    }
    out.push('\n');
}

fn render_mstp_section(out: &mut String, ctx: &DeviceContext) {
    let Some(prio) = ctx.mstp_priority else { return; };
    out.push_str(&format!(
        "set protocols spanning-tree mstp bridge-priority {}\n", prio));
    out.push('\n');
}

fn render_mlag_section(out: &mut String, ctx: &DeviceContext) {
    let Some(mlag) = ctx.mlag.as_ref() else { return; };
    // Require at least one of the two fields to render anything. An
    // MLAG-Peer link with neither the domain allocated nor the port
    // named is effectively empty state — skip.
    if mlag.domain_id.is_none() && mlag.peer_link_interface.is_none() {
        return;
    }
    if let Some(dom) = mlag.domain_id {
        out.push_str(&format!("set protocols mlag domain {}\n", dom));
    }
    if let Some(iface) = mlag.peer_link_interface.as_deref() {
        if !iface.is_empty() {
            out.push_str(&format!("set protocols mlag peer-link {}\n", iface));
        }
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
            bgp: None,
            bgp_neighbors: vec![],
            mstp_priority: None,
            mlag: None,
            l3_svis: vec![],
            port_descriptions: vec![],
            port_l2_rules: vec![],
            port_qos_bindings: vec![],
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
            mstp_priority: None,
            mlag: None,
            l3_svis: vec![],
            port_descriptions: vec![],
            port_l2_rules: vec![],
            port_qos_bindings: vec![],
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
            mstp_priority: None,
            mlag: None,
            l3_svis: vec![],
            port_descriptions: vec![],
            port_l2_rules: vec![],
            port_qos_bindings: vec![],
        }
    }

    fn fixture_with_svis(svis: Vec<L3SviLine>) -> DeviceContext {
        DeviceContext {
            device_id: Uuid::nil(),
            hostname: "CORE02".into(),
            loopback: None,
            management_ip: None,
            vlans: vec![],
            bgp: None,
            bgp_neighbors: vec![],
            mstp_priority: None,
            mlag: None,
            l3_svis: svis,
            port_descriptions: vec![],
            port_l2_rules: vec![],
            port_qos_bindings: vec![],
        }
    }

    fn fixture_with_ports(ports: Vec<PortDescriptionLine>) -> DeviceContext {
        DeviceContext {
            device_id: Uuid::nil(),
            hostname: "CORE02".into(),
            loopback: None,
            management_ip: None,
            vlans: vec![],
            bgp: None,
            bgp_neighbors: vec![],
            mstp_priority: None,
            mlag: None,
            l3_svis: vec![],
            port_descriptions: ports,
            port_l2_rules: vec![],
            port_qos_bindings: vec![],
        }
    }

    fn fixture_with_vlans_and_svis(
        vlans: Vec<VlanLine>,
        svis: Vec<L3SviLine>,
    ) -> DeviceContext {
        DeviceContext {
            device_id: Uuid::nil(),
            hostname: "CORE02".into(),
            loopback: None,
            management_ip: None,
            vlans,
            bgp: None,
            bgp_neighbors: vec![],
            mstp_priority: None,
            mlag: None,
            l3_svis: svis,
            port_descriptions: vec![],
            port_l2_rules: vec![],
            port_qos_bindings: vec![],
        }
    }

    fn fixture_with_l2_rules(rules: Vec<PortL2Line>) -> DeviceContext {
        DeviceContext {
            device_id: Uuid::nil(),
            hostname: "CORE02".into(),
            loopback: None,
            management_ip: None,
            vlans: vec![],
            bgp: None,
            bgp_neighbors: vec![],
            mstp_priority: None,
            mlag: None,
            l3_svis: vec![],
            port_descriptions: vec![],
            port_l2_rules: rules,
            port_qos_bindings: vec![],
        }
    }

    fn fixture_with_port_cfg(
        descs: Vec<PortDescriptionLine>,
        l2s: Vec<PortL2Line>,
    ) -> DeviceContext {
        DeviceContext {
            device_id: Uuid::nil(),
            hostname: "CORE02".into(),
            loopback: None,
            management_ip: None,
            vlans: vec![],
            bgp: None,
            bgp_neighbors: vec![],
            mstp_priority: None,
            mlag: None,
            l3_svis: vec![],
            port_descriptions: descs,
            port_l2_rules: l2s,
            port_qos_bindings: vec![],
        }
    }

    fn fixture_with_qos_bindings(ports: Vec<&str>) -> DeviceContext {
        DeviceContext {
            device_id: Uuid::nil(),
            hostname: "CORE02".into(),
            loopback: None,
            management_ip: None,
            vlans: vec![],
            bgp: None,
            bgp_neighbors: vec![],
            mstp_priority: None,
            mlag: None,
            l3_svis: vec![],
            port_descriptions: vec![],
            port_l2_rules: vec![],
            port_qos_bindings: ports.into_iter().map(String::from).collect(),
        }
    }

    fn fixture_with_mstp_mlag(
        mstp_priority: Option<i32>,
        mlag: Option<MlagContext>,
    ) -> DeviceContext {
        DeviceContext {
            device_id: Uuid::nil(),
            hostname: "CORE02".into(),
            loopback: None,
            management_ip: None,
            vlans: vec![],
            bgp: None,
            bgp_neighbors: vec![],
            mstp_priority,
            mlag,
            l3_svis: vec![],
            port_descriptions: vec![],
            port_l2_rules: vec![],
            port_qos_bindings: vec![],
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
        // Tightened past `"set vlans"` to `"set vlans vlan-id"` — the
        // voice-VLAN preset legitimately emits `set vlans voice-vlan`
        // lines regardless of per-device VLAN catalog contents.
        assert!(!out.contains("set vlans vlan-id"),
            "no VLAN catalog lines should appear when the context has no vlans:\n{out}");
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

    // ── Hostname template resolution ─────────────────────────────────

    #[test]
    fn resolve_hostname_expands_tokens_from_hierarchy() {
        let out = resolve_device_hostname(
            "stored-ignored",
            Some("{building_code}-CORE{instance}"),
            Some("Core"),
            Some("MP"),
            Some("MEP"),
            Some("MEP-91"),
            None,
            Some("2"),
        );
        assert_eq!(out, "MEP-91-CORE02",
            "template should expand building + instance tokens");
    }

    #[test]
    fn resolve_hostname_falls_back_to_stored_when_template_missing() {
        let out = resolve_device_hostname(
            "OLD-HOST01",
            None,
            Some("Core"), Some("MP"), Some("MEP"), Some("MEP-91"), None, Some("2"),
        );
        assert_eq!(out, "OLD-HOST01",
            "no template → stored hostname is emitted verbatim");
    }

    #[test]
    fn resolve_hostname_falls_back_when_template_is_empty_string() {
        let out = resolve_device_hostname(
            "STORED-HOST",
            Some(""),
            Some("Core"), None, None, None, None, None,
        );
        assert_eq!(out, "STORED-HOST",
            "empty template string → fall back to stored");
    }

    #[test]
    fn resolve_hostname_leaves_missing_tokens_as_empty_but_keeps_literals() {
        // Template has {site_code} but we pass None — the literal
        // separators ("-") are preserved even if the token is empty.
        let out = resolve_device_hostname(
            "stored-ignored",
            Some("{site_code}-{role_code}{instance}"),
            Some("Core"),
            None, None, None, None,
            Some("1"),
        );
        assert_eq!(out, "-Core01",
            "missing site_code should still emit literal separator");
    }

    #[test]
    fn resolve_hostname_falls_back_when_expansion_yields_whitespace_only() {
        // Defensive: every token missing AND template is pure tokens →
        // expansion is empty. Falling back to stored prevents emitting
        // `set system hostname ""`.
        let out = resolve_device_hostname(
            "FALLBACK",
            Some("{site_code}{building_code}{role_code}"),
            None, None, None, None, None, None,
        );
        assert_eq!(out, "FALLBACK",
            "all tokens empty → expansion empty → fall back to stored");
    }

    #[test]
    fn resolve_hostname_parses_device_code_as_instance_number() {
        // device_code "02" → instance 2 → zero-padded back to "02"
        // via the default padding=2 convention.
        let out = resolve_device_hostname(
            "stored",
            Some("{role_code}{instance}"),
            Some("L1Core"), None, None, None, None,
            Some("2"),
        );
        assert_eq!(out, "L1Core02");
    }

    #[test]
    fn resolve_hostname_non_numeric_device_code_yields_empty_instance() {
        // Non-numeric device_code (e.g. "A") can't be parsed — {instance}
        // substitutes empty, the rest of the template still renders.
        let out = resolve_device_hostname(
            "stored",
            Some("{building_code}-{role_code}{instance}"),
            Some("Core"), None, None, Some("MEP-91"), None,
            Some("A"),
        );
        assert_eq!(out, "MEP-91-Core",
            "unparseable device_code should leave {{instance}} empty");
    }

    #[test]
    fn picos_emits_voice_vlan_preset_lines() {
        let out = PicosRenderer::render(&fixture("CORE02", vec![]));
        assert!(out.contains("set vlans voice-vlan mac-address c8:1f:ea:66:72:b6 mask ff:ff:ff:00:00:00"),
            "voice-vlan MAC/mask line missing:\n{out}");
        assert!(out.contains(r#"set vlans voice-vlan mac-address c8:1f:ea:66:72:b6 description "Avaya""#),
            "voice-vlan Avaya description line missing:\n{out}");
        assert!(out.contains("set vlans voice-vlan local-priority 6"),
            "voice-vlan local-priority line missing:\n{out}");
        assert!(out.contains("set vlans voice-vlan dscp 46"),
            "voice-vlan DSCP line missing:\n{out}");
    }

    #[test]
    fn picos_voice_vlan_preset_emits_expected_line_count() {
        let out = PicosRenderer::render(&fixture("CORE02", vec![]));
        let voice_lines = out.lines()
            .filter(|l| l.starts_with("set vlans voice-vlan"))
            .count();
        assert_eq!(voice_lines, VOICE_VLAN_PRESET_LINES.len(),
            "voice VLAN line count drift:\n{out}");
    }

    #[test]
    fn picos_voice_vlan_preset_lands_after_qos_and_before_vlan_catalog() {
        // Legacy step 4 — after class-of-service definitions (voice
        // DSCP refers to a forwarding-class from QoS), before the
        // per-VLAN `set vlans vlan-id` catalog block.
        let ctx = fixture_with_vlans_and_svis(
            vec![VlanLine { vlan_id: 101, display_name: "IT".into(), description: None }],
            vec![],
        );
        let out = PicosRenderer::render(&ctx);
        let qos_last   = out.find(r#"scheduler "sched-best-effort""#).expect("QoS preset present");
        let voice      = out.find("set vlans voice-vlan mac-address").expect("voice preset present");
        let first_vlan = out.find("set vlans vlan-id 101 description").expect("VLAN catalog present");
        assert!(qos_last < voice,
            "voice VLAN must land AFTER QoS preset:\n{out}");
        assert!(voice < first_vlan,
            "voice VLAN must land BEFORE per-VLAN catalog lines:\n{out}");
    }

    #[test]
    fn picos_emits_qos_bindings_two_lines_per_port() {
        let ctx = fixture_with_qos_bindings(vec!["xe-1/1/1", "xe-1/1/2"]);
        let out = PicosRenderer::render(&ctx);
        for iface in ["xe-1/1/1", "xe-1/1/2"] {
            let classifier = format!(
                r#"set class-of-service interface {} classifier "qos-dscp-classifier""#, iface);
            let sched_prof = format!(
                r#"set class-of-service interface {} scheduler-profile "qos-flex-profile""#, iface);
            assert!(out.contains(&classifier), "classifier binding missing for {iface}:\n{out}");
            assert!(out.contains(&sched_prof), "scheduler-profile binding missing for {iface}:\n{out}");
        }
    }

    #[test]
    fn picos_qos_binding_count_is_exactly_twice_the_port_count() {
        let ctx = fixture_with_qos_bindings(vec!["xe-1/1/1", "xe-1/1/2", "xe-1/1/3"]);
        let out = PicosRenderer::render(&ctx);
        let binding_lines = out.lines()
            .filter(|l| l.starts_with("set class-of-service interface "))
            .count();
        assert_eq!(binding_lines, 6,
            "3 ports × 2 binding lines each = 6, got {binding_lines}:\n{out}");
    }

    #[test]
    fn picos_qos_bindings_emit_in_caller_provided_order() {
        // Fetch query ORDER BYs interface_name; renderer must preserve.
        let ctx = fixture_with_qos_bindings(vec!["ge-1/1/1", "xe-1/1/10", "xe-1/1/20"]);
        let out = PicosRenderer::render(&ctx);
        let p1  = out.find("interface ge-1/1/1 classifier").expect("ge-1/1/1 present");
        let p10 = out.find("interface xe-1/1/10 classifier").expect("xe-1/1/10 present");
        let p20 = out.find("interface xe-1/1/20 classifier").expect("xe-1/1/20 present");
        assert!(p1 < p10 && p10 < p20,
            "QoS binding lines must stay in caller-provided order:\n{out}");
    }

    #[test]
    fn picos_qos_bindings_land_right_after_the_preset_block() {
        // The binding lines reference "qos-dscp-classifier" + "qos-flex-profile"
        // which are declared in the preset — PicOS is forgiving about
        // forward references but diff readability wants them adjacent.
        let ctx = fixture_with_qos_bindings(vec!["xe-1/1/1"]);
        let out = PicosRenderer::render(&ctx);
        let preset_last = out.find(r#"scheduler "sched-best-effort""#).expect("preset present");
        let binding_first = out.find("set class-of-service interface ").expect("binding present");
        assert!(preset_last < binding_first,
            "QoS bindings must come AFTER the preset:\n{out}");
    }

    #[test]
    fn picos_omits_qos_bindings_when_no_ports() {
        let out = PicosRenderer::render(&fixture_with_qos_bindings(vec![]));
        assert!(!out.contains("set class-of-service interface "),
            "no ports → no binding lines:\n{out}");
        // Preset must still render.
        assert!(out.contains("set class-of-service forwarding-class"),
            "preset should still render even with no port bindings:\n{out}");
    }

    #[test]
    fn picos_emits_qos_preset_forwarding_class_lines() {
        let out = PicosRenderer::render(&fixture("CORE02", vec![]));
        // Spot-check one line from each block — full list is covered
        // by the count check below.
        assert!(out.contains("set class-of-service forwarding-class fc-voice-ef local-priority 6"),
            "voice forwarding-class line missing:\n{out}");
        assert!(out.contains(r#"set class-of-service scheduler sched-voice mode "SP""#),
            "voice scheduler line missing:\n{out}");
        assert!(out.contains(r#"set class-of-service classifier qos-dscp-classifier trust-mode "dscp""#),
            "classifier trust-mode line missing:\n{out}");
        assert!(out.contains(r#"set class-of-service scheduler-profile qos-flex-profile forwarding-class fc-voice-ef scheduler "sched-voice""#),
            "scheduler-profile binding line missing:\n{out}");
    }

    #[test]
    fn picos_qos_preset_emits_expected_line_count() {
        // If the preset ever needs to grow/shrink, the change lands
        // here too — prevents silent drift from the customer's
        // documented QoS policy.
        let out = PicosRenderer::render(&fixture("CORE02", vec![]));
        let qos_lines = out.lines()
            .filter(|l| l.starts_with("set class-of-service"))
            .count();
        assert_eq!(qos_lines, QOS_PRESET_LINES.len(),
            "QoS line count drift vs QOS_PRESET_LINES const:\n{out}");
    }

    #[test]
    fn picos_qos_preset_lands_after_ip_routing_and_before_loopback() {
        // Legacy step 3 — right after ip routing, well before any
        // per-device L3 content.
        let ctx = fixture_with_addrs("CORE02", Some("10.255.91.2/32"), None);
        let out = PicosRenderer::render(&ctx);
        let ip_routing = out.find("ip routing enable").expect("ip routing present");
        let qos_first  = out.find("class-of-service forwarding-class").expect("QoS present");
        let loopback   = out.find("loopback lo0").expect("loopback present");
        assert!(ip_routing < qos_first && qos_first < loopback,
            "QoS must sit between ip routing and loopback:\n{out}");
    }

    #[test]
    fn picos_emits_ip_routing_enable_line() {
        let out = PicosRenderer::render(&fixture("CORE02", vec![]));
        assert!(out.contains("set ip routing enable true"),
            "IP routing enable line missing:\n{out}");
    }

    #[test]
    fn picos_emits_lldp_enable_line() {
        let out = PicosRenderer::render(&fixture("CORE02", vec![]));
        assert!(out.contains("set protocols lldp enable true"),
            "LLDP enable line missing:\n{out}");
    }

    #[test]
    fn picos_ip_routing_lands_before_vlans_and_lldp_lands_after_port_rules() {
        // Matches legacy ConfigBuilderService section order: IP routing
        // is step 2 (right after system), LLDP is step 15 (late). Any
        // reorder would break diffs against prior generations.
        let ctx = fixture_with_vlans_and_svis(
            vec![VlanLine { vlan_id: 101, display_name: "IT".into(), description: None }],
            vec![L3SviLine { vlan_id: 101, address: "10.11.101.2/24".into() }],
        );
        let out = PicosRenderer::render(&ctx);
        let ip_routing = out.find("ip routing enable").expect("ip routing present");
        let first_vlan = out.find("set vlans vlan-id").expect("vlan present");
        let lldp       = out.find("protocols lldp enable").expect("lldp present");
        assert!(ip_routing < first_vlan,
            "ip routing must come before VLAN section:\n{out}");
        assert!(first_vlan < lldp,
            "LLDP must come after VLAN section (late in file):\n{out}");
    }

    #[test]
    fn picos_emits_each_protocol_enable_line_exactly_once() {
        let out = PicosRenderer::render(&fixture("CORE02", vec![]));
        assert_eq!(out.matches("ip routing enable true").count(), 1,
            "ip routing enable must not duplicate:\n{out}");
        assert_eq!(out.matches("protocols lldp enable true").count(), 1,
            "lldp enable must not duplicate:\n{out}");
    }

    #[test]
    fn picos_emits_vlan_svi_binding_when_svi_present() {
        let ctx = fixture_with_vlans_and_svis(
            vec![
                VlanLine { vlan_id: 101, display_name: "IT".into(), description: None },
            ],
            vec![
                L3SviLine { vlan_id: 101, address: "10.11.101.2/24".into() },
            ],
        );
        let out = PicosRenderer::render(&ctx);
        assert!(out.contains(r#"set vlans vlan-id 101 description "IT""#),
            "VLAN description missing:\n{out}");
        assert!(out.contains(r#"set vlans vlan-id 101 l3-interface "vlan-101""#),
            "VLAN→SVI binding line missing:\n{out}");
    }

    #[test]
    fn picos_omits_svi_binding_when_vlan_has_no_svi() {
        // Pure L2 trunk VLAN (no SVI) → no l3-interface binding line.
        let ctx = fixture_with_vlans_and_svis(
            vec![
                VlanLine { vlan_id: 500, display_name: "L2-only".into(), description: None },
            ],
            vec![],
        );
        let out = PicosRenderer::render(&ctx);
        assert!(out.contains(r#"set vlans vlan-id 500 description "L2-only""#));
        assert!(!out.contains("vlan-id 500 l3-interface"),
            "VLAN without SVI should not get the binding line:\n{out}");
    }

    #[test]
    fn picos_emits_binding_only_for_vlans_that_have_a_matching_svi() {
        // Mixed set: 101 has an SVI, 500 is pure L2. Binding emits only
        // for 101.
        let ctx = fixture_with_vlans_and_svis(
            vec![
                VlanLine { vlan_id: 101, display_name: "IT".into(), description: None },
                VlanLine { vlan_id: 500, display_name: "L2".into(), description: None },
            ],
            vec![
                L3SviLine { vlan_id: 101, address: "10.11.101.2/24".into() },
            ],
        );
        let out = PicosRenderer::render(&ctx);
        assert!(out.contains("vlan-id 101 l3-interface"),
            "101 should get binding:\n{out}");
        assert!(!out.contains("vlan-id 500 l3-interface"),
            "500 should NOT get binding:\n{out}");
    }

    #[test]
    fn picos_vlan_binding_lands_right_after_its_own_description() {
        // Order matters: for VLAN N, the binding line must sit between
        // that VLAN's description line and the NEXT VLAN's description
        // line. Otherwise diffing against prior output becomes messy.
        let ctx = fixture_with_vlans_and_svis(
            vec![
                VlanLine { vlan_id: 101, display_name: "IT".into(), description: None },
                VlanLine { vlan_id: 120, display_name: "Servers".into(), description: None },
            ],
            vec![
                L3SviLine { vlan_id: 101, address: "10.11.101.2/24".into() },
                L3SviLine { vlan_id: 120, address: "10.11.120.2/24".into() },
            ],
        );
        let out = PicosRenderer::render(&ctx);
        let desc_101    = out.find(r#"vlan-id 101 description"#).expect("101 desc");
        let bind_101    = out.find(r#"vlan-id 101 l3-interface"#).expect("101 binding");
        let desc_120    = out.find(r#"vlan-id 120 description"#).expect("120 desc");
        let bind_120    = out.find(r#"vlan-id 120 l3-interface"#).expect("120 binding");
        assert!(desc_101 < bind_101 && bind_101 < desc_120 && desc_120 < bind_120,
            "each binding must sit between its own description and the next:\n{out}");
    }

    #[test]
    fn picos_per_interface_lines_cluster_together_in_merged_ports_section() {
        // For each port that has BOTH a description and an L2 rule,
        // the three lines (description, native-vlan-id, port-mode)
        // must appear consecutively — no other port's lines
        // interleaved. This is the whole point of the merged section.
        let ctx = fixture_with_port_cfg(
            vec![
                PortDescriptionLine { interface_name: "xe-1/1/20".into(), description: "P2P".into() },
                PortDescriptionLine { interface_name: "ge-1/1/5".into(),  description: "Access".into() },
            ],
            vec![
                PortL2Line { interface_name: "xe-1/1/20".into(), port_mode: "trunk".into(),  native_vlan_id: Some(120) },
                PortL2Line { interface_name: "ge-1/1/5".into(),  port_mode: "access".into(), native_vlan_id: Some(101) },
            ],
        );
        let out = PicosRenderer::render(&ctx);
        let ge5_desc     = out.find("ge-1/1/5 description").expect("ge-1/1/5 desc");
        let ge5_native   = out.find("ge-1/1/5 family ethernet-switching native-vlan-id").expect("ge-1/1/5 native");
        let ge5_mode     = out.find(r#"ge-1/1/5 family ethernet-switching port-mode "access""#).expect("ge-1/1/5 mode");
        let xe20_desc    = out.find("xe-1/1/20 description").expect("xe-1/1/20 desc");
        // ge-1/1/5 sorts before xe-1/1/20 in BTreeMap order (g < x).
        assert!(ge5_desc < ge5_native && ge5_native < ge5_mode,
            "ge-1/1/5 description + native + mode must be contiguous:\n{out}");
        assert!(ge5_mode < xe20_desc,
            "ALL of ge-1/1/5's lines must appear before xe-1/1/20's first line:\n{out}");
    }

    #[test]
    fn picos_merged_ports_section_handles_description_only_interfaces() {
        // Interface with description but no L2 rule — e.g. a routed
        // port whose mode stays 'unset'. Only the description line
        // should render; no family ethernet-switching for that iface.
        let ctx = fixture_with_port_cfg(
            vec![PortDescriptionLine { interface_name: "xe-1/1/31".into(), description: "P2P-routed".into() }],
            vec![],
        );
        let out = PicosRenderer::render(&ctx);
        assert!(out.contains(r#"xe-1/1/31 description "P2P-routed""#),
            "description-only port should emit description line:\n{out}");
        assert!(!out.contains("xe-1/1/31 family ethernet-switching"),
            "description-only port should NOT emit ethernet-switching lines:\n{out}");
    }

    #[test]
    fn picos_merged_ports_section_handles_l2_only_interfaces() {
        // Interface with L2 rule but no description — e.g. an unused
        // trunk port we pre-configured. Only the L2 lines render.
        let ctx = fixture_with_port_cfg(
            vec![],
            vec![PortL2Line { interface_name: "xe-1/1/1".into(), port_mode: "trunk".into(), native_vlan_id: None }],
        );
        let out = PicosRenderer::render(&ctx);
        assert!(!out.contains("xe-1/1/1 description"),
            "no description row → no description line:\n{out}");
        assert!(out.contains(r#"xe-1/1/1 family ethernet-switching port-mode "trunk""#),
            "L2-only port should still emit port-mode line:\n{out}");
    }

    #[test]
    fn picos_emits_trunk_port_with_native_vlan() {
        let ctx = fixture_with_l2_rules(vec![
            PortL2Line { interface_name: "xe-1/1/20".into(), port_mode: "trunk".into(), native_vlan_id: Some(120) },
        ]);
        let out = PicosRenderer::render(&ctx);
        assert!(out.contains(r#"set interface gigabit-ethernet xe-1/1/20 family ethernet-switching native-vlan-id 120"#),
            "native-vlan-id line missing:\n{out}");
        assert!(out.contains(r#"set interface gigabit-ethernet xe-1/1/20 family ethernet-switching port-mode "trunk""#),
            "port-mode trunk line missing:\n{out}");
    }

    #[test]
    fn picos_emits_access_port_with_native_vlan() {
        let ctx = fixture_with_l2_rules(vec![
            PortL2Line { interface_name: "ge-1/1/5".into(), port_mode: "access".into(), native_vlan_id: Some(101) },
        ]);
        let out = PicosRenderer::render(&ctx);
        assert!(out.contains(r#"set interface gigabit-ethernet ge-1/1/5 family ethernet-switching native-vlan-id 101"#),
            "access native-vlan line missing:\n{out}");
        assert!(out.contains(r#"set interface gigabit-ethernet ge-1/1/5 family ethernet-switching port-mode "access""#),
            "port-mode access line missing:\n{out}");
    }

    #[test]
    fn picos_emits_port_mode_without_native_vlan_when_unset() {
        // A trunk port with no native VLAN set → emit port-mode only.
        let ctx = fixture_with_l2_rules(vec![
            PortL2Line { interface_name: "xe-1/1/1".into(), port_mode: "trunk".into(), native_vlan_id: None },
        ]);
        let out = PicosRenderer::render(&ctx);
        assert!(!out.contains("native-vlan-id"),
            "no native_vlan_id → no native-vlan-id line:\n{out}");
        assert!(out.contains(r#"port-mode "trunk""#),
            "port-mode line should still render:\n{out}");
    }

    #[test]
    fn picos_emits_native_vlan_before_port_mode_for_byte_parity() {
        // Legacy ConfigBuilderService emits native-vlan-id first, then
        // port-mode. Preserve that order so regenerated configs diff
        // cleanly against previously-shipped output.
        let ctx = fixture_with_l2_rules(vec![
            PortL2Line { interface_name: "xe-1/1/20".into(), port_mode: "trunk".into(), native_vlan_id: Some(120) },
        ]);
        let out = PicosRenderer::render(&ctx);
        let nat = out.find("native-vlan-id").expect("native-vlan-id present");
        let mode = out.find("port-mode").expect("port-mode present");
        assert!(nat < mode,
            "native-vlan-id must precede port-mode for this interface:\n{out}");
    }

    #[test]
    fn picos_omits_port_l2_section_when_empty() {
        let out = PicosRenderer::render(&fixture_with_l2_rules(vec![]));
        assert!(!out.contains("family ethernet-switching"),
            "no L2 rules in context → no ethernet-switching lines:\n{out}");
    }

    #[test]
    fn picos_emits_port_description_lines_with_gigabit_ethernet_keyword() {
        // PicOS uses the fixed keyword regardless of speed — xe-/ge-/et-
        // prefix in the interface name carries the actual media type.
        let ctx = fixture_with_ports(vec![
            PortDescriptionLine { interface_name: "xe-1/1/20".into(), description: "P2P-MEP-92-CORE01".into() },
            PortDescriptionLine { interface_name: "ge-1/1/12".into(), description: "Prox01-Trunk".into() },
        ]);
        let out = PicosRenderer::render(&ctx);
        assert!(out.contains(r#"set interface gigabit-ethernet xe-1/1/20 description "P2P-MEP-92-CORE01""#),
            "10G port description line missing:\n{out}");
        assert!(out.contains(r#"set interface gigabit-ethernet ge-1/1/12 description "Prox01-Trunk""#),
            "1G port description line missing:\n{out}");
    }

    #[test]
    fn picos_omits_port_description_section_when_empty() {
        let out = PicosRenderer::render(&fixture_with_ports(vec![]));
        assert!(!out.contains("set interface gigabit-ethernet"),
            "no port rows → no port description lines:\n{out}");
    }

    #[test]
    fn picos_port_description_escapes_quotes_in_description() {
        let ctx = fixture_with_ports(vec![
            PortDescriptionLine {
                interface_name: "xe-1/1/20".into(),
                description: r#"trunk "to" server01"#.into(),
            },
        ]);
        let out = PicosRenderer::render(&ctx);
        assert!(out.contains(r#"description "trunk \"to\" server01""#),
            "description quotes must be escaped:\n{out}");
    }

    #[test]
    fn picos_emits_port_descriptions_in_interface_name_order() {
        // Fetch query ORDER BYs interface_name; the renderer must
        // preserve that order so diffs against prior generations stay
        // stable.
        let ctx = fixture_with_ports(vec![
            PortDescriptionLine { interface_name: "ge-1/1/1".into(),  description: "a".into() },
            PortDescriptionLine { interface_name: "xe-1/1/10".into(), description: "b".into() },
            PortDescriptionLine { interface_name: "xe-1/1/20".into(), description: "c".into() },
        ]);
        let out = PicosRenderer::render(&ctx);
        let p1  = out.find("ge-1/1/1 ").expect("ge-1/1/1 present");
        let p10 = out.find("xe-1/1/10").expect("xe-1/1/10 present");
        let p20 = out.find("xe-1/1/20").expect("xe-1/1/20 present");
        assert!(p1 < p10 && p10 < p20,
            "port lines must stay in caller-provided order:\n{out}");
    }

    #[test]
    fn picos_emits_l3_svi_lines_for_each_subnet() {
        let ctx = fixture_with_svis(vec![
            L3SviLine { vlan_id: 101, address: "10.11.101.2/24".into() },
            L3SviLine { vlan_id: 120, address: "10.11.120.2/24".into() },
        ]);
        let out = PicosRenderer::render(&ctx);
        assert!(out.contains("set l3-interface vlan-interface vlan-101 address 10.11.101.2 prefix-length 24"),
            "vlan-101 SVI missing:\n{out}");
        assert!(out.contains("set l3-interface vlan-interface vlan-120 address 10.11.120.2 prefix-length 24"),
            "vlan-120 SVI missing:\n{out}");
    }

    #[test]
    fn picos_omits_l3_svi_section_when_empty() {
        let out = PicosRenderer::render(&fixture_with_svis(vec![]));
        // Only the header + hostname should be present; no SVI lines.
        assert!(!out.contains("set l3-interface vlan-interface"),
            "no SVIs in context → no SVI lines:\n{out}");
    }

    #[test]
    fn picos_skips_malformed_svi_inet_but_emits_others() {
        let ctx = fixture_with_svis(vec![
            L3SviLine { vlan_id: 101, address: "bogus".into() },
            L3SviLine { vlan_id: 120, address: "10.11.120.2/24".into() },
        ]);
        let out = PicosRenderer::render(&ctx);
        assert!(!out.contains("vlan-101"),
            "malformed vlan-101 entry should be skipped:\n{out}");
        assert!(out.contains("vlan-interface vlan-120 address 10.11.120.2 prefix-length 24"),
            "vlan-120 should still render:\n{out}");
    }

    #[test]
    fn picos_emits_svi_lines_in_vlan_id_order() {
        // The fetch query ORDER BYs vlan_id; if the fixture is given a
        // pre-sorted list the renderer must preserve that order.
        let ctx = fixture_with_svis(vec![
            L3SviLine { vlan_id: 82,  address: "10.11.82.2/24".into() },
            L3SviLine { vlan_id: 101, address: "10.11.101.2/24".into() },
            L3SviLine { vlan_id: 120, address: "10.11.120.2/24".into() },
        ]);
        let out = PicosRenderer::render(&ctx);
        let p82  = out.find("vlan-82 ").expect("vlan-82 present");
        let p101 = out.find("vlan-101").expect("vlan-101 present");
        let p120 = out.find("vlan-120").expect("vlan-120 present");
        assert!(p82 < p101 && p101 < p120,
            "SVI lines must stay in vlan_id order:\n{out}");
    }

    #[test]
    fn picos_emits_mstp_bridge_priority_when_allocated() {
        let out = PicosRenderer::render(&fixture_with_mstp_mlag(Some(12288), None));
        assert!(out.contains("set protocols spanning-tree mstp bridge-priority 12288"),
            "MSTP line missing:\n{out}");
    }

    #[test]
    fn picos_omits_mstp_section_when_no_allocation() {
        let out = PicosRenderer::render(&fixture_with_mstp_mlag(None, None));
        assert!(!out.contains("spanning-tree mstp"),
            "no MSTP allocation → no MSTP line:\n{out}");
    }

    #[test]
    fn picos_emits_full_mlag_block_when_both_fields_present() {
        let ctx = fixture_with_mstp_mlag(None, Some(MlagContext {
            domain_id: Some(1),
            peer_link_interface: Some("ae-100".into()),
        }));
        let out = PicosRenderer::render(&ctx);
        assert!(out.contains("set protocols mlag domain 1"),
            "MLAG domain line missing:\n{out}");
        assert!(out.contains("set protocols mlag peer-link ae-100"),
            "MLAG peer-link line missing:\n{out}");
    }

    #[test]
    fn picos_emits_partial_mlag_when_only_domain_set() {
        let ctx = fixture_with_mstp_mlag(None, Some(MlagContext {
            domain_id: Some(7),
            peer_link_interface: None,
        }));
        let out = PicosRenderer::render(&ctx);
        assert!(out.contains("set protocols mlag domain 7"),
            "partial MLAG should still emit the domain line:\n{out}");
        assert!(!out.contains("peer-link"),
            "no interface → no peer-link line:\n{out}");
    }

    #[test]
    fn picos_emits_partial_mlag_when_only_peer_link_set() {
        let ctx = fixture_with_mstp_mlag(None, Some(MlagContext {
            domain_id: None,
            peer_link_interface: Some("ae-100".into()),
        }));
        let out = PicosRenderer::render(&ctx);
        assert!(!out.contains("mlag domain"),
            "no domain → no domain line:\n{out}");
        assert!(out.contains("set protocols mlag peer-link ae-100"),
            "peer-link line should still render without domain:\n{out}");
    }

    #[test]
    fn picos_omits_mlag_block_when_mlag_context_empty_of_both_fields() {
        let ctx = fixture_with_mstp_mlag(None, Some(MlagContext {
            domain_id: None,
            peer_link_interface: None,
        }));
        let out = PicosRenderer::render(&ctx);
        assert!(!out.contains("protocols mlag"),
            "empty MlagContext → no MLAG lines:\n{out}");
    }

    #[test]
    fn picos_omits_mlag_peer_link_when_interface_is_empty_string() {
        // Defensive: interface_name can be an empty string in legacy
        // link rows where the port wasn't named — don't emit a
        // "peer-link " line with nothing after it.
        let ctx = fixture_with_mstp_mlag(None, Some(MlagContext {
            domain_id: Some(1),
            peer_link_interface: Some(String::new()),
        }));
        let out = PicosRenderer::render(&ctx);
        assert!(out.contains("mlag domain 1"));
        assert!(!out.contains("peer-link "),
            "empty interface string → no peer-link line:\n{out}");
    }

    #[test]
    fn clamp_render_list_limit_defaults_and_caps() {
        // None → default
        assert_eq!(clamp_render_list_limit(None), RENDER_LIST_DEFAULT);
        // Under 1 → snaps to 1
        assert_eq!(clamp_render_list_limit(Some(0)), 1);
        assert_eq!(clamp_render_list_limit(Some(-5)), 1);
        // Above cap → snaps to cap
        assert_eq!(clamp_render_list_limit(Some(RENDER_LIST_MAX + 1)), RENDER_LIST_MAX);
        assert_eq!(clamp_render_list_limit(Some(i64::MAX)), RENDER_LIST_MAX);
        // In-range passes through unchanged
        assert_eq!(clamp_render_list_limit(Some(25)), 25);
        assert_eq!(clamp_render_list_limit(Some(RENDER_LIST_MAX)), RENDER_LIST_MAX);
    }

    #[test]
    fn rendered_config_summary_serialises_camelcase_with_none_duration() {
        // Summary rows come back from the list endpoint — the shape is
        // the contract with Angular/WPF clients. Lock the camelCase
        // field names here so a rename on the Rust side breaks the
        // test (forces a matching update on the client).
        let s = RenderedConfigSummary {
            id:                 Uuid::nil(),
            device_id:          Uuid::nil(),
            flavor_code:        "PicOS".into(),
            body_sha256:        "deadbeef".into(),
            line_count:         42,
            render_duration_ms: None,
            previous_render_id: None,
            rendered_at:        Utc::now(),
            rendered_by:        Some(7),
        };
        let json = serde_json::to_string(&s).expect("serialises");
        assert!(json.contains("\"deviceId\""),         "deviceId key missing: {json}");
        assert!(json.contains("\"flavorCode\":\"PicOS\""), "flavorCode missing: {json}");
        assert!(json.contains("\"bodySha256\":\"deadbeef\""));
        assert!(json.contains("\"lineCount\":42"));
        assert!(json.contains("\"renderedBy\":7"));
        // None duration serialises as null (it's not Option-skipped here
        // — summaries always include it so clients can render "n/a"
        // without field-presence checks).
        assert!(json.contains("\"renderDurationMs\":null"),
            "renderDurationMs should be present-but-null when unset: {json}");
    }

    #[test]
    fn rendered_config_record_serialises_with_body() {
        // The full record — used by the GET-one endpoint — carries
        // the body. Make sure camelCase mapping is identical to the
        // summary so clients can upcast / downcast cleanly.
        let r = RenderedConfigRecord {
            id:                 Uuid::nil(),
            device_id:          Uuid::nil(),
            flavor_code:        "PicOS".into(),
            body:               "set system hostname \"x\"\n".into(),
            body_sha256:        "c0de".into(),
            line_count:         1,
            render_duration_ms: Some(12),
            previous_render_id: None,
            rendered_at:        Utc::now(),
            rendered_by:        None,
        };
        let json = serde_json::to_string(&r).expect("serialises");
        assert!(json.contains("\"body\":\"set system hostname"));
        assert!(json.contains("\"renderDurationMs\":12"));
    }

    #[test]
    fn build_result_leaves_persistence_fields_unset_on_dry_run() {
        // render_device (dry-run path) routes through build_result and
        // must NOT fill the persistence-only fields — those get
        // populated by persist_render during render_device_persisted.
        let body = "set system hostname \"x\"\n".to_string();
        let r = build_result(
            Uuid::nil(),
            cli_flavor::find_flavor("PicOS").unwrap(),
            body,
        );
        assert!(r.id.is_none(), "id stays None on dry-run render");
        assert!(r.previous_render_id.is_none(),
            "previous_render_id stays None on dry-run render");
        assert!(r.render_duration_ms.is_none(),
            "render_duration_ms stays None on dry-run render");
    }

    #[test]
    fn rendered_config_serde_skips_none_persistence_fields() {
        // Dry-run results serialise cleanly to JSON without dangling
        // `"id": null` keys — the camelCase API surface should only
        // show persistence fields when they're actually populated.
        let body = "x\n".to_string();
        let r = build_result(
            Uuid::nil(),
            cli_flavor::find_flavor("PicOS").unwrap(),
            body,
        );
        let json = serde_json::to_string(&r).expect("serializes");
        assert!(!json.contains("\"id\""),            "id should skip: {json}");
        assert!(!json.contains("previousRenderId"),  "previousRenderId should skip: {json}");
        assert!(!json.contains("renderDurationMs"),  "renderDurationMs should skip: {json}");
        // But the core fields stay present.
        assert!(json.contains("\"deviceId\""));
        assert!(json.contains("\"flavorCode\":\"PicOS\""));
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
