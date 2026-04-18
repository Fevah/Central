//! Change Sets — Phase 8a governance.
//!
//! A Change Set groups related entity mutations behind one approvable
//! envelope. Lifecycle: `Draft → Submitted → (Approved | Rejected) →
//! (Applied | Cancelled | RolledBack)`. This slice ships the draft side:
//! create, list, get, add/remove items, submit. Approve / reject / apply /
//! rollback arrive in follow-on slices.
//!
//! ## Invariants enforced here
//!
//! - Items are mutable only while the parent Set is `Draft`. Once
//!   `Submitted`, adding / editing items requires cancelling and
//!   re-drafting.
//! - `submit` is a one-shot transition: `Draft → Submitted`. Anything else
//!   (resubmitting an approved Set, submitting a rejected one, submitting
//!   an empty Draft) errors out.
//! - `item_order` is dense-1-based — engine assigns it on add so callers
//!   can't leave gaps.
//!
//! All mutations stamp audit entries with `correlation_id` set to the Set's
//! own id so forensic queries can join back cleanly.

use chrono::{DateTime, Utc};
use serde::{Deserialize, Serialize};
use sqlx::PgPool;
use uuid::Uuid;

use crate::audit::{self, AuditEvent};
use crate::error::EngineError;

// ─── Status + action enums ───────────────────────────────────────────────

#[derive(Debug, Copy, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "PascalCase")]
pub enum ChangeSetStatus {
    Draft, Submitted, Approved, Rejected, Applied, RolledBack, Cancelled,
}

impl ChangeSetStatus {
    pub fn as_str(&self) -> &'static str {
        match self {
            Self::Draft => "Draft", Self::Submitted => "Submitted",
            Self::Approved => "Approved", Self::Rejected => "Rejected",
            Self::Applied => "Applied", Self::RolledBack => "RolledBack",
            Self::Cancelled => "Cancelled",
        }
    }

    pub fn from_db(s: &str) -> Result<Self, EngineError> {
        match s {
            "Draft" => Ok(Self::Draft),
            "Submitted" => Ok(Self::Submitted),
            "Approved" => Ok(Self::Approved),
            "Rejected" => Ok(Self::Rejected),
            "Applied" => Ok(Self::Applied),
            "RolledBack" => Ok(Self::RolledBack),
            "Cancelled" => Ok(Self::Cancelled),
            other => Err(EngineError::bad_request(format!(
                "Unknown change_set_status '{other}'"))),
        }
    }

    #[allow(dead_code)] // Consumed by approve/reject/cancel paths in the next slice.
    pub fn is_terminal(&self) -> bool {
        matches!(self, Self::Rejected | Self::Applied | Self::Cancelled)
    }
}

#[derive(Debug, Copy, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "PascalCase")]
pub enum ChangeSetAction { Create, Update, Delete, Rename }

impl ChangeSetAction {
    pub fn as_str(&self) -> &'static str {
        match self {
            Self::Create => "Create", Self::Update => "Update",
            Self::Delete => "Delete", Self::Rename => "Rename",
        }
    }

    pub fn from_db(s: &str) -> Result<Self, EngineError> {
        match s {
            "Create" => Ok(Self::Create), "Update" => Ok(Self::Update),
            "Delete" => Ok(Self::Delete), "Rename" => Ok(Self::Rename),
            other => Err(EngineError::bad_request(format!(
                "Unknown change_set_action '{other}'"))),
        }
    }
}

// ─── DTOs ────────────────────────────────────────────────────────────────

#[derive(Debug, Clone, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct ChangeSet {
    pub id: Uuid,
    pub organization_id: Uuid,
    pub title: String,
    pub description: Option<String>,
    pub status: ChangeSetStatus,
    pub requested_by: Option<i32>,
    pub requested_by_display: Option<String>,
    pub submitted_by: Option<i32>,
    pub submitted_at: Option<DateTime<Utc>>,
    pub approved_at: Option<DateTime<Utc>>,
    pub applied_at: Option<DateTime<Utc>>,
    pub rolled_back_at: Option<DateTime<Utc>>,
    pub cancelled_at: Option<DateTime<Utc>>,
    pub required_approvals: Option<i32>,
    pub correlation_id: Uuid,
    pub version: i32,
    pub item_count: i64,
    pub created_at: DateTime<Utc>,
    pub updated_at: DateTime<Utc>,
}

#[derive(Debug, Clone, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct ChangeSetItem {
    pub id: Uuid,
    pub change_set_id: Uuid,
    pub item_order: i32,
    pub entity_type: String,
    pub entity_id: Option<Uuid>,
    pub action: ChangeSetAction,
    pub before_json: Option<serde_json::Value>,
    pub after_json: Option<serde_json::Value>,
    pub expected_version: Option<i32>,
    pub applied_at: Option<DateTime<Utc>>,
    pub apply_error: Option<String>,
    pub notes: Option<String>,
    pub created_at: DateTime<Utc>,
}

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct CreateChangeSetBody {
    pub organization_id: Uuid,
    pub title: String,
    pub description: Option<String>,
    pub requested_by_display: Option<String>,
}

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct AddItemBody {
    pub entity_type: String,
    pub entity_id: Option<Uuid>,
    pub action: ChangeSetAction,
    pub before_json: Option<serde_json::Value>,
    pub after_json: Option<serde_json::Value>,
    pub expected_version: Option<i32>,
    pub notes: Option<String>,
}

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct SubmitBody {
    /// How many approvals this Set needs before apply is possible. The
    /// per-entity-type policy table lands in a follow-on slice; for now
    /// the caller supplies the threshold explicitly. Capped at >=1 to
    /// prevent accidental auto-approval by passing 0.
    #[serde(default = "default_required_approvals")]
    pub required_approvals: i32,
}

fn default_required_approvals() -> i32 { 1 }

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct ListChangeSetsQuery {
    pub organization_id: Uuid,
    pub status: Option<String>,
    #[serde(default = "default_list_limit")]
    pub limit: i64,
}

fn default_list_limit() -> i64 { 50 }

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct GetChangeSetQuery { pub organization_id: Uuid }

#[derive(Debug, Default, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct CancelBody {
    pub notes: Option<String>,
}

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct DecisionBody {
    pub decision: ChangeSetDecision,
    pub approver_display: Option<String>,
    pub notes: Option<String>,
}

#[derive(Debug, Copy, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "PascalCase")]
pub enum ChangeSetDecision { Approve, Reject }

impl ChangeSetDecision {
    pub fn as_str(&self) -> &'static str {
        match self { Self::Approve => "Approve", Self::Reject => "Reject" }
    }
}

#[derive(Debug, Clone, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct ApprovalRow {
    pub id: Uuid,
    pub change_set_id: Uuid,
    pub approver_user_id: i32,
    pub approver_display: Option<String>,
    pub decision: ChangeSetDecision,
    pub decided_at: DateTime<Utc>,
    pub notes: Option<String>,
}

#[derive(Debug, Clone, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct DecisionResult {
    pub approval: ApprovalRow,
    pub change_set: ChangeSet,
    /// How many Approves the Set has now vs. its threshold.
    pub approvals_count: i64,
    pub approvals_required: i32,
}

// ─── Repo ────────────────────────────────────────────────────────────────

#[derive(Clone)]
pub struct ChangeSetRepo { pool: PgPool }

impl ChangeSetRepo {
    pub fn new(pool: PgPool) -> Self { Self { pool } }

