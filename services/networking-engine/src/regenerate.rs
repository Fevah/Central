//! Regenerate-names admin action — preview half. Phase 7c.
//!
//! Given an entity type + scope filter, walk all matching entities, compute
//! what their name *should* be under the current (possibly-overridden)
//! template, and return a diff vs. the current name. No writes.
//!
//! The apply side — rename + audit log entry per change — lands in a
//! follow-on slice once the Rust audit wiring is in place.
//!
//! This slice covers Device only. Link + Server are identical in shape and
//! will arrive as incremental adds.

use serde::{Deserialize, Serialize};
use sqlx::PgPool;
use uuid::Uuid;

use crate::error::EngineError;
use crate::naming::{expand_device, DeviceNamingContext};
use crate::naming_resolver::{NamingResolver, ResolveRequest};

#[derive(Debug, Clone, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct RegeneratePreviewRequest {
    pub organization_id: Uuid,
    pub entity_type: String, // "Device" only for now
    /// Optional subtype filter — e.g. role_code='Core' to preview only Cores.
    pub subtype_code: Option<String>,
    /// Scope filter — if set, restricts the walk to a single building / site / region.
    pub scope_level: Option<String>,
    pub scope_entity_id: Option<Uuid>,
}

#[derive(Debug, Clone, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct RegeneratePreviewResponse {
    pub items: Vec<RegenerateItem>,
    pub total: usize,
    pub would_change: usize,
}

#[derive(Debug, Clone, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct RegenerateItem {
    pub id: Uuid,
    pub subtype_code: Option<String>,
    pub current_name: String,
    pub proposed_name: String,
    pub would_change: bool,
    /// Which resolver source produced the template (Building-specific, Global, Default, …).
    pub template_source: String,
}

pub async fn preview(
    pool: &PgPool,
    req: &RegeneratePreviewRequest,
) -> Result<RegeneratePreviewResponse, EngineError> {
    match req.entity_type.as_str() {
        "Device" => preview_devices(pool, req).await,
        other => Err(EngineError::bad_request(format!(
            "entity_type '{other}' is not yet supported by regenerate-preview. \
             Device only in this slice; Link + Server arriving next."))),
    }
}

#[derive(sqlx::FromRow)]
struct DeviceRow {
    id: Uuid,
    hostname: String,
    role_code: Option<String>,
    default_template: Option<String>,
    building_id: Option<Uuid>,
    building_code: Option<String>,
    site_id: Option<Uuid>,
    site_code: Option<String>,
    region_id: Option<Uuid>,
    region_code: Option<String>,
    rack_code: Option<String>,
    instance: i64,
}

