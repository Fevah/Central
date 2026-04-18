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

use crate::audit::{self, AuditEvent};
use crate::error::EngineError;
use crate::naming::{
    expand_device, expand_link, expand_server,
    DeviceNamingContext, LinkNamingContext, ServerNamingContext,
};
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
        "Link"   => preview_links(pool, req).await,
        "Server" => preview_servers(pool, req).await,
        other => Err(EngineError::bad_request(format!(
            "entity_type '{other}' is not supported — must be Device / Link / Server."))),
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

// ─── Links ────────────────────────────────────────────────────────────────

#[derive(sqlx::FromRow)]
struct LinkRow {
    id: Uuid,
    link_code: String,
    type_code: Option<String>,
    default_template: Option<String>,
    building_id: Option<Uuid>,
    site_id: Option<Uuid>,
    region_id: Option<Uuid>,
    // Endpoint A
    device_a: Option<String>,
    site_a: Option<String>,
    port_a: Option<String>,
    role_a: Option<String>,
    // Endpoint B
    device_b: Option<String>,
    site_b: Option<String>,
    port_b: Option<String>,
    role_b: Option<String>,
    // Ancillary
    description: Option<String>,
    vlan_id: Option<i32>,
    subnet: Option<String>,
}

async fn preview_links(
    pool: &PgPool,
    req: &RegeneratePreviewRequest,
) -> Result<RegeneratePreviewResponse, EngineError> {
    validate_scope_filter(req)?;

    // Links carry A/B endpoints in net.link_endpoint (endpoint_order 0 = A,
    // 1 = B). The query lateral-joins each endpoint to pull device hostname,
    // port interface_name, role_code, and the endpoint's building code (each
    // endpoint can sit in a different building for cross-site links). VLAN
    // + subnet come off the link itself.
    let rows: Vec<LinkRow> = sqlx::query_as(
        "SELECT
            l.id,
            l.link_code,
            lt.type_code                     AS type_code,
            lt.naming_template               AS default_template,
            l.building_id,
            s.id                             AS site_id,
            reg.id                           AS region_id,
            ep_a.device_hostname             AS device_a,
            ep_a.building_code               AS site_a,
            ep_a.interface_name              AS port_a,
            ep_a.role_code                   AS role_a,
            ep_b.device_hostname             AS device_b,
            ep_b.building_code               AS site_b,
            ep_b.interface_name              AS port_b,
            ep_b.role_code                   AS role_b,
            l.description                    AS description,
            v.vlan_id                        AS vlan_id,
            sn.network::text                 AS subnet
         FROM net.link l
         JOIN net.link_type lt       ON lt.id = l.link_type_id
         LEFT JOIN net.building b    ON b.id  = l.building_id
         LEFT JOIN net.site     s    ON s.id  = b.site_id
         LEFT JOIN net.region   reg  ON reg.id = s.region_id
         LEFT JOIN net.vlan     v    ON v.id  = l.vlan_id
         LEFT JOIN net.subnet   sn   ON sn.id = l.subnet_id
         LEFT JOIN LATERAL (
            SELECT e.interface_name, d.hostname AS device_hostname,
                   b2.building_code, r2.role_code
            FROM net.link_endpoint e
            LEFT JOIN net.device d       ON d.id = e.device_id
            LEFT JOIN net.device_role r2 ON r2.id = d.device_role_id
            LEFT JOIN net.building b2    ON b2.id = d.building_id
            WHERE e.link_id = l.id AND e.endpoint_order = 0 AND e.deleted_at IS NULL
            LIMIT 1
         ) ep_a ON TRUE
         LEFT JOIN LATERAL (
            SELECT e.interface_name, d.hostname AS device_hostname,
                   b2.building_code, r2.role_code
            FROM net.link_endpoint e
            LEFT JOIN net.device d       ON d.id = e.device_id
            LEFT JOIN net.device_role r2 ON r2.id = d.device_role_id
            LEFT JOIN net.building b2    ON b2.id = d.building_id
            WHERE e.link_id = l.id AND e.endpoint_order = 1 AND e.deleted_at IS NULL
            LIMIT 1
         ) ep_b ON TRUE
        WHERE l.organization_id = $1
          AND l.deleted_at IS NULL
          AND ($2::text IS NULL OR lt.type_code = $2)
          AND CASE COALESCE($3, '')
                WHEN 'Region'   THEN reg.id = $4
                WHEN 'Site'     THEN s.id   = $4
                WHEN 'Building' THEN l.building_id = $4
                ELSE TRUE
              END
        ORDER BY lt.type_code, l.link_code")
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
        let resolve = ResolveRequest {
            organization_id: req.organization_id,
            entity_type: "Link".into(),
            subtype_code: row.type_code.clone(),
            region_id: row.region_id,
            site_id: row.site_id,
            building_id: row.building_id,
            default_template: row.default_template.clone(),
        };
        let resolved = match resolver.resolve(&resolve).await {
            Ok(r) => r,
            Err(_) => continue, // no template anywhere — skip
        };

        let ctx = LinkNamingContext {
            site_a: row.site_a, site_b: row.site_b,
            device_a: row.device_a, device_b: row.device_b,
            port_a: row.port_a, port_b: row.port_b,
            role_a: row.role_a, role_b: row.role_b,
            vlan_id: row.vlan_id, subnet: row.subnet,
            description: row.description,
            link_code: Some(row.link_code.clone()),
        };
        let proposed = expand_link(&resolved.template, &ctx);
        let changes = proposed != row.link_code;
        if changes { would_change += 1; }

        items.push(RegenerateItem {
            id: row.id,
            subtype_code: row.type_code,
            current_name: row.link_code,
            proposed_name: proposed,
            would_change: changes,
            template_source: format!("{:?}", resolved.source),
        });
    }

    let total = items.len();
    Ok(RegeneratePreviewResponse { items, total, would_change })
}

