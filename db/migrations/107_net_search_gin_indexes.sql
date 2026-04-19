-- =============================================================================
-- 107 — Networking engine Phase 10: GIN indexes for global search
--
-- Backs the dynamic-UNION search shipped in `services/networking-engine/
-- src/search.rs` with proper GIN indexes on each entity's tsvector
-- expression. Without these, every `/api/net/search?q=...` runs as a
-- six-way Seq Scan; small tenants don't notice, but at the
-- ~10k-rows-per-entity cliff (~60k rows total in the UNION) the cost
-- climbs into hundreds of milliseconds and competes with CRUD writes
-- for shared buffer space.
--
-- ## Why partial (`WHERE deleted_at IS NULL`)
--
-- The search WHERE clause filters out soft-deleted rows. Building the
-- index as a partial index on the same predicate keeps the index
-- smaller (no churn from deleted rows) and lets PG's planner pick it
-- up cleanly — a partial index can only be used when the query's
-- WHERE clause is provably implied by the index predicate.
--
-- ## Why ::regconfig
--
-- `to_tsvector('english', ...)` (the text-overload variant) is STABLE
-- because it looks up the configuration by name at query time —
-- search_path-dependent. STABLE expressions can NOT be used as the
-- key of a functional index. The `regconfig` overload —
-- `to_tsvector('english'::regconfig, ...)` — is IMMUTABLE because the
-- config is resolved once and frozen.
--
-- Migration 107 + the corresponding search.rs edit (this same commit)
-- both flip to ::regconfig. The expressions in the index and the
-- query must be byte-for-byte the same for the planner to use the
-- index — that's why the indexes here look so verbose: they have to
-- mirror exactly what search.rs emits.
--
-- ## Why CONCURRENTLY isn't used
--
-- `CREATE INDEX CONCURRENTLY` can't run inside a transaction; our
-- migration runner wraps each migration in a tx. For local + dev DBs
-- the brief lock is fine. Prod operators rolling this forward on a
-- large estate should pre-create these indexes manually with
-- CONCURRENTLY before applying the migration so the migration's
-- `CREATE INDEX IF NOT EXISTS` is a no-op.
-- =============================================================================

-- Device — hostname / device_code / notes
CREATE INDEX IF NOT EXISTS ix_device_search_gin
    ON net.device
    USING GIN (
        to_tsvector('english'::regconfig,
            coalesce(hostname,'') || ' ' ||
            coalesce(device_code,'') || ' ' ||
            coalesce(notes,'')))
    WHERE deleted_at IS NULL;

-- Vlan — display_name / description / notes
CREATE INDEX IF NOT EXISTS ix_vlan_search_gin
    ON net.vlan
    USING GIN (
        to_tsvector('english'::regconfig,
            coalesce(display_name,'') || ' ' ||
            coalesce(description,'') || ' ' ||
            coalesce(notes,'')))
    WHERE deleted_at IS NULL;

-- Subnet — subnet_code / display_name / notes
CREATE INDEX IF NOT EXISTS ix_subnet_search_gin
    ON net.subnet
    USING GIN (
        to_tsvector('english'::regconfig,
            coalesce(subnet_code,'') || ' ' ||
            coalesce(display_name,'') || ' ' ||
            coalesce(notes,'')))
    WHERE deleted_at IS NULL;

-- Server — hostname / display_name / notes
CREATE INDEX IF NOT EXISTS ix_server_search_gin
    ON net.server
    USING GIN (
        to_tsvector('english'::regconfig,
            coalesce(hostname,'') || ' ' ||
            coalesce(display_name,'') || ' ' ||
            coalesce(notes,'')))
    WHERE deleted_at IS NULL;

-- Link — link_code / display_name / description / notes
CREATE INDEX IF NOT EXISTS ix_link_search_gin
    ON net.link
    USING GIN (
        to_tsvector('english'::regconfig,
            coalesce(link_code,'') || ' ' ||
            coalesce(display_name,'') || ' ' ||
            coalesce(description,'') || ' ' ||
            coalesce(notes,'')))
    WHERE deleted_at IS NULL;

-- DhcpRelayTarget — host(server_ip) / notes
CREATE INDEX IF NOT EXISTS ix_dhcp_relay_search_gin
    ON net.dhcp_relay_target
    USING GIN (
        to_tsvector('english'::regconfig,
            coalesce(host(server_ip),'') || ' ' ||
            coalesce(notes,'')))
    WHERE deleted_at IS NULL;
