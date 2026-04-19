//! Global search — tsvector-based full-text search across the 6
//! core tenant-owned entities, returning a ranked flat list.
//!
//! ## Design
//!
//! Query-time UNION across six entity tables, no stored tsvector
//! columns, no triggers. Each entity has a partial GIN index on the
//! same `to_tsvector('english'::regconfig, …)` expression that the
//! query uses (migration 107 — `ix_<entity>_search_gin`). The
//! `::regconfig` cast is load-bearing: without it the expression is
//! STABLE rather than IMMUTABLE and the planner falls back to a
//! Seq Scan even when an index exists.
//!
//! Index expressions in migration 107 must stay byte-for-byte
//! identical to the SQL fragments built below — if you reorder the
//! `coalesce(...)` chain or add a column to the search projection,
//! land a follow-on migration that drops + recreates the matching
//! index (`ix_<entity>_search_gin`) so the planner keeps using it.
//!
//! ## RBAC
//!
//! Search intentionally does NOT filter by `read:entity_type` at the
//! SQL level — the join would explode the query complexity. Instead
//! the handler post-filters the result list by running
//! `scope_grants::has_permission` per row. Operator has read on a
//! subset of the tenant's estate → they see only the hits within
//! their grants. Operator has no grants at all + X-User-Id set →
//! empty result set. Service calls (no X-User-Id) skip the filter,
//! same rule as the rest of the surface.

use serde::{Deserialize, Serialize};
use sqlx::PgPool;
use uuid::Uuid;

use crate::error::EngineError;

// ─── Request / response types ────────────────────────────────────────────

#[derive(Debug, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct SearchQuery {
    pub organization_id: Uuid,
    /// Free-text query. Fed into `plainto_tsquery('english', …)` so
    /// operators can type plain english ("mep core 02") and Postgres
    /// handles tokenisation + stemming.
    pub q: String,
    /// Optional comma-separated entity filter (`Device,Vlan`). When
    /// omitted, searches every supported entity type.
    pub entity_types: Option<String>,
    /// Results cap — clamp to `[1, SEARCH_LIMIT_MAX]` in the handler.
    pub limit: Option<i64>,
}

#[derive(Debug, Clone, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct SearchResult {
    pub entity_type: String,
    pub id: Uuid,
    pub label: String,
    /// Postgres `ts_rank` score. Higher = better match.
    pub rank: f32,
    /// Short human-readable snippet — concatenation of the entity's
    /// searchable fields, truncated to 140 chars. Populated per-
    /// entity by the search expression below.
    pub snippet: String,
}

pub const SEARCH_LIMIT_DEFAULT: i64 = 50;
pub const SEARCH_LIMIT_MAX: i64 = 500;

pub fn clamp_search_limit(requested: Option<i64>) -> i64 {
    let n = requested.unwrap_or(SEARCH_LIMIT_DEFAULT);
    n.clamp(1, SEARCH_LIMIT_MAX)
}

/// Parse `entity_types` query parameter into a lower-cased HashSet.
/// Empty / missing → None (search all). Unknown names are silently
/// dropped rather than rejecting the query — operators pasting from
/// old docs get partial-filter behaviour rather than a 400.
pub fn parse_entity_types(s: Option<&str>) -> Option<std::collections::HashSet<String>> {
    let raw = s?.trim();
    if raw.is_empty() { return None; }
    let set: std::collections::HashSet<String> = raw.split(',')
        .map(|s| s.trim().to_string())
        .filter(|s| !s.is_empty())
        .collect();
    if set.is_empty() { None } else { Some(set) }
}

pub fn is_supported_entity_type(name: &str) -> bool {
    matches!(name, "Device"|"Vlan"|"Subnet"|"Server"|"Link"|"DhcpRelayTarget")
}

// ─── Search core ─────────────────────────────────────────────────────────

