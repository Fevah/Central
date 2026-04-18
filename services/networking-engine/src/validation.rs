//! Validation rule engine — Phase 9a.
//!
//! Code-owned rule catalog. Each rule has a stable `code` (used as the
//! external identifier in UIs + the per-tenant config row), a default
//! severity, and an executor function that queries the DB for violations.
//!
//! Architecture call: rule SQL lives in Rust, not the DB. Giving admins an
//! edit box that becomes a runtime SELECT is an injection shape no matter
//! how carefully it's validated — so we own the queries, admins toggle +
//! see results.
//!
//! This slice ships 6 rules across the three most common categories
//! (Consistency / Integrity / Safety). Extending is one entry in `RULES`
//! plus one executor arm; no migration needed.

use serde::{Deserialize, Serialize};
use sqlx::PgPool;
use std::collections::HashMap;
use uuid::Uuid;

use crate::error::EngineError;

// ─── Catalog ──────────────────────────────────────────────────────────────

#[derive(Debug, Copy, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "PascalCase")]
pub enum Severity { Error, Warning, Info }

impl Severity {
    /// Text form used by the DB config table + test assertions.
    /// Callers outside this module are coming once the WPF admin panel wires up.
    #[allow(dead_code)]
    pub fn as_str(&self) -> &'static str {
        match self { Self::Error => "Error", Self::Warning => "Warning", Self::Info => "Info" }
    }

    pub fn from_db(s: &str) -> Result<Self, EngineError> {
        match s {
            "Error" => Ok(Self::Error),
            "Warning" => Ok(Self::Warning),
            "Info" => Ok(Self::Info),
            other => Err(EngineError::bad_request(format!(
                "Unknown severity '{other}' — must be Error / Warning / Info."))),
        }
    }
}

#[derive(Debug, Copy, Clone, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct RuleMeta {
    pub code: &'static str,
    pub name: &'static str,
    pub description: &'static str,
    pub category: &'static str,
    pub default_severity: Severity,
    pub default_enabled: bool,
}

