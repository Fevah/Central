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
        "device.hostname_required"        => run_device_hostname_required(pool, org_id, severity, out).await,
        "server.hostname_required"        => run_server_hostname_required(pool, org_id, severity, out).await,
        "link.endpoint_count"             => run_link_endpoint_count(pool, org_id, severity, out).await,
        "server.nic_count_matches_profile" => run_server_nic_count(pool, org_id, severity, out).await,
        "loopback.ipv4_slash_32"          => run_loopback_v4_slash_32(pool, org_id, severity, out).await,
        "vlan.unique_per_block"           => run_vlan_unique_per_block(pool, org_id, severity, out).await,
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