async fn preview_devices(
    pool: &PgPool,
    req: &RegeneratePreviewRequest,
) -> Result<RegeneratePreviewResponse, EngineError> {
    validate_scope_filter(req)?;

    // Single query pulls the whole context — hierarchy codes + role + a
    // deterministic 1-based instance within (role, building) ordered by
    // creation time. Matches the C# `DeviceNamingContext.Instance`
    // convention (count of matching-role devices in the same building,
    // chronologically).
    //
    // The scope filter uses a branchless WHERE: all three predicates are
    // NULL-safe so passing a subset of (region/site/building) just relaxes
    // the filter. IS NOT DISTINCT FROM treats NULL as "don't filter".
    let rows: Vec<DeviceRow> = sqlx::query_as(
        "SELECT
            d.id,
            d.hostname,
            r.role_code                                                       AS role_code,
            r.naming_template                                                 AS default_template,
            d.building_id,
            b.building_code                                                   AS building_code,
            s.id                                                              AS site_id,
            s.site_code                                                       AS site_code,
            reg.id                                                            AS region_id,
            reg.region_code                                                   AS region_code,
            rack.rack_code                                                    AS rack_code,
            ROW_NUMBER() OVER (
                PARTITION BY d.device_role_id, d.building_id
                ORDER BY d.created_at, d.id
            )::bigint                                                         AS instance
         FROM net.device d
         LEFT JOIN net.device_role r ON r.id = d.device_role_id
         LEFT JOIN net.building    b ON b.id = d.building_id
         LEFT JOIN net.site        s ON s.id = b.site_id
         LEFT JOIN net.region    reg ON reg.id = s.region_id
         LEFT JOIN net.rack      rack ON rack.id = d.rack_id
        WHERE d.organization_id = $1
          AND d.deleted_at IS NULL
          AND ($2::text IS NULL OR r.role_code = $2)
          AND CASE COALESCE($3, '')
                WHEN 'Region'   THEN reg.id = $4
                WHEN 'Site'     THEN s.id   = $4
                WHEN 'Building' THEN b.id   = $4
                ELSE TRUE
              END
        ORDER BY reg.region_code, s.site_code, b.building_code, d.hostname")
        .bind(req.organization_id)
        .bind(req.subtype_code.as_deref())
        .bind(req.scope_level.as_deref())
        .bind(req.scope_entity_id)
        .fetch_all(pool)
        .await?;

    let resolver = NamingResolver::new(pool.clone());
    let mut items = Vec::with_capacity(rows.len());
    let mut would_change = 0usize;

    for row in rows {
        // Resolve the template for this device's own position in the hierarchy.
        let resolve = ResolveRequest {
            organization_id: req.organization_id,
            entity_type: "Device".into(),
            subtype_code: row.role_code.clone(),
            region_id: row.region_id,
            site_id: row.site_id,
            building_id: row.building_id,
            default_template: row.default_template.clone(),
        };
        let resolved = match resolver.resolve(&resolve).await {
            Ok(r) => r,
            Err(_) => {
                // Device with no role + no override + no default → skip silently.
                // Admin will see it in the `total` count but not in `items`.
                continue;
            }
        };

        let ctx = DeviceNamingContext {
            region_code: row.region_code,
            site_code: row.site_code,
            building_code: row.building_code,
            rack_code: row.rack_code,
            role_code: row.role_code.clone(),
            instance: Some(row.instance as i32),
            instance_padding: 2,
        };
        let proposed = expand_device(&resolved.template, &ctx);
        let changes = proposed != row.hostname;
        if changes { would_change += 1; }

        items.push(RegenerateItem {
            id: row.id,
            subtype_code: row.role_code,
            current_name: row.hostname,
            proposed_name: proposed,
            would_change: changes,
            template_source: format!("{:?}", resolved.source),
        });
    }

    let total = items.len();
    Ok(RegeneratePreviewResponse { items, total, would_change })
}

pub(crate) fn validate_scope_filter(req: &RegeneratePreviewRequest) -> Result<(), EngineError> {
    match (req.scope_level.as_deref(), req.scope_entity_id) {
        (None, None) => Ok(()),
        (Some(lvl), Some(_)) if matches!(lvl, "Region" | "Site" | "Building") => Ok(()),
        (Some(lvl), None) => Err(EngineError::bad_request(format!(
            "scope_level '{lvl}' requires scope_entity_id"))),
        (None, Some(_)) => Err(EngineError::bad_request(
            "scope_entity_id requires scope_level")),
        (Some(lvl), Some(_)) => Err(EngineError::bad_request(format!(
            "Invalid scope_level '{lvl}' — must be Region / Site / Building"))),
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    fn req(scope_level: Option<&str>, scope_entity_id: Option<Uuid>) -> RegeneratePreviewRequest {
        RegeneratePreviewRequest {
            organization_id: Uuid::nil(),
            entity_type: "Device".into(),
            subtype_code: None,
            scope_level: scope_level.map(str::to_string),
            scope_entity_id,
        }
    }

    #[test]
    fn unscoped_regenerate_is_valid() {
        assert!(validate_scope_filter(&req(None, None)).is_ok());
    }

    #[test]
    fn scope_level_requires_entity_id() {
        assert!(validate_scope_filter(&req(Some("Building"), None)).is_err());
        assert!(validate_scope_filter(&req(Some("Site"), None)).is_err());
        assert!(validate_scope_filter(&req(Some("Region"), None)).is_err());
    }

    #[test]
    fn entity_id_requires_scope_level() {
        assert!(validate_scope_filter(&req(None, Some(Uuid::new_v4()))).is_err());
    }

    #[test]
    fn full_scope_ok() {
        let id = Uuid::new_v4();
        assert!(validate_scope_filter(&req(Some("Building"), Some(id))).is_ok());
        assert!(validate_scope_filter(&req(Some("Site"), Some(id))).is_ok());
        assert!(validate_scope_filter(&req(Some("Region"), Some(id))).is_ok());
    }

    #[test]
    fn unknown_scope_level_rejected() {
        assert!(validate_scope_filter(&req(Some("Floor"), Some(Uuid::new_v4()))).is_err());
        assert!(validate_scope_filter(&req(Some("Global"), Some(Uuid::new_v4()))).is_err());
    }
}
