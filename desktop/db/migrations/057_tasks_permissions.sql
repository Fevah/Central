-- Migration 057: Task module permissions
INSERT INTO permissions (code, name, category, description) VALUES
    ('tasks:read', 'View Tasks', 'tasks', 'View task list and details'),
    ('tasks:write', 'Edit Tasks', 'tasks', 'Create and edit tasks'),
    ('tasks:delete', 'Delete Tasks', 'tasks', 'Delete tasks')
ON CONFLICT DO NOTHING;

-- Grant to Admin role
INSERT INTO role_permission_grants (role_name, permission_code)
SELECT 'Admin', code FROM permissions WHERE category = 'tasks'
ON CONFLICT DO NOTHING;

-- Grant read to Operator
INSERT INTO role_permission_grants (role_name, permission_code)
VALUES ('Operator', 'tasks:read'), ('Operator', 'tasks:write')
ON CONFLICT DO NOTHING;

-- Grant read to Viewer
INSERT INTO role_permission_grants (role_name, permission_code)
VALUES ('Viewer', 'tasks:read')
ON CONFLICT DO NOTHING;
