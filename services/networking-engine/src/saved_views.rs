//! Saved views — per-user named search queries.
//!
//! Companion to `/api/net/search`. An operator tweaks a search +
//! facet filter into something useful, saves it as "Retired devices
//! in MEP-91", and restores it later from the sidebar.
//!
//! ## Why no scope_grants gating
//!
//! Saved views are **per-user**. The user_id on every row IS the
//! access control: the list endpoint filters by the caller's
//! X-User-Id header; get / update / delete check that the view's
//! user_id matches the caller. A user with no grants can still
//! manage their own views — they're personal state, not tenant
//! config. This matches the "personal settings" pattern the WPF
//! side already uses for panel layouts.
//!
//! Service-call semantics (no X-User-Id): list returns nothing
//! (there's no user to scope to), mutations return 400. This keeps
//! the table genuinely per-user — no ambiguous "orphan" views
//! created by services.

use chrono::{DateTime, Utc};
use serde::{Deserialize, Serialize};
use sqlx::PgPool;
use uuid::Uuid;

use crate::audit::{self, AuditEvent};
use crate::error::EngineError;

// ─── DTOs ────────────────────────────────────────────────────────────────

#[derive(Debug, Clone, Serialize, sqlx::FromRow)]
#[serde(rename_all = "camelCase")]
pub struct SavedView {
    pub id: Uuid,
    pub organization_id: Uuid,
    pub user_id: i32,
    pub name: String,
    pub q: String,
    pub entity_types: Option<String>,
    pub filters: serde_json::Value,
    pub status: String,
    pub version: i32,
    pub created_at: DateTime<Utc>,
    pub updated_at: DateTime<Utc>,
    pub notes: Option<String>,
}

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct CreateSavedViewBody {
    pub organization_id: Uuid,
    pub name: String,
    #[serde(default)]
    pub q: String,
    pub entity_types: Option<String>,
    #[serde(default)]
    pub filters: serde_json::Value,
    pub notes: Option<String>,
}

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct UpdateSavedViewBody {
    pub name: String,
    pub q: String,
    pub entity_types: Option<String>,
    #[serde(default)]
    pub filters: serde_json::Value,
    pub notes: Option<String>,
    pub version: i32,
}

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct ListSavedViewsQuery {
    pub organization_id: Uuid,
}

// ─── Repo ────────────────────────────────────────────────────────────────

#[derive(Clone)]
pub struct SavedViewRepo {
    pool: PgPool,
}

impl SavedViewRepo {
    pub fn new(pool: PgPool) -> Self { Self { pool } }

