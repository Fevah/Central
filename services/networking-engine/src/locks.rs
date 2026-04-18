//! Lock-state helpers. Phase 8f.
//!
//! The DB trigger in migration 100 is the authoritative enforcement — this
//! module adds an ergonomic Rust surface for *changing* lock state and a
//! shared validator so API handlers don't re-invent the wheel.
//!
//! Scope today: ASN allocations. The function that writes the lock change
//! is written generically against a table name + id + org_id so extending
//! to vlan / mlag_domain / subnet / ip_address (and the universal-base
//! tables beyond) is one line of schema routing per entity type.

use chrono::{DateTime, Utc};
use serde::{Deserialize, Serialize};
use sqlx::PgPool;
use uuid::Uuid;

use crate::audit::{self, AuditEvent};
use crate::error::EngineError;

#[derive(Debug, Copy, Clone, PartialEq, Eq, Serialize, Deserialize)]
#[serde(rename_all = "PascalCase")]
pub enum LockState { Open, SoftLock, HardLock, Immutable }

impl LockState {
    pub fn as_str(&self) -> &'static str {
        match self {
            Self::Open => "Open", Self::SoftLock => "SoftLock",
            Self::HardLock => "HardLock", Self::Immutable => "Immutable",
        }
    }

    pub fn from_db(s: &str) -> Result<Self, EngineError> {
        match s {
            "Open" => Ok(Self::Open),
            "SoftLock" => Ok(Self::SoftLock),
            "HardLock" => Ok(Self::HardLock),
            "Immutable" => Ok(Self::Immutable),
            other => Err(EngineError::bad_request(format!(
                "Unknown lock_state '{other}'"))),
        }
    }
}

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct SetLockBody {
    pub lock_state: LockState,
    pub lock_reason: Option<String>,
}

/// Flat projection of a locked row for the WPF Locks grid. Same shape
/// for every numbering table so the frontend renders them uniformly —
/// the `table` column disambiguates which net.* table the id refers to,
/// and the `display_label` column gives a human-readable identifier
/// (hostname / cidr / vlan-id / etc) so admins don't have to chase
/// the UUID.
#[derive(Debug, Clone, Serialize, sqlx::FromRow)]
#[serde(rename_all = "camelCase")]
pub struct LockedRow {
    pub id: Uuid,
    pub table_name: String,     // asn_allocation / vlan / mlag_domain / subnet / ip_address
    pub display_label: String,  // best available human name for the row
    pub lock_state: String,
    pub lock_reason: Option<String>,
    pub locked_by: Option<i32>,
    pub locked_at: Option<DateTime<Utc>>,
    pub version: i32,
}

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct ListLockedQuery {
    pub organization_id: Uuid,
    /// Optional — restrict to one table. None = all 5 tables.
    pub table: Option<String>,
    /// Optional — filter by state. None = HardLock + Immutable +
    /// SoftLock (everything except Open, which is every other row).
    pub lock_state: Option<String>,
}

#[derive(Debug, Clone, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct LockChangeResult {
    pub id: Uuid,
    pub lock_state: LockState,
    pub lock_reason: Option<String>,
    pub locked_by: Option<i32>,
    pub locked_at: Option<DateTime<Utc>>,
    pub version: i32,
}

/// Validate that a requested transition is actually permitted. The trigger
/// won't stop *every* misuse — it stops business-column mutations on
/// HardLock but doesn't care about what state you're transitioning TO. So:
///
/// - Immutable is terminal. You can never transition away from it.
/// - SoftLock treated like Open for transition purposes (no blocker).
///
/// This validator surfaces those rules at the API layer so admins get a
/// clean 400 before the UPDATE is attempted.
pub fn validate_transition(current: LockState, next: LockState) -> Result<(), EngineError> {
    if current == LockState::Immutable && next != LockState::Immutable {
        return Err(EngineError::bad_request(
            "Immutable rows are terminal — the lock cannot be loosened."));
    }
    Ok(())
}

