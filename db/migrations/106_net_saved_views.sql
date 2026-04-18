-- =============================================================================
-- 106 — Networking engine Phase 10: saved views
--
-- Companion primitive to global search (`/api/net/search`). An operator
-- tweaks their query + entity-type filter + ad-hoc facets into something
-- useful ("Retired devices in MEP-91") and saves it as a named view;
-- later they pull it from a sidebar dropdown instead of reconstructing
-- the filter from memory.
--
-- Per-user. `UNIQUE (org, user_id, name)` makes names unique within a
-- user's own namespace — two different operators can both have a view
-- called "My critical devices" without collision.
--
-- `filters_jsonb` carries arbitrary facet state so future UI additions
-- (status facet, scope_level facet, tag facet) don't need schema
-- changes — the jsonb is structured by the client and read back as-is
-- when the view is restored.
-- =============================================================================

CREATE TABLE IF NOT EXISTS net.saved_view (
    id                uuid               PRIMARY KEY DEFAULT gen_random_uuid(),
    organization_id   uuid               NOT NULL REFERENCES central_platform.tenants(id) ON DELETE CASCADE,

    -- Who owns the view. app_users.id (int) because the .NET auth
    -- layer hasn't been ported; same shape as net.scope_grant.user_id.
    user_id           int                NOT NULL,
    name              varchar(128)       NOT NULL,

    -- The actual saved query. `q` is the free-text search; comma-
    -- separated entity_type filter matches the /search endpoint's
    -- `entityTypes` parameter; filters jsonb carries future facets
    -- without schema churn.
    q                 text               NOT NULL DEFAULT '',
    entity_types      varchar(256),       -- e.g. 'Device,Vlan' or NULL = all
    filters           jsonb              NOT NULL DEFAULT '{}'::jsonb,

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

    -- Names are unique in the OWNER's namespace — not tenant-wide —
    -- so two different operators can both have a "Critical" view.
    UNIQUE (organization_id, user_id, name)
);

-- Hot-path index for the "list this user's saved views" query.
CREATE INDEX IF NOT EXISTS ix_saved_view_user
    ON net.saved_view (organization_id, user_id, name)
    WHERE deleted_at IS NULL;

COMMENT ON TABLE net.saved_view IS
    'Per-user named search queries. (name, q, entity_types, filters jsonb) — restored via the saved-views picker in the global-search UI. Phase 10.';
