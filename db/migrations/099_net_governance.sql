-- =============================================================================
-- 099 — Networking governance foundation (Phase 8a).
--
-- Adds the change_set + items + approvals tables the governance layer needs.
-- This migration ships only the storage contract — the state-machine
-- enforcement + approver-count policy logic lives in
-- services/networking-engine/ and arrives incrementally across Phase 8b-f.
--
-- A Change Set is an admin's unit of intent: "I want to rename 47 devices /
-- retire 3 ASN allocations / create a new MLAG domain and link". Each
-- discrete mutation is one change_set_item. The Set moves through a
-- lifecycle from Draft -> Submitted -> (Approved | Rejected) -> (Applied |
-- Cancelled | Rolled_Back). The Rust engine writes actual entity mutations
-- only when Set.status hits Applied, so approvals enforce policy before
-- any visible change lands.
--
-- Correlation with audit: change_set_id feeds every audit entry's
-- correlation_id during that Set's apply phase, so forensic queries can
-- join "show me every row touched by Change Set X".
--
-- Idempotent; safe to re-run.
-- =============================================================================

BEGIN;

-- ─── Enums ───────────────────────────────────────────────────────────────

DO $$ BEGIN
    CREATE TYPE net.change_set_status AS ENUM (
        'Draft',         -- editable by creator, no visibility outside tenant admin
        'Submitted',     -- queued for approval, no further item changes
        'Approved',      -- cleared for apply, still reversible via Cancel
        'Rejected',      -- terminal, never applied
        'Applied',       -- entity mutations landed, audit rows stamped
        'RolledBack',    -- previously applied, then reverted
        'Cancelled'      -- terminal, withdrawn before apply
    );
EXCEPTION WHEN duplicate_object THEN NULL;
END $$;

DO $$ BEGIN
    CREATE TYPE net.change_set_action AS ENUM (
        'Create',
        'Update',
        'Delete',
        'Rename'         -- special-cased to thread regenerate-names through here
    );
EXCEPTION WHEN duplicate_object THEN NULL;
END $$;

DO $$ BEGIN
    CREATE TYPE net.change_set_decision AS ENUM (
        'Approve',
        'Reject'
    );
EXCEPTION WHEN duplicate_object THEN NULL;
END $$;

-- ─── net.change_set ──────────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS net.change_set (
    id                    uuid                    PRIMARY KEY DEFAULT gen_random_uuid(),
    organization_id       uuid                    NOT NULL REFERENCES central_platform.tenants(id) ON DELETE CASCADE,

    title                 varchar(128)            NOT NULL,
    description           text,

    -- Lifecycle. The Rust engine is the only writer that may transition
    -- this column; direct UPDATE by anyone else risks bypassing policy.
    status                net.change_set_status   NOT NULL DEFAULT 'Draft',

    -- Who's responsible. requested_by is the user who drafted the Set;
    -- submitted_by can be a different admin if Draft ownership is handed off.
    requested_by          int,
    requested_by_display  varchar(128),
    submitted_by          int,
    submitted_at          timestamptz,
    approved_at           timestamptz,
    applied_at            timestamptz,
    rolled_back_at        timestamptz,
    cancelled_at          timestamptz,

    -- How many approvers this Set needs. Set at submit time from the
    -- tenant's approval policy; NULL until Submitted so Draft Sets don't
    -- pretend to have a policy baked in yet.
    required_approvals    int,

    -- Stamped on apply so audit queries can join back.
    correlation_id        uuid                    NOT NULL DEFAULT gen_random_uuid(),

    -- Universal entity base.
    lock_state            net.lock_state          NOT NULL DEFAULT 'Open',
    lock_reason           text,
    locked_by             int,
    locked_at             timestamptz,
    created_at            timestamptz             NOT NULL DEFAULT now(),
    created_by            int,
    updated_at            timestamptz             NOT NULL DEFAULT now(),
    updated_by            int,
    deleted_at            timestamptz,
    deleted_by            int,
    notes                 text,
    tags                  jsonb                   NOT NULL DEFAULT '{}'::jsonb,
    external_refs         jsonb                   NOT NULL DEFAULT '[]'::jsonb,
    version               int                     NOT NULL DEFAULT 1
);

CREATE INDEX IF NOT EXISTS ix_change_set_tenant_status
    ON net.change_set (organization_id, status, created_at DESC)
    WHERE deleted_at IS NULL;

