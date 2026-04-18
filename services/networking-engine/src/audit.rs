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
    /// Filter to entries for one Change Set — join with
    /// change_set.correlation_id for "show me every lifecycle event of
    /// this Set".
    pub correlation_id: Option<Uuid>,
    /// Lower bound on created_at (inclusive). "Everything since I was
    /// last on call" is the intended query shape.
    pub from_at: Option<DateTime<Utc>>,
    /// Upper bound on created_at (inclusive). Combined with from_at
    /// gives a tight date-window for forensic review.
    pub to_at: Option<DateTime<Utc>>,
    #[serde(default = "default_limit")]
    pub limit: i64,
    /// Return rows with sequence_id strictly less than this (descending
    /// pagination cursor). None → start at the tip.
    pub before_sequence_id: Option<i64>,
}

fn default_limit() -> i64 { 100 }

// ─── Chain verification ──────────────────────────────────────────────────

/// Per-tenant chain verification report. `ok = true` means every row's
/// `entry_hash` recomputes exactly from the stored content + `prev_hash`
/// *and* the `prev_hash` linkage is intact (row N's `prev_hash` matches
/// row N-1's `entry_hash`).
#[derive(Debug, Clone, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct VerifyReport {
    pub organization_id: Uuid,
    pub rows_checked: i64,
    pub first_sequence_id: Option<i64>,
    pub last_sequence_id: Option<i64>,
    pub ok: bool,
    pub mismatches: Vec<VerifyMismatch>,
}

#[derive(Debug, Clone, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct VerifyMismatch {
    pub sequence_id: i64,
    pub id: Uuid,
    pub reason: String,
    pub expected_hash: Option<String>,
    pub stored_hash: String,
}

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct VerifyChainQuery {
    pub organization_id: Uuid,
    /// Cap how many rows to check — default is "everything", but very
    /// large tenants can slice by passing a cap to keep the check under a
    /// request budget and walk again with `from_sequence_id` on follow-up.
    #[serde(default)]
    pub limit: Option<i64>,
    #[serde(default)]
    pub from_sequence_id: Option<i64>,
}