    /// List saved views owned by `user_id` in `org_id`. Returns
    /// empty when `user_id` is None — saved views are personal, so
    /// service-call bypass returns nothing rather than every user's
    /// views.
    pub async fn list(
        &self,
        q: &ListSavedViewsQuery,
        user_id: Option<i32>,
    ) -> Result<Vec<SavedView>, EngineError> {
        let Some(uid) = user_id else { return Ok(vec![]); };
        let rows: Vec<SavedView> = sqlx::query_as(
            "SELECT id, organization_id, user_id, name, q, entity_types,
                    filters, status::text AS status, version,
                    created_at, updated_at, notes
               FROM net.saved_view
              WHERE organization_id = $1
                AND user_id         = $2
                AND deleted_at      IS NULL
              ORDER BY name")
            .bind(q.organization_id)
            .bind(uid)
            .fetch_all(&self.pool).await?;
        Ok(rows)
    }

    pub async fn get(
        &self,
        id: Uuid,
        org_id: Uuid,
        user_id: Option<i32>,
    ) -> Result<SavedView, EngineError> {
        let row: Option<SavedView> = sqlx::query_as(
            "SELECT id, organization_id, user_id, name, q, entity_types,
                    filters, status::text AS status, version,
                    created_at, updated_at, notes
               FROM net.saved_view
              WHERE id = $1 AND organization_id = $2 AND deleted_at IS NULL")
            .bind(id).bind(org_id)
            .fetch_optional(&self.pool).await?;
        let row = row.ok_or_else(|| EngineError::container_not_found("saved_view", id))?;
        require_owner(&row, user_id)?;
        Ok(row)
    }

    pub async fn create(
        &self,
        body: &CreateSavedViewBody,
        user_id: Option<i32>,
    ) -> Result<SavedView, EngineError> {
        // Saved views need an owner. Service calls without
        // X-User-Id can't create views — better to reject at the
        // API layer than materialise orphan rows.
        let uid = user_id.ok_or_else(|| EngineError::bad_request(
            "saved views require an X-User-Id header — service calls can't own a view"))?;
        if body.name.trim().is_empty() {
            return Err(EngineError::bad_request("name is required"));
        }

        let mut tx = self.pool.begin().await?;
        let row: SavedView = sqlx::query_as(
            "INSERT INTO net.saved_view
                (organization_id, user_id, name, q, entity_types, filters,
                 notes, status, lock_state, created_by, updated_by)
             VALUES ($1, $2, $3, $4, $5, $6, $7,
                     'Active'::net.entity_status, 'Open'::net.lock_state, $2, $2)
             RETURNING id, organization_id, user_id, name, q, entity_types,
                       filters, status::text AS status, version,
                       created_at, updated_at, notes")
            .bind(body.organization_id)
            .bind(uid)
            .bind(body.name.trim())
            .bind(&body.q)
            .bind(body.entity_types.as_deref())
            .bind(&body.filters)
            .bind(body.notes.as_deref())
            .fetch_one(&mut *tx).await?;

        audit::append_tx(&mut tx, &AuditEvent {
            organization_id: row.organization_id,
            source_service: "networking-engine",
            entity_type: "SavedView",
            entity_id: Some(row.id),
            action: "Created",
            actor_user_id: user_id,
            actor_display: None, client_ip: None, correlation_id: None,
            details: serde_json::json!({
                "owner_user_id": row.user_id,
                "name": row.name,
            }),
        }).await?;

        tx.commit().await?;
        Ok(row)
    }

    pub async fn update(
        &self,
        id: Uuid,
        org_id: Uuid,
        body: &UpdateSavedViewBody,
        user_id: Option<i32>,
    ) -> Result<SavedView, EngineError> {
        let uid = user_id.ok_or_else(|| EngineError::bad_request(
            "saved views require an X-User-Id header — service calls can't update a view"))?;
        if body.name.trim().is_empty() {
            return Err(EngineError::bad_request("name is required"));
        }

        let mut tx = self.pool.begin().await?;

        // Ownership check + optimistic concurrency collapsed into a
        // single UPDATE — if user_id OR version don't match, rows
        // affected is 0 and we return a clear error.
        let row: Option<SavedView> = sqlx::query_as(
            "UPDATE net.saved_view
                SET name = $3, q = $4, entity_types = $5,
                    filters = $6, notes = $7,
                    updated_at = now(), updated_by = $2, version = version + 1
              WHERE id = $1 AND organization_id = $8
                AND user_id = $2 AND version = $9 AND deleted_at IS NULL
              RETURNING id, organization_id, user_id, name, q, entity_types,
                        filters, status::text AS status, version,
                        created_at, updated_at, notes")
            .bind(id)
            .bind(uid)
            .bind(body.name.trim())
            .bind(&body.q)
            .bind(body.entity_types.as_deref())
            .bind(&body.filters)
            .bind(body.notes.as_deref())
            .bind(org_id)
            .bind(body.version)
            .fetch_optional(&mut *tx).await?;
        let row = row.ok_or_else(|| EngineError::bad_request(format!(
            "saved view {id} not found, not owned by caller, or version mismatch")))?;

        audit::append_tx(&mut tx, &AuditEvent {
            organization_id: org_id,
            source_service: "networking-engine",
            entity_type: "SavedView",
            entity_id: Some(row.id),
            action: "Updated",
            actor_user_id: user_id,
            actor_display: None, client_ip: None, correlation_id: None,
            details: serde_json::json!({
                "name": row.name,
                "q": row.q,
            }),
        }).await?;

        tx.commit().await?;
        Ok(row)
    }

    pub async fn soft_delete(
        &self,
        id: Uuid,
        org_id: Uuid,
        user_id: Option<i32>,
    ) -> Result<(), EngineError> {
        let uid = user_id.ok_or_else(|| EngineError::bad_request(
            "saved views require an X-User-Id header"))?;
        let mut tx = self.pool.begin().await?;
        let affected = sqlx::query(
            "UPDATE net.saved_view
                SET deleted_at = now(), deleted_by = $2
              WHERE id = $1 AND organization_id = $3 AND user_id = $2
                AND deleted_at IS NULL")
            .bind(id).bind(uid).bind(org_id)
            .execute(&mut *tx).await?.rows_affected();
        if affected == 0 {
            return Err(EngineError::container_not_found("saved_view", id));
        }
        audit::append_tx(&mut tx, &AuditEvent {
            organization_id: org_id,
            source_service: "networking-engine",
            entity_type: "SavedView",
            entity_id: Some(id),
            action: "Deleted",
            actor_user_id: user_id,
            actor_display: None, client_ip: None, correlation_id: None,
            details: serde_json::json!({}),
        }).await?;
        tx.commit().await?;
        Ok(())
    }
}

/// Reject reads that don't match the view's owner. Returns
/// container_not_found (404) rather than Forbidden (403) to avoid
/// leaking existence of other users' views — the caller can't tell
/// whether the id doesn't exist or just isn't theirs.
fn require_owner(view: &SavedView, user_id: Option<i32>) -> Result<(), EngineError> {
    match user_id {
        Some(uid) if uid == view.user_id => Ok(()),
        _ => Err(EngineError::container_not_found("saved_view", view.id)),
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    fn view_owned_by(uid: i32) -> SavedView {
        SavedView {
            id: Uuid::nil(), organization_id: Uuid::nil(),
            user_id: uid, name: "x".into(), q: "".into(),
            entity_types: None, filters: serde_json::json!({}),
            status: "Active".into(), version: 1,
            created_at: Utc::now(), updated_at: Utc::now(),
            notes: None,
        }
    }

    #[test]
    fn require_owner_accepts_matching_user() {
        let v = view_owned_by(42);
        assert!(require_owner(&v, Some(42)).is_ok());
    }

    #[test]
    fn require_owner_rejects_non_matching_user_as_not_found() {
        // Intentionally 404 (not 403) — leaking "this id exists but
        // belongs to someone else" is a privacy hole.
        let v = view_owned_by(42);
        let err = require_owner(&v, Some(99)).unwrap_err();
        assert!(err.to_string().contains("saved_view"), "err: {err}");
    }

    #[test]
    fn require_owner_rejects_missing_user_id() {
        // Service calls can't read individual views; list is the
        // only allowed read path without X-User-Id (returns empty).
        let v = view_owned_by(42);
        assert!(require_owner(&v, None).is_err());
    }

    #[test]
    fn saved_view_dto_serialises_camelcase() {
        let v = SavedView {
            id: Uuid::nil(), organization_id: Uuid::nil(),
            user_id: 7, name: "critical".into(), q: "retired core".into(),
            entity_types: Some("Device".into()),
            filters: serde_json::json!({"status": "Retired"}),
            status: "Active".into(), version: 3,
            created_at: Utc::now(), updated_at: Utc::now(),
            notes: Some("weekly review".into()),
        };
        let json = serde_json::to_string(&v).expect("serialises");
        assert!(json.contains("\"userId\":7"));
        assert!(json.contains("\"entityTypes\":\"Device\""));
        assert!(json.contains("\"filters\":{"),
            "filters jsonb should embed as JSON object: {json}");
    }
}
