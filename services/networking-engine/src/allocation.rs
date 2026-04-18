//! Integer-space numbering allocator (ASN / VLAN / MLAG) + reservation shelf.
//!
//! Port of `libs/persistence/Net/AllocationService.cs`. Every write into
//! `net.asn_allocation`, `net.vlan`, `net.mlag_domain`, and `net.reservation_shelf`
//! goes through here. Invariants:
//!
//! - Advisory-lock serialisation per container via `pg_advisory_xact_lock`.
//! - Shelf-cooldown check: values still on the shelf are skipped.
//! - Range containment and container-exists checks happen inside the tx.
//!
//! IP / subnet allocation lives in [`crate::ip_allocation`] — inet arithmetic is
//! a different shape of algorithm.

use chrono::Duration;
use sqlx::{PgPool, Postgres, Transaction};
use std::collections::HashSet;
use uuid::Uuid;

use crate::audit::{self, AuditEvent};
use crate::error::EngineError;
use crate::hash::stable_hash;
use crate::models::{AsnAllocation, MlagDomain, PoolScopeLevel, ReservationShelfEntry, ShelfResourceType, Vlan};

#[derive(Clone)]
pub struct AllocationService {
    pool: PgPool,
}

impl AllocationService {
    pub fn new(pool: PgPool) -> Self { Self { pool } }

    // ─── ASN ──────────────────────────────────────────────────────────────