pub async fn verify_chain(
    pool: &PgPool,
    q: &VerifyChainQuery,
) -> Result<VerifyReport, EngineError> {
    let limit = q.limit.map(|l| l.clamp(1, 100_000));
    let from = q.from_sequence_id.unwrap_or(0);

    // Ascending order is essential — we recompute each entry against the
    // *previous* row's stored hash, and that "previous" must already have
    // passed the inspection loop (we don't trust what the DB hands us for
    // the current row's prev_hash field until we've verified it matches
    // the prior row).
    let rows: Vec<AuditRow> = match limit {
        Some(l) => sqlx::query_as(
            "SELECT id, organization_id, sequence_id, source_service, entity_type,
                    entity_id, action, actor_user_id, actor_display,
                    correlation_id, details, prev_hash, entry_hash, created_at
               FROM net.audit_entry
              WHERE organization_id = $1 AND sequence_id >= $2
              ORDER BY sequence_id ASC
              LIMIT $3")
            .bind(q.organization_id).bind(from).bind(l)
            .fetch_all(pool).await?,
        None => sqlx::query_as(
            "SELECT id, organization_id, sequence_id, source_service, entity_type,
                    entity_id, action, actor_user_id, actor_display,
                    correlation_id, details, prev_hash, entry_hash, created_at
               FROM net.audit_entry
              WHERE organization_id = $1 AND sequence_id >= $2
              ORDER BY sequence_id ASC")
            .bind(q.organization_id).bind(from)
            .fetch_all(pool).await?,
    };

    let mut mismatches = Vec::new();
    let mut expected_prev: Option<String> = None;
    let first_sequence_id = rows.first().map(|r| r.sequence_id);
    let last_sequence_id = rows.last().map(|r| r.sequence_id);
    let rows_checked = rows.len() as i64;

    // When walking a slice that doesn't start at sequence 1, we can't
    // recompute the prev-link for the first row without first fetching
    // the row at (start - 1). Rather than chase the pointer, record the
    // slice's first entry's stored prev_hash as our baseline — the
    // mismatch detector kicks in from the second row onward.
    for (i, row) in rows.iter().enumerate() {
        // 1. Prev-link check: every row after the first in the slice
        //    must point at the previous row's stored hash.
        if i > 0 {
            let prior = &rows[i - 1];
            if row.prev_hash.as_deref() != Some(&prior.entry_hash) {
                mismatches.push(VerifyMismatch {
                    sequence_id: row.sequence_id,
                    id: row.id,
                    reason: "prev_hash does not match prior row's entry_hash".into(),
                    expected_hash: Some(prior.entry_hash.clone()),
                    stored_hash: row.prev_hash.clone().unwrap_or_default(),
                });
            }
        }

        // 2. Content check: recompute entry_hash from the stored content
        //    and compare.
        let payload = canonical_payload(
            row.prev_hash.as_deref(),
            row.organization_id,
            row.sequence_id,
            &row.source_service,
            &row.entity_type,
            row.entity_id,
            &row.action,
            row.actor_user_id,
            &row.details,
        );
        let expected = compute_hash(&payload);
        if expected != row.entry_hash {
            mismatches.push(VerifyMismatch {
                sequence_id: row.sequence_id,
                id: row.id,
                reason: "entry_hash does not match recomputed SHA-256 of content".into(),
                expected_hash: Some(expected),
                stored_hash: row.entry_hash.clone(),
            });
        }
        expected_prev = Some(row.entry_hash.clone());
    }

    let _ = expected_prev; // kept for future incremental-walk extension

    Ok(VerifyReport {
        organization_id: q.organization_id,
        rows_checked,
        first_sequence_id,
        last_sequence_id,
        ok: mismatches.is_empty(),
        mismatches,
    })
}

pub async fn list(pool: &PgPool, q: &ListAuditQuery) -> Result<Vec<AuditRow>, EngineError> {
    let limit = q.limit.clamp(1, 1000);
    let rows: Vec<AuditRow> = sqlx::query_as(
        "SELECT id, organization_id, sequence_id, source_service, entity_type,
                entity_id, action, actor_user_id, actor_display,
                correlation_id, details, prev_hash, entry_hash, created_at
           FROM net.audit_entry
          WHERE organization_id = $1
            AND ($2::text IS NULL OR entity_type    = $2)
            AND ($3::uuid IS NULL OR entity_id      = $3)
            AND ($4::text IS NULL OR action         = $4)
            AND ($5::int  IS NULL OR actor_user_id  = $5)
            AND ($6::uuid IS NULL OR correlation_id = $6)
            AND ($7::timestamptz IS NULL OR created_at >= $7)
            AND ($8::timestamptz IS NULL OR created_at <= $8)
            AND ($9::bigint IS NULL OR sequence_id < $9)
          ORDER BY sequence_id DESC
          LIMIT $10")
        .bind(q.organization_id)
        .bind(q.entity_type.as_deref())
        .bind(q.entity_id)
        .bind(q.action.as_deref())
        .bind(q.actor_user_id)
        .bind(q.correlation_id)
        .bind(q.from_at)
        .bind(q.to_at)
        .bind(q.before_sequence_id)
        .bind(limit)
        .fetch_all(pool)
        .await?;
    Ok(rows)
}

// ─── Export ──────────────────────────────────────────────────────────────

#[derive(Debug, Copy, Clone, Deserialize)]
#[serde(rename_all = "lowercase")]
pub enum ExportFormat { Csv, Ndjson }

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct ExportQuery {
    pub organization_id: Uuid,
    pub format: ExportFormat,
    pub entity_type: Option<String>,
    pub entity_id: Option<Uuid>,
    pub action: Option<String>,
    pub actor_user_id: Option<i32>,
    pub correlation_id: Option<Uuid>,
    pub from_at: Option<DateTime<Utc>>,
    pub to_at: Option<DateTime<Utc>>,
    /// Hard ceiling on rows returned — defaults to 50k. Higher-volume
    /// pulls should chunk via multiple from_at/to_at windows.
    #[serde(default = "default_export_limit")]
    pub limit: i64,
}

fn default_export_limit() -> i64 { 50_000 }

/// Export body as a single `String` (no streaming yet — 50k row default
/// is comfortably in-memory, and axum's streaming bodies need a Send +
/// 'static stream which complicates the sqlx lifetime dance). Can be
/// upgraded to a chunked stream later without changing the HTTP surface.
pub async fn export(pool: &PgPool, q: &ExportQuery) -> Result<(String, &'static str), EngineError> {
    let limit = q.limit.clamp(1, 1_000_000);

    let rows: Vec<AuditRow> = sqlx::query_as(
        "SELECT id, organization_id, sequence_id, source_service, entity_type,
                entity_id, action, actor_user_id, actor_display,
                correlation_id, details, prev_hash, entry_hash, created_at
           FROM net.audit_entry
          WHERE organization_id = $1
            AND ($2::text IS NULL OR entity_type    = $2)
            AND ($3::uuid IS NULL OR entity_id      = $3)
            AND ($4::text IS NULL OR action         = $4)
            AND ($5::int  IS NULL OR actor_user_id  = $5)
            AND ($6::uuid IS NULL OR correlation_id = $6)
            AND ($7::timestamptz IS NULL OR created_at >= $7)
            AND ($8::timestamptz IS NULL OR created_at <= $8)
          ORDER BY sequence_id ASC
          LIMIT $9")
        .bind(q.organization_id)
        .bind(q.entity_type.as_deref())
        .bind(q.entity_id)
        .bind(q.action.as_deref())
        .bind(q.actor_user_id)
        .bind(q.correlation_id)
        .bind(q.from_at)
        .bind(q.to_at)
        .bind(limit)
        .fetch_all(pool)
        .await?;

    match q.format {
        ExportFormat::Csv => Ok((render_csv(&rows), "text/csv; charset=utf-8")),
        ExportFormat::Ndjson => Ok((render_ndjson(&rows), "application/x-ndjson")),
    }
}

fn render_csv(rows: &[AuditRow]) -> String {
    let mut out = String::with_capacity(rows.len() * 160);
    out.push_str("sequence_id,created_at,source_service,entity_type,entity_id,action,\
                  actor_user_id,actor_display,correlation_id,prev_hash,entry_hash,details\n");
    for r in rows {
        out.push_str(&r.sequence_id.to_string()); out.push(',');
        out.push_str(&r.created_at.to_rfc3339()); out.push(',');
        push_csv_field(&mut out, &r.source_service); out.push(',');
        push_csv_field(&mut out, &r.entity_type); out.push(',');
        push_csv_field(&mut out, &r.entity_id.map(|id| id.to_string()).unwrap_or_default()); out.push(',');
        push_csv_field(&mut out, &r.action); out.push(',');
        out.push_str(&r.actor_user_id.map(|v| v.to_string()).unwrap_or_default()); out.push(',');
        push_csv_field(&mut out, r.actor_display.as_deref().unwrap_or("")); out.push(',');
        push_csv_field(&mut out, &r.correlation_id.map(|id| id.to_string()).unwrap_or_default()); out.push(',');
        push_csv_field(&mut out, r.prev_hash.as_deref().unwrap_or("")); out.push(',');
        push_csv_field(&mut out, &r.entry_hash); out.push(',');
        push_csv_field(&mut out, &r.details.to_string());
        out.push('\n');
    }
    out
}

/// RFC 4180 field quoting: wrap in double quotes if the value contains
/// commas, newlines, or quotes; double-up any embedded quotes.
fn push_csv_field(out: &mut String, s: &str) {
    let needs_quote = s.contains(',') || s.contains('"') || s.contains('\n') || s.contains('\r');
    if !needs_quote { out.push_str(s); return; }
    out.push('"');
    for ch in s.chars() {
        if ch == '"' { out.push('"'); }
        out.push(ch);
    }
    out.push('"');
}

fn render_ndjson(rows: &[AuditRow]) -> String {
    let mut out = String::with_capacity(rows.len() * 200);
    for r in rows {
        // Re-using AuditRow's Serialize impl gives the same camelCase
        // shape as the JSON API, which is the point — "the export is
        // the API output, one-per-line".
        if let Ok(line) = serde_json::to_string(r) {
            out.push_str(&line);
            out.push('\n');
        }
    }
    out
}

/// Entity-scoped chronological timeline. Powers "show me everything that
/// has ever happened to this device / link / server" forensics. Rows are
/// returned in ascending sequence_id order so the UI reads left-to-right.
///
/// `limit` is optional (None = everything). Per-entity histories are
/// typically small (tens of rows); if a pathological one grows into
/// thousands, fall back to `list()` with `entity_type` + `entity_id` +
/// `before_sequence_id` pagination.
pub async fn entity_timeline(
    pool: &PgPool,
    org_id: Uuid,
    entity_type: &str,
    entity_id: Uuid,
    limit: Option<i64>,
) -> Result<Vec<AuditRow>, EngineError> {
    let rows: Vec<AuditRow> = match limit {
        Some(l) => sqlx::query_as(
            "SELECT id, organization_id, sequence_id, source_service, entity_type,
                    entity_id, action, actor_user_id, actor_display,
                    correlation_id, details, prev_hash, entry_hash, created_at
               FROM net.audit_entry
              WHERE organization_id = $1 AND entity_type = $2 AND entity_id = $3
              ORDER BY sequence_id ASC
              LIMIT $4")
            .bind(org_id).bind(entity_type).bind(entity_id).bind(l.clamp(1, 10_000))
            .fetch_all(pool).await?,
        None => sqlx::query_as(
            "SELECT id, organization_id, sequence_id, source_service, entity_type,
                    entity_id, action, actor_user_id, actor_display,
                    correlation_id, details, prev_hash, entry_hash, created_at
               FROM net.audit_entry
              WHERE organization_id = $1 AND entity_type = $2 AND entity_id = $3
              ORDER BY sequence_id ASC")
            .bind(org_id).bind(entity_type).bind(entity_id)
            .fetch_all(pool).await?,
    };
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

    /// The chain-walker is broken out of `verify_chain` so the two checks
    /// (prev-link, content-rehash) can be exercised without a DB. Same
    /// rules as the async version — ascending order required.
    fn walk_and_verify(rows: &[AuditRow]) -> Vec<VerifyMismatch> {
        let mut mismatches = Vec::new();
        for (i, row) in rows.iter().enumerate() {
            if i > 0 {
                let prior = &rows[i - 1];
                if row.prev_hash.as_deref() != Some(&prior.entry_hash) {
                    mismatches.push(VerifyMismatch {
                        sequence_id: row.sequence_id,
                        id: row.id,
                        reason: "prev_hash does not match prior row's entry_hash".into(),
                        expected_hash: Some(prior.entry_hash.clone()),
                        stored_hash: row.prev_hash.clone().unwrap_or_default(),
                    });
                }
            }
            let payload = canonical_payload(
                row.prev_hash.as_deref(),
                row.organization_id,
                row.sequence_id,
                &row.source_service,
                &row.entity_type,
                row.entity_id,
                &row.action,
                row.actor_user_id,
                &row.details,
            );
            let expected = compute_hash(&payload);
            if expected != row.entry_hash {
                mismatches.push(VerifyMismatch {
                    sequence_id: row.sequence_id,
                    id: row.id,
                    reason: "entry_hash does not match recomputed SHA-256 of content".into(),
                    expected_hash: Some(expected),
                    stored_hash: row.entry_hash.clone(),
                });
            }
        }
        mismatches
    }

    fn build_row(seq: i64, prev: Option<&str>, details_key: &str) -> AuditRow {
        let details = serde_json::json!({ "k": details_key });
        let org_id = Uuid::parse_str("11111111-1111-1111-1111-111111111111").unwrap();
        let entity_id = Some(Uuid::parse_str("22222222-2222-2222-2222-222222222222").unwrap());
        let payload = canonical_payload(
            prev, org_id, seq, "networking-engine", "Device",
            entity_id, "Renamed", Some(42), &details);
        let hash = compute_hash(&payload);
        AuditRow {
            id: Uuid::parse_str(&format!("aaaaaaaa-aaaa-aaaa-aaaa-{:012x}", seq)).unwrap(),
            organization_id: org_id,
            sequence_id: seq,
            source_service: "networking-engine".into(),
            entity_type: "Device".into(),
            entity_id,
            action: "Renamed".into(),
            actor_user_id: Some(42),
            actor_display: None,
            correlation_id: None,
            details,
            prev_hash: prev.map(str::to_string),
            entry_hash: hash,
            created_at: chrono::Utc::now(),
        }
    }

    #[test]
    fn clean_chain_verifies() {
        let r1 = build_row(1, None, "a");
        let r2 = build_row(2, Some(&r1.entry_hash), "b");
        let r3 = build_row(3, Some(&r2.entry_hash), "c");
        assert!(walk_and_verify(&[r1, r2, r3]).is_empty());
    }

    #[test]
    fn tampered_content_detected() {
        let r1 = build_row(1, None, "a");
        let mut r2 = build_row(2, Some(&r1.entry_hash), "b");
        // Tamper: change details without regenerating the hash.
        r2.details = serde_json::json!({ "k": "TAMPERED" });
        let r3 = build_row(3, Some(&r2.entry_hash), "c");

        let found = walk_and_verify(&[r1, r2, r3]);
        // One mismatch: r2's stored entry_hash no longer matches its
        // content. r3's prev-link and content still match because we
        // derive them from r2's (untouched) stored entry_hash.
        assert_eq!(found.len(), 1);
        assert_eq!(found[0].sequence_id, 2);
        assert!(found[0].reason.contains("entry_hash"));
    }

    #[test]
    fn broken_prev_link_detected() {
        let r1 = build_row(1, None, "a");
        let r2 = build_row(2, Some(&r1.entry_hash), "b");
        // Tamper: make r2's prev_hash point at garbage, then re-hash r2
        // so the content check alone wouldn't see it. This isolates the
        // prev-link detector.
        let mut r2_fake = r2.clone();
        r2_fake.prev_hash = Some("0000000000000000000000000000000000000000000000000000000000000000".into());
        let payload = canonical_payload(
            r2_fake.prev_hash.as_deref(), r2_fake.organization_id,
            r2_fake.sequence_id, &r2_fake.source_service, &r2_fake.entity_type,
            r2_fake.entity_id, &r2_fake.action, r2_fake.actor_user_id, &r2_fake.details);
        r2_fake.entry_hash = compute_hash(&payload);

        let found = walk_and_verify(&[r1, r2_fake]);
        assert_eq!(found.len(), 1);
        assert_eq!(found[0].sequence_id, 2);
        assert!(found[0].reason.contains("prev_hash"));
    }

    #[test]
    fn empty_chain_verifies() {
        assert!(walk_and_verify(&[]).is_empty());
    }

    #[test]
    fn single_row_verifies() {
        let r1 = build_row(1, None, "alone");
        assert!(walk_and_verify(&[r1]).is_empty());
    }

    #[test]
    fn csv_plain_field_not_quoted() {
        let mut s = String::new();
        push_csv_field(&mut s, "hello");
        assert_eq!(s, "hello");
    }

    #[test]
    fn csv_field_with_comma_is_quoted() {
        let mut s = String::new();
        push_csv_field(&mut s, "a,b");
        assert_eq!(s, "\"a,b\"");
    }

    #[test]
    fn csv_field_with_quote_is_escaped() {
        let mut s = String::new();
        push_csv_field(&mut s, r#"he said "hi""#);
        assert_eq!(s, r#""he said ""hi""""#);
    }

    #[test]
    fn csv_field_with_newline_is_quoted() {
        let mut s = String::new();
        push_csv_field(&mut s, "line1\nline2");
        assert!(s.starts_with('"') && s.ends_with('"'));
        assert!(s.contains('\n'));
    }

    #[test]
    fn csv_field_with_cr_is_quoted() {
        let mut s = String::new();
        push_csv_field(&mut s, "line1\rline2");
        assert!(s.starts_with('"') && s.ends_with('"'));
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
