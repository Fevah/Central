-- Central Platform — First-time database seed
-- Run after all migrations are applied.
-- Creates: admin user, default roles, base permissions, default settings.

-- ── Roles ──
INSERT INTO roles (name, priority, is_system, description) VALUES
    ('Admin', 100, true, 'Full access to all modules'),
    ('Operator', 50, false, 'Can edit devices, switches, links, service desk'),
    ('Viewer', 10, false, 'Read-only access to all data')
ON CONFLICT (name) DO NOTHING;

-- ── Admin user (password: admin — change immediately) ──
INSERT INTO app_users (username, display_name, role, is_active, user_type, password_hash, salt, auto_login)
VALUES ('admin', 'Administrator', 'Admin', true, 'System',
    -- SHA256("admin" + "central-default-salt")
    'KPlJRTsXJr0nYn2YG3LLHA0rXyXq0/7jMJ5kNq0WQrU=', 'central-default-salt', false)
ON CONFLICT (username) DO NOTHING;

-- ── Grant all permissions to Admin role ──
INSERT INTO role_permission_grants (role_name, permission_code)
SELECT 'Admin', code FROM permissions
ON CONFLICT DO NOTHING;

-- ── Grant read permissions to Viewer role ──
INSERT INTO role_permission_grants (role_name, permission_code)
SELECT 'Viewer', code FROM permissions WHERE code LIKE '%:read' OR code LIKE '%:view'
ON CONFLICT DO NOTHING;

-- ── Default lookup values ──
INSERT INTO lookup_values (category, value, sort_order) VALUES
    ('status', 'Active', 1),
    ('status', 'RESERVED', 2),
    ('status', 'Decommissioned', 3),
    ('status', 'Maintenance', 4),
    ('device_type', 'Core Switch', 1),
    ('device_type', 'Access Switch', 2),
    ('device_type', 'Router', 3),
    ('device_type', 'Firewall', 4),
    ('device_type', 'Server', 5),
    ('device_type', 'Access Point', 6),
    ('building', 'MEP-91', 1),
    ('building', 'MEP-92', 2),
    ('building', 'MEP-93', 3),
    ('building', 'MEP-94', 4),
    ('building', 'MEP-96', 5)
ON CONFLICT DO NOTHING;

-- ── Default notification preferences for admin ──
INSERT INTO notification_preferences (user_id, event_type, channel, is_enabled)
SELECT u.id, et.event_type, 'toast', true
FROM app_users u
CROSS JOIN (VALUES
    ('sync_failure'), ('sync_complete'), ('auth_lockout'),
    ('backup_complete'), ('backup_failure'), ('data_changed'),
    ('password_expiry'), ('webhook_received')
) et(event_type)
WHERE u.username = 'admin'
ON CONFLICT DO NOTHING;

-- ── Seed complete ──
DO $$ BEGIN RAISE NOTICE 'Central database seeded successfully'; END $$;