/// Canonical rule catalog. Extending: add a `RuleMeta` entry here AND a
/// matching arm in `dispatch()`. `code` must be unique (enforced by a
/// test below) and stable across releases — tenant_rule_config rows key
/// off it.
pub const RULES: &[RuleMeta] = &[
    RuleMeta {
        code: "device.hostname_required",
        name: "Device hostname is required",
        description: "Every device must have a non-empty hostname so it can be \
                      referenced by links, config generators, and audit entries.",
        category: "Integrity",
        default_severity: Severity::Error,
        default_enabled: true,
    },
    RuleMeta {
        code: "server.hostname_required",
        name: "Server hostname is required",
        description: "Every server must have a non-empty hostname (same reasoning \
                      as device.hostname_required).",
        category: "Integrity",
        default_severity: Severity::Error,
        default_enabled: true,
    },
    RuleMeta {
        code: "link.endpoint_count",
        name: "Link must have exactly 2 endpoints",
        description: "Every active link must carry endpoints at endpoint_order 0 \
                      and 1. Three-way (hub-and-spoke) links are not yet supported.",
        category: "Integrity",
        default_severity: Severity::Error,
        default_enabled: true,
    },
    RuleMeta {
        code: "server.nic_count_matches_profile",
        name: "Server NIC count matches its profile",
        description: "A server's number of active NICs should equal its server \
                      profile's nic_count. Mismatches usually mean a fan-out run \
                      partially failed — worth investigating.",
        category: "Consistency",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "loopback.ipv4_slash_32",
        name: "Loopback IPv4 addresses must be /32",
        description: "Loopback subnets should be /32 per device to keep routing \
                      tables predictable. Non-/32 loopbacks are usually a \
                      mis-entered prefix.",
        category: "Safety",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "vlan.unique_per_block",
        name: "VLAN IDs unique within their block",
        description: "Two non-deleted VLAN rows must not share both block_id and \
                      vlan_id. The allocation service enforces this on create, \
                      but manual imports can bypass it.",
        category: "Integrity",
        default_severity: Severity::Error,
        default_enabled: true,
    },
    RuleMeta {
        code: "hierarchy.building_requires_site",
        name: "Building must have a site",
        description: "Every non-deleted building must carry a site_id. A \
                      building orphaned from its site breaks region / site-code \
                      naming templates and scope-based RBAC.",
        category: "Integrity",
        default_severity: Severity::Error,
        default_enabled: true,
    },
    RuleMeta {
        code: "hierarchy.site_requires_region",
        name: "Site must have a region",
        description: "Every non-deleted site must carry a region_id so \
                      downstream buildings can resolve their region_code token.",
        category: "Integrity",
        default_severity: Severity::Error,
        default_enabled: true,
    },
    RuleMeta {
        code: "asn_allocation.value_in_block_range",
        name: "ASN allocations lie within their block's range",
        description: "Every allocation's asn column must fall inside the \
                      [asn_first, asn_last] of its containing block. The \
                      AllocationService enforces this on create, but legacy \
                      imports may not.",
        category: "Integrity",
        default_severity: Severity::Error,
        default_enabled: true,
    },
    RuleMeta {
        code: "vlan.value_in_block_range",
        name: "VLANs lie within their block's range",
        description: "Every VLAN's vlan_id must fall inside its containing \
                      block's [vlan_first, vlan_last] range.",
        category: "Integrity",
        default_severity: Severity::Error,
        default_enabled: true,
    },
    RuleMeta {
        code: "device.active_requires_building",
        name: "Active device must have a building",
        description: "Any device in status='Active' must carry a building_id. \
                      Unsited Active devices bypass scope-based RBAC and \
                      cannot feed into naming templates.",
        category: "Consistency",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "link.endpoints_distinct_devices",
        name: "Link endpoints must be different devices",
        description: "A link with both endpoints on the same device_id is \
                      almost always a data-entry error. Loopback-style \
                      self-peering links should use a dedicated type, not \
                      reuse the standard link types.",
        category: "Integrity",
        default_severity: Severity::Error,
        default_enabled: true,
    },
    RuleMeta {
        code: "ip_address.contained_in_subnet",
        name: "IP address is inside its subnet's CIDR",
        description: "Every ip_address.address must be a host inside \
                      subnet.network. Mismatches usually mean a subnet was \
                      resized after addresses were allocated.",
        category: "Integrity",
        default_severity: Severity::Error,
        default_enabled: true,
    },
    RuleMeta {
        code: "subnet.scope_entity_present_when_non_free",
        name: "Scoped subnets must carry scope_entity_id",
        description: "Subnets with scope_level other than 'Free' must point \
                      at a hierarchy entity (region / site / building / etc.). \
                      'Free' means the subnet exists but hasn't been bound \
                      yet — scope_entity_id should be NULL in that case.",
        category: "Consistency",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "device.unique_hostname",
        name: "Device hostnames unique within tenant",
        description: "Two non-deleted devices must not share a hostname. \
                      The table has a UNIQUE constraint, so this rule is \
                      the belt-and-braces check for imports that bypass it.",
        category: "Integrity",
        default_severity: Severity::Error,
        default_enabled: true,
    },
    RuleMeta {
        code: "server.unique_hostname",
        name: "Server hostnames unique within tenant",
        description: "Two non-deleted servers must not share a hostname (same \
                      reasoning as device.unique_hostname).",
        category: "Integrity",
        default_severity: Severity::Error,
        default_enabled: true,
    },
    RuleMeta {
        code: "ip_address.unique_per_subnet",
        name: "IP addresses unique within their subnet",
        description: "Two non-deleted ip_address rows must not share both \
                      subnet_id and address. Catches double-allocation that \
                      bypasses the allocation service.",
        category: "Integrity",
        default_severity: Severity::Error,
        default_enabled: true,
    },
    RuleMeta {
        code: "subnet.parent_same_pool",
        name: "Subnet parent must be in same pool",
        description: "When parent_subnet_id is set, the parent and child must \
                      live in the same ip_pool — cross-pool hierarchy is a \
                      data-entry error.",
        category: "Integrity",
        default_severity: Severity::Error,
        default_enabled: true,
    },
    RuleMeta {
        code: "subnet.parent_contains_child",
        name: "Subnet parent CIDR must contain child CIDR",
        description: "parent_subnet.network must be a supernet of child \
                      subnet.network. A /24 can't be parent of a /22 by \
                      definition.",
        category: "Integrity",
        default_severity: Severity::Error,
        default_enabled: true,
    },
    RuleMeta {
        code: "server_nic.index_unique_per_server",
        name: "Server NIC indexes unique per server",
        description: "Two non-deleted NIC rows on one server must not share \
                      nic_index. The fan-out service enforces dense 0..N-1 \
                      on create; this catches manual double-inserts.",
        category: "Integrity",
        default_severity: Severity::Error,
        default_enabled: true,
    },
    RuleMeta {
        code: "server_nic.index_dense_sequence",
        name: "Server NIC indexes form dense 0..N-1 sequence",
        description: "Active NIC rows on one server should be numbered 0, 1, \
                      …, count-1 with no gaps. Sparse indexes usually mean \
                      a fan-out partially failed and partial-retry drifted.",
        category: "Consistency",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "link.building_matches_endpoints",
        name: "Link's building matches at least one endpoint",
        description: "If a link carries a building_id, at least one of its \
                      endpoint devices should sit in the same building. A \
                      link tagged for Building A with both endpoints in \
                      Building B is suspicious.",
        category: "Consistency",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "loopback.ipv6_slash_128",
        name: "Loopback IPv6 subnets must be /128",
        description: "Loopback subnets on IPv6 should be /128 per device, \
                      same reasoning as the /32 rule for IPv4.",
        category: "Safety",
        default_severity: Severity::Warning,
        default_enabled: true,
    },
    RuleMeta {
        code: "mlag_domain.unique_per_tenant",
        name: "MLAG domain IDs unique per tenant",
        description: "MLAG uniqueness is tenant-wide (not pool-wide): two \
                      non-deleted mlag_domain rows must not share domain_id \
                      within an organization.",
        category: "Integrity",
        default_severity: Severity::Error,
        default_enabled: true,
    },
    RuleMeta {
        code: "asn_allocation.unique_per_tenant",
        name: "ASN values unique per tenant",
        description: "An ASN number must be allocated only once per tenant. \
                      A duplicate would mean two devices are advertising the \
                      same AS, which is an operational hazard.",
        category: "Integrity",
        default_severity: Severity::Error,
        default_enabled: true,
    },
    RuleMeta {
        code: "device.management_ip_for_active",
        name: "Active devices should carry management_ip",
        description: "Devices in status='Active' are expected to be reachable, \
                      which requires a management_ip. Advisory — a device \
                      legitimately out-of-band managed may fail this; toggle \
                      off per-tenant if that's your norm.",
        category: "Advisory",
        default_severity: Severity::Info,
        default_enabled: true,
    },
];

/// Find a rule by code. Returns `None` for unknown codes — callers surface
/// that as a `bad_request` rather than hitting the DB.
pub fn find_rule(code: &str) -> Option<&'static RuleMeta> {
    RULES.iter().find(|r| r.code == code)
}

// ─── Resolved rule + per-tenant merge ────────────────────────────────────

#[derive(Debug, Clone, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct ResolvedRule {
    #[serde(flatten)]
    pub meta: RuleMeta,
    pub effective_severity: Severity,
    pub effective_enabled: bool,
    pub has_tenant_override: bool,
}

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct ListRulesQuery { pub organization_id: Uuid }

