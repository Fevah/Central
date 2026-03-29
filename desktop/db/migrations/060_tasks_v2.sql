-- 060_tasks_v2.sql — Phase 1: Hierarchy & Core Schema Expansion
-- Adds: portfolios, programmes, projects, sprints, releases, task_links, task_dependencies
-- Expands: tasks table with planning/scheduling/QA fields

-- ============================================================
-- 1. Portfolio / Programme / Project hierarchy
-- ============================================================

CREATE TABLE IF NOT EXISTS portfolios (
    id          serial PRIMARY KEY,
    name        varchar(256) NOT NULL,
    description text DEFAULT '',
    owner_id    integer REFERENCES app_users(id) ON DELETE SET NULL,
    archived    boolean DEFAULT false,
    created_at  timestamptz DEFAULT now(),
    updated_at  timestamptz DEFAULT now()
);

CREATE TABLE IF NOT EXISTS programmes (
    id            serial PRIMARY KEY,
    portfolio_id  integer REFERENCES portfolios(id) ON DELETE SET NULL,
    name          varchar(256) NOT NULL,
    description   text DEFAULT '',
    owner_id      integer REFERENCES app_users(id) ON DELETE SET NULL,
    created_at    timestamptz DEFAULT now(),
    updated_at    timestamptz DEFAULT now()
);

CREATE TABLE IF NOT EXISTS task_projects (
    id                  serial PRIMARY KEY,
    programme_id        integer REFERENCES programmes(id) ON DELETE SET NULL,
    name                varchar(256) NOT NULL,
    description         text DEFAULT '',
    scheduling_method   varchar(32) DEFAULT 'FixedDuration',  -- FixedDuration | FixedWork
    default_mode        varchar(16) DEFAULT 'Agile',          -- Agile | TaskBased
    method_template     varchar(16) DEFAULT 'Scrum',          -- Scrum | Kanban | SAFe | Custom
    calendar            varchar(64),
    archived            boolean DEFAULT false,
    created_at          timestamptz DEFAULT now(),
    updated_at          timestamptz DEFAULT now()
);

CREATE TABLE IF NOT EXISTS project_members (
    id          serial PRIMARY KEY,
    project_id  integer NOT NULL REFERENCES task_projects(id) ON DELETE CASCADE,
    user_id     integer NOT NULL REFERENCES app_users(id) ON DELETE CASCADE,
    role        varchar(32) DEFAULT 'Member',  -- MainManager | Member | QAUser | ReadOnly
    UNIQUE(project_id, user_id)
);

-- ============================================================
-- 2. Sprints & Releases
-- ============================================================

CREATE TABLE IF NOT EXISTS sprints (
    id              serial PRIMARY KEY,
    project_id      integer NOT NULL REFERENCES task_projects(id) ON DELETE CASCADE,
    name            varchar(128) NOT NULL,
    start_date      date,
    end_date        date,
    goal            text DEFAULT '',
    status          varchar(32) DEFAULT 'Planning',  -- Planning | Active | Closed
    velocity_points numeric(8,1),
    velocity_hours  numeric(8,1),
    created_at      timestamptz DEFAULT now()
);

CREATE TABLE IF NOT EXISTS releases (
    id          serial PRIMARY KEY,
    project_id  integer NOT NULL REFERENCES task_projects(id) ON DELETE CASCADE,
    name        varchar(128) NOT NULL,
    target_date date,
    description text DEFAULT '',
    status      varchar(32) DEFAULT 'Planned',  -- Planned | InProgress | Released
    created_at  timestamptz DEFAULT now()
);

-- ============================================================
-- 3. Expand tasks table
-- ============================================================

-- Project & Sprint linkage
ALTER TABLE tasks ADD COLUMN IF NOT EXISTS project_id       integer REFERENCES task_projects(id) ON DELETE SET NULL;
ALTER TABLE tasks ADD COLUMN IF NOT EXISTS sprint_id        integer REFERENCES sprints(id) ON DELETE SET NULL;
ALTER TABLE tasks ADD COLUMN IF NOT EXISTS wbs              varchar(64);

-- Epic / Story flags
ALTER TABLE tasks ADD COLUMN IF NOT EXISTS is_epic          boolean DEFAULT false;
ALTER TABLE tasks ADD COLUMN IF NOT EXISTS is_user_story    boolean DEFAULT false;
ALTER TABLE tasks ADD COLUMN IF NOT EXISTS user_story       text;
ALTER TABLE tasks ADD COLUMN IF NOT EXISTS detailed_description text;

