-- 067_time_activity.sql — Phase 9: Time Tracking & Activity Feed

CREATE TABLE IF NOT EXISTS time_entries (
    id              serial PRIMARY KEY,
    task_id         integer NOT NULL REFERENCES tasks(id) ON DELETE CASCADE,
    user_id         integer NOT NULL REFERENCES app_users(id) ON DELETE CASCADE,
    entry_date      date NOT NULL,
    hours           numeric(6,2) NOT NULL,
    activity_type   varchar(32),   -- Development|Testing|Review|Meeting|Admin
    notes           text,
    created_at      timestamptz DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_time_entries_user_date ON time_entries (user_id, entry_date);
CREATE INDEX IF NOT EXISTS idx_time_entries_task ON time_entries (task_id);

CREATE TABLE IF NOT EXISTS activity_feed (
    id              serial PRIMARY KEY,
    project_id      integer REFERENCES task_projects(id) ON DELETE SET NULL,
    task_id         integer REFERENCES tasks(id) ON DELETE SET NULL,
    user_id         integer REFERENCES app_users(id) ON DELETE SET NULL,
    user_name       varchar(128),
    action          varchar(32) NOT NULL,  -- created|updated|commented|status_changed|assigned|deleted
    summary         text,
    details         jsonb,
    created_at      timestamptz DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_activity_feed_project ON activity_feed (project_id, created_at DESC);
CREATE INDEX IF NOT EXISTS idx_activity_feed_task ON activity_feed (task_id, created_at DESC);

CREATE TABLE IF NOT EXISTS task_views (
    id              serial PRIMARY KEY,
    project_id      integer REFERENCES task_projects(id) ON DELETE CASCADE,
    name            varchar(128) NOT NULL,
    view_type       varchar(32) NOT NULL,  -- Tree|Grid|Board|Gantt|Backlog
    config_json     jsonb NOT NULL,
    created_by      integer REFERENCES app_users(id) ON DELETE SET NULL,
    is_default      boolean DEFAULT false,
    shared_with     jsonb,
    created_at      timestamptz DEFAULT now()
);

CREATE INDEX IF NOT EXISTS idx_task_views_project ON task_views (project_id);

-- pg_notify
DROP TRIGGER IF EXISTS trg_notify_time_entries ON time_entries;
CREATE TRIGGER trg_notify_time_entries AFTER INSERT OR UPDATE OR DELETE ON time_entries
    FOR EACH ROW EXECUTE FUNCTION notify_data_change();

DROP TRIGGER IF EXISTS trg_notify_activity_feed ON activity_feed;
CREATE TRIGGER trg_notify_activity_feed AFTER INSERT OR UPDATE OR DELETE ON activity_feed
    FOR EACH ROW EXECUTE FUNCTION notify_data_change();

DROP TRIGGER IF EXISTS trg_notify_task_views ON task_views;
CREATE TRIGGER trg_notify_task_views AFTER INSERT OR UPDATE OR DELETE ON task_views
    FOR EACH ROW EXECUTE FUNCTION notify_data_change();

-- Auto-feed: trigger that logs task changes to activity_feed
CREATE OR REPLACE FUNCTION log_task_activity() RETURNS trigger AS $$
BEGIN
    IF TG_OP = 'INSERT' THEN
        INSERT INTO activity_feed (project_id, task_id, user_id, action, summary)
        VALUES (NEW.project_id, NEW.id, NEW.created_by, 'created', 'Task created: ' || NEW.title);
    ELSIF TG_OP = 'UPDATE' THEN
        IF OLD.status != NEW.status THEN
            INSERT INTO activity_feed (project_id, task_id, user_id, action, summary, details)
            VALUES (NEW.project_id, NEW.id, NULL, 'status_changed',
                    'Status: ' || OLD.status || ' → ' || NEW.status,
                    jsonb_build_object('old_status', OLD.status, 'new_status', NEW.status));
        END IF;
        IF OLD.assigned_to IS DISTINCT FROM NEW.assigned_to THEN
            INSERT INTO activity_feed (project_id, task_id, user_id, action, summary)
            VALUES (NEW.project_id, NEW.id, NULL, 'assigned', 'Assignee changed for: ' || NEW.title);
        END IF;
    ELSIF TG_OP = 'DELETE' THEN
        INSERT INTO activity_feed (project_id, task_id, action, summary)
        VALUES (OLD.project_id, NULL, 'deleted', 'Task deleted: ' || OLD.title);
    END IF;
    RETURN COALESCE(NEW, OLD);
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_task_activity ON tasks;
CREATE TRIGGER trg_task_activity AFTER INSERT OR UPDATE OR DELETE ON tasks
    FOR EACH ROW EXECUTE FUNCTION log_task_activity();