    pub async fn allocate_asn(
        &self,
        block_id: Uuid,
        org_id: Uuid,
        allocated_to_type: &str,
        allocated_to_id: Uuid,
        user_id: Option<i32>,
    ) -> Result<AsnAllocation, EngineError> {
        let mut tx = self.pool.begin().await?;
        acquire_lock(&mut tx, block_id).await?;

        let (first, last) = fetch_asn_block_range(&mut tx, block_id, org_id).await?;
        let used = fetch_used_asns(&mut tx, block_id).await?;
        let shelved = fetch_shelf(&mut tx, org_id, ShelfResourceType::Asn).await?;
        let next = next_free_integer(first, last, &used, &shelved)
            .ok_or_else(|| EngineError::pool_exhausted("ASN", block_id))?;

        let row: (Uuid, chrono::DateTime<chrono::Utc>) = sqlx::query_as(
            "INSERT INTO net.asn_allocation
                (organization_id, block_id, asn, allocated_to_type, allocated_to_id,
                 status, lock_state, created_by, updated_by)
             VALUES ($1, $2, $3, $4, $5,
                     'Active'::net.entity_status, 'Open'::net.lock_state, $6, $6)
             RETURNING id, allocated_at")
            .bind(org_id)
            .bind(block_id)
            .bind(next)
            .bind(allocated_to_type)
            .bind(allocated_to_id)
            .bind(user_id)
            .fetch_one(&mut *tx)
            .await?;

        tx.commit().await?;
        Ok(AsnAllocation {
            id: row.0,
            organization_id: org_id,
            block_id,
            asn: next,
            allocated_to_type: allocated_to_type.to_string(),
            allocated_to_id,
            allocated_at: row.1,
        })
    }

    // ─── VLAN ─────────────────────────────────────────────────────────────

    pub async fn allocate_vlan(
        &self,
        block_id: Uuid,
        org_id: Uuid,
        display_name: &str,
        description: Option<&str>,
        scope_level: PoolScopeLevel,
        scope_entity_id: Option<Uuid>,
        template_id: Option<Uuid>,
        user_id: Option<i32>,
    ) -> Result<Vlan, EngineError> {
        let mut tx = self.pool.begin().await?;
        acquire_lock(&mut tx, block_id).await?;

        let (first, last) = fetch_vlan_block_range(&mut tx, block_id, org_id).await?;
        let used = fetch_used_vlans(&mut tx, block_id).await?;
        let shelved = fetch_shelf(&mut tx, org_id, ShelfResourceType::Vlan).await?;
        let next = next_free_integer(first, last, &used, &shelved)
            .ok_or_else(|| EngineError::pool_exhausted("VLAN", block_id))?;
        let vlan_id = next as i32;

        let id: Uuid = sqlx::query_scalar(
            "INSERT INTO net.vlan
                (organization_id, block_id, template_id, vlan_id, display_name, description,
                 scope_level, scope_entity_id,
                 status, lock_state, created_by, updated_by)
             VALUES ($1, $2, $3, $4, $5, $6, $7, $8,
                     'Active'::net.entity_status, 'Open'::net.lock_state, $9, $9)
             RETURNING id")
            .bind(org_id)
            .bind(block_id)
            .bind(template_id)
            .bind(vlan_id)
            .bind(display_name)
            .bind(description)
            .bind(scope_level.as_str())
            .bind(scope_entity_id)
            .bind(user_id)
            .fetch_one(&mut *tx)
            .await?;

        tx.commit().await?;
        Ok(Vlan {
            id,
            organization_id: org_id,
            block_id,
            template_id,
            vlan_id,
            display_name: display_name.to_string(),
            description: description.map(str::to_string),
            scope_level,
            scope_entity_id,
        })
    }

    // ─── MLAG ─────────────────────────────────────────────────────────────

    pub async fn allocate_mlag_domain(
        &self,
        pool_id: Uuid,
        org_id: Uuid,
        display_name: &str,
        scope_level: PoolScopeLevel,
        scope_entity_id: Option<Uuid>,
        user_id: Option<i32>,
    ) -> Result<MlagDomain, EngineError> {
        let mut tx = self.pool.begin().await?;
        acquire_lock(&mut tx, pool_id).await?;

        let (first, last) = fetch_mlag_pool_range(&mut tx, pool_id, org_id).await?;
        let used = fetch_used_mlag_domains(&mut tx, org_id).await?;
        let shelved = fetch_shelf(&mut tx, org_id, ShelfResourceType::Mlag).await?;
        let next = next_free_integer(first, last, &used, &shelved)
            .ok_or_else(|| EngineError::pool_exhausted("MLAG domain", pool_id))?;
        let domain_id = next as i32;

        let id: Uuid = sqlx::query_scalar(
            "INSERT INTO net.mlag_domain
                (organization_id, pool_id, domain_id, display_name,
                 scope_level, scope_entity_id,
                 status, lock_state, created_by, updated_by)
             VALUES ($1, $2, $3, $4, $5, $6,
                     'Active'::net.entity_status, 'Open'::net.lock_state, $7, $7)
             RETURNING id")
            .bind(org_id)
            .bind(pool_id)
            .bind(domain_id)
            .bind(display_name)
            .bind(scope_level.as_str())
            .bind(scope_entity_id)
            .bind(user_id)
            .fetch_one(&mut *tx)
            .await?;

        tx.commit().await?;
        Ok(MlagDomain {
            id,
            organization_id: org_id,
            pool_id,
            domain_id,
            display_name: display_name.to_string(),
            scope_level,
            scope_entity_id,
        })
    }

    // ─── Reservation shelf ────────────────────────────────────────────────

    pub async fn retire(
        &self,
        org_id: Uuid,
        resource_type: ShelfResourceType,
        resource_key: &str,
        cooldown: Duration,
        pool_id: Option<Uuid>,
        block_id: Option<Uuid>,
        reason: Option<&str>,
        user_id: Option<i32>,
    ) -> Result<ReservationShelfEntry, EngineError> {
        if cooldown < Duration::zero() {
            return Err(EngineError::bad_request("cooldown cannot be negative"));
        }

        // Transactional: the shelf insert and the audit append succeed
        // together or both roll back. If the audit chain can't be
        // written (e.g. DB full, tenant lock contention) we fail the
        // retire too — better than a silently-unauditable mutation.
        let mut tx = self.pool.begin().await?;

        let row: (Uuid, chrono::DateTime<chrono::Utc>, chrono::DateTime<chrono::Utc>) = sqlx::query_as(
            "INSERT INTO net.reservation_shelf
                (organization_id, resource_type, resource_key, pool_id, block_id,
                 retired_at, available_after, retired_reason,
                 status, lock_state, created_by, updated_by)
             VALUES ($1, $2, $3, $4, $5,
                     now(), now() + $6, $7,
                     'Active'::net.entity_status, 'Open'::net.lock_state, $8, $8)
             RETURNING id, retired_at, available_after")
            .bind(org_id)
            .bind(resource_type.as_db_str())
            .bind(resource_key)
            .bind(pool_id)
            .bind(block_id)
            .bind(cooldown)
            .bind(reason)
            .bind(user_id)
            .fetch_one(&mut *tx)
            .await?;

        let details = serde_json::json!({
            "resource_type": resource_type.as_db_str(),
            "resource_key": resource_key,
            "pool_id": pool_id,
            "block_id": block_id,
            "cooldown_seconds": cooldown.num_seconds(),
            "reason": reason,
            "available_after": row.2,
        });
        audit::append_tx(&mut tx, &AuditEvent {
            organization_id: org_id,
            source_service: "networking-engine",
            entity_type: "ReservationShelf",
            entity_id: Some(row.0),
            action: "Retired",
            actor_user_id: user_id,
            actor_display: None,
            client_ip: None,
            correlation_id: None,
            details,
        }).await?;

        tx.commit().await?;

        Ok(ReservationShelfEntry {
            id: row.0,
            organization_id: org_id,
            resource_type,
            resource_key: resource_key.to_string(),
            pool_id,
            block_id,
            retired_at: row.1,
            available_after: row.2,
            retired_reason: reason.map(str::to_string),
        })
    }

    pub async fn is_on_shelf(
        &self,
        org_id: Uuid,
        resource_type: ShelfResourceType,
        resource_key: &str,
    ) -> Result<bool, EngineError> {
        let row: Option<i32> = sqlx::query_scalar(
            "SELECT 1 FROM net.reservation_shelf
             WHERE organization_id = $1
               AND resource_type = $2
               AND resource_key = $3
               AND available_after > now()
               AND deleted_at IS NULL
             LIMIT 1")
            .bind(org_id)
            .bind(resource_type.as_db_str())
            .bind(resource_key)
            .fetch_optional(&self.pool)
            .await?;
        Ok(row.is_some())
    }
}

// ─── Helpers ──────────────────────────────────────────────────────────────

/// Acquire a per-container advisory lock for the current transaction.
/// Released automatically on commit/rollback via `pg_advisory_xact_lock`.
pub(crate) async fn acquire_lock(
    tx: &mut Transaction<'_, Postgres>,
    container_id: Uuid,
) -> Result<(), EngineError> {
    let key = stable_hash(container_id);
    sqlx::query("SELECT pg_advisory_xact_lock($1)")
        .bind(key)
        .execute(&mut **tx)
        .await?;
    Ok(())
}

/// Linear scan for the lowest free integer in `[first, last]` excluding `used` and `shelved`.
/// Returns `None` when the range is fully consumed. Fine for sub-million ranges — ASN blocks
/// hold 100s, VLAN blocks up to 2048, MLAG pools < 100.
pub fn next_free_integer(
    first: i64,
    last: i64,
    used: &HashSet<i64>,
    shelved: &HashSet<i64>,
) -> Option<i64> {
    let mut v = first;
    while v <= last {
        if !used.contains(&v) && !shelved.contains(&v) {
            return Some(v);
        }
        v += 1;
    }
    None
}

async fn fetch_asn_block_range(
    tx: &mut Transaction<'_, Postgres>,
    block_id: Uuid,
    org_id: Uuid,
) -> Result<(i64, i64), EngineError> {
    let row: Option<(i64, i64)> = sqlx::query_as(
        "SELECT asn_first, asn_last
         FROM net.asn_block
         WHERE id = $1 AND organization_id = $2 AND deleted_at IS NULL")
        .bind(block_id)
        .bind(org_id)
        .fetch_optional(&mut **tx)
        .await?;
    row.ok_or_else(|| EngineError::container_not_found("asn_block", block_id))
}

async fn fetch_used_asns(
    tx: &mut Transaction<'_, Postgres>,
    block_id: Uuid,
) -> Result<HashSet<i64>, EngineError> {
    let rows: Vec<(i64,)> = sqlx::query_as(
        "SELECT asn FROM net.asn_allocation
         WHERE block_id = $1 AND deleted_at IS NULL")
        .bind(block_id)
        .fetch_all(&mut **tx)
        .await?;
    Ok(rows.into_iter().map(|(a,)| a).collect())
}

async fn fetch_vlan_block_range(
    tx: &mut Transaction<'_, Postgres>,
    block_id: Uuid,
    org_id: Uuid,
) -> Result<(i64, i64), EngineError> {
    let row: Option<(i32, i32)> = sqlx::query_as(
        "SELECT vlan_first, vlan_last
         FROM net.vlan_block
         WHERE id = $1 AND organization_id = $2 AND deleted_at IS NULL")
        .bind(block_id)
        .bind(org_id)
        .fetch_optional(&mut **tx)
        .await?;
    row.map(|(f, l)| (f as i64, l as i64))
        .ok_or_else(|| EngineError::container_not_found("vlan_block", block_id))
}

async fn fetch_used_vlans(
    tx: &mut Transaction<'_, Postgres>,
    block_id: Uuid,
) -> Result<HashSet<i64>, EngineError> {
    let rows: Vec<(i32,)> = sqlx::query_as(
        "SELECT vlan_id FROM net.vlan
         WHERE block_id = $1 AND deleted_at IS NULL")
        .bind(block_id)
        .fetch_all(&mut **tx)
        .await?;
    Ok(rows.into_iter().map(|(v,)| v as i64).collect())
}

async fn fetch_mlag_pool_range(
    tx: &mut Transaction<'_, Postgres>,
    pool_id: Uuid,
    org_id: Uuid,
) -> Result<(i64, i64), EngineError> {
    let row: Option<(i32, i32)> = sqlx::query_as(
        "SELECT domain_first, domain_last
         FROM net.mlag_domain_pool
         WHERE id = $1 AND organization_id = $2 AND deleted_at IS NULL")
        .bind(pool_id)
        .bind(org_id)
        .fetch_optional(&mut **tx)
        .await?;
    row.map(|(f, l)| (f as i64, l as i64))
        .ok_or_else(|| EngineError::container_not_found("mlag_domain_pool", pool_id))
}

async fn fetch_used_mlag_domains(
    tx: &mut Transaction<'_, Postgres>,
    org_id: Uuid,
) -> Result<HashSet<i64>, EngineError> {
    // MLAG domain uniqueness is tenant-wide (not pool-wide).
    let rows: Vec<(i32,)> = sqlx::query_as(
        "SELECT domain_id FROM net.mlag_domain
         WHERE organization_id = $1 AND deleted_at IS NULL")
        .bind(org_id)
        .fetch_all(&mut **tx)
        .await?;
    Ok(rows.into_iter().map(|(d,)| d as i64).collect())
}

async fn fetch_shelf(
    tx: &mut Transaction<'_, Postgres>,
    org_id: Uuid,
    resource_type: ShelfResourceType,
) -> Result<HashSet<i64>, EngineError> {
    let rows: Vec<(String,)> = sqlx::query_as(
        "SELECT resource_key FROM net.reservation_shelf
         WHERE organization_id = $1
           AND resource_type = $2
           AND available_after > now()
           AND deleted_at IS NULL")
        .bind(org_id)
        .bind(resource_type.as_db_str())
        .fetch_all(&mut **tx)
        .await?;
    let mut set = HashSet::new();
    for (key,) in rows {
        if let Ok(v) = key.parse::<i64>() {
            set.insert(v);
        }
    }
    Ok(set)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn next_free_returns_first_when_nothing_used() {
        let used = HashSet::new();
        let shelved = HashSet::new();
        assert_eq!(next_free_integer(65000, 65100, &used, &shelved), Some(65000));
    }

    #[test]
    fn next_free_skips_used() {
        let used: HashSet<i64> = [65000, 65001, 65002].into_iter().collect();
        let shelved = HashSet::new();
        assert_eq!(next_free_integer(65000, 65100, &used, &shelved), Some(65003));
    }

    #[test]
    fn next_free_skips_shelved() {
        let used = HashSet::new();
        let shelved: HashSet<i64> = [65000].into_iter().collect();
        assert_eq!(next_free_integer(65000, 65100, &used, &shelved), Some(65001));
    }

    #[test]
    fn next_free_returns_none_when_exhausted() {
        let used: HashSet<i64> = (65000..=65002).collect();
        let shelved = HashSet::new();
        assert_eq!(next_free_integer(65000, 65002, &used, &shelved), None);
    }

    #[test]
    fn next_free_returns_none_when_all_shelved() {
        let used = HashSet::new();
        let shelved: HashSet<i64> = (65000..=65002).collect();
        assert_eq!(next_free_integer(65000, 65002, &used, &shelved), None);
    }
}
