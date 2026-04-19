-- =============================================================================
-- 097 — Networking Phase 7a: naming template overrides + tenant naming config.
--
-- Adds the per-tenant scope-resolution override table so a specific building
-- (or site, or region) can use a different hostname template than the default
-- seeded on the *-type catalog tables (link_type.naming_template,
-- device_role.naming_template, server_profile.naming_template).
--
-- Resolution order, most-specific-wins:
--   1. Building-scoped, specific subtype
--   2. Building-scoped, any subtype (subtype_code IS NULL)
--   3. Site-scoped,     specific subtype
--   4. Site-scoped,     any subtype
--   5. Region-scoped,   specific subtype
--   6. Region-scoped,   any subtype
--   7. Global (scope_level = 'Global'), specific subtype
--   8. Global, any subtype
--   9. Default on the *-type row (kept as the canonical fallback)
--
-- The resolver lives in services/networking-engine/ (Rust). This migration
-- owns only the storage contract.
--
-- Idempotent; safe to re-run.
-- =============================================================================

BEGIN;

-- ─── net.naming_template_override ────────────────────────────────────────
-- One row per (tenant, entity_type, subtype, scope) combination. Together
-- with the *-type default, these build the resolution list the engine walks.
CREATE TABLE IF NOT EXISTS net.naming_template_override (
    id                    uuid              PRIMARY KEY DEFAULT gen_random_uuid(),
    organization_id       uuid              NOT NULL REFERENCES central_platform.tenants(id) ON DELETE CASCADE,

    -- What kind of entity this template applies to. Matches the Rust
    -- resolver's `entity_type` string — Link / Device / Server today,
    -- extensible to anything that grows a naming_template later.
    entity_type           varchar(32)       NOT NULL,

    -- Optional discriminator inside the entity_type. For Device this is
    -- the `role_code` (Core / L1Core / Access / etc.); for Link it's
    -- `type_code`; for Server it's `profile_code`. NULL means the
    -- override applies to any subtype at this scope — useful for a
    -- site-wide rename rule that doesn't care which role.
    subtype_code          varchar(64),

    scope_level           varchar(16)       NOT NULL,     -- Global / Region / Site / Building
    scope_entity_id       uuid,                            -- NULL iff scope_level = 'Global'

    naming_template       text              NOT NULL,

    -- Universal entity base (same shape as every other net.* table).
    status                net.entity_status NOT NULL DEFAULT 'Active',
    lock_state            net.lock_state    NOT NULL DEFAULT 'Open',
    lock_reason           text,
    locked_by             int,
    locked_at             timestamptz,
    created_at            timestamptz       NOT NULL DEFAULT now(),
    created_by            int,
    updated_at            timestamptz       NOT NULL DEFAULT now(),
    updated_by            int,
    deleted_at            timestamptz,
    deleted_by            int,
    notes                 text,
    tags                  jsonb             NOT NULL DEFAULT '{}'::jsonb,
    external_refs         jsonb             NOT NULL DEFAULT '[]'::jsonb,
    version               int               NOT NULL DEFAULT 1,

    CHECK (scope_level IN ('Global','Region','Site','Building')),
    CHECK (
        (scope_level = 'Global' AND scope_entity_id IS NULL)
     OR (scope_level <> 'Global' AND scope_entity_id IS NOT NULL)
    )
);

-- Uniqueness: one template per (tenant, entity_type, subtype_code, scope, scope_entity_id).
-- Enforced as two partial indexes because PG's UNIQUE NULLS NOT DISTINCT
-- is 15+ only and we want a tight contract on both the "any subtype"
-- (subtype_code NULL) and "specific subtype" rows.
CREATE UNIQUE INDEX IF NOT EXISTS ux_naming_override_scoped
    ON net.naming_template_override
       (organization_id, entity_type, subtype_code, scope_level, scope_entity_id)
 WHERE deleted_at IS NULL AND subtype_code IS NOT NULL;

CREATE UNIQUE INDEX IF NOT EXISTS ux_naming_override_any_subtype
    ON net.naming_template_override
       (organization_id, entity_type, scope_level, scope_entity_id)
 WHERE deleted_at IS NULL AND subtype_code IS NULL;

-- Lookup index — the resolver queries by (tenant, entity_type) and walks
-- scope_level in (Building, Site, Region, Global) order in memory.
CREATE INDEX IF NOT EXISTS ix_naming_override_lookup
    ON net.naming_template_override (organization_id, entity_type, scope_level)
 WHERE deleted_at IS NULL;

COMMENT ON TABLE net.naming_template_override IS
    'Per-tenant scope-resolution overrides for naming templates. The '
    'resolver walks Building -> Site -> Region -> Global, preferring '
    'specific subtype rows over any-subtype (NULL subtype_code) rows at '
    'each scope. Falls back to the *-type default when nothing matches.';

-- ─── net.tenant_naming_config ────────────────────────────────────────────
-- Tenant-wide naming preferences. Currently just default_separator — the
-- character seeded into *newly-created* templates. Existing templates are
-- NOT rewritten when this flag changes; admins rename through the override
-- table if they want a retroactive change.
CREATE TABLE IF NOT EXISTS net.tenant_naming_config (
    organization_id       uuid              PRIMARY KEY REFERENCES central_platform.tenants(id) ON DELETE CASCADE,
    default_separator     varchar(2)        NOT NULL DEFAULT '-',
    applied_to_new        boolean           NOT NULL DEFAULT true,

    -- Universal base (single-row-per-tenant so version/audit still useful).
    status                net.entity_status NOT NULL DEFAULT 'Active',
    lock_state            net.lock_state    NOT NULL DEFAULT 'Open',
    lock_reason           text,
    locked_by             int,
    locked_at             timestamptz,
    created_at            timestamptz       NOT NULL DEFAULT now(),
    created_by            int,
    updated_at            timestamptz       NOT NULL DEFAULT now(),
    updated_by            int,
    deleted_at            timestamptz,
    deleted_by            int,
    notes                 text,
    tags                  jsonb             NOT NULL DEFAULT '{}'::jsonb,
    external_refs         jsonb             NOT NULL DEFAULT '[]'::jsonb,
    version               int               NOT NULL DEFAULT 1,

    CHECK (default_separator IN ('-','_','.'))
);

COMMENT ON TABLE net.tenant_naming_config IS
    'Per-tenant naming preferences. default_separator is seeded into '
    'newly-created templates only — existing templates stay untouched.';

-- ─── schema_versions record ──────────────────────────────────────────────
INSERT INTO public.schema_versions (version_number, description)
VALUES (97, 'Networking Phase 7a: naming template overrides + tenant naming config')
ON CONFLICT (version_number) DO NOTHING;

COMMIT;
