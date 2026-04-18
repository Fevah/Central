-- =============================================================================
-- 105 — Networking engine Phase 10: scope grants (RBAC foundation)
--
-- The `net.*` world today is single-role-per-tenant: any admin can touch any
-- entity. This migration introduces a tuple-based grant model
-- `(user, action, entity_type, scope_type, scope_entity_id)` so an operator
-- can express things like:
--
--   "Alice can read every Device in Region=EU"
--   "Bob can write to Vlan=IT.120 (that specific VLAN)"
--   "Charlie can approve every ChangeSet globally"
--
-- The grants land here; the `has_permission` resolver + per-endpoint
-- enforcement land in follow-on slices (this migration ships the engine as
-- AVAILABLE but NOT-YET-BLOCKING, so existing admin-any-access stays working
-- while clients opt in to enforcement one surface at a time).
--
-- ## Scope semantics (v1)
--
--   Global           → the grant applies to every entity of `entity_type`
--                      (scope_entity_id must be NULL)
--   Region/Site/
--   Building         → applies to entities located in / under that container
--                      (scope_entity_id points at the container id; resolver
--                       expands via the hierarchy joins in a follow-on slice)
--   EntityId         → applies to exactly one row (scope_entity_id points at
--                      the entity itself)
--
-- Hierarchical expansion (Region→Sites→Buildings→Devices) is NOT wired in
-- this migration — the first resolver slice handles Global + EntityId only.
-- The schema carries the wider scope_type enum so hierarchy resolution can
-- land later without another migration.
--
-- ## Why "scope grant" not "role"
--
-- "Role" would imply a named bundle of permissions (what the existing
-- public.roles table does for the .NET platform surface). For the networking
-- engine we deliberately want grants at the finest resolution — per user,
-- per (action, entity_type, scope) tuple — so admins can delegate specific
-- authority without inventing a named role for every combination. Named
-- bundles can be built on top later if the customer asks for them.
-- =============================================================================

CREATE TABLE IF NOT EXISTS net.scope_grant (
    id                uuid               PRIMARY KEY DEFAULT gen_random_uuid(),
    organization_id   uuid               NOT NULL REFERENCES central_platform.tenants(id) ON DELETE CASCADE,

    -- Who the grant is for. `user_id` references app_users.id (int)
    -- rather than a UUID because the .NET auth layer hasn't been
    -- ported; keeping the same column type means cross-ref queries
    -- stay JOIN-trivial between the two worlds during the transition.
    user_id           int                NOT NULL,

    -- The tuple.
    action            varchar(32)        NOT NULL,
        -- 'read' / 'write' / 'delete' / 'approve' / 'apply'
    entity_type       varchar(64)        NOT NULL,
        -- 'Device' / 'Vlan' / 'Subnet' / 'Link' / 'Server' /
        -- 'DhcpRelayTarget' / 'ChangeSet' / 'Building' / 'Site' /
        -- 'Region' etc. Intentionally a free-form varchar so adding
        -- new entity types doesn't require a schema change.
    scope_type        varchar(16)        NOT NULL,
        -- 'Global' / 'Region' / 'Site' / 'Building' / 'EntityId'
    scope_entity_id   uuid,
        -- NULL for Global; required for the rest. Not FK'd because
        -- the target table depends on scope_type + entity_type —
        -- the resolver enforces validity at query time.

    -- Standard 17-column universal base.
    status            net.entity_status  NOT NULL DEFAULT 'Active',
    lock_state        net.lock_state     NOT NULL DEFAULT 'Open',
    lock_reason       text,
    locked_by         int,
    locked_at         timestamptz,
    created_at        timestamptz        NOT NULL DEFAULT now(),
    created_by        int,
    updated_at        timestamptz        NOT NULL DEFAULT now(),
    updated_by        int,
    deleted_at        timestamptz,
    deleted_by        int,
    notes             text,
    tags              jsonb              NOT NULL DEFAULT '{}'::jsonb,
    external_refs     jsonb              NOT NULL DEFAULT '[]'::jsonb,
    version           int                NOT NULL DEFAULT 1,

    -- Integrity checks. Action + scope_type pinned to the v1 enums
    -- so misspellings land as a clear 400 at insert rather than a
    -- silently-never-matching grant at resolver time.
    CHECK (action      IN ('read','write','delete','approve','apply')),
    CHECK (scope_type  IN ('Global','Region','Site','Building','EntityId')),
    -- Global grants must NOT carry a scope_entity_id; all others
    -- MUST. Keeps the resolver logic simple (null-check scope_type,
    -- no "Global with rogue scope_entity_id" ambiguity).
    CHECK (
        (scope_type = 'Global'  AND scope_entity_id IS NULL) OR
        (scope_type <> 'Global' AND scope_entity_id IS NOT NULL)
    ),
    -- Dedup: two grants identical on the tuple are collapsible to
    -- one. Partial-unique on deleted_at IS NULL so re-grants after
    -- revoke work without a cleanup step.
    UNIQUE (organization_id, user_id, action, entity_type, scope_type, scope_entity_id)
);

-- Resolver hot path: given (org, user_id, action, entity_type),
-- find every grant that applies. Index tuned to that shape.
CREATE INDEX IF NOT EXISTS ix_scope_grant_resolver
    ON net.scope_grant (organization_id, user_id, action, entity_type)
    WHERE deleted_at IS NULL;

-- EntityId lookups go through the tail of the index above but some
-- read-paths (e.g. "list everyone granted on this specific device")
-- need scope_entity_id as the lead column — covered here.
CREATE INDEX IF NOT EXISTS ix_scope_grant_scope_entity
    ON net.scope_grant (organization_id, scope_entity_id)
    WHERE deleted_at IS NULL AND scope_entity_id IS NOT NULL;

COMMENT ON TABLE net.scope_grant IS
    'Tuple-based RBAC grants for the networking engine. (user, action, entity_type, scope_type, scope_entity_id) — resolver allows the action when ANY non-deleted grant matches. Phase 10.';
