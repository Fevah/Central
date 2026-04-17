-- Team hierarchy + scoped permissions + resources

-- Add parent_id to teams (for parent/child team hierarchy)
ALTER TABLE teams ADD COLUMN IF NOT EXISTS parent_id int REFERENCES teams(id) ON DELETE SET NULL;

-- Team-scoped resources (team workspaces)
CREATE TABLE IF NOT EXISTS team_resources (
    id              serial PRIMARY KEY,
    team_id         int NOT NULL REFERENCES teams(id) ON DELETE CASCADE,
    resource_type   text NOT NULL,   -- task, project, folder, dashboard
    resource_id     int NOT NULL,
    access_level    text NOT NULL DEFAULT 'read',  -- read, write, admin
    added_at        timestamptz NOT NULL DEFAULT now(),
    UNIQUE(team_id, resource_type, resource_id)
);

CREATE INDEX IF NOT EXISTS idx_team_resources_team ON team_resources(team_id);
CREATE INDEX IF NOT EXISTS idx_team_resources_resource ON team_resources(resource_type, resource_id);

-- Team-level permission grants
CREATE TABLE IF NOT EXISTS team_permissions (
    id              serial PRIMARY KEY,
    team_id         int NOT NULL REFERENCES teams(id) ON DELETE CASCADE,
    permission_code text NOT NULL,
    is_granted      boolean NOT NULL DEFAULT true,
    granted_at      timestamptz NOT NULL DEFAULT now(),
    UNIQUE(team_id, permission_code)
);

-- Company-level role assignments (user has role within specific company)
CREATE TABLE IF NOT EXISTS company_user_roles (
    id              serial PRIMARY KEY,
    company_id      int NOT NULL REFERENCES companies(id) ON DELETE CASCADE,
    user_id         int NOT NULL REFERENCES app_users(id) ON DELETE CASCADE,
    role_name       text NOT NULL,
    assigned_by     int REFERENCES app_users(id),
    assigned_at     timestamptz NOT NULL DEFAULT now(),
    UNIQUE(company_id, user_id)
);

CREATE INDEX IF NOT EXISTS idx_company_roles_company ON company_user_roles(company_id);
CREATE INDEX IF NOT EXISTS idx_company_roles_user ON company_user_roles(user_id);

-- Team activity tracking (for dashboards)
CREATE TABLE IF NOT EXISTS team_activity (
    id              bigserial PRIMARY KEY,
    team_id         int NOT NULL REFERENCES teams(id) ON DELETE CASCADE,
    user_id         int REFERENCES app_users(id),
    activity_type   text NOT NULL,    -- task_completed, joined, resource_added, etc.
    entity_type     text,
    entity_id       int,
    description     text,
    occurred_at     timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_team_activity_team ON team_activity(team_id, occurred_at DESC);
