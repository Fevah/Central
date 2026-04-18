//! CLI flavor catalog + per-tenant enable/default state.
//!
//! The customer (Immunocore) runs FS / PicOS on every switch in its
//! fabric. Other prospective tenants run Cisco / Arista / Juniper /
//! FRR. Rather than encoding PicOS specifics throughout the config
//! generator, each flavor gets a code + metadata + (eventually) a
//! [`crate::config_gen::Renderer`] implementation.
//!
//! The catalog lives here in code — not in the DB — so a flavor being
//! retired never orphans live config generation, and adding a flavor
//! is a compile-time check against the dispatcher (same pattern as
//! validation rules).
//!
//! Per-tenant enable + default flavor lives in `net.tenant_cli_flavor`
//! and is managed by this module. REST surface mirrors validation
//! rules — list merged state, toggle enable, toggle is_default.

use chrono::{DateTime, Utc};
use serde::{Deserialize, Serialize};
use sqlx::PgPool;
use uuid::Uuid;

use crate::error::EngineError;

// ─── Catalog ──────────────────────────────────────────────────────────────

#[derive(Debug, Copy, Clone, Serialize, PartialEq, Eq)]
#[serde(rename_all = "camelCase")]
pub struct FlavorMeta {
    pub code: &'static str,
    pub display_name: &'static str,
    pub vendor: &'static str,
    pub description: &'static str,
    pub status: &'static str,          // "Ga" / "Beta" / "Stub"
    pub default_enabled: bool,
}

/// Canonical flavor catalog. Ordered by "how real is the renderer" —
/// PicOS is production-grade (Immunocore's fabric), the others are
/// metadata stubs today. Adding a flavor = one entry here + (eventually)
/// a branch in `dispatch_renderer`.
pub const FLAVORS: &[FlavorMeta] = &[
    FlavorMeta {
        code: "PicOS",
        display_name: "FS PicOS 4.6",
        vendor: "FS",
        description: "FS N-series / PicOS 4.6 (set-style CLI). Immunocore's \
                      production stack. Full renderer implemented.",
        status: "Ga",
        default_enabled: true,
    },
    FlavorMeta {
        code: "CiscoNxos",
        display_name: "Cisco NX-OS",
        vendor: "Cisco",
        description: "Cisco Nexus switches (NX-OS). Renderer stub — metadata \
                      only, falls through to 'not implemented' on render.",
        status: "Stub",
        default_enabled: false,
    },
    FlavorMeta {
        code: "CiscoIos",
        display_name: "Cisco IOS / IOS-XE",
        vendor: "Cisco",
        description: "Cisco Catalyst / ISR / CSR platforms. Stub.",
        status: "Stub",
        default_enabled: false,
    },
    FlavorMeta {
        code: "AristaEos",
        display_name: "Arista EOS",
        vendor: "Arista",
        description: "Arista EOS (BGP-EVPN common). Stub.",
        status: "Stub",
        default_enabled: false,
    },
    FlavorMeta {
        code: "JunosOs",
        display_name: "Juniper Junos",
        vendor: "Juniper",
        description: "Juniper Junos (MX / QFX / SRX). Stub.",
        status: "Stub",
        default_enabled: false,
    },
    FlavorMeta {
        code: "FrrouterOs",
        display_name: "FRRouting",
        vendor: "FRR",
        description: "FRRouting on Linux. Stub.",
        status: "Stub",
        default_enabled: false,
    },
];

pub fn find_flavor(code: &str) -> Option<&'static FlavorMeta> {
    FLAVORS.iter().find(|f| f.code == code)
}

// ─── Resolved per-tenant state ────────────────────────────────────────────

#[derive(Debug, Clone, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct ResolvedFlavor {
    #[serde(flatten)]
    pub meta: FlavorMeta,
    pub effective_enabled: bool,
    pub is_default: bool,
    pub has_tenant_row: bool,
    pub updated_at: Option<DateTime<Utc>>,
    pub notes: Option<String>,
}

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct ListFlavorsQuery { pub organization_id: Uuid }

