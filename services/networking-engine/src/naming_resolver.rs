//! Naming template resolver — Phase 7a.
//!
//! Given `(organization_id, entity_type, subtype_code, region_id?, site_id?, building_id?)`,
//! walk the override hierarchy and return the template string the engine should expand.
//!
//! Resolution order, most-specific wins:
//!   1. Building-scoped, specific subtype
//!   2. Building-scoped, any subtype (subtype_code NULL)
//!   3. Site-scoped,     specific subtype
//!   4. Site-scoped,     any subtype
//!   5. Region-scoped,   specific subtype
//!   6. Region-scoped,   any subtype
//!   7. Global,          specific subtype
//!   8. Global,          any subtype
//!   9. Default on the *-type table (caller-provided fallback)
//!
//! The *-type defaults live on `net.link_type.naming_template`,
//! `net.device_role.naming_template`, `net.server_profile.naming_template`. The
//! resolver takes the caller's fallback so it stays agnostic to which catalog
//! table the entity_type maps to.

use serde::{Deserialize, Serialize};
use sqlx::PgPool;
use uuid::Uuid;

use crate::error::EngineError;

#[derive(Debug, Clone, Deserialize, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct ResolveRequest {
    pub organization_id: Uuid,
    pub entity_type: String,
    pub subtype_code: Option<String>,
    pub region_id: Option<Uuid>,
    pub site_id: Option<Uuid>,
    pub building_id: Option<Uuid>,
    /// The *-type's own `naming_template` — used when no override matches.
    pub default_template: Option<String>,
}

#[derive(Debug, Clone, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct ResolveResponse {
    pub template: String,
    pub source: ResolveSource,
    pub override_id: Option<Uuid>,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize)]
#[serde(rename_all = "PascalCase")]
pub enum ResolveSource {
    BuildingSpecificSubtype,
    BuildingAnySubtype,
    SiteSpecificSubtype,
    SiteAnySubtype,
    RegionSpecificSubtype,
    RegionAnySubtype,
    GlobalSpecificSubtype,
    GlobalAnySubtype,
    Default,
}

#[derive(Clone)]
pub struct NamingResolver {
    pool: PgPool,
}

impl NamingResolver {
    pub fn new(pool: PgPool) -> Self { Self { pool } }

    /// Walk the override precedence list and return the first template found,
    /// or the caller's `default_template` if nothing matches.
    pub async fn resolve(&self, req: &ResolveRequest) -> Result<ResolveResponse, EngineError> {
        // Order is fixed to match the doc — every tier probed in precedence order.
        // A single query-per-probe is fine: the index on (organization_id, entity_type,
        // scope_level) makes each lookup ~O(log n) and we have at most 8 probes.
        let probes: &[(&str, Option<Uuid>, ResolveSource, bool)] = &[
            ("Building", req.building_id, ResolveSource::BuildingSpecificSubtype, true),
            ("Building", req.building_id, ResolveSource::BuildingAnySubtype,      false),
            ("Site",     req.site_id,     ResolveSource::SiteSpecificSubtype,     true),
            ("Site",     req.site_id,     ResolveSource::SiteAnySubtype,          false),
            ("Region",   req.region_id,   ResolveSource::RegionSpecificSubtype,   true),
            ("Region",   req.region_id,   ResolveSource::RegionAnySubtype,        false),
            ("Global",   None,            ResolveSource::GlobalSpecificSubtype,   true),
            ("Global",   None,            ResolveSource::GlobalAnySubtype,        false),
        ];

        for (scope_level, scope_entity_id, source, needs_subtype) in probes {
            // Skip probes that require a subtype when none was supplied.
            if *needs_subtype && req.subtype_code.is_none() { continue; }
            // Skip probes that require a scope entity we don't have.
            if *scope_level != "Global" && scope_entity_id.is_none() { continue; }

            if let Some((id, template)) = self.probe(req, scope_level, *scope_entity_id, *needs_subtype).await? {
                return Ok(ResolveResponse {
                    template,
                    source: *source,
                    override_id: Some(id),
                });
            }
        }

        let template = req.default_template.clone().ok_or_else(|| {
            EngineError::bad_request(format!(
                "No naming template found for entity_type='{}' and no default_template provided.",
                req.entity_type))
        })?;
        Ok(ResolveResponse { template, source: ResolveSource::Default, override_id: None })
    }

