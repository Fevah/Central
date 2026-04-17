-- Permission inheritance + overrides
-- Allows: role → inherits from parent role; user-level overrides (explicit grant/deny)

-- Parent role for inheritance
ALTER TABLE roles ADD COLUMN IF NOT EXISTS parent_role_id int REFERENCES roles(id) ON DELETE SET NULL;

-- Per-user permission overrides (in addition to role permissions)
CREATE TABLE IF NOT EXISTS user_permission_overrides (
    id              serial PRIMARY KEY,
    user_id         int NOT NULL REFERENCES app_users(id) ON DELETE CASCADE,
    permission_code text NOT NULL,
    is_granted      boolean NOT NULL,   -- true = explicit grant, false = explicit deny
    reason          text,
    expires_at      timestamptz,
    granted_by      int REFERENCES app_users(id),
    created_at      timestamptz NOT NULL DEFAULT now(),
    UNIQUE(user_id, permission_code)
);

CREATE INDEX IF NOT EXISTS idx_user_overrides_user ON user_permission_overrides(user_id);

-- View to compute effective permissions (role + group + override)
-- Precedence: user deny > user grant > group grant > role grant
CREATE OR REPLACE VIEW v_user_effective_permissions AS
WITH user_roles AS (
    SELECT u.id AS user_id, r.id AS role_id
    FROM app_users u JOIN roles r ON r.name = u.role
),
role_perms AS (
    SELECT ur.user_id, rpg.permission_code, 'role' AS source, true AS is_granted
    FROM user_roles ur
    JOIN role_permission_grants rpg ON rpg.role_id = ur.role_id
),
group_perms AS (
    SELECT gm.user_id, gp.permission_code, 'group' AS source, gp.is_granted
    FROM group_members gm
    JOIN group_permissions gp ON gp.group_id = gm.group_id
),
user_overrides AS (
    SELECT user_id, permission_code, 'user' AS source, is_granted
    FROM user_permission_overrides
    WHERE expires_at IS NULL OR expires_at > NOW()
)
SELECT * FROM role_perms
UNION ALL SELECT * FROM group_perms
UNION ALL SELECT * FROM user_overrides;
