-- =============================================================================
-- 102 — Networking Phase 10: CLI flavor toggle + rendered config storage.
--
-- The customer (Immunocore) runs FS N-series on PicOS 4.6; other
-- prospective tenants run Cisco NX-OS / Arista EOS / Juniper Junos /
-- FRRouting. The engine needs to support per-device-role CLI flavors
-- with per-tenant enable/disable and a place to store rendered config
-- so diffs vs the last deploy are cheap.
--
-- The flavor catalog lives in code (services/networking-engine/src/
-- cli_flavor.rs) — same pattern as the validation rule catalog. This
-- migration only stores the per-tenant enable/default and a
-- generated_config history table.
--
-- Idempotent; safe to re-run.
-- =============================================================================

BEGIN;

-- ─── Per-tenant enable / default-flavor state ────────────────────────────
CREATE TABLE IF NOT EXISTS net.tenant_cli_flavor (
    organization_id   uuid        NOT NULL REFERENCES central_platform.tenants(id) ON DELETE CASCADE,

    -- Matches CliFlavor::code in cli_flavor.rs. No FK — catalog is
    -- code-owned; a flavor retired in code simply leaves stale rows
    -- that the resolver ignores (same contract as tenant_rule_config).
    flavor_code       varchar(32) NOT NULL,

    -- Per-tenant toggle. NULL = use the flavor's default_enabled from
    -- the catalog. Explicit true/false overrides.
    enabled           boolean,

    -- Tenant's default flavor — the one chosen when a device's role
    -- doesn't specify one. Only one row per tenant may have
    -- is_default = true; enforced via partial unique index below.
    is_default        boolean     NOT NULL DEFAULT false,

    -- Universal base (simplified — one row per tenant per flavor).
    created_at        timestamptz NOT NULL DEFAULT now(),
    created_by        int,
    updated_at        timestamptz NOT NULL DEFAULT now(),
    updated_by        int,
    notes             text,
    version           int         NOT NULL DEFAULT 1,

    PRIMARY KEY (organization_id, flavor_code)
);

-- One default per tenant. NULL + NULL duplicates allowed — only the
-- `is_default = true` rows are constrained.
CREATE UNIQUE INDEX IF NOT EXISTS ux_tenant_cli_flavor_single_default
    ON net.tenant_cli_flavor (organization_id)
    WHERE is_default = true;

COMMENT ON TABLE net.tenant_cli_flavor IS
    'Per-tenant enable/default state for CLI flavors. Flavor catalog '
    'itself lives in services/networking-engine/src/cli_flavor.rs — '
    'NULL enabled means "use catalog default".';

-- ─── Rendered config history ─────────────────────────────────────────────
-- Stores the output of each render so the engine can:
--   1. Diff the latest render vs the previous one to know what changed
--   2. Serve the most recent render without re-running the generator
--   3. Track which flavor emitted each render
CREATE TABLE IF NOT EXISTS net.rendered_config (
    id                uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
    organization_id   uuid        NOT NULL REFERENCES central_platform.tenants(id) ON DELETE CASCADE,
    device_id         uuid        NOT NULL REFERENCES net.device(id) ON DELETE CASCADE,

    flavor_code       varchar(32) NOT NULL,
    body              text        NOT NULL,
    body_sha256       varchar(64) NOT NULL,    -- hex; cheap diff-detection
    line_count        int         NOT NULL,
    render_duration_ms int,

    -- Chain to the previous render for this device+flavor, if any. Lets
    -- "what changed since last render" be a two-row join rather than a
    -- full text diff every time.
    previous_render_id uuid       REFERENCES net.rendered_config(id) ON DELETE SET NULL,

    rendered_at       timestamptz NOT NULL DEFAULT now(),
    rendered_by       int,

    -- Soft-delete for GDPR / audit hygiene.
    deleted_at        timestamptz,
    deleted_by        int
);

CREATE INDEX IF NOT EXISTS ix_rendered_config_device
    ON net.rendered_config (organization_id, device_id, rendered_at DESC)
    WHERE deleted_at IS NULL;

CREATE INDEX IF NOT EXISTS ix_rendered_config_flavor
    ON net.rendered_config (organization_id, flavor_code, rendered_at DESC)
    WHERE deleted_at IS NULL;

COMMENT ON TABLE net.rendered_config IS
    'History of every device config render. Each row carries the full '
    'body, its sha256 for O(1) change detection, the flavor that emitted '
    'it, and a chain pointer to the previous render for diff.';

INSERT INTO public.schema_versions (version_number, description)
VALUES (102, 'Networking Phase 10: tenant_cli_flavor + rendered_config')
ON CONFLICT (version_number) DO NOTHING;

COMMIT;