CREATE INDEX IF NOT EXISTS ix_change_set_correlation
    ON net.change_set (correlation_id);

COMMENT ON TABLE net.change_set IS
    'Unit of admin intent. Groups related entity mutations behind one '
    'approvable envelope. Status transitions are driven by the engine; '
    'direct writes bypass policy enforcement.';

-- ─── net.change_set_item ─────────────────────────────────────────────────
CREATE TABLE IF NOT EXISTS net.change_set_item (
    id                    uuid                    PRIMARY KEY DEFAULT gen_random_uuid(),
    organization_id       uuid                    NOT NULL REFERENCES central_platform.tenants(id) ON DELETE CASCADE,
    change_set_id         uuid                    NOT NULL REFERENCES net.change_set(id) ON DELETE CASCADE,

    -- Stable ordering within a Set — so "apply in order" is deterministic.
    item_order            int                     NOT NULL,

    -- What entity and what action. Same string convention as the audit
    -- engine (Device / Link / Server / Subnet / Allocation / ...).
    entity_type           varchar(64)             NOT NULL,
    entity_id             uuid,                       -- NULL for Create
    action                net.change_set_action   NOT NULL,

    -- Before / after JSONB snapshots. `before_json` is NULL for Create,
    -- `after_json` is NULL for Delete. For Rename + Update both carry
    -- the relevant fields so the apply step can diff + pick.
    before_json           jsonb,
    after_json            jsonb,

    -- Optimistic-concurrency tag the apply step checks against the live
    -- row. If the entity's current version doesn't match, the item is
    -- rejected rather than overwriting concurrent changes.
    expected_version      int,

    -- Per-item apply telemetry, populated when the Set is Applied.
    applied_at            timestamptz,
    apply_error           text,

    -- Universal base (simplified — item rows inherit lifecycle from their
    -- parent Set, so no status/lock columns here).
    created_at            timestamptz             NOT NULL DEFAULT now(),
    updated_at            timestamptz             NOT NULL DEFAULT now(),
    deleted_at            timestamptz,
    notes                 text,
    tags                  jsonb                   NOT NULL DEFAULT '{}'::jsonb,
    version               int                     NOT NULL DEFAULT 1,

    UNIQUE (change_set_id, item_order)
);

CREATE INDEX IF NOT EXISTS ix_change_set_item_set
    ON net.change_set_item (change_set_id, item_order)
    WHERE deleted_at IS NULL;

CREATE INDEX IF NOT EXISTS ix_change_set_item_entity
    ON net.change_set_item (organization_id, entity_type, entity_id)
    WHERE deleted_at IS NULL;

COMMENT ON TABLE net.change_set_item IS
    'A single mutation within a Change Set. item_order is stable — apply '
    'walks items in ascending order so cross-item dependencies resolve '
    'predictably (e.g. rename the parent first, then children).';

-- ─── net.change_set_approval ─────────────────────────────────────────────
-- One row per approver decision. Multiple approvers per Set are supported
-- via the required_approvals counter on change_set; the engine sums
-- Approve decisions and compares to the threshold.
CREATE TABLE IF NOT EXISTS net.change_set_approval (
    id                    uuid                    PRIMARY KEY DEFAULT gen_random_uuid(),
    organization_id       uuid                    NOT NULL REFERENCES central_platform.tenants(id) ON DELETE CASCADE,
    change_set_id         uuid                    NOT NULL REFERENCES net.change_set(id) ON DELETE CASCADE,

    approver_user_id      int                     NOT NULL,
    approver_display      varchar(128),
    decision              net.change_set_decision NOT NULL,
    decided_at            timestamptz             NOT NULL DEFAULT now(),
    notes                 text,

    -- One decision per (Set, approver) — flipping requires a new Set.
    UNIQUE (change_set_id, approver_user_id)
);

CREATE INDEX IF NOT EXISTS ix_change_set_approval_set
    ON net.change_set_approval (change_set_id, decided_at DESC);

COMMENT ON TABLE net.change_set_approval IS
    'Approver decisions on a Change Set. Once submitted, a Set needs at '
    'least required_approvals rows with decision = Approve before it can '
    'move to Approved. Any single Reject flips it to Rejected (terminal).';

-- ─── schema_versions ─────────────────────────────────────────────────────
INSERT INTO public.schema_versions (version, description)
VALUES (99, 'Networking governance (Phase 8a): change_set + items + approvals')
ON CONFLICT (version) DO NOTHING;

COMMIT;
