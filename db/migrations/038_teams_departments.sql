-- Phase 3: Team/Department hierarchy
-- Organizational structure for enterprise team management.

CREATE TABLE IF NOT EXISTS departments (
    id              serial PRIMARY KEY,
    tenant_id       uuid,
    name            text NOT NULL,
    parent_id       int REFERENCES departments(id) ON DELETE SET NULL,
    head_user_id    int REFERENCES app_users(id) ON DELETE SET NULL,
    cost_center     text,
    description     text,
    is_active       boolean NOT NULL DEFAULT true,
    created_at      timestamptz NOT NULL DEFAULT now(),
    updated_at      timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_departments_tenant ON departments(tenant_id);
CREATE INDEX IF NOT EXISTS idx_departments_parent ON departments(parent_id);

CREATE TABLE IF NOT EXISTS teams (
    id              serial PRIMARY KEY,
    tenant_id       uuid,
    department_id   int REFERENCES departments(id) ON DELETE SET NULL,
    name            text NOT NULL,
    description     text,
    team_lead_id    int REFERENCES app_users(id) ON DELETE SET NULL,
    is_active       boolean NOT NULL DEFAULT true,
    created_at      timestamptz NOT NULL DEFAULT now(),
    updated_at      timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_teams_tenant ON teams(tenant_id);
CREATE INDEX IF NOT EXISTS idx_teams_dept ON teams(department_id);

CREATE TABLE IF NOT EXISTS team_members (
    id              serial PRIMARY KEY,
    team_id         int NOT NULL REFERENCES teams(id) ON DELETE CASCADE,
    user_id         int NOT NULL REFERENCES app_users(id) ON DELETE CASCADE,
    role_in_team    text NOT NULL DEFAULT 'member',  -- member, lead, observer
    joined_at       timestamptz NOT NULL DEFAULT now(),
    UNIQUE(team_id, user_id)
);

CREATE INDEX IF NOT EXISTS idx_team_members_team ON team_members(team_id);
CREATE INDEX IF NOT EXISTS idx_team_members_user ON team_members(user_id);

-- Link app_users to department
ALTER TABLE app_users ADD COLUMN IF NOT EXISTS department_id int REFERENCES departments(id) ON DELETE SET NULL;

-- pg_notify
CREATE OR REPLACE FUNCTION notify_teams_change() RETURNS trigger AS $$
BEGIN PERFORM pg_notify('data_changed', json_build_object('table','teams','op',TG_OP,'id',COALESCE(NEW.id,OLD.id))::text); RETURN COALESCE(NEW,OLD); END;
$$ LANGUAGE plpgsql;
DROP TRIGGER IF EXISTS trg_teams_notify ON teams;
CREATE TRIGGER trg_teams_notify AFTER INSERT OR UPDATE OR DELETE ON teams FOR EACH ROW EXECUTE FUNCTION notify_teams_change();
DROP TRIGGER IF EXISTS trg_departments_notify ON departments;
CREATE TRIGGER trg_departments_notify AFTER INSERT OR UPDATE OR DELETE ON departments FOR EACH ROW EXECUTE FUNCTION notify_teams_change();