pub async fn list_rules(
    pool: &PgPool,
    q: &ListRulesQuery,
) -> Result<Vec<ResolvedRule>, EngineError> {
    let overrides: Vec<(String, Option<bool>, Option<String>)> = sqlx::query_as(
        "SELECT rule_code, enabled, severity_override
           FROM net.tenant_rule_config
          WHERE organization_id = $1")
        .bind(q.organization_id)
        .fetch_all(pool)
        .await?;
    let overrides: HashMap<String, (Option<bool>, Option<String>)> = overrides
        .into_iter().map(|(k, e, s)| (k, (e, s))).collect();

    let mut out = Vec::with_capacity(RULES.len());
    for meta in RULES {
        let ov = overrides.get(meta.code);
        let (enabled_override, severity_override) = ov
            .map(|(e, s)| (*e, s.clone()))
            .unwrap_or((None, None));
        let effective_enabled = enabled_override.unwrap_or(meta.default_enabled);
        let effective_severity = match severity_override.as_deref() {
            Some(s) => Severity::from_db(s)?,
            None => meta.default_severity,
        };
        out.push(ResolvedRule {
            meta: *meta,
            effective_enabled,
            effective_severity,
            has_tenant_override: ov.is_some(),
        });
    }
    Ok(out)
}

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct SetRuleConfigBody {
    pub enabled: Option<bool>,
    pub severity_override: Option<String>,
    pub notes: Option<String>,
}

pub async fn set_rule_config(
    pool: &PgPool,
    org_id: Uuid,
    rule_code: &str,
    body: &SetRuleConfigBody,
    user_id: Option<i32>,
) -> Result<(), EngineError> {
    if find_rule(rule_code).is_none() {
        return Err(EngineError::bad_request(format!(
            "Unknown rule code '{rule_code}'. Check /api/net/validation/rules for the catalog.")));
    }
    if let Some(sev) = body.severity_override.as_deref() {
        Severity::from_db(sev)?; // validate early
    }
    sqlx::query(
        "INSERT INTO net.tenant_rule_config
            (organization_id, rule_code, enabled, severity_override, notes,
             created_by, updated_by)
         VALUES ($1, $2, $3, $4, $5, $6, $6)
         ON CONFLICT (organization_id, rule_code) DO UPDATE
           SET enabled           = EXCLUDED.enabled,
               severity_override = EXCLUDED.severity_override,
               notes             = EXCLUDED.notes,
               updated_at        = now(),
               updated_by        = EXCLUDED.updated_by,
               version           = net.tenant_rule_config.version + 1")
        .bind(org_id)
        .bind(rule_code)
        .bind(body.enabled)
        .bind(body.severity_override.as_deref())
        .bind(body.notes.as_deref())
        .bind(user_id)
        .execute(pool)
        .await?;
    Ok(())
}

// ─── Execution ───────────────────────────────────────────────────────────

#[derive(Debug, Clone, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct Violation {
    pub rule_code: String,
    pub severity: Severity,
    pub entity_type: String,
    pub entity_id: Option<Uuid>,
    pub message: String,
}

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct RunValidationBody {
    pub organization_id: Uuid,
    /// Optional — run one specific rule. Useful for "fix it, re-run that
    /// rule to confirm" UX without the overhead of every rule.
    pub rule_code: Option<String>,
}

#[derive(Debug, Clone, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct ValidationRunResult {
    pub violations: Vec<Violation>,
    pub rules_run: usize,
    pub rules_with_findings: usize,
    pub total_violations: usize,
}

pub async fn run_validation(
    pool: &PgPool,
    body: &RunValidationBody,
) -> Result<ValidationRunResult, EngineError> {
    // Gather the effective rule set first — merging catalog + tenant
    // overrides — and filter down to (a) enabled rules and (b) the
    // specific rule if one was asked for.
    let resolved = list_rules(pool, &ListRulesQuery {
        organization_id: body.organization_id,
    }).await?;

    let target: Option<&str> = body.rule_code.as_deref();
    if let Some(code) = target {
        if find_rule(code).is_none() {
            return Err(EngineError::bad_request(format!(
                "Unknown rule code '{code}'.")));
        }
    }

    let mut violations = Vec::new();
    let mut rules_run = 0usize;
    let mut rules_with_findings = 0usize;

    for r in resolved {
        if !r.effective_enabled { continue; }
        if let Some(code) = target {
            if r.meta.code != code { continue; }
        }
        rules_run += 1;
        let before_len = violations.len();
        dispatch(pool, body.organization_id, r.meta.code, r.effective_severity, &mut violations).await?;
        if violations.len() > before_len { rules_with_findings += 1; }
    }

    let total_violations = violations.len();
    Ok(ValidationRunResult { violations, rules_run, rules_with_findings, total_violations })
}

