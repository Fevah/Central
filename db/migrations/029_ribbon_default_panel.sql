-- Migration 029: Ribbon page default panel + missing permissions
-- Applied: 2026-04-14
-- Adds default_panel column to ribbon_pages for auto-open-on-tab-click.
-- Adds tasks:* and admin:* permissions that were missing from the DB.

-- ── ribbon_pages.default_panel ──
ALTER TABLE ribbon_pages ADD COLUMN IF NOT EXISTS default_panel text;

-- Seed sensible defaults (panel x:Name that opens when tab is clicked)
UPDATE ribbon_pages SET default_panel = 'DevicesPanel'       WHERE header = 'Devices'       AND default_panel IS NULL;
UPDATE ribbon_pages SET default_panel = 'SwitchesPanel'      WHERE header = 'Switches'      AND default_panel IS NULL;
UPDATE ribbon_pages SET default_panel = 'TasksPanel'         WHERE header = 'Tasks'         AND default_panel IS NULL;
UPDATE ribbon_pages SET default_panel = 'ServiceDeskPanel'   WHERE header = 'Service Desk'  AND default_panel IS NULL;
UPDATE ribbon_pages SET default_panel = 'RolesPanel'         WHERE header = 'Admin'         AND default_panel IS NULL;
UPDATE ribbon_pages SET default_panel = 'GlobalTenantsPanel' WHERE header = 'Global Admin'  AND default_panel IS NULL;

-- ── Missing permission codes ──
INSERT INTO permissions (code, name, category, description, is_system, sort_order) VALUES
  ('tasks:read',        'View Tasks',       'tasks',     'View tasks and task panels',        true, 0),
  ('tasks:write',       'Edit Tasks',       'tasks',     'Create and edit tasks',             true, 0),
  ('tasks:delete',      'Delete Tasks',     'tasks',     'Delete tasks',                      true, 0),
  ('admin:ad',          'AD Browser',       'admin',     'Browse Active Directory',           true, 0),
  ('admin:backup',      'DB Backup',        'admin',     'Database backup management',        true, 0),
  ('admin:containers',  'Containers',       'admin',     'Container management',              true, 0),
  ('admin:locations',   'Locations',        'admin',     'Location management',               true, 0),
  ('admin:migrations',  'Migrations',       'admin',     'Schema migration management',       true, 0),
  ('admin:purge',       'Purge Deleted',    'admin',     'Purge soft-deleted records',        true, 0),
  ('admin:references',  'Reference Config', 'admin',     'Reference number config',           true, 0),
  ('scheduler:read',    'View Scheduler',   'scheduler', 'View scheduler',                    true, 0),
  ('scheduler:write',   'Edit Scheduler',   'scheduler', 'Edit scheduler',                    true, 0)
ON CONFLICT (code) DO NOTHING;

-- Grant tasks:* to Admin role
INSERT INTO role_permission_grants (role_id, permission_id)
SELECT r.id, p.id FROM roles r, permissions p
WHERE r.name = 'Admin' AND p.code IN ('tasks:read', 'tasks:write', 'tasks:delete',
  'admin:ad', 'admin:backup', 'admin:containers', 'admin:locations',
  'admin:migrations', 'admin:purge', 'admin:references',
  'scheduler:read', 'scheduler:write')
ON CONFLICT DO NOTHING;

-- Grant tasks:read to Operator
INSERT INTO role_permission_grants (role_id, permission_id)
SELECT r.id, p.id FROM roles r, permissions p
WHERE r.name = 'Operator' AND p.code = 'tasks:read'
ON CONFLICT DO NOTHING;
