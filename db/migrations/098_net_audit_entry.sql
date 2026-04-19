-- =============================================================================
-- 098 — Networking audit foundation. Groundwork for:
--   * Phase 7c apply (rename + audit entry per changed entity)
--   * Phase 8 governance (Change Set submit/approve/apply stamps)
--   * Phase 9 validation + audit (the formal hash-chained contract)
--
-- Creates `net.audit_entry` as a tamper-evident, per-tenant append-only log.
-- Differs from the pre-multi-tenant `public.audit_log` by:
--   1. organization_id column + per-tenant hash chain (each tenant has its
--      own independent sequence, so tampering inside one tenant's history
--      can't propagate to another's).
--   2. source_service discriminator (networking-engine today, others later)
--      so cross-service audit entries land in the same table without
--      collision.
--   3. entry_hash + prev_hash columns — SHA-256 chain over the entry
--      content. Mutating any past row invalidates every subsequent hash.
--
-- Idempotent; safe to re-run.
-- =============================================================================

BEGIN;

CREATE TABLE IF NOT EXISTS net.audit_entry (
    id                uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
    organization_id   uuid        NOT NULL REFERENCES central_platform.tenants(id) ON DELETE CASCADE,

    -- Monotonic per-tenant sequence. The hash chain follows this order —
    -- the chain for tenant X at sequence N hashes over the entry at N-1.
    -- sequence_id is not globally unique (only unique within a tenant).
    sequence_id       bigint      NOT NULL,

    -- Which service wrote this row. Lets operators slice the history by
    -- origin ("what did networking-engine do last hour?") and means
    -- future services can add their own audit writes here without a
    -- migration.
    source_service    varchar(32) NOT NULL,

    -- What was acted upon. entity_type is free-text so new entity kinds
    -- don't force a migration; canonical values today are the same ones
    -- the naming resolver uses: Device / Link / Server, plus the
    -- operational ones NamingOverride / Allocation / Subnet.
    entity_type       varchar(64) NOT NULL,
    entity_id         uuid,

    -- What happened. Verb-style: Created / Updated / Deleted / Retired /
    -- Allocated / Approved / Rejected / Renamed / etc.
    action            varchar(32) NOT NULL,

    actor_user_id     int,
    actor_display     varchar(128),
    client_ip         inet,
    correlation_id    uuid,           -- set to change_set_id once Phase 8 lands

    -- Structured details — before/after JSON, reason string, probe
    -- output, whatever the caller wants to leave for forensics. Kept as
    -- jsonb so queries can index specific keys cheaply.
    details           jsonb       NOT NULL DEFAULT '{}'::jsonb,

    -- Hash chain. prev_hash is the previous row's entry_hash in this
    -- tenant's sequence; NULL for the first entry. entry_hash is
    -- SHA-256 of a canonical byte stream over the row's semantic
    -- content + prev_hash. Storing as varchar(64) because lowercase
    -- hex is easier to inspect than raw bytes.
    prev_hash         varchar(64),
    entry_hash        varchar(64) NOT NULL,

    created_at        timestamptz NOT NULL DEFAULT now(),

    UNIQUE (organization_id, sequence_id)
);

-- The chain-forward query pattern: "give me tenant T's last entry so I
-- can compute the next prev_hash". Ordered descending by sequence so a
-- LIMIT 1 is cheap.
CREATE INDEX IF NOT EXISTS ix_audit_entry_tenant_seq
    ON net.audit_entry (organization_id, sequence_id DESC);

-- Entity-centric lookup: "show me every audit row for this device".
CREATE INDEX IF NOT EXISTS ix_audit_entry_entity
    ON net.audit_entry (organization_id, entity_type, entity_id, sequence_id DESC);

-- Service-slice dashboards.
CREATE INDEX IF NOT EXISTS ix_audit_entry_service
    ON net.audit_entry (organization_id, source_service, sequence_id DESC);

-- GIN index on details so jsonb-key filters on common forensic queries
-- stay sub-second.
CREATE INDEX IF NOT EXISTS ix_audit_entry_details_gin
    ON net.audit_entry USING gin (details jsonb_path_ops);

COMMENT ON TABLE net.audit_entry IS
    'Per-tenant append-only audit log with SHA-256 hash chaining. Each '
    'entry references the previous entry''s hash via prev_hash; modifying '
    'any past row invalidates every subsequent entry_hash. First entry '
    'per tenant has prev_hash IS NULL. The append path in '
    'services/networking-engine/ enforces ordering via pg_advisory_xact_lock '
    'keyed on the tenant id.';

INSERT INTO public.schema_versions (version_number, description)
VALUES (98, 'Networking audit: net.audit_entry hash-chained append-only log')
ON CONFLICT (version_number) DO NOTHING;

COMMIT;