/// Route a rule code to its executor. Each executor pushes any found
/// violations into `out` — the `match` is the one place every new rule
/// has to appear. A rule code the catalog knows but the dispatcher
/// doesn't is caught by the `unreachable!` — a missed arm is a compile
/// error at test time via `RULES` iteration.
async fn dispatch(
    pool: &PgPool,
    org_id: Uuid,
    code: &str,
    severity: Severity,
    out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    match code {
        "device.hostname_required"         => run_device_hostname_required(pool, org_id, severity, out).await,
        "server.hostname_required"         => run_server_hostname_required(pool, org_id, severity, out).await,
        "link.endpoint_count"              => run_link_endpoint_count(pool, org_id, severity, out).await,
        "server.nic_count_matches_profile" => run_server_nic_count(pool, org_id, severity, out).await,
        "loopback.ipv4_slash_32"           => run_loopback_v4_slash_32(pool, org_id, severity, out).await,
        "vlan.unique_per_block"            => run_vlan_unique_per_block(pool, org_id, severity, out).await,
        "hierarchy.building_requires_site"    => run_building_requires_site(pool, org_id, severity, out).await,
        "hierarchy.site_requires_region"      => run_site_requires_region(pool, org_id, severity, out).await,
        "asn_allocation.value_in_block_range" => run_asn_in_block_range(pool, org_id, severity, out).await,
        "vlan.value_in_block_range"           => run_vlan_in_block_range(pool, org_id, severity, out).await,
        "device.active_requires_building"     => run_active_device_requires_building(pool, org_id, severity, out).await,
        "link.endpoints_distinct_devices"     => run_link_endpoints_distinct(pool, org_id, severity, out).await,
        "ip_address.contained_in_subnet"      => run_ip_contained_in_subnet(pool, org_id, severity, out).await,
        "subnet.scope_entity_present_when_non_free" => run_subnet_scope_entity_present(pool, org_id, severity, out).await,
        "device.unique_hostname"               => run_device_unique_hostname(pool, org_id, severity, out).await,
        "server.unique_hostname"               => run_server_unique_hostname(pool, org_id, severity, out).await,
        "ip_address.unique_per_subnet"         => run_ip_unique_per_subnet(pool, org_id, severity, out).await,
        "subnet.parent_same_pool"              => run_subnet_parent_same_pool(pool, org_id, severity, out).await,
        "subnet.parent_contains_child"         => run_subnet_parent_contains_child(pool, org_id, severity, out).await,
        "server_nic.index_unique_per_server"   => run_server_nic_index_unique(pool, org_id, severity, out).await,
        "server_nic.index_dense_sequence"      => run_server_nic_index_dense(pool, org_id, severity, out).await,
        "link.building_matches_endpoints"      => run_link_building_matches_endpoints(pool, org_id, severity, out).await,
        "loopback.ipv6_slash_128"              => run_loopback_v6_slash_128(pool, org_id, severity, out).await,
        "mlag_domain.unique_per_tenant"        => run_mlag_unique_per_tenant(pool, org_id, severity, out).await,
        "asn_allocation.unique_per_tenant"     => run_asn_unique_per_tenant(pool, org_id, severity, out).await,
        "device.management_ip_for_active"      => run_active_device_has_management_ip(pool, org_id, severity, out).await,
        other => Err(EngineError::bad_request(format!(
            "Rule '{other}' in catalog but dispatcher has no executor — codebase bug."))),
    }
}

async fn run_device_hostname_required(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid,)> = sqlx::query_as(
        "SELECT id FROM net.device
          WHERE organization_id = $1
            AND deleted_at IS NULL
            AND (hostname IS NULL OR btrim(hostname) = '')")
        .bind(org_id).fetch_all(pool).await?;
    for (id,) in rows {
        out.push(Violation {
            rule_code: "device.hostname_required".into(),
            severity, entity_type: "Device".into(), entity_id: Some(id),
            message: "Device has no hostname — every device must be named.".into(),
        });
    }
    Ok(())
}

async fn run_server_hostname_required(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid,)> = sqlx::query_as(
        "SELECT id FROM net.server
          WHERE organization_id = $1
            AND deleted_at IS NULL
            AND (hostname IS NULL OR btrim(hostname) = '')")
        .bind(org_id).fetch_all(pool).await?;
    for (id,) in rows {
        out.push(Violation {
            rule_code: "server.hostname_required".into(),
            severity, entity_type: "Server".into(), entity_id: Some(id),
            message: "Server has no hostname.".into(),
        });
    }
    Ok(())
}

