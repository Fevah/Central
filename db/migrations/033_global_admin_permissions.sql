-- Global Admin permissions — gate platform-level operations
INSERT INTO permissions (code, name, category, description) VALUES
    ('global_admin:read',      'View Global Admin',      'global_admin', 'View Global Admin panels and data'),
    ('global_admin:write',     'Edit Global Admin',      'global_admin', 'Create/edit tenants, users, subscriptions'),
    ('global_admin:delete',    'Delete Global Admin',    'global_admin', 'Delete/suspend tenants, revoke licenses'),
    ('global_admin:provision', 'Provision Schemas',      'global_admin', 'Provision tenant database schemas')
ON CONFLICT (code) DO NOTHING;

-- Grant all global_admin permissions to the Admin role (id=1)
INSERT INTO role_permission_grants (role_id, permission_id)
SELECT 1, id FROM permissions WHERE code LIKE 'global_admin:%'
ON CONFLICT DO NOTHING;
