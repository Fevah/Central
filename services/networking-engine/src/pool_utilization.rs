//! Pool utilization — per-pool "what's used vs available" rollup
//! across ASN / VLAN / IP pools. One endpoint answers the operator
//! question: "am I about to run out of VLANs in the Immunocore
//! pool?". Web + WPF dashboards render a single flat grid from the
//! combined result.
//!
//! Capacity math differs per pool family:
//!   - ASN pool:  capacity = asn_last - asn_first + 1
//!                used     = COUNT(net.asn_allocation WHERE pool's block)
//!   - VLAN pool: capacity = vlan_last - vlan_first + 1
//!                used     = COUNT(net.vlan WHERE pool's block)
//!   - IP pool:   capacity = 2^(max_prefix - network prefix)
//!                           (IPv4 /24 → 256; IPv6 /64 → 1 for addresses
//!                           tracked as whole-prefix carver slots rather
//!                           than individual hosts. For the dashboard we
//!                           show subnet count as "subnets" and the
//!                           allocated ip_address count as "addresses".
//!                used     = COUNT(net.subnet) for subnets
//!                           COUNT(net.ip_address) for addresses
//!
//! The result shape is uniform to keep the grid simple: every row
//! carries used / capacity / percentFull. The IP pool gets a paired
//! row (one for subnets, one for addresses) so the operator sees
//! both dimensions without a second query.

use serde::Serialize;
use sqlx::PgPool;
use uuid::Uuid;

use crate::error::EngineError;

/// Result shape. `kind` disambiguates rows whose other fields alone
/// don't tell you which dimension they describe (IP pool has two
/// rows: Subnets + Addresses).
#[derive(Debug, Clone, Serialize, sqlx::FromRow)]
#[serde(rename_all = "camelCase")]
pub struct PoolUtilizationRow {
    pub pool_kind: String,       // "ASN" / "VLAN" / "IP:Subnets" / "IP:Addresses"
    pub pool_id: Uuid,
    pub pool_code: String,
    pub display_name: String,
    pub used: i64,
    pub capacity: i64,
    /// Integer percent [0, 100+]. Can exceed 100 for data-quality
    /// issues (used count > computed capacity); capped at 999 so
    /// the UI doesn't render junk on an Integer overflow.
    pub percent_full: i32,
    pub status: String,
}

/// Clamp + percentage helper. Division by zero → 0; overflow → 999
/// so the UI treats it as "something is very wrong" without
/// rendering garbage.
fn pct(used: i64, capacity: i64) -> i32 {
    if capacity <= 0 { return 0; }
    let raw = (used.saturating_mul(100)) / capacity;
    raw.min(999) as i32
}