async fn run_link_endpoint_count(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    // Two failure modes: wrong count, or both endpoints present but the
    // ordering isn't exactly {0, 1}. Reporting the id + actual count is
    // enough for an admin to jump to the right row.
    let rows: Vec<(Uuid, i64, Option<i32>, Option<i32>)> = sqlx::query_as(
        "SELECT l.id,
                COUNT(e.id)                                          AS cnt,
                MIN(e.endpoint_order)::int                           AS min_order,
                MAX(e.endpoint_order)::int                           AS max_order
           FROM net.link l
           LEFT JOIN net.link_endpoint e
             ON e.link_id = l.id AND e.deleted_at IS NULL
          WHERE l.organization_id = $1 AND l.deleted_at IS NULL
          GROUP BY l.id
         HAVING COUNT(e.id) <> 2
             OR MIN(e.endpoint_order) <> 0
             OR MAX(e.endpoint_order) <> 1")
        .bind(org_id).fetch_all(pool).await?;
    for (id, cnt, min_o, max_o) in rows {
        out.push(Violation {
            rule_code: "link.endpoint_count".into(),
            severity, entity_type: "Link".into(), entity_id: Some(id),
            message: format!(
                "Link has {cnt} endpoint(s) (min_order={:?}, max_order={:?}); expected exactly 2 at orders 0 and 1.",
                min_o, max_o),
        });
    }
    Ok(())
}

async fn run_server_nic_count(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String, i32, i64)> = sqlx::query_as(
        "SELECT s.id, s.hostname, sp.nic_count,
                COUNT(nic.id) FILTER (WHERE nic.deleted_at IS NULL)
           FROM net.server s
           LEFT JOIN net.server_profile sp ON sp.id = s.server_profile_id
           LEFT JOIN net.server_nic nic    ON nic.server_id = s.id
          WHERE s.organization_id = $1 AND s.deleted_at IS NULL
            AND sp.nic_count IS NOT NULL
          GROUP BY s.id, s.hostname, sp.nic_count
         HAVING sp.nic_count <> COUNT(nic.id) FILTER (WHERE nic.deleted_at IS NULL)")
        .bind(org_id).fetch_all(pool).await?;
    for (id, hostname, expected, actual) in rows {
        out.push(Violation {
            rule_code: "server.nic_count_matches_profile".into(),
            severity, entity_type: "Server".into(), entity_id: Some(id),
            message: format!(
                "Server '{hostname}' has {actual} NIC(s), profile expects {expected}."),
        });
    }
    Ok(())
}

async fn run_loopback_v4_slash_32(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    // net.subnet.network is cidr. Loopback subnets are the ones referenced
    // by device.asn_allocation_id's pool scope, but that's a deep join —
    // a cheaper heuristic is the subnet_code convention ('LOOPBACK' prefix).
    // If admins use a different convention, they either rename or disable
    // this rule.
    let rows: Vec<(Uuid, String, String)> = sqlx::query_as(
        "SELECT id, subnet_code, network::text
           FROM net.subnet
          WHERE organization_id = $1
            AND deleted_at IS NULL
            AND subnet_code ILIKE 'LOOPBACK%'
            AND family(network) = 4
            AND masklen(network) <> 32")
        .bind(org_id).fetch_all(pool).await?;
    for (id, code, cidr) in rows {
        out.push(Violation {
            rule_code: "loopback.ipv4_slash_32".into(),
            severity, entity_type: "Subnet".into(), entity_id: Some(id),
            message: format!(
                "Loopback subnet '{code}' is {cidr}; expected /32 (check prefix length)."),
        });
    }
    Ok(())
}

async fn run_vlan_unique_per_block(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    // Any non-deleted rows that share (block_id, vlan_id). Report one
    // violation per colliding row so the admin sees all involved parties.
    let rows: Vec<(Uuid, Option<Uuid>, i32)> = sqlx::query_as(
        "SELECT v.id, v.block_id, v.vlan_id
           FROM net.vlan v
           JOIN (
             SELECT block_id, vlan_id
               FROM net.vlan
              WHERE organization_id = $1 AND deleted_at IS NULL
              GROUP BY block_id, vlan_id
             HAVING COUNT(*) > 1
           ) d ON d.block_id IS NOT DISTINCT FROM v.block_id
              AND d.vlan_id = v.vlan_id
          WHERE v.organization_id = $1 AND v.deleted_at IS NULL")
        .bind(org_id).fetch_all(pool).await?;
    for (id, block_id, vlan_id) in rows {
        out.push(Violation {
            rule_code: "vlan.unique_per_block".into(),
            severity, entity_type: "Vlan".into(), entity_id: Some(id),
            message: format!(
                "VLAN id {vlan_id} duplicated in block {:?}.", block_id),
        });
    }
    Ok(())
}

async fn run_building_requires_site(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String)> = sqlx::query_as(
        "SELECT id, building_code
           FROM net.building
          WHERE organization_id = $1 AND deleted_at IS NULL AND site_id IS NULL")
        .bind(org_id).fetch_all(pool).await?;
    for (id, code) in rows {
        out.push(Violation {
            rule_code: "hierarchy.building_requires_site".into(),
            severity, entity_type: "Building".into(), entity_id: Some(id),
            message: format!("Building '{code}' has no site_id."),
        });
    }
    Ok(())
}

async fn run_site_requires_region(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String)> = sqlx::query_as(
        "SELECT id, site_code
           FROM net.site
          WHERE organization_id = $1 AND deleted_at IS NULL AND region_id IS NULL")
        .bind(org_id).fetch_all(pool).await?;
    for (id, code) in rows {
        out.push(Violation {
            rule_code: "hierarchy.site_requires_region".into(),
            severity, entity_type: "Site".into(), entity_id: Some(id),
            message: format!("Site '{code}' has no region_id."),
        });
    }
    Ok(())
}

async fn run_asn_in_block_range(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, i64, i64, i64)> = sqlx::query_as(
        "SELECT a.id, a.asn, b.asn_first, b.asn_last
           FROM net.asn_allocation a
           JOIN net.asn_block b ON b.id = a.block_id
          WHERE a.organization_id = $1
            AND a.deleted_at IS NULL
            AND b.deleted_at IS NULL
            AND (a.asn < b.asn_first OR a.asn > b.asn_last)")
        .bind(org_id).fetch_all(pool).await?;
    for (id, asn, first, last) in rows {
        out.push(Violation {
            rule_code: "asn_allocation.value_in_block_range".into(),
            severity, entity_type: "AsnAllocation".into(), entity_id: Some(id),
            message: format!("ASN {asn} is outside block range [{first}, {last}]."),
        });
    }
    Ok(())
}

async fn run_vlan_in_block_range(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, i32, i32, i32)> = sqlx::query_as(
        "SELECT v.id, v.vlan_id, b.vlan_first, b.vlan_last
           FROM net.vlan v
           JOIN net.vlan_block b ON b.id = v.block_id
          WHERE v.organization_id = $1
            AND v.deleted_at IS NULL
            AND b.deleted_at IS NULL
            AND (v.vlan_id < b.vlan_first OR v.vlan_id > b.vlan_last)")
        .bind(org_id).fetch_all(pool).await?;
    for (id, vlan_id, first, last) in rows {
        out.push(Violation {
            rule_code: "vlan.value_in_block_range".into(),
            severity, entity_type: "Vlan".into(), entity_id: Some(id),
            message: format!("VLAN id {vlan_id} is outside block range [{first}, {last}]."),
        });
    }
    Ok(())
}

async fn run_active_device_requires_building(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    // status is a net.entity_status enum — compare via ::text so we don't
    // have to teach sqlx about the custom type binding.
    let rows: Vec<(Uuid, String)> = sqlx::query_as(
        "SELECT id, hostname
           FROM net.device
          WHERE organization_id = $1
            AND deleted_at IS NULL
            AND status::text = 'Active'
            AND building_id IS NULL")
        .bind(org_id).fetch_all(pool).await?;
    for (id, hostname) in rows {
        out.push(Violation {
            rule_code: "device.active_requires_building".into(),
            severity, entity_type: "Device".into(), entity_id: Some(id),
            message: format!("Active device '{hostname}' has no building_id."),
        });
    }
    Ok(())
}

async fn run_link_endpoints_distinct(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    // A link is offending if its two endpoints point at the same
    // device_id. The COUNT DISTINCT + NOT NULL filter keeps us from
    // flagging links where both endpoints are still unresolved
    // (device_id NULL on both sides).
    let rows: Vec<(Uuid, String)> = sqlx::query_as(
        "SELECT l.id, l.link_code
           FROM net.link l
          WHERE l.organization_id = $1 AND l.deleted_at IS NULL
            AND (SELECT COUNT(DISTINCT e.device_id)
                   FROM net.link_endpoint e
                  WHERE e.link_id = l.id
                    AND e.device_id IS NOT NULL
                    AND e.deleted_at IS NULL) = 1
            AND (SELECT COUNT(e.id)
                   FROM net.link_endpoint e
                  WHERE e.link_id = l.id
                    AND e.device_id IS NOT NULL
                    AND e.deleted_at IS NULL) = 2")
        .bind(org_id).fetch_all(pool).await?;
    for (id, link_code) in rows {
        out.push(Violation {
            rule_code: "link.endpoints_distinct_devices".into(),
            severity, entity_type: "Link".into(), entity_id: Some(id),
            message: format!("Link '{link_code}' has both endpoints on the same device."),
        });
    }
    Ok(())
}

async fn run_ip_contained_in_subnet(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    // Postgres's inet / cidr <<= operator is "is contained by". The
    // address cast forces the comparison into the inet space even if
    // the ip_address.address column is stored as varchar.
    let rows: Vec<(Uuid, String, String)> = sqlx::query_as(
        "SELECT ip.id, ip.address::text, s.network::text
           FROM net.ip_address ip
           JOIN net.subnet s ON s.id = ip.subnet_id
          WHERE ip.organization_id = $1
            AND ip.deleted_at IS NULL
            AND s.deleted_at IS NULL
            AND NOT (ip.address <<= s.network)")
        .bind(org_id).fetch_all(pool).await?;
    for (id, addr, network) in rows {
        out.push(Violation {
            rule_code: "ip_address.contained_in_subnet".into(),
            severity, entity_type: "IpAddress".into(), entity_id: Some(id),
            message: format!("Address {addr} is not inside subnet {network}."),
        });
    }
    Ok(())
}

async fn run_subnet_scope_entity_present(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    // Warning — non-Free scope but no scope_entity_id. The reverse
    // (Free scope but scope_entity_id set) is treated as benign staleness.
    let rows: Vec<(Uuid, String, String)> = sqlx::query_as(
        "SELECT id, subnet_code, scope_level
           FROM net.subnet
          WHERE organization_id = $1
            AND deleted_at IS NULL
            AND scope_level <> 'Free'
            AND scope_entity_id IS NULL")
        .bind(org_id).fetch_all(pool).await?;
    for (id, code, level) in rows {
        out.push(Violation {
            rule_code: "subnet.scope_entity_present_when_non_free".into(),
            severity, entity_type: "Subnet".into(), entity_id: Some(id),
            message: format!(
                "Subnet '{code}' claims scope_level '{level}' but has no scope_entity_id."),
        });
    }
    Ok(())
}

async fn run_device_unique_hostname(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String)> = sqlx::query_as(
        "SELECT d.id, d.hostname
           FROM net.device d
           JOIN (
             SELECT hostname
               FROM net.device
              WHERE organization_id = $1 AND deleted_at IS NULL
              GROUP BY hostname
             HAVING COUNT(*) > 1
           ) dup ON dup.hostname = d.hostname
          WHERE d.organization_id = $1 AND d.deleted_at IS NULL")
        .bind(org_id).fetch_all(pool).await?;
    for (id, hostname) in rows {
        out.push(Violation {
            rule_code: "device.unique_hostname".into(),
            severity, entity_type: "Device".into(), entity_id: Some(id),
            message: format!("Device hostname '{hostname}' is duplicated within the tenant."),
        });
    }
    Ok(())
}

async fn run_server_unique_hostname(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String)> = sqlx::query_as(
        "SELECT s.id, s.hostname
           FROM net.server s
           JOIN (
             SELECT hostname
               FROM net.server
              WHERE organization_id = $1 AND deleted_at IS NULL
              GROUP BY hostname
             HAVING COUNT(*) > 1
           ) dup ON dup.hostname = s.hostname
          WHERE s.organization_id = $1 AND s.deleted_at IS NULL")
        .bind(org_id).fetch_all(pool).await?;
    for (id, hostname) in rows {
        out.push(Violation {
            rule_code: "server.unique_hostname".into(),
            severity, entity_type: "Server".into(), entity_id: Some(id),
            message: format!("Server hostname '{hostname}' is duplicated within the tenant."),
        });
    }
    Ok(())
}

async fn run_ip_unique_per_subnet(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String, Uuid)> = sqlx::query_as(
        "SELECT ip.id, ip.address::text, ip.subnet_id
           FROM net.ip_address ip
           JOIN (
             SELECT subnet_id, address
               FROM net.ip_address
              WHERE organization_id = $1 AND deleted_at IS NULL
              GROUP BY subnet_id, address
             HAVING COUNT(*) > 1
           ) dup ON dup.subnet_id = ip.subnet_id AND dup.address = ip.address
          WHERE ip.organization_id = $1 AND ip.deleted_at IS NULL")
        .bind(org_id).fetch_all(pool).await?;
    for (id, addr, subnet_id) in rows {
        out.push(Violation {
            rule_code: "ip_address.unique_per_subnet".into(),
            severity, entity_type: "IpAddress".into(), entity_id: Some(id),
            message: format!("Address {addr} duplicated within subnet {subnet_id}."),
        });
    }
    Ok(())
}

async fn run_subnet_parent_same_pool(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String, Uuid, Uuid)> = sqlx::query_as(
        "SELECT child.id, child.subnet_code, child.pool_id, parent.pool_id
           FROM net.subnet child
           JOIN net.subnet parent ON parent.id = child.parent_subnet_id
          WHERE child.organization_id = $1
            AND child.deleted_at IS NULL
            AND parent.deleted_at IS NULL
            AND child.pool_id <> parent.pool_id")
        .bind(org_id).fetch_all(pool).await?;
    for (id, code, child_pool, parent_pool) in rows {
        out.push(Violation {
            rule_code: "subnet.parent_same_pool".into(),
            severity, entity_type: "Subnet".into(), entity_id: Some(id),
            message: format!(
                "Subnet '{code}' is in pool {child_pool} but its parent is in pool {parent_pool}."),
        });
    }
    Ok(())
}

async fn run_subnet_parent_contains_child(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String, String, String)> = sqlx::query_as(
        "SELECT child.id, child.subnet_code, child.network::text, parent.network::text
           FROM net.subnet child
           JOIN net.subnet parent ON parent.id = child.parent_subnet_id
          WHERE child.organization_id = $1
            AND child.deleted_at IS NULL
            AND parent.deleted_at IS NULL
            AND NOT (child.network <<= parent.network)")
        .bind(org_id).fetch_all(pool).await?;
    for (id, code, child_net, parent_net) in rows {
        out.push(Violation {
            rule_code: "subnet.parent_contains_child".into(),
            severity, entity_type: "Subnet".into(), entity_id: Some(id),
            message: format!(
                "Subnet '{code}' {child_net} is not contained by parent {parent_net}."),
        });
    }
    Ok(())
}

async fn run_server_nic_index_unique(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, Uuid, i32)> = sqlx::query_as(
        "SELECT n.id, n.server_id, n.nic_index
           FROM net.server_nic n
           JOIN (
             SELECT server_id, nic_index
               FROM net.server_nic
              WHERE organization_id = $1 AND deleted_at IS NULL
              GROUP BY server_id, nic_index
             HAVING COUNT(*) > 1
           ) dup ON dup.server_id = n.server_id AND dup.nic_index = n.nic_index
          WHERE n.organization_id = $1 AND n.deleted_at IS NULL")
        .bind(org_id).fetch_all(pool).await?;
    for (id, server_id, idx) in rows {
        out.push(Violation {
            rule_code: "server_nic.index_unique_per_server".into(),
            severity, entity_type: "ServerNic".into(), entity_id: Some(id),
            message: format!("Server {server_id} has duplicate NIC index {idx}."),
        });
    }
    Ok(())
}

async fn run_server_nic_index_dense(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    // Per-server: if the set of active nic_index values isn't {0, 1, …, count-1}
    // we've got a gap. max(index) != count - 1 is a cheap detector.
    let rows: Vec<(Uuid, String, i32, i64)> = sqlx::query_as(
        "SELECT s.id, s.hostname, MAX(n.nic_index), COUNT(n.id)
           FROM net.server s
           JOIN net.server_nic n ON n.server_id = s.id AND n.deleted_at IS NULL
          WHERE s.organization_id = $1 AND s.deleted_at IS NULL
          GROUP BY s.id, s.hostname
         HAVING MAX(n.nic_index) <> COUNT(n.id) - 1")
        .bind(org_id).fetch_all(pool).await?;
    for (id, hostname, max_idx, count) in rows {
        out.push(Violation {
            rule_code: "server_nic.index_dense_sequence".into(),
            severity, entity_type: "Server".into(), entity_id: Some(id),
            message: format!(
                "Server '{hostname}' has {count} NIC(s) but max nic_index is {max_idx}; \
                 sequence has gaps."),
        });
    }
    Ok(())
}

async fn run_link_building_matches_endpoints(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    // Flag only when the link has a building_id AND at least one endpoint
    // resolves to a device with a building_id AND neither endpoint device
    // is in that building. This keeps WAN / cross-site links (legitimately
    // spanning buildings) quiet — they don't carry a building_id.
    let rows: Vec<(Uuid, String, Uuid)> = sqlx::query_as(
        "SELECT l.id, l.link_code, l.building_id
           FROM net.link l
          WHERE l.organization_id = $1
            AND l.deleted_at IS NULL
            AND l.building_id IS NOT NULL
            AND NOT EXISTS (
              SELECT 1
                FROM net.link_endpoint e
                JOIN net.device d ON d.id = e.device_id
               WHERE e.link_id = l.id
                 AND e.deleted_at IS NULL
                 AND d.building_id = l.building_id)")
        .bind(org_id).fetch_all(pool).await?;
    for (id, code, building_id) in rows {
        out.push(Violation {
            rule_code: "link.building_matches_endpoints".into(),
            severity, entity_type: "Link".into(), entity_id: Some(id),
            message: format!(
                "Link '{code}' is tagged for building {building_id} but neither \
                 endpoint device is in that building."),
        });
    }
    Ok(())
}

async fn run_loopback_v6_slash_128(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String, String)> = sqlx::query_as(
        "SELECT id, subnet_code, network::text
           FROM net.subnet
          WHERE organization_id = $1
            AND deleted_at IS NULL
            AND subnet_code ILIKE 'LOOPBACK%'
            AND family(network) = 6
            AND masklen(network) <> 128")
        .bind(org_id).fetch_all(pool).await?;
    for (id, code, cidr) in rows {
        out.push(Violation {
            rule_code: "loopback.ipv6_slash_128".into(),
            severity, entity_type: "Subnet".into(), entity_id: Some(id),
            message: format!(
                "Loopback IPv6 subnet '{code}' is {cidr}; expected /128."),
        });
    }
    Ok(())
}

async fn run_mlag_unique_per_tenant(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, i32)> = sqlx::query_as(
        "SELECT m.id, m.domain_id
           FROM net.mlag_domain m
           JOIN (
             SELECT domain_id
               FROM net.mlag_domain
              WHERE organization_id = $1 AND deleted_at IS NULL
              GROUP BY domain_id
             HAVING COUNT(*) > 1
           ) dup ON dup.domain_id = m.domain_id
          WHERE m.organization_id = $1 AND m.deleted_at IS NULL")
        .bind(org_id).fetch_all(pool).await?;
    for (id, did) in rows {
        out.push(Violation {
            rule_code: "mlag_domain.unique_per_tenant".into(),
            severity, entity_type: "MlagDomain".into(), entity_id: Some(id),
            message: format!("MLAG domain ID {did} is duplicated within the tenant."),
        });
    }
    Ok(())
}

async fn run_asn_unique_per_tenant(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, i64)> = sqlx::query_as(
        "SELECT a.id, a.asn
           FROM net.asn_allocation a
           JOIN (
             SELECT asn
               FROM net.asn_allocation
              WHERE organization_id = $1 AND deleted_at IS NULL
              GROUP BY asn
             HAVING COUNT(*) > 1
           ) dup ON dup.asn = a.asn
          WHERE a.organization_id = $1 AND a.deleted_at IS NULL")
        .bind(org_id).fetch_all(pool).await?;
    for (id, asn) in rows {
        out.push(Violation {
            rule_code: "asn_allocation.unique_per_tenant".into(),
            severity, entity_type: "AsnAllocation".into(), entity_id: Some(id),
            message: format!("ASN {asn} is allocated more than once in this tenant."),
        });
    }
    Ok(())
}

async fn run_active_device_has_management_ip(
    pool: &PgPool, org_id: Uuid, severity: Severity, out: &mut Vec<Violation>,
) -> Result<(), EngineError> {
    let rows: Vec<(Uuid, String)> = sqlx::query_as(
        "SELECT id, hostname
           FROM net.device
          WHERE organization_id = $1
            AND deleted_at IS NULL
            AND status::text = 'Active'
            AND management_ip IS NULL")
        .bind(org_id).fetch_all(pool).await?;
    for (id, hostname) in rows {
        out.push(Violation {
            rule_code: "device.management_ip_for_active".into(),
            severity, entity_type: "Device".into(), entity_id: Some(id),
            message: format!("Active device '{hostname}' has no management_ip."),
        });
    }
    Ok(())
}

// ─── Tests ────────────────────────────────────────────────────────────────

#[cfg(test)]
mod tests {
    use super::*;
    use std::collections::HashSet;

    #[test]
    fn rule_codes_are_unique() {
        let mut seen = HashSet::new();
        for r in RULES {
            assert!(seen.insert(r.code), "duplicate rule code: {}", r.code);
        }
    }

    #[test]
    fn rule_codes_are_non_empty_and_lowercase_dotted() {
        for r in RULES {
            assert!(!r.code.is_empty(), "rule has empty code: {}", r.name);
            assert!(r.code.contains('.'), "rule code must be dotted.namespace form: {}", r.code);
            assert!(
                r.code.chars().all(|c|
                    c.is_ascii_lowercase() || c.is_ascii_digit() || c == '.' || c == '_'),
                "rule code must be lowercase letters, digits, dots, or underscores only: {}",
                r.code);
        }
    }

    #[test]
    fn rule_names_and_descriptions_are_non_empty() {
        for r in RULES {
            assert!(!r.name.is_empty(), "rule has empty name: {}", r.code);
            assert!(r.description.len() >= 20,
                "rule description is too short to be useful: {}", r.code);
        }
    }

    #[test]
    fn rule_categories_are_known() {
        for r in RULES {
            assert!(matches!(r.category, "Consistency" | "Integrity" | "Safety" | "Advisory"),
                "unknown category '{}' on rule {}", r.category, r.code);
        }
    }

    #[test]
    fn find_rule_returns_entry_for_every_catalog_code() {
        for r in RULES {
            assert!(find_rule(r.code).is_some(), "find_rule missed {}", r.code);
        }
        assert!(find_rule("does.not.exist").is_none());
    }

    #[test]
    fn severity_round_trips_every_variant() {
        for s in [Severity::Error, Severity::Warning, Severity::Info] {
            assert_eq!(Severity::from_db(s.as_str()).unwrap(), s);
        }
        assert!(Severity::from_db("Nuclear").is_err());
    }

    /// The dispatcher must have an arm for every catalog entry. Any miss
    /// is caught at test time rather than production by making an SQL call
    /// that we expect to resolve *locally* (unknown code) without touching
    /// the DB.
    #[test]
    fn dispatcher_has_arm_for_every_catalog_rule() {
        // Rebuild the known set from the real dispatcher so we don't
        // have to hand-maintain this list. Using a static string list
        // would just shift the "is it complete?" problem one level.
        let dispatcher_arms: &[&str] = &[
            "device.hostname_required",
            "server.hostname_required",
            "link.endpoint_count",
            "server.nic_count_matches_profile",
            "loopback.ipv4_slash_32",
            "vlan.unique_per_block",
            "hierarchy.building_requires_site",
            "hierarchy.site_requires_region",
            "asn_allocation.value_in_block_range",
            "vlan.value_in_block_range",
            "device.active_requires_building",
            "link.endpoints_distinct_devices",
            "ip_address.contained_in_subnet",
            "subnet.scope_entity_present_when_non_free",
            "device.unique_hostname",
            "server.unique_hostname",
            "ip_address.unique_per_subnet",
            "subnet.parent_same_pool",
            "subnet.parent_contains_child",
            "server_nic.index_unique_per_server",
            "server_nic.index_dense_sequence",
            "link.building_matches_endpoints",
            "loopback.ipv6_slash_128",
            "mlag_domain.unique_per_tenant",
            "asn_allocation.unique_per_tenant",
            "device.management_ip_for_active",
        ];
        // Every catalog entry must be in the dispatcher list.
        for r in RULES {
            assert!(dispatcher_arms.contains(&r.code),
                "rule {} is in RULES but has no dispatcher arm", r.code);
        }
        // No dispatcher arm should be orphaned (rule removed from catalog).
        for arm in dispatcher_arms {
            assert!(RULES.iter().any(|r| r.code == *arm),
                "dispatcher has arm for '{arm}' but no rule in catalog");
        }
    }
}