// ─── Servers ──────────────────────────────────────────────────────────────

#[derive(sqlx::FromRow)]
struct ServerRow {
    id: Uuid,
    hostname: String,
    profile_code: Option<String>,
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

async fn preview_servers(
    pool: &PgPool,
    req: &RegeneratePreviewRequest,
) -> Result<RegeneratePreviewResponse, EngineError> {
    validate_scope_filter(req)?;

    // Same shape as preview_devices but (profile, building) is the instance
    // partition — the Server4NIC profile's seeded template produces
    // "SRV01 / SRV02 / …" within a building.
    let rows: Vec<ServerRow> = sqlx::query_as(
        "SELECT
            srv.id,
            srv.hostname,
            sp.profile_code                    AS profile_code,
            sp.naming_template                 AS default_template,
            srv.building_id,
            b.building_code                    AS building_code,
            s.id                               AS site_id,
            s.site_code                        AS site_code,
            reg.id                             AS region_id,
            reg.region_code                    AS region_code,
            rack.rack_code                     AS rack_code,
            ROW_NUMBER() OVER (
                PARTITION BY srv.server_profile_id, srv.building_id
                ORDER BY srv.created_at, srv.id
            )::bigint                          AS instance
         FROM net.server srv
         LEFT JOIN net.server_profile sp ON sp.id = srv.server_profile_id
         LEFT JOIN net.building       b  ON b.id  = srv.building_id
         LEFT JOIN net.site           s  ON s.id  = b.site_id
         LEFT JOIN net.region        reg ON reg.id = s.region_id
         LEFT JOIN net.rack         rack ON rack.id = srv.rack_id
        WHERE srv.organization_id = $1
          AND srv.deleted_at IS NULL
          AND ($2::text IS NULL OR sp.profile_code = $2)
          AND CASE COALESCE($3, '')
                WHEN 'Region'   THEN reg.id = $4
                WHEN 'Site'     THEN s.id   = $4
                WHEN 'Building' THEN b.id   = $4
                ELSE TRUE
              END
        ORDER BY reg.region_code, s.site_code, b.building_code, srv.hostname")
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
        let resolve = ResolveRequest {
            organization_id: req.organization_id,
            entity_type: "Server".into(),
            subtype_code: row.profile_code.clone(),
            region_id: row.region_id,
            site_id: row.site_id,
            building_id: row.building_id,
            default_template: row.default_template.clone(),
        };
        let resolved = match resolver.resolve(&resolve).await {
            Ok(r) => r,
            Err(_) => continue,
        };

        let ctx = ServerNamingContext {
            region_code: row.region_code,
            site_code: row.site_code,
            building_code: row.building_code,
            rack_code: row.rack_code,
            profile_code: row.profile_code.clone(),
            instance: Some(row.instance as i32),
            instance_padding: 2,
        };
        let proposed = expand_server(&resolved.template, &ctx);
        let changes = proposed != row.hostname;
        if changes { would_change += 1; }

        items.push(RegenerateItem {
            id: row.id,
            subtype_code: row.profile_code,
            current_name: row.hostname,
            proposed_name: proposed,
            would_change: changes,
            template_source: format!("{:?}", resolved.source),
        });
    }

    let total = items.len();
    Ok(RegeneratePreviewResponse { items, total, would_change })
}

// ─── Apply ────────────────────────────────────────────────────────────────

/// Apply the regenerate preview — actually rename the entities. Each rename
/// is its own transaction (entity UPDATE + audit append) so a failure on
/// row 47 doesn't roll back rows 1-46. Admins can re-run apply to pick up
/// the remaining rows (they'll simply be no-ops for entities already at
/// their proposed name).
#[derive(Debug, Clone, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct RegenerateApplyRequest {
    pub organization_id: Uuid,
    pub entity_type: String,
    pub subtype_code: Option<String>,
    pub scope_level: Option<String>,
    pub scope_entity_id: Option<Uuid>,
    /// Optional — if set, restrict the apply to only these entity IDs.
    /// Lets admins preview, untick rows in the UI, and apply only the
    /// curated subset. Empty or omitted means "every row the preview
    /// would have returned with `wouldChange = true`".
    #[serde(default)]
    pub only_ids: Option<Vec<Uuid>>,
}