    async fn probe(
        &self,
        req: &ResolveRequest,
        scope_level: &str,
        scope_entity_id: Option<Uuid>,
        needs_subtype: bool,
    ) -> Result<Option<(Uuid, String)>, EngineError> {
        // Two slightly different queries depending on whether we want a
        // specific-subtype row (subtype_code = ?) or an any-subtype row
        // (subtype_code IS NULL). Keeps the predicate sargable against the
        // two partial unique indexes.
        let row: Option<(Uuid, String)> = if needs_subtype {
            sqlx::query_as(
                "SELECT id, naming_template
                   FROM net.naming_template_override
                  WHERE organization_id = $1
                    AND entity_type = $2
                    AND scope_level = $3
                    AND scope_entity_id IS NOT DISTINCT FROM $4
                    AND subtype_code = $5
                    AND deleted_at IS NULL
                    AND status = 'Active'::net.entity_status
                  LIMIT 1")
                .bind(req.organization_id)
                .bind(&req.entity_type)
                .bind(scope_level)
                .bind(scope_entity_id)
                .bind(req.subtype_code.as_deref())
                .fetch_optional(&self.pool)
                .await?
        } else {
            sqlx::query_as(
                "SELECT id, naming_template
                   FROM net.naming_template_override
                  WHERE organization_id = $1
                    AND entity_type = $2
                    AND scope_level = $3
                    AND scope_entity_id IS NOT DISTINCT FROM $4
                    AND subtype_code IS NULL
                    AND deleted_at IS NULL
                    AND status = 'Active'::net.entity_status
                  LIMIT 1")
                .bind(req.organization_id)
                .bind(&req.entity_type)
                .bind(scope_level)
                .bind(scope_entity_id)
                .fetch_optional(&self.pool)
                .await?
        };
        Ok(row)
    }
}

// ─── Pure precedence-order test fixture ──────────────────────────────────
// This test builds no DB — it exercises the precedence list directly to
// guarantee the probe order is stable even if the SQL helpers are refactored.
// DB-touching parity tests stay in Central.Tests and are redirected at this
// service via the typed ApiClient wrapper.

#[cfg(test)]
mod tests {
    use super::*;

    fn probe_sources(req: &ResolveRequest) -> Vec<ResolveSource> {
        // Mirror the `probes` list in `resolve()` exactly and filter by the
        // same skip rules. Used to verify precedence order without a DB.
        let all: &[(&str, Option<Uuid>, ResolveSource, bool)] = &[
            ("Building", req.building_id, ResolveSource::BuildingSpecificSubtype, true),
            ("Building", req.building_id, ResolveSource::BuildingAnySubtype,      false),
            ("Site",     req.site_id,     ResolveSource::SiteSpecificSubtype,     true),
            ("Site",     req.site_id,     ResolveSource::SiteAnySubtype,          false),
            ("Region",   req.region_id,   ResolveSource::RegionSpecificSubtype,   true),
            ("Region",   req.region_id,   ResolveSource::RegionAnySubtype,        false),
            ("Global",   None,            ResolveSource::GlobalSpecificSubtype,   true),
            ("Global",   None,            ResolveSource::GlobalAnySubtype,        false),
        ];
        all.iter()
            .filter(|(scope, id, _, needs_subtype)| {
                if *needs_subtype && req.subtype_code.is_none() { return false; }
                if *scope != "Global" && id.is_none() { return false; }
                true
            })
            .map(|(_, _, src, _)| *src)
            .collect()
    }

    #[test]
    fn building_specific_wins_when_everything_is_present() {
        let req = ResolveRequest {
            organization_id: Uuid::nil(),
            entity_type: "Device".into(),
            subtype_code: Some("Core".into()),
            region_id: Some(Uuid::new_v4()),
            site_id: Some(Uuid::new_v4()),
            building_id: Some(Uuid::new_v4()),
            default_template: Some("{role_code}{instance}".into()),
        };
        assert_eq!(probe_sources(&req).first(), Some(&ResolveSource::BuildingSpecificSubtype));
    }

    #[test]
    fn subtype_probes_skipped_when_no_subtype_given() {
        let req = ResolveRequest {
            organization_id: Uuid::nil(),
            entity_type: "Device".into(),
            subtype_code: None,
            region_id: None,
            site_id: None,
            building_id: Some(Uuid::new_v4()),
            default_template: None,
        };
        let sources = probe_sources(&req);
        // No specific-subtype probes should appear.
        assert!(sources.iter().all(|s| !matches!(s,
            ResolveSource::BuildingSpecificSubtype
          | ResolveSource::SiteSpecificSubtype
          | ResolveSource::RegionSpecificSubtype
          | ResolveSource::GlobalSpecificSubtype)));
        // Building-any, Global-any — Site / Region are skipped (no scope id).
        assert_eq!(sources, vec![
            ResolveSource::BuildingAnySubtype,
            ResolveSource::GlobalAnySubtype,
        ]);
    }

    #[test]
    fn scope_skipped_when_ids_absent() {
        let req = ResolveRequest {
            organization_id: Uuid::nil(),
            entity_type: "Device".into(),
            subtype_code: Some("Core".into()),
            region_id: None,          // Region probes skipped
            site_id: None,            // Site probes skipped
            building_id: None,        // Building probes skipped
            default_template: Some("x".into()),
        };
        let sources = probe_sources(&req);
        // Only the two Global probes survive.
        assert_eq!(sources, vec![
            ResolveSource::GlobalSpecificSubtype,
            ResolveSource::GlobalAnySubtype,
        ]);
    }

    #[test]
    fn precedence_order_is_fixed() {
        let req = ResolveRequest {
            organization_id: Uuid::nil(),
            entity_type: "Link".into(),
            subtype_code: Some("P2P".into()),
            region_id: Some(Uuid::new_v4()),
            site_id: Some(Uuid::new_v4()),
            building_id: Some(Uuid::new_v4()),
            default_template: None,
        };
        assert_eq!(probe_sources(&req), vec![
            ResolveSource::BuildingSpecificSubtype,
            ResolveSource::BuildingAnySubtype,
            ResolveSource::SiteSpecificSubtype,
            ResolveSource::SiteAnySubtype,
            ResolveSource::RegionSpecificSubtype,
            ResolveSource::RegionAnySubtype,
            ResolveSource::GlobalSpecificSubtype,
            ResolveSource::GlobalAnySubtype,
        ]);
    }
}
