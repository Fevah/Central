-- =============================================================================
-- 101 — Validation rule configuration (Phase 9a).
--
-- The rule *catalog* lives in code (services/networking-engine/src/validation.rs)
-- so the engine can never run a rule it doesn't have an executor for. This
-- migration only stores the per-tenant config: which rules are enabled,
-- and whether their default severity has been overridden.
--
-- Why not store the rule SQL in the DB: giving admins an edit box that
-- becomes a SELECT at runtime is an SQL-injection shape no matter how
-- carefully it's validated. Rules are code-owned; admins toggle + see
-- results.
--
-- Idempotent; safe to re-run.
-- =============================================================================

BEGIN;

CREATE TABLE IF NOT EXISTS net.tenant_rule_config (
    organization_id       uuid             NOT NULL REFERENCES central_platform.tenants(id) ON DELETE CASCADE,

    -- Matches RULES[*].code in validation.rs. No FK — the catalog is
    -- code-owned, and a rule being retired from code shouldn't orphan
    -- existing tenant rows (they just stop affecting anything, which is
    -- the correct behaviour).
    rule_code             varchar(64)      NOT NULL,

    -- NULL / true / false — with NULL meaning "use the rule's default
    -- enabled state". Allows a global toggle at code level ("this rule is
    -- opt-in by default") without every existing tenant needing an
    -- explicit override.
    enabled               boolean,

    -- Either NULL ("use default") or one of Error / Warning / Info.
    -- Promoting Info to Error lets a tenant treat certain advisories as
    -- blockers for their own ops workflow without forking the catalog.
    severity_override     varchar(16),

    -- Universal base — single-row-per-(tenant, rule_code) so audit columns
    -- track who last changed the config.
    created_at            timestamptz      NOT NULL DEFAULT now(),
    created_by            int,
    updated_at            timestamptz      NOT NULL DEFAULT now(),
    updated_by            int,
    notes                 text,
    version               int              NOT NULL DEFAULT 1,

    PRIMARY KEY (organization_id, rule_code),
    CHECK (severity_override IS NULL OR severity_override IN ('Error','Warning','Info'))
);

CREATE INDEX IF NOT EXISTS ix_tenant_rule_config_enabled
    ON net.tenant_rule_config (organization_id)
    WHERE enabled = false;

COMMENT ON TABLE net.tenant_rule_config IS
    'Per-tenant toggle + severity override for code-owned validation rules. '
    'NULL columns mean "use the rule''s default". The rule catalog itself '
    'lives in services/networking-engine/src/validation.rs.';

INSERT INTO public.schema_versions (version_number, description)
VALUES (101, 'Networking Phase 9a: validation rule per-tenant configuration')
ON CONFLICT (version_number) DO NOTHING;

COMMIT;
