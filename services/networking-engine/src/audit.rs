//! Networking audit log — tamper-evident append-only row stream per tenant.
//!
//! This is the storage contract Phase 9 formalises ("audit_entry, append-only,
//! hash-chained"). Phase 7-apply and Phase 8 governance both land on top — so
//! the cheap way to unblock both is to ship the append path now and extend
//! the reader/exporter surface later.
//!
//! ## Hash chain
//!
//! Each row carries `prev_hash` (the previous entry's `entry_hash` in this
//! tenant's sequence, or NULL for the first entry) and `entry_hash`
//! (SHA-256 over a canonical UTF-8 byte stream of the row's semantic
//! content, with `prev_hash` mixed in). Modifying any past row invalidates
//! every subsequent `entry_hash`.
//!
//! ## Atomicity
//!
//! `append_tx` takes an open `sqlx::Transaction` so the audit write lands
//! atomically with the business write that spawned it. Caller picks up an
//! advisory lock on the tenant id first (see [`acquire_tenant_lock`]) so
//! concurrent appends for one tenant serialise — two transactions chaining
//! off the same `prev_hash` is exactly the race that breaks the chain.
//!
//! ## What gets hashed
//!
//! The canonical content string is a pipe-separated, ordered sequence of
//! fields so rearranging struct layout in future code changes can't shift
//! the hash silently. Format is documented in [`canonical_payload`].

use chrono::{DateTime, Utc};
use serde::{Deserialize, Serialize};
use sha2::{Digest, Sha256};
use sqlx::{PgPool, Postgres, Transaction};
use uuid::Uuid;

use crate::error::EngineError;
use crate::hash::stable_hash;

/// Call-site input to the audit append. Everything that's not on the row
/// itself is derived (sequence_id, prev_hash, entry_hash, created_at).
#[derive(Debug, Clone)]
pub struct AuditEvent<'a> {
    pub organization_id: Uuid,
    pub source_service: &'a str,
    pub entity_type: &'a str,
    pub entity_id: Option<Uuid>,
    pub action: &'a str,
    pub actor_user_id: Option<i32>,
    pub actor_display: Option<&'a str>,
    pub client_ip: Option<&'a str>,
    pub correlation_id: Option<Uuid>,
    pub details: serde_json::Value,
}

/// Persisted audit row as returned by reads.
#[derive(Debug, Clone, Serialize, sqlx::FromRow)]
#[serde(rename_all = "camelCase")]
pub struct AuditRow {
    pub id: Uuid,
    pub organization_id: Uuid,
    pub sequence_id: i64,
    pub source_service: String,
    pub entity_type: String,
    pub entity_id: Option<Uuid>,
    pub action: String,
    pub actor_user_id: Option<i32>,
    pub actor_display: Option<String>,
    pub correlation_id: Option<Uuid>,
    pub details: serde_json::Value,
    pub prev_hash: Option<String>,
    pub entry_hash: String,
    pub created_at: DateTime<Utc>,
}

/// Serialize the event into the exact byte stream that feeds the hasher.
/// Field order + field separator are the contract — never change without
/// understanding that it breaks every previously-written hash.
///
/// The details JSON goes through `to_string` so ordering-sensitive diffs
/// surface in the hash; callers wanting stable diffing should pass
/// canonically-ordered JSON (caller's responsibility — we don't rewrite it
/// because rewriting would make storage and hash disagree).
pub(crate) fn canonical_payload(
    prev_hash: Option<&str>,
    organization_id: Uuid,
    sequence_id: i64,
    source_service: &str,
    entity_type: &str,
    entity_id: Option<Uuid>,
    action: &str,
    actor_user_id: Option<i32>,
    details: &serde_json::Value,
) -> String {
    let mut s = String::with_capacity(256);
    // Missing prev_hash (the first entry in the chain) uses an explicit
    // sentinel so an empty string and "no previous" are distinguishable.
    s.push_str(prev_hash.unwrap_or("GENESIS"));
    s.push('|');
    s.push_str(&organization_id.to_string());
    s.push('|');
    s.push_str(&sequence_id.to_string());
    s.push('|');
    s.push_str(source_service);
    s.push('|');
    s.push_str(entity_type);
    s.push('|');
    s.push_str(&entity_id.map(|id| id.to_string()).unwrap_or_default());
    s.push('|');
    s.push_str(action);
    s.push('|');
    s.push_str(&actor_user_id.map(|v| v.to_string()).unwrap_or_default());
    s.push('|');
    s.push_str(&details.to_string());
    s
}

/// SHA-256 hex of the canonical payload.
pub(crate) fn compute_hash(payload: &str) -> String {
    let mut hasher = Sha256::new();
    hasher.update(payload.as_bytes());
    hex::encode(hasher.finalize())
}