pub async fn list_flavors(
    pool: &PgPool,
    q: &ListFlavorsQuery,
) -> Result<Vec<ResolvedFlavor>, EngineError> {
    let rows: Vec<(String, Option<bool>, bool, DateTime<Utc>, Option<String>)> = sqlx::query_as(
        "SELECT flavor_code, enabled, is_default, updated_at, notes
           FROM net.tenant_cli_flavor
          WHERE organization_id = $1")
        .bind(q.organization_id)
        .fetch_all(pool)
        .await?;
    let mut by_code: std::collections::HashMap<String, (Option<bool>, bool, DateTime<Utc>, Option<String>)>
        = rows.into_iter().map(|(c, e, d, u, n)| (c, (e, d, u, n))).collect();

    let mut out = Vec::with_capacity(FLAVORS.len());
    for meta in FLAVORS {
        let row = by_code.remove(meta.code);
        let has_row = row.is_some();
        let (enabled_override, is_default, updated_at, notes) =
            row.map(|(e, d, u, n)| (e, d, Some(u), n))
               .unwrap_or((None, false, None, None));
        out.push(ResolvedFlavor {
            meta: *meta,
            effective_enabled: enabled_override.unwrap_or(meta.default_enabled),
            is_default,
            has_tenant_row: has_row,
            updated_at,
            notes,
        });
    }
    Ok(out)
}

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct SetFlavorConfigBody {
    pub enabled: Option<bool>,
    pub is_default: Option<bool>,
    pub notes: Option<String>,
}

/// Upsert a tenant's config for one flavor. Also enforces
/// "one default per tenant" — setting is_default=true clears the flag
/// on any other row for the same tenant.
pub async fn set_flavor_config(
    pool: &PgPool,
    org_id: Uuid,
    flavor_code: &str,
    body: &SetFlavorConfigBody,
    user_id: Option<i32>,
) -> Result<(), EngineError> {
    if find_flavor(flavor_code).is_none() {
        return Err(EngineError::bad_request(format!(
            "Unknown flavor code '{flavor_code}'. Check /api/net/cli-flavors for the catalog.")));
    }

    let mut tx = pool.begin().await?;

    // If the caller is setting is_default=true, clear it elsewhere
    // first. The unique index would reject the update otherwise — do
    // it explicitly so we get a clean "replaced" behaviour rather than
    // a constraint violation.
    if body.is_default == Some(true) {
        sqlx::query(
            "UPDATE net.tenant_cli_flavor
                SET is_default = false, updated_at = now(), updated_by = $2,
                    version = version + 1
              WHERE organization_id = $1 AND is_default = true
                AND flavor_code <> $3")
            .bind(org_id)
            .bind(user_id)
            .bind(flavor_code)
            .execute(&mut *tx)
            .await?;
    }

    sqlx::query(
        "INSERT INTO net.tenant_cli_flavor
            (organization_id, flavor_code, enabled, is_default, notes,
             created_by, updated_by)
         VALUES ($1, $2, $3, COALESCE($4, false), $5, $6, $6)
         ON CONFLICT (organization_id, flavor_code) DO UPDATE
           SET enabled    = EXCLUDED.enabled,
               is_default = COALESCE(EXCLUDED.is_default, net.tenant_cli_flavor.is_default),
               notes      = EXCLUDED.notes,
               updated_at = now(),
               updated_by = EXCLUDED.updated_by,
               version    = net.tenant_cli_flavor.version + 1")
        .bind(org_id)
        .bind(flavor_code)
        .bind(body.enabled)
        .bind(body.is_default)
        .bind(body.notes.as_deref())
        .bind(user_id)
        .execute(&mut *tx)
        .await?;
    tx.commit().await?;
    Ok(())
}

/// Resolve the flavor a specific device should render in. Precedence:
/// 1. TODO: device-level cli_flavor_code column (future slice)
/// 2. TODO: device_role.cli_flavor_code (future slice)
/// 3. Tenant default (is_default=true in net.tenant_cli_flavor)
/// 4. Hardcoded "PicOS" fallback (matches the customer's stack)
///
/// Current implementation only handles tiers 3-4 — per-device and
/// per-role overrides ship when there's a concrete test tenant that
/// needs them.
pub async fn resolve_for_device(
    pool: &PgPool,
    org_id: Uuid,
    _device_id: Uuid,
) -> Result<&'static FlavorMeta, EngineError> {
    let code: Option<String> = sqlx::query_scalar(
        "SELECT flavor_code FROM net.tenant_cli_flavor
          WHERE organization_id = $1 AND is_default = true
          LIMIT 1")
        .bind(org_id)
        .fetch_optional(pool)
        .await?;
    match code {
        Some(c) => Ok(find_flavor(&c).unwrap_or_else(|| find_flavor("PicOS").unwrap())),
        None    => Ok(find_flavor("PicOS").unwrap()),
    }
}

// ─── Unit tests ──────────────────────────────────────────────────────────

#[cfg(test)]
mod tests {
    use super::*;
    use std::collections::HashSet;

    #[test]
    fn flavor_codes_are_unique() {
        let mut seen = HashSet::new();
        for f in FLAVORS {
            assert!(seen.insert(f.code), "duplicate flavor code: {}", f.code);
        }
    }

    #[test]
    fn picos_is_the_first_flavor_and_enabled_by_default() {
        let first = FLAVORS.first().expect("at least one flavor");
        assert_eq!(first.code, "PicOS", "PicOS must be catalog position 0 — customer's production stack");
        assert!(first.default_enabled, "PicOS defaults enabled");
    }

    #[test]
    fn only_one_ga_flavor_today() {
        // Sanity — we claim PicOS is production; others are stubs. When
        // a second flavor gets a real renderer this test updates.
        let ga_count = FLAVORS.iter().filter(|f| f.status == "Ga").count();
        assert_eq!(ga_count, 1, "exactly one Ga flavor today — PicOS");
    }

    #[test]
    fn stubs_default_disabled() {
        // Stub flavors shouldn't auto-enable — admins opt in explicitly
        // so they don't see "your Cisco config is broken" from a
        // renderer that hasn't been written yet.
        for f in FLAVORS {
            if f.status == "Stub" {
                assert!(!f.default_enabled, "stub flavor '{}' must default disabled", f.code);
            }
        }
    }

    #[test]
    fn find_flavor_resolves_every_catalog_entry() {
        for f in FLAVORS {
            assert!(find_flavor(f.code).is_some(), "find_flavor missed {}", f.code);
        }
        assert!(find_flavor("NosuchFlavor").is_none());
    }

    #[test]
    fn vendors_are_recognised() {
        // Sanity on vendor metadata — makes sure new entries don't
        // forget to set it.
        let known_vendors: HashSet<&str> =
            ["FS", "Cisco", "Arista", "Juniper", "FRR"].into_iter().collect();
        for f in FLAVORS {
            assert!(known_vendors.contains(f.vendor),
                "unknown vendor '{}' on flavor {}", f.vendor, f.code);
        }
    }
}