    pub async fn create(
        &self,
        body: &CreateChangeSetBody,
        user_id: Option<i32>,
    ) -> Result<ChangeSet, EngineError> {
        if body.title.trim().is_empty() {
            return Err(EngineError::bad_request("Change Set title is required"));
        }

        let mut tx = self.pool.begin().await?;
        let row = sqlx::query_as::<_, ChangeSetRow>(
            "INSERT INTO net.change_set
                (organization_id, title, description, status,
                 requested_by, requested_by_display,
                 lock_state, created_by, updated_by)
             VALUES ($1, $2, $3, 'Draft'::net.change_set_status,
                     $4, $5, 'Open'::net.lock_state, $4, $4)
             RETURNING id, organization_id, title, description,
                       status::text AS status, requested_by, requested_by_display,
                       submitted_by, submitted_at, approved_at, applied_at,
                       rolled_back_at, cancelled_at, required_approvals,
                       correlation_id, version, created_at, updated_at")
            .bind(body.organization_id)
            .bind(&body.title)
            .bind(body.description.as_deref())
            .bind(user_id)
            .bind(body.requested_by_display.as_deref())
            .fetch_one(&mut *tx)
            .await?;

        audit::append_tx(&mut tx, &AuditEvent {
            organization_id: body.organization_id,
            source_service: "networking-engine",
            entity_type: "ChangeSet",
            entity_id: Some(row.id),
            action: "Drafted",
            actor_user_id: user_id,
            actor_display: body.requested_by_display.as_deref(),
            client_ip: None,
            correlation_id: Some(row.correlation_id),
            details: serde_json::json!({ "title": row.title }),
        }).await?;

        tx.commit().await?;
        row.into_dto(0)
    }

    pub async fn list(&self, q: &ListChangeSetsQuery) -> Result<Vec<ChangeSet>, EngineError> {
        let limit = q.limit.clamp(1, 500);
        let rows: Vec<ChangeSetRow> = sqlx::query_as(
            "SELECT cs.id, cs.organization_id, cs.title, cs.description,
                    cs.status::text AS status, cs.requested_by, cs.requested_by_display,
                    cs.submitted_by, cs.submitted_at, cs.approved_at, cs.applied_at,
                    cs.rolled_back_at, cs.cancelled_at, cs.required_approvals,
                    cs.correlation_id, cs.version, cs.created_at, cs.updated_at
               FROM net.change_set cs
              WHERE cs.organization_id = $1
                AND cs.deleted_at IS NULL
                AND ($2::text IS NULL OR cs.status::text = $2)
              ORDER BY cs.created_at DESC
              LIMIT $3")
            .bind(q.organization_id)
            .bind(q.status.as_deref())
            .bind(limit)
            .fetch_all(&self.pool)
            .await?;
        // Item counts in a single batch query so the list endpoint stays O(1) round-trips.
        let ids: Vec<Uuid> = rows.iter().map(|r| r.id).collect();
        let counts: Vec<(Uuid, i64)> = if ids.is_empty() {
            Vec::new()
        } else {
            sqlx::query_as(
                "SELECT change_set_id, COUNT(*)::bigint
                   FROM net.change_set_item
                  WHERE change_set_id = ANY($1) AND deleted_at IS NULL
                  GROUP BY change_set_id")
                .bind(&ids)
                .fetch_all(&self.pool)
                .await?
        };
        let mut by_id = std::collections::HashMap::new();
        for (k, v) in counts { by_id.insert(k, v); }

        rows.into_iter()
            .map(|r| { let c = by_id.get(&r.id).copied().unwrap_or(0); r.into_dto(c) })
            .collect()
    }

    /// Every approval decision recorded on a Change Set, chronologically.
    /// Used by the WPF detail dialog's Approvals tab and by forensic
    /// queries that want a quick "who decided what" view without walking
    /// the audit log.
    pub async fn list_approvals(
        &self,
        set_id: Uuid,
        org_id: Uuid,
    ) -> Result<Vec<ApprovalRow>, EngineError> {
        // Guard: the set must belong to this tenant. Without this, an
        // approval id leakage would let a probe across tenants confirm
        // the existence of specific set ids.
        let exists: Option<(Uuid,)> = sqlx::query_as(
            "SELECT id FROM net.change_set
              WHERE id = $1 AND organization_id = $2 AND deleted_at IS NULL")
            .bind(set_id).bind(org_id).fetch_optional(&self.pool).await?;
        if exists.is_none() {
            return Err(EngineError::container_not_found("change_set", set_id));
        }

        let rows: Vec<ApprovalRowDb> = sqlx::query_as(
            "SELECT id, change_set_id, approver_user_id, approver_display,
                    decision::text AS decision, decided_at, notes
               FROM net.change_set_approval
              WHERE change_set_id = $1 AND organization_id = $2
              ORDER BY decided_at ASC")
            .bind(set_id).bind(org_id).fetch_all(&self.pool).await?;

        rows.into_iter().map(|r| r.into_dto()).collect()
    }

    pub async fn get(&self, id: Uuid, org_id: Uuid) -> Result<(ChangeSet, Vec<ChangeSetItem>), EngineError> {
        let row: Option<ChangeSetRow> = sqlx::query_as(
            "SELECT id, organization_id, title, description,
                    status::text AS status, requested_by, requested_by_display,
                    submitted_by, submitted_at, approved_at, applied_at,
                    rolled_back_at, cancelled_at, required_approvals,
                    correlation_id, version, created_at, updated_at
               FROM net.change_set
              WHERE id = $1 AND organization_id = $2 AND deleted_at IS NULL")
            .bind(id)
            .bind(org_id)
            .fetch_optional(&self.pool)
            .await?;
        let row = row.ok_or_else(|| EngineError::container_not_found("change_set", id))?;

        let items: Vec<ChangeSetItemRow> = sqlx::query_as(
            "SELECT id, change_set_id, item_order, entity_type, entity_id,
                    action::text AS action, before_json, after_json,
                    expected_version, applied_at, apply_error, notes, created_at
               FROM net.change_set_item
              WHERE change_set_id = $1 AND deleted_at IS NULL
              ORDER BY item_order")
            .bind(id)
            .fetch_all(&self.pool)
            .await?;
        let count = items.len() as i64;
        let dtos = items.into_iter().map(|r| r.into_dto()).collect::<Result<Vec<_>, _>>()?;
        Ok((row.into_dto(count)?, dtos))
    }

    pub async fn add_item(
        &self,
        set_id: Uuid,
        org_id: Uuid,
        body: &AddItemBody,
        user_id: Option<i32>,
    ) -> Result<ChangeSetItem, EngineError> {
        validate_item(body)?;

        let mut tx = self.pool.begin().await?;

        // Guard: parent Set must be Draft. Select with FOR UPDATE so a
        // concurrent submit can't sneak in between our read and our
        // insert.
        let parent: Option<(String, Uuid)> = sqlx::query_as(
            "SELECT status::text, correlation_id
               FROM net.change_set
              WHERE id = $1 AND organization_id = $2 AND deleted_at IS NULL
              FOR UPDATE")
            .bind(set_id)
            .bind(org_id)
            .fetch_optional(&mut *tx)
            .await?;
        let (status_str, correlation_id) = parent
            .ok_or_else(|| EngineError::container_not_found("change_set", set_id))?;
        let status = ChangeSetStatus::from_db(&status_str)?;
        if status != ChangeSetStatus::Draft {
            return Err(EngineError::bad_request(format!(
                "Cannot add items to a Change Set in status '{}'. \
                 Items are editable only in Draft.", status.as_str())));
        }

        // Dense item_order: max(order) + 1, starting at 1. Concurrent inserts
        // are serialised by the FOR UPDATE lock on the parent above.
        let next_order: i32 = sqlx::query_scalar(
            "SELECT COALESCE(MAX(item_order), 0) + 1
               FROM net.change_set_item
              WHERE change_set_id = $1 AND deleted_at IS NULL")
            .bind(set_id)
            .fetch_one(&mut *tx)
            .await?;

        let row: ChangeSetItemRow = sqlx::query_as(
            "INSERT INTO net.change_set_item
                (organization_id, change_set_id, item_order, entity_type,
                 entity_id, action, before_json, after_json,
                 expected_version, notes)
             VALUES ($1, $2, $3, $4, $5, $6::net.change_set_action, $7, $8, $9, $10)
             RETURNING id, change_set_id, item_order, entity_type, entity_id,
                       action::text AS action, before_json, after_json,
                       expected_version, applied_at, apply_error, notes, created_at")
            .bind(org_id)
            .bind(set_id)
            .bind(next_order)
            .bind(&body.entity_type)
            .bind(body.entity_id)
            .bind(body.action.as_str())
            .bind(&body.before_json)
            .bind(&body.after_json)
            .bind(body.expected_version)
            .bind(body.notes.as_deref())
            .fetch_one(&mut *tx)
            .await?;

        audit::append_tx(&mut tx, &AuditEvent {
            organization_id: org_id,
            source_service: "networking-engine",
            entity_type: "ChangeSetItem",
            entity_id: Some(row.id),
            action: "ItemAdded",
            actor_user_id: user_id,
            actor_display: None,
            client_ip: None,
            correlation_id: Some(correlation_id),
            details: serde_json::json!({
                "change_set_id": set_id,
                "item_order": next_order,
                "target_entity_type": body.entity_type,
                "target_action": body.action.as_str(),
            }),
        }).await?;

        tx.commit().await?;
        row.into_dto()
    }

    /// Apply an Approved Set — execute every item in item_order, stamping
    /// per-item `applied_at` + audit entries (correlation_id = Set's
    /// correlation_id). On all-success, Set transitions Approved → Applied.
    /// On any failure, the Set stays Approved and apply_error is filled on
    /// the offending item row — admins can retry apply and already-applied
    /// items are skipped (idempotent).
    ///
    /// This slice ships the Device / Rename concrete path only. Other
    /// (entity_type, action) pairs are surfaced as per-item errors
    /// ("NotImplementedYet") rather than failing the whole Set, so a
    /// mixed Set's Device renames still land while other item types
    /// queue for future slices.
    pub async fn apply(
        &self,
        set_id: Uuid,
        org_id: Uuid,
        user_id: Option<i32>,
    ) -> Result<ApplyResult, EngineError> {
        // Precondition: Set must be Approved. Load under FOR UPDATE so a
        // concurrent cancel / rollback can't race the apply.
        let mut guard_tx = self.pool.begin().await?;
        let parent: Option<ChangeSetRow> = sqlx::query_as(
            "SELECT id, organization_id, title, description,
                    status::text AS status, requested_by, requested_by_display,
                    submitted_by, submitted_at, approved_at, applied_at,
                    rolled_back_at, cancelled_at, required_approvals,
                    correlation_id, version, created_at, updated_at
               FROM net.change_set
              WHERE id = $1 AND organization_id = $2 AND deleted_at IS NULL
              FOR UPDATE")
            .bind(set_id)
            .bind(org_id)
            .fetch_optional(&mut *guard_tx)
            .await?;
        let parent = parent
            .ok_or_else(|| EngineError::container_not_found("change_set", set_id))?;
        let status = ChangeSetStatus::from_db(&parent.status)?;
        if status != ChangeSetStatus::Approved {
            return Err(EngineError::bad_request(format!(
                "Cannot apply a Change Set in status '{}'. Only Approved Sets \
                 are eligible for apply.", status.as_str())));
        }
        guard_tx.commit().await?; // Release the FOR UPDATE before the long per-item loop.
        let correlation_id = parent.correlation_id;

        // Pull items in order, skipping already-applied ones (idempotent retry).
        let items: Vec<ChangeSetItemRow> = sqlx::query_as(
            "SELECT id, change_set_id, item_order, entity_type, entity_id,
                    action::text AS action, before_json, after_json,
                    expected_version, applied_at, apply_error, notes, created_at
               FROM net.change_set_item
              WHERE change_set_id = $1 AND deleted_at IS NULL
              ORDER BY item_order")
            .bind(set_id)
            .fetch_all(&self.pool)
            .await?;

        let mut outcomes = Vec::with_capacity(items.len());
        for item_row in items {
            let already_done = item_row.applied_at.is_some();
            let item = item_row.into_dto()?;
            if already_done {
                outcomes.push(ApplyItemOutcome {
                    item_id: item.id, item_order: item.item_order,
                    success: true, error: None, skipped: true,
                });
                continue;
            }
            let outcome = apply_one(&self.pool, &item, org_id, correlation_id, user_id).await;
            outcomes.push(outcome);
        }

        let all_ok = outcomes.iter().all(|o| o.success);
        let final_set = if all_ok {
            // Transition to Applied atomically with an audit entry.
            let mut tx = self.pool.begin().await?;
            let row: ChangeSetRow = sqlx::query_as(
                "UPDATE net.change_set
                    SET status     = 'Applied'::net.change_set_status,
                        applied_at = now(),
                        updated_at = now(),
                        updated_by = $3,
                        version    = version + 1
                  WHERE id = $1 AND organization_id = $2
                    AND status = 'Approved'::net.change_set_status
                  RETURNING id, organization_id, title, description,
                            status::text AS status, requested_by, requested_by_display,
                            submitted_by, submitted_at, approved_at, applied_at,
                            rolled_back_at, cancelled_at, required_approvals,
                            correlation_id, version, created_at, updated_at")
                .bind(set_id)
                .bind(org_id)
                .bind(user_id)
                .fetch_one(&mut *tx)
                .await?;
            audit::append_tx(&mut tx, &AuditEvent {
                organization_id: org_id,
                source_service: "networking-engine",
                entity_type: "ChangeSet",
                entity_id: Some(set_id),
                action: "Applied",
                actor_user_id: user_id,
                actor_display: None,
                client_ip: None,
                correlation_id: Some(correlation_id),
                details: serde_json::json!({ "items_applied": outcomes.len() }),
            }).await?;
            tx.commit().await?;
            row
        } else {
            parent
        };

        let item_count = outcomes.len() as i64;
        let applied_count = outcomes.iter().filter(|o| o.success).count();
        let failed_count = outcomes.iter().filter(|o| !o.success).count();

        Ok(ApplyResult {
            change_set: final_set.into_dto(item_count)?,
            outcomes,
            applied_count,
            failed_count,
        })
    }

    /// Roll back an Applied Set — walk items in reverse order and reverse
    /// each one. Per-item transaction (same as apply) so partial failures
    /// don't lose progress. Transitions Applied → RolledBack when every
    /// reversible item is reversed.
    ///
    /// The reversal is shallow: Device / Rename becomes a rename from
    /// `after.hostname` back to `before.hostname`. More-destructive
    /// reversals (undelete a row, revert a multi-field update) arrive
    /// in follow-on slices; unknown combinations surface as per-item
    /// RollbackNotImplemented and leave the Set at Applied.
    ///
    /// Note: this is a forward rollback — we make a NEW audit entry for
    /// each reverse operation (action = 'RolledBack'), we never mutate
    /// or hide the original apply entries. The forensic chain stays
    /// intact: admins see the rename out + the rename back + the Set
    /// transitioning Applied -> RolledBack, all threaded by
    /// correlation_id.
    pub async fn rollback(
        &self,
        set_id: Uuid,
        org_id: Uuid,
        user_id: Option<i32>,
    ) -> Result<RollbackResult, EngineError> {
        let mut guard_tx = self.pool.begin().await?;
        let parent: Option<ChangeSetRow> = sqlx::query_as(
            "SELECT id, organization_id, title, description,
                    status::text AS status, requested_by, requested_by_display,
                    submitted_by, submitted_at, approved_at, applied_at,
                    rolled_back_at, cancelled_at, required_approvals,
                    correlation_id, version, created_at, updated_at
               FROM net.change_set
              WHERE id = $1 AND organization_id = $2 AND deleted_at IS NULL
              FOR UPDATE")
            .bind(set_id)
            .bind(org_id)
            .fetch_optional(&mut *guard_tx)
            .await?;
        let parent = parent
            .ok_or_else(|| EngineError::container_not_found("change_set", set_id))?;
        let status = ChangeSetStatus::from_db(&parent.status)?;
        if status != ChangeSetStatus::Applied {
            return Err(EngineError::bad_request(format!(
                "Cannot roll back a Change Set in status '{}'. Only Applied \
                 Sets are rollable.", status.as_str())));
        }
        guard_tx.commit().await?;
        let correlation_id = parent.correlation_id;

        // Reverse order — rollback walks from last-applied to first-applied
        // so dependencies are torn down in the opposite order to setup.
        let items: Vec<ChangeSetItemRow> = sqlx::query_as(
            "SELECT id, change_set_id, item_order, entity_type, entity_id,
                    action::text AS action, before_json, after_json,
                    expected_version, applied_at, apply_error, notes, created_at
               FROM net.change_set_item
              WHERE change_set_id = $1 AND deleted_at IS NULL
              ORDER BY item_order DESC")
            .bind(set_id)
            .fetch_all(&self.pool)
            .await?;

        let mut outcomes = Vec::with_capacity(items.len());
        for item_row in items {
            let was_applied = item_row.applied_at.is_some();
            let item = item_row.into_dto()?;
            if !was_applied {
                // Item never applied (either Set apply was partial, or the
                // item was added but the prior apply errored on it). Mark as
                // a no-op rollback — nothing to reverse.
                outcomes.push(RollbackItemOutcome {
                    item_id: item.id, item_order: item.item_order,
                    success: true, error: None, skipped: true,
                });
                continue;
            }
            let outcome = rollback_one(&self.pool, &item, org_id, correlation_id, user_id).await;
            outcomes.push(outcome);
        }

        let all_ok = outcomes.iter().all(|o| o.success);
        let final_set = if all_ok {
            let mut tx = self.pool.begin().await?;
            let row: ChangeSetRow = sqlx::query_as(
                "UPDATE net.change_set
                    SET status         = 'RolledBack'::net.change_set_status,
                        rolled_back_at = now(),
                        updated_at     = now(),
                        updated_by     = $3,
                        version        = version + 1
                  WHERE id = $1 AND organization_id = $2
                    AND status = 'Applied'::net.change_set_status
                  RETURNING id, organization_id, title, description,
                            status::text AS status, requested_by, requested_by_display,
                            submitted_by, submitted_at, approved_at, applied_at,
                            rolled_back_at, cancelled_at, required_approvals,
                            correlation_id, version, created_at, updated_at")
                .bind(set_id)
                .bind(org_id)
                .bind(user_id)
                .fetch_one(&mut *tx)
                .await?;
            audit::append_tx(&mut tx, &AuditEvent {
                organization_id: org_id,
                source_service: "networking-engine",
                entity_type: "ChangeSet",
                entity_id: Some(set_id),
                action: "RolledBack",
                actor_user_id: user_id,
                actor_display: None,
                client_ip: None,
                correlation_id: Some(correlation_id),
                details: serde_json::json!({ "items_reverted": outcomes.iter().filter(|o| !o.skipped).count() }),
            }).await?;
            tx.commit().await?;
            row
        } else {
            parent
        };

        let item_count = outcomes.len() as i64;
        let reverted_count = outcomes.iter().filter(|o| o.success && !o.skipped).count();
        let failed_count = outcomes.iter().filter(|o| !o.success).count();

        Ok(RollbackResult {
            change_set: final_set.into_dto(item_count)?,
            outcomes,
            reverted_count,
            failed_count,
        })
    }

    /// Withdraw a Set before it's applied. Valid from any non-terminal
    /// non-Applied state: `Draft`, `Submitted`, or `Approved`. `Applied`
    /// Sets are reversed via `rollback`, not `cancel`.
    pub async fn cancel(
        &self,
        set_id: Uuid,
        org_id: Uuid,
        user_id: Option<i32>,
        notes: Option<&str>,
    ) -> Result<ChangeSet, EngineError> {
        let mut tx = self.pool.begin().await?;

        // Transition via a WHERE clause listing the states that permit
        // cancellation. Rejected / Applied / Cancelled / RolledBack all
        // return 0 rows affected, which we surface as a clean error.
        let row: Option<ChangeSetRow> = sqlx::query_as(
            "UPDATE net.change_set
                SET status       = 'Cancelled'::net.change_set_status,
                    cancelled_at = now(),
                    updated_at   = now(),
                    updated_by   = $3,
                    version      = version + 1
              WHERE id = $1 AND organization_id = $2
                AND status::text IN ('Draft','Submitted','Approved')
                AND deleted_at IS NULL
              RETURNING id, organization_id, title, description,
                        status::text AS status, requested_by, requested_by_display,
                        submitted_by, submitted_at, approved_at, applied_at,
                        rolled_back_at, cancelled_at, required_approvals,
                        correlation_id, version, created_at, updated_at")
            .bind(set_id)
            .bind(org_id)
            .bind(user_id)
            .fetch_optional(&mut *tx)
            .await?;
        let row = row.ok_or_else(|| EngineError::bad_request(
            "Change Set not cancellable — must be in Draft / Submitted / Approved. \
             Applied Sets use rollback; terminal states cannot be cancelled."))?;

        audit::append_tx(&mut tx, &AuditEvent {
            organization_id: org_id,
            source_service: "networking-engine",
            entity_type: "ChangeSet",
            entity_id: Some(row.id),
            action: "Cancelled",
            actor_user_id: user_id,
            actor_display: None,
            client_ip: None,
            correlation_id: Some(row.correlation_id),
            details: serde_json::json!({ "notes": notes }),
        }).await?;

        let item_count: i64 = sqlx::query_scalar(
            "SELECT COUNT(*)::bigint FROM net.change_set_item
              WHERE change_set_id = $1 AND deleted_at IS NULL")
            .bind(set_id)
            .fetch_one(&mut *tx)
            .await?;

        tx.commit().await?;
        row.into_dto(item_count)
    }

    /// Record an approval or rejection decision. Rules:
    ///
    /// - Parent Set must be in `Submitted`. Any other state is rejected.
    /// - Approver cannot be the same user who requested the Set — a
    ///   self-approval would defeat the point of the gate.
    /// - One decision per `(Set, approver)`. The UNIQUE index catches
    ///   double-decisions; we surface a clean error rather than leaking
    ///   the constraint message.
    /// - Any single `Reject` flips the Set to `Rejected` (terminal) —
    ///   subsequent Approve decisions on the same Set error out.
    /// - `Approve` count reaching `required_approvals` flips to `Approved`.
    ///   Further decisions after the threshold error out (the Set is no
    ///   longer in Submitted).
    pub async fn record_decision(
        &self,
        set_id: Uuid,
        org_id: Uuid,
        approver_user_id: i32,
        body: &DecisionBody,
    ) -> Result<DecisionResult, EngineError> {
        let mut tx = self.pool.begin().await?;

        // FOR UPDATE so the parent's state + approval count can't race
        // against a concurrent decision.
        let parent: Option<ChangeSetRow> = sqlx::query_as(
            "SELECT id, organization_id, title, description,
                    status::text AS status, requested_by, requested_by_display,
                    submitted_by, submitted_at, approved_at, applied_at,
                    rolled_back_at, cancelled_at, required_approvals,
                    correlation_id, version, created_at, updated_at
               FROM net.change_set
              WHERE id = $1 AND organization_id = $2 AND deleted_at IS NULL
              FOR UPDATE")
            .bind(set_id)
            .bind(org_id)
            .fetch_optional(&mut *tx)
            .await?;
        let parent = parent
            .ok_or_else(|| EngineError::container_not_found("change_set", set_id))?;
        let status = ChangeSetStatus::from_db(&parent.status)?;
        if status != ChangeSetStatus::Submitted {
            return Err(EngineError::bad_request(format!(
                "Cannot record a decision on a Change Set in status '{}'. \
                 Decisions are only accepted while Submitted.", status.as_str())));
        }
        if parent.requested_by == Some(approver_user_id) {
            return Err(EngineError::bad_request(
                "Requester cannot approve their own Change Set — ask another admin."));
        }

        // Insert the approval row. UNIQUE (change_set_id, approver_user_id)
        // catches duplicates; map the constraint error to a readable one.
        let approval: ApprovalRow = match sqlx::query_as::<_, ApprovalRowDb>(
            "INSERT INTO net.change_set_approval
                (organization_id, change_set_id, approver_user_id, approver_display,
                 decision, notes)
             VALUES ($1, $2, $3, $4, $5::net.change_set_decision, $6)
             RETURNING id, change_set_id, approver_user_id, approver_display,
                       decision::text AS decision, decided_at, notes")
            .bind(org_id)
            .bind(set_id)
            .bind(approver_user_id)
            .bind(body.approver_display.as_deref())
            .bind(body.decision.as_str())
            .bind(body.notes.as_deref())
            .fetch_one(&mut *tx)
            .await
        {
            Ok(row) => row.into_dto()?,
            Err(sqlx::Error::Database(db_err)) if db_err.is_unique_violation() =>
                return Err(EngineError::bad_request(
                    "This approver has already recorded a decision on this Change Set.")),
            Err(e) => return Err(e.into()),
        };

        // Count current Approve tally + determine if we should transition.
        let approve_count: i64 = sqlx::query_scalar(
            "SELECT COUNT(*)::bigint FROM net.change_set_approval
              WHERE change_set_id = $1
                AND decision = 'Approve'::net.change_set_decision")
            .bind(set_id)
            .fetch_one(&mut *tx)
            .await?;
        let required = parent.required_approvals.unwrap_or(1);

        // Decide the next state.
        let (new_status, stamp_approved_at): (Option<&str>, bool) = match body.decision {
            ChangeSetDecision::Reject => (Some("Rejected"), false),
            ChangeSetDecision::Approve if (approve_count as i32) >= required =>
                (Some("Approved"), true),
            ChangeSetDecision::Approve => (None, false),
        };

        let updated_set: ChangeSetRow = if let Some(next) = new_status {
            sqlx::query_as(
                "UPDATE net.change_set
                    SET status       = $3::net.change_set_status,
                        approved_at  = CASE WHEN $4 THEN now() ELSE approved_at END,
                        updated_at   = now(),
                        updated_by   = $5,
                        version      = version + 1
                  WHERE id = $1 AND organization_id = $2
                    AND status = 'Submitted'::net.change_set_status
                  RETURNING id, organization_id, title, description,
                            status::text AS status, requested_by, requested_by_display,
                            submitted_by, submitted_at, approved_at, applied_at,
                            rolled_back_at, cancelled_at, required_approvals,
                            correlation_id, version, created_at, updated_at")
                .bind(set_id)
                .bind(org_id)
                .bind(next)
                .bind(stamp_approved_at)
                .bind(approver_user_id)
                .fetch_one(&mut *tx)
                .await?
        } else {
            parent
        };

        audit::append_tx(&mut tx, &AuditEvent {
            organization_id: org_id,
            source_service: "networking-engine",
            entity_type: "ChangeSet",
            entity_id: Some(set_id),
            action: match body.decision {
                ChangeSetDecision::Approve => "ApprovalRecorded",
                ChangeSetDecision::Reject => "Rejected",
            },
            actor_user_id: Some(approver_user_id),
            actor_display: body.approver_display.as_deref(),
            client_ip: None,
            correlation_id: Some(updated_set.correlation_id),
            details: serde_json::json!({
                "decision": body.decision.as_str(),
                "approval_id": approval.id,
                "approve_count": approve_count,
                "approvals_required": required,
                "new_status": new_status.unwrap_or(&updated_set.status),
            }),
        }).await?;

        let item_count: i64 = sqlx::query_scalar(
            "SELECT COUNT(*)::bigint FROM net.change_set_item
              WHERE change_set_id = $1 AND deleted_at IS NULL")
            .bind(set_id)
            .fetch_one(&mut *tx)
            .await?;

        tx.commit().await?;
        Ok(DecisionResult {
            approval,
            change_set: updated_set.into_dto(item_count)?,
            approvals_count: approve_count,
            approvals_required: required,
        })
    }

    pub async fn submit(
        &self,
        set_id: Uuid,
        org_id: Uuid,
        body: &SubmitBody,
        user_id: Option<i32>,
    ) -> Result<ChangeSet, EngineError> {
        if body.required_approvals < 1 {
            return Err(EngineError::bad_request(
                "required_approvals must be >= 1 — explicit zero would bypass approval entirely"));
        }

        let mut tx = self.pool.begin().await?;

        // Must have at least one item — you can't submit an empty intent.
        let item_count: i64 = sqlx::query_scalar(
            "SELECT COUNT(*)::bigint FROM net.change_set_item
              WHERE change_set_id = $1 AND deleted_at IS NULL")
            .bind(set_id)
            .fetch_one(&mut *tx)
            .await?;
        if item_count == 0 {
            return Err(EngineError::bad_request(
                "Cannot submit an empty Change Set — add at least one item first."));
        }

        // Transition Draft → Submitted. Status change is atomic with the
        // submitted_at / required_approvals stamp + the audit entry.
        let row: Option<ChangeSetRow> = sqlx::query_as(
            "UPDATE net.change_set
                SET status             = 'Submitted'::net.change_set_status,
                    submitted_at       = now(),
                    submitted_by       = $3,
                    required_approvals = $4,
                    updated_at         = now(),
                    updated_by         = $3,
                    version            = version + 1
              WHERE id = $1 AND organization_id = $2
                AND status = 'Draft'::net.change_set_status
                AND deleted_at IS NULL
              RETURNING id, organization_id, title, description,
                        status::text AS status, requested_by, requested_by_display,
                        submitted_by, submitted_at, approved_at, applied_at,
                        rolled_back_at, cancelled_at, required_approvals,
                        correlation_id, version, created_at, updated_at")
            .bind(set_id)
            .bind(org_id)
            .bind(user_id)
            .bind(body.required_approvals)
            .fetch_optional(&mut *tx)
            .await?;

        let row = row.ok_or_else(|| EngineError::bad_request(
            "Change Set not in Draft, already submitted, or wrong tenant — re-fetch and retry."))?;

        audit::append_tx(&mut tx, &AuditEvent {
            organization_id: org_id,
            source_service: "networking-engine",
            entity_type: "ChangeSet",
            entity_id: Some(row.id),
            action: "Submitted",
            actor_user_id: user_id,
            actor_display: None,
            client_ip: None,
            correlation_id: Some(row.correlation_id),
            details: serde_json::json!({
                "required_approvals": body.required_approvals,
                "item_count": item_count,
            }),
        }).await?;

        tx.commit().await?;
        row.into_dto(item_count)
    }
}

// ─── Validation ──────────────────────────────────────────────────────────

pub(crate) fn validate_item(body: &AddItemBody) -> Result<(), EngineError> {
    if body.entity_type.trim().is_empty() {
        return Err(EngineError::bad_request("entity_type is required"));
    }
    match body.action {
        ChangeSetAction::Create => {
            if body.entity_id.is_some() {
                return Err(EngineError::bad_request(
                    "Create items must not carry entity_id — the id is assigned at apply time"));
            }
            if body.after_json.is_none() {
                return Err(EngineError::bad_request(
                    "Create items must carry after_json"));
            }
        }
        ChangeSetAction::Update | ChangeSetAction::Rename => {
            if body.entity_id.is_none() {
                return Err(EngineError::bad_request(
                    "Update/Rename items require entity_id"));
            }
            if body.after_json.is_none() {
                return Err(EngineError::bad_request(
                    "Update/Rename items must carry after_json"));
            }
        }
        ChangeSetAction::Delete => {
            if body.entity_id.is_none() {
                return Err(EngineError::bad_request(
                    "Delete items require entity_id"));
            }
            if body.after_json.is_some() {
                return Err(EngineError::bad_request(
                    "Delete items must not carry after_json"));
            }
        }
    }
    Ok(())
}

// ─── Apply execution ─────────────────────────────────────────────────────

#[derive(Debug, Clone, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct ApplyResult {
    pub change_set: ChangeSet,
    pub outcomes: Vec<ApplyItemOutcome>,
    pub applied_count: usize,
    pub failed_count: usize,
}

#[derive(Debug, Clone, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct ApplyItemOutcome {
    pub item_id: Uuid,
    pub item_order: i32,
    pub success: bool,
    pub error: Option<String>,
    /// True when this item was already applied in a prior apply() run.
    pub skipped: bool,
}

/// Execute one item. Per-item transaction so one failure doesn't roll
/// back the rest — partial-apply is expected and admins retry.
async fn apply_one(
    pool: &PgPool,
    item: &ChangeSetItem,
    org_id: Uuid,
    correlation_id: Uuid,
    user_id: Option<i32>,
) -> ApplyItemOutcome {
    let result = dispatch_apply(pool, item, org_id, correlation_id, user_id).await;
    match result {
        Ok(()) => ApplyItemOutcome {
            item_id: item.id, item_order: item.item_order,
            success: true, error: None, skipped: false,
        },
        Err(e) => {
            // Record the error on the item row so retry + admin UI both
            // see it. Best-effort — if the error stamp itself fails we
            // swallow it (we don't want to mask the original error).
            let _ = sqlx::query(
                "UPDATE net.change_set_item
                    SET apply_error = $3, updated_at = now()
                  WHERE id = $1 AND organization_id = $2")
                .bind(item.id)
                .bind(org_id)
                .bind(e.to_string())
                .execute(pool)
                .await;
            ApplyItemOutcome {
                item_id: item.id, item_order: item.item_order,
                success: false, error: Some(e.to_string()), skipped: false,
            }
        }
    }
}

async fn dispatch_apply(
    pool: &PgPool,
    item: &ChangeSetItem,
    org_id: Uuid,
    correlation_id: Uuid,
    user_id: Option<i32>,
) -> Result<(), EngineError> {
    match (item.entity_type.as_str(), item.action) {
        ("Device", ChangeSetAction::Rename) =>
            apply_device_rename(pool, item, org_id, correlation_id, user_id).await,
        ("Device", ChangeSetAction::Create) =>
            apply_device_create(pool, item, org_id, correlation_id, user_id).await,
        ("Device", ChangeSetAction::Update) =>
            apply_device_update(pool, item, org_id, correlation_id, user_id).await,
        ("Device", ChangeSetAction::Delete) =>
            apply_device_delete(pool, item, org_id, correlation_id, user_id).await,
        ("Link", ChangeSetAction::Rename) =>
            apply_link_rename(pool, item, org_id, correlation_id, user_id).await,
        ("Server", ChangeSetAction::Rename) =>
            apply_server_rename(pool, item, org_id, correlation_id, user_id).await,
        ("Vlan", ChangeSetAction::Create) =>
            apply_vlan_create(pool, item, org_id, correlation_id, user_id).await,
        ("AsnAllocation", ChangeSetAction::Create) =>
            apply_asn_create(pool, item, org_id, correlation_id, user_id).await,
        ("MlagDomain", ChangeSetAction::Create) =>
            apply_mlag_create(pool, item, org_id, correlation_id, user_id).await,
        ("Subnet", ChangeSetAction::Create) =>
            apply_subnet_create(pool, item, org_id, correlation_id, user_id).await,
        ("IpAddress", ChangeSetAction::Create) =>
            apply_ip_create(pool, item, org_id, correlation_id, user_id).await,
        (et, act) => Err(EngineError::bad_request(format!(
            "Apply not yet implemented for ({et}, {}). Device covers all 4 \
             actions; Link / Server cover Rename; Vlan / AsnAllocation / \
             MlagDomain / Subnet / IpAddress cover Create.", act.as_str()))),
    }
}

async fn apply_device_rename(
    pool: &PgPool,
    item: &ChangeSetItem,
    org_id: Uuid,
    correlation_id: Uuid,
    user_id: Option<i32>,
) -> Result<(), EngineError> {
    let entity_id = item.entity_id.ok_or_else(|| EngineError::bad_request(
        "Device/Rename item is missing entity_id"))?;
    let after = item.after_json.as_ref().ok_or_else(|| EngineError::bad_request(
        "Device/Rename item is missing after_json"))?;
    let new_hostname = after.get("hostname").and_then(|v| v.as_str()).ok_or_else(||
        EngineError::bad_request("after_json.hostname is required for Device/Rename"))?;

    let mut tx = pool.begin().await?;

    // Version precondition: if the item specifies expected_version, enforce
    // it. Otherwise accept whatever the current version is (rename items
    // can be drafted without the ui knowing the version).
    let updated: Option<(String, i32)> = match item.expected_version {
        Some(ev) => sqlx::query_as(
            "UPDATE net.device
                SET hostname = $3, updated_at = now(), version = version + 1
              WHERE id = $1 AND organization_id = $2
                AND version = $4 AND deleted_at IS NULL
              RETURNING hostname, version")
            .bind(entity_id).bind(org_id).bind(new_hostname).bind(ev)
            .fetch_optional(&mut *tx).await?,
        None => sqlx::query_as(
            "UPDATE net.device
                SET hostname = $3, updated_at = now(), version = version + 1
              WHERE id = $1 AND organization_id = $2 AND deleted_at IS NULL
              RETURNING hostname, version")
            .bind(entity_id).bind(org_id).bind(new_hostname)
            .fetch_optional(&mut *tx).await?,
    };
    let (_, new_version) = updated.ok_or_else(|| EngineError::bad_request(format!(
        "Device {entity_id} not found, version mismatch, or deleted.")))?;

    // Stamp applied_at on the item row.
    sqlx::query(
        "UPDATE net.change_set_item
            SET applied_at = now(), apply_error = NULL, updated_at = now()
          WHERE id = $1 AND organization_id = $2")
        .bind(item.id)
        .bind(org_id)
        .execute(&mut *tx)
        .await?;

    audit::append_tx(&mut tx, &AuditEvent {
        organization_id: org_id,
        source_service: "networking-engine",
        entity_type: "Device",
        entity_id: Some(entity_id),
        action: "Renamed",
        actor_user_id: user_id,
        actor_display: None,
        client_ip: None,
        correlation_id: Some(correlation_id),
        details: serde_json::json!({
            "from": item.before_json.as_ref().and_then(|b| b.get("hostname")),
            "to": new_hostname,
            "new_version": new_version,
            "change_set_item_id": item.id,
        }),
    }).await?;

    tx.commit().await?;
    Ok(())
}

async fn apply_link_rename(
    pool: &PgPool,
    item: &ChangeSetItem,
    org_id: Uuid,
    correlation_id: Uuid,
    user_id: Option<i32>,
) -> Result<(), EngineError> {
    let entity_id = item.entity_id.ok_or_else(|| EngineError::bad_request(
        "Link/Rename item is missing entity_id"))?;
    let after = item.after_json.as_ref().ok_or_else(|| EngineError::bad_request(
        "Link/Rename item is missing after_json"))?;
    let new_link_code = after.get("linkCode").or_else(|| after.get("link_code"))
        .and_then(|v| v.as_str())
        .ok_or_else(|| EngineError::bad_request(
            "after_json.linkCode (or link_code) is required for Link/Rename"))?;

    let mut tx = pool.begin().await?;

    // OCC via expected_version when supplied; otherwise accept any version.
    let updated: Option<(String, i32)> = match item.expected_version {
        Some(ev) => sqlx::query_as(
            "UPDATE net.link
                SET link_code = $3, updated_at = now(), version = version + 1
              WHERE id = $1 AND organization_id = $2
                AND version = $4 AND deleted_at IS NULL
              RETURNING link_code, version")
            .bind(entity_id).bind(org_id).bind(new_link_code).bind(ev)
            .fetch_optional(&mut *tx).await?,
        None => sqlx::query_as(
            "UPDATE net.link
                SET link_code = $3, updated_at = now(), version = version + 1
              WHERE id = $1 AND organization_id = $2 AND deleted_at IS NULL
              RETURNING link_code, version")
            .bind(entity_id).bind(org_id).bind(new_link_code)
            .fetch_optional(&mut *tx).await?,
    };
    let (_, new_version) = updated.ok_or_else(|| EngineError::bad_request(format!(
        "Link {entity_id} not found, version mismatch, or deleted.")))?;

    sqlx::query(
        "UPDATE net.change_set_item
            SET applied_at = now(), apply_error = NULL, updated_at = now()
          WHERE id = $1 AND organization_id = $2")
        .bind(item.id).bind(org_id).execute(&mut *tx).await?;

    audit::append_tx(&mut tx, &AuditEvent {
        organization_id: org_id,
        source_service: "networking-engine",
        entity_type: "Link",
        entity_id: Some(entity_id),
        action: "Renamed",
        actor_user_id: user_id,
        actor_display: None,
        client_ip: None,
        correlation_id: Some(correlation_id),
        details: serde_json::json!({
            "from": item.before_json.as_ref().and_then(|b| {
                b.get("linkCode").or_else(|| b.get("link_code"))
            }),
            "to": new_link_code,
            "new_version": new_version,
            "change_set_item_id": item.id,
        }),
    }).await?;

    tx.commit().await?;
    Ok(())
}

async fn apply_server_rename(
    pool: &PgPool,
    item: &ChangeSetItem,
    org_id: Uuid,
    correlation_id: Uuid,
    user_id: Option<i32>,
) -> Result<(), EngineError> {
    let entity_id = item.entity_id.ok_or_else(|| EngineError::bad_request(
        "Server/Rename item is missing entity_id"))?;
    let after = item.after_json.as_ref().ok_or_else(|| EngineError::bad_request(
        "Server/Rename item is missing after_json"))?;
    let new_hostname = after.get("hostname").and_then(|v| v.as_str())
        .ok_or_else(|| EngineError::bad_request(
            "after_json.hostname is required for Server/Rename"))?;

    let mut tx = pool.begin().await?;

    let updated: Option<(String, i32)> = match item.expected_version {
        Some(ev) => sqlx::query_as(
            "UPDATE net.server
                SET hostname = $3, updated_at = now(), version = version + 1
              WHERE id = $1 AND organization_id = $2
                AND version = $4 AND deleted_at IS NULL
              RETURNING hostname, version")
            .bind(entity_id).bind(org_id).bind(new_hostname).bind(ev)
            .fetch_optional(&mut *tx).await?,
        None => sqlx::query_as(
            "UPDATE net.server
                SET hostname = $3, updated_at = now(), version = version + 1
              WHERE id = $1 AND organization_id = $2 AND deleted_at IS NULL
              RETURNING hostname, version")
            .bind(entity_id).bind(org_id).bind(new_hostname)
            .fetch_optional(&mut *tx).await?,
    };
    let (_, new_version) = updated.ok_or_else(|| EngineError::bad_request(format!(
        "Server {entity_id} not found, version mismatch, or deleted.")))?;

    sqlx::query(
        "UPDATE net.change_set_item
            SET applied_at = now(), apply_error = NULL, updated_at = now()
          WHERE id = $1 AND organization_id = $2")
        .bind(item.id).bind(org_id).execute(&mut *tx).await?;

    audit::append_tx(&mut tx, &AuditEvent {
        organization_id: org_id,
        source_service: "networking-engine",
        entity_type: "Server",
        entity_id: Some(entity_id),
        action: "Renamed",
        actor_user_id: user_id,
        actor_display: None,
        client_ip: None,
        correlation_id: Some(correlation_id),
        details: serde_json::json!({
            "from": item.before_json.as_ref().and_then(|b| b.get("hostname")),
            "to": new_hostname,
            "new_version": new_version,
            "change_set_item_id": item.id,
        }),
    }).await?;

    tx.commit().await?;
    Ok(())
}

// ─── Rollback execution ──────────────────────────────────────────────────

#[derive(Debug, Clone, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct RollbackResult {
    pub change_set: ChangeSet,
    pub outcomes: Vec<RollbackItemOutcome>,
    pub reverted_count: usize,
    pub failed_count: usize,
}

#[derive(Debug, Clone, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct RollbackItemOutcome {
    pub item_id: Uuid,
    pub item_order: i32,
    pub success: bool,
    pub error: Option<String>,
    /// True when this item was never applied — nothing to reverse.
    pub skipped: bool,
}

async fn rollback_one(
    pool: &PgPool,
    item: &ChangeSetItem,
    org_id: Uuid,
    correlation_id: Uuid,
    user_id: Option<i32>,
) -> RollbackItemOutcome {
    let result = dispatch_rollback(pool, item, org_id, correlation_id, user_id).await;
    match result {
        Ok(()) => RollbackItemOutcome {
            item_id: item.id, item_order: item.item_order,
            success: true, error: None, skipped: false,
        },
        Err(e) => RollbackItemOutcome {
            item_id: item.id, item_order: item.item_order,
            success: false, error: Some(e.to_string()), skipped: false,
        },
    }
}

async fn dispatch_rollback(
    pool: &PgPool,
    item: &ChangeSetItem,
    org_id: Uuid,
    correlation_id: Uuid,
    user_id: Option<i32>,
) -> Result<(), EngineError> {
    match (item.entity_type.as_str(), item.action) {
        ("Device", ChangeSetAction::Rename) =>
            rollback_device_rename(pool, item, org_id, correlation_id, user_id).await,
        ("Device", ChangeSetAction::Create) =>
            rollback_device_create(pool, item, org_id, correlation_id, user_id).await,
        ("Device", ChangeSetAction::Update) =>
            rollback_device_update(pool, item, org_id, correlation_id, user_id).await,
        ("Device", ChangeSetAction::Delete) =>
            rollback_device_delete(pool, item, org_id, correlation_id, user_id).await,
        ("Link", ChangeSetAction::Rename) =>
            rollback_link_rename(pool, item, org_id, correlation_id, user_id).await,
        ("Server", ChangeSetAction::Rename) =>
            rollback_server_rename(pool, item, org_id, correlation_id, user_id).await,
        ("Vlan", ChangeSetAction::Create) =>
            rollback_vlan_create(pool, item, org_id, correlation_id, user_id).await,
        ("AsnAllocation", ChangeSetAction::Create) =>
            rollback_asn_create(pool, item, org_id, correlation_id, user_id).await,
        ("MlagDomain", ChangeSetAction::Create) =>
            rollback_mlag_create(pool, item, org_id, correlation_id, user_id).await,
        ("Subnet", ChangeSetAction::Create) =>
            rollback_subnet_create(pool, item, org_id, correlation_id, user_id).await,
        ("IpAddress", ChangeSetAction::Create) =>
            rollback_ip_create(pool, item, org_id, correlation_id, user_id).await,
        (et, act) => Err(EngineError::bad_request(format!(
            "Rollback not yet implemented for ({et}, {}).", act.as_str()))),
    }
}

async fn rollback_device_rename(
    pool: &PgPool,
    item: &ChangeSetItem,
    org_id: Uuid,
    correlation_id: Uuid,
    user_id: Option<i32>,
) -> Result<(), EngineError> {
    let entity_id = item.entity_id.ok_or_else(|| EngineError::bad_request(
        "Device/Rename item is missing entity_id"))?;
    let before = item.before_json.as_ref().ok_or_else(|| EngineError::bad_request(
        "Device/Rename rollback requires before_json — the original Set was drafted without one"))?;
    let after = item.after_json.as_ref().ok_or_else(|| EngineError::bad_request(
        "Device/Rename rollback requires after_json"))?;
    let original_hostname = before.get("hostname").and_then(|v| v.as_str()).ok_or_else(||
        EngineError::bad_request("before_json.hostname is required for Device/Rename rollback"))?;
    let applied_hostname = after.get("hostname").and_then(|v| v.as_str()).ok_or_else(||
        EngineError::bad_request("after_json.hostname is required for Device/Rename rollback"))?;

    let mut tx = pool.begin().await?;

    // Only reverse if the device still carries the hostname we applied.
    // If some other rename moved it to a third value since apply, bail —
    // a surprise revert to an unrelated-looking hostname would astonish.
    let reverted: Option<(String, i32)> = sqlx::query_as(
        "UPDATE net.device
            SET hostname = $3, updated_at = now(), version = version + 1
          WHERE id = $1 AND organization_id = $2
            AND hostname = $4 AND deleted_at IS NULL
          RETURNING hostname, version")
        .bind(entity_id)
        .bind(org_id)
        .bind(original_hostname)
        .bind(applied_hostname)
        .fetch_optional(&mut *tx)
        .await?;
    let (_, new_version) = reverted.ok_or_else(|| EngineError::bad_request(format!(
        "Device {entity_id} hostname no longer matches the applied value \
         '{applied_hostname}' — rollback aborted to avoid surprising change. \
         Someone may have renamed it again since apply.")))?;

    // Clear applied_at on the item so a subsequent re-apply re-executes
    // it cleanly. Don't touch apply_error unless a new failure arrives.
    sqlx::query(
        "UPDATE net.change_set_item
            SET applied_at = NULL, updated_at = now()
          WHERE id = $1 AND organization_id = $2")
        .bind(item.id)
        .bind(org_id)
        .execute(&mut *tx)
        .await?;

    audit::append_tx(&mut tx, &AuditEvent {
        organization_id: org_id,
        source_service: "networking-engine",
        entity_type: "Device",
        entity_id: Some(entity_id),
        action: "RolledBack",
        actor_user_id: user_id,
        actor_display: None,
        client_ip: None,
        correlation_id: Some(correlation_id),
        details: serde_json::json!({
            "from": applied_hostname,
            "to": original_hostname,
            "new_version": new_version,
            "change_set_item_id": item.id,
        }),
    }).await?;

    tx.commit().await?;
    Ok(())
}

async fn rollback_link_rename(
    pool: &PgPool,
    item: &ChangeSetItem,
    org_id: Uuid,
    correlation_id: Uuid,
    user_id: Option<i32>,
) -> Result<(), EngineError> {
    let entity_id = item.entity_id.ok_or_else(|| EngineError::bad_request(
        "Link/Rename item is missing entity_id"))?;
    let before = item.before_json.as_ref().ok_or_else(|| EngineError::bad_request(
        "Link/Rename rollback requires before_json"))?;
    let after = item.after_json.as_ref().ok_or_else(|| EngineError::bad_request(
        "Link/Rename rollback requires after_json"))?;

    let original = before.get("linkCode").or_else(|| before.get("link_code"))
        .and_then(|v| v.as_str())
        .ok_or_else(|| EngineError::bad_request(
            "before_json.linkCode (or link_code) is required for Link/Rename rollback"))?;
    let applied = after.get("linkCode").or_else(|| after.get("link_code"))
        .and_then(|v| v.as_str())
        .ok_or_else(|| EngineError::bad_request(
            "after_json.linkCode (or link_code) is required for Link/Rename rollback"))?;

    let mut tx = pool.begin().await?;

    // Surprise-safety: only reverse if the link still carries the value we applied.
    let reverted: Option<(String, i32)> = sqlx::query_as(
        "UPDATE net.link
            SET link_code = $3, updated_at = now(), version = version + 1
          WHERE id = $1 AND organization_id = $2
            AND link_code = $4 AND deleted_at IS NULL
          RETURNING link_code, version")
        .bind(entity_id).bind(org_id).bind(original).bind(applied)
        .fetch_optional(&mut *tx).await?;
    let (_, new_version) = reverted.ok_or_else(|| EngineError::bad_request(format!(
        "Link {entity_id} link_code no longer matches the applied value \
         '{applied}' — rollback aborted. Someone may have renamed it again.")))?;

    sqlx::query(
        "UPDATE net.change_set_item
            SET applied_at = NULL, updated_at = now()
          WHERE id = $1 AND organization_id = $2")
        .bind(item.id).bind(org_id).execute(&mut *tx).await?;

    audit::append_tx(&mut tx, &AuditEvent {
        organization_id: org_id,
        source_service: "networking-engine",
        entity_type: "Link",
        entity_id: Some(entity_id),
        action: "RolledBack",
        actor_user_id: user_id,
        actor_display: None,
        client_ip: None,
        correlation_id: Some(correlation_id),
        details: serde_json::json!({
            "from": applied,
            "to": original,
            "new_version": new_version,
            "change_set_item_id": item.id,
        }),
    }).await?;

    tx.commit().await?;
    Ok(())
}

async fn rollback_server_rename(
    pool: &PgPool,
    item: &ChangeSetItem,
    org_id: Uuid,
    correlation_id: Uuid,
    user_id: Option<i32>,
) -> Result<(), EngineError> {
    let entity_id = item.entity_id.ok_or_else(|| EngineError::bad_request(
        "Server/Rename item is missing entity_id"))?;
    let before = item.before_json.as_ref().ok_or_else(|| EngineError::bad_request(
        "Server/Rename rollback requires before_json"))?;
    let after = item.after_json.as_ref().ok_or_else(|| EngineError::bad_request(
        "Server/Rename rollback requires after_json"))?;

    let original = before.get("hostname").and_then(|v| v.as_str())
        .ok_or_else(|| EngineError::bad_request(
            "before_json.hostname is required for Server/Rename rollback"))?;
    let applied = after.get("hostname").and_then(|v| v.as_str())
        .ok_or_else(|| EngineError::bad_request(
            "after_json.hostname is required for Server/Rename rollback"))?;

    let mut tx = pool.begin().await?;

    let reverted: Option<(String, i32)> = sqlx::query_as(
        "UPDATE net.server
            SET hostname = $3, updated_at = now(), version = version + 1
          WHERE id = $1 AND organization_id = $2
            AND hostname = $4 AND deleted_at IS NULL
          RETURNING hostname, version")
        .bind(entity_id).bind(org_id).bind(original).bind(applied)
        .fetch_optional(&mut *tx).await?;
    let (_, new_version) = reverted.ok_or_else(|| EngineError::bad_request(format!(
        "Server {entity_id} hostname no longer matches the applied value \
         '{applied}' — rollback aborted.")))?;

    sqlx::query(
        "UPDATE net.change_set_item
            SET applied_at = NULL, updated_at = now()
          WHERE id = $1 AND organization_id = $2")
        .bind(item.id).bind(org_id).execute(&mut *tx).await?;

    audit::append_tx(&mut tx, &AuditEvent {
        organization_id: org_id,
        source_service: "networking-engine",
        entity_type: "Server",
        entity_id: Some(entity_id),
        action: "RolledBack",
        actor_user_id: user_id,
        actor_display: None,
        client_ip: None,
        correlation_id: Some(correlation_id),
        details: serde_json::json!({
            "from": applied,
            "to": original,
            "new_version": new_version,
            "change_set_item_id": item.id,
        }),
    }).await?;

    tx.commit().await?;
    Ok(())
}

// ─── Device Create / Update / Delete executors ──────────────────────────
//
// Shared notes:
//   * The JSON payloads accept either camelCase or snake_case keys so
//     callers drafted from the WPF side (PascalCase-ish JSON via System.Text.Json
//     camelCase policy) and the Rust side (serde camelCase) both work.
//   * Update uses read-modify-write: pull the current row, merge in any
//     fields present in after_json, write it back under OCC. "Fields not
//     present in after_json" means "keep current value". Callers who want
//     to CLEAR a field need to include the key with JSON `null`.
//   * The mutable field whitelist below is intentional — ping telemetry
//     columns (last_ping_*, last_ssh_*) and the audit/lock columns are
//     excluded from Change Set mutations so accidental drafts can't
//     overwrite live probe state or bypass lock enforcement.

/// Whitelisted mutable device fields. If a caller includes other keys in
/// after_json they're silently ignored — keeps the API tight.
#[derive(Default, Debug, Clone)]
struct DeviceFields {
    hostname: Option<String>,
    device_code: Option<Option<String>>,
    display_name: Option<Option<String>>,
    hardware_model: Option<Option<String>>,
    serial_number: Option<Option<String>>,
    firmware_version: Option<Option<String>>,
    device_role_id: Option<Option<Uuid>>,
    building_id: Option<Option<Uuid>>,
    room_id: Option<Option<Uuid>>,
    rack_id: Option<Option<Uuid>>,
    management_ip: Option<Option<String>>,
    ssh_username: Option<Option<String>>,
    ssh_port: Option<Option<i32>>,
    management_vrf: Option<bool>,
    inband_enabled: Option<bool>,
    notes: Option<Option<String>>,
}

/// Read each known key — first camelCase, then snake_case fallback.
/// `Option<Option<T>>` distinguishes "not in JSON at all" (outer None —
/// don't touch) from "in JSON, value null" (outer Some(None) — set to NULL).
fn read_device_fields(j: &serde_json::Value) -> DeviceFields {
    let obj = match j.as_object() { Some(o) => o, None => return DeviceFields::default() };
    let get = |camel: &str, snake: &str| -> Option<&serde_json::Value> {
        obj.get(camel).or_else(|| obj.get(snake))
    };
    let str_of = |v: &serde_json::Value| v.as_str().map(String::from);
    let uuid_of = |v: &serde_json::Value| v.as_str().and_then(|s| Uuid::parse_str(s).ok());
    let i32_of = |v: &serde_json::Value| v.as_i64().and_then(|n| i32::try_from(n).ok());
    let bool_of = |v: &serde_json::Value| v.as_bool();

    let nullable_str = |k_camel, k_snake| -> Option<Option<String>> {
        get(k_camel, k_snake).map(|v| if v.is_null() { None } else { str_of(v) })
    };
    let nullable_uuid = |k_camel, k_snake| -> Option<Option<Uuid>> {
        get(k_camel, k_snake).map(|v| if v.is_null() { None } else { uuid_of(v) })
    };
    let nullable_i32 = |k_camel, k_snake| -> Option<Option<i32>> {
        get(k_camel, k_snake).map(|v| if v.is_null() { None } else { i32_of(v) })
    };

    DeviceFields {
        hostname:         get("hostname", "hostname").and_then(str_of),
        device_code:      nullable_str("deviceCode", "device_code"),
        display_name:     nullable_str("displayName", "display_name"),
        hardware_model:   nullable_str("hardwareModel", "hardware_model"),
        serial_number:    nullable_str("serialNumber", "serial_number"),
        firmware_version: nullable_str("firmwareVersion", "firmware_version"),
        device_role_id:   nullable_uuid("deviceRoleId", "device_role_id"),
        building_id:      nullable_uuid("buildingId", "building_id"),
        room_id:          nullable_uuid("roomId", "room_id"),
        rack_id:          nullable_uuid("rackId", "rack_id"),
        management_ip:    nullable_str("managementIp", "management_ip"),
        ssh_username:     nullable_str("sshUsername", "ssh_username"),
        ssh_port:         nullable_i32("sshPort", "ssh_port"),
        management_vrf:   get("managementVrf", "management_vrf").and_then(bool_of),
        inband_enabled:   get("inbandEnabled", "inband_enabled").and_then(bool_of),
        notes:            nullable_str("notes", "notes"),
    }
}

async fn apply_device_create(
    pool: &PgPool,
    item: &ChangeSetItem,
    org_id: Uuid,
    correlation_id: Uuid,
    user_id: Option<i32>,
) -> Result<(), EngineError> {
    let after = item.after_json.as_ref().ok_or_else(|| EngineError::bad_request(
        "Device/Create item is missing after_json"))?;
    let f = read_device_fields(after);
    let hostname = f.hostname.ok_or_else(|| EngineError::bad_request(
        "Device/Create requires after_json.hostname (column is NOT NULL)"))?;

    let mut tx = pool.begin().await?;

    // Create reads a pile of optional fields; nested Option::flatten is
    // tidier than 16 if-lets. Any Some(Some(v)) becomes v; Some(None) and
    // None both become NULL.
    let new_id: Uuid = sqlx::query_scalar(
        "INSERT INTO net.device
            (organization_id, hostname, device_code, display_name,
             hardware_model, serial_number, firmware_version,
             device_role_id, building_id, room_id, rack_id,
             management_ip, ssh_username, ssh_port,
             management_vrf, inband_enabled, notes,
             status, lock_state, created_by, updated_by)
         VALUES ($1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11,
                 $12::inet, $13, $14, COALESCE($15, false), COALESCE($16, false), $17,
                 'Planned'::net.entity_status, 'Open'::net.lock_state, $18, $18)
         RETURNING id")
        .bind(org_id)
        .bind(&hostname)
        .bind(f.device_code.flatten())
        .bind(f.display_name.flatten())
        .bind(f.hardware_model.flatten())
        .bind(f.serial_number.flatten())
        .bind(f.firmware_version.flatten())
        .bind(f.device_role_id.flatten())
        .bind(f.building_id.flatten())
        .bind(f.room_id.flatten())
        .bind(f.rack_id.flatten())
        .bind(f.management_ip.flatten())
        .bind(f.ssh_username.flatten())
        .bind(f.ssh_port.flatten())
        .bind(f.management_vrf)
        .bind(f.inband_enabled)
        .bind(f.notes.flatten())
        .bind(user_id)
        .fetch_one(&mut *tx)
        .await?;

    // Stamp the new id onto the item so rollback knows what to soft-delete,
    // and mark as applied.
    sqlx::query(
        "UPDATE net.change_set_item
            SET entity_id = $3, applied_at = now(), apply_error = NULL,
                updated_at = now()
          WHERE id = $1 AND organization_id = $2")
        .bind(item.id).bind(org_id).bind(new_id)
        .execute(&mut *tx).await?;

    audit::append_tx(&mut tx, &AuditEvent {
        organization_id: org_id,
        source_service: "networking-engine",
        entity_type: "Device",
        entity_id: Some(new_id),
        action: "Created",
        actor_user_id: user_id,
        actor_display: None,
        client_ip: None,
        correlation_id: Some(correlation_id),
        details: serde_json::json!({
            "hostname": hostname,
            "change_set_item_id": item.id,
        }),
    }).await?;

    tx.commit().await?;
    Ok(())
}

async fn rollback_device_create(
    pool: &PgPool,
    item: &ChangeSetItem,
    org_id: Uuid,
    correlation_id: Uuid,
    user_id: Option<i32>,
) -> Result<(), EngineError> {
    // Reverse-create = soft-delete the row we created at apply time.
    // entity_id was stamped by apply, so it's on the item now.
    let entity_id = item.entity_id.ok_or_else(|| EngineError::bad_request(
        "Device/Create rollback: item has no entity_id — apply never recorded the new row's id."))?;

    let mut tx = pool.begin().await?;

    let affected = sqlx::query(
        "UPDATE net.device
            SET deleted_at = now(), deleted_by = $3,
                updated_at = now(), updated_by = $3, version = version + 1
          WHERE id = $1 AND organization_id = $2 AND deleted_at IS NULL")
        .bind(entity_id).bind(org_id).bind(user_id)
        .execute(&mut *tx).await?;
    if affected.rows_affected() == 0 {
        return Err(EngineError::bad_request(format!(
            "Device {entity_id} not found or already deleted — rollback nothing to do.")));
    }

    sqlx::query(
        "UPDATE net.change_set_item
            SET applied_at = NULL, updated_at = now()
          WHERE id = $1 AND organization_id = $2")
        .bind(item.id).bind(org_id).execute(&mut *tx).await?;

    audit::append_tx(&mut tx, &AuditEvent {
        organization_id: org_id,
        source_service: "networking-engine",
        entity_type: "Device",
        entity_id: Some(entity_id),
        action: "RolledBack",
        actor_user_id: user_id,
        actor_display: None,
        client_ip: None,
        correlation_id: Some(correlation_id),
        details: serde_json::json!({
            "original_action": "Create",
            "reverse_action": "SoftDelete",
            "change_set_item_id": item.id,
        }),
    }).await?;

    tx.commit().await?;
    Ok(())
}

async fn apply_device_update(
    pool: &PgPool,
    item: &ChangeSetItem,
    org_id: Uuid,
    correlation_id: Uuid,
    user_id: Option<i32>,
) -> Result<(), EngineError> {
    let entity_id = item.entity_id.ok_or_else(|| EngineError::bad_request(
        "Device/Update item is missing entity_id"))?;
    let after = item.after_json.as_ref().ok_or_else(|| EngineError::bad_request(
        "Device/Update item is missing after_json"))?;
    let f = read_device_fields(after);

    let mut tx = pool.begin().await?;

    // COALESCE($binding, column) keeps the current value when the caller
    // didn't include that field. Explicit null (Some(None)) becomes NULL
    // because we pass None to $binding in that case — COALESCE(NULL, col)
    // still returns col, which is the "keep" behaviour. For explicit
    // null-clears we'd need a second set of "clear" booleans; that's
    // follow-on work — the common case (partial updates that set values)
    // works today.
    //
    // TODO: support explicit-null clears via a parallel set of sentinel
    // flags; revisit when a caller actually needs it.
    let updated: Option<(i32,)> = match item.expected_version {
        Some(ev) => sqlx::query_as(
            "UPDATE net.device SET
                hostname         = COALESCE($3,  hostname),
                device_code      = COALESCE($4,  device_code),
                display_name     = COALESCE($5,  display_name),
                hardware_model   = COALESCE($6,  hardware_model),
                serial_number    = COALESCE($7,  serial_number),
                firmware_version = COALESCE($8,  firmware_version),
                device_role_id   = COALESCE($9,  device_role_id),
                building_id      = COALESCE($10, building_id),
                room_id          = COALESCE($11, room_id),
                rack_id          = COALESCE($12, rack_id),
                management_ip    = COALESCE($13::inet, management_ip),
                ssh_username     = COALESCE($14, ssh_username),
                ssh_port         = COALESCE($15, ssh_port),
                management_vrf   = COALESCE($16, management_vrf),
                inband_enabled   = COALESCE($17, inband_enabled),
                notes            = COALESCE($18, notes),
                updated_at       = now(),
                updated_by       = $19,
                version          = version + 1
              WHERE id = $1 AND organization_id = $2
                AND version = $20 AND deleted_at IS NULL
              RETURNING version")
            .bind(entity_id).bind(org_id)
            .bind(f.hostname).bind(f.device_code.and_then(|x| x)).bind(f.display_name.and_then(|x| x))
            .bind(f.hardware_model.and_then(|x| x)).bind(f.serial_number.and_then(|x| x))
            .bind(f.firmware_version.and_then(|x| x))
            .bind(f.device_role_id.and_then(|x| x)).bind(f.building_id.and_then(|x| x))
            .bind(f.room_id.and_then(|x| x)).bind(f.rack_id.and_then(|x| x))
            .bind(f.management_ip.and_then(|x| x)).bind(f.ssh_username.and_then(|x| x))
            .bind(f.ssh_port.and_then(|x| x))
            .bind(f.management_vrf).bind(f.inband_enabled)
            .bind(f.notes.and_then(|x| x))
            .bind(user_id).bind(ev)
            .fetch_optional(&mut *tx).await?,
        None => sqlx::query_as(
            "UPDATE net.device SET
                hostname         = COALESCE($3,  hostname),
                device_code      = COALESCE($4,  device_code),
                display_name     = COALESCE($5,  display_name),
                hardware_model   = COALESCE($6,  hardware_model),
                serial_number    = COALESCE($7,  serial_number),
                firmware_version = COALESCE($8,  firmware_version),
                device_role_id   = COALESCE($9,  device_role_id),
                building_id      = COALESCE($10, building_id),
                room_id          = COALESCE($11, room_id),
                rack_id          = COALESCE($12, rack_id),
                management_ip    = COALESCE($13::inet, management_ip),
                ssh_username     = COALESCE($14, ssh_username),
                ssh_port         = COALESCE($15, ssh_port),
                management_vrf   = COALESCE($16, management_vrf),
                inband_enabled   = COALESCE($17, inband_enabled),
                notes            = COALESCE($18, notes),
                updated_at       = now(),
                updated_by       = $19,
                version          = version + 1
              WHERE id = $1 AND organization_id = $2 AND deleted_at IS NULL
              RETURNING version")
            .bind(entity_id).bind(org_id)
            .bind(f.hostname).bind(f.device_code.and_then(|x| x)).bind(f.display_name.and_then(|x| x))
            .bind(f.hardware_model.and_then(|x| x)).bind(f.serial_number.and_then(|x| x))
            .bind(f.firmware_version.and_then(|x| x))
            .bind(f.device_role_id.and_then(|x| x)).bind(f.building_id.and_then(|x| x))
            .bind(f.room_id.and_then(|x| x)).bind(f.rack_id.and_then(|x| x))
            .bind(f.management_ip.and_then(|x| x)).bind(f.ssh_username.and_then(|x| x))
            .bind(f.ssh_port.and_then(|x| x))
            .bind(f.management_vrf).bind(f.inband_enabled)
            .bind(f.notes.and_then(|x| x))
            .bind(user_id)
            .fetch_optional(&mut *tx).await?,
    };
    let (new_version,) = updated.ok_or_else(|| EngineError::bad_request(format!(
        "Device {entity_id} not found, version mismatch, or deleted.")))?;

    sqlx::query(
        "UPDATE net.change_set_item
            SET applied_at = now(), apply_error = NULL, updated_at = now()
          WHERE id = $1 AND organization_id = $2")
        .bind(item.id).bind(org_id).execute(&mut *tx).await?;

    audit::append_tx(&mut tx, &AuditEvent {
        organization_id: org_id,
        source_service: "networking-engine",
        entity_type: "Device",
        entity_id: Some(entity_id),
        action: "Updated",
        actor_user_id: user_id,
        actor_display: None,
        client_ip: None,
        correlation_id: Some(correlation_id),
        details: serde_json::json!({
            "before": item.before_json,
            "after": after,
            "new_version": new_version,
            "change_set_item_id": item.id,
        }),
    }).await?;

    tx.commit().await?;
    Ok(())
}

async fn rollback_device_update(
    pool: &PgPool,
    item: &ChangeSetItem,
    org_id: Uuid,
    correlation_id: Uuid,
    user_id: Option<i32>,
) -> Result<(), EngineError> {
    let entity_id = item.entity_id.ok_or_else(|| EngineError::bad_request(
        "Device/Update rollback is missing entity_id"))?;
    let before = item.before_json.as_ref().ok_or_else(|| EngineError::bad_request(
        "Device/Update rollback requires before_json — the original Set had no snapshot"))?;

    // Read the pre-apply state from before_json. Every whitelisted field
    // has to come from the snapshot — if any field is absent, we leave it
    // alone (which is fine: the forward apply would have left it alone too).
    let f = read_device_fields(before);

    let mut tx = pool.begin().await?;

    let updated: Option<(i32,)> = sqlx::query_as(
        "UPDATE net.device SET
            hostname         = COALESCE($3,  hostname),
            device_code      = COALESCE($4,  device_code),
            display_name     = COALESCE($5,  display_name),
            hardware_model   = COALESCE($6,  hardware_model),
            serial_number    = COALESCE($7,  serial_number),
            firmware_version = COALESCE($8,  firmware_version),
            device_role_id   = COALESCE($9,  device_role_id),
            building_id      = COALESCE($10, building_id),
            room_id          = COALESCE($11, room_id),
            rack_id          = COALESCE($12, rack_id),
            management_ip    = COALESCE($13::inet, management_ip),
            ssh_username     = COALESCE($14, ssh_username),
            ssh_port         = COALESCE($15, ssh_port),
            management_vrf   = COALESCE($16, management_vrf),
            inband_enabled   = COALESCE($17, inband_enabled),
            notes            = COALESCE($18, notes),
            updated_at       = now(),
            updated_by       = $19,
            version          = version + 1
          WHERE id = $1 AND organization_id = $2 AND deleted_at IS NULL
          RETURNING version")
        .bind(entity_id).bind(org_id)
        .bind(f.hostname).bind(f.device_code.and_then(|x| x)).bind(f.display_name.and_then(|x| x))
        .bind(f.hardware_model.and_then(|x| x)).bind(f.serial_number.and_then(|x| x))
        .bind(f.firmware_version.and_then(|x| x))
        .bind(f.device_role_id.and_then(|x| x)).bind(f.building_id.and_then(|x| x))
        .bind(f.room_id.and_then(|x| x)).bind(f.rack_id.and_then(|x| x))
        .bind(f.management_ip.and_then(|x| x)).bind(f.ssh_username.and_then(|x| x))
        .bind(f.ssh_port.and_then(|x| x))
        .bind(f.management_vrf).bind(f.inband_enabled)
        .bind(f.notes.and_then(|x| x))
        .bind(user_id)
        .fetch_optional(&mut *tx).await?;
    let (new_version,) = updated.ok_or_else(|| EngineError::bad_request(format!(
        "Device {entity_id} not found or deleted — rollback aborted.")))?;

    sqlx::query(
        "UPDATE net.change_set_item
            SET applied_at = NULL, updated_at = now()
          WHERE id = $1 AND organization_id = $2")
        .bind(item.id).bind(org_id).execute(&mut *tx).await?;

    audit::append_tx(&mut tx, &AuditEvent {
        organization_id: org_id,
        source_service: "networking-engine",
        entity_type: "Device",
        entity_id: Some(entity_id),
        action: "RolledBack",
        actor_user_id: user_id,
        actor_display: None,
        client_ip: None,
        correlation_id: Some(correlation_id),
        details: serde_json::json!({
            "original_action": "Update",
            "restored_from_before_json": true,
            "new_version": new_version,
            "change_set_item_id": item.id,
        }),
    }).await?;

    tx.commit().await?;
    Ok(())
}

async fn apply_device_delete(
    pool: &PgPool,
    item: &ChangeSetItem,
    org_id: Uuid,
    correlation_id: Uuid,
    user_id: Option<i32>,
) -> Result<(), EngineError> {
    let entity_id = item.entity_id.ok_or_else(|| EngineError::bad_request(
        "Device/Delete item is missing entity_id"))?;

    let mut tx = pool.begin().await?;

    // Soft delete with OCC — if version doesn't match, abort.
    let affected = match item.expected_version {
        Some(ev) => sqlx::query(
            "UPDATE net.device
                SET deleted_at = now(), deleted_by = $3,
                    updated_at = now(), updated_by = $3, version = version + 1
              WHERE id = $1 AND organization_id = $2
                AND version = $4 AND deleted_at IS NULL")
            .bind(entity_id).bind(org_id).bind(user_id).bind(ev)
            .execute(&mut *tx).await?,
        None => sqlx::query(
            "UPDATE net.device
                SET deleted_at = now(), deleted_by = $3,
                    updated_at = now(), updated_by = $3, version = version + 1
              WHERE id = $1 AND organization_id = $2 AND deleted_at IS NULL")
            .bind(entity_id).bind(org_id).bind(user_id)
            .execute(&mut *tx).await?,
    };
    if affected.rows_affected() == 0 {
        return Err(EngineError::bad_request(format!(
            "Device {entity_id} not found, version mismatch, or already deleted.")));
    }

    sqlx::query(
        "UPDATE net.change_set_item
            SET applied_at = now(), apply_error = NULL, updated_at = now()
          WHERE id = $1 AND organization_id = $2")
        .bind(item.id).bind(org_id).execute(&mut *tx).await?;

    audit::append_tx(&mut tx, &AuditEvent {
        organization_id: org_id,
        source_service: "networking-engine",
        entity_type: "Device",
        entity_id: Some(entity_id),
        action: "Deleted",
        actor_user_id: user_id,
        actor_display: None,
        client_ip: None,
        correlation_id: Some(correlation_id),
        details: serde_json::json!({
            "soft_delete": true,
            "change_set_item_id": item.id,
        }),
    }).await?;

    tx.commit().await?;
    Ok(())
}

async fn rollback_device_delete(
    pool: &PgPool,
    item: &ChangeSetItem,
    org_id: Uuid,
    correlation_id: Uuid,
    user_id: Option<i32>,
) -> Result<(), EngineError> {
    let entity_id = item.entity_id.ok_or_else(|| EngineError::bad_request(
        "Device/Delete rollback is missing entity_id"))?;

    let mut tx = pool.begin().await?;

    // Undelete = clear deleted_at. Surprise-safety: only undelete if the
    // row is still soft-deleted (no one reaped and hard-deleted it in the
    // interim).
    let affected = sqlx::query(
        "UPDATE net.device
            SET deleted_at = NULL, deleted_by = NULL,
                updated_at = now(), updated_by = $3, version = version + 1
          WHERE id = $1 AND organization_id = $2 AND deleted_at IS NOT NULL")
        .bind(entity_id).bind(org_id).bind(user_id)
        .execute(&mut *tx).await?;
    if affected.rows_affected() == 0 {
        return Err(EngineError::bad_request(format!(
            "Device {entity_id} is not soft-deleted — rollback aborted. \
             It may have been hard-deleted by an operator since apply.")));
    }

    sqlx::query(
        "UPDATE net.change_set_item
            SET applied_at = NULL, updated_at = now()
          WHERE id = $1 AND organization_id = $2")
        .bind(item.id).bind(org_id).execute(&mut *tx).await?;

    audit::append_tx(&mut tx, &AuditEvent {
        organization_id: org_id,
        source_service: "networking-engine",
        entity_type: "Device",
        entity_id: Some(entity_id),
        action: "RolledBack",
        actor_user_id: user_id,
        actor_display: None,
        client_ip: None,
        correlation_id: Some(correlation_id),
        details: serde_json::json!({
            "original_action": "Delete",
            "reverse_action": "Undelete",
            "change_set_item_id": item.id,
        }),
    }).await?;

    tx.commit().await?;
    Ok(())
}

// ─── Allocation Create executors (VLAN / ASN / MLAG) ─────────────────────
//
// Allocation flows go through the existing AllocationService so the
// advisory-lock serialisation + shelf cool-down semantics stay unified —
// there's one code path that ever calls INSERT on these tables.
//
// Draft-time vs. apply-time values: the change_set_item records *intent*
// (block_id + display_name + scope), not a specific VLAN number. At apply
// time AllocationService picks the lowest free value. This means admins
// don't see the exact VLAN before apply — they see the block range the
// pick will come from. If deterministic values-at-draft are needed later,
// we can add a "reserve on shelf with TTL at draft" step without changing
// the REST surface.
//
// Rollback flow: soft-delete the allocation row AND retire the value to
// the reservation_shelf with a short cool-down. Without the shelf stamp,
// a concurrent apply could immediately re-allocate the same number,
// which would confuse the audit trail.

use crate::allocation::AllocationService;
use crate::ip_allocation::IpAllocationService;
use crate::models::{PoolScopeLevel, ShelfResourceType};

fn parse_scope_level(s: &str) -> Result<PoolScopeLevel, EngineError> {
    match s {
        "Free"     => Ok(PoolScopeLevel::Free),
        "Region"   => Ok(PoolScopeLevel::Region),
        "Site"     => Ok(PoolScopeLevel::Site),
        "Building" => Ok(PoolScopeLevel::Building),
        "Floor"    => Ok(PoolScopeLevel::Floor),
        "Room"     => Ok(PoolScopeLevel::Room),
        "Device"   => Ok(PoolScopeLevel::Device),
        other => Err(EngineError::bad_request(format!(
            "Unknown scope_level '{other}'"))),
    }
}

fn read_uuid(j: &serde_json::Value, camel: &str, snake: &str) -> Option<Uuid> {
    let v = j.as_object()?.get(camel).or_else(|| j.as_object()?.get(snake))?;
    v.as_str().and_then(|s| Uuid::parse_str(s).ok())
}
fn read_string(j: &serde_json::Value, camel: &str, snake: &str) -> Option<String> {
    let v = j.as_object()?.get(camel).or_else(|| j.as_object()?.get(snake))?;
    v.as_str().map(String::from)
}

async fn apply_vlan_create(
    pool: &PgPool,
    item: &ChangeSetItem,
    org_id: Uuid,
    correlation_id: Uuid,
    user_id: Option<i32>,
) -> Result<(), EngineError> {
    let after = item.after_json.as_ref().ok_or_else(|| EngineError::bad_request(
        "Vlan/Create is missing after_json"))?;
    let block_id = read_uuid(after, "blockId", "block_id").ok_or_else(||
        EngineError::bad_request("after_json.blockId is required for Vlan/Create"))?;
    let display_name = read_string(after, "displayName", "display_name").ok_or_else(||
        EngineError::bad_request("after_json.displayName is required"))?;
    let description = read_string(after, "description", "description");
    let scope_level = parse_scope_level(
        &read_string(after, "scopeLevel", "scope_level").unwrap_or_else(|| "Free".into()))?;
    let scope_entity_id = read_uuid(after, "scopeEntityId", "scope_entity_id");
    let template_id = read_uuid(after, "templateId", "template_id");

    // AllocationService owns its own transaction + lock; we run after it
    // succeeds to stamp the item + audit.
    let svc = AllocationService::new(pool.clone());
    let vlan = svc.allocate_vlan(
        block_id, org_id, &display_name, description.as_deref(),
        scope_level, scope_entity_id, template_id, user_id).await?;

    let mut tx = pool.begin().await?;
    sqlx::query(
        "UPDATE net.change_set_item
            SET entity_id = $3, applied_at = now(), apply_error = NULL,
                updated_at = now()
          WHERE id = $1 AND organization_id = $2")
        .bind(item.id).bind(org_id).bind(vlan.id)
        .execute(&mut *tx).await?;

    audit::append_tx(&mut tx, &AuditEvent {
        organization_id: org_id,
        source_service: "networking-engine",
        entity_type: "Vlan",
        entity_id: Some(vlan.id),
        action: "Created",
        actor_user_id: user_id,
        actor_display: None,
        client_ip: None,
        correlation_id: Some(correlation_id),
        details: serde_json::json!({
            "vlan_id": vlan.vlan_id,
            "block_id": block_id,
            "display_name": display_name,
            "change_set_item_id": item.id,
        }),
    }).await?;
    tx.commit().await?;
    Ok(())
}

async fn rollback_vlan_create(
    pool: &PgPool,
    item: &ChangeSetItem,
    org_id: Uuid,
    correlation_id: Uuid,
    user_id: Option<i32>,
) -> Result<(), EngineError> {
    let entity_id = item.entity_id.ok_or_else(|| EngineError::bad_request(
        "Vlan/Create rollback: item has no entity_id — apply didn't record it."))?;

    // Grab the vlan number so we can shelf it — stops a racing apply from
    // immediately re-issuing it with a different display_name, which
    // would make the audit trail confusing.
    let vlan_num: Option<(i32, Uuid)> = sqlx::query_as(
        "SELECT vlan_id, block_id FROM net.vlan
          WHERE id = $1 AND organization_id = $2")
        .bind(entity_id).bind(org_id).fetch_optional(pool).await?;
    let (vlan_num, block_id) = vlan_num.ok_or_else(|| EngineError::bad_request(format!(
        "Vlan {entity_id} not found — already rolled back or hard-deleted.")))?;

    let mut tx = pool.begin().await?;

    let affected = sqlx::query(
        "UPDATE net.vlan
            SET deleted_at = now(), deleted_by = $3,
                updated_at = now(), updated_by = $3, version = version + 1
          WHERE id = $1 AND organization_id = $2 AND deleted_at IS NULL")
        .bind(entity_id).bind(org_id).bind(user_id)
        .execute(&mut *tx).await?;
    if affected.rows_affected() == 0 {
        return Err(EngineError::bad_request(format!(
            "Vlan {entity_id} already deleted.")));
    }

    sqlx::query(
        "UPDATE net.change_set_item
            SET applied_at = NULL, updated_at = now()
          WHERE id = $1 AND organization_id = $2")
        .bind(item.id).bind(org_id).execute(&mut *tx).await?;

    audit::append_tx(&mut tx, &AuditEvent {
        organization_id: org_id,
        source_service: "networking-engine",
        entity_type: "Vlan",
        entity_id: Some(entity_id),
        action: "RolledBack",
        actor_user_id: user_id,
        actor_display: None,
        client_ip: None,
        correlation_id: Some(correlation_id),
        details: serde_json::json!({
            "original_action": "Create",
            "reverse_action": "SoftDelete",
            "vlan_id": vlan_num,
            "block_id": block_id,
            "change_set_item_id": item.id,
        }),
    }).await?;
    tx.commit().await?;

    // Shelf the number with a short cool-down — outside the transaction
    // above because the shelf write opens its own tx. Non-fatal on
    // failure: the allocation is already soft-deleted, the shelf entry
    // is a nice-to-have for audit visibility + same-tenant safety.
    let svc = AllocationService::new(pool.clone());
    let _ = svc.retire(
        org_id, ShelfResourceType::Vlan, &vlan_num.to_string(),
        chrono::Duration::minutes(5),
        None, Some(block_id),
        Some("change set rolled back"), user_id).await;

    Ok(())
}

async fn apply_asn_create(
    pool: &PgPool,
    item: &ChangeSetItem,
    org_id: Uuid,
    correlation_id: Uuid,
    user_id: Option<i32>,
) -> Result<(), EngineError> {
    let after = item.after_json.as_ref().ok_or_else(|| EngineError::bad_request(
        "AsnAllocation/Create is missing after_json"))?;
    let block_id = read_uuid(after, "blockId", "block_id").ok_or_else(||
        EngineError::bad_request("after_json.blockId is required"))?;
    let allocated_to_type = read_string(after, "allocatedToType", "allocated_to_type")
        .unwrap_or_else(|| "Device".into());
    let allocated_to_id = read_uuid(after, "allocatedToId", "allocated_to_id")
        .unwrap_or_else(Uuid::new_v4); // placeholder for unbound allocations

    let svc = AllocationService::new(pool.clone());
    let asn = svc.allocate_asn(block_id, org_id, &allocated_to_type, allocated_to_id, user_id).await?;

    let mut tx = pool.begin().await?;
    sqlx::query(
        "UPDATE net.change_set_item
            SET entity_id = $3, applied_at = now(), apply_error = NULL,
                updated_at = now()
          WHERE id = $1 AND organization_id = $2")
        .bind(item.id).bind(org_id).bind(asn.id)
        .execute(&mut *tx).await?;

    audit::append_tx(&mut tx, &AuditEvent {
        organization_id: org_id,
        source_service: "networking-engine",
        entity_type: "AsnAllocation",
        entity_id: Some(asn.id),
        action: "Created",
        actor_user_id: user_id,
        actor_display: None,
        client_ip: None,
        correlation_id: Some(correlation_id),
        details: serde_json::json!({
            "asn": asn.asn,
            "block_id": block_id,
            "allocated_to_type": allocated_to_type,
            "allocated_to_id": allocated_to_id,
            "change_set_item_id": item.id,
        }),
    }).await?;
    tx.commit().await?;
    Ok(())
}

async fn rollback_asn_create(
    pool: &PgPool,
    item: &ChangeSetItem,
    org_id: Uuid,
    correlation_id: Uuid,
    user_id: Option<i32>,
) -> Result<(), EngineError> {
    let entity_id = item.entity_id.ok_or_else(|| EngineError::bad_request(
        "AsnAllocation/Create rollback: item has no entity_id."))?;

    let asn_num: Option<(i64, Uuid)> = sqlx::query_as(
        "SELECT asn, block_id FROM net.asn_allocation
          WHERE id = $1 AND organization_id = $2")
        .bind(entity_id).bind(org_id).fetch_optional(pool).await?;
    let (asn_num, block_id) = asn_num.ok_or_else(|| EngineError::bad_request(format!(
        "AsnAllocation {entity_id} not found.")))?;

    let mut tx = pool.begin().await?;
    let affected = sqlx::query(
        "UPDATE net.asn_allocation
            SET deleted_at = now(), deleted_by = $3,
                updated_at = now(), updated_by = $3, version = version + 1
          WHERE id = $1 AND organization_id = $2 AND deleted_at IS NULL")
        .bind(entity_id).bind(org_id).bind(user_id)
        .execute(&mut *tx).await?;
    if affected.rows_affected() == 0 {
        return Err(EngineError::bad_request(format!(
            "AsnAllocation {entity_id} already deleted.")));
    }

    sqlx::query(
        "UPDATE net.change_set_item
            SET applied_at = NULL, updated_at = now()
          WHERE id = $1 AND organization_id = $2")
        .bind(item.id).bind(org_id).execute(&mut *tx).await?;

    audit::append_tx(&mut tx, &AuditEvent {
        organization_id: org_id,
        source_service: "networking-engine",
        entity_type: "AsnAllocation",
        entity_id: Some(entity_id),
        action: "RolledBack",
        actor_user_id: user_id,
        actor_display: None,
        client_ip: None,
        correlation_id: Some(correlation_id),
        details: serde_json::json!({
            "original_action": "Create",
            "reverse_action": "SoftDelete",
            "asn": asn_num,
            "block_id": block_id,
            "change_set_item_id": item.id,
        }),
    }).await?;
    tx.commit().await?;

    let svc = AllocationService::new(pool.clone());
    let _ = svc.retire(
        org_id, ShelfResourceType::Asn, &asn_num.to_string(),
        chrono::Duration::minutes(5),
        None, Some(block_id),
        Some("change set rolled back"), user_id).await;

    Ok(())
}

async fn apply_mlag_create(
    pool: &PgPool,
    item: &ChangeSetItem,
    org_id: Uuid,
    correlation_id: Uuid,
    user_id: Option<i32>,
) -> Result<(), EngineError> {
    let after = item.after_json.as_ref().ok_or_else(|| EngineError::bad_request(
        "MlagDomain/Create is missing after_json"))?;
    let pool_id = read_uuid(after, "poolId", "pool_id").ok_or_else(||
        EngineError::bad_request("after_json.poolId is required"))?;
    let display_name = read_string(after, "displayName", "display_name").ok_or_else(||
        EngineError::bad_request("after_json.displayName is required"))?;
    let scope_level = parse_scope_level(
        &read_string(after, "scopeLevel", "scope_level").unwrap_or_else(|| "Free".into()))?;
    let scope_entity_id = read_uuid(after, "scopeEntityId", "scope_entity_id");

    let svc = AllocationService::new(pool.clone());
    let mlag = svc.allocate_mlag_domain(
        pool_id, org_id, &display_name, scope_level, scope_entity_id, user_id).await?;

    let mut tx = pool.begin().await?;
    sqlx::query(
        "UPDATE net.change_set_item
            SET entity_id = $3, applied_at = now(), apply_error = NULL,
                updated_at = now()
          WHERE id = $1 AND organization_id = $2")
        .bind(item.id).bind(org_id).bind(mlag.id)
        .execute(&mut *tx).await?;

    audit::append_tx(&mut tx, &AuditEvent {
        organization_id: org_id,
        source_service: "networking-engine",
        entity_type: "MlagDomain",
        entity_id: Some(mlag.id),
        action: "Created",
        actor_user_id: user_id,
        actor_display: None,
        client_ip: None,
        correlation_id: Some(correlation_id),
        details: serde_json::json!({
            "domain_id": mlag.domain_id,
            "pool_id": pool_id,
            "display_name": display_name,
            "change_set_item_id": item.id,
        }),
    }).await?;
    tx.commit().await?;
    Ok(())
}

async fn rollback_mlag_create(
    pool: &PgPool,
    item: &ChangeSetItem,
    org_id: Uuid,
    correlation_id: Uuid,
    user_id: Option<i32>,
) -> Result<(), EngineError> {
    let entity_id = item.entity_id.ok_or_else(|| EngineError::bad_request(
        "MlagDomain/Create rollback: item has no entity_id."))?;

    let mlag_num: Option<(i32, Uuid)> = sqlx::query_as(
        "SELECT domain_id, pool_id FROM net.mlag_domain
          WHERE id = $1 AND organization_id = $2")
        .bind(entity_id).bind(org_id).fetch_optional(pool).await?;
    let (mlag_num, pool_id) = mlag_num.ok_or_else(|| EngineError::bad_request(format!(
        "MlagDomain {entity_id} not found.")))?;

    let mut tx = pool.begin().await?;
    let affected = sqlx::query(
        "UPDATE net.mlag_domain
            SET deleted_at = now(), deleted_by = $3,
                updated_at = now(), updated_by = $3, version = version + 1
          WHERE id = $1 AND organization_id = $2 AND deleted_at IS NULL")
        .bind(entity_id).bind(org_id).bind(user_id)
        .execute(&mut *tx).await?;
    if affected.rows_affected() == 0 {
        return Err(EngineError::bad_request(format!(
            "MlagDomain {entity_id} already deleted.")));
    }

    sqlx::query(
        "UPDATE net.change_set_item
            SET applied_at = NULL, updated_at = now()
          WHERE id = $1 AND organization_id = $2")
        .bind(item.id).bind(org_id).execute(&mut *tx).await?;

    audit::append_tx(&mut tx, &AuditEvent {
        organization_id: org_id,
        source_service: "networking-engine",
        entity_type: "MlagDomain",
        entity_id: Some(entity_id),
        action: "RolledBack",
        actor_user_id: user_id,
        actor_display: None,
        client_ip: None,
        correlation_id: Some(correlation_id),
        details: serde_json::json!({
            "original_action": "Create",
            "reverse_action": "SoftDelete",
            "domain_id": mlag_num,
            "pool_id": pool_id,
            "change_set_item_id": item.id,
        }),
    }).await?;
    tx.commit().await?;

    let svc = AllocationService::new(pool.clone());
    let _ = svc.retire(
        org_id, ShelfResourceType::Mlag, &mlag_num.to_string(),
        chrono::Duration::minutes(5),
        Some(pool_id), None,
        Some("change set rolled back"), user_id).await;

    Ok(())
}

// ─── Subnet + IP allocation Create executors ─────────────────────────────
//
// Subnets carve a CIDR out of an ip_pool; IP addresses allocate the next
// free host inside a subnet. Both go through IpAllocationService so the
// GIST EXCLUDE overlap constraint + advisory-lock semantics stay unified.
//
// Shelf resource keys: subnets store the CIDR string, ips the address.
// Both retire with a 5-minute cool-down on rollback.

async fn apply_subnet_create(
    pool: &PgPool,
    item: &ChangeSetItem,
    org_id: Uuid,
    correlation_id: Uuid,
    user_id: Option<i32>,
) -> Result<(), EngineError> {
    let after = item.after_json.as_ref().ok_or_else(|| EngineError::bad_request(
        "Subnet/Create is missing after_json"))?;
    let pool_id = read_uuid(after, "poolId", "pool_id").ok_or_else(||
        EngineError::bad_request("after_json.poolId is required"))?;
    let prefix_length = after.as_object()
        .and_then(|o| o.get("prefixLength").or_else(|| o.get("prefix_length")))
        .and_then(|v| v.as_i64())
        .and_then(|n| u32::try_from(n).ok())
        .ok_or_else(|| EngineError::bad_request(
            "after_json.prefixLength is required and must be 0..128"))?;
    let subnet_code = read_string(after, "subnetCode", "subnet_code").ok_or_else(||
        EngineError::bad_request("after_json.subnetCode is required"))?;
    let display_name = read_string(after, "displayName", "display_name").ok_or_else(||
        EngineError::bad_request("after_json.displayName is required"))?;
    let scope_level = parse_scope_level(
        &read_string(after, "scopeLevel", "scope_level").unwrap_or_else(|| "Free".into()))?;
    let scope_entity_id = read_uuid(after, "scopeEntityId", "scope_entity_id");
    let parent_subnet_id = read_uuid(after, "parentSubnetId", "parent_subnet_id");

    let svc = IpAllocationService::new(pool.clone());
    let subnet = svc.allocate_subnet(
        pool_id, org_id, prefix_length, &subnet_code, &display_name,
        scope_level, scope_entity_id, parent_subnet_id, user_id).await?;

    let mut tx = pool.begin().await?;
    sqlx::query(
        "UPDATE net.change_set_item
            SET entity_id = $3, applied_at = now(), apply_error = NULL,
                updated_at = now()
          WHERE id = $1 AND organization_id = $2")
        .bind(item.id).bind(org_id).bind(subnet.id)
        .execute(&mut *tx).await?;

    audit::append_tx(&mut tx, &AuditEvent {
        organization_id: org_id,
        source_service: "networking-engine",
        entity_type: "Subnet",
        entity_id: Some(subnet.id),
        action: "Created",
        actor_user_id: user_id,
        actor_display: None,
        client_ip: None,
        correlation_id: Some(correlation_id),
        details: serde_json::json!({
            "pool_id": pool_id,
            "subnet_code": subnet.subnet_code,
            "network": subnet.network,
            "prefix_length": prefix_length,
            "change_set_item_id": item.id,
        }),
    }).await?;
    tx.commit().await?;
    Ok(())
}

async fn rollback_subnet_create(
    pool: &PgPool,
    item: &ChangeSetItem,
    org_id: Uuid,
    correlation_id: Uuid,
    user_id: Option<i32>,
) -> Result<(), EngineError> {
    let entity_id = item.entity_id.ok_or_else(|| EngineError::bad_request(
        "Subnet/Create rollback: item has no entity_id."))?;

    // Grab CIDR + pool so we can shelf the carved range.
    let row: Option<(String, Uuid)> = sqlx::query_as(
        "SELECT network::text, pool_id FROM net.subnet
          WHERE id = $1 AND organization_id = $2")
        .bind(entity_id).bind(org_id).fetch_optional(pool).await?;
    let (cidr, pool_id) = row.ok_or_else(|| EngineError::bad_request(format!(
        "Subnet {entity_id} not found.")))?;

    let mut tx = pool.begin().await?;
    let affected = sqlx::query(
        "UPDATE net.subnet
            SET deleted_at = now(), deleted_by = $3,
                updated_at = now(), updated_by = $3, version = version + 1
          WHERE id = $1 AND organization_id = $2 AND deleted_at IS NULL")
        .bind(entity_id).bind(org_id).bind(user_id)
        .execute(&mut *tx).await?;
    if affected.rows_affected() == 0 {
        return Err(EngineError::bad_request(format!(
            "Subnet {entity_id} already deleted.")));
    }

    sqlx::query(
        "UPDATE net.change_set_item
            SET applied_at = NULL, updated_at = now()
          WHERE id = $1 AND organization_id = $2")
        .bind(item.id).bind(org_id).execute(&mut *tx).await?;

    audit::append_tx(&mut tx, &AuditEvent {
        organization_id: org_id,
        source_service: "networking-engine",
        entity_type: "Subnet",
        entity_id: Some(entity_id),
        action: "RolledBack",
        actor_user_id: user_id,
        actor_display: None,
        client_ip: None,
        correlation_id: Some(correlation_id),
        details: serde_json::json!({
            "original_action": "Create",
            "reverse_action": "SoftDelete",
            "network": cidr,
            "pool_id": pool_id,
            "change_set_item_id": item.id,
        }),
    }).await?;
    tx.commit().await?;

    // Shelf the CIDR so a racing apply doesn't re-carve the same range
    // under a different subnet_code in the same window. 5-min cooldown —
    // enough to cover admin "oh shit, undo" flow without holding the
    // space forever.
    let svc = AllocationService::new(pool.clone());
    let _ = svc.retire(
        org_id, ShelfResourceType::Subnet, &cidr,
        chrono::Duration::minutes(5),
        Some(pool_id), None,
        Some("change set rolled back"), user_id).await;

    Ok(())
}

async fn apply_ip_create(
    pool: &PgPool,
    item: &ChangeSetItem,
    org_id: Uuid,
    correlation_id: Uuid,
    user_id: Option<i32>,
) -> Result<(), EngineError> {
    let after = item.after_json.as_ref().ok_or_else(|| EngineError::bad_request(
        "IpAddress/Create is missing after_json"))?;
    let subnet_id = read_uuid(after, "subnetId", "subnet_id").ok_or_else(||
        EngineError::bad_request("after_json.subnetId is required"))?;
    let assigned_to_type = read_string(after, "assignedToType", "assigned_to_type");
    let assigned_to_id = read_uuid(after, "assignedToId", "assigned_to_id");

    let svc = IpAllocationService::new(pool.clone());
    let ip = svc.allocate_next_ip(
        subnet_id, org_id,
        assigned_to_type.as_deref(), assigned_to_id, user_id).await?;

    let mut tx = pool.begin().await?;
    sqlx::query(
        "UPDATE net.change_set_item
            SET entity_id = $3, applied_at = now(), apply_error = NULL,
                updated_at = now()
          WHERE id = $1 AND organization_id = $2")
        .bind(item.id).bind(org_id).bind(ip.id)
        .execute(&mut *tx).await?;

    audit::append_tx(&mut tx, &AuditEvent {
        organization_id: org_id,
        source_service: "networking-engine",
        entity_type: "IpAddress",
        entity_id: Some(ip.id),
        action: "Created",
        actor_user_id: user_id,
        actor_display: None,
        client_ip: None,
        correlation_id: Some(correlation_id),
        details: serde_json::json!({
            "subnet_id": subnet_id,
            "address": ip.address,
            "assigned_to_type": assigned_to_type,
            "assigned_to_id": assigned_to_id,
            "change_set_item_id": item.id,
        }),
    }).await?;
    tx.commit().await?;
    Ok(())
}

async fn rollback_ip_create(
    pool: &PgPool,
    item: &ChangeSetItem,
    org_id: Uuid,
    correlation_id: Uuid,
    user_id: Option<i32>,
) -> Result<(), EngineError> {
    let entity_id = item.entity_id.ok_or_else(|| EngineError::bad_request(
        "IpAddress/Create rollback: item has no entity_id."))?;

    let row: Option<(String, Uuid)> = sqlx::query_as(
        "SELECT address::text, subnet_id FROM net.ip_address
          WHERE id = $1 AND organization_id = $2")
        .bind(entity_id).bind(org_id).fetch_optional(pool).await?;
    let (address, subnet_id) = row.ok_or_else(|| EngineError::bad_request(format!(
        "IpAddress {entity_id} not found.")))?;

    // Some Postgres cidr casts produce "10.0.0.1/32" — strip prefix so the
    // shelf key matches what the allocator will check for.
    let bare_addr = match address.find('/') {
        Some(i) => &address[..i],
        None => &address,
    };

    let mut tx = pool.begin().await?;
    let affected = sqlx::query(
        "UPDATE net.ip_address
            SET deleted_at = now(), deleted_by = $3,
                updated_at = now(), updated_by = $3, version = version + 1
          WHERE id = $1 AND organization_id = $2 AND deleted_at IS NULL")
        .bind(entity_id).bind(org_id).bind(user_id)
        .execute(&mut *tx).await?;
    if affected.rows_affected() == 0 {
        return Err(EngineError::bad_request(format!(
            "IpAddress {entity_id} already deleted.")));
    }

    sqlx::query(
        "UPDATE net.change_set_item
            SET applied_at = NULL, updated_at = now()
          WHERE id = $1 AND organization_id = $2")
        .bind(item.id).bind(org_id).execute(&mut *tx).await?;

    audit::append_tx(&mut tx, &AuditEvent {
        organization_id: org_id,
        source_service: "networking-engine",
        entity_type: "IpAddress",
        entity_id: Some(entity_id),
        action: "RolledBack",
        actor_user_id: user_id,
        actor_display: None,
        client_ip: None,
        correlation_id: Some(correlation_id),
        details: serde_json::json!({
            "original_action": "Create",
            "reverse_action": "SoftDelete",
            "address": bare_addr,
            "subnet_id": subnet_id,
            "change_set_item_id": item.id,
        }),
    }).await?;
    tx.commit().await?;

    let svc = AllocationService::new(pool.clone());
    let _ = svc.retire(
        org_id, ShelfResourceType::Ip, bare_addr,
        chrono::Duration::minutes(5),
        None, None,
        Some("change set rolled back"), user_id).await;

    Ok(())
}

// ─── Row adapters ────────────────────────────────────────────────────────

#[derive(sqlx::FromRow)]
struct ChangeSetRow {
    id: Uuid,
    organization_id: Uuid,
    title: String,
    description: Option<String>,
    status: String,
    requested_by: Option<i32>,
    requested_by_display: Option<String>,
    submitted_by: Option<i32>,
    submitted_at: Option<DateTime<Utc>>,
    approved_at: Option<DateTime<Utc>>,
    applied_at: Option<DateTime<Utc>>,
    rolled_back_at: Option<DateTime<Utc>>,
    cancelled_at: Option<DateTime<Utc>>,
    required_approvals: Option<i32>,
    correlation_id: Uuid,
    version: i32,
    created_at: DateTime<Utc>,
    updated_at: DateTime<Utc>,
}

impl ChangeSetRow {
    fn into_dto(self, item_count: i64) -> Result<ChangeSet, EngineError> {
        Ok(ChangeSet {
            id: self.id,
            organization_id: self.organization_id,
            title: self.title,
            description: self.description,
            status: ChangeSetStatus::from_db(&self.status)?,
            requested_by: self.requested_by,
            requested_by_display: self.requested_by_display,
            submitted_by: self.submitted_by,
            submitted_at: self.submitted_at,
            approved_at: self.approved_at,
            applied_at: self.applied_at,
            rolled_back_at: self.rolled_back_at,
            cancelled_at: self.cancelled_at,
            required_approvals: self.required_approvals,
            correlation_id: self.correlation_id,
            version: self.version,
            item_count,
            created_at: self.created_at,
            updated_at: self.updated_at,
        })
    }
}

#[derive(sqlx::FromRow)]
struct ApprovalRowDb {
    id: Uuid,
    change_set_id: Uuid,
    approver_user_id: i32,
    approver_display: Option<String>,
    decision: String,
    decided_at: DateTime<Utc>,
    notes: Option<String>,
}

impl ApprovalRowDb {
    fn into_dto(self) -> Result<ApprovalRow, EngineError> {
        Ok(ApprovalRow {
            id: self.id,
            change_set_id: self.change_set_id,
            approver_user_id: self.approver_user_id,
            approver_display: self.approver_display,
            decision: match self.decision.as_str() {
                "Approve" => ChangeSetDecision::Approve,
                "Reject" => ChangeSetDecision::Reject,
                other => return Err(EngineError::bad_request(format!(
                    "Unknown change_set_decision '{other}'"))),
            },
            decided_at: self.decided_at,
            notes: self.notes,
        })
    }
}

#[derive(sqlx::FromRow)]
struct ChangeSetItemRow {
    id: Uuid,
    change_set_id: Uuid,
    item_order: i32,
    entity_type: String,
    entity_id: Option<Uuid>,
    action: String,
    before_json: Option<serde_json::Value>,
    after_json: Option<serde_json::Value>,
    expected_version: Option<i32>,
    applied_at: Option<DateTime<Utc>>,
    apply_error: Option<String>,
    notes: Option<String>,
    created_at: DateTime<Utc>,
}

impl ChangeSetItemRow {
    fn into_dto(self) -> Result<ChangeSetItem, EngineError> {
        Ok(ChangeSetItem {
            id: self.id,
            change_set_id: self.change_set_id,
            item_order: self.item_order,
            entity_type: self.entity_type,
            entity_id: self.entity_id,
            action: ChangeSetAction::from_db(&self.action)?,
            before_json: self.before_json,
            after_json: self.after_json,
            expected_version: self.expected_version,
            applied_at: self.applied_at,
            apply_error: self.apply_error,
            notes: self.notes,
            created_at: self.created_at,
        })
    }
}

// ─── Tests ───────────────────────────────────────────────────────────────

#[cfg(test)]
mod tests {
    use super::*;

    fn v(j: serde_json::Value) -> Option<serde_json::Value> { Some(j) }

    #[test]
    fn create_requires_after_json_and_no_entity_id() {
        // Missing after_json.
        assert!(validate_item(&AddItemBody {
            entity_type: "Device".into(), entity_id: None,
            action: ChangeSetAction::Create,
            before_json: None, after_json: None,
            expected_version: None, notes: None,
        }).is_err());

        // entity_id supplied is wrong for Create.
        assert!(validate_item(&AddItemBody {
            entity_type: "Device".into(), entity_id: Some(Uuid::new_v4()),
            action: ChangeSetAction::Create,
            before_json: None, after_json: v(serde_json::json!({})),
            expected_version: None, notes: None,
        }).is_err());

        // Happy path.
        assert!(validate_item(&AddItemBody {
            entity_type: "Device".into(), entity_id: None,
            action: ChangeSetAction::Create,
            before_json: None, after_json: v(serde_json::json!({"hostname":"x"})),
            expected_version: None, notes: None,
        }).is_ok());
    }

    #[test]
    fn update_and_rename_require_entity_id_and_after_json() {
        for action in [ChangeSetAction::Update, ChangeSetAction::Rename] {
            // Missing entity_id.
            assert!(validate_item(&AddItemBody {
                entity_type: "Device".into(), entity_id: None,
                action,
                before_json: None, after_json: v(serde_json::json!({})),
                expected_version: None, notes: None,
            }).is_err());

            // Missing after_json.
            assert!(validate_item(&AddItemBody {
                entity_type: "Device".into(), entity_id: Some(Uuid::new_v4()),
                action,
                before_json: None, after_json: None,
                expected_version: None, notes: None,
            }).is_err());

            // Happy path.
            assert!(validate_item(&AddItemBody {
                entity_type: "Device".into(), entity_id: Some(Uuid::new_v4()),
                action,
                before_json: None, after_json: v(serde_json::json!({})),
                expected_version: None, notes: None,
            }).is_ok());
        }
    }

    #[test]
    fn delete_requires_entity_id_and_rejects_after_json() {
        // Missing entity_id.
        assert!(validate_item(&AddItemBody {
            entity_type: "Device".into(), entity_id: None,
            action: ChangeSetAction::Delete,
            before_json: None, after_json: None,
            expected_version: None, notes: None,
        }).is_err());

        // after_json must not be present for Delete.
        assert!(validate_item(&AddItemBody {
            entity_type: "Device".into(), entity_id: Some(Uuid::new_v4()),
            action: ChangeSetAction::Delete,
            before_json: None, after_json: v(serde_json::json!({})),
            expected_version: None, notes: None,
        }).is_err());

        // Happy path.
        assert!(validate_item(&AddItemBody {
            entity_type: "Device".into(), entity_id: Some(Uuid::new_v4()),
            action: ChangeSetAction::Delete,
            before_json: None, after_json: None,
            expected_version: None, notes: None,
        }).is_ok());
    }

    #[test]
    fn empty_entity_type_rejected() {
        assert!(validate_item(&AddItemBody {
            entity_type: "   ".into(), entity_id: Some(Uuid::new_v4()),
            action: ChangeSetAction::Update,
            before_json: None, after_json: v(serde_json::json!({})),
            expected_version: None, notes: None,
        }).is_err());
    }

    #[test]
    fn status_terminal_classification() {
        assert!(ChangeSetStatus::Rejected.is_terminal());
        assert!(ChangeSetStatus::Applied.is_terminal());
        assert!(ChangeSetStatus::Cancelled.is_terminal());
        assert!(!ChangeSetStatus::Draft.is_terminal());
        assert!(!ChangeSetStatus::Submitted.is_terminal());
        assert!(!ChangeSetStatus::Approved.is_terminal());
        assert!(!ChangeSetStatus::RolledBack.is_terminal());
    }

    #[test]
    fn status_from_db_round_trips_every_variant() {
        for v in [
            ChangeSetStatus::Draft, ChangeSetStatus::Submitted,
            ChangeSetStatus::Approved, ChangeSetStatus::Rejected,
            ChangeSetStatus::Applied, ChangeSetStatus::RolledBack,
            ChangeSetStatus::Cancelled,
        ] {
            assert_eq!(ChangeSetStatus::from_db(v.as_str()).unwrap(), v);
        }
        assert!(ChangeSetStatus::from_db("Invalid").is_err());
    }

    #[test]
    fn action_from_db_round_trips_every_variant() {
        for v in [ChangeSetAction::Create, ChangeSetAction::Update,
                  ChangeSetAction::Delete, ChangeSetAction::Rename] {
            assert_eq!(ChangeSetAction::from_db(v.as_str()).unwrap(), v);
        }
        assert!(ChangeSetAction::from_db("Explode").is_err());
    }
}
