-- Groups — dynamic, rule-based, cross-team permission buckets.
-- Different from teams: groups are for batch permission management and can have rule-based auto-assignment.

CREATE TABLE IF NOT EXISTS user_groups (
    id              serial PRIMARY KEY,
    tenant_id       uuid,
    name            text NOT NULL,
    description     text,
    group_type      text NOT NULL DEFAULT 'static',  -- static, dynamic
    rule_expression text,             -- JSONLogic-style rule for dynamic groups
    is_active       boolean NOT NULL DEFAULT true,
    created_by      int REFERENCES app_users(id),
    created_at      timestamptz NOT NULL DEFAULT now(),
    updated_at      timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_user_groups_tenant ON user_groups(tenant_id);
CREATE INDEX IF NOT EXISTS idx_user_groups_name ON user_groups(name);

CREATE TABLE IF NOT EXISTS group_members (
    id              serial PRIMARY KEY,
    group_id        int NOT NULL REFERENCES user_groups(id) ON DELETE CASCADE,
    user_id         int NOT NULL REFERENCES app_users(id) ON DELETE CASCADE,
    added_by        int REFERENCES app_users(id),
    added_at        timestamptz NOT NULL DEFAULT now(),
    auto_assigned   boolean NOT NULL DEFAULT false,  -- true if by rule
    UNIQUE(group_id, user_id)
);

CREATE INDEX IF NOT EXISTS idx_group_members_group ON group_members(group_id);
CREATE INDEX IF NOT EXISTS idx_group_members_user ON group_members(user_id);

-- Group-level permission grants (in addition to role permissions)
CREATE TABLE IF NOT EXISTS group_permissions (
    id              serial PRIMARY KEY,
    group_id        int NOT NULL REFERENCES user_groups(id) ON DELETE CASCADE,
    permission_code text NOT NULL,
    is_granted      boolean NOT NULL DEFAULT true,    -- false = explicit deny
    granted_at      timestamptz NOT NULL DEFAULT now(),
    UNIQUE(group_id, permission_code)
);

-- Group-based resource access (scopes resources to a group)
CREATE TABLE IF NOT EXISTS group_resource_access (
    id              serial PRIMARY KEY,
    group_id        int NOT NULL REFERENCES user_groups(id) ON DELETE CASCADE,
    resource_type   text NOT NULL,   -- task, project, switch, device, etc.
    resource_id     int NOT NULL,
    access_level    text NOT NULL DEFAULT 'read',  -- read, write, admin
    granted_at      timestamptz NOT NULL DEFAULT now(),
    UNIQUE(group_id, resource_type, resource_id)
);

CREATE INDEX IF NOT EXISTS idx_group_resources_group ON group_resource_access(group_id);
CREATE INDEX IF NOT EXISTS idx_group_resources_resource ON group_resource_access(resource_type, resource_id);

-- Auto-assignment rules
CREATE TABLE IF NOT EXISTS group_assignment_rules (
    id              serial PRIMARY KEY,
    group_id        int NOT NULL REFERENCES user_groups(id) ON DELETE CASCADE,
    rule_name       text NOT NULL,
    rule_type       text NOT NULL,   -- department, title, role, email_domain, custom
    rule_value      text NOT NULL,
    priority        int NOT NULL DEFAULT 100,
    is_enabled      boolean NOT NULL DEFAULT true,
    created_at      timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_group_rules_group ON group_assignment_rules(group_id);