/// Take a tenant-scoped advisory lock so two concurrent appends don't both
/// read the same "previous" row and produce a broken chain (both new rows
/// pointing at the same prev_hash, each unaware of the other).
///
/// Re-uses the FNV-1a `stable_hash` from `crate::hash` with a salt so the
/// key doesn't collide with the allocation-layer's per-container locks
/// (which also derive keys from UUIDs). Salt = tenant id XOR a fixed
/// marker turns the whole space disjoint in practice.
pub async fn acquire_tenant_lock(
    tx: &mut Transaction<'_, Postgres>,
    organization_id: Uuid,
) -> Result<(), EngineError> {
    // Two-arg pg_advisory_xact_lock gives us a separate lock space from
    // the single-arg one the allocation service uses. So we pass a
    // namespace (1) + the FNV-1a hash of the tenant id as the second
    // lock key — no collision with allocation.
    let key = stable_hash(organization_id);
    sqlx::query("SELECT pg_advisory_xact_lock($1::int, $2)")
        .bind(1_i32)
        .bind(key)
        .execute(&mut **tx)
        .await?;
    Ok(())
}

/// Append an audit row inside the caller's transaction. Serialises with
/// `acquire_tenant_lock` so the hash chain stays contiguous.
pub async fn append_tx<'e>(
    tx: &mut Transaction<'_, Postgres>,
    evt: &AuditEvent<'e>,
) -> Result<AuditRow, EngineError> {
    acquire_tenant_lock(tx, evt.organization_id).await?;

    // Pull the most recent row in this tenant's chain. Use sequence_id
    // DESC since the ix_audit_entry_tenant_seq index is sorted that way.
    let prev: Option<(i64, String)> = sqlx::query_as(
        "SELECT sequence_id, entry_hash
           FROM net.audit_entry
          WHERE organization_id = $1
          ORDER BY sequence_id DESC
          LIMIT 1")
        .bind(evt.organization_id)
        .fetch_optional(&mut **tx)
        .await?;

    let (sequence_id, prev_hash) = match prev {
        Some((seq, hash)) => (seq + 1, Some(hash)),
        None => (1, None),
    };

    let payload = canonical_payload(
        prev_hash.as_deref(),
        evt.organization_id,
        sequence_id,
        evt.source_service,
        evt.entity_type,
        evt.entity_id,
        evt.action,
        evt.actor_user_id,
        &evt.details,
    );
    let entry_hash = compute_hash(&payload);

    let row: AuditRow = sqlx::query_as(
        "INSERT INTO net.audit_entry
            (organization_id, sequence_id, source_service, entity_type, entity_id,
             action, actor_user_id, actor_display, client_ip, correlation_id,
             details, prev_hash, entry_hash)
         VALUES ($1, $2, $3, $4, $5,
                 $6, $7, $8, $9::inet, $10,
                 $11, $12, $13)
         RETURNING id, organization_id, sequence_id, source_service, entity_type,
                   entity_id, action, actor_user_id, actor_display,
                   correlation_id, details, prev_hash, entry_hash, created_at")
        .bind(evt.organization_id)
        .bind(sequence_id)
        .bind(evt.source_service)
        .bind(evt.entity_type)
        .bind(evt.entity_id)
        .bind(evt.action)
        .bind(evt.actor_user_id)
        .bind(evt.actor_display)
        .bind(evt.client_ip)
        .bind(evt.correlation_id)
        .bind(&evt.details)
        .bind(prev_hash.as_deref())
        .bind(&entry_hash)
        .fetch_one(&mut **tx)
        .await?;

    Ok(row)
}

// ─── Query side ──────────────────────────────────────────────────────────

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct ListAuditQuery {
    pub organization_id: Uuid,
    pub entity_type: Option<String>,
    pub entity_id: Option<Uuid>,
    pub action: Option<String>,
    pub actor_user_id: Option<i32>,
    #[serde(default = "default_limit")]
    pub limit: i64,
    /// Return rows with sequence_id strictly less than this (descending
    /// pagination cursor). None → start at the tip.
    pub before_sequence_id: Option<i64>,
}

fn default_limit() -> i64 { 100 }

pub async fn list(pool: &PgPool, q: &ListAuditQuery) -> Result<Vec<AuditRow>, EngineError> {
    let limit = q.limit.clamp(1, 1000);
    let rows: Vec<AuditRow> = sqlx::query_as(
        "SELECT id, organization_id, sequence_id, source_service, entity_type,
                entity_id, action, actor_user_id, actor_display,
                correlation_id, details, prev_hash, entry_hash, created_at
           FROM net.audit_entry
          WHERE organization_id = $1
            AND ($2::text IS NULL OR entity_type  = $2)
            AND ($3::uuid IS NULL OR entity_id    = $3)
            AND ($4::text IS NULL OR action       = $4)
            AND ($5::int  IS NULL OR actor_user_id = $5)
            AND ($6::bigint IS NULL OR sequence_id < $6)
          ORDER BY sequence_id DESC
          LIMIT $7")
        .bind(q.organization_id)
        .bind(q.entity_type.as_deref())
        .bind(q.entity_id)
        .bind(q.action.as_deref())
        .bind(q.actor_user_id)
        .bind(q.before_sequence_id)
        .bind(limit)
        .fetch_all(pool)
        .await?;
    Ok(rows)
}

// ─── Unit tests ──────────────────────────────────────────────────────────

#[cfg(test)]
mod tests {
    use super::*;

    fn sample_payload(seq: i64, prev: Option<&str>) -> String {
        canonical_payload(
            prev,
            Uuid::parse_str("11111111-1111-1111-1111-111111111111").unwrap(),
            seq,
            "networking-engine",
            "Device",
            Some(Uuid::parse_str("22222222-2222-2222-2222-222222222222").unwrap()),
            "Renamed",
            Some(42),
            &serde_json::json!({"from":"MEP-91-CORE2","to":"MEP-91-CORE02"}),
        )
    }

    #[test]
    fn hash_is_deterministic() {
        let a = compute_hash(&sample_payload(1, None));
        let b = compute_hash(&sample_payload(1, None));
        assert_eq!(a, b);
    }

    #[test]
    fn hash_length_is_64_hex_chars() {
        let h = compute_hash(&sample_payload(1, None));
        assert_eq!(h.len(), 64);
        assert!(h.chars().all(|c| c.is_ascii_hexdigit()));
        assert!(h.chars().all(|c| !c.is_ascii_uppercase()),
            "we standardise on lowercase hex");
    }

    #[test]
    fn genesis_entry_distinguishable_from_empty_prev() {
        // If we hashed empty string and "GENESIS" the same, a tampering
        // attacker could pretend a later entry was the first. The sentinel
        // must appear in the payload.
        let first = sample_payload(1, None);
        let faked_empty_prev = sample_payload(1, Some(""));
        assert_ne!(compute_hash(&first), compute_hash(&faked_empty_prev));
    }

    #[test]
    fn changing_any_field_changes_hash() {
        let base = compute_hash(&sample_payload(1, None));
        // Different sequence.
        let diff_seq = compute_hash(&sample_payload(2, None));
        assert_ne!(base, diff_seq);
        // Different prev_hash.
        let diff_prev = compute_hash(&sample_payload(1, Some("abc")));
        assert_ne!(base, diff_prev);
    }

    #[test]
    fn chain_propagates_tampering() {
        // A 3-entry chain: entry N's hash depends on entry N-1's hash. If
        // we swap entry 1's content, entries 2 and 3 must both change.
        let h1_original = compute_hash(&sample_payload(1, None));
        let h2_original = compute_hash(&sample_payload(2, Some(&h1_original)));
        let h3_original = compute_hash(&sample_payload(3, Some(&h2_original)));

        // Tamper: recompute entry 1 with different content. Because we're
        // using the same canonical_payload helper, the easiest way to
        // produce different content is a different sequence.
        let h1_tampered = compute_hash(&sample_payload(99, None));
        let h2_tampered = compute_hash(&sample_payload(2, Some(&h1_tampered)));
        let h3_tampered = compute_hash(&sample_payload(3, Some(&h2_tampered)));

        assert_ne!(h1_original, h1_tampered);
        assert_ne!(h2_original, h2_tampered);
        assert_ne!(h3_original, h3_tampered);
    }

    #[test]
    fn canonical_payload_format_is_pipe_separated() {
        let p = sample_payload(1, None);
        // 9 fields → 8 pipes.
        assert_eq!(p.matches('|').count(), 8);
        // Genesis marker appears as the first field.
        assert!(p.starts_with("GENESIS|"));
    }

    #[test]
    fn details_json_affects_hash() {
        let base = sample_payload(1, None);
        let alt = canonical_payload(
            None,
            Uuid::parse_str("11111111-1111-1111-1111-111111111111").unwrap(),
            1, "networking-engine", "Device",
            Some(Uuid::parse_str("22222222-2222-2222-2222-222222222222").unwrap()),
            "Renamed", Some(42),
            &serde_json::json!({"from":"X","to":"Y"}),   // different details
        );
        assert_ne!(compute_hash(&base), compute_hash(&alt));
    }

    #[test]
    fn null_entity_id_is_handled() {
        // Audit rows without an entity_id (e.g. tenant-config updates)
        // should still hash cleanly, and the empty slot must be
        // distinguishable from a real UUID that renders to an empty
        // string (impossible, but belt+braces).
        let p = canonical_payload(
            None, Uuid::nil(), 1,
            "networking-engine", "TenantConfig", None,
            "Updated", None, &serde_json::json!({}));
        let h = compute_hash(&p);
        assert_eq!(h.len(), 64);
        // The entity_id slot is empty, which means two consecutive pipes.
        assert!(p.contains("TenantConfig||Updated"));
    }
}