/// Run the search query. Each UNION branch is gated on whether the
/// entity type is requested (no filter, or the set contains this
/// entity) so a narrow search still only scans the relevant tables.
/// Results come back pre-sorted by rank DESC + limited server-side.
pub async fn global_search(
    pool: &PgPool,
    org_id: Uuid,
    q: &str,
    entity_types: Option<&std::collections::HashSet<String>>,
    limit: i64,
) -> Result<Vec<SearchResult>, EngineError> {
    // Empty query → empty result set. Saves a full table scan and
    // avoids tsquery's "unrecognisable token" noise.
    if q.trim().is_empty() { return Ok(vec![]); }

    let wants = |name: &str| -> bool {
        match entity_types {
            Some(set) => set.contains(name),
            None      => true,
        }
    };

    // Build a vector of SQL fragments — one per entity type. Each
    // fragment projects the same shape: (entity_type, id, label,
    // rank, snippet). NULL-coalescing ensures empty columns don't
    // tsvector-null the whole row.
    let mut fragments: Vec<String> = Vec::with_capacity(6);
    if wants("Device") {
        fragments.push(String::from(
            "SELECT 'Device' AS entity_type, d.id,
                    d.hostname AS label,
                    ts_rank(
                        to_tsvector('english'::regconfig,
                            coalesce(d.hostname,'') || ' ' ||
                            coalesce(d.device_code,'') || ' ' ||
                            coalesce(d.notes,'')),
                        plainto_tsquery('english', $1)
                    ) AS rank,
                    left(coalesce(d.hostname,'') || ' · ' ||
                         coalesce(d.device_code,''), 140) AS snippet
               FROM net.device d
              WHERE d.organization_id = $2 AND d.deleted_at IS NULL
                AND to_tsvector('english'::regconfig,
                        coalesce(d.hostname,'') || ' ' ||
                        coalesce(d.device_code,'') || ' ' ||
                        coalesce(d.notes,''))
                    @@ plainto_tsquery('english', $1)"));
    }
    if wants("Vlan") {
        fragments.push(String::from(
            "SELECT 'Vlan' AS entity_type, v.id,
                    (v.vlan_id::text || ' ' || v.display_name) AS label,
                    ts_rank(
                        to_tsvector('english'::regconfig,
                            coalesce(v.display_name,'') || ' ' ||
                            coalesce(v.description,'') || ' ' ||
                            coalesce(v.notes,'')),
                        plainto_tsquery('english', $1)
                    ) AS rank,
                    left(coalesce(v.display_name,'') || ' · ' ||
                         coalesce(v.description,''), 140) AS snippet
               FROM net.vlan v
              WHERE v.organization_id = $2 AND v.deleted_at IS NULL
                AND to_tsvector('english'::regconfig,
                        coalesce(v.display_name,'') || ' ' ||
                        coalesce(v.description,'') || ' ' ||
                        coalesce(v.notes,''))
                    @@ plainto_tsquery('english', $1)"));
    }
    if wants("Subnet") {
        fragments.push(String::from(
            "SELECT 'Subnet' AS entity_type, s.id,
                    (s.subnet_code || ' ' || s.display_name) AS label,
                    ts_rank(
                        to_tsvector('english'::regconfig,
                            coalesce(s.subnet_code,'') || ' ' ||
                            coalesce(s.display_name,'') || ' ' ||
                            coalesce(s.notes,'')),
                        plainto_tsquery('english', $1)
                    ) AS rank,
                    left(coalesce(s.subnet_code,'') || ' · ' ||
                         coalesce(s.display_name,'') || ' · ' ||
                         s.network::text, 140) AS snippet
               FROM net.subnet s
              WHERE s.organization_id = $2 AND s.deleted_at IS NULL
                AND to_tsvector('english'::regconfig,
                        coalesce(s.subnet_code,'') || ' ' ||
                        coalesce(s.display_name,'') || ' ' ||
                        coalesce(s.notes,''))
                    @@ plainto_tsquery('english', $1)"));
    }
    if wants("Server") {
        fragments.push(String::from(
            "SELECT 'Server' AS entity_type, srv.id,
                    srv.hostname AS label,
                    ts_rank(
                        to_tsvector('english'::regconfig,
                            coalesce(srv.hostname,'') || ' ' ||
                            coalesce(srv.display_name,'') || ' ' ||
                            coalesce(srv.notes,'')),
                        plainto_tsquery('english', $1)
                    ) AS rank,
                    left(coalesce(srv.hostname,'') || ' · ' ||
                         coalesce(srv.display_name,''), 140) AS snippet
               FROM net.server srv
              WHERE srv.organization_id = $2 AND srv.deleted_at IS NULL
                AND to_tsvector('english'::regconfig,
                        coalesce(srv.hostname,'') || ' ' ||
                        coalesce(srv.display_name,'') || ' ' ||
                        coalesce(srv.notes,''))
                    @@ plainto_tsquery('english', $1)"));
    }
    if wants("Link") {
        fragments.push(String::from(
            "SELECT 'Link' AS entity_type, l.id,
                    l.link_code AS label,
                    ts_rank(
                        to_tsvector('english'::regconfig,
                            coalesce(l.link_code,'') || ' ' ||
                            coalesce(l.display_name,'') || ' ' ||
                            coalesce(l.description,'') || ' ' ||
                            coalesce(l.notes,'')),
                        plainto_tsquery('english', $1)
                    ) AS rank,
                    left(coalesce(l.link_code,'') || ' · ' ||
                         coalesce(l.display_name,''), 140) AS snippet
               FROM net.link l
              WHERE l.organization_id = $2 AND l.deleted_at IS NULL
                AND to_tsvector('english'::regconfig,
                        coalesce(l.link_code,'') || ' ' ||
                        coalesce(l.display_name,'') || ' ' ||
                        coalesce(l.description,'') || ' ' ||
                        coalesce(l.notes,''))
                    @@ plainto_tsquery('english', $1)"));
    }
    if wants("DhcpRelayTarget") {
        // DHCP relay rows don't have a natural name — use the
        // vlan_id + server_ip as the label; search notes.
        fragments.push(String::from(
            "SELECT 'DhcpRelayTarget' AS entity_type, drt.id,
                    ('vlan ' || v.vlan_id::text || ' → ' || host(drt.server_ip)) AS label,
                    ts_rank(
                        to_tsvector('english'::regconfig,
                            coalesce(host(drt.server_ip),'') || ' ' ||
                            coalesce(drt.notes,'')),
                        plainto_tsquery('english', $1)
                    ) AS rank,
                    left(coalesce(host(drt.server_ip),'') || ' · ' ||
                         coalesce(drt.notes,''), 140) AS snippet
               FROM net.dhcp_relay_target drt
               JOIN net.vlan v ON v.id = drt.vlan_id AND v.deleted_at IS NULL
              WHERE drt.organization_id = $2 AND drt.deleted_at IS NULL
                AND to_tsvector('english'::regconfig,
                        coalesce(host(drt.server_ip),'') || ' ' ||
                        coalesce(drt.notes,''))
                    @@ plainto_tsquery('english', $1)"));
    }

    if fragments.is_empty() {
        // Every filter rejected — empty set. Not an error; caller
        // asked about entity types we don't support.
        return Ok(vec![]);
    }

    let sql = format!(
        "{}\nORDER BY rank DESC\nLIMIT $3",
        fragments.join("\nUNION ALL\n"));

    let rows: Vec<(String, Uuid, String, f32, String)> = sqlx::query_as(&sql)
        .bind(q)
        .bind(org_id)
        .bind(limit)
        .fetch_all(pool)
        .await?;

    Ok(rows.into_iter().map(|(entity_type, id, label, rank, snippet)| SearchResult {
        entity_type, id, label, rank, snippet,
    }).collect())
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn clamp_search_limit_defaults_to_50_when_none() {
        assert_eq!(clamp_search_limit(None), SEARCH_LIMIT_DEFAULT);
    }

    #[test]
    fn clamp_search_limit_clamps_to_max() {
        assert_eq!(clamp_search_limit(Some(SEARCH_LIMIT_MAX + 1)), SEARCH_LIMIT_MAX);
        assert_eq!(clamp_search_limit(Some(i64::MAX)),              SEARCH_LIMIT_MAX);
    }

    #[test]
    fn clamp_search_limit_clamps_low_to_one() {
        assert_eq!(clamp_search_limit(Some(0)),  1);
        assert_eq!(clamp_search_limit(Some(-5)), 1);
    }

    #[test]
    fn clamp_search_limit_passes_in_range_through() {
        assert_eq!(clamp_search_limit(Some(25)), 25);
        assert_eq!(clamp_search_limit(Some(SEARCH_LIMIT_MAX)), SEARCH_LIMIT_MAX);
    }

    #[test]
    fn parse_entity_types_accepts_comma_list() {
        let set = parse_entity_types(Some("Device,Vlan,Subnet")).expect("Some");
        assert_eq!(set.len(), 3);
        assert!(set.contains("Device"));
        assert!(set.contains("Vlan"));
        assert!(set.contains("Subnet"));
    }

    #[test]
    fn parse_entity_types_trims_whitespace_and_skips_empties() {
        let set = parse_entity_types(Some(" Device , , Vlan ")).expect("Some");
        assert_eq!(set.len(), 2);
        assert!(set.contains("Device"));
        assert!(set.contains("Vlan"));
    }

    #[test]
    fn parse_entity_types_none_or_empty_returns_none() {
        assert!(parse_entity_types(None).is_none());
        assert!(parse_entity_types(Some("")).is_none());
        assert!(parse_entity_types(Some("   ")).is_none());
        assert!(parse_entity_types(Some(",,,,")).is_none());
    }

    #[test]
    fn is_supported_entity_type_covers_every_union_branch() {
        for e in ["Device","Vlan","Subnet","Server","Link","DhcpRelayTarget"] {
            assert!(is_supported_entity_type(e), "{e} should be supported");
        }
    }

    #[test]
    fn is_supported_entity_type_rejects_unknowns() {
        assert!(!is_supported_entity_type("Vrrp"));
        assert!(!is_supported_entity_type("Region"));
        assert!(!is_supported_entity_type(""));
        assert!(!is_supported_entity_type("; DROP TABLE net.device;--"));
    }

    /// Guardrail: every `to_tsvector(...)` in the search query must
    /// use the `'english'::regconfig` cast so the expression is
    /// IMMUTABLE (and therefore matchable by the partial GIN indexes
    /// created in migration 107). Catching a slip from `'english'` to
    /// `'english'::regconfig` (or vice-versa) at unit-test time beats
    /// debugging a Seq Scan in prod.
    #[test]
    fn every_to_tsvector_uses_regconfig_cast() {
        // Inspect only the production (pre-test-module) source so the
        // test's own string literals don't count toward the totals.
        let full = include_str!("search.rs");
        let cut = full.find("#[cfg(test)]")
            .expect("search.rs must have a #[cfg(test)] module marker");
        let production_src = &full[..cut];
        let total = production_src.matches("to_tsvector(").count();
        let casted = production_src.matches("to_tsvector('english'::regconfig").count();
        assert!(total > 0, "expected at least one to_tsvector call in production code");
        assert_eq!(
            total, casted,
            "every to_tsvector must use 'english'::regconfig — got {casted} casted out of {total} total \
             (a plain 'english' literal makes the expression STABLE, so the partial GIN indexes from \
             migration 107 won't match and search falls back to Seq Scan)"
        );
    }

    #[test]
    fn search_result_serialises_camelcase() {
        let r = SearchResult {
            entity_type: "Device".into(),
            id: Uuid::nil(),
            label: "MEP-91-CORE02".into(),
            rank: 0.23,
            snippet: "snippet here".into(),
        };
        let json = serde_json::to_string(&r).expect("serialises");
        assert!(json.contains("\"entityType\":\"Device\""));
        assert!(json.contains("\"label\":\"MEP-91-CORE02\""));
        assert!(json.contains("\"rank\":"));
        assert!(json.contains("\"snippet\":\"snippet here\""));
    }
}
