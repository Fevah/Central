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
        (et, act) => Err(EngineError::bad_request(format!(
            "Apply not yet implemented for ({et}, {}). Device/Rename is \
             the only concrete path in this slice; other combinations \
             are queued for follow-on slices.", act.as_str()))),
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
        (et, act) => Err(EngineError::bad_request(format!(
            "Rollback not yet implemented for ({et}, {}). Device/Rename is \
             the only concrete path in this slice.", act.as_str()))),
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
