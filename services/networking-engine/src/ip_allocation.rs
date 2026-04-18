//! IP address + subnet allocation. Port of `libs/persistence/Net/IpAllocationService.cs`.
//!
//! Sibling of [`crate::allocation`] — integer-space lives there, CIDR arithmetic here.
//! Same invariants: advisory lock per container, shelf cool-down, range containment.
//! No-overlap for subnets is enforced by the DB's GIST EXCLUDE on `net.subnet`.
//!
//! Family dispatch happens on the `:` detection pattern (same as the C# source).

use sqlx::{PgPool, Postgres, Transaction};
use std::collections::{BTreeSet, HashSet};
use uuid::Uuid;

use crate::allocation::acquire_lock;
use crate::error::EngineError;
use crate::models::{IpAddress, PoolScopeLevel, Subnet};
use crate::{ip_math, ip_math6};

#[derive(Clone)]
pub struct IpAllocationService {
    pool: PgPool,
}

impl IpAllocationService {
    pub fn new(pool: PgPool) -> Self { Self { pool } }

    // ─── Next free IP in subnet ───────────────────────────────────────────

    pub async fn allocate_next_ip(
        &self,
        subnet_id: Uuid,
        org_id: Uuid,
        assigned_to_type: Option<&str>,
        assigned_to_id: Option<Uuid>,
        user_id: Option<i32>,
    ) -> Result<IpAddress, EngineError> {
        let mut tx = self.pool.begin().await?;
        acquire_lock(&mut tx, subnet_id).await?;

        let cidr = fetch_subnet_cidr(&mut tx, subnet_id, org_id).await?;
        let addr_str = if is_v6(&cidr) {
            pick_next_ip_v6(&mut tx, subnet_id, org_id, &cidr).await?
        } else {
            pick_next_ip_v4(&mut tx, subnet_id, org_id, &cidr).await?
        };

        let row: (Uuid, chrono::DateTime<chrono::Utc>) = sqlx::query_as(
            "INSERT INTO net.ip_address
                (organization_id, subnet_id, address, assigned_to_type, assigned_to_id,
                 is_reserved, status, lock_state, created_by, updated_by)
             VALUES ($1, $2, $3::inet, $4, $5,
                     false, 'Active'::net.entity_status, 'Open'::net.lock_state, $6, $6)
             RETURNING id, assigned_at")
            .bind(org_id)
            .bind(subnet_id)
            .bind(&addr_str)
            .bind(assigned_to_type)
            .bind(assigned_to_id)
            .bind(user_id)
            .fetch_one(&mut *tx)
            .await?;

        tx.commit().await?;
        Ok(IpAddress {
            id: row.0,
            organization_id: org_id,
            subnet_id,
            address: addr_str,
            assigned_to_type: assigned_to_type.map(str::to_string),
            assigned_to_id,
            is_reserved: false,
            assigned_at: row.1,
        })
    }

    // ─── Subnet carving (next free /N in pool) ────────────────────────────

    pub async fn allocate_subnet(
        &self,
        pool_id: Uuid,
        org_id: Uuid,
        prefix_length: u32,
        subnet_code: &str,
        display_name: &str,
        scope_level: PoolScopeLevel,
        scope_entity_id: Option<Uuid>,
        parent_subnet_id: Option<Uuid>,
        user_id: Option<i32>,
    ) -> Result<Subnet, EngineError> {
        let mut tx = self.pool.begin().await?;
        acquire_lock(&mut tx, pool_id).await?;

        let pool_cidr = fetch_pool_cidr(&mut tx, pool_id, org_id).await?;
        let cidr = if is_v6(&pool_cidr) {
            let existing = fetch_subnet_ranges_in_pool_v6(&mut tx, pool_id).await?;
            let shelved = fetch_shelved_subnet_ranges_v6(&mut tx, org_id).await?;
            carve_v6(&pool_cidr, prefix_length, pool_id, existing, shelved)?
        } else {
            let existing = fetch_subnet_ranges_in_pool_v4(&mut tx, pool_id).await?;
            let shelved = fetch_shelved_subnet_ranges_v4(&mut tx, org_id).await?;
            carve_v4(&pool_cidr, prefix_length, pool_id, existing, shelved)?
        };

        let id: Uuid = sqlx::query_scalar(
            "INSERT INTO net.subnet
                (organization_id, pool_id, parent_subnet_id, subnet_code, display_name,
                 network, scope_level, scope_entity_id,
                 status, lock_state, created_by, updated_by)
             VALUES ($1, $2, $3, $4, $5, $6::cidr, $7, $8,
                     'Active'::net.entity_status, 'Open'::net.lock_state, $9, $9)
             RETURNING id")
            .bind(org_id)
            .bind(pool_id)
            .bind(parent_subnet_id)
            .bind(subnet_code)
            .bind(display_name)
            .bind(&cidr)
            .bind(scope_level.as_str())
            .bind(scope_entity_id)
            .bind(user_id)
            .fetch_one(&mut *tx)
            .await?;

        tx.commit().await?;
        Ok(Subnet {
            id,
            organization_id: org_id,
            pool_id,
            parent_subnet_id,
            subnet_code: subnet_code.to_string(),
            display_name: display_name.to_string(),
            network: cidr,
            scope_level,
            scope_entity_id,
        })
    }
}

// ─── Carvers (pure) ──────────────────────────────────────────────────────

/// Gap-finder over a sorted blocked list. Walks the pool range in strides of
/// `2^(32 - prefix_length)` and returns the first stride that doesn't overlap
/// any blocked range.
pub fn find_free_aligned_v4(
    pool_network: i64,
    pool_broadcast: i64,
    prefix_length: u32,
    blocked_sorted: &[(i64, i64)],
) -> Option<i64> {
    let stride = ip_math::block_size(prefix_length);
    let mut cursor = ip_math::align_up(pool_network, stride);
    let mut bi = 0usize;

    while cursor + stride - 1 <= pool_broadcast {
        let candidate_last = cursor + stride - 1;

        while bi < blocked_sorted.len() && blocked_sorted[bi].1 < cursor { bi += 1; }
        if bi >= blocked_sorted.len() { return Some(cursor); }

        let (b_first, b_last) = blocked_sorted[bi];
        if candidate_last < b_first { return Some(cursor); }

        cursor = ip_math::align_up(b_last + 1, stride);
    }
    None
}

pub fn find_free_aligned_v6(
    pool_network: u128,
    pool_last: u128,
    prefix_length: u32,
    blocked_sorted: &[(u128, u128)],
) -> Option<u128> {
    let stride = ip_math6::block_size(prefix_length);
    if stride == 0 { return None; }
    let mut cursor = ip_math6::align_up(pool_network, stride);
    let mut bi = 0usize;

    while cursor.checked_add(stride).map(|next| next - 1 <= pool_last).unwrap_or(false) {
        let candidate_last = cursor + stride - 1;
        while bi < blocked_sorted.len() && blocked_sorted[bi].1 < cursor { bi += 1; }
        if bi >= blocked_sorted.len() { return Some(cursor); }

        let (b_first, b_last) = blocked_sorted[bi];
        if candidate_last < b_first { return Some(cursor); }

        cursor = ip_math6::align_up(b_last.saturating_add(1), stride);
    }
    None
}

fn carve_v4(
    pool_cidr: &str,
    prefix_length: u32,
    pool_id: Uuid,
    existing: Vec<(i64, i64)>,
    shelved: Vec<(i64, i64)>,
) -> Result<String, EngineError> {
    let (pool_net, pool_bcast, pool_prefix) = ip_math::parse_v4(pool_cidr)?;
    if prefix_length < pool_prefix || prefix_length > 32 {
        return Err(EngineError::range_violation(
            "subnet prefix", prefix_length as i64, pool_prefix as i64, 32));
    }

    let mut blocked: Vec<(i64, i64)> = Vec::with_capacity(existing.len() + shelved.len());
    blocked.extend(existing);
    blocked.extend(shelved);
    blocked.sort_by_key(|(f, _)| *f);

    let candidate = find_free_aligned_v4(pool_net, pool_bcast, prefix_length, &blocked)
        .ok_or_else(|| EngineError::pool_exhausted(format!("subnet /{prefix_length}"), pool_id))?;
    Ok(ip_math::to_cidr(candidate, prefix_length))
}

fn carve_v6(
    pool_cidr: &str,
    prefix_length: u32,
    pool_id: Uuid,
    existing: Vec<(u128, u128)>,
    shelved: Vec<(u128, u128)>,
) -> Result<String, EngineError> {
    let (pool_net, pool_last, pool_prefix) = ip_math6::parse_v6(pool_cidr)?;
    if prefix_length < pool_prefix || prefix_length > 128 {
        return Err(EngineError::range_violation(
            "subnet prefix", prefix_length as i64, pool_prefix as i64, 128));
    }

    let mut blocked: Vec<(u128, u128)> = Vec::with_capacity(existing.len() + shelved.len());
    blocked.extend(existing);
    blocked.extend(shelved);
    blocked.sort_by_key(|(f, _)| *f);

    let candidate = find_free_aligned_v6(pool_net, pool_last, prefix_length, &blocked)
        .ok_or_else(|| EngineError::pool_exhausted(format!("IPv6 subnet /{prefix_length}"), pool_id))?;
    Ok(ip_math6::to_cidr(candidate, prefix_length))
}

// ─── DB helpers ──────────────────────────────────────────────────────────

fn is_v6(cidr: &str) -> bool { cidr.contains(':') }

fn strip_prefix(s: &str) -> &str {
    match s.find('/') {
        Some(i) => &s[..i],
        None => s,
    }
}

async fn fetch_subnet_cidr(
    tx: &mut Transaction<'_, Postgres>,
    subnet_id: Uuid,
    org_id: Uuid,
) -> Result<String, EngineError> {
    let raw: Option<String> = sqlx::query_scalar(
        "SELECT network::text FROM net.subnet
         WHERE id = $1 AND organization_id = $2 AND deleted_at IS NULL")
        .bind(subnet_id)
        .bind(org_id)
        .fetch_optional(&mut **tx)
        .await?;
    raw.ok_or_else(|| EngineError::container_not_found("subnet", subnet_id))
}

async fn fetch_pool_cidr(
    tx: &mut Transaction<'_, Postgres>,
    pool_id: Uuid,
    org_id: Uuid,
) -> Result<String, EngineError> {
    let raw: Option<String> = sqlx::query_scalar(
        "SELECT network::text FROM net.ip_pool
         WHERE id = $1 AND organization_id = $2 AND deleted_at IS NULL")
        .bind(pool_id)
        .bind(org_id)
        .fetch_optional(&mut **tx)
        .await?;
    raw.ok_or_else(|| EngineError::container_not_found("ip_pool", pool_id))
}

async fn pick_next_ip_v4(
    tx: &mut Transaction<'_, Postgres>,
    subnet_id: Uuid,
    org_id: Uuid,
    cidr: &str,
) -> Result<String, EngineError> {
    let (network, broadcast, prefix) = ip_math::parse_v4(cidr)?;
    let (first, last) = ip_math::host_range(network, broadcast, prefix);

    let used = fetch_used_ips_v4(tx, subnet_id).await?;
    let shelved = fetch_shelved_ips_v4(tx, org_id).await?;

    let mut candidate = first;
    while candidate <= last {
        if !used.contains(&candidate) && !shelved.contains(&candidate) {
            return Ok(ip_math::to_ip(candidate));
        }
        candidate += 1;
    }
    Err(EngineError::pool_exhausted("IP address", subnet_id))
}

async fn pick_next_ip_v6(
    tx: &mut Transaction<'_, Postgres>,
    subnet_id: Uuid,
    org_id: Uuid,
    cidr: &str,
) -> Result<String, EngineError> {
    let (network, last, _) = ip_math6::parse_v6(cidr)?;
    let (first, end_usable) = ip_math6::host_range(network, last);

    let used = fetch_used_ips_v6(tx, subnet_id).await?;
    let shelved = fetch_shelved_ips_v6(tx, org_id).await?;

    // Blocked set ordered ascending for gap walk (subnets can be huge; naive scan won't finish).
    let mut blocked: BTreeSet<u128> = BTreeSet::new();
    blocked.extend(&used);
    blocked.extend(&shelved);

    let mut candidate = first;
    for b in &blocked {
        if *b < candidate { continue; }
        if *b > candidate { return Ok(ip_math6::to_ip(candidate)); }
        // b == candidate — advance past.
        if candidate == end_usable {
            return Err(EngineError::pool_exhausted("IPv6 address", subnet_id));
        }
        candidate += 1;
    }
    if candidate > end_usable {
        return Err(EngineError::pool_exhausted("IPv6 address", subnet_id));
    }
    Ok(ip_math6::to_ip(candidate))
}

async fn fetch_used_ips_v4(
    tx: &mut Transaction<'_, Postgres>,
    subnet_id: Uuid,
) -> Result<HashSet<i64>, EngineError> {
    let rows: Vec<(String,)> = sqlx::query_as(
        "SELECT address::text FROM net.ip_address
         WHERE subnet_id = $1 AND deleted_at IS NULL")
        .bind(subnet_id)
        .fetch_all(&mut **tx)
        .await?;
    let mut set = HashSet::new();
    for (s,) in rows {
        let stripped = strip_prefix(&s);
        if !stripped.contains(':') {
            if let Ok(v) = ip_math::ip_to_long(stripped) { set.insert(v); }
        }
    }
    Ok(set)
}

async fn fetch_shelved_ips_v4(
    tx: &mut Transaction<'_, Postgres>,
    org_id: Uuid,
) -> Result<HashSet<i64>, EngineError> {
    let rows: Vec<(String,)> = sqlx::query_as(
        "SELECT resource_key FROM net.reservation_shelf
         WHERE organization_id = $1
           AND resource_type = 'ip'
           AND available_after > now()
           AND deleted_at IS NULL")
        .bind(org_id)
        .fetch_all(&mut **tx)
        .await?;
    let mut set = HashSet::new();
    for (s,) in rows {
        if !s.contains(':') {
            if let Ok(v) = ip_math::ip_to_long(&s) { set.insert(v); }
        }
    }
    Ok(set)
}

async fn fetch_used_ips_v6(
    tx: &mut Transaction<'_, Postgres>,
    subnet_id: Uuid,
) -> Result<HashSet<u128>, EngineError> {
    let rows: Vec<(String,)> = sqlx::query_as(
        "SELECT address::text FROM net.ip_address
         WHERE subnet_id = $1 AND deleted_at IS NULL")
        .bind(subnet_id)
        .fetch_all(&mut **tx)
        .await?;
    let mut set = HashSet::new();
    for (s,) in rows {
        let stripped = strip_prefix(&s);
        if stripped.contains(':') {
            if let Ok(v) = ip_math6::ip_to_u128(stripped) { set.insert(v); }
        }
    }
    Ok(set)
}

async fn fetch_shelved_ips_v6(
    tx: &mut Transaction<'_, Postgres>,
    org_id: Uuid,
) -> Result<HashSet<u128>, EngineError> {
    let rows: Vec<(String,)> = sqlx::query_as(
        "SELECT resource_key FROM net.reservation_shelf
         WHERE organization_id = $1
           AND resource_type = 'ip'
           AND available_after > now()
           AND deleted_at IS NULL")
        .bind(org_id)
        .fetch_all(&mut **tx)
        .await?;
    let mut set = HashSet::new();
    for (s,) in rows {
        if s.contains(':') {
            if let Ok(v) = ip_math6::ip_to_u128(&s) { set.insert(v); }
        }
    }
    Ok(set)
}

async fn fetch_subnet_ranges_in_pool_v4(
    tx: &mut Transaction<'_, Postgres>,
    pool_id: Uuid,
) -> Result<Vec<(i64, i64)>, EngineError> {
    let rows: Vec<(String,)> = sqlx::query_as(
        "SELECT network::text FROM net.subnet
         WHERE pool_id = $1 AND deleted_at IS NULL")
        .bind(pool_id)
        .fetch_all(&mut **tx)
        .await?;
    let mut list = Vec::with_capacity(rows.len());
    for (cidr,) in rows {
        if !cidr.contains(':') {
            if let Ok((net, bcast, _)) = ip_math::parse_v4(&cidr) {
                list.push((net, bcast));
            }
        }
    }
    Ok(list)
}

async fn fetch_shelved_subnet_ranges_v4(
    tx: &mut Transaction<'_, Postgres>,
    org_id: Uuid,
) -> Result<Vec<(i64, i64)>, EngineError> {
    let rows: Vec<(String,)> = sqlx::query_as(
        "SELECT resource_key FROM net.reservation_shelf
         WHERE organization_id = $1
           AND resource_type = 'subnet'
           AND available_after > now()
           AND deleted_at IS NULL")
        .bind(org_id)
        .fetch_all(&mut **tx)
        .await?;
    let mut list = Vec::new();
    for (cidr,) in rows {
        if cidr.contains(':') { continue; }
        if let Ok((net, bcast, _)) = ip_math::parse_v4(&cidr) {
            list.push((net, bcast));
        }
    }
    Ok(list)
}

async fn fetch_subnet_ranges_in_pool_v6(
    tx: &mut Transaction<'_, Postgres>,
    pool_id: Uuid,
) -> Result<Vec<(u128, u128)>, EngineError> {
    let rows: Vec<(String,)> = sqlx::query_as(
        "SELECT network::text FROM net.subnet
         WHERE pool_id = $1 AND deleted_at IS NULL")
        .bind(pool_id)
        .fetch_all(&mut **tx)
        .await?;
    let mut list = Vec::with_capacity(rows.len());
    for (cidr,) in rows {
        if cidr.contains(':') {
            if let Ok((net, last, _)) = ip_math6::parse_v6(&cidr) {
                list.push((net, last));
            }
        }
    }
    Ok(list)
}

async fn fetch_shelved_subnet_ranges_v6(
    tx: &mut Transaction<'_, Postgres>,
    org_id: Uuid,
) -> Result<Vec<(u128, u128)>, EngineError> {
    let rows: Vec<(String,)> = sqlx::query_as(
        "SELECT resource_key FROM net.reservation_shelf
         WHERE organization_id = $1
           AND resource_type = 'subnet'
           AND available_after > now()
           AND deleted_at IS NULL")
        .bind(org_id)
        .fetch_all(&mut **tx)
        .await?;
    let mut list = Vec::new();
    for (cidr,) in rows {
        if !cidr.contains(':') { continue; }
        if let Ok((net, last, _)) = ip_math6::parse_v6(&cidr) {
            list.push((net, last));
        }
    }
    Ok(list)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn find_free_aligned_v4_returns_first_stride_when_empty() {
        // /24 pool, carve /30 → stride 4, first candidate is 10.0.0.0.
        let (net, bcast, _) = ip_math::parse_v4("10.0.0.0/24").unwrap();
        let got = find_free_aligned_v4(net, bcast, 30, &[]);
        assert_eq!(got, Some(net));
    }

    #[test]
    fn find_free_aligned_v4_skips_existing() {
        let (net, bcast, _) = ip_math::parse_v4("10.0.0.0/24").unwrap();
        // First /30 occupied (.0-.3), second (.4-.7) should be picked.
        let blocked = vec![(net, net + 3)];
        let got = find_free_aligned_v4(net, bcast, 30, &blocked);
        assert_eq!(got, Some(net + 4));
    }

    #[test]
    fn find_free_aligned_v4_returns_none_when_exhausted() {
        // /30 pool completely covered by one /30 allocation.
        let (net, bcast, _) = ip_math::parse_v4("10.0.0.0/30").unwrap();
        let blocked = vec![(net, bcast)];
        assert_eq!(find_free_aligned_v4(net, bcast, 30, &blocked), None);
    }

    #[test]
    fn find_free_aligned_v4_handles_realignment() {
        // /24 pool, first /30 occupied at .0-.3, carve /30 → should get .4
        // (not .1, not .4-.7 mangled to .3-.6).
        let (net, bcast, _) = ip_math::parse_v4("10.0.0.0/24").unwrap();
        let blocked = vec![(net, net + 3)];
        let got = find_free_aligned_v4(net, bcast, 30, &blocked).unwrap();
        assert_eq!(got % 4, 0, "candidate must be /30-aligned");
        assert_eq!(got, net + 4);
    }

    #[test]
    fn find_free_aligned_v4_jumps_over_misaligned_blocker() {
        // Pool .0-.255, blocker spans .0-.5 (unaligned), carve /30 → should
        // skip past .5 and realign to .8.
        let (net, bcast, _) = ip_math::parse_v4("10.0.0.0/24").unwrap();
        let blocked = vec![(net, net + 5)];
        let got = find_free_aligned_v4(net, bcast, 30, &blocked).unwrap();
        assert_eq!(got, net + 8);
    }

    #[test]
    fn find_free_aligned_v6_returns_first_stride_when_empty() {
        let (net, last, _) = ip_math6::parse_v6("2001:db8::/48").unwrap();
        let got = find_free_aligned_v6(net, last, 64, &[]);
        assert_eq!(got, Some(net));
    }

    #[test]
    fn find_free_aligned_v6_skips_existing() {
        let (net, last, _) = ip_math6::parse_v6("2001:db8::/48").unwrap();
        let first_64 = ip_math6::block_size(64);
        let blocked = vec![(net, net + first_64 - 1)];
        let got = find_free_aligned_v6(net, last, 64, &blocked);
        assert_eq!(got, Some(net + first_64));
    }

    #[test]
    fn carve_v4_rejects_prefix_shorter_than_pool() {
        let err = carve_v4("10.0.0.0/24", 20, Uuid::nil(), vec![], vec![]).unwrap_err();
        match err {
            EngineError::RangeViolation { .. } => {},
            _ => panic!("expected RangeViolation, got {err:?}"),
        }
    }

    #[test]
    fn carve_v4_basic() {
        let cidr = carve_v4("10.0.0.0/24", 30, Uuid::nil(), vec![], vec![]).unwrap();
        assert_eq!(cidr, "10.0.0.0/30");
    }

    #[test]
    fn carve_v4_skips_existing() {
        let cidr = carve_v4("10.0.0.0/24", 30, Uuid::nil(),
            vec![(ip_math::ip_to_long("10.0.0.0").unwrap(), ip_math::ip_to_long("10.0.0.3").unwrap())],
            vec![]).unwrap();
        assert_eq!(cidr, "10.0.0.4/30");
    }

    #[test]
    fn is_v6_detects_colon() {
        assert!(is_v6("2001:db8::/64"));
        assert!(!is_v6("10.0.0.0/24"));
    }
}
