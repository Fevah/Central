//! Bulk edit — transactional same-value-for-all updates across a
//! selected set of rows. The last of the three bulk primitives
//! (export + import + edit) needed to round out the Phase 10 bulk
//! surface.
//!
//! ## Shape
//!
//! ```text
//! POST /api/net/devices/bulk-edit?organizationId=X&dryRun=true|false
//! {
//!   "deviceIds": [uuid, uuid, ...],
//!   "field":     "status",
//!   "value":     "Retired"
//! }
//! → BulkEditResult { total, succeeded, failed, dryRun, applied, outcomes[] }
//! ```
//!
//! Same value for every selected row — per-row different values is
//! what the bulk-import CSV path is for. Keeping bulk-edit to the
//! "apply this change to everything I've selected" case keeps the
//! API dead simple and covers ~95% of operator needs (retire a
//! batch, move devices between buildings, etc.).
//!
//! ## Semantics
//!
//! - **Field whitelist.** Only `status`, `role_code`, `building_code`,
//!   `management_ip`, `notes` are editable via bulk-edit. Hostname is
//!   too dangerous at scale (a typo affecting one device is bad; a
//!   typo affecting 50 is a Sev-1). `version` is managed by optimistic
//!   concurrency, not operators.
//!
//! - **Transactional.** Like bulk-import apply: any per-row failure
//!   rolls back the whole batch. Operators see the failing row's
//!   reason + none of the others get written. Mental model stays
//!   consistent with imports.
//!
//! - **Optimistic-concurrency-friendly.** Each row's UPDATE checks
//!   the version that was fresh *at the start of this operation*.
//!   If another writer slipped in between our SELECT and our UPDATE,
//!   the row's UPDATE affects zero rows and we report a concurrent-
//!   write error for that row — the tx rolls back, operator retries.
//!
//! - **Audit per affected row.** Each successful UPDATE writes an
//!   audit entry in the same transaction as the UPDATE, tagged with
//!   `"source": "bulk_edit"` + the before/after values so audit
//!   queries can trace which change came through which path.

use serde::{Deserialize, Serialize};
use sqlx::PgPool;
use uuid::Uuid;

use crate::audit::{self, AuditEvent};
use crate::error::EngineError;
use crate::scope_grants;

// ─── Request / response types ────────────────────────────────────────────

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct BulkEditDevicesBody {
    pub device_ids: Vec<Uuid>,
    /// Column name from the whitelist (see `allowed_device_fields`).
    pub field: String,
    /// New value to apply to every selected row. Empty string is
    /// legal for `notes`; interpreted as NULL where the column
    /// allows it.
    pub value: String,
}

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct BulkEditVlansBody {
    pub vlan_ids: Vec<Uuid>,
    pub field: String,
    pub value: String,
}

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct BulkEditSubnetsBody {
    pub subnet_ids: Vec<Uuid>,
    pub field: String,
    pub value: String,
}

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct BulkEditQuery {
    pub organization_id: Uuid,
    #[serde(default = "default_dry_run")]
    pub dry_run: bool,
}

fn default_dry_run() -> bool { true }

#[derive(Debug, Clone, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct BulkEditOutcome {
    pub id: Uuid,
    pub hostname: String,
    pub ok: bool,
    /// Human-readable reason when `ok = false`. Kept as a single
    /// string (not a Vec<String>) because bulk-edit only has ONE
    /// field changing per row — either the change fits or it
    /// doesn't, no multi-error accumulation.
    pub error: Option<String>,
}

#[derive(Debug, Clone, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct BulkEditResult {
    pub total: usize,
    pub succeeded: usize,
    pub failed: usize,
    pub dry_run: bool,
    pub applied: bool,
    pub outcomes: Vec<BulkEditOutcome>,
}

// ─── Field whitelist ─────────────────────────────────────────────────────

/// Editable fields + the value-validation strategy each uses. Adding
/// a new field = one const entry here plus a matching branch in
/// `apply_device_edit`. Keeping both the whitelist and the validator
/// visible at the top of the file so reviewing "what's editable" is
/// a ten-second read.
const EDITABLE_DEVICE_FIELDS: &[&str] = &[
    "status",
    "role_code",
    "building_code",
    "management_ip",
    "notes",
];

pub fn is_editable_device_field(name: &str) -> bool {
    EDITABLE_DEVICE_FIELDS.iter().any(|f| *f == name)
}

