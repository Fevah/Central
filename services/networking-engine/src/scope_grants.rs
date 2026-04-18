//! Scope grants — tuple-based RBAC for the networking engine.
//!
//! Ships the CRUD + `has_permission` resolver; per-endpoint
//! enforcement lands in follow-on slices. Engine is AVAILABLE now
//! but not yet BLOCKING anywhere, so existing admin-any-access
//! stays working while callers opt in to enforcement one surface
//! at a time.
//!
//! ## Resolver v1 — Global + EntityId only
//!
//! `has_permission(pool, org, user_id, action, entity_type, entity_id)`
//! returns `true` when any non-deleted grant matches:
//!
//!   - `scope_type = 'Global'` for the (user, action, entity_type), OR
//!   - `scope_type = 'EntityId' AND scope_entity_id = entity_id`
//!
//! Region/Site/Building-scoped grants ARE stored but NOT yet
//! enforced by the resolver — hierarchical expansion needs a
//! careful join across the hierarchy that belongs in its own
//! slice. Grants at those scopes currently behave like "grant
//! exists but doesn't match anything"; enforcing them is a
//! resolver change with no schema side.

use chrono::{DateTime, Utc};
use serde::{Deserialize, Serialize};
use sqlx::PgPool;
use uuid::Uuid;

use crate::audit::{self, AuditEvent};
use crate::error::EngineError;

// ─── DTOs ────────────────────────────────────────────────────────────────

#[derive(Debug, Clone, Serialize, sqlx::FromRow)]
#[serde(rename_all = "camelCase")]
pub struct ScopeGrant {
    pub id: Uuid,
    pub organization_id: Uuid,
    pub user_id: i32,
    pub action: String,
    pub entity_type: String,
    pub scope_type: String,
    pub scope_entity_id: Option<Uuid>,
    pub status: String,
    pub version: i32,
    pub created_at: DateTime<Utc>,
    pub updated_at: DateTime<Utc>,
    pub notes: Option<String>,
}

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct CreateScopeGrantBody {
    pub organization_id: Uuid,
    pub user_id: i32,
    pub action: String,
    pub entity_type: String,
    pub scope_type: String,
    pub scope_entity_id: Option<Uuid>,
    pub notes: Option<String>,
}

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct ListScopeGrantsQuery {
    pub organization_id: Uuid,
    pub user_id: Option<i32>,
    pub action: Option<String>,
    pub entity_type: Option<String>,
}

// ─── Allowed tuples — pinned at the module level so misspellings ─────────
// ─── fail fast in the client rather than at the DB CHECK. ────────────────

pub const ALLOWED_ACTIONS: &[&str] = &["read","write","delete","approve","apply"];
pub const ALLOWED_SCOPE_TYPES: &[&str] = &["Global","Region","Site","Building","EntityId"];

fn validate_action(a: &str) -> Result<(), EngineError> {
    if ALLOWED_ACTIONS.contains(&a) { Ok(()) }
    else { Err(EngineError::bad_request(format!(
        "action '{a}' must be one of: {}", ALLOWED_ACTIONS.join(","))))
    }
}

fn validate_scope(scope_type: &str, scope_entity_id: Option<Uuid>) -> Result<(), EngineError> {
    if !ALLOWED_SCOPE_TYPES.contains(&scope_type) {
        return Err(EngineError::bad_request(format!(
            "scope_type '{scope_type}' must be one of: {}", ALLOWED_SCOPE_TYPES.join(","))));
    }
    // Schema enforces this via CHECK but surfacing at the API layer
    // gives a clearer error and avoids a DB round-trip.
    match (scope_type, scope_entity_id) {
        ("Global", Some(_)) => Err(EngineError::bad_request(
            "Global scope must not carry a scope_entity_id")),
        (s, None) if s != "Global" => Err(EngineError::bad_request(format!(
            "scope_type '{s}' requires scope_entity_id"))),
        _ => Ok(()),
    }
}

// ─── Repo ────────────────────────────────────────────────────────────────

#[derive(Clone)]
pub struct ScopeGrantRepo {
    pool: PgPool,
}

impl ScopeGrantRepo {
    pub fn new(pool: PgPool) -> Self { Self { pool } }