-- Visual
ALTER TABLE tasks ADD COLUMN IF NOT EXISTS color            varchar(16);
ALTER TABLE tasks ADD COLUMN IF NOT EXISTS hyperlink        text;

-- Planning & Estimation
ALTER TABLE tasks ADD COLUMN IF NOT EXISTS points           numeric(6,1);
ALTER TABLE tasks ADD COLUMN IF NOT EXISTS work_remaining   numeric(8,1);
ALTER TABLE tasks ADD COLUMN IF NOT EXISTS budgeted_work    numeric(8,1);

-- Scheduling
ALTER TABLE tasks ADD COLUMN IF NOT EXISTS start_date       date;
ALTER TABLE tasks ADD COLUMN IF NOT EXISTS finish_date      date;
ALTER TABLE tasks ADD COLUMN IF NOT EXISTS is_milestone     boolean DEFAULT false;

-- Classification
ALTER TABLE tasks ADD COLUMN IF NOT EXISTS risk             varchar(16);
ALTER TABLE tasks ADD COLUMN IF NOT EXISTS confidence       varchar(16);
ALTER TABLE tasks ADD COLUMN IF NOT EXISTS severity         varchar(16);
ALTER TABLE tasks ADD COLUMN IF NOT EXISTS bug_priority     varchar(16);
ALTER TABLE tasks ADD COLUMN IF NOT EXISTS backlog_priority integer DEFAULT 0;
ALTER TABLE tasks ADD COLUMN IF NOT EXISTS sprint_priority  integer DEFAULT 0;
ALTER TABLE tasks ADD COLUMN IF NOT EXISTS committed_to     integer REFERENCES sprints(id) ON DELETE SET NULL;
ALTER TABLE tasks ADD COLUMN IF NOT EXISTS category         varchar(64);

-- Board
ALTER TABLE tasks ADD COLUMN IF NOT EXISTS board_column     varchar(64);
ALTER TABLE tasks ADD COLUMN IF NOT EXISTS board_lane       varchar(64);

-- Time
ALTER TABLE tasks ADD COLUMN IF NOT EXISTS time_spent       numeric(8,1) DEFAULT 0;

-- ============================================================
-- 4. Release tagging (many-to-many)
-- ============================================================

CREATE TABLE IF NOT EXISTS task_releases (
    task_id    integer NOT NULL REFERENCES tasks(id) ON DELETE CASCADE,
    release_id integer NOT NULL REFERENCES releases(id) ON DELETE CASCADE,
    PRIMARY KEY (task_id, release_id)
);

-- ============================================================
-- 5. Cross-project item links
-- ============================================================

CREATE TABLE IF NOT EXISTS task_links (
    id          serial PRIMARY KEY,
    source_id   integer NOT NULL REFERENCES tasks(id) ON DELETE CASCADE,
    target_id   integer NOT NULL REFERENCES tasks(id) ON DELETE CASCADE,
    link_type   varchar(32) NOT NULL DEFAULT 'relates_to',  -- relates_to | blocks | blocked_by | duplicates
    lag_days    integer DEFAULT 0,
    created_at  timestamptz DEFAULT now(),
    UNIQUE(source_id, target_id, link_type)
);

-- ============================================================
-- 6. Gantt dependency links
-- ============================================================

CREATE TABLE IF NOT EXISTS task_dependencies (
    id              serial PRIMARY KEY,
    predecessor_id  integer NOT NULL REFERENCES tasks(id) ON DELETE CASCADE,
    successor_id    integer NOT NULL REFERENCES tasks(id) ON DELETE CASCADE,
    dep_type        varchar(4) NOT NULL DEFAULT 'FS',  -- FS | FF | SF | SS
    lag_days        integer DEFAULT 0,
    UNIQUE(predecessor_id, successor_id)
);

-- ============================================================
-- 7. Indexes
-- ============================================================

