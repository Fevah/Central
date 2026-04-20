-- =============================================================================
-- 109 — Module versioning (Phase 1 of the module-update system).
--
-- See docs/MODULE_UPDATE_SYSTEM.md. This migration adds the schema needed to
-- answer three questions the desktop client asks on startup:
--   1. What modules does the platform publish for this tenant?
--   2. What's the latest version of each?
--   3. What kind of release is it (HotSwap / SoftReload / FullRestart)?
--
-- Phase 1 is READ-ONLY client side — the client compares loaded-version to
-- catalog-version and surfaces a banner. Phase 2 adds the DLL-download
-- pipeline; Phase 3 adds in-process hot-swap via AssemblyLoadContext; Phase 4
-- adds SignalR push so running clients pick up new versions live.
--
-- Safe to re-run (CREATE TABLE IF NOT EXISTS + ON CONFLICT DO NOTHING).
-- =============================================================================

-- ─── 1. Extend module_catalog with current-version bookkeeping ────────────
-- The catalog already exists (migration 026) as a licensing lookup. Phase 1
-- layers versioning metadata on top: current_version is the version the
-- server considers "latest stable" for every tenant on the default channel.
-- Per-tenant version-policy overrides live in central_platform.tenant_version_policy
-- (migration shipped with Phase 1 of the merge — we use it read-only here).

ALTER TABLE central_platform.module_catalog
    ADD COLUMN IF NOT EXISTS current_version varchar(32);

ALTER TABLE central_platform.module_catalog
    ADD COLUMN IF NOT EXISTS current_version_updated_at timestamptz;

COMMENT ON COLUMN central_platform.module_catalog.current_version IS
  'Latest module version published to the default channel. Clients on the '
  'default channel compare to their loaded Version and surface a banner '
  'when these differ. Never null after 109 — the migration backfills 1.0.0 '
  'for every existing catalog row.';

-- ─── 2. module_versions — one row per published version per module ────────
CREATE TABLE IF NOT EXISTS central_platform.module_versions (
    id                  bigserial PRIMARY KEY,
    module_code         varchar(64) NOT NULL
                           REFERENCES central_platform.module_catalog(code) ON DELETE CASCADE,
    version             varchar(32) NOT NULL,

    -- Release classification drives the client's reaction. See
    -- docs/MODULE_UPDATE_SYSTEM.md for the three-kind contract.
    change_kind         varchar(16) NOT NULL
                           CHECK (change_kind IN ('HotSwap', 'SoftReload', 'FullRestart')),

    -- Engine contract this DLL was compiled against. Host refuses to load
    -- if this exceeds EngineContract.CurrentVersion. Matches the constant
    -- in libs/engine/Modules/IModule.cs.
    min_engine_contract integer NOT NULL DEFAULT 1 CHECK (min_engine_contract >= 1),

    -- DLL distribution. Phase 1 leaves these nullable because no DLLs are
    -- published yet. Phase 2 populates them via the /api/modules/publish
    -- endpoint.
    blob_url            text,
    sha256              varchar(64),
    size_bytes          bigint,

    -- Release metadata.
    release_notes       text,
    published_at        timestamptz NOT NULL DEFAULT now(),
    published_by        uuid,                          -- nullable for system/seed inserts
    is_yanked           boolean NOT NULL DEFAULT false,
    yanked_at           timestamptz,
    yanked_reason       text,

    UNIQUE (module_code, version)
);

CREATE INDEX IF NOT EXISTS module_versions_module_code_idx
    ON central_platform.module_versions (module_code);

CREATE INDEX IF NOT EXISTS module_versions_published_at_idx
    ON central_platform.module_versions (published_at DESC);

-- Partial index: most queries want non-yanked rows. Partial index keeps
-- the common path narrow.
CREATE INDEX IF NOT EXISTS module_versions_active_idx
    ON central_platform.module_versions (module_code, published_at DESC)
    WHERE is_yanked = false;

COMMENT ON TABLE central_platform.module_versions IS
  'One row per published version per module. Driven by CI; read by the '
  'desktop client via /api/modules/catalog. change_kind drives the client '
  'reaction (silent/banner/scheduled-restart).';

-- ─── 3. Seed v1.0.0 HotSwap for every existing catalog row ────────────────
-- Every current module is at 1.0.0 by definition (Phase 1 baseline). change_kind
-- is HotSwap because the rows are informational only until Phase 2 lands the
-- DLL distribution — no actual code gets swapped yet.
INSERT INTO central_platform.module_versions
    (module_code, version, change_kind, min_engine_contract, release_notes, published_at)
SELECT
    code,
    '1.0.0',
    'HotSwap',
    1,
    'Phase 1 baseline — seeded by migration 109. No DLL distribution yet; '
    'this row exists so the catalog endpoint has a latest-version to return.',
    now()
FROM central_platform.module_catalog
ON CONFLICT (module_code, version) DO NOTHING;

-- Backfill current_version + timestamp on module_catalog.
UPDATE central_platform.module_catalog
   SET current_version = '1.0.0',
       current_version_updated_at = now()
 WHERE current_version IS NULL;

-- ─── 4. Record in schema_versions if the table exists ─────────────────────
DO $$ BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'schema_versions') THEN
        INSERT INTO schema_versions (version_number, description)
        VALUES ('109_module_versions',
                'Phase 1 module-update system: module_catalog.current_version + central_platform.module_versions table + 1.0.0 baseline seed for every module.')
        ON CONFLICT (version_number) DO NOTHING;
    END IF;
END $$;