pub async fn list_utilization(
    pool: &PgPool,
    org_id: Uuid,
) -> Result<Vec<PoolUtilizationRow>, EngineError> {
    // ASN pools — one row per pool. capacity is asn_last-asn_first+1;
    // used is the count of active allocations across every block
    // under the pool.
    let asn: Vec<(Uuid, String, String, i64, i64, String)> = sqlx::query_as(
        "SELECT p.id,
                p.pool_code,
                p.display_name,
                COALESCE((
                  SELECT COUNT(*)::bigint
                    FROM net.asn_allocation a
                    JOIN net.asn_block b ON b.id = a.block_id
                   WHERE b.pool_id = p.id
                     AND a.deleted_at IS NULL
                     AND a.status = 'Active'::net.entity_status
                ), 0) AS used,
                (p.asn_last - p.asn_first + 1)::bigint AS capacity,
                p.status::text
           FROM net.asn_pool p
          WHERE p.organization_id = $1 AND p.deleted_at IS NULL")
        .bind(org_id).fetch_all(pool).await?;

    // VLAN pools — one row per pool. Used is the count of live
    // net.vlan rows across every block under the pool.
    let vlan: Vec<(Uuid, String, String, i64, i64, String)> = sqlx::query_as(
        "SELECT p.id,
                p.pool_code,
                p.display_name,
                COALESCE((
                  SELECT COUNT(*)::bigint
                    FROM net.vlan v
                    JOIN net.vlan_block b ON b.id = v.block_id
                   WHERE b.pool_id = p.id
                     AND v.deleted_at IS NULL
                     AND v.status = 'Active'::net.entity_status
                ), 0) AS used,
                (p.vlan_last - p.vlan_first + 1)::bigint AS capacity,
                p.status::text
           FROM net.vlan_pool p
          WHERE p.organization_id = $1 AND p.deleted_at IS NULL")
        .bind(org_id).fetch_all(pool).await?;

    // IP pools — two rows per pool (Subnets + Addresses).
    //   Subnet capacity computed from pool CIDR via PG
    //   `(2 ^ (32 - masklen(network)))` for v4 — gives total host
    //   slots. For subnet *count* capacity we'd need a nominal
    //   "carve size"; the dashboard reports address-level capacity
    //   instead which is the metric operators actually care about
    //   ("am I running out of IPs?"). Subnet row just reports
    //   count-used / count-of-subnets-defined.
    let ip: Vec<(Uuid, String, String, i64, i64, String, i64)> = sqlx::query_as(
        "SELECT p.id,
                p.pool_code,
                p.display_name,
                -- subnet count under this pool
                COALESCE((
                  SELECT COUNT(*)::bigint
                    FROM net.subnet s
                   WHERE s.pool_id = p.id
                     AND s.deleted_at IS NULL
                     AND s.status = 'Active'::net.entity_status
                ), 0) AS subnet_count,
                -- address count under this pool (IPs allocated from
                -- any subnet that belongs to this pool)
                COALESCE((
                  SELECT COUNT(*)::bigint
                    FROM net.ip_address ip
                    JOIN net.subnet sn ON sn.id = ip.subnet_id
                   WHERE sn.pool_id = p.id
                     AND ip.deleted_at IS NULL
                ), 0) AS address_count,
                p.status::text,
                -- capacity in number of addresses. 2^(host_bits) for v4,
                -- 2^64 is too large for i64 so we cap at 2^48 for v6
                -- (enough for UI display; the dashboard surfaces this
                -- as a proxy, not a true allocation-limit number).
                CASE
                  WHEN p.address_family = 'v4'
                    THEN LEAST((1::bigint) << (32 - masklen(p.network)), 9223372036854775807::bigint)
                  ELSE LEAST((1::bigint) << LEAST(48, 128 - masklen(p.network)), 9223372036854775807::bigint)
                END AS address_capacity
           FROM net.ip_pool p
          WHERE p.organization_id = $1 AND p.deleted_at IS NULL")
        .bind(org_id).fetch_all(pool).await?;

    let mut rows: Vec<PoolUtilizationRow> = Vec::with_capacity(
        asn.len() + vlan.len() + 2 * ip.len());

    for (id, code, name, used, cap, status) in asn {
        rows.push(PoolUtilizationRow {
            pool_kind: "ASN".into(),
            pool_id: id, pool_code: code, display_name: name,
            used, capacity: cap, percent_full: pct(used, cap), status,
        });
    }
    for (id, code, name, used, cap, status) in vlan {
        rows.push(PoolUtilizationRow {
            pool_kind: "VLAN".into(),
            pool_id: id, pool_code: code, display_name: name,
            used, capacity: cap, percent_full: pct(used, cap), status,
        });
    }
    for (id, code, name, subnet_count, address_count, status, address_capacity) in ip {
        // IP:Subnets row — used is the count of Active subnets; we
        // don't carry a subnet-count capacity (would need a nominal
        // carve size per pool). Reporting "N subnets defined" in
        // the `used` column with capacity=0 suppresses the percent.
        rows.push(PoolUtilizationRow {
            pool_kind: "IP:Subnets".into(),
            pool_id: id, pool_code: code.clone(), display_name: name.clone(),
            used: subnet_count, capacity: 0, percent_full: 0,
            status: status.clone(),
        });
        rows.push(PoolUtilizationRow {
            pool_kind: "IP:Addresses".into(),
            pool_id: id, pool_code: code, display_name: name,
            used: address_count, capacity: address_capacity,
            percent_full: pct(address_count, address_capacity),
            status,
        });
    }

    // Stable order — sort by kind then pool_code so the UI grid
    // is predictable across reloads.
    rows.sort_by(|a, b| {
        a.pool_kind.cmp(&b.pool_kind).then_with(|| a.pool_code.cmp(&b.pool_code))
    });
    Ok(rows)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn pct_handles_zero_capacity_without_panic() {
        assert_eq!(pct(5, 0), 0);
        assert_eq!(pct(0, 0), 0);
    }

    #[test]
    fn pct_truncates_to_integer() {
        assert_eq!(pct(1, 3), 33);    // 33.33… → 33
        assert_eq!(pct(2, 3), 66);    // 66.66… → 66
        assert_eq!(pct(50, 100), 50);
        assert_eq!(pct(100, 100), 100);
    }

    #[test]
    fn pct_caps_at_999_to_avoid_overflow_display() {
        // Used > capacity (data-quality issue); percent caps at 999
        // so the UI renders "999%" instead of something senseless.
        assert_eq!(pct(100_000, 1), 999);
    }

    #[test]
    fn pct_handles_negative_capacity_as_zero() {
        assert_eq!(pct(5, -1), 0);
    }
}