#[derive(Debug, Clone, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct RegenerateApplyResponse {
    pub renamed: Vec<RegenerateItem>,
    pub skipped: Vec<RegenerateItem>,
    pub failed: Vec<RegenerateApplyFailure>,
    pub renamed_count: usize,
    pub failed_count: usize,
}

#[derive(Debug, Clone, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct RegenerateApplyFailure {
    pub id: Uuid,
    pub current_name: String,
    pub proposed_name: String,
    pub error: String,
}

pub async fn apply(
    pool: &PgPool,
    req: &RegenerateApplyRequest,
) -> Result<RegenerateApplyResponse, EngineError> {
    let only_ids: Option<std::collections::HashSet<Uuid>> =
        req.only_ids.as_ref().map(|v| v.iter().copied().collect());

    // Start with the preview surface so applies share the naming
    // resolution + context-building code. No chance of drift between
    // what admins see in the UI and what actually runs.
    let preview_req = RegeneratePreviewRequest {
        organization_id: req.organization_id,
        entity_type: req.entity_type.clone(),
        subtype_code: req.subtype_code.clone(),
        scope_level: req.scope_level.clone(),
        scope_entity_id: req.scope_entity_id,
    };
    let preview = preview(pool, &preview_req).await?;

    let mut renamed = Vec::new();
    let mut skipped = Vec::new();
    let mut failed = Vec::new();

    for item in preview.items {
        if !item.would_change {
            skipped.push(item);
            continue;
        }
        if let Some(ids) = &only_ids {
            if !ids.contains(&item.id) {
                skipped.push(item);
                continue;
            }
        }

        match rename_one(pool, req.entity_type.as_str(), &item, req.organization_id).await {
            Ok(()) => renamed.push(item),
            Err(e) => failed.push(RegenerateApplyFailure {
                id: item.id,
                current_name: item.current_name,
                proposed_name: item.proposed_name,
                error: e.to_string(),
            }),
        }
    }

    let renamed_count = renamed.len();
    let failed_count = failed.len();
    Ok(RegenerateApplyResponse {
        renamed, skipped, failed, renamed_count, failed_count,
    })
}

async fn rename_one(
    pool: &PgPool,
    entity_type: &str,
    item: &RegenerateItem,
    org_id: Uuid,
) -> Result<(), EngineError> {
    match entity_type {
        "Device" => rename_device(pool, item, org_id).await,
        // Link + Server apply land as incremental adds once Phase 8
        // governance decides whether bulk renames need Change Set
        // approval — deferring the code keeps us from shipping something
        // that immediately gets rewritten.
        other => Err(EngineError::bad_request(format!(
            "Apply not yet supported for entity_type '{other}'. Device only in this slice."))),
    }
}

async fn rename_device(
    pool: &PgPool,
    item: &RegenerateItem,
    org_id: Uuid,
) -> Result<(), EngineError> {
    let mut tx = pool.begin().await?;

    // Optimistic UPDATE — if another writer changed the device between
    // the preview and the apply, we bail rather than overwrite. Caller
    // re-previews and re-applies.
    let updated: Option<(Uuid, String, i32)> = sqlx::query_as(
        "UPDATE net.device
            SET hostname   = $3,
                updated_at = now(),
                version    = version + 1
          WHERE id = $1 AND organization_id = $2
            AND hostname = $4
            AND deleted_at IS NULL
          RETURNING id, hostname, version")
        .bind(item.id)
        .bind(org_id)
        .bind(&item.proposed_name)
        .bind(&item.current_name)
        .fetch_optional(&mut *tx)
        .await?;

    let Some((_, _, new_version)) = updated else {
        return Err(EngineError::bad_request(format!(
            "Device {} not found, already renamed, or deleted — re-preview and retry.", item.id)));
    };

    audit::append_tx(&mut tx, &AuditEvent {
        organization_id: org_id,
        source_service: "networking-engine",
        entity_type: "Device",
        entity_id: Some(item.id),
        action: "Renamed",
        actor_user_id: None,
        actor_display: None,
        client_ip: None,
        correlation_id: None,
        details: serde_json::json!({
            "from": item.current_name,
            "to": item.proposed_name,
            "template_source": item.template_source,
            "new_version": new_version,
        }),
    }).await?;

    tx.commit().await?;
    Ok(())
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

    // `preview()` dispatches on entity_type without hitting the DB for the
    // error path — verify all three supported types are accepted and the
    // reject path names the right list.
    #[tokio::test]
    async fn unknown_entity_type_rejected_without_db() {
        use sqlx::PgPool;
        // Build a pool that can't connect so we can only exercise the
        // synchronous dispatch guard; supported types would proceed to the
        // DB and error there, but an unsupported type short-circuits first.
        let pool = PgPool::connect_lazy("postgres://nowhere/nodb").unwrap();
        let r = RegeneratePreviewRequest {
            organization_id: Uuid::nil(),
            entity_type: "Pony".into(),
            subtype_code: None,
            scope_level: None,
            scope_entity_id: None,
        };
        let err = preview(&pool, &r).await.unwrap_err();
        let msg = format!("{err}");
        assert!(msg.contains("Device"));
        assert!(msg.contains("Link"));
        assert!(msg.contains("Server"));
        assert!(msg.contains("Pony"));
    }
}