CREATE INDEX IF NOT EXISTS idx_tasks_project ON tasks (project_id);
CREATE INDEX IF NOT EXISTS idx_tasks_sprint ON tasks (sprint_id);
CREATE INDEX IF NOT EXISTS idx_tasks_committed ON tasks (committed_to);
CREATE INDEX IF NOT EXISTS idx_sprints_project ON sprints (project_id);
CREATE INDEX IF NOT EXISTS idx_releases_project ON releases (project_id);
CREATE INDEX IF NOT EXISTS idx_project_members_project ON project_members (project_id);
CREATE INDEX IF NOT EXISTS idx_task_links_source ON task_links (source_id);
CREATE INDEX IF NOT EXISTS idx_task_links_target ON task_links (target_id);
CREATE INDEX IF NOT EXISTS idx_task_deps_pred ON task_dependencies (predecessor_id);
CREATE INDEX IF NOT EXISTS idx_task_deps_succ ON task_dependencies (successor_id);

-- ============================================================
-- 8. pg_notify triggers
-- ============================================================

DROP TRIGGER IF EXISTS trg_notify_portfolios ON portfolios;
CREATE TRIGGER trg_notify_portfolios AFTER INSERT OR UPDATE OR DELETE ON portfolios
    FOR EACH ROW EXECUTE FUNCTION notify_data_change();

DROP TRIGGER IF EXISTS trg_notify_programmes ON programmes;
CREATE TRIGGER trg_notify_programmes AFTER INSERT OR UPDATE OR DELETE ON programmes
    FOR EACH ROW EXECUTE FUNCTION notify_data_change();

DROP TRIGGER IF EXISTS trg_notify_task_projects ON task_projects;
CREATE TRIGGER trg_notify_task_projects AFTER INSERT OR UPDATE OR DELETE ON task_projects
    FOR EACH ROW EXECUTE FUNCTION notify_data_change();

DROP TRIGGER IF EXISTS trg_notify_sprints ON sprints;
CREATE TRIGGER trg_notify_sprints AFTER INSERT OR UPDATE OR DELETE ON sprints
    FOR EACH ROW EXECUTE FUNCTION notify_data_change();

DROP TRIGGER IF EXISTS trg_notify_releases ON releases;
CREATE TRIGGER trg_notify_releases AFTER INSERT OR UPDATE OR DELETE ON releases
    FOR EACH ROW EXECUTE FUNCTION notify_data_change();

DROP TRIGGER IF EXISTS trg_notify_task_links ON task_links;
CREATE TRIGGER trg_notify_task_links AFTER INSERT OR UPDATE OR DELETE ON task_links
    FOR EACH ROW EXECUTE FUNCTION notify_data_change();

DROP TRIGGER IF EXISTS trg_notify_task_deps ON task_dependencies;
CREATE TRIGGER trg_notify_task_deps AFTER INSERT OR UPDATE OR DELETE ON task_dependencies
    FOR EACH ROW EXECUTE FUNCTION notify_data_change();

-- ============================================================
-- 9. Permissions
-- ============================================================

INSERT INTO permissions (code, description) VALUES
    ('projects:read',   'View projects, portfolios, and programmes'),
    ('projects:write',  'Create and edit projects'),
    ('projects:delete', 'Delete projects'),
    ('sprints:read',    'View sprints and releases'),
    ('sprints:write',   'Create and manage sprints'),
    ('sprints:delete',  'Delete sprints')
ON CONFLICT (code) DO NOTHING;

-- Grant to roles
INSERT INTO role_permission_grants (role_id, permission_id)
SELECT r.id, p.id FROM roles r CROSS JOIN permissions p
WHERE r.name = 'Admin' AND p.code IN ('projects:read','projects:write','projects:delete','sprints:read','sprints:write','sprints:delete')
ON CONFLICT DO NOTHING;

INSERT INTO role_permission_grants (role_id, permission_id)
SELECT r.id, p.id FROM roles r CROSS JOIN permissions p
WHERE r.name = 'Operator' AND p.code IN ('projects:read','projects:write','sprints:read','sprints:write')
ON CONFLICT DO NOTHING;

INSERT INTO role_permission_grants (role_id, permission_id)
SELECT r.id, p.id FROM roles r CROSS JOIN permissions p
WHERE r.name = 'Viewer' AND p.code IN ('projects:read','sprints:read')
ON CONFLICT DO NOTHING;

-- ============================================================
-- 10. Seed a default project
-- ============================================================

INSERT INTO task_projects (name, description, scheduling_method, default_mode, method_template)
VALUES ('Default Project', 'Default project for existing tasks', 'FixedDuration', 'Agile', 'Scrum')
ON CONFLICT DO NOTHING;

-- Link existing tasks to the default project
UPDATE tasks SET project_id = (SELECT id FROM task_projects WHERE name = 'Default Project' LIMIT 1)
WHERE project_id IS NULL;
