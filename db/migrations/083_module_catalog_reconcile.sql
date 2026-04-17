-- =============================================================================
-- 083 — Reconcile module_catalog with the post-consolidation WPF layout.
--
-- The WPF shell was restructured 2026-04-17:
--   - modules/switches + routing + vlans + links  -> modules/networking (one module)
--   - modules/tasks                                -> modules/projects (renamed)
--   - modules/admin + dashboard + global-admin     -> modules/global (one always-on assembly)
--
-- module_catalog still had the pre-consolidation codes. Without this migration
-- the tenant-aware IModuleLicenseGate always falls back to allow-all (because
-- GetModulesAsync returns zero rows matching the new Bootstrapper codes), so
-- the gate has no real effect.
--
-- This migration:
--   1. Adds the new consolidated codes to module_catalog.
--   2. Preserves every tenant's existing entitlements: a tenant licensed for
--      any of {switches, links, routing, vlans} gets a `networking` licence,
--      a tenant licensed for `tasks` gets `projects`, and so on.
--   3. Removes the now-obsolete catalog rows + licence rows.
--
-- Safe to re-run (ON CONFLICT DO NOTHING + idempotent deletes).
-- =============================================================================

-- ─── 1. Insert the new consolidated module codes ──────────────────────────
INSERT INTO central_platform.module_catalog (code, display_name, is_base) VALUES
    ('global',     'Platform Core (always on)',                  true),
    ('networking', 'Networking (switches + routing + VLANs + links)', false),
    ('projects',   'Projects & Tasks',                           false),
    ('crm',        'CRM (accounts + deals + pipeline)',          false)
ON CONFLICT (code) DO NOTHING;

-- ─── 2. Preserve tenant entitlements: grant the new code to any tenant that
--        had one of the merged-away old codes. No-op if the tenant already
--        has the new code. ────────────────────────────────────────────────

-- Any tenant with devices / switches / links / routing / vlans -> networking.
-- devices (IPAM) was folded into networking on 2026-04-17 along with the rest.
INSERT INTO central_platform.tenant_module_licenses (tenant_id, module_id, granted_at)
SELECT DISTINCT
    l.tenant_id,
    (SELECT id FROM central_platform.module_catalog WHERE code = 'networking'),
    NOW()
FROM central_platform.tenant_module_licenses l
JOIN central_platform.module_catalog m ON m.id = l.module_id
WHERE m.code IN ('devices', 'switches', 'links', 'routing', 'vlans')
ON CONFLICT (tenant_id, module_id) DO NOTHING;

-- Any tenant with tasks -> projects
INSERT INTO central_platform.tenant_module_licenses (tenant_id, module_id, granted_at)
SELECT DISTINCT
    l.tenant_id,
    (SELECT id FROM central_platform.module_catalog WHERE code = 'projects'),
    NOW()
FROM central_platform.tenant_module_licenses l
JOIN central_platform.module_catalog m ON m.id = l.module_id
WHERE m.code = 'tasks'
ON CONFLICT (tenant_id, module_id) DO NOTHING;

-- Any tenant with admin or globaladmin -> global
INSERT INTO central_platform.tenant_module_licenses (tenant_id, module_id, granted_at)
SELECT DISTINCT
    l.tenant_id,
    (SELECT id FROM central_platform.module_catalog WHERE code = 'global'),
    NOW()
FROM central_platform.tenant_module_licenses l
JOIN central_platform.module_catalog m ON m.id = l.module_id
WHERE m.code IN ('admin', 'globaladmin')
ON CONFLICT (tenant_id, module_id) DO NOTHING;

-- ─── 3. Delete licence rows for the obsolete codes, then drop the codes. ──
DELETE FROM central_platform.tenant_module_licenses
WHERE module_id IN (
    SELECT id FROM central_platform.module_catalog
    WHERE code IN ('devices', 'switches', 'links', 'routing', 'vlans', 'tasks', 'admin', 'globaladmin')
);

DELETE FROM central_platform.module_catalog
WHERE code IN ('devices', 'switches', 'links', 'routing', 'vlans', 'tasks', 'admin', 'globaladmin');

-- ─── Record in schema_versions if the table exists ────────────────────────
DO $$ BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'schema_versions') THEN
        INSERT INTO schema_versions (version_number, description)
        VALUES ('083_module_catalog_reconcile',
                'Reconcile central_platform.module_catalog with the post-merge WPF module layout (networking / projects / global / crm).')
        ON CONFLICT (version_number) DO NOTHING;
    END IF;
END $$;
