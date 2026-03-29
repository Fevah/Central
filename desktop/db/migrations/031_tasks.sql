-- 031_tasks.sql — Task management module tables

CREATE TABLE IF NOT EXISTS tasks (
    id              serial PRIMARY KEY,
    parent_id       integer REFERENCES tasks(id) ON DELETE CASCADE,
    title           varchar(256) NOT NULL,
    description     text DEFAULT '',
    status          varchar(32) DEFAULT 'Open',
    priority        varchar(16) DEFAULT 'Medium',
    task_type       varchar(32) DEFAULT 'Task',
    assigned_to     integer REFERENCES app_users(id) ON DELETE SET NULL,
    created_by      integer REFERENCES app_users(id) ON DELETE SET NULL,
    building        varchar(64),
    due_date        date,
    estimated_hours numeric(6,1),
    actual_hours    numeric(6,1),
    tags            text DEFAULT '',
    sort_order      integer DEFAULT 0,
    created_at      timestamptz DEFAULT now(),
    updated_at      timestamptz DEFAULT now(),
    completed_at    timestamptz
);

CREATE TABLE IF NOT EXISTS task_comments (
    id              serial PRIMARY KEY,
    task_id         integer NOT NULL REFERENCES tasks(id) ON DELETE CASCADE,
    user_id         integer REFERENCES app_users(id) ON DELETE SET NULL,
    comment_text    text NOT NULL,
    created_at      timestamptz DEFAULT now()
);

CREATE TABLE IF NOT EXISTS task_attachments (
    id              serial PRIMARY KEY,
    task_id         integer NOT NULL REFERENCES tasks(id) ON DELETE CASCADE,
    attachment_type varchar(32) NOT NULL,
    reference_id    varchar(128),
    reference_name  varchar(256),
    created_at      timestamptz DEFAULT now()
);

-- Indexes
CREATE INDEX IF NOT EXISTS idx_tasks_parent ON tasks (parent_id);
CREATE INDEX IF NOT EXISTS idx_tasks_status ON tasks (status);
CREATE INDEX IF NOT EXISTS idx_tasks_assigned ON tasks (assigned_to);
CREATE INDEX IF NOT EXISTS idx_tasks_building ON tasks (building);
CREATE INDEX IF NOT EXISTS idx_task_comments_task ON task_comments (task_id);
CREATE INDEX IF NOT EXISTS idx_task_attachments_task ON task_attachments (task_id);

-- pg_notify trigger for real-time
DROP TRIGGER IF EXISTS trg_notify_tasks ON tasks;
CREATE TRIGGER trg_notify_tasks AFTER INSERT OR UPDATE OR DELETE ON tasks
    FOR EACH ROW EXECUTE FUNCTION notify_data_change();

-- Seed sample data
INSERT INTO tasks (title, status, priority, task_type, description, building, created_by) VALUES
    ('Network Refresh MEP-91', 'InProgress', 'High', 'Epic', 'Full network refresh for Building 91', 'MEP-91', 1),
    ('Replace core switches', 'Open', 'High', 'Story', 'Replace MEP-91-Core01 and Core02 with new FS N8560', 'MEP-91', 1),
    ('Configure BGP peering', 'Open', 'Medium', 'Task', 'Set up eBGP between MEP-91 and MEP-92', 'MEP-91', 1),
    ('Verify VLAN trunks', 'Open', 'Medium', 'Task', 'Test all trunk ports carry VLANs 101-254', 'MEP-91', 1),
    ('Update documentation', 'Open', 'Low', 'Task', 'Update network diagrams and IPAM records', 'MEP-91', 1)
ON CONFLICT DO NOTHING;

-- Set parent_id for hierarchy
UPDATE tasks SET parent_id = (SELECT id FROM tasks WHERE title = 'Network Refresh MEP-91' LIMIT 1)
WHERE title IN ('Replace core switches', 'Configure BGP peering', 'Verify VLAN trunks', 'Update documentation')
AND parent_id IS NULL;
