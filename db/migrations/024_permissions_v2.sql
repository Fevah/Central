-- =============================================================================
-- 024_permissions_v2.sql — Fine-grained module:action permissions
-- Adds new tables alongside existing role_permissions (non-destructive).
-- Existing data preserved — legacy role_permissions still used as fallback.
-- =============================================================================

-- Permissions catalogue
CREATE TABLE IF NOT EXISTS permissions (
    id          SERIAL PRIMARY KEY,
    code        VARCHAR(100) NOT NULL UNIQUE,
    name        VARCHAR(255) NOT NULL,
    category    VARCHAR(64) NOT NULL,
    description TEXT,
    is_system   BOOLEAN NOT NULL DEFAULT TRUE,
    sort_order  INTEGER NOT NULL DEFAULT 0
);

-- Role → permission grants (many-to-many)
CREATE TABLE IF NOT EXISTS role_permission_grants (
    role_id         INTEGER NOT NULL REFERENCES roles(id) ON DELETE CASCADE,
    permission_id   INTEGER NOT NULL REFERENCES permissions(id) ON DELETE CASCADE,
    PRIMARY KEY (role_id, permission_id)
);

-- Add priority to roles for hierarchy
ALTER TABLE roles ADD COLUMN IF NOT EXISTS priority INTEGER NOT NULL DEFAULT 10;

-- Seed permissions
INSERT INTO permissions (code, name, category, sort_order) VALUES
    -- Devices / IPAM
    ('devices:read',     'View Devices',          'devices',  10),
    ('devices:write',    'Edit Devices',          'devices',  20),
    ('devices:delete',   'Delete Devices',        'devices',  30),
    ('devices:export',   'Export Devices',        'devices',  40),
    ('devices:reserved', 'View Reserved',         'devices',  50),
    -- Switches
    ('switches:read',    'View Switches',         'switches', 10),
    ('switches:write',   'Edit Switches',         'switches', 20),
    ('switches:delete',  'Delete Switches',       'switches', 30),
    ('switches:ping',    'Ping Switches',         'switches', 40),
    ('switches:ssh',     'SSH to Switches',       'switches', 50),
    ('switches:sync',    'Sync Running Config',   'switches', 60),
    ('switches:deploy',  'Deploy Config',         'switches', 70),
    -- Links
    ('links:read',       'View Links',            'links',    10),
    ('links:write',      'Edit Links',            'links',    20),
    ('links:delete',     'Delete Links',          'links',    30),
    -- Routing / BGP
    ('bgp:read',         'View BGP',              'bgp',      10),
    ('bgp:write',        'Edit BGP',              'bgp',      20),
    ('bgp:sync',         'Sync BGP from Switch',  'bgp',      30),
    -- VLANs
    ('vlans:read',       'View VLANs',            'vlans',    10),
    ('vlans:write',      'Edit VLANs',            'vlans',    20),
    -- Admin
    ('admin:users',      'Manage Users',          'admin',    10),
    ('admin:roles',      'Manage Roles',          'admin',    20),
    ('admin:lookups',    'Manage Lookups',        'admin',    30),
    ('admin:settings',   'Manage Settings',       'admin',    40),
    ('admin:audit',      'View Audit Log',        'admin',    50)
ON CONFLICT (code) DO NOTHING;

-- Update role priorities
UPDATE roles SET priority = 1000 WHERE name = 'Admin';
UPDATE roles SET priority = 50   WHERE name = 'Operator';
UPDATE roles SET priority = 10   WHERE name = 'Viewer';

-- Seed Admin role with ALL permissions
INSERT INTO role_permission_grants (role_id, permission_id)
SELECT r.id, p.id FROM roles r CROSS JOIN permissions p
WHERE r.name = 'Admin'
ON CONFLICT DO NOTHING;

-- Seed Operator: read + write + sync, no delete, no deploy
INSERT INTO role_permission_grants (role_id, permission_id)
SELECT r.id, p.id FROM roles r CROSS JOIN permissions p
WHERE r.name = 'Operator'
  AND p.code IN (
    'devices:read', 'devices:write', 'devices:export', 'devices:reserved',
    'switches:read', 'switches:write', 'switches:ping', 'switches:ssh', 'switches:sync',
    'links:read', 'links:write',
    'bgp:read', 'bgp:write', 'bgp:sync',
    'vlans:read', 'vlans:write',
    'admin:audit'
  )
ON CONFLICT DO NOTHING;

-- Seed Viewer: read-only
INSERT INTO role_permission_grants (role_id, permission_id)
SELECT r.id, p.id FROM roles r CROSS JOIN permissions p
WHERE r.name = 'Viewer'
  AND p.code IN (
    'devices:read',
    'switches:read',
    'links:read',
    'bgp:read',
    'vlans:read',
    'admin:audit'
  )
ON CONFLICT DO NOTHING;