    pub async fn list(&self, q: &ListScopeGrantsQuery) -> Result<Vec<ScopeGrant>, EngineError> {
        let rows: Vec<ScopeGrant> = sqlx::query_as(
            "SELECT id, organization_id, user_id, action, entity_type,
                    scope_type, scope_entity_id, status::text AS status,
                    version, created_at, updated_at, notes
               FROM net.scope_grant
              WHERE organization_id = $1
                AND deleted_at IS NULL
                AND ($2::int  IS NULL OR user_id     = $2)
                AND ($3::text IS NULL OR action      = $3)
                AND ($4::text IS NULL OR entity_type = $4)
              ORDER BY user_id, entity_type, action, scope_type")
            .bind(q.organization_id)
            .bind(q.user_id)
            .bind(q.action.as_deref())
            .bind(q.entity_type.as_deref())
            .fetch_all(&self.pool)
            .await?;
        Ok(rows)
    }

    pub async fn get(&self, id: Uuid, org_id: Uuid) -> Result<ScopeGrant, EngineError> {
        let row: Option<ScopeGrant> = sqlx::query_as(
            "SELECT id, organization_id, user_id, action, entity_type,
                    scope_type, scope_entity_id, status::text AS status,
                    version, created_at, updated_at, notes
               FROM net.scope_grant
              WHERE id = $1 AND organization_id = $2 AND deleted_at IS NULL")
            .bind(id).bind(org_id)
            .fetch_optional(&self.pool)
            .await?;
        row.ok_or_else(|| EngineError::container_not_found("scope_grant", id))
    }

    pub async fn create(
        &self,
        body: &CreateScopeGrantBody,
        user_id: Option<i32>,
    ) -> Result<ScopeGrant, EngineError> {
        validate_action(&body.action)?;
        validate_scope(&body.scope_type, body.scope_entity_id)?;

        let mut tx = self.pool.begin().await?;
        let row: ScopeGrant = sqlx::query_as(
            "INSERT INTO net.scope_grant
                (organization_id, user_id, action, entity_type, scope_type,
                 scope_entity_id, notes, status, lock_state, created_by, updated_by)
             VALUES
                ($1, $2, $3, $4, $5, $6, $7,
                 'Active'::net.entity_status, 'Open'::net.lock_state, $8, $8)
             RETURNING id, organization_id, user_id, action, entity_type,
                       scope_type, scope_entity_id, status::text AS status,
                       version, created_at, updated_at, notes")
            .bind(body.organization_id)
            .bind(body.user_id)
            .bind(&body.action)
            .bind(&body.entity_type)
            .bind(&body.scope_type)
            .bind(body.scope_entity_id)
            .bind(body.notes.as_deref())
            .bind(user_id)
            .fetch_one(&mut *tx).await?;

        audit::append_tx(&mut tx, &AuditEvent {
            organization_id: row.organization_id,
            source_service: "networking-engine",
            entity_type: "ScopeGrant",
            entity_id: Some(row.id),
            action: "Created",
            actor_user_id: user_id,
            actor_display: None, client_ip: None, correlation_id: None,
            details: serde_json::json!({
                "granted_user_id": row.user_id,
                "grant_action":    row.action,
                "grant_entity_type": row.entity_type,
                "scope_type":      row.scope_type,
                "scope_entity_id": row.scope_entity_id,
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
        let mut tx = self.pool.begin().await?;
        let affected = sqlx::query(
            "UPDATE net.scope_grant
                SET deleted_at = now(), deleted_by = $3
              WHERE id = $1 AND organization_id = $2 AND deleted_at IS NULL")
            .bind(id).bind(org_id).bind(user_id)
            .execute(&mut *tx).await?.rows_affected();
        if affected == 0 {
            return Err(EngineError::container_not_found("scope_grant", id));
        }
        audit::append_tx(&mut tx, &AuditEvent {
            organization_id: org_id,
            source_service: "networking-engine",
            entity_type: "ScopeGrant",
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

// ─── Resolver ────────────────────────────────────────────────────────────

/// Resolver result — expands to carry the matching grant id for
/// audit purposes later (follow-on slice). For now only `allowed`
/// is used by callers.
#[derive(Debug, Clone, Serialize, PartialEq, Eq)]
#[serde(rename_all = "camelCase")]
pub struct PermissionDecision {
    pub allowed: bool,
    /// id of the grant that matched, for "why was this allowed?"
    /// audit logging. None when `allowed = false`.
    pub matched_grant_id: Option<Uuid>,
}

/// Check whether `user_id` may perform `action` on `entity_type` /
/// `entity_id` in tenant `org_id`.
///
/// v1 only matches Global + EntityId scopes. Region / Site /
/// Building scopes are stored but don't yet contribute to the
/// decision — that expansion lands in a follow-on slice so this
/// first resolver stays simple + auditable.
pub async fn has_permission(
    pool: &PgPool,
    org_id: Uuid,
    user_id: i32,
    action: &str,
    entity_type: &str,
    entity_id: Option<Uuid>,
) -> Result<PermissionDecision, EngineError> {
    // Single query checks both scope flavours — Global (no
    // scope_entity_id) OR EntityId (scope_entity_id = entity_id).
    // LIMIT 1 gives us the first-matching grant for the audit id.
    let row: Option<(Uuid,)> = sqlx::query_as(
        "SELECT id FROM net.scope_grant
          WHERE organization_id = $1
            AND user_id         = $2
            AND action          = $3
            AND entity_type     = $4
            AND deleted_at      IS NULL
            AND (
                scope_type = 'Global'
                OR (scope_type = 'EntityId' AND scope_entity_id = $5)
            )
          LIMIT 1")
        .bind(org_id)
        .bind(user_id)
        .bind(action)
        .bind(entity_type)
        .bind(entity_id)
        .fetch_optional(pool)
        .await?;
    Ok(match row {
        Some((grant_id,)) => PermissionDecision { allowed: true, matched_grant_id: Some(grant_id) },
        None              => PermissionDecision { allowed: false, matched_grant_id: None },
    })
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn validate_action_accepts_every_documented_action() {
        for a in ALLOWED_ACTIONS { assert!(validate_action(a).is_ok(), "{a}"); }
    }

    #[test]
    fn validate_action_rejects_typos_and_injection() {
        for a in ["Read", "READ", "create", "", "'; DROP TABLE net.scope_grant;--"] {
            assert!(validate_action(a).is_err(), "'{a}' should be rejected");
        }
    }

    #[test]
    fn validate_scope_global_requires_null_entity_id() {
        assert!(validate_scope("Global", None).is_ok());
        // A non-NULL scope_entity_id on a Global grant is an ambiguous
        // tuple — reject at the API rather than letting the DB CHECK
        // surface a cryptic constraint error.
        let err = validate_scope("Global", Some(Uuid::nil())).unwrap_err().to_string();
        assert!(err.contains("must not carry"), "err: {err}");
    }

    #[test]
    fn validate_scope_non_global_requires_entity_id() {
        for s in ["Region","Site","Building","EntityId"] {
            assert!(validate_scope(s, Some(Uuid::nil())).is_ok(), "{s} with id");
            let err = validate_scope(s, None).unwrap_err().to_string();
            assert!(err.contains("requires scope_entity_id"), "{s} without id: {err}");
        }
    }

    #[test]
    fn validate_scope_rejects_unknown_scope_type() {
        for s in ["global", "RegionX", "Tenant", "", "' OR '1'='1"] {
            assert!(validate_scope(s, Some(Uuid::nil())).is_err(), "{s}");
        }
    }

    #[test]
    fn permission_decision_serialises_camelcase() {
        let d = PermissionDecision { allowed: true, matched_grant_id: Some(Uuid::nil()) };
        let json = serde_json::to_string(&d).expect("serialises");
        assert!(json.contains("\"allowed\":true"));
        assert!(json.contains("matchedGrantId"),
            "matchedGrantId must be camelCase: {json}");
    }

    #[test]
    fn permission_decision_serialises_denied_null_matched_id() {
        let d = PermissionDecision { allowed: false, matched_grant_id: None };
        let json = serde_json::to_string(&d).expect("serialises");
        assert!(json.contains("\"allowed\":false"));
        assert!(json.contains("\"matchedGrantId\":null"),
            "matchedGrantId must serialise as null when denied (so callers can distinguish 'allowed by global' from 'allowed by specific id'): {json}");
    }
}