/// List every non-Open row across the five numbering tables that lock
/// enforcement attaches to. Filters: optional table (one of the five)
/// and optional lock_state (else: HardLock + Immutable + SoftLock, i.e.
/// everything that isn't the default Open).
///
/// Projection is uniform — one row shape regardless of source table —
/// so the WPF grid binds a single set of columns. The `table` column
/// tells the caller which net.* row the id refers to when they want
/// to change state.
pub async fn list_locked(
    pool: &PgPool,
    q: &ListLockedQuery,
) -> Result<Vec<LockedRow>, EngineError> {
    // Whitelist — same set as set_lock. A bogus `table` query parameter
    // falls through to "no results" rather than an error since this is
    // a read endpoint; set_lock is the mutating one and that enforces
    // the whitelist hard.
    let tables = match q.table.as_deref() {
        Some(t) if matches!(t,
            "asn_allocation" | "vlan" | "mlag_domain" | "subnet" | "ip_address") => vec![t.to_string()],
        Some(_) => return Ok(Vec::new()),
        None => vec![
            "asn_allocation".into(), "vlan".into(), "mlag_domain".into(),
            "subnet".into(), "ip_address".into(),
        ],
    };

    // Runtime-built UNION ALL across the selected tables. Each SELECT
    // picks a display label appropriate for that entity kind:
    //   asn_allocation  asn::text
    //   vlan            'VLAN ' || vlan_id::text
    //   mlag_domain     'domain ' || domain_id::text
    //   subnet          subnet_code || ' (' || network::text || ')'
    //   ip_address      address::text
    let mut unions: Vec<String> = Vec::with_capacity(tables.len());
    for t in &tables {
        let label_expr = match t.as_str() {
            "asn_allocation" => "asn::text",
            "vlan"           => "'VLAN ' || vlan_id::text",
            "mlag_domain"    => "'domain ' || domain_id::text",
            "subnet"         => "subnet_code || ' (' || network::text || ')'",
            "ip_address"     => "address::text",
            _ => unreachable!(),
        };
        unions.push(format!(
            "SELECT id, '{t}' AS table_name, ({label_expr}) AS display_label,
                    lock_state::text AS lock_state, lock_reason, locked_by, locked_at, version
               FROM net.{t}
              WHERE organization_id = $1
                AND deleted_at IS NULL
                AND lock_state::text <> 'Open'
                AND ($2::text IS NULL OR lock_state::text = $2)"));
    }
    let sql = unions.join("\n            UNION ALL\n") + "\n         ORDER BY lock_state, table_name, display_label";

    let rows: Vec<LockedRow> = sqlx::query_as(&sql)
        .bind(q.organization_id)
        .bind(q.lock_state.as_deref())
        .fetch_all(pool)
        .await?;
    Ok(rows)
}

/// Update lock_state + lock_reason + locked_by/at on a net.* table row.
/// Transactional with an audit entry so "who locked this and why" stays
/// queryable. The DB trigger is still the last-line defence — if the row
/// is already Immutable, the UPDATE fails there regardless of what the
/// caller passed.
pub async fn set_lock(
    pool: &PgPool,
    table_name: &str, // unqualified net.* table name
    id: Uuid,
    org_id: Uuid,
    body: &SetLockBody,
    user_id: Option<i32>,
) -> Result<LockChangeResult, EngineError> {
    // Whitelist of tables we're willing to touch. Prevents stray routes from
    // pointing this helper at schema we haven't audited.
    if !matches!(table_name,
        "asn_allocation" | "vlan" | "mlag_domain" | "subnet" | "ip_address")
    {
        return Err(EngineError::bad_request(format!(
            "Lock operations on net.{table_name} not supported by this endpoint.")));
    }

    let mut tx = pool.begin().await?;

    // Read current state for the transition validator + audit before-snapshot.
    let current: Option<(String, Option<String>, i32)> = sqlx::query_as(
        &format!("SELECT lock_state::text, lock_reason, version
                    FROM net.{table_name}
                   WHERE id = $1 AND organization_id = $2 AND deleted_at IS NULL"))
        .bind(id)
        .bind(org_id)
        .fetch_optional(&mut *tx)
        .await?;
    let (current_state, current_reason, _current_version) = current
        .ok_or_else(|| EngineError::container_not_found(table_name, id))?;
    let current_state = LockState::from_db(&current_state)?;
    validate_transition(current_state, body.lock_state)?;

    // Write the change. Schema is uniform across the whitelisted tables
    // so the format!-ed SQL is safe: table_name comes from the whitelist,
    // never from user input. lock_* columns are the only ones touched —
    // the trigger allows this transition even when the row was in HardLock.
    let row: (Uuid, String, Option<String>, Option<i32>, Option<DateTime<Utc>>, i32) = sqlx::query_as(
        &format!(
            "UPDATE net.{table_name}
                SET lock_state = $3::net.lock_state,
                    lock_reason = $4,
                    locked_by = $5,
                    locked_at = CASE
                        WHEN $3::net.lock_state = 'Open'::net.lock_state THEN NULL
                        ELSE now()
                    END,
                    updated_at = now(),
                    updated_by = $5,
                    version = version + 1
              WHERE id = $1 AND organization_id = $2 AND deleted_at IS NULL
              RETURNING id, lock_state::text, lock_reason, locked_by, locked_at, version"))
        .bind(id)
        .bind(org_id)
        .bind(body.lock_state.as_str())
        .bind(body.lock_reason.as_deref())
        .bind(user_id)
        .fetch_one(&mut *tx)
        .await?;

    audit::append_tx(&mut tx, &AuditEvent {
        organization_id: org_id,
        source_service: "networking-engine",
        entity_type: &pascal_table_name(table_name),
        entity_id: Some(id),
        action: "LockChanged",
        actor_user_id: user_id,
        actor_display: None,
        client_ip: None,
        correlation_id: None,
        details: serde_json::json!({
            "table": format!("net.{table_name}"),
            "from_state": current_state.as_str(),
            "to_state": body.lock_state.as_str(),
            "from_reason": current_reason,
            "to_reason": body.lock_reason,
        }),
    }).await?;

    tx.commit().await?;

    Ok(LockChangeResult {
        id: row.0,
        lock_state: LockState::from_db(&row.1)?,
        lock_reason: row.2,
        locked_by: row.3,
        locked_at: row.4,
        version: row.5,
    })
}

/// Map the snake_case table name to the PascalCase entity name the audit
/// log uses. Single-argument transform so the audit entity_type stays
/// consistent ("AsnAllocation", not "asn_allocation"); keeps audit filter
/// UX the same shape as the Device / Link / Server entries we already stamp.
fn pascal_table_name(t: &str) -> String {
    let mut out = String::with_capacity(t.len());
    let mut upper_next = true;
    for ch in t.chars() {
        if ch == '_' { upper_next = true; continue; }
        if upper_next { out.extend(ch.to_uppercase()); upper_next = false; }
        else { out.push(ch); }
    }
    out
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn immutable_blocks_downgrade() {
        assert!(validate_transition(LockState::Immutable, LockState::Open).is_err());
        assert!(validate_transition(LockState::Immutable, LockState::HardLock).is_err());
        assert!(validate_transition(LockState::Immutable, LockState::SoftLock).is_err());
        // Immutable -> Immutable is a legal no-op (reassert).
        assert!(validate_transition(LockState::Immutable, LockState::Immutable).is_ok());
    }

    #[test]
    fn open_to_anything_allowed() {
        for next in [LockState::Open, LockState::SoftLock,
                     LockState::HardLock, LockState::Immutable] {
            assert!(validate_transition(LockState::Open, next).is_ok());
        }
    }

    #[test]
    fn hardlock_to_anything_allowed() {
        // The DB trigger enforces what HardLock rows can CHANGE — it doesn't
        // care what lock_state they transition TO. Our validator trusts that.
        for next in [LockState::Open, LockState::SoftLock,
                     LockState::HardLock, LockState::Immutable] {
            assert!(validate_transition(LockState::HardLock, next).is_ok());
        }
    }

    #[test]
    fn softlock_to_anything_allowed() {
        for next in [LockState::Open, LockState::SoftLock,
                     LockState::HardLock, LockState::Immutable] {
            assert!(validate_transition(LockState::SoftLock, next).is_ok());
        }
    }

    #[test]
    fn lock_state_round_trips_every_variant() {
        for v in [LockState::Open, LockState::SoftLock,
                  LockState::HardLock, LockState::Immutable] {
            assert_eq!(LockState::from_db(v.as_str()).unwrap(), v);
        }
        assert!(LockState::from_db("Vault").is_err());
    }

    #[test]
    fn pascal_table_name_converts_single_word() {
        assert_eq!(pascal_table_name("vlan"), "Vlan");
    }

    #[test]
    fn pascal_table_name_converts_snake_case() {
        assert_eq!(pascal_table_name("asn_allocation"), "AsnAllocation");
        assert_eq!(pascal_table_name("ip_address"), "IpAddress");
        assert_eq!(pascal_table_name("mlag_domain"), "MlagDomain");
    }
}
