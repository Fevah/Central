-- 062_kanban_board.sql — Phase 3: Kanban Board
-- Adds: board_columns (configurable per project), board_lanes

CREATE TABLE IF NOT EXISTS board_columns (
    id              serial PRIMARY KEY,
    project_id      integer NOT NULL REFERENCES task_projects(id) ON DELETE CASCADE,
    board_name      varchar(128) DEFAULT 'Default',
    column_name     varchar(64) NOT NULL,
    status_mapping  varchar(32),
    sort_order      integer DEFAULT 0,
    wip_limit       integer,
    color           varchar(16),
    UNIQUE(project_id, board_name, column_name)
);

CREATE TABLE IF NOT EXISTS board_lanes (
    id              serial PRIMARY KEY,
    project_id      integer NOT NULL REFERENCES task_projects(id) ON DELETE CASCADE,
    board_name      varchar(128) DEFAULT 'Default',
    lane_name       varchar(64) NOT NULL,
    lane_field      varchar(64),
    sort_order      integer DEFAULT 0
);

CREATE INDEX IF NOT EXISTS idx_board_columns_project ON board_columns (project_id, board_name, sort_order);
CREATE INDEX IF NOT EXISTS idx_board_lanes_project ON board_lanes (project_id, board_name, sort_order);

-- pg_notify
DROP TRIGGER IF EXISTS trg_notify_board_columns ON board_columns;
CREATE TRIGGER trg_notify_board_columns AFTER INSERT OR UPDATE OR DELETE ON board_columns
    FOR EACH ROW EXECUTE FUNCTION notify_data_change();

DROP TRIGGER IF EXISTS trg_notify_board_lanes ON board_lanes;
CREATE TRIGGER trg_notify_board_lanes AFTER INSERT OR UPDATE OR DELETE ON board_lanes
    FOR EACH ROW EXECUTE FUNCTION notify_data_change();

-- Seed default columns for the default project
INSERT INTO board_columns (project_id, board_name, column_name, status_mapping, sort_order, wip_limit)
SELECT tp.id, 'Default', col.name, col.status, col.ord, col.wip
FROM task_projects tp
CROSS JOIN (VALUES
    ('Backlog',     'Open',       0, NULL),
    ('To Do',       'Open',       1, NULL),
    ('In Progress', 'InProgress', 2, 5),
    ('Review',      'Review',     3, 3),
    ('Done',        'Done',       4, NULL)
) AS col(name, status, ord, wip)
WHERE tp.name = 'Default Project'
ON CONFLICT DO NOTHING;
