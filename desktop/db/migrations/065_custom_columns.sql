-- 065_custom_columns.sql — Phase 7: Custom Columns & Field Permissions

CREATE TABLE IF NOT EXISTS custom_columns (
    id              serial PRIMARY KEY,
    project_id      integer NOT NULL REFERENCES task_projects(id) ON DELETE CASCADE,
    name            varchar(64) NOT NULL,
    column_type     varchar(32) NOT NULL,  -- Text|RichText|Number|Hours|DropList|Date|DateTime|People|Computed
    config          jsonb,                  -- DropList options, Computed formula, aggregation type
    sort_order      integer DEFAULT 0,
    default_value   text,
    is_required     boolean DEFAULT false,
    UNIQUE(project_id, name)
);

CREATE TABLE IF NOT EXISTS custom_column_permissions (
    id              serial PRIMARY KEY,
    column_id       integer NOT NULL REFERENCES custom_columns(id) ON DELETE CASCADE,
    user_id         integer REFERENCES app_users(id) ON DELETE CASCADE,
    group_name      varchar(64),
    can_view        boolean DEFAULT true,
    can_edit        boolean DEFAULT true
);

CREATE TABLE IF NOT EXISTS task_custom_values (
    task_id         integer NOT NULL REFERENCES tasks(id) ON DELETE CASCADE,
    column_id       integer NOT NULL REFERENCES custom_columns(id) ON DELETE CASCADE,
    value_text      text,
    value_number    numeric(12,4),
    value_date      timestamptz,
    value_json      jsonb,
    PRIMARY KEY(task_id, column_id)
);

CREATE INDEX IF NOT EXISTS idx_custom_cols_project ON custom_columns (project_id, sort_order);
CREATE INDEX IF NOT EXISTS idx_custom_perms_column ON custom_column_permissions (column_id);
CREATE INDEX IF NOT EXISTS idx_custom_values_task ON task_custom_values (task_id);

-- pg_notify
DROP TRIGGER IF EXISTS trg_notify_custom_columns ON custom_columns;
CREATE TRIGGER trg_notify_custom_columns AFTER INSERT OR UPDATE OR DELETE ON custom_columns
    FOR EACH ROW EXECUTE FUNCTION notify_data_change();

DROP TRIGGER IF EXISTS trg_notify_custom_values ON task_custom_values;
CREATE TRIGGER trg_notify_custom_values AFTER INSERT OR UPDATE OR DELETE ON task_custom_values
    FOR EACH ROW EXECUTE FUNCTION notify_data_change();