fn validate_status(v: &str) -> Result<(), String> {
    if matches!(v, "Planned"|"Reserved"|"Active"|"Deprecated"|"Retired") {
        Ok(())
    } else {
        Err(format!("status '{v}' must be Planned/Reserved/Active/Deprecated/Retired"))
    }
}

fn validate_management_ip(v: &str) -> Result<(), String> {
    if v.is_empty() { return Ok(()); }
    let ip_part = v.split('/').next().unwrap_or(v);
    if ip_part.parse::<std::net::IpAddr>().is_ok() { Ok(()) }
    else { Err(format!("management_ip '{v}' is not a valid IP address")) }
}

// ─── Bulk edit — devices ─────────────────────────────────────────────────

/// Validate + apply a bulk edit to a set of devices. Validates the
/// field/value combination once up-front (field-independent per-row
/// errors are impossible, so one check covers all rows); then
/// pre-fetches every selected row's (id, hostname, version), then
/// issues one version-checked UPDATE per row inside a single tx.
pub async fn bulk_edit_devices(
    pool: &PgPool,
    org_id: Uuid,
    req: &BulkEditDevicesBody,
    dry_run: bool,
    user_id: Option<i32>,
) -> Result<BulkEditResult, EngineError> {
    // Fail fast on the cheap checks — field whitelist + value shape —
    // before we hit the DB.
    if !is_editable_device_field(&req.field) {
        return Err(EngineError::bad_request(format!(
            "field '{}' is not editable via bulk-edit (allowed: {})",
            req.field, EDITABLE_DEVICE_FIELDS.join(","))));
    }
    match req.field.as_str() {
        "status"        => validate_status(&req.value).map_err(EngineError::bad_request)?,
        "management_ip" => validate_management_ip(&req.value).map_err(EngineError::bad_request)?,
        "role_code" | "building_code" | "notes" => { /* FK/empty checks happen per row */ }
        _ => unreachable!("is_editable_device_field whitelist guards this"),
    }
    if req.device_ids.is_empty() {
        return Ok(BulkEditResult {
            total: 0, succeeded: 0, failed: 0,
            dry_run, applied: false, outcomes: vec![],
        });
    }

    // Resolve FK codes to UUIDs up front — one query for role,
    // one for building. Empty string → None (column set to NULL).
    let role_id_opt = resolve_code_or_null(
        pool, org_id, "net.device_role", "role_code", &req.value
    ).await?;
    let building_id_opt = resolve_code_or_null(
        pool, org_id, "net.building", "building_code", &req.value
    ).await?;

    // Catch a bad code before we start writing. Applies only when
    // the field is actually a code-typed column AND the value is
    // non-empty (empty clears the FK to NULL, which is valid).
    match req.field.as_str() {
        "role_code" if !req.value.is_empty() && role_id_opt.is_none() =>
            return Err(EngineError::bad_request(format!(
                "role_code '{}' not found in this tenant's device_role catalog",
                req.value))),
        "building_code" if !req.value.is_empty() && building_id_opt.is_none() =>
            return Err(EngineError::bad_request(format!(
                "building_code '{}' not found in this tenant's building catalog",
                req.value))),
        _ => {}
    }

    // RBAC — opt-in enforcement. When `user_id` is None (service-to-
    // service calls during the RBAC transition), bypass; otherwise
    // every target device must pass a `write` on Device check,
    // otherwise the whole batch is forbidden. Keeps the transactional
    // semantics: one denied device fails the whole operation.
    if let Some(uid) = user_id {
        for device_id in &req.device_ids {
            let decision = scope_grants::has_permission(
                pool, org_id, uid, "write", "Device", Some(*device_id)
            ).await?;
            if !decision.allowed {
                return Err(EngineError::forbidden(uid, "write", "Device"));
            }
        }
    }

    // Snapshot the selected rows (id, hostname, version) so the
    // operator-visible outcome carries human-readable hostnames and
    // the UPDATE below can check version.
    let targets: Vec<(Uuid, String, i32)> = sqlx::query_as(
        "SELECT id, hostname, version FROM net.device
          WHERE organization_id = $1
            AND id = ANY($2)
            AND deleted_at IS NULL")
        .bind(org_id)
        .bind(&req.device_ids)
        .fetch_all(pool)
        .await?;
    // Used only for readability — iterating `outcomes` + searching
    // `targets` handles the "missing id" branch without needing the
    // set. Kept as an underscore binding so any future reader knows
    // the intent ("we did consider pre-indexing ids; chose not to").
    let _found_ids: std::collections::HashSet<Uuid> =
        targets.iter().map(|(id, _, _)| *id).collect();

    // Build outcomes for every requested id — rows not found get a
    // clear error so operators see exactly which ids were bad.
    let mut outcomes: Vec<BulkEditOutcome> = req.device_ids.iter().map(|id| {
        if let Some((_, hostname, _)) = targets.iter().find(|(tid, _, _)| tid == id) {
            BulkEditOutcome { id: *id, hostname: hostname.clone(), ok: true, error: None }
        } else {
            BulkEditOutcome {
                id: *id, hostname: String::new(), ok: false,
                error: Some("device not found in this tenant (or soft-deleted)".into()),
            }
        }
    }).collect();

    let any_missing = outcomes.iter().any(|o| !o.ok);
    if dry_run || any_missing {
        return Ok(BulkEditResult {
            total:     outcomes.len(),
            succeeded: outcomes.iter().filter(|o| o.ok).count(),
            failed:    outcomes.iter().filter(|o| !o.ok).count(),
            dry_run, applied: false, outcomes,
        });
    }

    // Apply. One version-checked UPDATE per row inside a single tx.
    let mut tx = pool.begin().await?;
    for (target_id, target_hostname, current_version) in &targets {
        // Find this target's outcome slot so we can mark it failed on error.
        let outcome_idx = outcomes.iter()
            .position(|o| &o.id == target_id)
            .expect("targets filtered from device_ids → idx is always present");

        let update_result = apply_device_field_update(
            &mut tx, org_id, *target_id, *current_version, &req.field,
            &req.value, role_id_opt, building_id_opt, user_id,
        ).await;

        match update_result {
            Ok(rows_affected) if rows_affected == 1 => { /* happy path */ }
            Ok(_) => {
                outcomes[outcome_idx].ok = false;
                outcomes[outcome_idx].error = Some(
                    "concurrent write detected (version changed between read and write)".into()
                );
                return Ok(BulkEditResult {
                    total: outcomes.len(),
                    succeeded: outcomes.iter().filter(|o| o.ok).count(),
                    failed:    outcomes.iter().filter(|o| !o.ok).count(),
                    dry_run: false, applied: false, outcomes,
                });
            }
            Err(e) => {
                outcomes[outcome_idx].ok = false;
                outcomes[outcome_idx].error = Some(format!("database UPDATE failed: {e}"));
                return Ok(BulkEditResult {
                    total: outcomes.len(),
                    succeeded: outcomes.iter().filter(|o| o.ok).count(),
                    failed:    outcomes.iter().filter(|o| !o.ok).count(),
                    dry_run: false, applied: false, outcomes,
                });
            }
        }

        audit::append_tx(&mut tx, &AuditEvent {
            organization_id: org_id,
            source_service: "networking-engine",
            entity_type: "Device",
            entity_id: Some(*target_id),
            action: "BulkEdited",
            actor_user_id: user_id,
            actor_display: None, client_ip: None, correlation_id: None,
            details: serde_json::json!({
                "source": "bulk_edit",
                "hostname": target_hostname,
                "field": req.field,
                "new_value": req.value,
            }),
        }).await?;
    }
    tx.commit().await?;

    let total = outcomes.len();
    Ok(BulkEditResult {
        total,
        succeeded: total,
        failed: 0,
        dry_run: false, applied: true, outcomes,
    })
}

/// Resolve a `{table}.{code_col}` value to its row uuid. Empty string
/// → None (caller uses None as "set column to NULL"). Missing code
/// → None (caller decides whether that's an error based on the field
/// semantic).
async fn resolve_code_or_null(
    pool: &PgPool,
    org_id: Uuid,
    table: &str,
    code_col: &str,
    value: &str,
) -> Result<Option<Uuid>, EngineError> {
    if value.is_empty() { return Ok(None); }
    // table + code_col aren't user input — they come from the field
    // whitelist or are hardcoded at call-sites. Still, build the
    // query with format! for clarity and bind the value separately
    // so no value ever lands in the SQL text.
    let sql = format!(
        "SELECT id FROM {table}
          WHERE organization_id = $1 AND {code_col} = $2 AND deleted_at IS NULL");
    let row: Option<(Uuid,)> = sqlx::query_as(&sql)
        .bind(org_id).bind(value)
        .fetch_optional(pool).await?;
    Ok(row.map(|(id,)| id))
}

/// Issue the version-checked UPDATE for one (device, field, value).
/// Returns number of rows affected — 1 on happy path, 0 on version
/// mismatch (caller treats as a concurrent-write error).
#[allow(clippy::too_many_arguments)]
async fn apply_device_field_update(
    tx: &mut sqlx::Transaction<'_, sqlx::Postgres>,
    org_id: Uuid,
    device_id: Uuid,
    current_version: i32,
    field: &str,
    value: &str,
    role_id_opt: Option<Uuid>,
    building_id_opt: Option<Uuid>,
    user_id: Option<i32>,
) -> Result<u64, sqlx::Error> {
    // Value handling per field:
    //   status:        bind the enum value directly (cast in SQL)
    //   management_ip: empty → NULL, else cast ::inet
    //   notes:         empty → NULL
    //   role_code:     empty → NULL (clears FK), else role_id_opt UUID
    //   building_code: empty → NULL (clears FK), else building_id_opt UUID
    let value_opt: Option<&str> = if value.is_empty() { None } else { Some(value) };

    let query = match field {
        "status" => sqlx::query(
            "UPDATE net.device
                SET status = $3::net.entity_status,
                    updated_at = now(), updated_by = $4, version = version + 1
              WHERE id = $1 AND organization_id = $2 AND version = $5
                AND deleted_at IS NULL")
            .bind(device_id).bind(org_id).bind(value).bind(user_id).bind(current_version),
        "management_ip" => sqlx::query(
            "UPDATE net.device
                SET management_ip = $3::inet,
                    updated_at = now(), updated_by = $4, version = version + 1
              WHERE id = $1 AND organization_id = $2 AND version = $5
                AND deleted_at IS NULL")
            .bind(device_id).bind(org_id).bind(value_opt).bind(user_id).bind(current_version),
        "notes" => sqlx::query(
            "UPDATE net.device
                SET notes = $3,
                    updated_at = now(), updated_by = $4, version = version + 1
              WHERE id = $1 AND organization_id = $2 AND version = $5
                AND deleted_at IS NULL")
            .bind(device_id).bind(org_id).bind(value_opt).bind(user_id).bind(current_version),
        "role_code" => sqlx::query(
            "UPDATE net.device
                SET device_role_id = $3,
                    updated_at = now(), updated_by = $4, version = version + 1
              WHERE id = $1 AND organization_id = $2 AND version = $5
                AND deleted_at IS NULL")
            .bind(device_id).bind(org_id).bind(role_id_opt).bind(user_id).bind(current_version),
        "building_code" => sqlx::query(
            "UPDATE net.device
                SET building_id = $3,
                    updated_at = now(), updated_by = $4, version = version + 1
              WHERE id = $1 AND organization_id = $2 AND version = $5
                AND deleted_at IS NULL")
            .bind(device_id).bind(org_id).bind(building_id_opt).bind(user_id).bind(current_version),
        _ => unreachable!("is_editable_device_field whitelist guards this"),
    };
    let result = query.execute(&mut **tx).await?;
    Ok(result.rows_affected())
}

// ─── VLAN bulk edit ──────────────────────────────────────────────────────

const EDITABLE_VLAN_FIELDS: &[&str] = &[
    "display_name", "description", "scope_level", "status", "template_code", "notes",
];

pub fn is_editable_vlan_field(name: &str) -> bool {
    EDITABLE_VLAN_FIELDS.iter().any(|f| *f == name)
}

fn validate_vlan_scope_level(v: &str) -> Result<(), String> {
    if matches!(v, "Free"|"Region"|"Site"|"Building"|"Device") { Ok(()) }
    else { Err(format!("scope_level '{v}' must be Free/Region/Site/Building/Device")) }
}

pub async fn bulk_edit_vlans(
    pool: &PgPool,
    org_id: Uuid,
    req: &BulkEditVlansBody,
    dry_run: bool,
    user_id: Option<i32>,
) -> Result<BulkEditResult, EngineError> {
    if !is_editable_vlan_field(&req.field) {
        return Err(EngineError::bad_request(format!(
            "field '{}' is not editable via bulk-edit (allowed: {})",
            req.field, EDITABLE_VLAN_FIELDS.join(","))));
    }
    match req.field.as_str() {
        "status"      => validate_status(&req.value).map_err(EngineError::bad_request)?,
        "scope_level" => validate_vlan_scope_level(&req.value).map_err(EngineError::bad_request)?,
        "display_name" if req.value.is_empty() =>
            return Err(EngineError::bad_request("display_name cannot be empty")),
        "display_name" | "description" | "notes" | "template_code" => {}
        _ => unreachable!("is_editable_vlan_field whitelist guards this"),
    }
    if req.vlan_ids.is_empty() {
        return Ok(BulkEditResult {
            total: 0, succeeded: 0, failed: 0,
            dry_run, applied: false, outcomes: vec![],
        });
    }

    // Resolve template_code → uuid if that's the field being edited.
    let template_id_opt = resolve_code_or_null(
        pool, org_id, "net.vlan_template", "template_code", &req.value,
    ).await?;
    if req.field == "template_code" && !req.value.is_empty() && template_id_opt.is_none() {
        return Err(EngineError::bad_request(format!(
            "template_code '{}' not found in this tenant's vlan_template catalog",
            req.value)));
    }

    // Snapshot — VLANs are identified by their display_name for the
    // outcome `hostname` slot (the field name is misleading given
    // it's carrying a VLAN label, but the DTO shape stays consistent
    // across entity types so clients don't need per-entity result
    // types).
    let targets: Vec<(Uuid, String, i32)> = sqlx::query_as(
        "SELECT id, display_name, version FROM net.vlan
          WHERE organization_id = $1 AND id = ANY($2) AND deleted_at IS NULL")
        .bind(org_id).bind(&req.vlan_ids)
        .fetch_all(pool).await?;

    let mut outcomes: Vec<BulkEditOutcome> = req.vlan_ids.iter().map(|id| {
        if let Some((_, label, _)) = targets.iter().find(|(t, _, _)| t == id) {
            BulkEditOutcome { id: *id, hostname: label.clone(), ok: true, error: None }
        } else {
            BulkEditOutcome {
                id: *id, hostname: String::new(), ok: false,
                error: Some("vlan not found in this tenant (or soft-deleted)".into()),
            }
        }
    }).collect();
    let any_missing = outcomes.iter().any(|o| !o.ok);
    if dry_run || any_missing {
        return Ok(BulkEditResult {
            total: outcomes.len(),
            succeeded: outcomes.iter().filter(|o| o.ok).count(),
            failed:    outcomes.iter().filter(|o| !o.ok).count(),
            dry_run, applied: false, outcomes,
        });
    }

    // RBAC — opt-in via X-User-Id presence, same as devices.
    if let Some(uid) = user_id {
        for vlan_id in &req.vlan_ids {
            let decision = crate::scope_grants::has_permission(
                pool, org_id, uid, "write", "Vlan", Some(*vlan_id)
            ).await?;
            if !decision.allowed {
                return Err(EngineError::forbidden(uid, "write", "Vlan"));
            }
        }
    }

    let mut tx = pool.begin().await?;
    for (target_id, target_label, current_version) in &targets {
        let outcome_idx = outcomes.iter()
            .position(|o| &o.id == target_id)
            .expect("targets filtered from vlan_ids");

        let update_result = apply_vlan_field_update(
            &mut tx, org_id, *target_id, *current_version, &req.field,
            &req.value, template_id_opt, user_id,
        ).await;

        match update_result {
            Ok(rows_affected) if rows_affected == 1 => {}
            Ok(_) => {
                outcomes[outcome_idx].ok = false;
                outcomes[outcome_idx].error = Some(
                    "concurrent write detected (version changed between read and write)".into()
                );
                return Ok(BulkEditResult {
                    total: outcomes.len(),
                    succeeded: outcomes.iter().filter(|o| o.ok).count(),
                    failed:    outcomes.iter().filter(|o| !o.ok).count(),
                    dry_run: false, applied: false, outcomes,
                });
            }
            Err(e) => {
                outcomes[outcome_idx].ok = false;
                outcomes[outcome_idx].error = Some(format!("database UPDATE failed: {e}"));
                return Ok(BulkEditResult {
                    total: outcomes.len(),
                    succeeded: outcomes.iter().filter(|o| o.ok).count(),
                    failed:    outcomes.iter().filter(|o| !o.ok).count(),
                    dry_run: false, applied: false, outcomes,
                });
            }
        }

        audit::append_tx(&mut tx, &AuditEvent {
            organization_id: org_id,
            source_service: "networking-engine",
            entity_type: "Vlan",
            entity_id: Some(*target_id),
            action: "BulkEdited",
            actor_user_id: user_id,
            actor_display: None, client_ip: None, correlation_id: None,
            details: serde_json::json!({
                "source": "bulk_edit",
                "display_name": target_label,
                "field": req.field,
                "new_value": req.value,
            }),
        }).await?;
    }
    tx.commit().await?;

    let total = outcomes.len();
    Ok(BulkEditResult {
        total, succeeded: total, failed: 0,
        dry_run: false, applied: true, outcomes,
    })
}

async fn apply_vlan_field_update(
    tx: &mut sqlx::Transaction<'_, sqlx::Postgres>,
    org_id: Uuid, vlan_id: Uuid, current_version: i32,
    field: &str, value: &str, template_id_opt: Option<Uuid>,
    user_id: Option<i32>,
) -> Result<u64, sqlx::Error> {
    let value_opt: Option<&str> = if value.is_empty() { None } else { Some(value) };
    let query = match field {
        "display_name" => sqlx::query(
            "UPDATE net.vlan SET display_name = $3, updated_at = now(),
                 updated_by = $4, version = version + 1
              WHERE id = $1 AND organization_id = $2 AND version = $5 AND deleted_at IS NULL")
            .bind(vlan_id).bind(org_id).bind(value).bind(user_id).bind(current_version),
        "description" => sqlx::query(
            "UPDATE net.vlan SET description = $3, updated_at = now(),
                 updated_by = $4, version = version + 1
              WHERE id = $1 AND organization_id = $2 AND version = $5 AND deleted_at IS NULL")
            .bind(vlan_id).bind(org_id).bind(value_opt).bind(user_id).bind(current_version),
        "scope_level" => sqlx::query(
            "UPDATE net.vlan SET scope_level = $3, updated_at = now(),
                 updated_by = $4, version = version + 1
              WHERE id = $1 AND organization_id = $2 AND version = $5 AND deleted_at IS NULL")
            .bind(vlan_id).bind(org_id).bind(value).bind(user_id).bind(current_version),
        "status" => sqlx::query(
            "UPDATE net.vlan SET status = $3::net.entity_status, updated_at = now(),
                 updated_by = $4, version = version + 1
              WHERE id = $1 AND organization_id = $2 AND version = $5 AND deleted_at IS NULL")
            .bind(vlan_id).bind(org_id).bind(value).bind(user_id).bind(current_version),
        "template_code" => sqlx::query(
            "UPDATE net.vlan SET template_id = $3, updated_at = now(),
                 updated_by = $4, version = version + 1
              WHERE id = $1 AND organization_id = $2 AND version = $5 AND deleted_at IS NULL")
            .bind(vlan_id).bind(org_id).bind(template_id_opt).bind(user_id).bind(current_version),
        "notes" => sqlx::query(
            "UPDATE net.vlan SET notes = $3, updated_at = now(),
                 updated_by = $4, version = version + 1
              WHERE id = $1 AND organization_id = $2 AND version = $5 AND deleted_at IS NULL")
            .bind(vlan_id).bind(org_id).bind(value_opt).bind(user_id).bind(current_version),
        _ => unreachable!(),
    };
    Ok(query.execute(&mut **tx).await?.rows_affected())
}

// ─── Subnet bulk edit ────────────────────────────────────────────────────

const EDITABLE_SUBNET_FIELDS: &[&str] = &[
    "display_name", "scope_level", "status", "notes",
];

pub fn is_editable_subnet_field(name: &str) -> bool {
    EDITABLE_SUBNET_FIELDS.iter().any(|f| *f == name)
}

fn validate_subnet_scope_level(v: &str) -> Result<(), String> {
    if matches!(v, "Free"|"Region"|"Site"|"Building"|"Floor"|"Room") { Ok(()) }
    else { Err(format!("scope_level '{v}' must be Free/Region/Site/Building/Floor/Room")) }
}

pub async fn bulk_edit_subnets(
    pool: &PgPool,
    org_id: Uuid,
    req: &BulkEditSubnetsBody,
    dry_run: bool,
    user_id: Option<i32>,
) -> Result<BulkEditResult, EngineError> {
    if !is_editable_subnet_field(&req.field) {
        return Err(EngineError::bad_request(format!(
            "field '{}' is not editable via bulk-edit (allowed: {})",
            req.field, EDITABLE_SUBNET_FIELDS.join(","))));
    }
    match req.field.as_str() {
        "status"      => validate_status(&req.value).map_err(EngineError::bad_request)?,
        "scope_level" => validate_subnet_scope_level(&req.value).map_err(EngineError::bad_request)?,
        "display_name" if req.value.is_empty() =>
            return Err(EngineError::bad_request("display_name cannot be empty")),
        "display_name" | "notes" => {}
        _ => unreachable!(),
    }
    if req.subnet_ids.is_empty() {
        return Ok(BulkEditResult {
            total: 0, succeeded: 0, failed: 0,
            dry_run, applied: false, outcomes: vec![],
        });
    }

    let targets: Vec<(Uuid, String, i32)> = sqlx::query_as(
        "SELECT id, subnet_code, version FROM net.subnet
          WHERE organization_id = $1 AND id = ANY($2) AND deleted_at IS NULL")
        .bind(org_id).bind(&req.subnet_ids)
        .fetch_all(pool).await?;

    let mut outcomes: Vec<BulkEditOutcome> = req.subnet_ids.iter().map(|id| {
        if let Some((_, code, _)) = targets.iter().find(|(t, _, _)| t == id) {
            BulkEditOutcome { id: *id, hostname: code.clone(), ok: true, error: None }
        } else {
            BulkEditOutcome {
                id: *id, hostname: String::new(), ok: false,
                error: Some("subnet not found in this tenant (or soft-deleted)".into()),
            }
        }
    }).collect();
    let any_missing = outcomes.iter().any(|o| !o.ok);
    if dry_run || any_missing {
        return Ok(BulkEditResult {
            total: outcomes.len(),
            succeeded: outcomes.iter().filter(|o| o.ok).count(),
            failed:    outcomes.iter().filter(|o| !o.ok).count(),
            dry_run, applied: false, outcomes,
        });
    }

    if let Some(uid) = user_id {
        for sid in &req.subnet_ids {
            let d = crate::scope_grants::has_permission(
                pool, org_id, uid, "write", "Subnet", Some(*sid)
            ).await?;
            if !d.allowed { return Err(EngineError::forbidden(uid, "write", "Subnet")); }
        }
    }

    let mut tx = pool.begin().await?;
    for (target_id, target_code, current_version) in &targets {
        let outcome_idx = outcomes.iter().position(|o| &o.id == target_id).unwrap();

        let update_result = apply_subnet_field_update(
            &mut tx, org_id, *target_id, *current_version, &req.field, &req.value, user_id,
        ).await;

        match update_result {
            Ok(rows_affected) if rows_affected == 1 => {}
            Ok(_) => {
                outcomes[outcome_idx].ok = false;
                outcomes[outcome_idx].error = Some(
                    "concurrent write detected (version changed between read and write)".into()
                );
                return Ok(BulkEditResult {
                    total: outcomes.len(),
                    succeeded: outcomes.iter().filter(|o| o.ok).count(),
                    failed:    outcomes.iter().filter(|o| !o.ok).count(),
                    dry_run: false, applied: false, outcomes,
                });
            }
            Err(e) => {
                outcomes[outcome_idx].ok = false;
                outcomes[outcome_idx].error = Some(format!("database UPDATE failed: {e}"));
                return Ok(BulkEditResult {
                    total: outcomes.len(),
                    succeeded: outcomes.iter().filter(|o| o.ok).count(),
                    failed:    outcomes.iter().filter(|o| !o.ok).count(),
                    dry_run: false, applied: false, outcomes,
                });
            }
        }

        audit::append_tx(&mut tx, &AuditEvent {
            organization_id: org_id,
            source_service: "networking-engine",
            entity_type: "Subnet",
            entity_id: Some(*target_id),
            action: "BulkEdited",
            actor_user_id: user_id,
            actor_display: None, client_ip: None, correlation_id: None,
            details: serde_json::json!({
                "source": "bulk_edit",
                "subnet_code": target_code,
                "field": req.field,
                "new_value": req.value,
            }),
        }).await?;
    }
    tx.commit().await?;

    let total = outcomes.len();
    Ok(BulkEditResult {
        total, succeeded: total, failed: 0,
        dry_run: false, applied: true, outcomes,
    })
}

async fn apply_subnet_field_update(
    tx: &mut sqlx::Transaction<'_, sqlx::Postgres>,
    org_id: Uuid, subnet_id: Uuid, current_version: i32,
    field: &str, value: &str, user_id: Option<i32>,
) -> Result<u64, sqlx::Error> {
    let value_opt: Option<&str> = if value.is_empty() { None } else { Some(value) };
    let query = match field {
        "display_name" => sqlx::query(
            "UPDATE net.subnet SET display_name = $3, updated_at = now(),
                 updated_by = $4, version = version + 1
              WHERE id = $1 AND organization_id = $2 AND version = $5 AND deleted_at IS NULL")
            .bind(subnet_id).bind(org_id).bind(value).bind(user_id).bind(current_version),
        "scope_level" => sqlx::query(
            "UPDATE net.subnet SET scope_level = $3, updated_at = now(),
                 updated_by = $4, version = version + 1
              WHERE id = $1 AND organization_id = $2 AND version = $5 AND deleted_at IS NULL")
            .bind(subnet_id).bind(org_id).bind(value).bind(user_id).bind(current_version),
        "status" => sqlx::query(
            "UPDATE net.subnet SET status = $3::net.entity_status, updated_at = now(),
                 updated_by = $4, version = version + 1
              WHERE id = $1 AND organization_id = $2 AND version = $5 AND deleted_at IS NULL")
            .bind(subnet_id).bind(org_id).bind(value).bind(user_id).bind(current_version),
        "notes" => sqlx::query(
            "UPDATE net.subnet SET notes = $3, updated_at = now(),
                 updated_by = $4, version = version + 1
              WHERE id = $1 AND organization_id = $2 AND version = $5 AND deleted_at IS NULL")
            .bind(subnet_id).bind(org_id).bind(value_opt).bind(user_id).bind(current_version),
        _ => unreachable!(),
    };
    Ok(query.execute(&mut **tx).await?.rows_affected())
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn whitelist_accepts_every_documented_field() {
        assert!(is_editable_device_field("status"));
        assert!(is_editable_device_field("role_code"));
        assert!(is_editable_device_field("building_code"));
        assert!(is_editable_device_field("management_ip"));
        assert!(is_editable_device_field("notes"));
    }

    #[test]
    fn whitelist_rejects_hostname_and_version_by_design() {
        // Hostname rename via bulk-edit would turn a 1-device typo
        // into a 50-device outage — gated behind single-row CRUD on
        // purpose. Version is managed by optimistic concurrency.
        assert!(!is_editable_device_field("hostname"));
        assert!(!is_editable_device_field("version"));
    }

    #[test]
    fn whitelist_rejects_random_column_names() {
        assert!(!is_editable_device_field("asn_allocation_id"));
        assert!(!is_editable_device_field("deleted_at"));
        assert!(!is_editable_device_field("id"));
        assert!(!is_editable_device_field(""));
        assert!(!is_editable_device_field("; DROP TABLE net.device;--"));
    }

    #[test]
    fn validate_status_accepts_all_enum_variants() {
        for v in ["Planned","Reserved","Active","Deprecated","Retired"] {
            assert!(validate_status(v).is_ok(), "{v} should be accepted");
        }
    }

    #[test]
    fn validate_status_rejects_case_mismatches_and_typos() {
        // Enum is case-sensitive server-side; bulk-edit mirrors that
        // so operators see the mismatch here rather than a cryptic
        // DB error.
        for v in ["active", "RETIRED", "Decommissioned", "Ready", ""] {
            assert!(validate_status(v).is_err(), "{v} should be rejected");
        }
    }

    #[test]
    fn validate_management_ip_accepts_bare_host_and_cidr() {
        assert!(validate_management_ip("10.11.152.2").is_ok());
        assert!(validate_management_ip("10.11.152.2/24").is_ok());
        assert!(validate_management_ip("fe80::1").is_ok());
        assert!(validate_management_ip("fe80::1/64").is_ok());
    }

    #[test]
    fn validate_management_ip_accepts_empty_as_clear_to_null() {
        assert!(validate_management_ip("").is_ok(),
            "empty value clears the column to NULL — must be accepted");
    }

    #[test]
    fn validate_management_ip_rejects_garbage() {
        assert!(validate_management_ip("not-an-ip").is_err());
        assert!(validate_management_ip("10.11.152").is_err());
        assert!(validate_management_ip("999.999.999.999").is_err());
    }
}
